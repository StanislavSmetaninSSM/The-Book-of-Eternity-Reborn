using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static partial class ExplorerLocalTurnRollbackArtifacts
{
    private const int LocalBrowserSchema = 7;
    internal sealed record LocalBrowserManifest(int SchemaVersion, string TransactionKind, string Status,
        string Scope, string CreatedAtUtc, string Generation,
        IReadOnlyList<BrowserWriteRollbackEntry> Entries, IReadOnlyList<string> CleanupDirectories);
    internal sealed record LocalBrowserCleanupIntent(int SchemaVersion, string TransactionRoot, string Scope,
        string CreatedAtUtc, string Generation, BrowserWriteCleanupOutcome Outcome);
    internal sealed class LocalBrowserTransaction(LocalBrowserManifest document, BrowserLocalStorageAccess access)
    {
        internal LocalBrowserManifest Document { get; set; } = document;
        internal BrowserLocalStorageAccess Access { get; } = access;
    }

    private static async Task<BrowserWriteRollbackTransaction> StageLocalBrowserTransactionAsync(
        FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease, IEnumerable<string> trackedFiles,
        string scope, IEnumerable<string>? cleanupDirectories, IEnumerable<string>? externalIds)
    {
        if ((externalIds ?? []).Any())
            throw new InvalidOperationException("Declared external browser participant is not prepared.");
        var paths = trackedFiles.Select(path => NormalizeRelativePath(fs, path)).Distinct(StringComparer.Ordinal).ToArray();
        if (paths.Any(path => path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal)))
            throw new InvalidDataException("Browser members cannot own transaction evidence.");
        var clean = (cleanupDirectories ?? []).Select(path => NormalizeRelativePath(fs, path)).Distinct(StringComparer.Ordinal).ToArray();
        if (clean.Any(path => !IsAllowedRollbackCleanupDirectory(path)))
            throw new InvalidDataException("Browser cleanup directory is outside the original allowlist.");
        var safeScope = SafeSegment(scope);
        var root = $"{Root}/{safeScope}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";
        var generation = fs.ReadExistingSessionGeneration(lease) ?? throw new InvalidDataException("Browser generation is missing.");
        var access = fs.BeginBrowserLocalStorage(lease, root, generation);
        var created = new List<string>();
        try
        {
            var entries = new List<BrowserWriteRollbackEntry>();
            foreach (var path in paths)
            {
                var bytes = await fs.ReadLocalFileBytesAsync(lease, path);
                string? backup = null;
                if (bytes != null)
                {
                    backup = $"{root}/{entries.Count:D4}.rollback";
                    await fs.WriteFileAtomicBytesAsync(lease, backup, bytes);
                    created.Add(backup);
                }
                entries.Add(new(path, bytes != null, backup, bytes == null ? null : ComputeSha256(bytes)));
            }
            var document = new LocalBrowserManifest(LocalBrowserSchema, "browser_local_write", "staged", safeScope,
                DateTime.UtcNow.ToString("O"), generation, entries, clean);
            var local = new LocalBrowserTransaction(document, access);
            var transaction = new BrowserWriteRollbackTransaction(root, root + "/" + BrowserWriteManifestFileName,
                safeScope, document.CreatedAtUtc, entries, clean, []) { SchemaVersion = LocalBrowserSchema, LocalTransaction = local };
            await PersistLocalBrowserAsync(fs, lease, transaction);
            created.Add(transaction.ManifestPath);
            access.RecordIntent = async (absolutePath, desired) =>
            {
                var index = entries.FindIndex(entry => string.Equals(fs.ResolvePath(entry.TrackedFile), absolutePath, StringComparison.Ordinal));
                if (index < 0) return; // UI/artifacts and undeclared callback writes are not rollback members.
                var entry = entries[index];
                var hashes = (entry.PublishedSha256s ?? []).ToList();
                if (desired != null && !hashes.Contains(ComputeSha256(desired), StringComparer.Ordinal)) hashes.Add(ComputeSha256(desired));
                entries[index] = entry with { PublishedSha256s = hashes, DeletionIntended = entry.DeletionIntended || desired == null };
                await PersistLocalBrowserAsync(fs, lease, transaction);
            };
            return transaction;
        }
        catch
        {
            try
            {
                foreach (var path in created.AsEnumerable().Reverse()) fs.DeleteFile(lease, path);
                RemoveLocalBrowserEmptyParents(fs, lease, root);
            }
            finally { access.Dispose(); }
            throw;
        }
    }

    private static Task PersistLocalBrowserAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        BrowserWriteRollbackTransaction transaction) => fs.WriteFileAtomicBytesAsync(lease, transaction.ManifestPath,
            JsonSerializer.SerializeToUtf8Bytes(transaction.LocalTransaction!.Document, ManifestJsonOptions));

    private static Task MarkLocalBrowserCommittedAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        BrowserWriteRollbackTransaction transaction) => fs.WriteFileAtomicBytesAsync(lease,
            GetBrowserWriteCommittedMarkerPath(transaction), []);

    private static async Task RestoreLocalBrowserTransactionAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, BrowserWriteRollbackTransaction transaction)
    {
        var local = transaction.LocalTransaction!;
        local.Access.Validate();
        local.Access.RecordIntent = null;
        foreach (var entry in local.Document.Entries)
        {
            byte[]? baseline = null;
            if (entry.Existed)
            {
                baseline = await fs.ReadLocalFileBytesAsync(lease, entry.BackupPath!)
                    ?? throw new InvalidDataException("Browser baseline evidence is missing.");
                if (ComputeSha256(baseline) != entry.Sha256) throw new InvalidDataException("Browser baseline evidence hash differs.");
            }
            var current = await fs.ReadLocalFileBytesAsync(lease, entry.TrackedFile);
            if (BytesEqual(current, baseline)) continue;
            if (current == null ? !entry.DeletionIntended : !(entry.PublishedSha256s ?? []).Contains(ComputeSha256(current), StringComparer.Ordinal))
                throw new InvalidDataException("Browser rollback found unknown current bytes; evidence retained.");
            fs.RequireCommittedLocalPublication(await fs.PublishLocalFilesAsync(lease, [new(entry.TrackedFile, current, baseline)]));
        }
        foreach (var directory in local.Document.CleanupDirectories) fs.DeleteOriginalDirectoryTree(lease, directory);
        local.Document = local.Document with { Status = "restored" };
        await PersistLocalBrowserAsync(fs, lease, transaction);
    }

    private static bool TryDeleteLocalBrowserTransaction(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        BrowserWriteRollbackTransaction transaction, BrowserWriteCleanupOutcome outcome, out Exception? failure)
    {
        try
        {
            var local = transaction.LocalTransaction!;
            local.Access.Validate();
            local.Access.RecordIntent = null;
            var intent = GetBrowserWriteCleanupIntentPath(transaction, outcome);
            fs.WriteFileAtomicBytesAsync(lease, intent, JsonSerializer.SerializeToUtf8Bytes(new LocalBrowserCleanupIntent(
                LocalBrowserSchema, transaction.TransactionRoot, transaction.Scope, transaction.CreatedAtUtc,
                local.Document.Generation, outcome), ManifestJsonOptions)).GetAwaiter().GetResult();
            foreach (var path in local.Document.Entries.Where(entry => entry.BackupPath != null).Select(entry => entry.BackupPath!))
                fs.DeleteFile(lease, path);
            fs.DeleteFile(lease, GetBrowserWriteCommittedMarkerPath(transaction));
            var scope = new TrustedLocalFileScope([fs.GameSessionPath]);
            var root = scope.ValidateDirectory(fs.ResolvePath(transaction.TransactionRoot), allowMissing: false);
            var allowed = new[] { fs.ResolvePath(transaction.ManifestPath), fs.ResolvePath(intent) };
            if (Directory.EnumerateFileSystemEntries(root).Any(path => !allowed.Contains(path, StringComparer.Ordinal)))
                throw new InvalidDataException("Browser cleanup retained unknown evidence.");
            fs.DeleteFile(lease, transaction.ManifestPath);
            fs.DeleteFile(lease, intent);
            RemoveLocalBrowserEmptyParents(fs, lease, transaction.TransactionRoot);
            failure = null; return true;
        }
        catch (Exception error) { failure = error; return false; }
    }

    private static void RemoveLocalBrowserEmptyParents(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease, string root)
    {
        fs.VerifyCurrentSessionOperation(lease);
        var scope = new TrustedLocalFileScope([fs.GameSessionPath]);
        for (var path = fs.ResolvePath(root); path.StartsWith(fs.ResolvePath(Root), StringComparison.Ordinal); path = Path.GetDirectoryName(path)!)
        {
            scope.ValidateDirectory(path);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        }
    }
    private static bool BytesEqual(byte[]? left, byte[]? right) => left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);
}
