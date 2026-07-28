using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class ExplorerLocalTurnRollbackArtifacts
{
    public const string Root = "game_state/control/explorer_local_turn_rollback";
    internal const string DarenRewardProfileExternalFileId = "daren_reward_profile";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string BrowserWriteManifestFileName = "browser_write_manifest.json";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public sealed record StagedBackup(string TrackedFile, string BackupPath);

    internal sealed record BrowserWriteRollbackEntry(
        string TrackedFile,
        bool Existed,
        string? BackupPath,
        string? Sha256);

    internal sealed record BrowserWriteExternalRollbackEntry(
        string FileId,
        bool Existed,
        string? BackupPath,
        string? Sha256);

    internal sealed record BrowserWriteRollbackManifest(
        int SchemaVersion,
        string TransactionKind,
        string Status,
        string Scope,
        string CreatedAtUtc,
        IReadOnlyList<BrowserWriteRollbackEntry> Entries,
        IReadOnlyList<string>? CleanupDirectories = null,
        IReadOnlyList<BrowserWriteExternalRollbackEntry>? ExternalEntries = null);

    internal sealed record BrowserWriteRollbackTransaction(
        string TransactionRoot,
        string ManifestPath,
        string Scope,
        string CreatedAtUtc,
        IReadOnlyList<BrowserWriteRollbackEntry> Entries,
        IReadOnlyList<string> CleanupDirectories,
        IReadOnlyList<BrowserWriteExternalRollbackEntry> ExternalEntries);

    public static async Task<string?> StageFileAsync(
        FileSystemManager fs,
        string trackedFile,
        string scope)
    {
        if (string.IsNullOrWhiteSpace(trackedFile))
            return null;

        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        if (!fs.FileExists(writeLease, trackedFile))
            return null;

        var content = await fs.ReadFileBytesAsync(writeLease, trackedFile);
        if (content == null)
            return null;

        var backupPath =
            $"{Root}/{SafeSegment(scope)}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}/{CreateSafeBackupFileName(trackedFile)}.rollback.{Guid.NewGuid():N}";
        await fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        return backupPath;
    }

    internal static async Task<string?> StageFileAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string trackedFile,
        string scope)
    {
        if (string.IsNullOrWhiteSpace(trackedFile) || !fs.FileExists(writeLease, trackedFile))
            return null;

        var content = await fs.ReadFileBytesAsync(writeLease, trackedFile);
        if (content == null)
            return null;

        var backupPath =
            $"{Root}/{SafeSegment(scope)}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}/{CreateSafeBackupFileName(trackedFile)}.rollback.{Guid.NewGuid():N}";
        await fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        return backupPath;
    }

    internal static async Task<BrowserWriteRollbackTransaction> StageBrowserWriteTransactionAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<string> trackedFiles,
        string scope,
        IEnumerable<string>? rollbackCleanupDirectories = null,
        IEnumerable<string>? rollbackExternalFileIds = null)
    {
        var normalizedPaths = trackedFiles
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Replace('\\', '/').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var transactionRoot =
            $"{Root}/{SafeSegment(scope)}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}";
        var manifestPath = $"{transactionRoot}/{BrowserWriteManifestFileName}";
        var cleanupDirectories = (rollbackCleanupDirectories ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizeRelativePath(fs, path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (cleanupDirectories.Any(path =>
                !IsAllowedRollbackCleanupDirectory(path) ||
                transactionRoot.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Browser rollback cleanup directory cannot contain its transaction evidence.");
        }
        var externalFileIds = (rollbackExternalFileIds ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (externalFileIds.Any(static value =>
                !string.Equals(
                    value,
                    DarenRewardProfileExternalFileId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Browser rollback external file identifier is unsupported.");
        }

        var safeScope = SafeSegment(scope);
        var createdAtUtc = DateTime.UtcNow.ToString("O");
        var entries = new List<BrowserWriteRollbackEntry>(normalizedPaths.Length);
        var externalEntries =
            new List<BrowserWriteExternalRollbackEntry>(externalFileIds.Length);
        var createdPaths = new List<string>();

        try
        {
            for (var index = 0; index < normalizedPaths.Length; index++)
            {
                var trackedFile = normalizedPaths[index];
                var content = await fs.ReadFileBytesAsync(writeLease, trackedFile);
                if (content == null)
                {
                    entries.Add(new BrowserWriteRollbackEntry(
                        trackedFile,
                        Existed: false,
                        BackupPath: null,
                        Sha256: null));
                    continue;
                }

                var backupPath =
                    $"{transactionRoot}/{CreateSafeBackupFileName(trackedFile)}.rollback.{index:D4}_{Guid.NewGuid():N}";
                await fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
                createdPaths.Add(backupPath);
                entries.Add(new BrowserWriteRollbackEntry(
                    trackedFile,
                    Existed: true,
                    BackupPath: backupPath,
                    Sha256: ComputeSha256(content)));
            }

            for (var index = 0; index < externalFileIds.Length; index++)
            {
                var fileId = externalFileIds[index];
                var content = ReadExternalRollbackBytes(fs, fileId);
                if (content == null)
                {
                    externalEntries.Add(new BrowserWriteExternalRollbackEntry(
                        fileId,
                        Existed: false,
                        BackupPath: null,
                        Sha256: null));
                    continue;
                }

                var backupPath =
                    $"{transactionRoot}/external_{index:D4}_{Guid.NewGuid():N}.rollback";
                await fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
                createdPaths.Add(backupPath);
                externalEntries.Add(new BrowserWriteExternalRollbackEntry(
                    fileId,
                    Existed: true,
                    BackupPath: backupPath,
                    Sha256: ComputeSha256(content)));
            }

            var manifest = new BrowserWriteRollbackManifest(
                SchemaVersion: 3,
                TransactionKind: "browser_local_write",
                Status: "staged",
                Scope: safeScope,
                CreatedAtUtc: createdAtUtc,
                Entries: entries,
                CleanupDirectories: cleanupDirectories,
                ExternalEntries: externalEntries);
            await fs.WriteFileAtomicAsync(
                writeLease,
                manifestPath,
                JsonSerializer.Serialize(manifest, ManifestJsonOptions));
            createdPaths.Add(manifestPath);
            return new BrowserWriteRollbackTransaction(
                transactionRoot,
                manifestPath,
                safeScope,
                createdAtUtc,
                entries,
                cleanupDirectories,
                externalEntries);
        }
        catch
        {
            foreach (var path in createdPaths.AsEnumerable().Reverse())
            {
                try
                {
                    if (fs.FileExists(writeLease, path))
                        fs.DeleteFile(writeLease, path);
                }
                catch
                {
                    // Preserve any evidence that could not be removed.
                }
            }

            TryDeleteEmptyDirectories(fs, writeLease);
            throw;
        }
    }

    internal static async Task MarkBrowserWriteTransactionCommittedAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction)
    {
        var manifest = new BrowserWriteRollbackManifest(
            SchemaVersion: 3,
            TransactionKind: "browser_local_write",
            Status: "committed",
            Scope: transaction.Scope,
            CreatedAtUtc: transaction.CreatedAtUtc,
            Entries: transaction.Entries,
            CleanupDirectories: transaction.CleanupDirectories,
            ExternalEntries: transaction.ExternalEntries);
        await fs.WriteFileAtomicAsync(
            writeLease,
            transaction.ManifestPath,
            JsonSerializer.Serialize(manifest, ManifestJsonOptions));
    }

    internal static async Task RecoverInterruptedBrowserWriteTransactionsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var manifestPaths = fs.EnumerateFiles(writeLease, BrowserWriteManifestFileName)
            .Where(path => path.StartsWith($"{Root}/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetTransactionTicks)
            .ThenByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (manifestPaths.Length == 0)
            return;

        var transactions = new List<(BrowserWriteRollbackTransaction Transaction, string Status)>(
            manifestPaths.Length);
        foreach (var manifestPath in manifestPaths)
        {
            var manifestBytes = await fs.ReadFileBytesAsync(writeLease, manifestPath) ??
                                throw new FileNotFoundException(
                                    "Browser rollback manifest disappeared during recovery.",
                                    manifestPath);
            BrowserWriteRollbackManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<BrowserWriteRollbackManifest>(
                               DecodeUtf8(manifestBytes),
                               ManifestJsonOptions) ??
                           throw new InvalidDataException(
                               $"Browser rollback manifest '{manifestPath}' is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' is malformed.",
                    ex);
            }

            transactions.Add(ValidateBrowserWriteManifest(fs, manifestPath, manifest));
        }

        foreach (var (transaction, status) in transactions)
        {
            if (string.Equals(status, "staged", StringComparison.Ordinal))
                await RestoreBrowserWriteTransactionAsync(fs, writeLease, transaction);

            if (!TryDeleteBrowserWriteTransaction(
                    fs,
                    writeLease,
                    transaction,
                    out var cleanupFailure))
            {
                throw new IOException(
                    $"Browser rollback transaction '{transaction.ManifestPath}' was resolved, but its evidence could not be cleaned.",
                    cleanupFailure);
            }
        }
    }

    internal static async Task<byte[]?> ReadBrowserWriteBeforeImageAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackEntry entry)
    {
        if (!entry.Existed)
            return null;
        if (string.IsNullOrWhiteSpace(entry.BackupPath) || string.IsNullOrWhiteSpace(entry.Sha256))
            throw new InvalidDataException($"Rollback evidence for '{entry.TrackedFile}' is incomplete.");

        var content = await fs.ReadFileBytesAsync(writeLease, entry.BackupPath);
        if (content == null)
            throw new FileNotFoundException(
                $"Rollback evidence for '{entry.TrackedFile}' is missing.",
                entry.BackupPath);
        if (!string.Equals(ComputeSha256(content), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Rollback evidence for '{entry.TrackedFile}' failed its exact-byte hash check.");

        return content;
    }

    internal static async Task RestoreBrowserWriteEntryAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackEntry entry)
    {
        var content = await ReadBrowserWriteBeforeImageAsync(fs, writeLease, entry);
        if (entry.Existed)
        {
            var current = await fs.ReadFileBytesAsync(writeLease, entry.TrackedFile);
            if (current != null && current.AsSpan().SequenceEqual(content))
                return;

            await fs.WriteFileAtomicBytesAsync(writeLease, entry.TrackedFile, content!);
            return;
        }

        if (fs.FileExists(writeLease, entry.TrackedFile))
            fs.DeleteFile(writeLease, entry.TrackedFile);
    }

    internal static bool TryDeleteBrowserWriteTransaction(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction,
        out Exception? failure)
    {
        try
        {
            if (fs.FileExists(writeLease, transaction.ManifestPath))
                fs.DeleteFile(writeLease, transaction.ManifestPath);

            var transactionRoot = fs.ResolvePath(transaction.TransactionRoot);
            if (Directory.Exists(transactionRoot))
                fs.DeleteDirectoryTree(writeLease, transaction.TransactionRoot);
            TryDeleteEmptyBrowserWriteParents(fs, writeLease, transaction.TransactionRoot);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            failure = ex;
            return false;
        }
    }

    private static (
        BrowserWriteRollbackTransaction Transaction,
        string Status) ValidateBrowserWriteManifest(
        FileSystemManager fs,
        string manifestPath,
        BrowserWriteRollbackManifest manifest)
    {
        if (manifest.SchemaVersion is not (1 or 2 or 3) ||
            !string.Equals(manifest.TransactionKind, "browser_local_write", StringComparison.Ordinal) ||
            manifest.Status is not ("staged" or "committed") ||
            string.IsNullOrWhiteSpace(manifest.Scope) ||
            !string.Equals(manifest.Scope, SafeSegment(manifest.Scope), StringComparison.Ordinal) ||
            !DateTimeOffset.TryParse(
                manifest.CreatedAtUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _) ||
            manifest.Entries == null)
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' has an invalid contract.");
        }

        var normalizedManifestPath = NormalizeRelativePath(fs, manifestPath);
        var transactionRoot = normalizedManifestPath[
            ..normalizedManifestPath.LastIndexOf("/", StringComparison.Ordinal)];
        var transactionSegments = transactionRoot[(Root.Length + 1)..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (transactionSegments.Length != 2 ||
            !string.Equals(transactionSegments[0], manifest.Scope, StringComparison.Ordinal) ||
            !IsValidTransactionDirectoryName(transactionSegments[1]))
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' is outside its declared transaction root.");
        }

        var cleanupDirectories = (manifest.CleanupDirectories ?? [])
            .Select(path => NormalizeRelativePath(fs, path))
            .ToArray();
        if (manifest.SchemaVersion == 1 && cleanupDirectories.Length > 0)
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' declares cleanup directories under legacy schema.");
        }
        if (cleanupDirectories.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            cleanupDirectories.Length ||
            cleanupDirectories.Any(path =>
                !IsAllowedRollbackCleanupDirectory(path) ||
                transactionRoot.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' contains an unsafe or duplicate cleanup directory.");
        }

        var trackedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var backupPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<BrowserWriteRollbackEntry>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            if (entry == null)
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' contains an empty entry.");

            var trackedFile = NormalizeRelativePath(fs, entry.TrackedFile);
            if (trackedFile.StartsWith($"{Root}/", StringComparison.OrdinalIgnoreCase) ||
                !trackedFiles.Add(trackedFile))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' contains an unsafe or duplicate tracked path.");
            }

            if (!entry.Existed)
            {
                if (!string.IsNullOrWhiteSpace(entry.BackupPath) ||
                    !string.IsNullOrWhiteSpace(entry.Sha256))
                {
                    throw new InvalidDataException(
                        $"Browser rollback manifest '{manifestPath}' has evidence for a missing baseline.");
                }

                entries.Add(entry with { TrackedFile = trackedFile });
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.BackupPath) ||
                !IsSha256(entry.Sha256))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' has incomplete exact-byte evidence.");
            }

            var backupPath = NormalizeRelativePath(fs, entry.BackupPath);
            if (!backupPath.StartsWith($"{transactionRoot}/", StringComparison.OrdinalIgnoreCase) ||
                !backupPaths.Add(backupPath))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' contains an unsafe or duplicate backup path.");
            }

            entries.Add(entry with
            {
                TrackedFile = trackedFile,
                BackupPath = backupPath,
                Sha256 = entry.Sha256!.ToLowerInvariant()
            });
        }

        var sourceExternalEntries = manifest.ExternalEntries ?? [];
        if (manifest.SchemaVersion < 3 && sourceExternalEntries.Count > 0)
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' declares external evidence under a legacy schema.");
        }

        var externalFileIds = new HashSet<string>(StringComparer.Ordinal);
        var externalEntries =
            new List<BrowserWriteExternalRollbackEntry>(sourceExternalEntries.Count);
        foreach (var entry in sourceExternalEntries)
        {
            if (entry == null ||
                !string.Equals(
                    entry.FileId,
                    DarenRewardProfileExternalFileId,
                    StringComparison.Ordinal) ||
                !externalFileIds.Add(entry.FileId))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' contains an unsupported or duplicate external file.");
            }

            if (!entry.Existed)
            {
                if (!string.IsNullOrWhiteSpace(entry.BackupPath) ||
                    !string.IsNullOrWhiteSpace(entry.Sha256))
                {
                    throw new InvalidDataException(
                        $"Browser rollback manifest '{manifestPath}' has evidence for a missing external baseline.");
                }

                externalEntries.Add(entry);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.BackupPath) ||
                !IsSha256(entry.Sha256))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' has incomplete external exact-byte evidence.");
            }

            var backupPath = NormalizeRelativePath(fs, entry.BackupPath);
            if (!backupPath.StartsWith($"{transactionRoot}/", StringComparison.OrdinalIgnoreCase) ||
                !backupPaths.Add(backupPath))
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' contains an unsafe or duplicate external backup path.");
            }

            externalEntries.Add(entry with
            {
                BackupPath = backupPath,
                Sha256 = entry.Sha256!.ToLowerInvariant()
            });
        }

        return (
            new BrowserWriteRollbackTransaction(
                transactionRoot,
                normalizedManifestPath,
                manifest.Scope,
                manifest.CreatedAtUtc,
                entries,
                cleanupDirectories,
                externalEntries),
            manifest.Status);
    }

    internal static async Task RestoreBrowserWriteTransactionAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction)
    {
        var failures = new List<Exception>();
        foreach (var entry in transaction.Entries)
        {
            try
            {
                await RestoreBrowserWriteEntryAsync(fs, writeLease, entry);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException(
                    $"Could not restore interrupted browser write '{entry.TrackedFile}'.",
                    ex));
            }
        }

        foreach (var entry in transaction.ExternalEntries)
        {
            try
            {
                var content = await ReadExternalBeforeImageAsync(
                    fs,
                    writeLease,
                    entry);
                RestoreExternalRollbackBytes(fs, entry.FileId, content);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException(
                    $"Could not restore interrupted browser external file '{entry.FileId}'.",
                    ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                $"Interrupted browser write '{transaction.ManifestPath}' could not be fully restored. Recovery evidence was retained.",
                new AggregateException(failures));
        }

        foreach (var cleanupDirectory in transaction.CleanupDirectories)
        {
            try
            {
                if (Directory.Exists(fs.ResolvePath(cleanupDirectory)))
                    fs.DeleteDirectoryTree(writeLease, cleanupDirectory);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException(
                    $"Could not remove interrupted browser-write artifacts '{cleanupDirectory}'.",
                    ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                $"Interrupted browser write '{transaction.ManifestPath}' restored tracked files but could not clean dynamic artifacts. Recovery evidence was retained.",
                new AggregateException(failures));
        }
    }

    private static string NormalizeRelativePath(FileSystemManager fs, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new InvalidDataException("Browser rollback path must be a non-empty relative path.");

        var normalized = path.Replace('\\', '/').Trim();
        _ = fs.ResolvePath(normalized);
        return Path.GetRelativePath(fs.ResolvePath(""), fs.ResolvePath(normalized))
            .Replace('\\', '/');
    }

    private static bool IsValidTransactionDirectoryName(string value)
    {
        var separator = value.IndexOf('_');
        return separator > 0 &&
               long.TryParse(value[..separator], out var ticks) &&
               ticks > 0 &&
               Guid.TryParseExact(value[(separator + 1)..], "N", out _);
    }

    private static bool IsAllowedRollbackCleanupDirectory(string path) =>
        string.Equals(path, PendingTurnSnapshotDirectory, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            $"{Root}/browser_direct_gacha",
            StringComparison.OrdinalIgnoreCase);

    private static byte[]? ReadExternalRollbackBytes(
        FileSystemManager fs,
        string fileId) =>
        fileId switch
        {
            DarenRewardProfileExternalFileId =>
                QteSceneService.ReadDarenProfileRollbackBytes(fs),
            _ => throw new InvalidDataException(
                $"Unsupported browser rollback external file '{fileId}'.")
        };

    private static async Task<byte[]?> ReadExternalBeforeImageAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteExternalRollbackEntry entry)
    {
        if (!entry.Existed)
            return null;
        if (string.IsNullOrWhiteSpace(entry.BackupPath) ||
            string.IsNullOrWhiteSpace(entry.Sha256))
        {
            throw new InvalidDataException(
                $"Rollback evidence for external file '{entry.FileId}' is incomplete.");
        }

        var content = await fs.ReadFileBytesAsync(writeLease, entry.BackupPath);
        if (content == null)
        {
            throw new FileNotFoundException(
                $"Rollback evidence for external file '{entry.FileId}' is missing.",
                entry.BackupPath);
        }
        if (!string.Equals(
                ComputeSha256(content),
                entry.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Rollback evidence for external file '{entry.FileId}' failed its exact-byte hash check.");
        }

        return content;
    }

    private static void RestoreExternalRollbackBytes(
        FileSystemManager fs,
        string fileId,
        byte[]? content)
    {
        if (!string.Equals(
                fileId,
                DarenRewardProfileExternalFileId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported browser rollback external file '{fileId}'.");
        }

        QteSceneService.RestoreDarenProfileRollbackBytes(fs, content);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(static ch =>
            ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static string DecodeUtf8(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
            .GetString(bytes, offset, bytes.Length - offset);
    }

    private static void TryDeleteEmptyBrowserWriteParents(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string transactionRoot)
    {
        try
        {
            var scopePath = transactionRoot[..transactionRoot.LastIndexOf("/", StringComparison.Ordinal)];
            var scopeFullPath = fs.ResolvePath(scopePath);
            if (Directory.Exists(scopeFullPath) &&
                !Directory.EnumerateFileSystemEntries(scopeFullPath).Any())
            {
                fs.DeleteDirectoryTree(writeLease, scopePath);
            }

            var rootFullPath = fs.ResolvePath(Root);
            if (Directory.Exists(rootFullPath) &&
                !Directory.EnumerateFileSystemEntries(rootFullPath).Any())
            {
                fs.DeleteDirectoryTree(writeLease, Root);
            }
        }
        catch
        {
            // Parent cleanup is cosmetic once the transaction manifest is gone.
        }
    }

    public static IReadOnlyList<StagedBackup> DiscoverBackups(
        FileSystemManager fs,
        IEnumerable<string> trackedFiles)
    {
        var root = fs.ResolvePath(Root);
        if (!Directory.Exists(root))
            return Array.Empty<StagedBackup>();

        var gameSessionRoot = fs.ResolvePath("");
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };
        var availableBackups = Directory
            .EnumerateFiles(root, "*.rollback.*", options)
            .Select(path => new FileInfo(path))
            .ToArray();
        var backups = new List<StagedBackup>();
        foreach (var trackedFile in trackedFiles
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path.Replace('\\', '/').Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var safeName = CreateSafeBackupFileName(trackedFile);
            var match = availableBackups
                .Where(file => file.Name.StartsWith(
                    $"{safeName}.rollback.",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .ThenByDescending(static file => file.FullName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (match == null)
                continue;

            var relative = Path.GetRelativePath(gameSessionRoot, match.FullName).Replace('\\', '/');
            backups.Add(new StagedBackup(trackedFile, relative));
        }

        return backups;
    }

    internal static IReadOnlyList<StagedBackup> DiscoverBackups(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<string> trackedFiles)
    {
        var backups = new List<StagedBackup>();
        foreach (var trackedFile in trackedFiles
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path.Replace('\\', '/').Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var safeName = CreateSafeBackupFileName(trackedFile);
            var match = fs.EnumerateFiles(writeLease, $"{safeName}.rollback.*")
                .Where(path => path.StartsWith($"{Root}/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(GetTransactionTicks)
                .ThenByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (match != null)
                backups.Add(new StagedBackup(trackedFile, match));
        }

        return backups;
    }

    public static void DeleteBackup(FileSystemManager fs, string? backupPath)
    {
        var writeLease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        try
        {
            if (!string.IsNullOrWhiteSpace(backupPath))
                fs.DeleteFile(writeLease, backupPath);
            DeleteEmptyDirectories(fs, writeLease);
        }
        finally
        {
            writeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    internal static void DeleteBackup(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string? backupPath)
    {
        if (!string.IsNullOrWhiteSpace(backupPath) && fs.FileExists(writeLease, backupPath))
            fs.DeleteFile(writeLease, backupPath);

        DeleteEmptyDirectories(fs, writeLease);
    }

    public static void DeleteEmptyDirectories(FileSystemManager fs)
    {
        var writeLease = fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        try
        {
            DeleteEmptyDirectories(fs, writeLease);
        }
        finally
        {
            writeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void TryDeleteEmptyDirectories(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        try
        {
            DeleteEmptyDirectories(fs, writeLease);
        }
        catch
        {
            // Best effort after an interrupted staging operation.
        }
    }

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static long GetTransactionTicks(string relativePath)
    {
        foreach (var segment in relativePath.Replace('\\', '/').Split('/'))
        {
            var separator = segment.IndexOf('_');
            var candidate = separator >= 0 ? segment[..separator] : segment;
            if (long.TryParse(candidate, out var ticks))
                return ticks;
        }

        return 0;
    }

    internal static void DeleteEmptyDirectories(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        fs.DeleteEmptyDirectories(writeLease, Root);

    private static string CreateSafeBackupFileName(string trackedFile)
    {
        var normalizedPath = trackedFile.Replace('\\', '/').Trim('/');
        var safePath = new string(normalizedPath
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safePath) ? "tracked_file" : safePath;
    }

    private static string SafeSegment(string value)
    {
        var safe = new string((value ?? string.Empty)
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "browser" : safe;
    }
}
