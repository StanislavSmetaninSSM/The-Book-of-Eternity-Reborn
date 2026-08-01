using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed class GmWorkerExecutionWorkspaceHooks
{
    internal Func<string, string, Task>? BeforeRuntimeRootCreateAsync { get; init; }
    internal Func<string, Task>? BeforeWorkspaceFileCreateAsync { get; init; }
    internal Func<string, Task>? BeforeWorkspaceFileOpenAsync { get; init; }
    internal Func<string, Task>? BeforeWorkspaceDeleteAsync { get; init; }
    internal Func<string, Task>? AfterQuarantineAuditTempCreatedAsync { get; init; }
}

internal sealed class GmWorkerExecutionWorkspace : IAsyncDisposable
{
    internal const string QuarantineAuditDirectoryName = "quarantine-audit";
    private const int CleanupRetryCount = 5;
    private static readonly TimeSpan CleanupRetryDelay =
        TimeSpan.FromMilliseconds(50);
    private static readonly JsonSerializerOptions CompactJsonOptions =
        new(GmWorkerJson.Options)
        {
            WriteIndented = false
        };

    private readonly string _runtimeRoot;
    private readonly string _workspaceRoot;
    private readonly PhysicalFileAuthority.FileIdentity?
        _workspaceRootIdentity;
    private readonly GmWorkerExecutionWorkspaceHooks? _hooks;
    private readonly SemaphoreSlim _disposeGate = new(1, 1);
    private PhysicalFileAuthority.StableDirectory? _runtimeRootAuthority;
    private PhysicalFileAuthority.StableDirectory? _workspaceRootAuthority;
    private PhysicalFileAuthority.StableDirectory? _gameSessionAuthority;
    private int _disposeState;
    private bool _workspaceDeleted;
    private bool _workspaceDeleteHookCompleted;

