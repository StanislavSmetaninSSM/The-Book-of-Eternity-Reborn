using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static partial class ExplorerLocalTurnRollbackArtifacts
{
    private static readonly JsonSerializerOptions LocalBrowserJson = new(ManifestJsonOptions)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private sealed record LocalBrowserEvidence(string Root, LocalBrowserManifest? Manifest,
        LocalBrowserCleanupIntent? Cleanup, bool CommittedMarker);

    // Read-only admission: no old/unknown browser evidence may be mutated by the
    // common publisher before the original Linux handler has recognized its schema.
    internal static void PreflightLocalBrowserEvidence(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease) =>
        _ = ReadLocalBrowserEvidence(fs, lease);

    internal static async Task RecoverLocalBrowserEvidenceAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        foreach (var evidence in ReadLocalBrowserEvidence(fs, lease))
        {
            var generation = evidence.Manifest?.Generation ?? evidence.Cleanup!.Generation;
            using var access = fs.BeginBrowserLocalStorage(lease, evidence.Root, generation, evidence.Manifest?.ExternalEntries?.Count > 0);
            if (evidence.Manifest == null)
            {
                var path = CleanupPath(evidence.Root, evidence.Cleanup!.Outcome);
                fs.DeleteFile(lease, path);
                RemoveLocalBrowserEmptyParents(fs, lease, evidence.Root);
                continue;
            }
            var document = evidence.Manifest;
            var transaction = new BrowserWriteRollbackTransaction(evidence.Root, evidence.Root + "/" + BrowserWriteManifestFileName,
                document.Scope, document.CreatedAtUtc, document.Entries, document.CleanupDirectories, [])
            {
                SchemaVersion = LocalBrowserSchema, LocalTransaction = new(document, access)
            };
            var outcome = evidence.Cleanup?.Outcome ?? (evidence.CommittedMarker || document.Status == "committed"
                ? BrowserWriteCleanupOutcome.Committed : BrowserWriteCleanupOutcome.Restored);
            if (evidence.Cleanup == null && !evidence.CommittedMarker && document.Status == "staged")
                await RestoreLocalBrowserTransactionAsync(fs, lease, transaction);
            if (!TryDeleteLocalBrowserTransaction(fs, lease, transaction, outcome, out var failure))
                throw new IOException("Resolved browser decision retained cleanup debt.", failure);
        }
    }

    private static IReadOnlyList<LocalBrowserEvidence> ReadLocalBrowserEvidence(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var files = fs.ListBrowserStorageEvidence(lease);
        IReadOnlyList<string> pendingScratch = files.Count == 0 ? [] : fs.ReadPendingBrowserPublicationScratch(lease);
        var roots = files.Select(path => path[..path.LastIndexOf('/')]).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var result = new List<LocalBrowserEvidence>();
        foreach (var root in roots)
        {
            // Long-lived pending-turn backup is not a schema7 transaction.
            // The original publisher alone authenticates any interrupted Stage
            // scratch here; acquiring a lease never retires this before-image.
            if (root.StartsWith(DirectGachaRoot + "/", StringComparison.Ordinal))
            {
                if (!IsLocalDirectGachaPath(root) || files.Where(path => path.StartsWith(root + "/", StringComparison.Ordinal))
                    .Any(path => !IsLocalDirectGachaBackup(path) && !pendingScratch.Contains(path, StringComparer.Ordinal)))
                    throw new InvalidDataException("Unrecognized direct-gacha evidence; original bytes retained.");
                continue;
            }
            var parts = root.Split('/');
            var rootParts = Root.Split('/');
            if (parts.Length != rootParts.Length + 2 || !root.StartsWith(Root + "/", StringComparison.Ordinal) ||
                !IsValidTransactionDirectoryName(parts[^1]) || parts[^2] != SafeSegment(parts[^2]))
                throw new InvalidDataException("Unrecognized browser evidence namespace; original evidence retained.");
            var own = files.Where(path => path.StartsWith(root + "/", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            var manifestPath = root + "/" + BrowserWriteManifestFileName;
            var markerPath = root + "/" + BrowserWriteCommittedMarkerFileName;
            var committedPath = CleanupPath(root, BrowserWriteCleanupOutcome.Committed);
            var restoredPath = CleanupPath(root, BrowserWriteCleanupOutcome.Restored);
            if (own.Contains(committedPath) && own.Contains(restoredPath))
                throw new InvalidDataException("Conflicting browser cleanup decisions; evidence retained.");
            var intentPath = own.Contains(committedPath) ? committedPath : own.Contains(restoredPath) ? restoredPath : null;
            LocalBrowserCleanupIntent? cleanup = null;
            if (intentPath != null)
            {
                cleanup = ParseLocal<LocalBrowserCleanupIntent>(fs.ReadBrowserStorageEvidence(lease, intentPath)!);
                if (cleanup.SchemaVersion != LocalBrowserSchema || cleanup.TransactionRoot != root || cleanup.Scope != parts[^2] ||
                    cleanup.Outcome != (intentPath == committedPath ? BrowserWriteCleanupOutcome.Committed : BrowserWriteCleanupOutcome.Restored))
                    throw new InvalidDataException("Unsupported browser cleanup binding; evidence retained.");
                ValidateLocalBinding(fs, lease, cleanup.Generation, cleanup.CreatedAtUtc);
            }
            LocalBrowserManifest? manifest = null;
            if (own.Contains(manifestPath))
            {
                manifest = ParseLocal<LocalBrowserManifest>(fs.ReadBrowserStorageEvidence(lease, manifestPath)!);
                ValidateLocalManifest(fs, lease, root, parts[^2], manifest);
                if (cleanup != null && (cleanup.Generation != manifest.Generation || cleanup.CreatedAtUtc != manifest.CreatedAtUtc))
                    throw new InvalidDataException("Browser cleanup does not bind the original manifest.");
            }
            if (manifest == null && cleanup == null)
                throw new InvalidDataException("Browser evidence has no supported schema7 manifest or cleanup binding.");
            if (own.Contains(markerPath) && fs.ReadBrowserStorageEvidence(lease, markerPath)!.Length != 0)
                throw new InvalidDataException("Malformed browser committed marker; evidence retained.");
            if (own.Contains(markerPath) && cleanup?.Outcome == BrowserWriteCleanupOutcome.Restored)
                throw new InvalidDataException("Browser committed marker conflicts with rollback cleanup.");
            var allowed = new HashSet<string>(StringComparer.Ordinal) { manifestPath, markerPath };
            if (intentPath != null) allowed.Add(intentPath);
            foreach (var entry in manifest?.Entries ?? []) if (entry.BackupPath != null) allowed.Add(entry.BackupPath);
            foreach (var entry in manifest?.ExternalEntries ?? []) if (entry.BackupPath != null) allowed.Add(entry.BackupPath);
            allowed.UnionWith(pendingScratch);
            if (own.Any(path => !allowed.Contains(path)) || manifest == null && own.Count != 1)
                throw new InvalidDataException("Browser cleanup retained unknown evidence.");
            result.Add(new(root, manifest, cleanup, own.Contains(markerPath)));
        }
        return result;
    }

    private static T ParseLocal<T>(byte[] bytes)
    {
        try
        {
            return StrictJsonAuthority.Deserialize<T>(DecodeUtf8(bytes), LocalBrowserJson, "Linux browser evidence")
                ?? throw new InvalidDataException("Empty browser evidence.");
        }
        catch (JsonException failure) { throw new InvalidDataException("Unsupported/malformed browser evidence; original bytes retained.", failure); }
    }

    private static void ValidateLocalBinding(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease, string generation, string created)
    {
        if (!Guid.TryParseExact(generation, "N", out var id) || generation != id.ToString("N") ||
            generation != fs.ReadExistingSessionGeneration(lease) || !DateTimeOffset.TryParse(created, out var time) || time.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Browser evidence has a stale or invalid original generation/binding.");
    }

    private static void ValidateLocalManifest(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string root, string scope, LocalBrowserManifest manifest)
    {
        if (manifest.SchemaVersion != LocalBrowserSchema || manifest.TransactionKind != "browser_local_write" ||
            manifest.Scope != scope || manifest.Status is not ("staged" or "restored" or "committed") ||
            manifest.Entries == null || manifest.CleanupDirectories == null)
            throw new InvalidDataException("Original unsupported browser manifest retained.");
        ValidateLocalBinding(fs, lease, manifest.Generation, manifest.CreatedAtUtc);
        var members = new HashSet<string>(StringComparer.Ordinal);
        var backups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Entries)
        {
            if (entry == null || NormalizeRelativePath(fs, entry.TrackedFile) != entry.TrackedFile ||
                entry.TrackedFile == Root || entry.TrackedFile.StartsWith(Root + "/", StringComparison.Ordinal) ||
                !members.Add(entry.TrackedFile) || entry.MutationIntent != null || entry.PublicationReceipt != null ||
                (entry.PublishedSha256s ?? []).Any(hash => !IsSha256(hash) || hash != hash.ToLowerInvariant()) ||
                (entry.PublishedSha256s ?? []).Distinct(StringComparer.Ordinal).Count() != (entry.PublishedSha256s ?? []).Count)
                throw new InvalidDataException("Invalid browser member or byte intent.");
            if (entry.Existed)
            {
                if (!IsSha256(entry.Sha256) || entry.BackupPath == null ||
                    NormalizeRelativePath(fs, entry.BackupPath) != entry.BackupPath ||
                    Path.GetDirectoryName(entry.BackupPath)?.Replace('\\', '/') != root || !backups.Add(entry.BackupPath))
                    throw new InvalidDataException("Invalid browser before-image membership.");
            }
            else if (entry.BackupPath != null || entry.Sha256 != null)
                throw new InvalidDataException("Absent browser baseline has unexpected evidence.");
        }
        if ((manifest.ExternalEntries ?? []).Count > 1) throw new InvalidDataException("Duplicate browser external participant.");
        foreach (var entry in manifest.ExternalEntries ?? [])
        {
            if (entry == null || entry.FileId != DarenRewardProfileExternalFileId ||
                (entry.PublishedSha256s ?? []).Any(hash => !IsSha256(hash) || hash != hash.ToLowerInvariant()) ||
                (entry.PublishedSha256s ?? []).Distinct(StringComparer.Ordinal).Count() != (entry.PublishedSha256s ?? []).Count)
                throw new InvalidDataException("Invalid declared browser external intent.");
            if (entry.Existed)
            {
                if (!IsSha256(entry.Sha256) || entry.BackupPath == null || NormalizeRelativePath(fs, entry.BackupPath) != entry.BackupPath ||
                    Path.GetDirectoryName(entry.BackupPath)?.Replace('\\', '/') != root || !backups.Add(entry.BackupPath))
                    throw new InvalidDataException("Invalid external browser baseline membership.");
            }
            else if (entry.BackupPath != null || entry.Sha256 != null)
                throw new InvalidDataException("Absent external baseline has unexpected evidence.");
        }
        if (manifest.CleanupDirectories.Distinct(StringComparer.Ordinal).Count() != manifest.CleanupDirectories.Count ||
            manifest.CleanupDirectories.Any(path => NormalizeRelativePath(fs, path) != path || !IsAllowedRollbackCleanupDirectory(path)))
            throw new InvalidDataException("Browser cleanup exceeds the original directory allowlist.");
    }
    private static string CleanupPath(string root, BrowserWriteCleanupOutcome outcome) => root + "/" +
        (outcome == BrowserWriteCleanupOutcome.Committed ? BrowserWriteCommittedCleanupIntentFileName : BrowserWriteRestoredCleanupIntentFileName);
}
