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
        WorkerTaskPacket task)
    {
        var basePath = Directory.GetParent(fs.GameSessionPath)?.FullName ??
                       throw new InvalidOperationException("Cannot resolve worker runtime parent directory.");
        var runtimeRoot = Path.GetFullPath(Path.Combine(basePath, GmWorkerBridgePool.WorkerRuntimeRoot));
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
                if (!GmWorkerContractValidator.IsSafeRelativePath(contextFile.Path))
                    throw new InvalidOperationException($"Worker context path is unsafe: {contextFile.Path}.");

                var content = await fs.ReadFileBytesAsync(contextFile.Path);
                VerifyPinnedContext(contextFile, content);
                if (content == null)
                    continue;

                await WriteWorkspaceFileAsync(gameSessionPath, contextFile.Path, content);
            }

            await WriteAbsoluteFileAsync(taskPath, Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)));
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
            await DeleteWorkspaceAsync(runtimeRoot, workspaceRoot);
            throw;
        }
    }

    internal async Task<byte[]?> ReadFileBytesAsync(string relativePath)
    {
        var fullPath = ResolveWorkspacePath(GameSessionPath, relativePath);
        if (!File.Exists(fullPath))
            return null;

        RejectReparsePoints(GameSessionPath, fullPath);
        return await File.ReadAllBytesAsync(fullPath);
    }

    internal Task<byte[]?> ReadProposalBytesAsync() =>
        ReadFileBytesAsync(Path.GetRelativePath(GameSessionPath, ProposalPath).Replace('\\', '/'));

    public async ValueTask DisposeAsync() =>
        await DeleteWorkspaceAsync(_runtimeRoot, _workspaceRoot);

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
        byte[] content)
    {
        var fullPath = ResolveWorkspacePath(gameSessionPath, relativePath);
        await WriteAbsoluteFileAsync(fullPath, content);
    }

    private static async Task WriteAbsoluteFileAsync(string fullPath, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content);
    }

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

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "worker-task" : sanitized;
    }
}
