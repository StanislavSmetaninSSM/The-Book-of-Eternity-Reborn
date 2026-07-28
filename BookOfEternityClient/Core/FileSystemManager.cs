using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Core;

internal sealed class FileSystemManagerHooks
{
    internal Func<Task>? CanonicalWriteLockContendedAsync { get; init; }
    internal Func<Task>? SessionLifecycleLockContendedAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalReadOpenAsync { get; init; }
    internal Func<Task>? SessionOperationClosingAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationBoundaryAsync { get; init; }
}

public enum CanonicalFileMutationResult
{
    Applied,
    Conflict
}

internal enum CanonicalWritePurpose
{
    SessionMutation,
    SessionReplacement,
    SessionFinalization
}

internal sealed record CanonicalLoadTransactionPaths(
    string TransactionId,
    string TransactionRoot,
    string StagingSessionPath,
    string BackupSessionPath,
    string FailedSessionPath);

internal sealed record CanonicalWorkerApplyChange(
    string Path,
    byte[]? BaselineBytes,
    byte[]? AppliedBytes);

internal sealed record CanonicalWorkerApplyTransaction(
    string TransactionId,
    string TransactionRoot);

/// <summary>
/// Manages the game_session directory structure per CLI API specification.
/// Creates all required directories and validates file system integrity.
/// </summary>
public class FileSystemManager
{
    internal sealed class SessionLifecycleLease : IAsyncDisposable
    {
        private FileStream? _stream;

        internal SessionLifecycleLease(FileSystemManager owner, FileStream stream)
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

    internal sealed class CanonicalWriteLease : IAsyncDisposable
    {
        private FileStream? _stream;

        internal CanonicalWriteLease(
            FileSystemManager owner,
            FileStream stream,
            CanonicalWritePurpose purpose)
        {
            Owner = owner;
            Purpose = purpose;
            _stream = stream;
        }

        internal FileSystemManager Owner { get; }
        internal CanonicalWritePurpose Purpose { get; }
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
    private readonly ILoadTransactionOperations _loadTransactionOperations;
    private readonly FileSystemManagerHooks? _hooks;
    private const int TransientFileAccessRetryCount = 20;
    private static readonly TimeSpan TransientFileAccessRetryDelay = TimeSpan.FromMilliseconds(50);
    private const int CanonicalWriteLockRetryCount = 200;
    private const int SessionLifecycleLockRetryCount = 200;

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
    internal string CanonicalWriteLockPath =>
        Path.Combine(_basePath, ".boe_runtime", "locks", "canonical-write.lock");
    internal string SessionLifecycleLockPath =>
        Path.Combine(_basePath, ".boe_runtime", "locks", "session-lifecycle.lock");
    internal string ActiveLoadTransactionJournalPath =>
        Path.Combine(_basePath, ".boe_runtime", "load-transactions", "active.json");
    internal string SessionGenerationPath =>
        Path.Combine(_basePath, ".boe_runtime", "session-generation", "current.json");
    internal string ActiveWorkerApplyTransactionJournalPath =>
        Path.Combine(_basePath, ".boe_runtime", "worker-apply-transactions", "active.json");

    public FileSystemManager(string basePath, ILogger<FileSystemManager> logger)
        : this(basePath, logger, PhysicalLoadTransactionOperations.Instance, hooks: null)
    {
    }

    internal FileSystemManager(
        string basePath,
        ILogger<FileSystemManager> logger,
        ILoadTransactionOperations loadTransactionOperations)
        : this(basePath, logger, loadTransactionOperations, hooks: null)
    {
    }

    internal FileSystemManager(
        string basePath,
        ILogger<FileSystemManager> logger,
        ILoadTransactionOperations loadTransactionOperations,
        FileSystemManagerHooks? hooks)
    {
        _basePath = ResolvePhysicalBasePath(basePath);
        _logger = logger;
        _loadTransactionOperations = loadTransactionOperations ??
            throw new ArgumentNullException(nameof(loadTransactionOperations));
        _hooks = hooks;
    }

    public void EnsureDirectoryStructure()
    {
        var writeLease = AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        try
        {
            RecoverInterruptedLoadTransaction(writeLease);
            EnsureDirectoryStructureCore();
        }
        finally
        {
            writeLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    internal void EnsureDirectoryStructure(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureDirectoryStructureCore();
    }

    private void EnsureDirectoryStructureCore()
    {
        foreach (var dir in RequiredDirectories)
        {
            var relativePath = dir.Equals("game_session", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : dir["game_session/".Length..];
            var fullPath = ResolvePath(relativePath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogDebug("Создана директория: {Path}", dir);
            }
        }
    }

    public string ResolvePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var sessionRoot = Path.GetFullPath(GameSessionPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalized = relativePath.Trim().Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
            throw new InvalidDataException("Canonical game-session path must be relative.");

        if (normalized.Length > 0)
        {
            var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);
            if (segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment is "." or ".."))
            {
                throw new InvalidDataException("Canonical game-session path contains an invalid segment.");
            }
        }

        var fullPath = Path.GetFullPath(Path.Combine(sessionRoot, normalized));
        var sessionPrefix = sessionRoot + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, sessionRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(sessionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Canonical game-session path escapes game_session.");
        }

        EnsureNoExistingReparsePoint(sessionRoot, fullPath);
        return fullPath;
    }

    private static string ResolvePhysicalBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("Base path is required.", nameof(basePath));

        var requestedPath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(requestedPath);
        var root = Path.GetPathRoot(requestedPath)
            ?? throw new InvalidDataException("Base path has no filesystem root.");
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, requestedPath)
                     .Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
                continue;

            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            var target = info.ResolveLinkTarget(returnFinalTarget: true)
                ?? throw new InvalidDataException($"Cannot resolve physical base-path alias '{current}'.");
            current = Path.GetFullPath(target.FullName);
        }

        return Path.GetFullPath(current).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static void EnsureNoExistingReparsePoint(string sessionRoot, string fullPath)
    {
        var current = sessionRoot;
        if (Directory.Exists(current) && IsReparsePoint(current))
            throw new InvalidDataException("Canonical game_session root cannot be a reparse point.");

        var relative = Path.GetRelativePath(sessionRoot, fullPath);
        if (relative == ".")
            return;

        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
                break;
            if (IsReparsePoint(current))
            {
                throw new InvalidDataException(
                    $"Canonical game-session path traverses reparse point '{current}'.");
            }
        }
    }

    /// <summary>
    /// Atomic write: write to temp file then rename to prevent partial writes.
    /// </summary>
    public async Task WriteFileAtomicAsync(string relativePath, string content)
    {
        await WriteFileAtomicBytesAsync(relativePath, EncodeUtf8WithPreamble(content));
    }

