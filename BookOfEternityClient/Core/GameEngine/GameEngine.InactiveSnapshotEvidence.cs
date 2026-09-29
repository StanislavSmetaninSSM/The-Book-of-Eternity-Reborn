using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Retains an inactive snapshot as exact diagnostic evidence before retiring its fixed active paths.
    /// Metadata consistency classifies evidence only; it does not establish rollback or publication authority.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the complete verified cohort was removed from its active paths;
    /// <see langword="false"/> on refusal or incomplete removal.
    /// </returns>
    private async Task<bool> ArchiveInactivePendingSnapshotEvidenceAsync()
    {
        try
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var generation = _fs.ReadExistingSessionGeneration(lease);
            if (generation is null)
                return false;
            return await SessionOperationContext.RunBoundAsync(_fs, generation, lease, async () =>
            {
                if (!_fs.FileExists(lease, PendingTurnSnapshotManifestPath) || HasInactiveSnapshotEvidenceBlocker(lease))
                    return false;
                var originals = await ReadInactiveSnapshotEvidenceCohortAsync(lease);
                if (!originals.TryGetValue(PendingTurnSnapshotManifestPath, out var manifestBytes) ||
                    !originals.TryGetValue(PendingTurnSnapshotAuthority.AuthorityPath, out var authorityBytes))
                    return false;

                var manifestJson = ReadUnambiguousSnapshotEvidenceJson(manifestBytes);
                var authorityJson = ReadUnambiguousSnapshotEvidenceJson(authorityBytes);
                using var envelope = JsonDocument.Parse(authorityJson);
                if (!envelope.RootElement.TryGetProperty("payloadJsonBase64", out var encodedPayload) ||
                    encodedPayload.ValueKind != JsonValueKind.String)
                    return false;
                _ = ReadUnambiguousSnapshotEvidenceJson(Convert.FromBase64String(encodedPayload.GetString()!));
                if (!PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(authorityJson, out var detached) ||
                    detached is null || detached.Files is null || detached.SnapshotFileHashes is null ||
                    detached.ClientOwnedValidationHashes is null || detached.RollbackBackups is null ||
                    detached.RollbackBackupHashes is null || detached.RollbackBaselineFiles is null)
                    return false;
                var manifest = JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(manifestJson, JsonOpts);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.RequestId) ||
                    string.IsNullOrWhiteSpace(manifest.SessionId) || string.IsNullOrWhiteSpace(_gameLoop.SessionId) ||
                    !string.Equals(manifest.SessionId, _gameLoop.SessionId, StringComparison.Ordinal) ||
                    manifest.TurnNumber <= 0 || manifest.TurnNumber > _gameLoop.TurnNumber ||
                    !PendingTurnSnapshotAuthority.HasUsableManifestStructure(manifest,
                        static value => value.Files, static value => value.SnapshotFileHashes,
                        static value => value.ClientOwnedValidationHashes, static value => value.RollbackBackups,
                        static value => value.SourceLabel, static value => value.RollbackBaselineFiles) ||
                    !PendingTurnSnapshotAuthority.TryValidateManifestAgainstAuthority(
                        manifest, authorityJson, SnapshotHashJsonOpts,
                        static value => value.ManifestPayloadHash,
                        static (value, hash) => value.ManifestPayloadHash = hash,
                        static value => value.SessionId, static value => value.RequestId,
                        static value => value.TurnNumber, static value => value.Files,
                        static value => value.SnapshotFileHashes, static value => value.ClientOwnedValidationHashes,
                        static value => value.RollbackBaselineFiles, static value => value.SourceLabel,
                        out var payload, out _) || payload is null ||
                    manifest.RollbackBackups.Count != payload.RollbackBackups.Count ||
                    manifest.RollbackBackups.Any(pair => !payload.RollbackBackups.TryGetValue(pair.Key, out var backup) ||
                        !string.Equals(pair.Value, backup, StringComparison.Ordinal)))
                    return false;

                // No path from the manifest is opened. The physical fixed cohort is the entire evidence source.
                var entries = originals.Select((pair, ordinal) => new
                {
                    sourcePath = pair.Key, blob = $"{ordinal:D6}.bin",
                    sha256 = ComputeSha256(pair.Value), length = pair.Value.LongLength
                }).ToArray();
                var index = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    formatVersion = 1, purpose = "inactive-snapshot-diagnostic-only",
                    sessionId = manifest.SessionId, requestId = manifest.RequestId, turnNumber = manifest.TurnNumber,
                    entries
                });
                var archiveRoot = $"diagnostics/inactive-pending-turn-snapshots/{ComputeSha256(index)}";
                var copies = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [$"{archiveRoot}/index.json"] = index
                };
                foreach (var entry in entries)
                    copies.Add($"{archiveRoot}/{entry.blob}", originals[entry.sourcePath]);
                foreach (var copy in copies)
                {
                    var existing = await _fs.ReadFileBytesAsync(lease, copy.Key);
                    if (existing is null)
                        await _fs.WriteFileAtomicBytesAsync(lease, copy.Key, copy.Value);
                    else if (!existing.AsSpan().SequenceEqual(copy.Value))
                        return false;
                    var readback = await _fs.ReadFileBytesAsync(lease, copy.Key);
                    if (readback is null || !readback.AsSpan().SequenceEqual(copy.Value))
                        return false;
                }

                // Retain verified read handles denying writes/deletes until all source deletions finish.
                using var sessionRoot = PhysicalFileAuthority.OpenStableDirectory(_fs.GameSessionPath, "Snapshot evidence root");
                using var archiveDirectory = PhysicalFileAuthority.OpenExistingStableDirectory(
                    sessionRoot, Path.Combine(_fs.GameSessionPath, archiveRoot), "Snapshot evidence archive");
                var retainedCopies = new List<FileStream>();
                try
                {
                    foreach (var copy in copies)
                    {
                        var retained = PhysicalFileAuthority.OpenReadFile(archiveDirectory,
                            Path.Combine(_fs.GameSessionPath, copy.Key), "Snapshot evidence blob", asynchronous: true)
                            ?? throw new InvalidDataException("Snapshot evidence copy disappeared.");
                        retainedCopies.Add(retained);
                        using var bytes = new MemoryStream();
                        await retained.CopyToAsync(bytes);
                        if (!bytes.ToArray().AsSpan().SequenceEqual(copy.Value))
                            return false;
                    }
                    var current = await ReadInactiveSnapshotEvidenceCohortAsync(lease);
                    if (HasInactiveSnapshotEvidenceBlocker(lease) ||
                        current.Count != originals.Count || originals.Any(pair =>
                            !current.TryGetValue(pair.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(pair.Value)))
                        return false;
                    _fs.VerifyCurrentSessionOperation(lease);
                    // Keep the manifest until last so interrupted removal retains its identity plus the complete archive.
                    foreach (var pair in originals.OrderBy(pair => pair.Key == PendingTurnSnapshotManifestPath ? 2 :
                                 pair.Key == PendingTurnSnapshotAuthority.AuthorityPath ? 1 : 0))
                    {
                        await _fs.DeleteFileIfCurrentOwnedAsync(lease, pair.Key, new[] { ComputeSha256(pair.Value) });
                        if (_fs.FileExists(lease, pair.Key))
                            return false;
                    }
                    return true;
                }
                finally
                {
                    foreach (var retained in retainedCopies)
                        retained.Dispose();
                }
            });
        }
        catch (SessionReplacedException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or
                                     FormatException or DecoderFallbackException or InvalidOperationException)
        {
            _logger.LogWarning(error, "Inactive pending snapshot evidence could not be retired; retained evidence remains diagnostic only.");
            return false;
        }
    }

    /// <summary>
    /// Refuses evidence retirement while any current turn, repair or spiritual continuation control exists.
    /// </summary>
    /// <param name="lease">
    /// Current canonical lease protecting the physical control-path inspection.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if any control file still requires the active snapshot to remain in place;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool HasInactiveSnapshotEvidenceBlocker(FileSystemManager.CanonicalWriteLease lease) =>
        new[]
        {
            "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
            ValidationRepairRequestPath, ValidationRepairReadyPath, TerminalProtocolFailureRequestPath,
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath
        }.Any(path => _fs.FileExists(lease, path));

    /// <summary>
    /// Reads only the fixed metadata files and physically enumerated snapshot subtree, without following manifest paths.
    /// </summary>
    /// <param name="lease">
    /// Canonical lease covering inventory and exact byte reads.
    /// </param>
    /// <returns>
    /// Ordinally sorted session-relative paths and their detached exact physical bytes.
    /// </returns>
    private async Task<SortedDictionary<string, byte[]>> ReadInactiveSnapshotEvidenceCohortAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var paths = new List<string>();
        foreach (var path in new[] { PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
            if (_fs.FileExists(lease, path))
                paths.Add(path);
        using var sessionRoot = PhysicalFileAuthority.OpenStableDirectory(_fs.GameSessionPath, "Snapshot evidence inventory root");
        var pendingDirectories = new Stack<string>();
        if (_fs.DirectoryExists(lease, PendingTurnSnapshotDirectory))
            pendingDirectories.Push(PendingTurnSnapshotDirectory);
        while (pendingDirectories.TryPop(out var relativeDirectory))
        {
            using var directory = PhysicalFileAuthority.OpenExistingStableDirectory(sessionRoot,
                Path.Combine(_fs.GameSessionPath, relativeDirectory), "Snapshot evidence inventory directory");
            foreach (var child in Directory.EnumerateFileSystemEntries(directory.FullPath))
            {
                var relative = Path.GetRelativePath(_fs.GameSessionPath, child).Replace('\\', '/');
                switch (PhysicalFileAuthority.ProbeNamespaceEntry(directory, child, "Snapshot evidence inventory entry"))
                {
                    case PhysicalFileAuthority.NamespaceEntryKind.Directory:
                        pendingDirectories.Push(relative);
                        break;
                    case PhysicalFileAuthority.NamespaceEntryKind.RegularFile:
                        paths.Add(relative);
                        break;
                    default:
                        throw new InvalidDataException("Snapshot evidence inventory contains a changed or non-regular entry.");
                }
            }
        }
        foreach (var path in paths)
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path)
                ?? throw new InvalidDataException("Inactive snapshot source disappeared during inventory.");
            result.Add(path, bytes);
        }
        return result;
    }

    /// <summary>
    /// Rejects invalid UTF-8, non-object roots and duplicate keys before interpreting diagnostic identity metadata.
    /// </summary>
    /// <param name="bytes">
    /// Exact metadata bytes; one UTF-8 preamble is permitted.
    /// </param>
    /// <returns>
    /// Strict decoded JSON text after every nested object's keys have been checked.
    /// </returns>
    private static string ReadUnambiguousSnapshotEvidenceJson(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes);
        if (text.StartsWith('\uFEFF'))
            text = text[1..];
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Snapshot evidence metadata must be an object.");
        var pending = new Stack<JsonElement>();
        pending.Push(document.RootElement);
        while (pending.TryPop(out var value))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                        throw new InvalidDataException("Snapshot evidence metadata has duplicate keys.");
                    pending.Push(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray())
                    pending.Push(item);
        }
        return text;
    }
}
