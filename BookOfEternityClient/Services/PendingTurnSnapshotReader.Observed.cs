using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class PendingTurnSnapshotReader
{
    private static readonly object OriginalReadIssuer = new();

    internal static PendingTurnSnapshotObservedReadResult ReadObservedCurrent(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyCollection<string> requiredLogicalPaths)
    {
        var session = new OriginalSourceReadSession(fs, lease);
        var result = ReadCurrentCore(fs, lease, requiredLogicalPaths, session);
        return new PendingTurnSnapshotObservedReadResult(
            result.Success, result.Snapshot,
            result.Success ? session.Issue(result.Snapshot!) : null, result.Issues);
    }

    internal sealed class OriginalReadEvidence
    {
        private readonly FrozenOriginalSource _source;
        private readonly PendingTurnSnapshotReadAuthority _snapshot;

        internal OriginalReadEvidence(object issuer, object frozenSource, PendingTurnSnapshotReadAuthority snapshot)
        {
            if (!ReferenceEquals(issuer, OriginalReadIssuer))
                throw new InvalidOperationException("Only the actual original reader can issue source evidence.");
            _source = frozenSource as FrozenOriginalSource
                ?? throw new InvalidOperationException("Original source closure is unavailable.");
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>
        /// Revalidates the exact issued owner identity and all captured original source observations.
        /// </summary>
        /// <param name="fs">
        /// Manager retained by the original reader.
        /// </param>
        /// <param name="lease">
        /// Exact original physical lease, which source readers must still accept.
        /// </param>
        /// <param name="issuedSnapshot">
        /// The exact snapshot issued together with this evidence.
        /// </param>
        internal void RequireCurrent(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority issuedSnapshot)
        {
            RequireOwner(fs, lease, issuedSnapshot);
            var issues = new List<ValidationIssue>();
            if (!_source.Recheck(issues))
                throw new InvalidDataException(string.Join("; ", issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        }

        /// <summary>
        /// Checks exact evidence identity without replacing its separate physical source recheck.
        /// </summary>
        /// <param name="fs">
        /// Manager retained by this evidence; a different instance is refused.
        /// </param>
        /// <param name="lease">
        /// Exact lease retained by this evidence, never a reacquired replacement.
        /// </param>
        /// <param name="issuedSnapshot">
        /// Exact issued snapshot identity; detached data cannot replace it.
        /// </param>
        internal void RequireOwner(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority issuedSnapshot)
        {
            if (!ReferenceEquals(fs, _source.Files) || !ReferenceEquals(lease, _source.Lease) ||
                !ReferenceEquals(issuedSnapshot, _snapshot))
                throw new InvalidOperationException("Original evidence requires its exact owner, lease and issued snapshot.");
        }

        // Every export is detached data. It cannot mint/rebind evidence after the lease ends.
        internal JsonObject ExportDiagnosticData()
        {
            var operations = new JsonArray();
            foreach (var operation in _source.Operations)
            {
                var row = new JsonObject
                {
                    ["ordinal"] = operation.Ordinal,
                    ["role"] = operation.Role.ToString(),
                    ["path"] = operation.Path,
                    ["logicalPath"] = operation.LogicalPath,
                    ["isRepairRequest"] = operation.IsRepairRequest
                };
                if (operation.IsPresence) row["present"] = operation.Present;
                else
                {
                    row["bytesBase64"] = operation.Bytes is null ? null : Convert.ToBase64String(operation.Bytes);
                    row["rawHash"] = operation.Bytes is null ? null : PendingTurnSnapshotAuthority.ComputeSha256(operation.Bytes);
                }
                operations.Add(row);
            }
            return new JsonObject
            {
                ["generation"] = _source.GenerationId,
                ["generationBytesBase64"] = Convert.ToBase64String(_source.GenerationBytes),
                ["manifestRawHash"] = _source.RawHash(OriginalSourceRole.Manifest),
                ["authorityRawHash"] = _source.RawHash(OriginalSourceRole.DetachedAuthority),
                ["sessionId"] = _snapshot.SessionId,
                ["requestId"] = _snapshot.RequestId,
                ["snapshotToken"] = _snapshot.SnapshotToken,
                ["turnNumber"] = _snapshot.TurnNumber,
                ["realm"] = _snapshot.Realm,
                ["selectedLogicalPaths"] = Strings(_snapshot.CoveredLogicalPaths),
                ["absentLogicalPaths"] = Strings(_snapshot.AbsentLogicalPaths),
                ["declaredOriginalLogicalPaths"] = Strings(_snapshot.DeclaredOriginalLogicalPaths),
                ["operations"] = operations
            };
        }

        private static JsonArray Strings(IEnumerable<string> values) =>
            new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    }

    private enum OriginalSourceRole { Manifest, DetachedAuthority, RollbackArtifact, ContextPresence, ContextText, SelectedOriginal }
    private enum OriginalSourceState { Capturing, Rechecking, Issued, Failed }

    private sealed class OriginalSourceOperation
    {
        internal readonly int Ordinal;
        internal readonly OriginalSourceRole Role;
        internal readonly string Path;
        internal readonly string? LogicalPath;
        internal readonly bool? IsRepairRequest;
        internal readonly bool Present;
        internal readonly byte[]? Bytes;
        internal bool IsPresence => Role == OriginalSourceRole.ContextPresence;
        internal string DiagnosticPath => LogicalPath ?? Path;

        internal OriginalSourceOperation(int ordinal, OriginalSourceRole role, string path,
            string? logicalPath, bool? isRepairRequest, bool present, byte[]? bytes)
        {
            Ordinal = ordinal; Role = role; Path = path; LogicalPath = logicalPath;
            IsRepairRequest = isRepairRequest; Present = present; Bytes = bytes?.ToArray();
        }

        internal OriginalSourceOperation Copy() => new(Ordinal, Role, Path, LogicalPath, IsRepairRequest, Present, Bytes);
        internal bool SameValue(OriginalSourceOperation other) => IsPresence == other.IsPresence &&
            (IsPresence ? Present == other.Present : EqualBytes(Bytes, other.Bytes));
    }

    private sealed class OriginalSourceReadSession(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        private OriginalSourceState _state = OriginalSourceState.Capturing;
        private readonly List<OriginalSourceOperation> _operations = new();
        private readonly Dictionary<string, OriginalSourceOperation> _first = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private OriginalSourceOperation? _drift;
        private Exception? _fault;
        private LocalSessionGenerationSnapshot? _generation;
        private FrozenOriginalSource? _closed;
        private LiveTurnPendingSnapshotManifest? _rollbackManifest;
        private IReadOnlyList<KeyValuePair<string, string>>? _rollbackRows;
        private int _rollbackOrdinal;

        internal bool Begin(List<ValidationIssue> issues)
        {
            EnsureCapturing();
            files.EnsureCanonicalWriteLeaseActive(lease);
            lease.EnsureNoPendingLocalDecision();
            var generation = files.ReadLocalGenerationSnapshot(lease);
            files.EnsureCanonicalWriteLeaseActive(lease);
            lease.EnsureNoPendingLocalDecision();
            if (generation.Binding.Id is null || generation.Bytes is null)
            {
                _state = OriginalSourceState.Failed;
                Add(issues, files.SessionGenerationPath, "pending_turn_snapshot_reader_source_generation_unavailable",
                    "the observed reader requires an existing session generation");
                return false;
            }
            _generation = new(generation.Binding, generation.Bytes.ToArray());
            return true;
        }

        internal string? ReadManifestText() => Text(CaptureBytes(OriginalSourceRole.Manifest,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        internal string? ReadDetachedAuthorityText() => Text(CaptureBytes(OriginalSourceRole.DetachedAuthority,
            PendingTurnSnapshotAuthority.AuthorityPath));

        internal byte[]? ReadRollbackBytesFromAuthorityCallback(LiveTurnPendingSnapshotManifest manifest, string path)
        {
            EnsureCapturing();
            // This is lazy: the existing checker has already accepted its structure/hash/envelope.
            // Preserve its normalized map iteration, including every alias read of a shared artifact.
            _rollbackRows ??= PendingTurnSnapshotAuthority.CopyNormalizedRollbackCoordinates(manifest.RollbackBackups);
            _rollbackManifest ??= manifest;
            if (!ReferenceEquals(_rollbackManifest, manifest) || _rollbackOrdinal >= _rollbackRows.Count ||
                !string.Equals(path, _rollbackRows[_rollbackOrdinal].Value, StringComparison.Ordinal))
            {
                var fault = new InvalidOperationException("Unsupported original rollback source callback.");
                _fault ??= fault;
                throw fault;
            }
            var row = _rollbackRows[_rollbackOrdinal++];
            return CaptureBytes(OriginalSourceRole.RollbackArtifact, path, row.Key);
        }

        internal bool ProbeCurrentContext(PendingTurnSnapshotContextSource context)
        {
            RequireContext(context);
            EnsureCapturing();
            try
            {
                var present = files.FileExists(lease, context.Path);
                files.EnsureCanonicalWriteLeaseActive(lease);
                Append(new(_operations.Count, OriginalSourceRole.ContextPresence, context.Path, null,
                    context.IsRepairRequest, present, null));
                return present;
            }
            catch (Exception error) { _fault ??= error; throw; }
        }

        internal string? ReadCurrentContextText(PendingTurnSnapshotContextSource context)
        {
            RequireContext(context);
            return Text(CaptureBytes(OriginalSourceRole.ContextText, context.Path, isRepairRequest: context.IsRepairRequest));
        }

        internal byte[]? ReadSelectedOriginalBytes(string logicalPath, string physicalPath) =>
            CaptureBytes(OriginalSourceRole.SelectedOriginal, physicalPath, logicalPath);

        private byte[]? CaptureBytes(OriginalSourceRole role, string path, string? logicalPath = null,
            bool? isRepairRequest = null)
        {
            EnsureCapturing();
            try
            {
                var bytes = files.ReadFileBytesSync(lease, path);
                Append(new(_operations.Count, role, path, logicalPath, isRepairRequest, false, bytes));
                return bytes?.ToArray();
            }
            catch (Exception error) { _fault ??= error; throw; }
        }

        private void Append(OriginalSourceOperation operation)
        {
            // Pure path identity only. ResolvePath would perform additional, unrecorded IO here.
            var coordinate = Path.GetFullPath(Path.Combine(files.GameSessionPath,
                operation.Path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));
            var key = (operation.IsPresence ? "presence:" : "bytes:") + coordinate;
            if (_first.TryGetValue(key, out var first))
            {
                if (!first.SameValue(operation)) _drift ??= operation;
            }
            else _first.Add(key, operation);
            _operations.Add(operation);
        }

        internal bool CloseAndRecheck(List<ValidationIssue> issues)
        {
            EnsureCapturing();
            _state = OriginalSourceState.Rechecking;
            if (_fault is not null || _generation?.Bytes is null)
            {
                Add(issues, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "pending_turn_snapshot_reader_source_unavailable", "the original source session has a latched fault");
                _state = OriginalSourceState.Failed;
                return false;
            }
            if (_drift is not null)
            {
                SourceChanged(issues, _drift);
                _state = OriginalSourceState.Failed;
                return false;
            }
            var closure = new FrozenOriginalSource(files, lease, _generation, _operations);
            try
            {
                if (!closure.Recheck(issues)) { _state = OriginalSourceState.Failed; return false; }
                _closed = closure;
                return true;
            }
            catch (Exception error) { _fault ??= error; _state = OriginalSourceState.Failed; throw; }
        }

        internal OriginalReadEvidence Issue(PendingTurnSnapshotReadAuthority snapshot)
        {
            if (_state != OriginalSourceState.Rechecking || _closed is null)
                throw new InvalidOperationException("Original source closure was not checked before issuance.");
            _state = OriginalSourceState.Issued;
            return new(OriginalReadIssuer, _closed, snapshot);
        }

        private void EnsureCapturing()
        {
            if (_state != OriginalSourceState.Capturing)
                throw new InvalidOperationException("The original source session is closed.");
        }
        private static void RequireContext(PendingTurnSnapshotContextSource context)
        {
            if (!CurrentContextSources.Any(source => ReferenceEquals(source, context)))
                throw new InvalidOperationException("Unsupported original lifecycle source.");
        }
        private static string? Text(byte[]? bytes) => bytes is null ? null : FileSystemManager.DecodeFileBytes(bytes);
    }

    // Evidence retains this frozen closure, never the mutable capture session.
    private sealed class FrozenOriginalSource
    {
        internal readonly FileSystemManager Files;
        internal readonly FileSystemManager.CanonicalWriteLease Lease;
        internal readonly string GenerationId;
        internal readonly byte[] GenerationBytes;
        internal readonly IReadOnlyList<OriginalSourceOperation> Operations;

        internal FrozenOriginalSource(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            LocalSessionGenerationSnapshot generation, IEnumerable<OriginalSourceOperation> operations)
        {
            Files = files; Lease = lease;
            GenerationId = generation.Binding.Id!;
            GenerationBytes = generation.Bytes!.ToArray();
            Operations = Array.AsReadOnly(operations.Select(operation => operation.Copy()).ToArray());
        }

        internal bool Recheck(List<ValidationIssue> issues)
        {
            if (!CheckGeneration(issues)) return false;
            foreach (var operation in Operations)
            {
                bool same;
                if (operation.IsPresence)
                {
                    var present = Files.FileExists(Lease, operation.Path);
                    Files.EnsureCanonicalWriteLeaseActive(Lease);
                    same = operation.Present == present;
                }
                else same = EqualBytes(operation.Bytes, Files.ReadFileBytesSync(Lease, operation.Path));
                if (!same) { SourceChanged(issues, operation); return false; }
            }
            return CheckGeneration(issues);
        }

        private bool CheckGeneration(List<ValidationIssue> issues)
        {
            Files.EnsureCanonicalWriteLeaseActive(Lease);
            Lease.EnsureNoPendingLocalDecision();
            var current = Files.ReadLocalGenerationSnapshot(Lease);
            Files.EnsureCanonicalWriteLeaseActive(Lease);
            Lease.EnsureNoPendingLocalDecision();
            if (string.Equals(current.Binding.Id, GenerationId, StringComparison.Ordinal) &&
                EqualBytes(current.Bytes, GenerationBytes)) return true;
            Add(issues, Files.SessionGenerationPath, "pending_turn_snapshot_reader_source_generation_changed",
                "the existing session generation changed during original source observation");
            return false;
        }

        internal string? RawHash(OriginalSourceRole role)
        {
            var bytes = Operations.FirstOrDefault(operation => operation.Role == role)?.Bytes;
            return bytes is null ? null : PendingTurnSnapshotAuthority.ComputeSha256(bytes);
        }
    }

    private static bool EqualBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private static void SourceChanged(List<ValidationIssue> issues, OriginalSourceOperation operation) =>
        Add(issues, operation.DiagnosticPath, "pending_turn_snapshot_reader_source_changed",
            $"{operation.Role} source changed at observation {operation.Ordinal}");
}

internal sealed class PendingTurnSnapshotObservedReadResult
{
    internal bool Success { get; }
    internal PendingTurnSnapshotReadAuthority? Snapshot { get; }
    internal PendingTurnSnapshotReader.OriginalReadEvidence? Evidence { get; }
    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal PendingTurnSnapshotObservedReadResult(bool success, PendingTurnSnapshotReadAuthority? snapshot,
        PendingTurnSnapshotReader.OriginalReadEvidence? evidence, IEnumerable<ValidationIssue> issues)
    {
        if (success != (snapshot is not null && evidence is not null) ||
            !success && (snapshot is not null || evidence is not null))
            throw new InvalidOperationException("Observed success requires both issued snapshot and evidence.");
        Success = success; Snapshot = snapshot; Evidence = evidence;
        Issues = Array.AsReadOnly(issues.ToArray());
    }
}