    private GmWorkerExecutionWorkspace(
        string runtimeRoot,
        string workspaceRoot,
        string gameSessionPath,
        string taskPath,
        string proposalPath,
        PhysicalFileAuthority.StableDirectory runtimeRootAuthority,
        PhysicalFileAuthority.StableDirectory workspaceRootAuthority,
        PhysicalFileAuthority.StableDirectory gameSessionAuthority,
        PhysicalFileAuthority.FileIdentity? workspaceRootIdentity,
        GmWorkerExecutionWorkspaceHooks? hooks)
    {
        _runtimeRoot = runtimeRoot;
        _workspaceRoot = workspaceRoot;
        _runtimeRootAuthority = runtimeRootAuthority;
        _workspaceRootAuthority = workspaceRootAuthority;
        _gameSessionAuthority = gameSessionAuthority;
        _workspaceRootIdentity = workspaceRootIdentity;
        _hooks = hooks;
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
        CancellationToken cancellationToken,
        GmWorkerExecutionWorkspaceHooks? hooks = null,
        string? configuredRuntimeBase = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRoot = configuredRuntimeBase == null
            ? ResolveRuntimeRoot(fs.BasePath)
            : ResolveRuntimeRoot(fs.BasePath, configuredRuntimeBase);
        var safeTaskId = SanitizeSegment(task.TaskId);
        var workspaceRoot = Path.GetFullPath(
            Path.Combine(
                runtimeRoot,
                $"{safeTaskId}-{Guid.NewGuid():N}"));
        EnsureWorkspaceIsInsideRuntime(runtimeRoot, workspaceRoot);

        var gameSessionPath = Path.Combine(
            workspaceRoot,
            "game_session");
        var taskPath = ResolveWorkspacePath(
            gameSessionPath,
            GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
        var proposalPath = ResolveWorkspacePath(
            gameSessionPath,
            GmWorkerBridgePool.GetProposalInboxPath(task.TaskId));
        var runtimeParentPath = Path.GetDirectoryName(runtimeRoot)
            ?? throw new InvalidDataException(
                "Worker runtime root has no parent.");
        var physicalRoot = Path.GetPathRoot(runtimeParentPath)
            ?? throw new InvalidDataException(
                "Worker runtime parent has no physical root.");

        using var runtimeParentAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                physicalRoot,
                runtimeParentPath,
                "Worker runtime parent");
        if (hooks?.BeforeRuntimeRootCreateAsync != null)
        {
            await hooks.BeforeRuntimeRootCreateAsync(
                runtimeParentPath,
                runtimeRoot);
        }

        PhysicalFileAuthority.StableDirectory? runtimeRootAuthority = null;
        PhysicalFileAuthority.StableDirectory? workspaceRootAuthority = null;
        PhysicalFileAuthority.StableDirectory? gameSessionAuthority = null;
        GmWorkerExecutionWorkspace? workspace = null;
        try
        {
            runtimeRootAuthority =
                PhysicalFileAuthority.CreateStableChildDirectory(
                    runtimeParentAuthority,
                    runtimeRoot,
                    "Worker runtime root",
                    requireNew: false);
            workspaceRootAuthority =
                PhysicalFileAuthority.CreateStableChildDirectory(
                    runtimeRootAuthority,
                    workspaceRoot,
                    "Worker workspace root",
                    requireNew: true);
            gameSessionAuthority =
                PhysicalFileAuthority.CreateStableChildDirectory(
                    workspaceRootAuthority,
                    gameSessionPath,
                    "Worker detached session root",
                    requireNew: true);
            var workspaceRootIdentity =
                OperatingSystem.IsWindows()
                    ? PhysicalFileAuthority.CaptureFileIdentity(
                        workspaceRootAuthority.Handle
                        ?? throw new InvalidOperationException(
                            "Worker workspace authority has no retained handle."),
                        "Worker workspace root")
                    : null;

            workspace = new GmWorkerExecutionWorkspace(
                runtimeRoot,
                workspaceRoot,
                gameSessionPath,
                taskPath,
                proposalPath,
                runtimeRootAuthority,
                workspaceRootAuthority,
                gameSessionAuthority,
                workspaceRootIdentity,
                hooks);
            runtimeRootAuthority = null;
            workspaceRootAuthority = null;
            gameSessionAuthority = null;

            await workspace.StageTaskAsync(
                fs,
                task,
                cancellationToken);
            return workspace;
        }
        catch
        {
            if (workspace != null)
            {
                try
                {
                    await workspace.DisposeAsync();
                }
                catch
                {
                    // Staging cleanup must not replace the authoritative failure.
                }
            }
            else
            {
                gameSessionAuthority?.Dispose();
                workspaceRootAuthority?.Dispose();
                if (runtimeRootAuthority != null)
                {
                    try
                    {
                        await DeleteWorkspaceAsync(
                            runtimeRootAuthority,
                            workspaceRoot);
                    }
                    catch
                    {
                        // Staging cleanup must not replace the authoritative failure.
                    }
                }
            }

            throw;
        }
        finally
        {
            gameSessionAuthority?.Dispose();
            workspaceRootAuthority?.Dispose();
            runtimeRootAuthority?.Dispose();
        }
    }

