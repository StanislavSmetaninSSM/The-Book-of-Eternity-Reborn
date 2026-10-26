using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly object DebugPrefixIssuer = new();

    /// <summary>Captures complete converted manifest DATA for comparison, never original or publication authority.</summary>
    internal string CaptureAcceptedTurnReasoningOriginalData(object manifest)
    {
        var converted = ConvertToValidationPendingTurnSnapshotManifest(manifest)
            ?? throw new InvalidDataException("debug_prefix_actual_original_missing");
        return JsonSerializer.Serialize(converted, ManifestHashJsonOpts);
    }

    /// <summary>Reads the fixed debug original through the ordinary nullable policy; the text is DATA only.</summary>
    internal Task<string?> ReadAcceptedTurnReasoningOriginalDebugAsync() =>
        ReadValidatedCurrentPreTurnTrackedFileCoreAsync("output/debug_logs.json", null, null);

    /// <summary>Evaluates the shared prefix once under a borrowed physical lease; the result grants no engine authority.</summary>
    internal async Task<DebugPrefixPass> EvaluateObservedDebugPrefixAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        using var capture = _fs.MeasureOperation("ReasoningPrefix.Capture");
        var source = new DebugPrefixSourceSession(this, _fs, lease);
        try
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            var result = await EvaluateAcceptedReasoningPrefixAsync(source);
            source.Complete();
            return DebugPrefixPass.Issue(DebugPrefixIssuer, this, source, result.Issues,
                result.Terminal ? "TerminalPrefix" : "NeedsActorDependencies");
        }
        catch { source.Dispose(); throw; }
    }

    /// <summary>Disposable actual invocation DATA. Only the engine can privately associate its own returned result.</summary>
    internal sealed class DebugPrefixPass : IDisposable
    {
        private readonly ValidationService _validator;
        private readonly DebugPrefixSourceSession _source;
        private ValidationIssue[] _issues;
        private bool _disposed;
        private DebugPrefixPass(ValidationService validator, DebugPrefixSourceSession source,
            IReadOnlyList<ValidationIssue> issues, string disposition)
        { _validator = validator; _source = source; _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(); Disposition = disposition; }
        internal static DebugPrefixPass Issue(object issuer, ValidationService validator, DebugPrefixSourceSession source,
            IReadOnlyList<ValidationIssue> issues, string disposition)
        {
            if (!ReferenceEquals(issuer, DebugPrefixIssuer)) throw new InvalidOperationException("Actual debug prefix issuer required.");
            return new(validator, source, issues, disposition);
        }
        internal string Disposition { get; }
        internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());
        internal void RequireIssuer(ValidationService validator)
        {
            if (_disposed || !ReferenceEquals(validator, _validator)) throw new InvalidDataException("debug_prefix_expired_or_foreign_validator");
        }
        internal void RequireOriginalContext(object actualManifest)
        {
            RequireIssuer(_validator);
            var manifest = ConvertToValidationPendingTurnSnapshotManifest(actualManifest)
                ?? throw new InvalidDataException("debug_prefix_actual_original_missing");
            _source.JoinOriginalContext(manifest.SessionId, manifest.RequestId, manifest.ManifestPayloadHash,
                manifest.TurnNumber, _validator.CaptureAcceptedTurnReasoningOriginalData(manifest));
        }
        internal Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            RequireIssuer(_validator); return _source.RequireCurrentAsync(files, lease);
        }
        internal DebugPrefixDiagnostic CaptureDiagnostic() => _source.CaptureDiagnostic();
        public void Dispose() { if (_disposed) return; _disposed = true; _issues = []; _source.Dispose(); }
    }

    /// <summary>Copied physical or policy occurrence; it never grants source or publication authority.</summary>
    internal sealed record DebugPrefixOccurrence(long Ordinal, string Path, string Route, string State, byte[]? Bytes);
    /// <summary>Copied observations and actual policy-call counts for one invocation.</summary>
    internal sealed record DebugPrefixDiagnostic(bool Completed, bool Disposed, string? Fault, int TimestampCalls,
        ValidationIssue[] TimestampIssues, DebugPrefixOccurrence[] Occurrences);

    internal sealed class DebugPrefixSourceSession : IDisposable
    {
        private const string Debug = "output/debug_logs.json";
        private readonly ValidationService _validator;
        private readonly FileSystemManager _files;
        private readonly string _generation;
        private readonly byte[] _generationBytes;
        private readonly FileSystemManager.MainAdmission? _main;
        private readonly List<DebugPrefixOccurrence> _occurrences = [];
        private readonly List<(string Session, string Request, string Token, int Turn)> _contexts = [];
        private readonly List<ValidationIssue> _timestampIssues = [];
        private string? _fault, _manifestData;
        private bool _completed, _disposed, _needsOriginalJoin, _originalJoined;
        private int _timestampCalls;
        private long _ordinal;
        internal FileSystemManager.CanonicalWriteLease Lease { get; }
        internal DebugPrefixSourceSession(ValidationService validator, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            _validator = validator; _files = files; Lease = lease;
            files.EnsureCanonicalWriteLeaseActive(lease);
            using var fresh = FileSystemManager.SuppressValidationReuse();
            var generation = files.ReadLocalGenerationSnapshot(lease);
            _generation = generation.Binding.Id ?? throw new InvalidDataException("debug_prefix_missing_generation");
            _generationBytes = generation.Bytes?.ToArray() ?? throw new InvalidDataException("debug_prefix_missing_physical_generation"); _main = lease.MainAdmission;
        }
        internal Task<string?> ReadOriginalDebugAsync() => _validator.ReadValidatedCurrentPreTurnTrackedFileCoreAsync(Debug, Lease, this);
        internal Task<string?> ReadDebugForFreshnessAsync() => ReadAuxiliaryTextAsync(Debug, Debug, "freshness-current", Lease);
        internal Task<string?> ReadDebugForParseAsync() => ReadAuxiliaryTextAsync(Debug, Debug, "parse-current", Lease);
        internal DebugPrefixReadObservation BeginRead(string role, string path, string route)
        {
            if (role != Debug || !PendingTurnSnapshotAuthority.IsSafeRelativePath(path))
                throw new InvalidDataException("debug_prefix_unsupported_source_role");
            return new(this, path, route);
        }
        internal async Task<string?> ReadAuxiliaryTextAsync(string role, string path, string route, FileSystemManager.CanonicalWriteLease? lease)
        {
            try
            {
                RequireLease(_files, lease);
                return await _files.ReadDebugPrefixTextAsync(Lease, path, BeginRead(role, path, route));
            }
            catch (Exception error) { Fault(error); throw; }
        }
        internal async Task<byte[]?> ReadAuxiliaryBytesAsync(string role, string path, string route, FileSystemManager.CanonicalWriteLease? lease)
        {
            try
            {
                RequireLease(_files, lease);
                return await _files.ReadDebugPrefixBytesAsync(Lease, path, BeginRead(role, path, route));
            }
            catch (Exception error) { Fault(error); throw; }
        }
        internal void Policy(string role, string route, string state)
        {
            if (role != Debug) { Unavailable("debug_prefix_unsupported_policy_role"); return; }
            Add(Debug, route, state, null);
        }
        internal void OriginalContext(string session, string request, string token, int turn, string provenance, string data)
        {
            if (provenance != "PhysicalLookupData") RequireOriginalJoin();
            if (_manifestData is not null && _manifestData != data) Unavailable("debug_prefix_original_data_changed");
            _manifestData ??= data; _contexts.Add((session, request, token, turn));
            Policy(Debug, "original-context", provenance);
        }
        internal void RequireOriginalJoin() => _needsOriginalJoin = true;
        internal void JoinOriginalContext(string session, string request, string token, int turn, string data)
        {
            RequireCompleted();
            if (_contexts.Count == 0 || _contexts.Any(value => value != (session, request, token, turn)) || _manifestData != data)
                throw Refuse("debug_prefix_original_context_or_data_changed");
            _originalJoined = true;
        }
        internal void BeginTimestampPolicy(JsonElement root)
        {
            _timestampCalls++;
            Policy(Debug, "timestamp-context", System.Globalization.CultureInfo.CurrentCulture.Name + "|" + TimeZoneInfo.Local.Id);
            Policy(Debug, "timestamp-input", root.TryGetProperty("timestamp", out var value) ? value.GetRawText() : "Absent");
        }
        internal void EndTimestampPolicy(IEnumerable<ValidationIssue> issues) => _timestampIssues.AddRange(issues.Select(WoundAcceptedTurnData.CloneIssue));
        internal void Complete()
        {
            if (!_occurrences.Any(value => value.Path == Debug && value.Route == "parse-current" && (value.State is "Present" or "Absent")))
                Unavailable("debug_prefix_missing_parse_read");
            if (!_disposed) _completed = true;
        }
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var timing = files.MeasureOperation("ReasoningPrefix.Recheck");
            try
            {
                RequireUsable(); RequireLease(files, lease);
                using var fresh = FileSystemManager.SuppressValidationReuse();
                var generation = files.ReadLocalGenerationSnapshot(lease);
                if (generation.Binding.Id != _generation || generation.Bytes is null || !generation.Bytes.AsSpan().SequenceEqual(_generationBytes))
                    throw Refuse("debug_prefix_generation_changed");
                var checkedPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var occurrence in _occurrences)
                {
                    if (occurrence.Route == "lookup-presence")
                    {
                        if (files.FileExists(lease, occurrence.Path) != (occurrence.State == "Exists"))
                            throw Refuse("debug_prefix_presence_changed: " + occurrence.Path);
                    }
                    else if ((occurrence.State is "Present" or "Absent") && checkedPaths.Add(occurrence.Path))
                    {
                        var current = await files.ReadFileBytesAsync(lease, occurrence.Path);
                        if (!Same(current, occurrence.Bytes)) throw Refuse("debug_prefix_source_changed: " + occurrence.Path);
                    }
                }
                RequireUsable(); RequireLease(files, lease);
            }
            catch (Exception error) { Fault(error); throw; }
        }
        private void RequireLease(FileSystemManager files, FileSystemManager.CanonicalWriteLease? lease)
        {
            if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, Lease)) throw new InvalidDataException("debug_prefix_foreign_files_or_lease");
            files.EnsureCanonicalWriteLeaseActive(Lease);
            if (!files.IsCurrentSessionGeneration(Lease, _generation)) throw new InvalidDataException("debug_prefix_generation_changed");
            if (_main is not null && (!ReferenceEquals(Lease.MainAdmission, _main) || _main.Closed))
                throw new InvalidDataException("debug_prefix_main_changed");
        }
        private void RequireCompleted()
        {
            if (_disposed || !_completed || _fault is not null) throw new InvalidDataException("debug_prefix_unavailable: " + (_fault ?? (_disposed ? "expired" : "incomplete")));
        }
        private void RequireUsable()
        {
            RequireCompleted();
            if (_needsOriginalJoin && !_originalJoined) throw new InvalidDataException("debug_prefix_original_join_required");
        }
        private InvalidDataException Refuse(string reason) { Unavailable(reason); return new(reason); }
        internal void Fault(Exception error) => _fault ??= error.GetType().Name + ": " + error.Message;
        internal void Unavailable(string reason) => _fault ??= reason;
        private void Add(string path, string route, string state, byte[]? bytes)
        {
            if (_completed || _disposed) { Unavailable("debug_prefix_observation_after_close"); return; }
            if (state is "Present" or "Absent")
            {
                var prior = _occurrences.LastOrDefault(value => value.Path == path && (value.State is "Present" or "Absent"));
                if (prior is not null && !Same(prior.Bytes, bytes)) Unavailable("debug_prefix_observed_source_changed: " + path);
            }
            _occurrences.Add(new(++_ordinal, path, route, state, bytes?.ToArray()));
        }
        private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
        internal DebugPrefixDiagnostic CaptureDiagnostic() => new(_completed, _disposed, _fault, _timestampCalls,
            _timestampIssues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(), _occurrences.Select(value => value with { Bytes = value.Bytes?.ToArray() }).ToArray());
        public void Dispose() { if (_disposed) return; _disposed = true; _occurrences.Clear(); _contexts.Clear(); _timestampIssues.Clear(); _manifestData = null; Array.Clear(_generationBytes); }
        internal sealed class DebugPrefixReadObservation(DebugPrefixSourceSession owner, string path, string route)
        {
            private readonly List<Exception> _faults = [];
            internal void Snapshot(byte[]? bytes, bool physical = true)
            {
                if (!physical) owner.Unavailable("debug_prefix_partial_source_provenance: " + path);
                owner.Add(path, route, bytes is null ? "Absent" : "Present", bytes);
            }
            internal void Presence(bool present) => owner.Add(path, route, present ? "Exists" : "Missing", null);
            internal void Fault(Exception error)
            {
                if (_faults.Any(prior => ReferenceEquals(prior, error))) return;
                _faults.Add(error); owner.Add(path, route, "SourceFault:" + error.GetType().Name, null); owner.Fault(error);
            }
        }
    }
}
