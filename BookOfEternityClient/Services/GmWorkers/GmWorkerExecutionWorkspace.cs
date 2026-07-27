using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed class GmWorkerExecutionWorkspace : IAsyncDisposable
{
    private const int CleanupRetryCount = 5;
    private static readonly TimeSpan CleanupRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly string _runtimeRoot;
    private readonly string _workspaceRoot;

    private GmWorkerExecutionWorkspace(
        string runtimeRoot,
        string workspaceRoot,
        string gameSessionPath,
        string taskPath,
        string proposalPath)
    {
        _runtimeRoot = runtimeRoot;
        _workspaceRoot = workspaceRoot;
        GameSessionPath = gameSessionPath;
        TaskPath = taskPath;
        ProposalPath = proposalPath;
    }

    internal string GameSessionPath { get; }
    internal string TaskPath { get; }
    internal string ProposalPath { get; }

    internal static async Task<GmWorkerExecutionWorkspace> CreateAsync(
        FileSystemManager fs,
        WorkerTaskPacket task,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRoot = ResolveRuntimeRoot(fs.BasePath);
        var safeTaskId = SanitizeSegment(task.TaskId);
        var workspaceRoot = Path.GetFullPath(
            Path.Combine(runtimeRoot, $"{safeTaskId}-{Guid.NewGuid():N}"));
        EnsureWorkspaceIsInsideRuntime(runtimeRoot, workspaceRoot);

        var gameSessionPath = Path.Combine(workspaceRoot, "game_session");
        var taskPath = ResolveWorkspacePath(
            gameSessionPath,
            GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
        var proposalPath = ResolveWorkspacePath(
            gameSessionPath,
            GmWorkerBridgePool.GetProposalInboxPath(task.TaskId));

        Directory.CreateDirectory(gameSessionPath);
        try
        {
            foreach (var contextFile in task.ContextFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GmWorkerContractValidator.IsSafeRelativePath(contextFile.Path))
                    throw new InvalidOperationException($"Worker context path is unsafe: {contextFile.Path}.");

                var content = await fs.ReadFileBytesAsync(
                    contextFile.Path,
                    cancellationToken);
                VerifyPinnedContext(contextFile, content);
                if (content == null)
                    continue;

                await WriteWorkspaceFileAsync(
                    gameSessionPath,
                    contextFile.Path,
                    content,
                    cancellationToken);
            }

            await WriteAbsoluteFileAsync(
                taskPath,
                Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(proposalPath)!);
            return new GmWorkerExecutionWorkspace(
                runtimeRoot,
                workspaceRoot,
                gameSessionPath,
                taskPath,
                proposalPath);
        }
        catch
        {
            try
            {
                await DeleteWorkspaceAsync(runtimeRoot, workspaceRoot);
            }
            catch
            {
                // Staging cleanup must not replace the authoritative staging failure.
            }
            throw;
        }
    }

    internal bool ProposalExists() => File.Exists(ProposalPath);

    internal async Task<byte[]?> ReadFileBytesAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWorkspacePath(GameSessionPath, relativePath);
        if (!File.Exists(fullPath))
            return null;

        RejectReparsePoints(GameSessionPath, fullPath);
        return await ReadBoundedFileAsync(
            fullPath,
            GmWorkerBridgePool.MaxContentRefBytes,
            $"Worker contentRef '{relativePath}'",
            cancellationToken);
    }

    internal async Task<byte[]?> ReadProposalBytesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ProposalPath))
            return null;

        RejectReparsePoints(GameSessionPath, ProposalPath);
        return await ReadBoundedFileAsync(
            ProposalPath,
            GmWorkerBridgePool.MaxProposalBytes,
            "Worker proposal",
            cancellationToken);
    }

    public async ValueTask DisposeAsync() =>
        await DeleteWorkspaceAsync(_runtimeRoot, _workspaceRoot);

    internal static string ResolveRuntimeRoot(string canonicalBasePath)
    {
        var configuredBase = Environment.GetEnvironmentVariable(
            GmWorkerBridgePool.WorkerRuntimeBaseEnvironmentVariable);
        var runtimeBase = string.IsNullOrWhiteSpace(configuredBase)
            ? ResolveDefaultRuntimeBase(canonicalBasePath)
            : ResolveConfiguredRuntimeBase(configuredBase);
        return BuildValidatedRuntimeRoot(canonicalBasePath, runtimeBase);
    }

    internal static string ResolveRuntimeRoot(
        string canonicalBasePath,
        string configuredRuntimeBase)
    {
        var runtimeBase = ResolveConfiguredRuntimeBase(configuredRuntimeBase);
        return BuildValidatedRuntimeRoot(canonicalBasePath, runtimeBase);
    }

    private static string BuildValidatedRuntimeRoot(
        string canonicalBasePath,
        string runtimeBase)
    {
        EnsureRuntimeOutsideCanonicalSession(canonicalBasePath, runtimeBase);
        var sessionIdentity = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeIdentityPath(canonicalBasePath))))
            .ToLowerInvariant()[..24];
        var runtimeRoot = Path.GetFullPath(
            Path.Combine(runtimeBase, GmWorkerBridgePool.WorkerRuntimeRoot, sessionIdentity));
        EnsureRuntimeOutsideCanonicalSession(canonicalBasePath, runtimeRoot);
        return runtimeRoot;
    }

    private static void VerifyPinnedContext(WorkerFileReference contextFile, byte[]? content)
    {
        if (string.Equals(contextFile.Sha256, "missing", StringComparison.OrdinalIgnoreCase))
        {
            if (content != null)
                throw new InvalidOperationException($"Worker context changed after task creation: {contextFile.Path}.");
            return;
        }

        if (content == null)
            throw new InvalidOperationException($"Worker context disappeared after task creation: {contextFile.Path}.");

        var currentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(currentHash, contextFile.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Worker context changed after task creation: {contextFile.Path}.");
    }

    private static async Task WriteWorkspaceFileAsync(
        string gameSessionPath,
        string relativePath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var fullPath = ResolveWorkspacePath(gameSessionPath, relativePath);
        await WriteAbsoluteFileAsync(fullPath, content, cancellationToken);
    }

    private static async Task WriteAbsoluteFileAsync(
        string fullPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
    }

    private static async Task<byte[]?> ReadBoundedFileAsync(
        string fullPath,
        int maxBytes,
        string artifactName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath))
            return null;

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        if (stream.Length > maxBytes)
            throw CreateArtifactLimitException(artifactName, maxBytes);

        using var content = new MemoryStream(
            capacity: checked((int)Math.Min(stream.Length, maxBytes)));
        var buffer = new byte[Math.Min(81920, maxBytes + 1)];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            if (content.Length + read > maxBytes)
                throw CreateArtifactLimitException(artifactName, maxBytes);
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return content.ToArray();
    }

    private static InvalidDataException CreateArtifactLimitException(string artifactName, int maxBytes) =>
        new($"{artifactName} exceeds the {maxBytes}-byte import limit.");

    private static string ResolveWorkspacePath(string gameSessionPath, string relativePath)
    {
        if (!GmWorkerContractValidator.IsSafeRelativePath(relativePath))
            throw new InvalidOperationException($"Worker workspace path is unsafe: {relativePath}.");

        var sessionRoot = EnsureTrailingSeparator(Path.GetFullPath(gameSessionPath));
        var fullPath = Path.GetFullPath(Path.Combine(gameSessionPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(sessionRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Worker workspace path escapes the detached session: {relativePath}.");
        return fullPath;
    }

    private static void RejectReparsePoints(string gameSessionPath, string fullPath)
    {
        var sessionRoot = Path.GetFullPath(gameSessionPath);
        FileSystemInfo? current = new FileInfo(fullPath);
        while (current is { Exists: true } &&
               !string.Equals(current.FullName, sessionRoot, StringComparison.OrdinalIgnoreCase))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Worker output uses a reparse point: {current.FullName}.");
            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }

    private static async Task DeleteWorkspaceAsync(string runtimeRoot, string workspaceRoot)
    {
        EnsureWorkspaceIsInsideRuntime(runtimeRoot, workspaceRoot);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(workspaceRoot))
                    DeleteTreeWithoutFollowingReparsePoints(workspaceRoot);

                if (Directory.Exists(runtimeRoot) && !Directory.EnumerateFileSystemEntries(runtimeRoot).Any())
                    Directory.Delete(runtimeRoot);
                return;
            }
            catch (IOException) when (attempt < CleanupRetryCount - 1)
            {
                await Task.Delay(CleanupRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < CleanupRetryCount - 1)
            {
                await Task.Delay(CleanupRetryDelay);
            }
        }
    }

    private static void DeleteTreeWithoutFollowingReparsePoints(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            DeleteEntryWithoutFollowing(path, attributes);
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly))
        {
            var childAttributes = File.GetAttributes(child);
            if ((childAttributes & FileAttributes.ReparsePoint) != 0)
                DeleteEntryWithoutFollowing(child, childAttributes);
            else if ((childAttributes & FileAttributes.Directory) != 0)
                DeleteTreeWithoutFollowingReparsePoints(child);
            else
                DeleteFile(child, childAttributes);
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        Directory.Delete(path, recursive: false);
    }

    private static void DeleteEntryWithoutFollowing(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.Directory) != 0)
            Directory.Delete(path, recursive: false);
        else
            DeleteFile(path, attributes);
    }

    private static void DeleteFile(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        File.Delete(path);
    }

    private static void EnsureWorkspaceIsInsideRuntime(string runtimeRoot, string workspaceRoot)
    {
        var normalizedRuntime = EnsureTrailingSeparator(Path.GetFullPath(runtimeRoot));
        var normalizedWorkspace = Path.GetFullPath(workspaceRoot);
        if (!normalizedWorkspace.StartsWith(normalizedRuntime, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Worker workspace is outside the managed runtime root.");
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static string ResolveConfiguredRuntimeBase(string configuredBase)
    {
        if (!Path.IsPathRooted(configuredBase))
        {
            throw new InvalidOperationException(
                $"{GmWorkerBridgePool.WorkerRuntimeBaseEnvironmentVariable} must be an absolute path.");
        }

        return Path.GetFullPath(configuredBase);
    }

    private static string ResolveDefaultRuntimeBase(string canonicalBasePath)
    {
        var fullBasePath = Path.GetFullPath(canonicalBasePath);
        if (OperatingSystem.IsWindows())
        {
            var canonicalVolume = Path.GetPathRoot(fullBasePath);
            var systemVolume = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(canonicalVolume) &&
                !string.Equals(canonicalVolume, systemVolume, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(canonicalVolume, "BookOfEternityRuntime");
            }
        }

        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
            return Path.Combine(localApplicationData, "BookOfEternityReborn");

        return Path.Combine(Path.GetTempPath(), "BookOfEternityReborn");
    }

    private static string NormalizeIdentityPath(string path)
    {
        var normalized = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows()
            ? normalized.ToUpperInvariant()
            : normalized;
    }

    private static void EnsureRuntimeOutsideCanonicalSession(
        string canonicalBasePath,
        string candidatePath)
    {
        var canonicalSessionIdentity = ResolvePhysicalIdentityPath(
            Path.Combine(Path.GetFullPath(canonicalBasePath), "game_session"));
        var candidateIdentity = ResolvePhysicalIdentityPath(candidatePath);
        if (!IsSameOrDescendant(candidateIdentity, canonicalSessionIdentity))
            return;

        throw new InvalidOperationException(
            $"Worker runtime path must stay outside canonical game_session: {candidatePath}.");
    }

    private static string ResolvePhysicalIdentityPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
            return NormalizeIdentityPath(fullPath);

        var current = root;
        var relative = Path.GetRelativePath(root, fullPath);
        if (string.Equals(relative, ".", StringComparison.Ordinal))
            return NormalizeIdentityPath(current);

        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, segment);
            FileSystemInfo? entry = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : File.Exists(candidate)
                    ? new FileInfo(candidate)
                    : null;
            if (entry != null && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var resolved = entry.ResolveLinkTarget(returnFinalTarget: true);
                current = resolved == null
                    ? candidate
                    : Path.GetFullPath(resolved.FullName);
                continue;
            }

            current = candidate;
        }

        return NormalizeIdentityPath(current);
    }

    private static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidatePath, rootPath, comparison))
            return true;

        var rootWithSeparator = EnsureTrailingSeparator(rootPath);
        return candidatePath.StartsWith(rootWithSeparator, comparison);
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "worker-task" : sanitized;
    }
}
