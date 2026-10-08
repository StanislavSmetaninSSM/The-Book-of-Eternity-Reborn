using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private sealed record ConsoleEvidenceOwner(string Generation, IReadOnlyDictionary<string, string> Hashes);
    private readonly Dictionary<string, ConsoleEvidenceOwner> _consoleEvidenceOwners = new(StringComparer.Ordinal);

    internal void ForgetConsoleRollbackEvidenceOwnership() => _consoleEvidenceOwners.Clear();

    internal void RegisterConsoleRollbackEvidence(CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]> evidence)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        var generation = ReadExistingSessionGeneration(lease) ?? throw new InvalidDataException("Console preparation generation is missing.");
        foreach (var cohort in evidence.GroupBy(pair => ConsoleLocalTurnRollbackArtifacts.GetCohortRoot(pair.Key)))
        {
            var hashes = _consoleEvidenceOwners.TryGetValue(cohort.Key, out var owner) && owner.Generation == generation
                ? new Dictionary<string, string>(owner.Hashes, StringComparer.Ordinal) : new(StringComparer.Ordinal);
            foreach (var (path, bytes) in cohort)
            {
                var actual = ReadBrowserStorageEvidence(lease, path);
                if (actual == null || !actual.AsSpan().SequenceEqual(bytes))
                    throw new InvalidDataException("Console preparation publication differs from owned evidence.");
                hashes[path] = ConsoleLocalTurnRollbackArtifacts.Hash(bytes);
            }
            var marker = cohort.Key + "/" + ConsoleLocalTurnRollbackArtifacts.MarkerName;
            ConsoleLocalTurnRollbackArtifacts.ValidateMarker(cohort.Key,
                ReadBrowserStorageEvidence(lease, marker) ?? throw new InvalidDataException("Console marker is absent."), generation);
            if (!hashes.ContainsKey(marker)) throw new InvalidDataException("Console marker ownership is absent.");
            _consoleEvidenceOwners[cohort.Key] = new(generation, hashes);
        }
    }

    private void AdmitConsoleRollbackEvidence(CanonicalWriteLease lease)
    {
        EnsureCanonicalWriteLeaseActive(lease);
        VerifyCurrentSessionOperation(lease);
        var files = EnumerateLocalTreeFiles(new TrustedLocalFileScope([GameSessionPath]),
            ResolvePath(ConsoleLocalTurnRollbackArtifacts.Root))
            .Select(path => GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows())).ToArray();
        if (files.Length == 0) return;
        var generation = ReadExistingSessionGeneration(lease) ?? throw new InvalidDataException("Console preparation generation is missing.");
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload? signed = null;
        foreach (var cohort in files.GroupBy(ConsoleLocalTurnRollbackArtifacts.GetCohortRoot))
        {
            var actual = cohort.ToDictionary(path => path,
                path => ReadBrowserStorageEvidence(lease, path) ?? throw new InvalidDataException("Console evidence disappeared."), StringComparer.Ordinal);
            var marker = cohort.Key + "/" + ConsoleLocalTurnRollbackArtifacts.MarkerName;
            if (!actual.TryGetValue(marker, out var markerBytes))
                throw new InvalidDataException("Console preparation marker is missing; evidence retained.");
            ConsoleLocalTurnRollbackArtifacts.ValidateMarker(cohort.Key, markerBytes, generation);
            if (!_consoleEvidenceOwners.TryGetValue(cohort.Key, out var owner))
            {
                signed ??= ExplorerLocalTurnRollbackArtifacts.RequireCurrentPendingRollbackAuthority(this, lease);
                if (!signed.Files.ContainsKey(marker) || !signed.SnapshotFileHashes.TryGetValue(marker, out var markerHash))
                    throw new InvalidDataException("Console preparation has no current signed marker ownership; evidence retained.");
                var hashes = new Dictionary<string, string>(StringComparer.Ordinal) { [marker] = markerHash };
                foreach (var (original, backup) in signed.RollbackBackups.Where(pair => pair.Value.StartsWith(cohort.Key + "/", StringComparison.Ordinal)))
                {
                    if (!signed.RollbackBackupHashes.TryGetValue(original, out var hash))
                        throw new InvalidDataException("Console before-image signed hash is missing.");
                    hashes.Add(backup, hash);
                }
                if (!actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(hashes.Keys))
                    throw new InvalidDataException("Console signed evidence is incomplete or contains unknown artifacts; evidence retained.");
                owner = new(generation, hashes);
            }
            if (owner.Generation != generation || actual.Any(pair =>
                    !owner.Hashes.TryGetValue(pair.Key, out var hash) || ConsoleLocalTurnRollbackArtifacts.Hash(pair.Value) != hash))
                throw new InvalidDataException("Console preparation owned bytes or generation differ; evidence retained.");
            // Storage admission only. Retain the entire immutable cohort before
            // cancellation removes request/manifest/backups across later leases.
            _consoleEvidenceOwners[cohort.Key] = owner;
        }
    }
}
