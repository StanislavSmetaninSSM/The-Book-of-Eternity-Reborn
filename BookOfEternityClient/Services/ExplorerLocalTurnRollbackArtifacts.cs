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
    private const string BrowserWriteCommittedMarkerFileName =
        "browser_write_committed.marker";
    private const string BrowserWriteCommittedCleanupIntentFileName =
        "browser_write_cleanup_committed.intent";
    private const string BrowserWriteRestoredCleanupIntentFileName =
        "browser_write_cleanup_restored.intent";
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
        string? Sha256,
        PhysicalFileAuthority.FileIdentity? ParentIdentity = null,
        PhysicalFileAuthority.FileIdentity? BaselineIdentity = null,
        PhysicalFileAuthority.FileIdentity? PublishedIdentity = null,
        string? PublishedSha256 = null,
        string? PublicationTransactionId = null);

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
        IReadOnlyList<BrowserWriteExternalRollbackEntry> ExternalEntries)
    {
        internal DarenRewardProfileRollbackTransaction? DarenTransaction
        {
            get;
            init;
        }
    }

    internal enum BrowserWriteCleanupOutcome
    {
        Committed,
        Restored
    }

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
        DarenRewardProfileRollbackTransaction? darenTransaction = null;

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
                byte[]? content;
                if (string.Equals(
                        fileId,
                        DarenRewardProfileExternalFileId,
                        StringComparison.Ordinal))
                {
                    darenTransaction =
                        DarenRewardProfileRollbackTransaction.Capture(
                            fs,
                            writeLease);
                    content = darenTransaction.BaselineBytes?.ToArray();
                }
                else
                {
                    content = await ReadExternalRollbackBytesAsync(
                        fs,
                        writeLease,
                        fileId);
                }

                if (content == null)
                {
                    externalEntries.Add(new BrowserWriteExternalRollbackEntry(
                        fileId,
                        Existed: false,
                        BackupPath: null,
                        Sha256: null,
                        ParentIdentity: darenTransaction?.ParentIdentity));
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
                    Sha256: ComputeSha256(content),
                    ParentIdentity: darenTransaction?.ParentIdentity,
                    BaselineIdentity: darenTransaction?.BaselineIdentity));
            }

            var manifest = new BrowserWriteRollbackManifest(
                SchemaVersion: 4,
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
            if (darenTransaction != null)
            {
                var darenEntryIndex = externalEntries.FindIndex(entry =>
                    string.Equals(
                        entry.FileId,
                        DarenRewardProfileExternalFileId,
                        StringComparison.Ordinal));
                darenTransaction.SetPublishedAuthorityRecorder(
                    async (publishedIdentity, publishedSha256, publicationTransactionId) =>
                    {
                        externalEntries[darenEntryIndex] =
                            externalEntries[darenEntryIndex] with
                            {
                                PublishedIdentity = publishedIdentity,
                                PublishedSha256 = publishedSha256,
                                PublicationTransactionId =
                                    publicationTransactionId
                            };
                        var updatedManifest =
                            new BrowserWriteRollbackManifest(
                                SchemaVersion: 4,
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
                            JsonSerializer.Serialize(
                                updatedManifest,
                                ManifestJsonOptions));
                    });
            }

            if (darenTransaction != null)
            {
                if (writeLease.ExternalPublicationContext != null)
                {
                    throw new InvalidOperationException(
                        "Canonical write lease already owns an external publication transaction.");
                }

                writeLease.ExternalPublicationContext = darenTransaction;
            }

            return new BrowserWriteRollbackTransaction(
                transactionRoot,
                manifestPath,
                safeScope,
                createdAtUtc,
                entries,
                cleanupDirectories,
                externalEntries)
            {
                DarenTransaction = darenTransaction
            };
        }
        catch
        {
            darenTransaction?.Dispose();
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

    internal static Task MarkBrowserWriteTransactionCommittedAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction)
    {
        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var darenTransaction = transaction.DarenTransaction;
        try
        {
            darenTransaction?.Commit();
        }
        catch when (
            darenTransaction?.PublicationCommitted == true)
        {
            // The durable physical marker already committed the transaction.
        }

        try
        {
            CreateBrowserWriteCommittedMarker(
                fs,
                writeLease,
                transaction);
        }
        catch when (
            darenTransaction?.PublicationCommitted == true ||
            HasBrowserWriteCommittedMarker(fs, transaction))
        {
            // A durable commit cannot be revoked by cleanup failure.
        }

        return Task.CompletedTask;
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
                manifest = StrictJsonAuthority.Deserialize<BrowserWriteRollbackManifest>(
                               DecodeUtf8(manifestBytes),
                               ManifestJsonOptions,
                               $"Browser rollback manifest '{manifestPath}'") ??
                           throw new InvalidDataException(
                               $"Browser rollback manifest '{manifestPath}' is empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Browser rollback manifest '{manifestPath}' is malformed.",
                    ex);
            }

            var validated = ValidateBrowserWriteManifest(
                fs,
                manifestPath,
                manifest);
            transactions.Add((
                validated.Transaction,
                await ResolveBrowserWriteTransactionStatusAsync(
                    fs,
                    writeLease,
                    validated.Transaction,
                    validated.Status)));
        }

        foreach (var (transaction, status) in transactions)
        {
            var cleanupOutcome = BrowserWriteCleanupOutcome.Committed;
            if (string.Equals(status, "staged", StringComparison.Ordinal))
            {
                await RestoreBrowserWriteTransactionAsync(fs, writeLease, transaction);
                cleanupOutcome = BrowserWriteCleanupOutcome.Restored;
            }
            else if (string.Equals(status, "restored", StringComparison.Ordinal))
            {
                cleanupOutcome = BrowserWriteCleanupOutcome.Restored;
            }

            if (!TryDeleteBrowserWriteTransaction(
                    fs,
                    writeLease,
                    transaction,
                    cleanupOutcome,
                    out var cleanupFailure))
            {
                throw new IOException(
                    $"Browser rollback transaction '{transaction.ManifestPath}' was resolved, but its evidence could not be cleaned.",
                    cleanupFailure);
            }
        }

        await RecoverOrphanedBrowserWriteCleanupIntentsAsync(
            fs,
            writeLease);
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
        BrowserWriteCleanupOutcome outcome,
        out Exception? failure)
    {
        try
        {
            EnsureDarenPublicationResolvedForCleanup(
                fs,
                writeLease,
                transaction);
            var cleanupIntentPath = CreateBrowserWriteCleanupIntent(
                fs,
                writeLease,
                transaction,
                outcome);
            var evidencePaths = transaction.Entries
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.BackupPath))
                .Select(static entry => entry.BackupPath!)
                .Concat(transaction.ExternalEntries
                    .Where(static entry => !string.IsNullOrWhiteSpace(entry.BackupPath))
                    .Select(static entry => entry.BackupPath!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var evidencePath in evidencePaths)
            {
                if (fs.FileExists(writeLease, evidencePath))
                    fs.DeleteFile(writeLease, evidencePath);
            }

            var committedMarkerPath =
                GetBrowserWriteCommittedMarkerPath(transaction);
            if (fs.FileExists(writeLease, committedMarkerPath))
                fs.DeleteFile(writeLease, committedMarkerPath);
            fs.DeleteEmptyDirectories(writeLease, transaction.TransactionRoot);
            var transactionRoot = fs.ResolvePath(transaction.TransactionRoot);
            var manifestFullPath = fs.ResolvePath(transaction.ManifestPath);
            var cleanupIntentFullPath = fs.ResolvePath(cleanupIntentPath);
            if (Directory.Exists(transactionRoot))
            {
                var unexpectedEvidence = Directory
                    .EnumerateFileSystemEntries(
                        transactionRoot,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(path => !string.Equals(
                        Path.GetFullPath(path),
                        Path.GetFullPath(manifestFullPath),
                        StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(
                            Path.GetFullPath(path),
                            Path.GetFullPath(cleanupIntentFullPath),
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (unexpectedEvidence.Length > 0)
                {
                    throw new IOException(
                        "Browser rollback transaction contains unknown evidence; manifest retained.");
                }
            }

            if (fs.FileExists(writeLease, transaction.ManifestPath))
                fs.DeleteFile(writeLease, transaction.ManifestPath);
            if (fs.FileExists(writeLease, cleanupIntentPath))
                fs.DeleteFile(writeLease, cleanupIntentPath);
            fs.DeleteEmptyDirectories(writeLease, transaction.TransactionRoot);
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
        if (manifest.SchemaVersion is not (1 or 2 or 3 or 4) ||
            !string.Equals(manifest.TransactionKind, "browser_local_write", StringComparison.Ordinal) ||
            manifest.Status is not ("staged" or "committed" or "restored") ||
            manifest.Status == "restored" && manifest.SchemaVersion < 3 ||
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
        if (manifest.SchemaVersion < 4 && sourceExternalEntries.Count > 0)
        {
            throw new InvalidDataException(
                $"Browser rollback manifest '{manifestPath}' declares non-authoritative external evidence under a legacy schema.");
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

            var hasPublishedAuthority =
                entry.PublishedIdentity != null ||
                !string.IsNullOrWhiteSpace(entry.PublishedSha256) ||
                !string.IsNullOrWhiteSpace(
                    entry.PublicationTransactionId);
            if (manifest.SchemaVersion < 4)
            {
                if (entry.ParentIdentity != null ||
                    entry.BaselineIdentity != null ||
                    hasPublishedAuthority)
                {
                    throw new InvalidDataException(
                        $"Browser rollback manifest '{manifestPath}' declares physical external authority under a legacy schema.");
                }
            }
            else
            {
                if (OperatingSystem.IsWindows() &&
                    entry.ParentIdentity is not
                    {
                        IsDirectory: true
                    } ||
                    entry.BaselineIdentity is
                    {
                        IsDirectory: true
                    } ||
                    entry.BaselineIdentity is
                    {
                        NumberOfLinks: not 1
                    } ||
                    entry.Existed !=
                        (entry.BaselineIdentity != null) ||
                    hasPublishedAuthority !=
                        (entry.PublishedIdentity != null &&
                         IsSha256(entry.PublishedSha256) &&
                         Guid.TryParseExact(
                             entry.PublicationTransactionId,
                             "N",
                             out _)) ||
                    entry.PublishedIdentity is
                    {
                        IsDirectory: true
                    } ||
                    entry.PublishedIdentity is
                    {
                        NumberOfLinks: not 1
                    })
                {
                    throw new InvalidDataException(
                        $"Browser rollback manifest '{manifestPath}' has incomplete external physical authority.");
                }
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
                Sha256 = entry.Sha256!.ToLowerInvariant(),
                PublishedSha256 =
                    entry.PublishedSha256?.ToLowerInvariant()
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
                if (transaction.DarenTransaction != null &&
                    string.Equals(
                        entry.FileId,
                        DarenRewardProfileExternalFileId,
                        StringComparison.Ordinal))
                {
                    transaction.DarenTransaction.RollBack();
                }
                else if (entry.ParentIdentity != null)
                {
                    var content = await ReadExternalBeforeImageAsync(
                        fs,
                        writeLease,
                        entry);
                    DarenRewardProfileRollbackTransaction
                        .RestoreRecoveredBaseline(
                            fs,
                            writeLease,
                            entry.ParentIdentity,
                            entry.BaselineIdentity,
                            entry.Sha256,
                            content,
                            entry.Existed);
                }
                else
                {
                    var content = await ReadExternalBeforeImageAsync(
                        fs,
                        writeLease,
                        entry);
                    await RestoreExternalRollbackBytesAsync(
                        fs,
                        writeLease,
                        entry.FileId,
                        content);
                }
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

        var restoredManifest = new BrowserWriteRollbackManifest(
            SchemaVersion: transaction.ExternalEntries.Any(
                static entry => entry.ParentIdentity != null)
                ? 4
                : 3,
            TransactionKind: "browser_local_write",
            Status: "restored",
            Scope: transaction.Scope,
            CreatedAtUtc: transaction.CreatedAtUtc,
            Entries: transaction.Entries,
            CleanupDirectories: transaction.CleanupDirectories,
            ExternalEntries: transaction.ExternalEntries);
        await fs.WriteFileAtomicAsync(
            writeLease,
            transaction.ManifestPath,
            JsonSerializer.Serialize(restoredManifest, ManifestJsonOptions));
    }

    private static void CreateBrowserWriteCommittedMarker(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction)
    {
        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        fs.VerifyCurrentSessionOperation(writeLease);
        var transactionRoot = fs.ResolvePath(transaction.TransactionRoot);
        using var transactionAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                fs.BasePath,
                transactionRoot,
                "Browser write commit marker");
        var markerPath = Path.Combine(
            transactionRoot,
            BrowserWriteCommittedMarkerFileName);
        if (File.Exists(markerPath))
        {
            using var existing = PhysicalFileAuthority.OpenReadFile(
                transactionAuthority,
                markerPath,
                "Browser write commit marker",
                asynchronous: false)
                ?? throw new FileNotFoundException(
                    "Browser write commit marker disappeared.",
                    markerPath);
            if (existing.Length != 0)
            {
                throw new InvalidDataException(
                    "Browser write commit marker must be empty.");
            }

            PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
                existing.SafeFileHandle,
                markerPath,
                "Browser write commit marker completion");
            return;
        }

        using var marker = PhysicalFileAuthority.CreateNewWritableFile(
            transactionAuthority,
            markerPath,
            "Browser write commit marker",
            asynchronous: false);
        marker.Flush(flushToDisk: true);
        fs.VerifyCurrentSessionOperation(writeLease);
    }

    private static async Task<string> ResolveBrowserWriteTransactionStatusAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction,
        string manifestStatus)
    {
        var committedCleanupIntent = await fs.ReadFileBytesAsync(
            writeLease,
            GetBrowserWriteCleanupIntentPath(
                transaction,
                BrowserWriteCleanupOutcome.Committed));
        var restoredCleanupIntent = await fs.ReadFileBytesAsync(
            writeLease,
            GetBrowserWriteCleanupIntentPath(
                transaction,
                BrowserWriteCleanupOutcome.Restored));
        if (committedCleanupIntent != null &&
            restoredCleanupIntent != null)
        {
            throw new InvalidDataException(
                "Browser write transaction declares conflicting cleanup outcomes.");
        }
        if (committedCleanupIntent != null)
        {
            EnsureEmptyBrowserWriteMarker(
                committedCleanupIntent,
                "Browser write committed cleanup intent");
            return "committed";
        }
        if (restoredCleanupIntent != null)
        {
            EnsureEmptyBrowserWriteMarker(
                restoredCleanupIntent,
                "Browser write restored cleanup intent");
            return "restored";
        }

        var marker = await fs.ReadFileBytesAsync(
            writeLease,
            GetBrowserWriteCommittedMarkerPath(transaction));
        if (marker != null)
        {
            if (marker.Length != 0)
            {
                throw new InvalidDataException(
                    "Browser write commit marker must be empty.");
            }

            return "committed";
        }

        var publicationTransactionId = transaction.ExternalEntries
            .Select(static entry => entry.PublicationTransactionId)
            .SingleOrDefault(static value =>
                !string.IsNullOrWhiteSpace(value));
        if (publicationTransactionId == null)
            return manifestStatus;

        var publicationState =
            ReversibleFilePublication.GetDeferredState(
                fs.BasePath,
                fs.PhysicalPublicationTransactionsRootPath,
                publicationTransactionId);
        return publicationState ==
               ReversibleFilePublication.DeferredPublicationState.Committed
            ? "committed"
            : manifestStatus;
    }

    private static string CreateBrowserWriteCleanupIntent(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction,
        BrowserWriteCleanupOutcome outcome)
    {
        fs.EnsureCanonicalWriteLeaseActive(writeLease);
        fs.VerifyCurrentSessionOperation(writeLease);
        var transactionRoot = fs.ResolvePath(transaction.TransactionRoot);
        using var transactionAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                fs.BasePath,
                transactionRoot,
                "Browser write cleanup intent");
        var intentPath = fs.ResolvePath(
            GetBrowserWriteCleanupIntentPath(transaction, outcome));
        var conflictingIntentPath = fs.ResolvePath(
            GetBrowserWriteCleanupIntentPath(
                transaction,
                outcome == BrowserWriteCleanupOutcome.Committed
                    ? BrowserWriteCleanupOutcome.Restored
                    : BrowserWriteCleanupOutcome.Committed));
        using (var conflictingIntent = PhysicalFileAuthority.OpenReadFile(
                   transactionAuthority,
                   conflictingIntentPath,
                   "Browser write conflicting cleanup intent",
                   asynchronous: false))
        {
            if (conflictingIntent != null)
            {
                throw new InvalidDataException(
                    "Browser write transaction declares conflicting cleanup outcomes.");
            }
        }

        using (var existingIntent = PhysicalFileAuthority.OpenReadFile(
                   transactionAuthority,
                   intentPath,
                   "Browser write cleanup intent",
                   asynchronous: false))
        {
            if (existingIntent != null)
            {
                if (existingIntent.Length != 0)
                {
                    throw new InvalidDataException(
                        "Browser write cleanup intent must be empty.");
                }

                PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
                    existingIntent.SafeFileHandle,
                    intentPath,
                    "Browser write cleanup intent completion");
                return GetBrowserWriteCleanupIntentPath(transaction, outcome);
            }
        }

        using var intent = PhysicalFileAuthority.CreateNewWritableFile(
            transactionAuthority,
            intentPath,
            "Browser write cleanup intent",
            asynchronous: false);
        intent.Flush(flushToDisk: true);
        fs.VerifyCurrentSessionOperation(writeLease);
        return GetBrowserWriteCleanupIntentPath(transaction, outcome);
    }

    private static async Task RecoverOrphanedBrowserWriteCleanupIntentsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var intentPaths = fs
            .EnumerateFiles(
                writeLease,
                BrowserWriteCommittedCleanupIntentFileName)
            .Concat(fs.EnumerateFiles(
                writeLease,
                BrowserWriteRestoredCleanupIntentFileName))
            .Where(path => path.StartsWith(
                $"{Root}/",
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var intentPath in intentPaths)
        {
            var content = await fs.ReadFileBytesAsync(writeLease, intentPath);
            if (content == null)
                continue;
            EnsureEmptyBrowserWriteMarker(
                content,
                "Browser write cleanup intent");

            var transactionRoot = ValidateCleanupIntentPath(fs, intentPath);
            var transactionRootPath = fs.ResolvePath(transactionRoot);
            var intentFullPath = fs.ResolvePath(intentPath);
            var unexpectedEvidence = Directory
                .EnumerateFileSystemEntries(
                    transactionRootPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path => !string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(intentFullPath),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (unexpectedEvidence.Length > 0)
            {
                throw new IOException(
                    "Browser cleanup-only transaction contains unknown evidence; intent retained.");
            }

            fs.DeleteFile(writeLease, intentPath);
            fs.DeleteEmptyDirectories(writeLease, transactionRoot);
            TryDeleteEmptyBrowserWriteParents(fs, writeLease, transactionRoot);
        }
    }

    private static string ValidateCleanupIntentPath(
        FileSystemManager fs,
        string intentPath)
    {
        var normalizedIntentPath = NormalizeRelativePath(fs, intentPath);
        var fileName = Path.GetFileName(normalizedIntentPath);
        if (fileName is not (
                BrowserWriteCommittedCleanupIntentFileName or
                BrowserWriteRestoredCleanupIntentFileName))
        {
            throw new InvalidDataException(
                "Browser write cleanup intent has an invalid filename.");
        }

        var transactionRoot = normalizedIntentPath[
            ..normalizedIntentPath.LastIndexOf("/", StringComparison.Ordinal)];
        if (!transactionRoot.StartsWith(
                $"{Root}/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Browser write cleanup intent is outside the rollback root.");
        }

        var transactionSegments = transactionRoot[(Root.Length + 1)..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (transactionSegments.Length != 2 ||
            !string.Equals(
                transactionSegments[0],
                SafeSegment(transactionSegments[0]),
                StringComparison.Ordinal) ||
            !IsValidTransactionDirectoryName(transactionSegments[1]))
        {
            throw new InvalidDataException(
                "Browser write cleanup intent is outside a valid transaction root.");
        }

        return transactionRoot;
    }

    private static void EnsureEmptyBrowserWriteMarker(
        byte[] content,
        string markerName)
    {
        if (content.Length != 0)
        {
            throw new InvalidDataException(
                $"{markerName} must be empty.");
        }
    }

    private static void EnsureDarenPublicationResolvedForCleanup(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserWriteRollbackTransaction transaction)
    {
        var entry = transaction.ExternalEntries.SingleOrDefault(
            static item => string.Equals(
                item.FileId,
                DarenRewardProfileExternalFileId,
                StringComparison.Ordinal));
        if (entry == null || entry.ParentIdentity == null)
            return;

        var publicationState =
            string.IsNullOrWhiteSpace(entry.PublicationTransactionId)
                ? ReversibleFilePublication.DeferredPublicationState.Missing
                : ReversibleFilePublication.GetDeferredState(
                    fs.BasePath,
                    fs.PhysicalPublicationTransactionsRootPath,
                    entry.PublicationTransactionId);
        var committed =
            HasBrowserWriteCommittedMarker(fs, transaction) ||
            publicationState ==
            ReversibleFilePublication.DeferredPublicationState.Committed ||
            transaction.DarenTransaction?.PublicationCommitted == true;

        if (transaction.DarenTransaction != null)
        {
            if (transaction.DarenTransaction.RetainedEvidence)
            {
                throw new InvalidDataException(
                    "Daren linked post-image evidence remains unresolved.");
            }

            if (committed &&
                transaction.DarenTransaction.PublicationCommitted)
            {
                transaction.DarenTransaction.ValidateCommittedForCleanup();
                if (!transaction.DarenTransaction
                        .TryAcknowledgeCommittedJournal())
                {
                    throw new IOException(
                        "Daren committed publication journal cleanup failed.");
                }

                return;
            }
        }

        if (publicationState ==
            ReversibleFilePublication.DeferredPublicationState.Pending)
        {
            throw new InvalidDataException(
                "Daren publication evidence remains unresolved.");
        }

        if (committed &&
            entry.PublishedIdentity != null)
        {
            DarenRewardProfileRollbackTransaction.VerifyRecoveredFileState(
                fs,
                writeLease,
                entry.ParentIdentity,
                entry.PublishedIdentity,
                entry.PublishedSha256,
                expectExistence: true,
                authorityName: "Daren committed post-image");
            if (publicationState ==
                ReversibleFilePublication.DeferredPublicationState.Committed)
            {
                ReversibleFilePublication.AcknowledgeDeferredCommit(
                    fs.BasePath,
                    fs.PhysicalPublicationTransactionsRootPath,
                    entry.PublicationTransactionId!);
            }

            return;
        }

        DarenRewardProfileRollbackTransaction.VerifyRecoveredFileState(
            fs,
            writeLease,
            entry.ParentIdentity,
            entry.Existed ? entry.BaselineIdentity : null,
            entry.Existed ? entry.Sha256 : null,
            expectExistence: entry.Existed,
            authorityName: "Daren restored baseline");
    }

    private static bool HasBrowserWriteCommittedMarker(
        FileSystemManager fs,
        BrowserWriteRollbackTransaction transaction)
    {
        var transactionRoot = fs.ResolvePath(transaction.TransactionRoot);
        using var transactionAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                fs.BasePath,
                transactionRoot,
                "Browser write commit marker");
        var markerPath = Path.Combine(
            transactionRoot,
            BrowserWriteCommittedMarkerFileName);
        using var marker = PhysicalFileAuthority.OpenReadFile(
            transactionAuthority,
            markerPath,
            "Browser write commit marker",
            asynchronous: false);
        if (marker == null)
            return false;
        if (marker.Length != 0)
        {
            throw new InvalidDataException(
                "Browser write commit marker must be empty.");
        }

        PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
            marker.SafeFileHandle,
            markerPath,
            "Browser write commit marker completion");
        return true;
    }

    private static string GetBrowserWriteCommittedMarkerPath(
        BrowserWriteRollbackTransaction transaction) =>
        $"{transaction.TransactionRoot}/{BrowserWriteCommittedMarkerFileName}";

    private static string GetBrowserWriteCleanupIntentPath(
        BrowserWriteRollbackTransaction transaction,
        BrowserWriteCleanupOutcome outcome) =>
        $"{transaction.TransactionRoot}/" +
        (outcome == BrowserWriteCleanupOutcome.Committed
            ? BrowserWriteCommittedCleanupIntentFileName
            : BrowserWriteRestoredCleanupIntentFileName);

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

    private static Task<byte[]?> ReadExternalRollbackBytesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string fileId) =>
        fileId switch
        {
            DarenRewardProfileExternalFileId =>
                QteSceneService.ReadDarenProfileRollbackBytesAsync(
                    fs,
                    writeLease),
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

    private static Task RestoreExternalRollbackBytesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
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

        return QteSceneService.RestoreDarenProfileRollbackBytesAsync(
            fs,
            writeLease,
            content);
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
            // Parent cleanup is cosmetic after transaction evidence is gone.
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
