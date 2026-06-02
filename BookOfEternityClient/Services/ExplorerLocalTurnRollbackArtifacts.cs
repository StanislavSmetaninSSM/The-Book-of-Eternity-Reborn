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
        if (string.IsNullOrWhiteSpace(trackedFile) || !fs.FileExists(trackedFile))
            return null;

        var content = await fs.ReadFileAsync(trackedFile);
        if (content == null)
            return null;

        var backupPath =
            $"{Root}/{SafeSegment(scope)}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}/{CreateSafeBackupFileName(trackedFile)}.rollback.{Guid.NewGuid():N}";
        await fs.WriteFileAtomicAsync(backupPath, content);
        return backupPath;
    }

    public static IReadOnlyList<StagedBackup> DiscoverBackups(
        FileSystemManager fs,
        IEnumerable<string> trackedFiles)
    {
        var root = fs.ResolvePath(Root);
        if (!Directory.Exists(root))
            return Array.Empty<StagedBackup>();

        var gameSessionRoot = fs.ResolvePath("");
        var backups = new List<StagedBackup>();
        foreach (var trackedFile in trackedFiles
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Select(static path => path.Replace('\\', '/').Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var safeName = CreateSafeBackupFileName(trackedFile);
            var match = Directory
                .GetFiles(root, $"{safeName}.rollback.*", SearchOption.AllDirectories)
                .Select(static path => new FileInfo(path))
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

    public static void DeleteBackup(FileSystemManager fs, string? backupPath)
    {
        if (!string.IsNullOrWhiteSpace(backupPath) && fs.FileExists(backupPath))
            fs.DeleteFile(backupPath);

        DeleteEmptyDirectories(fs);
    }

    public static void DeleteEmptyDirectories(FileSystemManager fs)
    {
        var rollbackRoot = fs.ResolvePath(Root);
        if (!Directory.Exists(rollbackRoot))
            return;

        foreach (var directory in Directory.GetDirectories(rollbackRoot, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }

        if (!Directory.EnumerateFileSystemEntries(rollbackRoot).Any())
            Directory.Delete(rollbackRoot);
    }

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
