using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class ExplorerLocalTurnRollbackArtifacts
{
    public const string Root = "game_state/control/explorer_local_turn_rollback";

    public sealed record StagedBackup(string TrackedFile, string BackupPath);

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

    internal static IReadOnlyList<StagedBackup> DiscoverBackups(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<string> trackedFiles)
    {
        var availableBackups = fs.EnumerateFiles(writeLease, "*")
            .Where(path => path.StartsWith($"{Root}/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var backups = new List<StagedBackup>();
        foreach (var trackedFile in trackedFiles
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path.Replace('\\', '/').Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var safeName = CreateSafeBackupFileName(trackedFile);
            var match = availableBackups
                .Where(path => Path.GetFileName(path)
                    .StartsWith($"{safeName}.rollback.", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => File.GetLastWriteTimeUtc(fs.ResolvePath(path)))
                .ThenByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (match == null)
                continue;

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