    internal async Task WriteFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        await WriteFileAtomicBytesCoreAsync(relativePath, EncodeUtf8WithPreamble(content));
    }

    public async Task WriteFileAtomicBytesAsync(string relativePath, byte[] content)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        await WriteFileAtomicBytesCoreAsync(relativePath, content);
    }

    internal async Task WriteFileAtomicBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        byte[] content)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        await WriteFileAtomicBytesCoreAsync(relativePath, content);
    }

    public async Task AppendFileAtomicAsync(string relativePath, string content)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        await AppendFileAtomicAsync(writeLock, relativePath, content);
    }

    internal async Task AppendFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content)
    {
        await AppendFileAtomicAsync(
            writeLease,
            relativePath,
            content,
            CancellationToken.None);
    }

    internal async Task AppendFileAtomicAsync(
        CanonicalWriteLease writeLease,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        cancellationToken.ThrowIfCancellationRequested();
        var currentContent = await ReadFileBytesCoreAsync(relativePath, cancellationToken) ??
                             Encoding.UTF8.GetPreamble();

        var appendedContent = Encoding.UTF8.GetBytes(content);
        var nextContent = new byte[currentContent.Length + appendedContent.Length];
        Buffer.BlockCopy(currentContent, 0, nextContent, 0, currentContent.Length);
        Buffer.BlockCopy(appendedContent, 0, nextContent, currentContent.Length, appendedContent.Length);
        await WriteFileAtomicBytesCoreAsync(relativePath, nextContent, cancellationToken);
    }

    internal async Task<bool> AppendFileAtomicIfCurrentSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration)
    {
        return await AppendFileAtomicIfCurrentSessionAsync(
            relativePath,
            content,
            expectedSessionGeneration,
            CancellationToken.None);
    }

    internal async Task<bool> AppendFileAtomicIfCurrentSessionAsync(
        string relativePath,
        string content,
        string expectedSessionGeneration,
        CancellationToken cancellationToken)
    {
        await using var writeLease = await AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
        if (!IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            return false;

        await AppendFileAtomicAsync(
            writeLease,
            relativePath,
            content,
            cancellationToken);
        return true;
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
        var currentContent = await ReadFileBytesCoreAsync(relativePath);

        if (!ExactBytesEqual(currentContent, expectedContent))
            return CanonicalFileMutationResult.Conflict;

        if (desiredContent == null)
            DeleteFileCore(relativePath);
        else
            await WriteFileAtomicBytesCoreAsync(relativePath, desiredContent);

        return CanonicalFileMutationResult.Applied;
    }

    private Task WriteFileAtomicBytesCoreAsync(string relativePath, byte[] content) =>
        WriteFileAtomicBytesCoreAsync(relativePath, content, CancellationToken.None);

    private async Task WriteFileAtomicBytesCoreAsync(
        string relativePath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        EnsureCanonicalPathStillSafe(relativePath, fullPath);

        var tempRelativePath = relativePath + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        var tempPath = ResolvePath(tempRelativePath);
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
                await stream.WriteAsync(content, cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            await InvokeBeforeCanonicalMutationBoundaryAsync(relativePath);
            cancellationToken.ThrowIfCancellationRequested();

            for (var attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    EnsureCanonicalMutationBoundary(relativePath, fullPath);
                    File.Move(tempPath, fullPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
                {
                    await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                }
            }
        }
        catch
        {
            TryDeleteAtomicTempFileWithoutFollowingReparsePoints(tempRelativePath, tempPath);
            throw;
        }
    }

    private void EnsureCanonicalMutationBoundary(string relativePath, string expectedFullPath)
    {
        var revalidatedFullPath = ResolvePath(relativePath);
        if (!string.Equals(
                revalidatedFullPath,
                expectedFullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical game-session path identity changed before mutation.");
        }
    }

    private void TryDeleteAtomicTempFileWithoutFollowingReparsePoints(
        string tempRelativePath,
        string expectedTempPath)
    {
        try
        {
            var revalidatedTempPath = ResolvePath(tempRelativePath);
            if (string.Equals(
                    revalidatedTempPath,
                    expectedTempPath,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(revalidatedTempPath))
            {
                File.Delete(revalidatedTempPath);
            }
        }
        catch
        {
            // Retaining an unreachable temp file is safer than following a replaced path.
        }
    }

    public async Task<string?> ReadFileAsync(string relativePath)
    {
        return await ReadFileCoreAsync(relativePath);
    }

    internal async Task<string?> ReadFileAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return await ReadFileCoreAsync(relativePath);
    }

    private async Task<string?> ReadFileCoreAsync(string relativePath)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var stream = await OpenCanonicalReadStreamAsync(relativePath);
                if (stream == null)
                    return null;

                await using (stream)
                using (var reader = new StreamReader(
                           stream,
                           Encoding.UTF8,
                           detectEncodingFromByteOrderMarks: true,
                           leaveOpen: false))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }
    }

    public async Task<byte[]?> ReadFileBytesAsync(string relativePath)
    {
        return await ReadFileBytesCoreAsync(relativePath, CancellationToken.None);
    }

    internal async Task<byte[]?> ReadFileBytesAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        return await ReadFileBytesCoreAsync(relativePath, cancellationToken);
    }

    internal async Task<byte[]?> ReadFileBytesAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return await ReadFileBytesCoreAsync(relativePath, CancellationToken.None);
    }

    private Task<byte[]?> ReadFileBytesCoreAsync(string relativePath) =>
        ReadFileBytesCoreAsync(relativePath, CancellationToken.None);

    private async Task<byte[]?> ReadFileBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = await OpenCanonicalReadStreamAsync(relativePath, cancellationToken);
                if (stream == null)
                    return null;

                await using (stream)
                {
                    using var buffer = stream.Length is > 0 and <= int.MaxValue
                        ? new MemoryStream((int)stream.Length)
                        : new MemoryStream();
                    await stream.CopyToAsync(buffer, cancellationToken);
                    return buffer.ToArray();
                }
            }
            catch (Exception ex) when (IsTransientFileAccessException(ex) && attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
            }
        }
    }

    private static bool IsTransientFileAccessException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    public bool FileExists(string relativePath)
    {
        return File.Exists(ResolvePath(relativePath));
    }

    internal bool FileExists(CanonicalWriteLease writeLease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return File.Exists(ResolvePath(relativePath));
    }

    internal IReadOnlyList<string> EnumerateFiles(
        CanonicalWriteLease writeLease,
        string searchPattern)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (string.IsNullOrWhiteSpace(searchPattern))
            throw new ArgumentException("Search pattern is required.", nameof(searchPattern));

        return EnumerateFilesWithoutFollowingReparsePoints(GameSessionPath, searchPattern)
            .Select(path => Path.GetRelativePath(GameSessionPath, path).Replace('\\', '/'))
            .ToArray();
    }

    internal void DeleteDirectoryTree(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureSafeCanonicalRelativePath(relativePath);
        var fullPath = ResolvePath(relativePath);
        InvokeBeforeCanonicalMutationBoundaryAsync(relativePath).GetAwaiter().GetResult();
        EnsureCanonicalMutationBoundary(relativePath, fullPath);
        DeleteDirectoryTreeWithoutFollowingReparsePoints(fullPath);
    }

    internal async Task MoveRuntimeDirectoryIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        string sourceDirectoryPath,
        string destinationRelativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
        EnsureSafeCanonicalRelativePath(destinationRelativePath);

        var stagingRoot = Path.GetFullPath(Path.Combine(
            _basePath,
            ".boe_runtime",
            "proposal-staging"));
        var sourcePath = Path.GetFullPath(sourceDirectoryPath);
        if (!IsSameOrDescendant(sourcePath, stagingRoot) ||
            string.Equals(sourcePath, stagingRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime directory move source must be inside proposal staging.");
        }

        EnsureNoExistingReparsePoint(stagingRoot, sourcePath);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(
                $"Runtime staging directory does not exist: {sourcePath}");

        var destinationPath = ResolvePath(destinationRelativePath);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("Canonical destination has no parent directory.");
        Directory.CreateDirectory(destinationParent);
        EnsureCanonicalPathStillSafe(destinationRelativePath, destinationPath);

        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        EnsureNoExistingReparsePoint(stagingRoot, sourcePath);
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        Directory.Move(sourcePath, destinationPath);
    }

    internal void DeleteEmptyDirectories(
        CanonicalWriteLease writeLease,
        string relativeRoot)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureSafeCanonicalRelativePath(relativeRoot);
        DeleteEmptyDirectoriesWithoutFollowingReparsePoints(ResolvePath(relativeRoot));
    }

    public void DeleteFile(string relativePath)
    {
        DeleteFileWithLockAsync(relativePath).GetAwaiter().GetResult();
    }

    internal void DeleteFile(CanonicalWriteLease writeLease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        DeleteFileCore(relativePath);
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
                InvokeBeforeCanonicalMutationBoundaryAsync(relativePath)
                    .GetAwaiter()
                    .GetResult();
                EnsureCanonicalMutationBoundary(relativePath, fullPath);
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

    internal async Task<CanonicalWriteLease> AcquireCanonicalWriteLeaseAsync(
        CanonicalWritePurpose purpose = CanonicalWritePurpose.SessionMutation,
        CancellationToken cancellationToken = default)
    {
        if (purpose == CanonicalWritePurpose.SessionReplacement)
        {
            throw new InvalidOperationException(
                "Session replacement writes require an active session lifecycle lease.");
        }

        return await AcquireCanonicalWriteLeaseCoreAsync(purpose, cancellationToken);
    }

    internal async Task<CanonicalWriteLease> AcquireSessionReplacementWriteLeaseAsync(
        SessionLifecycleLease lifecycleLease,
        CancellationToken cancellationToken = default)
    {
        EnsureValidSessionLifecycleLease(lifecycleLease);
        return await AcquireCanonicalWriteLeaseCoreAsync(
            CanonicalWritePurpose.SessionReplacement,
            cancellationToken);
    }

    private async Task<CanonicalWriteLease> AcquireCanonicalWriteLeaseCoreAsync(
        CanonicalWritePurpose purpose,
        CancellationToken cancellationToken)
    {
        EnsureCanonicalSessionRootIsNotReparsePoint();
        var lockPath = CanonicalWriteLockPath;
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        for (var attempt = 0; attempt < CanonicalWriteLockRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileStream stream;
            try
            {
                stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException) when (attempt < CanonicalWriteLockRetryCount - 1)
            {
                if (_hooks?.CanonicalWriteLockContendedAsync != null)
                    await _hooks.CanonicalWriteLockContendedAsync();
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                continue;
            }

            var writeLease = new CanonicalWriteLease(this, stream, purpose);
            try
            {
                RecoverInterruptedLoadTransaction(writeLease);
                await RecoverInterruptedWorkerApplyTransactionAsync(writeLease);
                if (purpose == CanonicalWritePurpose.SessionMutation)
                {
                    await ExplorerLocalTurnRollbackArtifacts
                        .RecoverInterruptedBrowserWriteTransactionsAsync(this, writeLease);
                }

                if (purpose == CanonicalWritePurpose.SessionMutation)
                    EnsureBoundSessionOperationCanWrite(writeLease);
                return writeLease;
            }
            catch
            {
                await writeLease.DisposeAsync();
                throw;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new IOException("Timed out waiting for the canonical game-session write lock.");
    }

    private void EnsureBoundSessionOperationCanWrite(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (!SessionOperationContext.TryGetExpectedGeneration(_basePath, out var expectedGeneration))
            return;

        var actualGeneration = _loadTransactionOperations.FileExists(SessionGenerationPath)
            ? ReadSessionGeneration()
            : null;
        if (string.Equals(expectedGeneration, actualGeneration, StringComparison.Ordinal))
            return;

        throw SessionOperationContext.MarkReplaced(
            _basePath,
            actualGeneration,
            "The game session was replaced while an older operation was still running.");
    }

    internal async Task VerifyCurrentSessionOperationAsync()
    {
        if (!SessionOperationContext.TryGetExpectedGeneration(_basePath, out _))
            return;

        await using var writeLease = await AcquireCanonicalWriteLeaseAsync();
    }

    internal void VerifyCurrentSessionOperation(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureBoundSessionOperationCanWrite(writeLease);
    }

    internal Task InvokeSessionOperationClosingHookAsync() =>
        _hooks?.SessionOperationClosingAsync?.Invoke() ?? Task.CompletedTask;

    internal async Task<SessionLifecycleLease> AcquireSessionLifecycleLeaseAsync()
    {
        EnsureCanonicalSessionRootIsNotReparsePoint();
        var lockPath = SessionLifecycleLockPath;
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        for (var attempt = 0; attempt < SessionLifecycleLockRetryCount; attempt++)
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
                return new SessionLifecycleLease(this, stream);
            }
            catch (IOException) when (attempt < SessionLifecycleLockRetryCount - 1)
            {
                if (_hooks?.SessionLifecycleLockContendedAsync != null)
                    await _hooks.SessionLifecycleLockContendedAsync();
                await Task.Delay(TransientFileAccessRetryDelay);
            }
        }

        throw new IOException("Timed out waiting for the game-session lifecycle lock.");
    }

    internal async Task<SessionLifecycleLease?> TryAcquireSessionLifecycleLeaseAsync(
        string expectedSessionGeneration)
    {
        var lifecycleLease = await AcquireSessionLifecycleLeaseAsync();
        try
        {
            await using var writeLease = await AcquireCanonicalWriteLeaseAsync();
            if (!IsCurrentSessionGeneration(writeLease, expectedSessionGeneration))
            {
                await lifecycleLease.DisposeAsync();
                return null;
            }

            return lifecycleLease;
        }
        catch
        {
            await lifecycleLease.DisposeAsync();
            throw;
        }
    }

    private void EnsureValidCanonicalWriteLease(CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        if (!ReferenceEquals(writeLease.Owner, this) || !writeLease.IsActive)
            throw new InvalidOperationException("Canonical write lease is not active for this game session.");
    }

    internal void EnsureCanonicalWriteLeaseActive(CanonicalWriteLease writeLease) =>
        EnsureValidCanonicalWriteLease(writeLease);

    private void EnsureValidSessionReplacementLease(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (writeLease.Purpose != CanonicalWritePurpose.SessionReplacement)
        {
            throw new InvalidOperationException(
                "This operation requires a session replacement write lease.");
        }
    }

    private void EnsureValidSessionLifecycleLease(SessionLifecycleLease lifecycleLease)
    {
        ArgumentNullException.ThrowIfNull(lifecycleLease);
        if (!ReferenceEquals(lifecycleLease.Owner, this) || !lifecycleLease.IsActive)
            throw new InvalidOperationException("Session lifecycle lease is not active for this game session.");
    }

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

    private static byte[] EncodeUtf8WithPreamble(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    internal CanonicalLoadTransactionPaths GetLoadTransactionPaths(string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _))
            throw new ArgumentException("Load transaction ID must be a GUID in N format.", nameof(transactionId));

        var transactionRoot = Path.Combine(
            _basePath,
            ".boe_runtime",
            "load-transactions",
            transactionId);
        return new CanonicalLoadTransactionPaths(
            transactionId,
            transactionRoot,
            Path.Combine(transactionRoot, "stage", "game_session"),
            Path.Combine(transactionRoot, "backup", "game_session"),
            Path.Combine(transactionRoot, "failed", "game_session"));
    }

    internal string BeginLoadTransaction(CanonicalWriteLease writeLease, string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        RecoverInterruptedLoadTransaction(writeLease);
        var previousGenerationId = _loadTransactionOperations.FileExists(SessionGenerationPath)
            ? ReadSessionGeneration()
            : null;
        var replacementGenerationId = Guid.NewGuid().ToString("N");
        WriteLoadTransactionJournal(
            new LoadTransactionJournal(
                SchemaVersion: 2,
                TransactionId: transactionId,
                Committed: false,
                PreviousGenerationId: previousGenerationId,
                ReplacementGenerationId: replacementGenerationId));
        return replacementGenerationId;
    }

    internal void ActivateLoadTransactionSession(
        CanonicalWriteLease writeLease,
        string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        var journal = ReadLoadTransactionJournal();
        if (!string.Equals(journal.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(journal.ReplacementGenerationId))
        {
            throw new InvalidDataException("Active load transaction replacement authority is invalid.");
        }

        WriteSessionGeneration(journal.ReplacementGenerationId);
        DeleteWorkerSessionArtifactsCore();
    }

    internal void CommitLoadTransaction(CanonicalWriteLease writeLease, string transactionId)
    {
        EnsureValidSessionReplacementLease(writeLease);
        var journal = ReadLoadTransactionJournal();
        if (!string.Equals(journal.TransactionId, transactionId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(journal.ReplacementGenerationId) ||
            !IsCurrentSessionGeneration(writeLease, journal.ReplacementGenerationId))
        {
            throw new InvalidDataException("Active load transaction generation was not activated.");
        }

        WriteLoadTransactionJournal(journal with { Committed = true });

        try
        {
            CleanupCommittedLoadTransaction(GetLoadTransactionPaths(transactionId));
        }
        catch (Exception ex)
        {
            // The committed journal makes cleanup retryable at the next startup.
            _logger.LogWarning(ex, "Не удалось сразу очистить завершённую транзакцию загрузки {TransactionId}.", transactionId);
        }
    }

    internal void RecoverInterruptedLoadTransaction(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (!_loadTransactionOperations.FileExists(ActiveLoadTransactionJournalPath))
            return;

        var journal = ReadLoadTransactionJournal();
        var paths = GetLoadTransactionPaths(journal.TransactionId);

        if (!journal.Committed || !_loadTransactionOperations.DirectoryExists(GameSessionPath))
        {
            RestoreLoadTransactionBackup(paths);
            if (journal.SchemaVersion >= 2)
                RestoreLoadTransactionGeneration(journal.PreviousGenerationId);
        }
        else if (!string.IsNullOrWhiteSpace(journal.ReplacementGenerationId))
        {
            WriteSessionGeneration(journal.ReplacementGenerationId);
        }

        CleanupCommittedLoadTransaction(paths);
        _logger.LogWarning(
            "Восстановлена незавершённая транзакция загрузки {TransactionId} (committed={Committed}).",
            journal.TransactionId,
            journal.Committed);
    }

    internal void CleanupInactiveLoadTransaction(CanonicalLoadTransactionPaths paths)
    {
        if (ActiveLoadTransactionReferences(paths.TransactionId))
            return;

        if (_loadTransactionOperations.DirectoryExists(paths.TransactionRoot))
            _loadTransactionOperations.DeleteDirectory(paths.TransactionRoot, recursive: true);
    }

    internal bool LoadDirectoryExists(string path) => _loadTransactionOperations.DirectoryExists(path);
    internal void CreateLoadDirectory(string path) => _loadTransactionOperations.CreateDirectory(path);
    internal void MoveLoadDirectory(string sourcePath, string destinationPath) =>
        _loadTransactionOperations.MoveDirectory(sourcePath, destinationPath);

    private void RestoreLoadTransactionBackup(CanonicalLoadTransactionPaths paths)
    {
        if (!_loadTransactionOperations.DirectoryExists(paths.BackupSessionPath))
            return;

        if (_loadTransactionOperations.DirectoryExists(GameSessionPath))
        {
            if (_loadTransactionOperations.DirectoryExists(paths.FailedSessionPath))
                _loadTransactionOperations.DeleteDirectory(paths.FailedSessionPath, recursive: true);

            _loadTransactionOperations.CreateDirectory(Path.GetDirectoryName(paths.FailedSessionPath)!);
            _loadTransactionOperations.MoveDirectory(GameSessionPath, paths.FailedSessionPath);
        }

        _loadTransactionOperations.CreateDirectory(Path.GetDirectoryName(GameSessionPath)!);
        _loadTransactionOperations.MoveDirectory(paths.BackupSessionPath, GameSessionPath);
    }

    private void CleanupCommittedLoadTransaction(CanonicalLoadTransactionPaths paths)
    {
        if (_loadTransactionOperations.DirectoryExists(paths.TransactionRoot))
            _loadTransactionOperations.DeleteDirectory(paths.TransactionRoot, recursive: true);
        if (_loadTransactionOperations.FileExists(ActiveLoadTransactionJournalPath))
            _loadTransactionOperations.DeleteFile(ActiveLoadTransactionJournalPath);
    }

    private void WriteLoadTransactionJournal(LoadTransactionJournal journal)
    {
        var json = JsonSerializer.Serialize(journal);
        _loadTransactionOperations.WriteAllTextAtomic(ActiveLoadTransactionJournalPath, json);
    }

    private void RestoreLoadTransactionGeneration(string? previousGenerationId)
    {
        if (string.IsNullOrWhiteSpace(previousGenerationId))
        {
            if (_loadTransactionOperations.FileExists(SessionGenerationPath))
                _loadTransactionOperations.DeleteFile(SessionGenerationPath);
            return;
        }

        WriteSessionGeneration(previousGenerationId);
    }

    private LoadTransactionJournal ReadLoadTransactionJournal()
    {
        try
        {
            var journal = JsonSerializer.Deserialize<LoadTransactionJournal>(
                _loadTransactionOperations.ReadAllText(ActiveLoadTransactionJournalPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (journal is null ||
                journal.SchemaVersion is < 1 or > 2 ||
                !Guid.TryParseExact(journal.TransactionId, "N", out _) ||
                !IsOptionalCanonicalGenerationId(journal.PreviousGenerationId) ||
                !IsOptionalCanonicalGenerationId(journal.ReplacementGenerationId) ||
                (journal.SchemaVersion == 2 && string.IsNullOrWhiteSpace(journal.ReplacementGenerationId)))
            {
                throw new InvalidDataException("Active load transaction journal is invalid.");
            }

            return journal;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Active load transaction journal is invalid.", ex);
        }
    }

    private bool ActiveLoadTransactionReferences(string transactionId)
    {
        if (!_loadTransactionOperations.FileExists(ActiveLoadTransactionJournalPath))
            return false;

        try
        {
            return string.Equals(
                ReadLoadTransactionJournal().TransactionId,
                transactionId,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Never delete recovery evidence when the journal cannot be interpreted.
            return true;
        }
    }

    private static bool IsOptionalCanonicalGenerationId(string? generationId) =>
        string.IsNullOrWhiteSpace(generationId) ||
        Guid.TryParseExact(generationId, "N", out var parsedGeneration) &&
        string.Equals(generationId, parsedGeneration.ToString("N"), StringComparison.Ordinal);

    private sealed record LoadTransactionJournal(
        int SchemaVersion,
        string TransactionId,
        bool Committed,
        string? PreviousGenerationId = null,
        string? ReplacementGenerationId = null);

    internal async Task<CanonicalWorkerApplyTransaction> BeginWorkerApplyTransactionAsync(
        CanonicalWriteLease writeLease,
        IReadOnlyList<CanonicalWorkerApplyChange> changes)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (changes.Count == 0)
            throw new ArgumentException("Worker apply transaction requires at least one changed file.", nameof(changes));
        if (_loadTransactionOperations.FileExists(ActiveWorkerApplyTransactionJournalPath))
            throw new InvalidOperationException("An active worker apply transaction already exists.");

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        var entries = new List<WorkerApplyTransactionEntry>(changes.Count);

        try
        {
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                EnsureSafeCanonicalRelativePath(change.Path);
                var beforeImage = change.BaselineBytes == null
                    ? null
                    : $"before/{index:D4}.bin";
                if (beforeImage != null)
                {
                    await WriteExternalBytesAtomicAsync(
                        Path.Combine(transactionRoot, beforeImage.Replace('/', Path.DirectorySeparatorChar)),
                        change.BaselineBytes!);
                }

                entries.Add(new WorkerApplyTransactionEntry
                {
                    Path = change.Path.Replace('\\', '/'),
                    BaselineExists = change.BaselineBytes != null,
                    BeforeImage = beforeImage,
                    BeforeSha256 = ComputeSha256OrMissing(change.BaselineBytes),
                    AppliedSha256 = ComputeSha256OrMissing(change.AppliedBytes)
                });
            }

            var manifest = new WorkerApplyTransactionManifest
            {
                TransactionId = transactionId,
                Entries = entries
            };
            _loadTransactionOperations.WriteAllTextAtomic(
                Path.Combine(transactionRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest));
            WriteWorkerApplyJournal(transactionId, committed: false, rolledBack: false);
            return new CanonicalWorkerApplyTransaction(transactionId, transactionRoot);
        }
        catch
        {
            if (!_loadTransactionOperations.FileExists(ActiveWorkerApplyTransactionJournalPath) &&
                Directory.Exists(transactionRoot))
            {
                Directory.Delete(transactionRoot, recursive: true);
            }

            throw;
        }
    }

    internal void CommitWorkerApplyTransaction(
        CanonicalWriteLease writeLease,
        CanonicalWorkerApplyTransaction transaction)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        EnsureActiveWorkerApplyTransaction(transaction.TransactionId);
        WriteWorkerApplyJournal(transaction.TransactionId, committed: true, rolledBack: false);
        try
        {
            CleanupWorkerApplyTransaction(transaction.TransactionId);
        }
        catch (Exception ex)
        {
            // The committed journal makes cleanup retryable without revoking accepted canonical bytes.
            _logger.LogWarning(
                ex,
                "Не удалось сразу очистить завершённую worker apply транзакцию {TransactionId}.",
                transaction.TransactionId);
        }
    }

    internal async Task<IReadOnlyList<string>> RollbackWorkerApplyTransactionAsync(
        CanonicalWriteLease writeLease,
        CanonicalWorkerApplyTransaction transaction)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        try
        {
            EnsureActiveWorkerApplyTransaction(transaction.TransactionId);
            await RecoverInterruptedWorkerApplyTransactionAsync(writeLease);
            return [];
        }
        catch (Exception ex)
        {
            return [$"worker apply rollback remains pending: {ex.Message}"];
        }
    }

    private async Task RecoverInterruptedWorkerApplyTransactionAsync(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (!_loadTransactionOperations.FileExists(ActiveWorkerApplyTransactionJournalPath))
            return;

        var journal = ReadWorkerApplyJournal();
        var transactionRoot = GetWorkerApplyTransactionRoot(journal.TransactionId);
        if (journal.Committed || journal.RolledBack)
        {
            CleanupWorkerApplyTransaction(journal.TransactionId);
            return;
        }

        var manifestPath = Path.Combine(transactionRoot, "manifest.json");
        WorkerApplyTransactionManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<WorkerApplyTransactionManifest>(
                           _loadTransactionOperations.ReadAllText(manifestPath),
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                       throw new InvalidDataException("Worker apply transaction manifest is missing.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Worker apply transaction manifest is invalid.", ex);
        }

        if (manifest.SchemaVersion != 1 ||
            !string.Equals(manifest.TransactionId, journal.TransactionId, StringComparison.Ordinal) ||
            manifest.Entries.Count == 0 ||
            manifest.Entries
                .GroupBy(entry => entry.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Worker apply transaction manifest is invalid.");
        }

        var errors = new List<string>();
        foreach (var entry in manifest.Entries.AsEnumerable().Reverse())
        {
            try
            {
                EnsureSafeCanonicalRelativePath(entry.Path);
                var baseline = ReadWorkerApplyBeforeImage(transactionRoot, entry);
                var current = await ReadFileBytesCoreAsync(
                    entry.Path,
                    CancellationToken.None);
                var currentHash = ComputeSha256OrMissing(current);
                if (string.Equals(currentHash, entry.BeforeSha256, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(currentHash, entry.AppliedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"worker apply recovery found unowned canonical bytes: {entry.Path}.");
                    continue;
                }

                if (baseline == null)
                    DeleteFileCore(entry.Path);
                else
                    await WriteFileAtomicBytesCoreAsync(entry.Path, baseline);
            }
            catch (Exception ex)
            {
                errors.Add($"worker apply recovery failed for {entry.Path}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                "Interrupted worker apply transaction could not be fully recovered: " +
                string.Join(" | ", errors));
        }

        WriteWorkerApplyJournal(journal.TransactionId, committed: false, rolledBack: true);
        CleanupWorkerApplyTransaction(journal.TransactionId);
    }

    private byte[]? ReadWorkerApplyBeforeImage(
        string transactionRoot,
        WorkerApplyTransactionEntry entry)
    {
        if (!entry.BaselineExists)
        {
            if (!string.IsNullOrWhiteSpace(entry.BeforeImage) ||
                !string.Equals(entry.BeforeSha256, "missing", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Missing worker baseline has an invalid before-image contract.");
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.BeforeImage) ||
            !entry.BeforeImage.StartsWith("before/", StringComparison.Ordinal) ||
            entry.BeforeImage.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Worker apply before-image path is invalid.");
        }

        var beforePath = Path.GetFullPath(Path.Combine(
            transactionRoot,
            entry.BeforeImage.Replace('/', Path.DirectorySeparatorChar)));
        var expectedRoot = Path.GetFullPath(transactionRoot) + Path.DirectorySeparatorChar;
        if (!beforePath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(beforePath))
            throw new InvalidDataException("Worker apply before-image is missing or escapes its transaction.");

        var bytes = ReadExactBytesFromStablePath(beforePath);
        if (!string.Equals(ComputeSha256OrMissing(bytes), entry.BeforeSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Worker apply before-image hash is invalid.");
        return bytes;
    }

    private void EnsureActiveWorkerApplyTransaction(string expectedTransactionId)
    {
        var journal = ReadWorkerApplyJournal();
        if (!string.Equals(journal.TransactionId, expectedTransactionId, StringComparison.Ordinal))
            throw new InvalidOperationException("Active worker apply transaction identity changed.");
    }

    private WorkerApplyTransactionJournal ReadWorkerApplyJournal()
    {
        try
        {
            var journal = JsonSerializer.Deserialize<WorkerApplyTransactionJournal>(
                _loadTransactionOperations.ReadAllText(ActiveWorkerApplyTransactionJournalPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (journal is null || journal.SchemaVersion != 1 ||
                (journal.Committed && journal.RolledBack) ||
                !Guid.TryParseExact(journal.TransactionId, "N", out _))
            {
                throw new InvalidDataException("Active worker apply transaction journal is invalid.");
            }

            return journal;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Active worker apply transaction journal is invalid.", ex);
        }
    }

    private void WriteWorkerApplyJournal(
        string transactionId,
        bool committed,
        bool rolledBack)
    {
        var journal = new WorkerApplyTransactionJournal
        {
            TransactionId = transactionId,
            Committed = committed,
            RolledBack = rolledBack
        };
        _loadTransactionOperations.WriteAllTextAtomic(
            ActiveWorkerApplyTransactionJournalPath,
            JsonSerializer.Serialize(journal));
    }

    private void CleanupWorkerApplyTransaction(string transactionId)
    {
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        if (_loadTransactionOperations.DirectoryExists(transactionRoot))
            _loadTransactionOperations.DeleteDirectory(transactionRoot, recursive: true);
        if (_loadTransactionOperations.FileExists(ActiveWorkerApplyTransactionJournalPath))
            _loadTransactionOperations.DeleteFile(ActiveWorkerApplyTransactionJournalPath);
    }

    private string GetWorkerApplyTransactionRoot(string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _))
            throw new InvalidDataException("Worker apply transaction ID is invalid.");
        return Path.Combine(_basePath, ".boe_runtime", "worker-apply-transactions", transactionId);
    }

    private void EnsureSafeCanonicalRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("Worker apply transaction path is invalid.");

        _ = ResolvePath(relativePath);
    }

    private void EnsureCanonicalSessionRootIsNotReparsePoint()
    {
        var sessionRoot = Path.GetFullPath(GameSessionPath);
        if (!Directory.Exists(sessionRoot) && !File.Exists(sessionRoot))
            return;

        if ((File.GetAttributes(sessionRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Canonical game_session root must not be a reparse-point alias.");
        }
    }

    private async Task<FileStream?> OpenCanonicalReadStreamAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedFullPath = ResolvePath(relativePath);
        if (!File.Exists(expectedFullPath))
            return null;

        await InvokeBeforeCanonicalReadOpenAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();

        FileStream stream;
        try
        {
            stream = new FileStream(
                expectedFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            return null;
        }

        try
        {
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            if (OperatingSystem.IsWindows())
            {
                var openedPath = GetFinalPath(stream.SafeFileHandle);
                if (!string.Equals(
                        NormalizeWindowsHandlePath(openedPath),
                        Path.GetFullPath(expectedFullPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Canonical game-session file identity changed before read.");
                }
            }

            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private async Task InvokeBeforeCanonicalMutationBoundaryAsync(string relativePath)
    {
        if (_hooks?.BeforeCanonicalMutationAsync != null)
            await _hooks.BeforeCanonicalMutationAsync(relativePath);
        if (_hooks?.BeforeCanonicalMutationBoundaryAsync != null)
            await _hooks.BeforeCanonicalMutationBoundaryAsync(relativePath);
    }

    private Task InvokeBeforeCanonicalReadOpenAsync(string relativePath) =>
        _hooks?.BeforeCanonicalReadOpenAsync?.Invoke(relativePath) ?? Task.CompletedTask;

    private void EnsureCanonicalPathStillSafe(string relativePath, string expectedFullPath)
    {
        var currentFullPath = ResolvePath(relativePath);
        if (!string.Equals(currentFullPath, expectedFullPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Canonical game-session path identity changed before mutation.");
    }

    private static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidatePath, rootPath, comparison))
            return true;

        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ||
                                rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }


    private static string ComputeSha256OrMissing(byte[]? content) =>
        content == null
            ? "missing"
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

    private static async Task WriteExternalBytesAtomicAsync(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];
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
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private sealed record WorkerApplyTransactionJournal
    {
        public int SchemaVersion { get; init; } = 1;
        public string TransactionId { get; init; } = "";
        public bool Committed { get; init; }
        public bool RolledBack { get; init; }
    }

    private sealed record WorkerApplyTransactionManifest
    {
        public int SchemaVersion { get; init; } = 1;
        public string TransactionId { get; init; } = "";
        public IReadOnlyList<WorkerApplyTransactionEntry> Entries { get; init; } = [];
    }

    private sealed record WorkerApplyTransactionEntry
    {
        public string Path { get; init; } = "";
        public bool BaselineExists { get; init; }
        public string? BeforeImage { get; init; }
        public string BeforeSha256 { get; init; } = "";
        public string AppliedSha256 { get; init; } = "";
    }

    internal string GetOrCreateSessionGeneration(CanonicalWriteLease writeLease)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (_loadTransactionOperations.FileExists(SessionGenerationPath))
            return ReadSessionGeneration();

        var generationId = Guid.NewGuid().ToString("N");
        WriteSessionGeneration(generationId);
        return generationId;
    }

    internal bool IsCurrentSessionGeneration(
        CanonicalWriteLease writeLease,
        string? expectedGenerationId)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        if (string.IsNullOrWhiteSpace(expectedGenerationId) ||
            !_loadTransactionOperations.FileExists(SessionGenerationPath))
        {
            return false;
        }

        return string.Equals(
            ReadSessionGeneration(),
            expectedGenerationId,
            StringComparison.Ordinal);
    }

    internal string RotateSessionGeneration(CanonicalWriteLease writeLease)
    {
        EnsureValidSessionReplacementLease(writeLease);
        var generationId = Guid.NewGuid().ToString("N");
        WriteSessionGeneration(generationId);
        DeleteWorkerSessionArtifactsCore();
        return generationId;
    }

    private string ReadSessionGeneration()
    {
        try
        {
            var document = JsonSerializer.Deserialize<SessionGenerationDocument>(
                _loadTransactionOperations.ReadAllText(SessionGenerationPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document is null || document.SchemaVersion != 1 ||
                !Guid.TryParseExact(document.GenerationId, "N", out var parsedGeneration) ||
                !string.Equals(
                    document.GenerationId,
                    parsedGeneration.ToString("N"),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Session generation authority is invalid.");
            }

            return document.GenerationId;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Session generation authority is invalid.", ex);
        }
    }

    private void WriteSessionGeneration(string generationId)
    {
        var json = JsonSerializer.Serialize(new SessionGenerationDocument(1, generationId));
        _loadTransactionOperations.WriteAllTextAtomic(SessionGenerationPath, json);
    }

    private void DeleteWorkerSessionArtifactsCore()
    {
        DeleteDirectoryTreeWithoutFollowingReparsePoints(ResolvePath("worker_tasks"));
        DeleteDirectoryTreeWithoutFollowingReparsePoints(ResolvePath("worker_proposals"));
        DeleteFileCore("game_state/control/gm_worker_latest_validation_repair_task.json");
        DeleteFileCore("game_state/control/validation_repair_ready.json");
    }

    private static void DeleteDirectoryTreeWithoutFollowingReparsePoints(string path)
    {
        if (!Directory.Exists(path))
            return;

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path, recursive: false);
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly))
        {
            var childAttributes = File.GetAttributes(child);
            if ((childAttributes & FileAttributes.ReparsePoint) != 0)
            {
                if ((childAttributes & FileAttributes.Directory) != 0)
                    Directory.Delete(child, recursive: false);
                else
                    File.Delete(child);
            }
            else if ((childAttributes & FileAttributes.Directory) != 0)
            {
                DeleteDirectoryTreeWithoutFollowingReparsePoints(child);
            }
            else
            {
                if ((childAttributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(child, childAttributes & ~FileAttributes.ReadOnly);
                File.Delete(child);
            }
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        Directory.Delete(path, recursive: false);
    }

    private static void DeleteEmptyDirectoriesWithoutFollowingReparsePoints(string path)
    {
        if (!Directory.Exists(path))
            return;

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing to traverse reparse-point directory '{path}'.");

        foreach (var child in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            var childAttributes = File.GetAttributes(child);
            if ((childAttributes & FileAttributes.ReparsePoint) != 0)
                continue;

            DeleteEmptyDirectoriesWithoutFollowingReparsePoints(child);
        }

        if (!Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly).Any())
            Directory.Delete(path, recursive: false);
    }

    private sealed record SessionGenerationDocument(int SchemaVersion, string GenerationId);

    /// <summary>
    /// Create a backup of a file before modification.
    /// </summary>
    public string? CreateBackup(string relativePath)
    {
        return CreateBackupAsync(relativePath).GetAwaiter().GetResult();
    }

    public async Task<string?> CreateBackupAsync(string relativePath)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        return CreateBackupCore(relativePath);
    }

    internal string? CreateBackup(CanonicalWriteLease writeLease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return CreateBackupCore(relativePath);
    }

    private string? CreateBackupCore(string relativePath)
    {
        var content = ReadFileBytesCoreAsync(relativePath).GetAwaiter().GetResult();
        if (content == null)
            return null;

        var normalizedRelativePath = GetCanonicalRelativePath(ResolvePath(relativePath));
        var backupRelativePath =
            normalizedRelativePath + $".backup.{DateTime.UtcNow.Ticks}";
        WriteFileAtomicBytesCoreAsync(backupRelativePath, content).GetAwaiter().GetResult();
        return ResolvePath(backupRelativePath);
    }

    public void RestoreBackup(string backupFullPath, string originalRelativePath)
    {
        RestoreBackupAsync(backupFullPath, originalRelativePath).GetAwaiter().GetResult();
    }

    public async Task RestoreBackupAsync(string backupFullPath, string originalRelativePath)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        RestoreBackupCore(backupFullPath, originalRelativePath);
    }

    internal void RestoreBackup(
        CanonicalWriteLease writeLease,
        string backupFullPath,
        string originalRelativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        RestoreBackupCore(backupFullPath, originalRelativePath);
    }

    private void RestoreBackupCore(string backupFullPath, string originalRelativePath)
    {
        var backupRelativePath = GetCanonicalRelativePath(backupFullPath);
        var content = ReadFileBytesCoreAsync(backupRelativePath).GetAwaiter().GetResult();
        if (content == null)
        {
            throw new FileNotFoundException(
                "Canonical rollback before-image is missing.",
                backupFullPath);
        }

        WriteFileAtomicBytesCoreAsync(originalRelativePath, content).GetAwaiter().GetResult();
        DeleteFileCore(backupRelativePath);
    }

    public void CleanupBackup(string backupFullPath)
    {
        CleanupBackupAsync(backupFullPath).GetAwaiter().GetResult();
    }

    public async Task CleanupBackupAsync(string backupFullPath)
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        CleanupBackupCore(backupFullPath);
    }

    internal void CleanupBackup(CanonicalWriteLease writeLease, string backupFullPath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        CleanupBackupCore(backupFullPath);
    }

    private void CleanupBackupCore(string backupFullPath)
    {
        var backupRelativePath = GetCanonicalRelativePath(backupFullPath);
        DeleteFileCore(backupRelativePath);
    }

    private string GetCanonicalRelativePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new InvalidDataException("Canonical backup path is required.");

        var canonicalRoot = Path.GetFullPath(GameSessionPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        if (!IsSameOrDescendant(normalizedFullPath, canonicalRoot) ||
            string.Equals(normalizedFullPath, canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical backup path must stay inside game_session.");
        }

        var relativePath = Path.GetRelativePath(canonicalRoot, normalizedFullPath)
            .Replace('\\', '/');
        var resolvedPath = ResolvePath(relativePath);
        if (!string.Equals(
                resolvedPath,
                normalizedFullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Canonical backup path identity is invalid.");
        }

        return relativePath;
    }

    /// <summary>
    /// Get all JSON files in game_state directory.
    /// </summary>
    public string[] GetAllGameStateFiles()
    {
        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (!Directory.Exists(gameStatePath))
            return Array.Empty<string>();
        return EnumerateFilesWithoutFollowingReparsePoints(gameStatePath, "*.json").ToArray();
    }

    /// <summary>
    /// Clear all game state for a new game.
    /// </summary>
    public void ClearGameState()
    {
        ClearGameStateAsync().GetAwaiter().GetResult();
    }

    public async Task ClearGameStateAsync()
    {
        await using var lifecycleLease = await AcquireSessionLifecycleLeaseAsync();
        _ = await ClearGameStateAsync(lifecycleLease);
    }

    internal async Task<string> ClearGameStateAsync(SessionLifecycleLease lifecycleLease)
    {
        EnsureValidSessionLifecycleLease(lifecycleLease);
        await using var writeLock = await AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);
        var sessionGeneration = RotateSessionGeneration(writeLock);
        ClearGameStateCore();
        return sessionGeneration;
    }

    private void ClearGameStateCore()
    {
        var inputPath = Path.Combine(_basePath, "game_session", "input");
        if (Directory.Exists(inputPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(inputPath, "*.json"))
                File.Delete(file);
        }

        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        if (Directory.Exists(gameStatePath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(gameStatePath, "*"))
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
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(outputPath, "*.json"))
                File.Delete(file);
        }

        var readyPath = Path.Combine(_basePath, "game_session", "ready");
        if (Directory.Exists(readyPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(readyPath, "*.json"))
                File.Delete(file);
        }

        var lorePath = Path.Combine(_basePath, "game_session", "lore");
        if (Directory.Exists(lorePath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(lorePath, "*"))
                File.Delete(file);
        }

        var storiesPath = Path.Combine(_basePath, "game_session", "stories");
        if (Directory.Exists(storiesPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(storiesPath, "*"))
                File.Delete(file);
        }

        // Re-create structure
        EnsureDirectoryStructureCore();
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
        ClearCurrentWorldLoreAsync().GetAwaiter().GetResult();
    }

    public async Task ClearCurrentWorldLoreAsync()
    {
        await using var writeLock = await AcquireCanonicalWriteLeaseAsync();
        ClearCurrentWorldLoreCore();
    }

    private void ClearCurrentWorldLoreCore()
    {
        var currentWorldPath = Path.Combine(_basePath, "game_session", "lore", "current_world");
        if (!Directory.Exists(currentWorldPath))
            return;

        foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(currentWorldPath, "*"))
        {
            if (file.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(file);
        }
    }

    internal static IEnumerable<string> EnumerateFilesWithoutFollowingReparsePoints(
        string rootPath,
        string searchPattern)
    {
        if (!Directory.Exists(rootPath) || IsReparsePoint(rootPath))
            yield break;

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var file in Directory.EnumerateFiles(
                         currentDirectory,
                         searchPattern,
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsReparsePoint(file))
                    yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(
                         currentDirectory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsReparsePoint(directory))
                    pendingDirectories.Push(directory);
            }
        }
    }

    internal static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var capacity = 512;
        while (true)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(
                handle,
                buffer,
                (uint)buffer.Capacity,
                0);
            if (length == 0)
                throw new IOException(
                    "Could not resolve the opened canonical file handle.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            if (length < buffer.Capacity)
                return buffer.ToString();

            capacity = checked((int)length + 1);
        }
    }

    private static string NormalizeWindowsHandlePath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(@"\\" + path[8..]);
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(path[4..]);
        return Path.GetFullPath(path);
    }

    private static byte[] ReadExactBytesFromStablePath(string expectedFullPath)
    {
        var normalizedExpectedPath = Path.GetFullPath(expectedFullPath);
        using var stream = new FileStream(
            normalizedExpectedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (OperatingSystem.IsWindows() &&
            !string.Equals(
                NormalizeWindowsHandlePath(GetFinalPath(stream.SafeFileHandle)),
                normalizedExpectedPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Recovery evidence file identity changed before read.");
        }

        using var buffer = stream.Length is > 0 and <= int.MaxValue
            ? new MemoryStream((int)stream.Length)
            : new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}
