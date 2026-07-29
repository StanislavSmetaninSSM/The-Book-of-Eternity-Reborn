using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

internal sealed class FileSystemManagerHooks
{
    internal Func<Task>? CanonicalWriteLockContendedAsync { get; init; }
    internal Func<Task>? SessionLifecycleLockContendedAsync { get; init; }
    internal Func<Task>? BeforeCanonicalWriteLockOpenAsync { get; init; }
    internal Func<Task>? AfterCanonicalWriteLockOpenedAsync { get; init; }
    internal Func<Task>? BeforeSessionLifecycleLockOpenAsync { get; init; }
    internal Func<Task>? AfterSessionLifecycleLockOpenedAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalReadOpenAsync { get; init; }
    internal Func<Task>? SessionOperationClosingAsync { get; init; }
    internal Func<string, Task>? BeforeCanonicalMutationBoundaryAsync { get; init; }
    internal Func<string, Task>? AfterCanonicalMutationBoundaryValidatedAsync { get; init; }
    internal Func<string, Task>? BeforeRuntimeFileReadOpenAsync { get; init; }
    internal Func<string, Task>? AfterRuntimeFileReadOpenedAsync { get; init; }
    internal Func<string, Task>? BeforeRuntimeFileCreateAsync { get; init; }
    internal Func<string, Task>? AfterRuntimeMutationBoundaryValidatedAsync { get; init; }
    internal Func<string, string, Task>? BeforeLoadDirectoryMoveAsync { get; init; }
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
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal SessionLifecycleLease(
            FileSystemManager owner,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority)
        {
            Owner = owner;
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal FileSystemManager Owner { get; }
        internal bool IsActive =>
            _stream != null &&
            _parentAuthority != null;

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            try
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }
            finally
            {
                parentAuthority?.Dispose();
            }
        }
    }

    internal sealed class CanonicalWriteLease : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal CanonicalWriteLease(
            FileSystemManager owner,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority,
            CanonicalWritePurpose purpose)
        {
            Owner = owner;
            Purpose = purpose;
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal FileSystemManager Owner { get; }
        internal CanonicalWritePurpose Purpose { get; }
        internal bool IsActive =>
            _stream != null &&
            _parentAuthority != null;

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            try
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }
            finally
            {
                parentAuthority?.Dispose();
            }
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
    internal string RuntimeRootPath => Path.Combine(_basePath, ".boe_runtime");
    internal string CanonicalWriteLockPath =>
        Path.Combine(RuntimeRootPath, "locks", "canonical-write.lock");
    internal string SessionLifecycleLockPath =>
        Path.Combine(RuntimeRootPath, "locks", "session-lifecycle.lock");
    internal string ActiveLoadTransactionJournalPath =>
        Path.Combine(RuntimeRootPath, "load-transactions", "active.json");
    internal string SessionGenerationPath =>
        Path.Combine(RuntimeRootPath, "session-generation", "current.json");
    internal string ActiveWorkerApplyTransactionJournalPath =>
        Path.Combine(RuntimeRootPath, "worker-apply-transactions", "active.json");

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
            var existed = Directory.Exists(fullPath);
            using (PhysicalFileAuthority.EnsureStableDirectory(
                       _basePath,
                       fullPath,
                       "Canonical directory structure"))
            {
                if (!existed)
                {
                    _logger.LogDebug("Создана директория: {Path}", dir);
                }
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

        EnsureNoExistingReparsePoint(
            sessionRoot,
            fullPath,
            "Canonical game-session");
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

    private static void EnsureNoExistingReparsePoint(
        string authorityRoot,
        string fullPath,
        string authorityName)
    {
        var current = authorityRoot;
        if (Directory.Exists(current) && IsReparsePoint(current))
            throw new InvalidDataException($"{authorityName} root cannot be a reparse point.");

        var relative = Path.GetRelativePath(authorityRoot, fullPath);
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
                    $"{authorityName} path traverses reparse point '{current}'.");
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
        await InvokeBeforeCanonicalMutationBoundaryAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        using var parentAuthority = EnsureStableCanonicalParent(
            relativePath,
            fullPath);
        var tempRelativePath = relativePath + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        var tempPath = ResolvePath(tempRelativePath);
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                tempPath,
                "Canonical atomic temporary",
                asynchronous: true);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);

            for (var attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    EnsureCanonicalMutationBoundary(relativePath, fullPath);
                    await InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath);
                    if (OperatingSystem.IsWindows())
                    {
                        PhysicalFileAuthority.ValidateExistingReplacementTarget(
                            parentAuthority,
                            fullPath,
                            "Canonical atomic write");
                        PhysicalFileAuthority.RenameOpenedObject(
                            stream.SafeFileHandle,
                            fullPath,
                            replaceExisting: true,
                            "Canonical atomic write");
                    }
                    else
                    {
                        File.Move(tempPath, fullPath, overwrite: true);
                    }

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
            if (stream != null)
            {
                await stream.DisposeAsync();
                stream = null;
            }

            TryDeleteAtomicTempFileWithoutFollowingReparsePoints(
                parentAuthority,
                tempRelativePath,
                tempPath);
            throw;
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
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
        PhysicalFileAuthority.StableDirectory parentAuthority,
        string tempRelativePath,
        string expectedTempPath)
    {
        try
        {
            var revalidatedTempPath = ResolvePath(tempRelativePath);
            if (string.Equals(
                    revalidatedTempPath,
                    expectedTempPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    revalidatedTempPath,
                    "Canonical atomic temporary cleanup");
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
        var snapshot = await ReadFileSnapshotCoreAsync(
            relativePath,
            CancellationToken.None);
        if (snapshot == null)
            return null;

        using var stream = new MemoryStream(snapshot.Content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
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

    internal async Task<CanonicalFileReadSnapshot?> ReadFileSnapshotAsync(
        CanonicalWriteLease writeLease,
        string relativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        return await ReadFileSnapshotCoreAsync(
            relativePath,
            CancellationToken.None);
    }

    internal string? ReadFileSync(string relativePath)
    {
        var snapshot = ReadFileSnapshotCore(relativePath);
        if (snapshot == null)
            return null;

        using var stream = new MemoryStream(snapshot.Content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal byte[]? ReadFileBytesSync(string relativePath) =>
        ReadFileSnapshotCore(relativePath)?.Content;

    private Task<byte[]?> ReadFileBytesCoreAsync(string relativePath) =>
        ReadFileBytesCoreAsync(relativePath, CancellationToken.None);

    private async Task<byte[]?> ReadFileBytesCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var snapshot = await ReadFileSnapshotCoreAsync(
            relativePath,
            cancellationToken);
        return snapshot?.Content;
    }

    private async Task<CanonicalFileReadSnapshot?> ReadFileSnapshotCoreAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var openedFile = await OpenCanonicalReadStreamAsync(
                    relativePath,
                    cancellationToken);
                if (openedFile == null)
                    return null;

                await using (openedFile)
                {
                    var stream = openedFile.Stream;
                    var lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                        stream.SafeFileHandle);
                    using var buffer = stream.Length is > 0 and <= int.MaxValue
                        ? new MemoryStream((int)stream.Length)
                        : new MemoryStream();
                    await stream.CopyToAsync(buffer, cancellationToken);
                    PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                        stream.SafeFileHandle,
                        ResolvePath(relativePath),
                        "Canonical game-session read completion");
                    return new CanonicalFileReadSnapshot(
                        buffer.ToArray(),
                        lastWriteTimeUtc);
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
        using var parentAuthority = EnsureStableCanonicalParent(
            relativePath,
            fullPath);
        EnsureCanonicalMutationBoundary(relativePath, fullPath);
        InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath)
            .GetAwaiter()
            .GetResult();
        PhysicalFileAuthority.TryDeleteTree(
            parentAuthority,
            fullPath,
            "Canonical directory-tree deletion");
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
            RuntimeRootPath,
            "proposal-staging"));
        var sourcePath = Path.GetFullPath(sourceDirectoryPath);
        if (!IsSameOrDescendant(sourcePath, stagingRoot) ||
            string.Equals(sourcePath, stagingRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime directory move source must be inside proposal staging.");
        }

        EnsureRuntimePathIsSafe(sourcePath);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(
                $"Runtime staging directory does not exist: {sourcePath}");

        var destinationPath = ResolvePath(destinationRelativePath);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("Canonical destination has no parent directory.");
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        EnsureRuntimePathIsSafe(sourcePath);
        using var sourceParentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidDataException(
                "Runtime staging directory source has no parent."));
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        using var sourceHandle = OperatingSystem.IsWindows()
            ? PhysicalFileAuthority.OpenForRename(
                sourceParentAuthority,
                sourcePath,
                isDirectory: true,
                "Runtime proposal publication")
            : null;
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        if (OperatingSystem.IsWindows())
        {
            PhysicalFileAuthority.RenameOpenedObject(
                sourceHandle!,
                destinationPath,
                replaceExisting: false,
                "Runtime proposal publication");
        }
        else
        {
            Directory.Move(sourcePath, destinationPath);
        }
    }

    private CanonicalFileReadSnapshot? ReadFileSnapshotCore(
        string relativePath)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var expectedFullPath = ResolvePath(relativePath);
                if (!File.Exists(expectedFullPath))
                    return null;

                using var parentAuthority = EnsureStableCanonicalParent(
                    relativePath,
                    expectedFullPath);
                EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
                using var stream = PhysicalFileAuthority.OpenReadFile(
                    parentAuthority,
                    expectedFullPath,
                    "Canonical synchronous game-session read",
                    asynchronous: false);
                if (stream == null)
                    return null;

                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(
                    stream.SafeFileHandle);
                using var buffer = stream.Length is > 0 and <= int.MaxValue
                    ? new MemoryStream((int)stream.Length)
                    : new MemoryStream();
                stream.CopyTo(buffer);
                PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    expectedFullPath,
                    "Canonical synchronous game-session read completion");
                return new CanonicalFileReadSnapshot(
                    buffer.ToArray(),
                    lastWriteTimeUtc);
            }
            catch (Exception ex) when (
                IsTransientFileAccessException(ex) &&
                attempt < TransientFileAccessRetryCount)
            {
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }
    }

    internal sealed record CanonicalFileReadSnapshot(
        byte[] Content,
        DateTime LastWriteTimeUtc);

    internal sealed class StableReadFile : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal StableReadFile(
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority)
        {
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal FileStream Stream => _stream ??
            throw new ObjectDisposedException(nameof(StableReadFile));

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority?.Dispose();
        }
    }

    internal sealed class RuntimeStagedFile : IAsyncDisposable
    {
        private FileStream? _stream;
        private PhysicalFileAuthority.StableDirectory? _parentAuthority;

        internal RuntimeStagedFile(
            FileSystemManager owner,
            string path,
            FileStream stream,
            PhysicalFileAuthority.StableDirectory parentAuthority)
        {
            Owner = owner;
            Path = System.IO.Path.GetFullPath(path);
            _stream = stream;
            _parentAuthority = parentAuthority;
        }

        internal FileSystemManager Owner { get; }
        internal string Path { get; }
        internal FileStream Stream => _stream ??
            throw new ObjectDisposedException(nameof(RuntimeStagedFile));
        internal PhysicalFileAuthority.StableDirectory ParentAuthority =>
            _parentAuthority ??
            throw new ObjectDisposedException(nameof(RuntimeStagedFile));

        public async ValueTask DisposeAsync()
        {
            var stream = _stream;
            var parentAuthority = _parentAuthority;
            _stream = null;
            _parentAuthority = null;
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority?.Dispose();
        }
    }

    internal string CreateRuntimeProposalStagingRoot()
        => CreateRuntimeStagingRoot("proposal-staging");

    internal void DeleteRuntimeProposalStagingRoot(string stagingRoot)
        => DeleteRuntimeStagingRoot("proposal-staging", stagingRoot);

    internal string CreateRuntimeSaveStagingRoot()
        => CreateRuntimeStagingRoot("save-staging");

    internal void DeleteRuntimeSaveStagingRoot(string stagingRoot)
        => DeleteRuntimeStagingRoot("save-staging", stagingRoot);

    internal async Task WriteRuntimeStagedFileAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime staging file has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        using var parentAuthority = EnsureStableRuntimeDirectory(parent);
        if (_hooks?.BeforeRuntimeFileCreateAsync != null)
            await _hooks.BeforeRuntimeFileCreateAsync(path);

        await using var stream = PhysicalFileAuthority.CreateNewWritableFile(
            parentAuthority,
            path,
            "Runtime staging",
            asynchronous: true);
        await stream.WriteAsync(content, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        EnsureRuntimePathIsSafe(path);
    }

    internal async Task<RuntimeStagedFile> CreateRuntimeStagedFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime staged file has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        var parentAuthority = EnsureStableRuntimeDirectory(parent);
        FileStream? stream = null;
        try
        {
            if (_hooks?.BeforeRuntimeFileCreateAsync != null)
                await _hooks.BeforeRuntimeFileCreateAsync(path);
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                path,
                "Runtime staged file",
                asynchronous: true);
            return new RuntimeStagedFile(
                this,
                path,
                stream,
                parentAuthority);
        }
        catch
        {
            if (stream != null)
                await stream.DisposeAsync();
            parentAuthority.Dispose();
            throw;
        }
    }

    internal async Task MoveRuntimeFileIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        RuntimeStagedFile stagedFile,
        string destinationRelativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        ArgumentNullException.ThrowIfNull(stagedFile);
        if (!ReferenceEquals(stagedFile.Owner, this))
        {
            throw new InvalidOperationException(
                "Runtime staged file belongs to another file-system authority.");
        }

        EnsureSafeCanonicalRelativePath(destinationRelativePath);
        EnsureRuntimePathIsSafe(stagedFile.Path);
        var saveStagingRoot = Path.GetFullPath(Path.Combine(
            RuntimeRootPath,
            "save-staging"));
        if (!IsSameOrDescendant(stagedFile.Path, saveStagingRoot) ||
            string.Equals(
                stagedFile.Path,
                saveStagingRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime save source must be inside save staging.");
        }

        var destinationPath = ResolvePath(destinationRelativePath);
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
            stagedFile.Stream.SafeFileHandle,
            stagedFile.Path,
            "Runtime staged save");
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        await stagedFile.Stream.FlushAsync();
        stagedFile.Stream.Flush(flushToDisk: true);
        if (OperatingSystem.IsWindows())
        {
            PhysicalFileAuthority.RenameOpenedObject(
                stagedFile.Stream.SafeFileHandle,
                destinationPath,
                replaceExisting: false,
                "Runtime save publication");
        }
        else
        {
            File.Move(stagedFile.Path, destinationPath);
        }

        await stagedFile.DisposeAsync();
    }

    internal async Task MoveRuntimeFileIntoCanonicalSessionAsync(
        CanonicalWriteLease writeLease,
        string sourceFilePath,
        string destinationRelativePath)
    {
        EnsureValidCanonicalWriteLease(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
        EnsureSafeCanonicalRelativePath(destinationRelativePath);

        var saveStagingRoot = Path.GetFullPath(Path.Combine(
            RuntimeRootPath,
            "save-staging"));
        var sourcePath = Path.GetFullPath(sourceFilePath);
        if (!IsSameOrDescendant(sourcePath, saveStagingRoot) ||
            string.Equals(
                sourcePath,
                saveStagingRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime save source must be inside save staging.");
        }

        EnsureRuntimePathIsSafe(sourcePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Runtime staged save does not exist.", sourcePath);

        var destinationPath = ResolvePath(destinationRelativePath);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("Canonical save destination has no parent directory.");
        await InvokeBeforeCanonicalMutationBoundaryAsync(destinationRelativePath);
        EnsureRuntimePathIsSafe(sourcePath);
        using var sourceParentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidDataException(
                "Runtime staged save source has no parent."));
        using var destinationParentAuthority = EnsureStableCanonicalParent(
            destinationRelativePath,
            destinationPath);
        using var sourceHandle = OperatingSystem.IsWindows()
            ? PhysicalFileAuthority.OpenForRename(
                sourceParentAuthority,
                sourcePath,
                isDirectory: false,
                "Runtime save publication")
            : null;
        EnsureCanonicalMutationBoundary(destinationRelativePath, destinationPath);
        await InvokeAfterCanonicalMutationBoundaryValidatedAsync(destinationRelativePath);
        if (OperatingSystem.IsWindows())
        {
            PhysicalFileAuthority.RenameOpenedObject(
                sourceHandle!,
                destinationPath,
                replaceExisting: false,
                "Runtime save publication");
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }
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
                using var parentAuthority = EnsureStableCanonicalParent(
                    relativePath,
                    fullPath);
                EnsureCanonicalMutationBoundary(relativePath, fullPath);
                InvokeAfterCanonicalMutationBoundaryValidatedAsync(relativePath)
                    .GetAwaiter()
                    .GetResult();
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    fullPath,
                    "Canonical file deletion");
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
        var lockParentPath = Path.GetDirectoryName(lockPath)
            ?? throw new InvalidDataException(
                "Canonical write lock has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(lockParentPath);
        for (var attempt = 0; attempt < CanonicalWriteLockRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRuntimePathIsSafe(lockPath);
            if (_hooks?.BeforeCanonicalWriteLockOpenAsync != null)
                await _hooks.BeforeCanonicalWriteLockOpenAsync();
            var parentAuthority = EnsureStableRuntimeDirectory(lockParentPath);
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
                parentAuthority.Dispose();
                if (_hooks?.CanonicalWriteLockContendedAsync != null)
                    await _hooks.CanonicalWriteLockContendedAsync();
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                continue;
            }
            catch
            {
                parentAuthority.Dispose();
                throw;
            }

            try
            {
                if (_hooks?.AfterCanonicalWriteLockOpenedAsync != null)
                    await _hooks.AfterCanonicalWriteLockOpenedAsync();
                EnsureRuntimePathIsSafe(lockPath);
                PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    lockPath,
                    "Canonical write lock");
            }
            catch
            {
                await stream.DisposeAsync();
                parentAuthority.Dispose();
                throw;
            }

            var writeLease = new CanonicalWriteLease(
                this,
                stream,
                parentAuthority,
                purpose);
            try
            {
                RecoverInterruptedLoadTransaction(writeLease);
                await RecoverInterruptedWorkerApplyTransactionAsync(writeLease);
                if (purpose is CanonicalWritePurpose.SessionMutation or
                    CanonicalWritePurpose.SessionReplacement)
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

        var actualGeneration = RuntimeFileExists(SessionGenerationPath)
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
        var lockParentPath = Path.GetDirectoryName(lockPath)
            ?? throw new InvalidDataException(
                "Session lifecycle lock has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(lockParentPath);
        for (var attempt = 0; attempt < SessionLifecycleLockRetryCount; attempt++)
        {
            EnsureRuntimePathIsSafe(lockPath);
            if (_hooks?.BeforeSessionLifecycleLockOpenAsync != null)
                await _hooks.BeforeSessionLifecycleLockOpenAsync();
            var parentAuthority = EnsureStableRuntimeDirectory(lockParentPath);
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
            catch (IOException) when (attempt < SessionLifecycleLockRetryCount - 1)
            {
                parentAuthority.Dispose();
                if (_hooks?.SessionLifecycleLockContendedAsync != null)
                    await _hooks.SessionLifecycleLockContendedAsync();
                await Task.Delay(TransientFileAccessRetryDelay);
                continue;
            }
            catch
            {
                parentAuthority.Dispose();
                throw;
            }

            try
            {
                if (_hooks?.AfterSessionLifecycleLockOpenedAsync != null)
                    await _hooks.AfterSessionLifecycleLockOpenedAsync();
                EnsureRuntimePathIsSafe(lockPath);
                PhysicalFileAuthority.EnsureHandleMatchesExpectedPath(
                    stream.SafeFileHandle,
                    lockPath,
                    "Session lifecycle lock");
                return new SessionLifecycleLease(
                    this,
                    stream,
                    parentAuthority);
            }
            catch
            {
                await stream.DisposeAsync();
                parentAuthority.Dispose();
                throw;
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
        var previousGenerationId = RuntimeFileExists(SessionGenerationPath)
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
        if (!RuntimeFileExists(ActiveLoadTransactionJournalPath))
            return;

        var journal = ReadLoadTransactionJournal();
        var paths = GetLoadTransactionPaths(journal.TransactionId);

        if (!journal.Committed || !LoadDirectoryExists(GameSessionPath))
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

        if (RuntimeDirectoryExists(paths.TransactionRoot))
            DeleteRuntimeDirectory(paths.TransactionRoot);
    }

    internal bool LoadDirectoryExists(string path)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        var exists = _loadTransactionOperations.DirectoryExists(path);
        EnsureLoadTransactionOperationPathIsSafe(path);
        return exists;
    }

    internal void CreateLoadDirectory(string path)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        _loadTransactionOperations.BeforeCreateDirectory(path);
        var fullPath = Path.GetFullPath(path);
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath);
        if (IsSameOrDescendant(fullPath, runtimeRoot))
        {
            using var authority = EnsureStableRuntimeDirectory(fullPath);
            return;
        }

        if (string.Equals(
                fullPath,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            using var authority = PhysicalFileAuthority.EnsureStableDirectory(
                _basePath,
                fullPath,
                "Canonical load session");
            EnsureCanonicalSessionRootIsNotReparsePoint();
            return;
        }

        throw new InvalidDataException(
            "Load directory creation is outside runtime and canonical session authority.");
    }

    internal async Task WriteLoadTransactionFileAsync(
        string path,
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        EnsureRuntimePathIsSafe(path);
        var parent = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Load transaction file has no parent.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parent);
        using var parentAuthority = EnsureStableRuntimeDirectory(parent);
        if (_hooks?.BeforeRuntimeFileCreateAsync != null)
            await _hooks.BeforeRuntimeFileCreateAsync(path);

        await using var output = PhysicalFileAuthority.CreateNewWritableFile(
            parentAuthority,
            path,
            "Load transaction staging",
            asynchronous: true);
        await source.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        output.Flush(flushToDisk: true);
        EnsureRuntimePathIsSafe(path);
    }

    internal void DeleteLoadTransactionFile(string path)
    {
        EnsureRuntimePathIsSafe(path);
        DeleteRuntimeFile(path);
    }

    internal void DeleteLoadTransactionDirectory(string path)
    {
        EnsureRuntimePathIsSafe(path);
        DeleteRuntimeDirectory(path);
    }

    internal void MoveLoadDirectory(string sourcePath, string destinationPath)
    {
        EnsureLoadTransactionOperationPathIsSafe(sourcePath);
        EnsureLoadTransactionOperationPathIsSafe(destinationPath);
        using var sourceParentAuthority = EnsureStableLoadOperationParent(
            sourcePath,
            "Load transaction source");
        using var destinationParentAuthority = EnsureStableLoadOperationParent(
            destinationPath,
            "Load transaction destination");
        using var sourceHandle = OperatingSystem.IsWindows()
            ? PhysicalFileAuthority.OpenForRename(
                sourceParentAuthority,
                sourcePath,
                isDirectory: true,
                "Load transaction directory move")
            : null;
        _hooks?.BeforeLoadDirectoryMoveAsync?.Invoke(sourcePath, destinationPath)
            .GetAwaiter()
            .GetResult();
        _loadTransactionOperations.BeforeMoveDirectory(
            sourcePath,
            destinationPath);
        if (OperatingSystem.IsWindows())
        {
            PhysicalFileAuthority.RenameOpenedObject(
                sourceHandle!,
                destinationPath,
                replaceExisting: false,
                "Load transaction directory move");
        }
        else
        {
            Directory.Move(sourcePath, destinationPath);
        }

        EnsureLoadTransactionOperationPathIsSafe(destinationPath);
    }

    private void RestoreLoadTransactionBackup(CanonicalLoadTransactionPaths paths)
    {
        if (!RuntimeDirectoryExists(paths.BackupSessionPath))
            return;

        if (LoadDirectoryExists(GameSessionPath))
        {
            if (RuntimeDirectoryExists(paths.FailedSessionPath))
                DeleteRuntimeDirectory(paths.FailedSessionPath);

            CreateLoadDirectory(Path.GetDirectoryName(paths.FailedSessionPath)!);
            MoveLoadDirectory(GameSessionPath, paths.FailedSessionPath);
        }

        MoveLoadDirectory(paths.BackupSessionPath, GameSessionPath);
    }

    private void CleanupCommittedLoadTransaction(CanonicalLoadTransactionPaths paths)
    {
        if (RuntimeDirectoryExists(paths.TransactionRoot))
            DeleteRuntimeDirectory(paths.TransactionRoot);
        if (RuntimeFileExists(ActiveLoadTransactionJournalPath))
            DeleteRuntimeFile(ActiveLoadTransactionJournalPath);
    }

    private void WriteLoadTransactionJournal(LoadTransactionJournal journal)
    {
        var json = JsonSerializer.Serialize(journal);
        WriteRuntimeTextAtomic(ActiveLoadTransactionJournalPath, json);
    }

    private void RestoreLoadTransactionGeneration(string? previousGenerationId)
    {
        if (string.IsNullOrWhiteSpace(previousGenerationId))
        {
            if (RuntimeFileExists(SessionGenerationPath))
                DeleteRuntimeFile(SessionGenerationPath);
            return;
        }

        WriteSessionGeneration(previousGenerationId);
    }

    private LoadTransactionJournal ReadLoadTransactionJournal()
    {
        try
        {
            var journal = JsonSerializer.Deserialize<LoadTransactionJournal>(
                ReadRuntimeText(ActiveLoadTransactionJournalPath),
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
        if (!RuntimeFileExists(ActiveLoadTransactionJournalPath))
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
        if (RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
            throw new InvalidOperationException("An active worker apply transaction already exists.");

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        CreateLoadDirectory(beforeRoot);
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
                    await WriteRuntimeBytesAtomicAsync(
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
            WriteRuntimeTextAtomic(
                Path.Combine(transactionRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest));
            WriteWorkerApplyJournal(transactionId, committed: false, rolledBack: false);
            return new CanonicalWorkerApplyTransaction(transactionId, transactionRoot);
        }
        catch
        {
            if (!RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath) &&
                RuntimeDirectoryExists(transactionRoot))
            {
                DeleteRuntimeDirectory(transactionRoot);
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
        if (!RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
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
                           ReadRuntimeText(manifestPath),
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
        if (!beforePath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Worker apply before-image escapes its transaction.");
        EnsureRuntimePathIsSafe(beforePath);
        if (!File.Exists(beforePath))
            throw new InvalidDataException("Worker apply before-image is missing or escapes its transaction.");

        var bytes = ReadExactBytesFromStablePath(beforePath);
        EnsureRuntimePathIsSafe(beforePath);
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
                ReadRuntimeText(ActiveWorkerApplyTransactionJournalPath),
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
        WriteRuntimeTextAtomic(
            ActiveWorkerApplyTransactionJournalPath,
            JsonSerializer.Serialize(journal));
    }

    private void CleanupWorkerApplyTransaction(string transactionId)
    {
        var transactionRoot = GetWorkerApplyTransactionRoot(transactionId);
        if (RuntimeDirectoryExists(transactionRoot))
            DeleteRuntimeDirectory(transactionRoot);
        if (RuntimeFileExists(ActiveWorkerApplyTransactionJournalPath))
            DeleteRuntimeFile(ActiveWorkerApplyTransactionJournalPath);
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

    private PhysicalFileAuthority.StableDirectory EnsureStableCanonicalParent(
        string relativePath,
        string expectedFullPath)
    {
        EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
        var parent = Path.GetDirectoryName(expectedFullPath)
            ?? throw new InvalidDataException(
                "Canonical authority path has no parent directory.");
        var authority = PhysicalFileAuthority.EnsureStableDirectory(
            _basePath,
            parent,
            "Canonical game-session");
        try
        {
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private PhysicalFileAuthority.StableDirectory EnsureStableRuntimeDirectory(
        string directoryPath)
    {
        EnsureRuntimePathIsSafe(directoryPath);
        var authority = PhysicalFileAuthority.EnsureStableDirectory(
            _basePath,
            directoryPath,
            "Client runtime");
        try
        {
            EnsureRuntimePathIsSafe(directoryPath);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private void EnsureRuntimeDirectoryExistsAndIsSafe(string directoryPath)
    {
        using var authority = EnsureStableRuntimeDirectory(directoryPath);
    }

    private PhysicalFileAuthority.StableDirectory EnsureStableLoadOperationParent(
        string path,
        string authorityName)
    {
        EnsureLoadTransactionOperationPathIsSafe(path);
        var fullPath = Path.GetFullPath(path);
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException(
                $"{authorityName} has no parent directory.");
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath);
        if (IsSameOrDescendant(fullPath, runtimeRoot))
            return EnsureStableRuntimeDirectory(parentPath);

        if (string.Equals(
                fullPath,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalFileAuthority.EnsureStableDirectory(
                _basePath,
                parentPath,
                authorityName);
        }

        throw new InvalidDataException(
            $"{authorityName} is outside runtime and canonical session authority.");
    }

    private string CreateRuntimeStagingRoot(string area)
    {
        var areaRoot = Path.Combine(RuntimeRootPath, area);
        EnsureRuntimeDirectoryExistsAndIsSafe(areaRoot);

        var stagingRoot = Path.Combine(areaRoot, Guid.NewGuid().ToString("N"));
        using var authority = EnsureStableRuntimeDirectory(stagingRoot);
        EnsureRuntimePathIsSafe(stagingRoot);
        return stagingRoot;
    }

    private void DeleteRuntimeStagingRoot(string area, string stagingRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        var areaRoot = Path.GetFullPath(Path.Combine(RuntimeRootPath, area));
        var fullStagingRoot = Path.GetFullPath(stagingRoot);
        if (!IsSameOrDescendant(fullStagingRoot, areaRoot) ||
            string.Equals(fullStagingRoot, areaRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Runtime staging cleanup path is outside its authority root.");
        }

        EnsureRuntimePathIsSafe(fullStagingRoot);
        if (Directory.Exists(fullStagingRoot))
            DeleteRuntimeDirectory(fullStagingRoot);
    }

    private void EnsureRuntimePathIsSafe(string fullPath)
    {
        var runtimeRoot = Path.GetFullPath(RuntimeRootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(fullPath);
        if (!IsSameOrDescendant(candidate, runtimeRoot))
        {
            throw new InvalidDataException(
                "Client runtime path is outside the physical runtime authority root.");
        }

        EnsureNoExistingReparsePoint(
            runtimeRoot,
            candidate,
            "Client runtime");
    }

    private void EnsureLoadTransactionOperationPathIsSafe(string path)
    {
        var candidate = Path.GetFullPath(path);
        if (IsSameOrDescendant(candidate, Path.GetFullPath(RuntimeRootPath)))
        {
            EnsureRuntimePathIsSafe(candidate);
            return;
        }

        if (string.Equals(
                candidate,
                Path.GetFullPath(GameSessionPath),
                StringComparison.OrdinalIgnoreCase))
        {
            EnsureCanonicalSessionRootIsNotReparsePoint();
            return;
        }

        throw new InvalidDataException(
            "Load transaction operation path is outside runtime and canonical session authority.");
    }

    private bool RuntimeFileExists(string path)
    {
        EnsureRuntimePathIsSafe(path);
        var exists = _loadTransactionOperations.FileExists(path);
        EnsureRuntimePathIsSafe(path);
        return exists;
    }

    private bool RuntimeDirectoryExists(string path)
    {
        EnsureRuntimePathIsSafe(path);
        var exists = _loadTransactionOperations.DirectoryExists(path);
        EnsureRuntimePathIsSafe(path);
        return exists;
    }

    private string ReadRuntimeText(string path)
    {
        EnsureRuntimePathIsSafe(path);
        _hooks?.BeforeRuntimeFileReadOpenAsync?.Invoke(path)
            .GetAwaiter()
            .GetResult();
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority file has no parent."));
        using var stream = PhysicalFileAuthority.OpenReadFile(
            parentAuthority,
            path,
            "Runtime authority read",
            asynchronous: false,
            afterOpenedBeforeValidation: () =>
                _hooks?.AfterRuntimeFileReadOpenedAsync?.Invoke(path)
                    .GetAwaiter()
                    .GetResult())
            ?? throw new FileNotFoundException(
                "Runtime authority file does not exist.",
                path);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private void WriteRuntimeTextAtomic(string path, string content)
    {
        EnsureRuntimePathIsSafe(path);
        _loadTransactionOperations.BeforeWriteAllTextAtomic(path, content);
        WriteRuntimeBytesAtomicCoreAsync(
                path,
                Encoding.UTF8.GetBytes(content),
                CancellationToken.None,
                "Runtime authority")
            .GetAwaiter()
            .GetResult();
        EnsureRuntimePathIsSafe(path);
    }

    private void DeleteRuntimeFile(string path)
    {
        EnsureRuntimePathIsSafe(path);
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority file has no parent."));
        _loadTransactionOperations.BeforeDeleteFile(path);
        PhysicalFileAuthority.TryDeleteFile(
            parentAuthority,
            path,
            "Runtime authority file cleanup");
        EnsureRuntimePathIsSafe(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Runtime authority file has no parent."));
    }

    private void DeleteRuntimeDirectory(string path)
    {
        EnsureRuntimePathIsSafe(path);
        using var parentAuthority = EnsureStableRuntimeDirectory(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                "Runtime authority directory has no parent."));
        _loadTransactionOperations.BeforeDeleteDirectory(path);
        PhysicalFileAuthority.TryDeleteTree(
            parentAuthority,
            path,
            "Runtime authority directory cleanup");
        EnsureRuntimePathIsSafe(
            Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("Runtime authority directory has no parent."));
    }

    internal StableReadFile? OpenExactPhysicalReadFile(
        string fullPath,
        string authorityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityName);
        var expectedPath = Path.GetFullPath(fullPath);
        var parentPath = Path.GetDirectoryName(expectedPath)
            ?? throw new InvalidDataException(
                $"{authorityName} file has no parent directory.");
        var parentAuthority = PhysicalFileAuthority.OpenStableDirectory(
            parentPath,
            authorityName);
        try
        {
            var stream = PhysicalFileAuthority.OpenReadFile(
                parentAuthority,
                expectedPath,
                authorityName,
                asynchronous: true);
            if (stream == null)
            {
                parentAuthority.Dispose();
                return null;
            }

            return new StableReadFile(stream, parentAuthority);
        }
        catch
        {
            parentAuthority.Dispose();
            throw;
        }
    }

    private async Task<StableReadFile?> OpenCanonicalReadStreamAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedFullPath = ResolvePath(relativePath);
        if (!File.Exists(expectedFullPath))
            return null;

        await InvokeBeforeCanonicalReadOpenAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();

        PhysicalFileAuthority.StableDirectory? parentAuthority = null;
        try
        {
            parentAuthority = EnsureStableCanonicalParent(
                relativePath,
                expectedFullPath);
            EnsureCanonicalPathStillSafe(relativePath, expectedFullPath);
            var stream = PhysicalFileAuthority.OpenReadFile(
                parentAuthority,
                expectedFullPath,
                "Canonical game-session read",
                asynchronous: true);
            if (stream == null)
            {
                parentAuthority.Dispose();
                return null;
            }

            return new StableReadFile(stream, parentAuthority);
        }
        catch
        {
            parentAuthority?.Dispose();
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

    private Task InvokeAfterCanonicalMutationBoundaryValidatedAsync(string relativePath) =>
        _hooks?.AfterCanonicalMutationBoundaryValidatedAsync?.Invoke(relativePath) ??
        Task.CompletedTask;

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

    private async Task WriteRuntimeBytesAtomicAsync(string path, byte[] content)
    {
        await WriteRuntimeBytesAtomicCoreAsync(
            path,
            content,
            CancellationToken.None,
            "Runtime before-image");
    }

    private async Task WriteRuntimeBytesAtomicCoreAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken,
        string authorityName)
    {
        EnsureRuntimePathIsSafe(path);
        var parentPath = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException(
                $"{authorityName} has no parent directory.");
        EnsureRuntimeDirectoryExistsAndIsSafe(parentPath);
        using var parentAuthority = EnsureStableRuntimeDirectory(parentPath);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        EnsureRuntimePathIsSafe(tempPath);
        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                tempPath,
                authorityName + " temporary",
                asynchronous: true);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);

            if (_hooks?.AfterRuntimeMutationBoundaryValidatedAsync != null)
            {
                await _hooks.AfterRuntimeMutationBoundaryValidatedAsync(path);
            }

            EnsureRuntimePathIsSafe(tempPath);
            EnsureRuntimePathIsSafe(path);
            if (OperatingSystem.IsWindows())
            {
                PhysicalFileAuthority.ValidateExistingReplacementTarget(
                    parentAuthority,
                    path,
                    authorityName);
                PhysicalFileAuthority.RenameOpenedObject(
                    stream.SafeFileHandle,
                    path,
                    replaceExisting: true,
                    authorityName);
            }
            else
            {
                File.Move(tempPath, path, overwrite: true);
            }

            EnsureRuntimePathIsSafe(path);
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
            try
            {
                PhysicalFileAuthority.TryDeleteFile(
                    parentAuthority,
                    tempPath,
                    authorityName + " temporary cleanup");
            }
            catch
            {
                // Retaining an unreachable runtime temp is safer than following a replaced path.
            }
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
        if (RuntimeFileExists(SessionGenerationPath))
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
            !RuntimeFileExists(SessionGenerationPath))
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
                ReadRuntimeText(SessionGenerationPath),
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
        WriteRuntimeTextAtomic(SessionGenerationPath, json);
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
        PhysicalFileAuthority.TryDeleteTree(
            path,
            "No-follow directory cleanup");
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
        {
            using var parentAuthority = PhysicalFileAuthority.OpenStableDirectory(
                Path.GetDirectoryName(path)
                ?? throw new InvalidDataException(
                    "Empty-directory cleanup target has no parent."),
                "Empty-directory cleanup");
            PhysicalFileAuthority.TryDeleteEmptyDirectory(
                parentAuthority,
                path,
                "Empty-directory cleanup");
        }
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
                DeleteCanonicalFileByFullPath(file);
        }

        var gameStatePath = Path.Combine(_basePath, "game_session", "game_state");
        var browserRollbackRoot = ResolvePath(
            ExplorerLocalTurnRollbackArtifacts.Root);
        if (File.Exists(browserRollbackRoot))
            DeleteCanonicalFileByFullPath(browserRollbackRoot);
        else if (Directory.Exists(browserRollbackRoot))
            DeleteCanonicalDirectoryTreeByFullPath(browserRollbackRoot);

        if (Directory.Exists(gameStatePath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(gameStatePath, "*"))
            {
                if (ShouldPreserveAcrossGameStateClear(gameStatePath, file))
                    continue;

                DeleteCanonicalFileByFullPath(file);
            }
        }

        // Clear output and ready
        var outputPath = Path.Combine(_basePath, "game_session", "output");
        if (Directory.Exists(outputPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(outputPath, "*.json"))
                DeleteCanonicalFileByFullPath(file);
        }

        var readyPath = Path.Combine(_basePath, "game_session", "ready");
        if (Directory.Exists(readyPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(readyPath, "*.json"))
                DeleteCanonicalFileByFullPath(file);
        }

        var lorePath = Path.Combine(_basePath, "game_session", "lore");
        if (Directory.Exists(lorePath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(lorePath, "*"))
                DeleteCanonicalFileByFullPath(file);
        }

        var storiesPath = Path.Combine(_basePath, "game_session", "stories");
        if (Directory.Exists(storiesPath))
        {
            foreach (var file in EnumerateFilesWithoutFollowingReparsePoints(storiesPath, "*"))
                DeleteCanonicalFileByFullPath(file);
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

            DeleteCanonicalFileByFullPath(file);
        }
    }

    private void DeleteCanonicalFileByFullPath(string fullPath)
    {
        var relativePath = Path.GetRelativePath(GameSessionPath, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        DeleteFileCore(relativePath);
    }

    private void DeleteCanonicalDirectoryTreeByFullPath(string fullPath)
    {
        var relativePath = Path.GetRelativePath(GameSessionPath, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var expectedFullPath = ResolvePath(relativePath);
        using var parentAuthority = EnsureStableCanonicalParent(
            relativePath,
            expectedFullPath);
        PhysicalFileAuthority.TryDeleteTree(
            parentAuthority,
            expectedFullPath,
            "Canonical directory-tree cleanup");
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

    private static byte[] ReadExactBytesFromStablePath(string expectedFullPath)
    {
        var normalizedExpectedPath = Path.GetFullPath(expectedFullPath);
        using var parentAuthority = PhysicalFileAuthority.OpenStableDirectory(
            Path.GetDirectoryName(normalizedExpectedPath)
            ?? throw new InvalidDataException(
                "Recovery evidence file has no parent directory."),
            "Recovery evidence");
        using var stream = PhysicalFileAuthority.OpenReadFile(
            parentAuthority,
            normalizedExpectedPath,
            "Recovery evidence",
            asynchronous: false)
            ?? throw new FileNotFoundException(
                "Recovery evidence file does not exist.",
                normalizedExpectedPath);

        using var buffer = stream.Length is > 0 and <= int.MaxValue
            ? new MemoryStream((int)stream.Length)
            : new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