    internal async Task<byte[]?> ReadFileBytesAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveWorkspacePath(
            GameSessionPath,
            relativePath);
        return await ReadBoundedFileAsync(
            fullPath,
            GmWorkerBridgePool.MaxContentRefBytes,
            $"Worker contentRef '{relativePath}'",
            cancellationToken);
    }

    internal async Task<byte[]?> ReadProposalBytesAsync(
        CancellationToken cancellationToken = default) =>
        await ReadBoundedFileAsync(
            ProposalPath,
            GmWorkerBridgePool.MaxProposalBytes,
            "Worker proposal",
            cancellationToken);

    internal async Task PersistQuarantineAuditReceiptAsync(
        string sessionGeneration,
        WorkerAuditEvent auditEvent)
    {
        if (string.IsNullOrWhiteSpace(sessionGeneration))
        {
            throw new ArgumentException(
                "Quarantine audit session generation is required.",
                nameof(sessionGeneration));
        }

        if (string.IsNullOrWhiteSpace(auditEvent.EventId) ||
            !SanitizeSegment(auditEvent.EventId).Equals(
                auditEvent.EventId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Quarantine audit event id is not a safe receipt identity.");
        }

        var runtimeRootAuthority = _runtimeRootAuthority
            ?? throw new ObjectDisposedException(
                nameof(GmWorkerExecutionWorkspace));
        var receiptDirectoryPath = Path.Combine(
            _runtimeRoot,
            QuarantineAuditDirectoryName);
        using var receiptDirectoryAuthority =
            PhysicalFileAuthority.CreateStableChildDirectory(
                runtimeRootAuthority,
                receiptDirectoryPath,
                "Worker quarantine audit directory",
                requireNew: false);
        var receiptPath = Path.Combine(
            receiptDirectoryPath,
            auditEvent.EventId + ".json");
        var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(
            new GmWorkerQuarantineAuditReceipt(
                SchemaVersion: 1,
                sessionGeneration,
                auditEvent),
            CompactJsonOptions);

        using (var existing = PhysicalFileAuthority.OpenReadFile(
                   receiptDirectoryAuthority,
                   receiptPath,
                   "Worker quarantine audit receipt",
                   asynchronous: false))
        {
            if (existing != null)
            {
                EnsureQuarantineAuditReceiptMatches(
                    existing,
                    receiptPath,
                    receiptBytes);
                return;
            }
        }

        var tempPath = receiptPath +
                       ".tmp." +
                       Guid.NewGuid().ToString("N");
        FileStream? stream = null;
        var published = false;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                receiptDirectoryAuthority,
                tempPath,
                "Worker quarantine audit temporary receipt",
                asynchronous: true,
                requestDeleteAccess: true);
            if (_hooks?.AfterQuarantineAuditTempCreatedAsync != null)
            {
                await _hooks.AfterQuarantineAuditTempCreatedAsync(
                    tempPath);
            }

            await stream.WriteAsync(receiptBytes);
            await stream.FlushAsync();
            stream.Flush(flushToDisk: true);
            var stagedAuthority =
                PhysicalFileAuthority.CaptureOpenedFileAuthority(
                    stream.SafeFileHandle,
                    tempPath,
                    "Worker quarantine audit temporary receipt");
            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                tempPath,
                stagedAuthority.Identity,
                stagedAuthority.Sha256,
                "Worker quarantine audit temporary receipt",
                stagedAuthority.Length);
            try
            {
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    stream.SafeFileHandle,
                    receiptDirectoryAuthority,
                    receiptPath,
                    replaceExisting: false,
                    "Worker quarantine audit receipt publication");
                published = true;
            }
            catch (IOException)
            {
                using var racedExisting =
                    PhysicalFileAuthority.OpenReadFile(
                        receiptDirectoryAuthority,
                        receiptPath,
                        "Worker quarantine audit receipt",
                        asynchronous: false);
                if (racedExisting == null)
                    throw;

                EnsureQuarantineAuditReceiptMatches(
                    racedExisting,
                    receiptPath,
                    receiptBytes);
                return;
            }

            var publishedAuthority =
                PhysicalFileAuthority.CaptureOpenedFileAuthority(
                    stream.SafeFileHandle,
                    receiptPath,
                    "Worker quarantine audit receipt");
            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                receiptPath,
                publishedAuthority.Identity,
                publishedAuthority.Sha256,
                "Worker quarantine audit receipt",
                publishedAuthority.Length);
        }
        finally
        {
            if (stream != null)
            {
                if (!published)
                {
                    try
                    {
                        PhysicalFileAuthority
                            .EnsureRegularFileHandleMatchesExpectedPath(
                                stream.SafeFileHandle,
                                tempPath,
                                "Worker quarantine audit temporary receipt");
                        PhysicalFileAuthority.DeleteOpenedFile(
                            stream.SafeFileHandle,
                            "Worker quarantine audit temporary receipt");
                    }
                    catch
                    {
                        // A unique unpublished temporary cannot become terminal evidence.
                    }
                }

                await stream.DisposeAsync();
            }
        }
    }

    internal async Task DeleteDetachedSessionRetainingRuntimeAuthorityAsync()
    {
        if (Volatile.Read(ref _disposeState) != 0)
            return;

        await _disposeGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _disposeState) != 0)
                return;

            var failures = await DeleteDetachedSessionCoreAsync();
            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    "Worker workspace cleanup did not complete exactly.",
                    failures);
            }
        }
        finally
        {
            _disposeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _disposeState) != 0)
            return;

        await _disposeGate.WaitAsync();
        try
        {
            if (Volatile.Read(ref _disposeState) != 0)
                return;

            var failures = await DeleteDetachedSessionCoreAsync();
            if (failures == null)
            {
                TryDisposeAuthority(
                    ref _runtimeRootAuthority,
                    ref failures);
            }

            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    "Worker workspace cleanup did not complete exactly.",
                    failures);
            }

            Volatile.Write(ref _disposeState, 1);
        }
        finally
        {
            _disposeGate.Release();
        }
    }

    private async Task<List<Exception>?> DeleteDetachedSessionCoreAsync()
    {
        List<Exception>? failures = null;
        TryDisposeAuthority(
            ref _gameSessionAuthority,
            ref failures);
        TryDisposeAuthority(
            ref _workspaceRootAuthority,
            ref failures);
        if (failures != null ||
            _runtimeRootAuthority == null ||
            _workspaceDeleted)
        {
            return failures;
        }

        try
        {
            if (!_workspaceDeleteHookCompleted)
            {
                if (_hooks?.BeforeWorkspaceDeleteAsync != null)
                {
                    await _hooks.BeforeWorkspaceDeleteAsync(
                        _workspaceRoot);
                }

                _workspaceDeleteHookCompleted = true;
            }

            await DeleteWorkspaceAsync(
                _runtimeRootAuthority,
                _workspaceRoot,
                _workspaceRootIdentity);
            _workspaceDeleted = true;
        }
        catch (Exception ex)
        {
            failures ??= [];
            failures.Add(ex);
        }

        return failures;
    }

    internal static string ResolveRuntimeRoot(string canonicalBasePath)
    {
        var configuredBase = Environment.GetEnvironmentVariable(
            GmWorkerBridgePool.WorkerRuntimeBaseEnvironmentVariable);
        var runtimeBase = string.IsNullOrWhiteSpace(configuredBase)
            ? ResolveDefaultRuntimeBase(canonicalBasePath)
            : ResolveConfiguredRuntimeBase(configuredBase);
        return BuildValidatedRuntimeRoot(
            canonicalBasePath,
            runtimeBase);
    }

    internal static string ResolveRuntimeRoot(
        string canonicalBasePath,
        string configuredRuntimeBase)
    {
        var runtimeBase = ResolveConfiguredRuntimeBase(
            configuredRuntimeBase);
        return BuildValidatedRuntimeRoot(
            canonicalBasePath,
            runtimeBase);
    }

    private async Task StageTaskAsync(
        FileSystemManager fs,
        WorkerTaskPacket task,
        CancellationToken cancellationToken)
    {
        foreach (var contextFile in task.ContextFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GmWorkerContractValidator.IsSafeRelativePath(
                    contextFile.Path))
            {
                throw new InvalidOperationException(
                    $"Worker context path is unsafe: {contextFile.Path}.");
            }

            var content = await fs.ReadFileBytesAsync(
                contextFile.Path,
                cancellationToken);
            VerifyPinnedContext(
                contextFile,
                content);
            if (content == null)
                continue;

            await WriteWorkspaceFileAsync(
                contextFile.Path,
                content,
                cancellationToken);
        }

        await WriteAbsoluteFileAsync(
            TaskPath,
            Encoding.UTF8.GetBytes(
                GmWorkerJson.Serialize(task)),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var proposalParent = Path.GetDirectoryName(ProposalPath)
            ?? throw new InvalidDataException(
                "Worker proposal path has no parent.");
        using var proposalParentAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                _gameSessionAuthority
                ?? throw new ObjectDisposedException(
                    nameof(GmWorkerExecutionWorkspace)),
                proposalParent,
                "Worker proposal parent");
    }

    private static string BuildValidatedRuntimeRoot(
        string canonicalBasePath,
        string runtimeBase)
    {
        EnsureRuntimeOutsideCanonicalSession(
            canonicalBasePath,
            runtimeBase);
        var sessionIdentity = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        NormalizeIdentityPath(
                            canonicalBasePath))))
            .ToLowerInvariant()[..24];
        var runtimeRoot = Path.GetFullPath(
            Path.Combine(
                runtimeBase,
                GmWorkerBridgePool.WorkerRuntimeRoot,
                sessionIdentity));
        EnsureRuntimeOutsideCanonicalSession(
            canonicalBasePath,
            runtimeRoot);
        return runtimeRoot;
    }

    private static void VerifyPinnedContext(
        WorkerFileReference contextFile,
        byte[]? content)
    {
        if (string.Equals(
                contextFile.Sha256,
                "missing",
                StringComparison.OrdinalIgnoreCase))
        {
            if (content != null)
            {
                throw new InvalidOperationException(
                    $"Worker context changed after task creation: {contextFile.Path}.");
            }

            return;
        }

        if (content == null)
        {
            throw new InvalidOperationException(
                $"Worker context disappeared after task creation: {contextFile.Path}.");
        }

        var currentHash = Convert.ToHexString(
                SHA256.HashData(content))
            .ToLowerInvariant();
        if (!string.Equals(
                currentHash,
                contextFile.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Worker context changed after task creation: {contextFile.Path}.");
        }
    }

    private async Task WriteWorkspaceFileAsync(
        string relativePath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var fullPath = ResolveWorkspacePath(
            GameSessionPath,
            relativePath);
        await WriteAbsoluteFileAsync(
            fullPath,
            content,
            cancellationToken);
    }

    private async Task WriteAbsoluteFileAsync(
        string fullPath,
        byte[] content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException(
                "Worker workspace file has no parent.");
        using var parentAuthority =
            PhysicalFileAuthority.EnsureStableDirectory(
                _gameSessionAuthority
                ?? throw new ObjectDisposedException(
                    nameof(GmWorkerExecutionWorkspace)),
                parentPath,
                "Worker workspace file parent");
        if (_hooks?.BeforeWorkspaceFileCreateAsync != null)
        {
            await _hooks.BeforeWorkspaceFileCreateAsync(
                fullPath);
        }

        FileStream? stream = null;
        try
        {
            stream = PhysicalFileAuthority.CreateNewWritableFile(
                parentAuthority,
                fullPath,
                "Worker workspace staging file",
                asynchronous: true,
                requestDeleteAccess: false);
            await stream.WriteAsync(
                content,
                cancellationToken);
            await stream.FlushAsync(
                cancellationToken);
            stream.Flush(flushToDisk: true);
            var authority =
                PhysicalFileAuthority.CaptureOpenedFileAuthority(
                    stream.SafeFileHandle,
                    fullPath,
                    "Worker workspace staging file");
            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                fullPath,
                authority.Identity,
                authority.Sha256,
                "Worker workspace staging file",
                authority.Length);
        }
        finally
        {
            if (stream != null)
                await stream.DisposeAsync();
        }
    }

    private async Task<byte[]?> ReadBoundedFileAsync(
        string fullPath,
        int maxBytes,
        string artifactName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parentPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException(
                $"{artifactName} has no parent.");
        if (_hooks?.BeforeWorkspaceFileOpenAsync != null)
        {
            await _hooks.BeforeWorkspaceFileOpenAsync(
                fullPath);
        }

        PhysicalFileAuthority.StableDirectory? parentAuthority;
        try
        {
            parentAuthority =
                PhysicalFileAuthority.OpenExistingStableDirectory(
                    _gameSessionAuthority
                    ?? throw new ObjectDisposedException(
                        nameof(GmWorkerExecutionWorkspace)),
                    parentPath,
                    artifactName + " parent");
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        using (parentAuthority)
        {
            using var stream = PhysicalFileAuthority.OpenReadFile(
                parentAuthority,
                fullPath,
                artifactName,
                asynchronous: false,
                shareDelete: true);
            if (stream == null)
                return null;

            var authority =
                PhysicalFileAuthority.CaptureOpenedFileAuthority(
                    stream.SafeFileHandle,
                    fullPath,
                    artifactName);
            if (authority.Length > maxBytes)
            {
                throw CreateArtifactLimitException(
                    artifactName,
                    maxBytes);
            }

            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                fullPath,
                authority.Identity,
                authority.Sha256,
                artifactName,
                authority.Length);
            cancellationToken.ThrowIfCancellationRequested();
            var content = PhysicalFileAuthority.ReadOpenedFileBytes(
                stream.SafeFileHandle,
                artifactName);
            cancellationToken.ThrowIfCancellationRequested();
            PhysicalFileAuthority.EnsureExactOpenedFileAuthority(
                stream.SafeFileHandle,
                fullPath,
                authority.Identity,
                authority.Sha256,
                artifactName,
                authority.Length);
            return content;
        }
    }

    private static InvalidDataException CreateArtifactLimitException(
        string artifactName,
        int maxBytes) =>
        new(
            $"{artifactName} exceeds the {maxBytes}-byte import limit.");

    private static string ResolveWorkspacePath(
        string gameSessionPath,
        string relativePath)
    {
        if (!GmWorkerContractValidator.IsSafeRelativePath(
                relativePath))
        {
            throw new InvalidOperationException(
                $"Worker workspace path is unsafe: {relativePath}.");
        }

        var sessionRoot = EnsureTrailingSeparator(
            Path.GetFullPath(gameSessionPath));
        var fullPath = Path.GetFullPath(
            Path.Combine(
                gameSessionPath,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(
                sessionRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Worker workspace path escapes the detached session: {relativePath}.");
        }

        return fullPath;
    }

    private static async Task DeleteWorkspaceAsync(
        PhysicalFileAuthority.StableDirectory runtimeRootAuthority,
        string workspaceRoot,
        PhysicalFileAuthority.FileIdentity? workspaceRootIdentity = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (workspaceRootIdentity == null)
                {
                    PhysicalFileAuthority.TryDeleteDirectoryTree(
                        runtimeRootAuthority,
                        workspaceRoot,
                        "Worker workspace cleanup");
                }
                else
                {
                    PhysicalFileAuthority.TryDeleteDirectoryTree(
                        runtimeRootAuthority,
                        workspaceRoot,
                        "Worker workspace cleanup",
                        workspaceRootIdentity);
                }

                return;
            }
            catch (IOException) when (
                attempt < CleanupRetryCount - 1)
            {
                await Task.Delay(CleanupRetryDelay);
            }
            catch (UnauthorizedAccessException) when (
                attempt < CleanupRetryCount - 1)
            {
                await Task.Delay(CleanupRetryDelay);
            }
        }
    }

    private static void TryDisposeAuthority(
        ref PhysicalFileAuthority.StableDirectory? authority,
        ref List<Exception>? failures)
    {
        if (authority == null)
            return;

        try
        {
            authority.Dispose();
            authority = null;
        }
        catch (Exception ex)
        {
            failures ??= [];
            failures.Add(ex);
        }
    }

    private static void EnsureWorkspaceIsInsideRuntime(
        string runtimeRoot,
        string workspaceRoot)
    {
        var normalizedRuntime = EnsureTrailingSeparator(
            Path.GetFullPath(runtimeRoot));
        var normalizedWorkspace = Path.GetFullPath(
            workspaceRoot);
        if (!normalizedWorkspace.StartsWith(
                normalizedRuntime,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Worker workspace is outside the managed runtime root.");
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string ResolveConfiguredRuntimeBase(
        string configuredBase)
    {
        if (!Path.IsPathRooted(configuredBase))
        {
            throw new InvalidOperationException(
                $"{GmWorkerBridgePool.WorkerRuntimeBaseEnvironmentVariable} must be an absolute path.");
        }

        return Path.GetFullPath(configuredBase);
    }

    private static string ResolveDefaultRuntimeBase(
        string canonicalBasePath)
    {
        var fullBasePath = Path.GetFullPath(
            canonicalBasePath);
        if (OperatingSystem.IsWindows())
        {
            var canonicalVolume = Path.GetPathRoot(
                fullBasePath);
            var systemVolume = Path.GetPathRoot(
                Environment.SystemDirectory);
            if (!string.IsNullOrWhiteSpace(canonicalVolume) &&
                !string.Equals(
                    canonicalVolume,
                    systemVolume,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(
                    canonicalVolume,
                    "BookOfEternityRuntime");
            }
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(
                localApplicationData))
        {
            return Path.Combine(
                localApplicationData,
                "BookOfEternityReborn");
        }

        return Path.Combine(
            Path.GetTempPath(),
            "BookOfEternityReborn");
    }

    private static string NormalizeIdentityPath(string path)
    {
        var normalized = Path.GetFullPath(path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows()
            ? normalized.ToUpperInvariant()
            : normalized;
    }

    private static void EnsureRuntimeOutsideCanonicalSession(
        string canonicalBasePath,
        string candidatePath)
    {
        var canonicalSessionIdentity =
            ResolvePhysicalIdentityPath(
                Path.Combine(
                    Path.GetFullPath(canonicalBasePath),
                    "game_session"));
        var candidateIdentity =
            ResolvePhysicalIdentityPath(candidatePath);
        if (!IsSameOrDescendant(
                candidateIdentity,
                canonicalSessionIdentity))
        {
            return;
        }

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
        var relative = Path.GetRelativePath(
            root,
            fullPath);
        if (string.Equals(
                relative,
                ".",
                StringComparison.Ordinal))
        {
            return NormalizeIdentityPath(current);
        }

        foreach (var segment in relative.Split(
                     [
                         Path.DirectorySeparatorChar,
                         Path.AltDirectorySeparatorChar
                     ],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(
                current,
                segment);
            FileSystemInfo? entry = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : File.Exists(candidate)
                    ? new FileInfo(candidate)
                    : null;
            if (entry != null &&
                (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var resolved = entry.ResolveLinkTarget(
                    returnFinalTarget: true);
                current = resolved == null
                    ? candidate
                    : Path.GetFullPath(resolved.FullName);
                continue;
            }

            current = candidate;
        }

        return NormalizeIdentityPath(current);
    }

    private static bool IsSameOrDescendant(
        string candidatePath,
        string rootPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(
                candidatePath,
                rootPath,
                comparison))
        {
            return true;
        }

        var rootWithSeparator = EnsureTrailingSeparator(
            rootPath);
        return candidatePath.StartsWith(
            rootWithSeparator,
            comparison);
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(
            value
                .Select(character =>
                    invalid.Contains(character)
                        ? '_'
                        : character)
                .ToArray());
        return string.IsNullOrWhiteSpace(sanitized)
            ? "worker-task"
            : sanitized;
    }

    private static void EnsureQuarantineAuditReceiptMatches(
        FileStream stream,
        string receiptPath,
        byte[] expectedBytes)
    {
        var actualBytes = PhysicalFileAuthority.ReadOpenedFileBytes(
            stream.SafeFileHandle,
            "Worker quarantine audit receipt");
        PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
            stream.SafeFileHandle,
            receiptPath,
            "Worker quarantine audit receipt");
        if (!actualBytes.AsSpan().SequenceEqual(expectedBytes))
        {
            throw new InvalidDataException(
                "Worker quarantine audit receipt identity is already bound to different evidence.");
        }
    }

    private sealed record GmWorkerQuarantineAuditReceipt(
        int SchemaVersion,
        string SessionGeneration,
        WorkerAuditEvent AuditEvent);
}
