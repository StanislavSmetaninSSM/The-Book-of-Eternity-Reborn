using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public enum CanonicalFileMutationResult
{
    Applied,
    Conflict
}

/// <summary>
/// Manages the game_session directory structure per CLI API specification.
/// Creates all required directories and validates file system integrity.
/// </summary>
public class FileSystemManager
{
    internal sealed class CanonicalWriteLease : IAsyncDisposable
    {
        private FileStream? _stream;

        internal CanonicalWriteLease(FileSystemManager owner, FileStream stream)
        {
            Owner = owner;
            _stream = stream;
        }

        internal FileSystemManager Owner { get; }
        internal bool IsActive => _stream != null;

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            _stream = null;
            if (stream != null)
                await stream.DisposeAsync();
        }
    }

    private readonly string _basePath;
    private readonly ILogger<FileSystemManager> _logger;
    private const int TransientFileAccessRetryCount = 20;
    private static readonly TimeSpan TransientFileAccessRetryDelay = TimeSpan.FromMilliseconds(50);
    private const int CanonicalWriteLockRetryCount = 200;

    private static readonly string[] RequiredDirectories =
    {
        "game_session/input",
        "game_session/game_state/core",
        "game_session/game_state/player",
        "game_session/game_state/inventory",
        "game_session/game_state/world",
        "game_session/game_state/quests",
        "game_session/game_state/npcs",
        "game_session/game_state/combat",
        "game_session/game_state/factions",
        "game_session/game_state/meta",
        "game_session/game_state/misc",
        "game_session/game_state/control",
        "game_session/game_state/history",
        "game_session/lore/chaos_sea",
        "game_session/lore/shining_abode",
        "game_session/lore/current_world",
        "game_session/mods",
        "game_session/world_profiles",
        "game_session/output",
        "game_session/ready",
        "game_session/saves/manual_saves",
        "game_session/saves/autosaves",
        "game_session/saves/checkpoint_saves",
        "game_session/stories",
        "game_session/images"
    };

    public string BasePath => _basePath;
    public string GameSessionPath => Path.Combine(_basePath, "game_session");

    public FileSystemManager(string basePath, ILogger<FileSystemManager> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public void EnsureDirectoryStructure()
    {
        foreach (var dir in RequiredDirectories)
        {
            var fullPath = Path.Combine(_basePath, dir);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogDebug("Создана директория: {Path}", dir);
            }
        }
    }

    public string ResolvePath(string relativePath)
    {
        return Path.Combine(_basePath, "game_session", relativePath);
    }

    /// <summary>
    /// Atomic write: write to temp file then rename to prevent partial writes.
    /// </summary>
    public async Task WriteFileAtomicAsync(string relativePath, string content)
    {
        var encoding = System.Text.Encoding.UTF8;
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        await WriteFileAtomicBytesAsync(relativePath, bytes);
    }

    public async Task WriteFileAtomicBytesAsync(string relativePath, byte[] content)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        await WriteFileAtomicBytesCoreAsync(relativePath, content);
    }

    public async Task AppendFileAtomicAsync(string relativePath, string content)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        var fullPath = ResolvePath(relativePath);
        byte[] currentContent;
        try
        {
            currentContent = await File.ReadAllBytesAsync(fullPath);
        }
        catch (FileNotFoundException)
        {
            currentContent = System.Text.Encoding.UTF8.GetPreamble();
        }
        catch (DirectoryNotFoundException)
        {
            currentContent = System.Text.Encoding.UTF8.GetPreamble();
        }

        var appendedContent = System.Text.Encoding.UTF8.GetBytes(content);
        var nextContent = new byte[currentContent.Length + appendedContent.Length];
        Buffer.BlockCopy(currentContent, 0, nextContent, 0, currentContent.Length);
        Buffer.BlockCopy(appendedContent, 0, nextContent, currentContent.Length, appendedContent.Length);
        await WriteFileAtomicBytesCoreAsync(relativePath, nextContent);
    }

    public async Task<CanonicalFileMutationResult> CompareExchangeFileBytesAsync(
        string relativePath,
        byte[]? expectedContent,
        byte[]? desiredContent)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        return await CompareExchangeFileBytesAsync(writeLock, relativePath, expectedContent, desiredContent);
    }

    internal async Task<CanonicalFileMutationResult> CompareExchangeFileBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[]? expectedContent,
        byte[]? desiredContent)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        var fullPath = ResolvePath(relativePath);
        byte[]? currentContent = null;
        try
        {
            currentContent = await File.ReadAllBytesAsync(fullPath);
        }
        catch (FileNotFoundException)
        {
            // A missing file is a valid expected state for an add operation.
        }
        catch (DirectoryNotFoundException)
        {
            // A missing parent is also a valid expected state for an add operation.
        }

        if (!ExactBytesEqual(currentContent, expectedContent))
            return CanonicalFileMutationResult.Conflict;

        if (desiredContent == null)
            DeleteFileCore(relativePath);
        else
            await WriteFileAtomicBytesCoreAsync(relativePath, desiredContent);

        return CanonicalFileMutationResult.Applied;
    }

    private async Task WriteFileAtomicBytesCoreAsync(string relativePath, byte[] content)
    {
        var fullPath = ResolvePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tempPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content);
                stream.Flush(flushToDisk: true);
            }
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(tempPath, fullPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
                {
                    await Task.Delay(TransientFileAccessRetryDelay);
                }
            }
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public async Task<string?> ReadFileAsync(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return null;
                return await File.ReadAllTextAsync(fullPath, System.Text.Encoding.UTF8);
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }
    }

    public async Task<byte[]?> ReadFileBytesAsync(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return null;
                return await File.ReadAllBytesAsync(fullPath);
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }
    }

    private static bool IsTransientFileAccessException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    public bool FileExists(string relativePath)
    {
        return File.Exists(ResolvePath(relativePath));
    }

    public void DeleteFile(string relativePath)
    {
        DeleteFileWithLockAsync(relativePath).GetAwaiter().GetResult();
    }

    private async Task DeleteFileWithLockAsync(string relativePath)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        DeleteFileCore(relativePath);
    }

    private void DeleteFileCore(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                return;
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }
    }

    internal async Task<CanonicalWriteLease> AcquireCanonicalWriteLeaseAsync()
    {
        var lockPath = Path.Combine(GameSessionPath, ".locks", "canonical-write.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        for (var attempt = 0; attempt < CanonicalWriteLockRetryCount; attempt++)
        {
            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
                return new CanonicalWriteLease(this, stream);
            }
            catch (IOException) when (attempt < CanonicalWriteLockRetryCount - 1)
            {
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }

        throw new IOException("Timed out waiting for the canonical game-session write lock.");
    }

    private void EnsureValidCanonicalWriteLease(CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        if (!ReferenceEquals(writeLease.Owner, this) || !writeLease.IsActive)
            throw new InvalidOperationException("Canonical write lease is not active for this game session.");
    }

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

    /// <summary>
    /// Create a backup of a file before modification.
    /// </summary>
    public string? CreateBackup(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        var backupPath = fullPath + $".backup.{DateTime.UtcNow.Ticks}";
        File.Copy(fullPath, backupPath, overwrite: true);
        return backupPath;
    }

    public void RestoreBackup(string backupFullPath, string originalRelativePath)
    {
        var originalFullPath = ResolvePath(originalRelativePath);
        if (File.Exists(backupFullPath))
        {
            File.Copy(backupFullPath, originalFullPath, overwrite: true);
            File.Delete(backupFullPath);
        }
    }

    public void CleanupBackup(string backupFullPath)
    {
        if (File.Exists(backupFullPath))
            File.Delete(backupFullPath);
    }

    /// <summary>
    /// Get all JSON files in game_state directory.
    /// </summary>
    public string[] GetAllGameStateFiles()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (!Directory.Exists(gameStatePath))
            return Array.Empty<string>();
        return Directory.GetFiles(gameStatePath, "*.json", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Clear all game state for a new game.
    /// </summary>
    public void ClearGameState()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (Directory.Exists(gameStatePath))
        {
            foreach (var file in Directory.GetFiles(gameStatePath, "*.json", SearchOption.AllDirectories))
            {
                if (ShouldPreserveAcrossGameStateClear(gameStatePath, file))
                    continue;

                File.Delete(file);
            }
        }

        // Clear output and ready
        var outputPath = Path.Combine(_basePath, "game_session", "output");
        if (Directory.Exists(outputPath))
        {
            foreach (var file in Directory.GetFiles(outputPath, "*.json"))
                File.Delete(file);
        }

        var readyPath = Path.Combine(_basePath, "game_session", "ready");
        if (Directory.Exists(readyPath))
        {
            foreach (var file in Directory.GetFiles(readyPath, "*.json"))
                File.Delete(file);
        }

        var lorePath = Path.Combine(_basePath, "game_session", "lore");
        if (Directory.Exists(lorePath))
        {
            foreach (var file in Directory.GetFiles(lorePath, "*", SearchOption.AllDirectories))
                File.Delete(file);
        }

        var storiesPath = Path.Combine(_basePath, "game_session", "stories");
        if (Directory.Exists(storiesPath))
        {
            foreach (var file in Directory.GetFiles(storiesPath, "*", SearchOption.AllDirectories))
                File.Delete(file);
        }

        // Re-create structure
        EnsureDirectoryStructure();
    }

    private static bool ShouldPreserveAcrossGameStateClear(string gameStatePath, string filePath)
    {
        var relative = Path.GetRelativePath(gameStatePath, filePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return relative.Equals("control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
               relative.Equals("control/gm_cli_window_binding.json", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("control/gm_context_pack/", StringComparison.OrdinalIgnoreCase);
    }

    public void ClearCurrentWorldLore()
    {
        var currentWorldPath = Path.Combine(_basePath, "game_session", "lore", "current_world");
        if (!Directory.Exists(currentWorldPath))
            return;

        foreach (var file in Directory.GetFiles(currentWorldPath, "*", SearchOption.AllDirectories))
        {
            if (file.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(file);
        }
    }
}
