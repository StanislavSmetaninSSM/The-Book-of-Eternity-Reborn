using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed class GmWorkerBridgePoolHooks
{
    internal Func<Task>? BeforeWorkerSlotWaitAsync { get; init; }
    internal Func<Task>? BeforeTaskReservationAsync { get; init; }
    internal Func<Task>? BeforeProposalPublicationAsync { get; init; }
}

public sealed class GmWorkerBridgePool
{
    public const string TaskRoot = "worker_tasks";
    public const string ProposalInboxRoot = "worker_proposals/inbox";
    public const string WorkerRuntimeRoot = ".worker_runtime";
    public const string TaskPathEnvironmentVariable = "BOE_WORKER_TASK_PATH";
    public const string ProposalPathEnvironmentVariable = "BOE_WORKER_PROPOSAL_PATH";
    public const string SessionPathEnvironmentVariable = "BOE_WORKER_SESSION_PATH";

    private readonly FileSystemManager _fs;
    private readonly GmWorkerProposalStore _proposalStore;
    private readonly GmWorkerAuditLog? _auditLog;
    private readonly GmWorkerBridgePoolHooks? _hooks;
    private static readonly ConcurrentDictionary<string, WorkerConcurrencyGate> WorkerConcurrencyGates =
        new(StringComparer.OrdinalIgnoreCase);

    public GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore = null,
        GmWorkerAuditLog? auditLog = null)
        : this(fs, proposalStore, auditLog, hooks: null)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks)
    {
        _fs = fs;
        _proposalStore = proposalStore ?? new GmWorkerProposalStore(fs);
        _auditLog = auditLog;
        _hooks = hooks;
    }

    public static WorkerRoutingResult SelectWorkerForTask(
        IReadOnlyList<WorkerBridgeProfile> profiles,
        WorkerTaskType taskType)
    {
        foreach (var profile in profiles)
        {
            if (!profile.Enabled)
                continue;
            var validation = GmWorkerContractValidator.ValidateProfile(profile);
            if (!validation.IsValid)
                continue;
            if (profile.Permissions.TaskTypes.Contains(taskType))
                return new WorkerRoutingResult(true, profile, "");
        }

        return new WorkerRoutingResult(false, null, $"No enabled worker profile can handle task type {taskType}.");
    }

    public static IReadOnlyList<WorkerBridgeStatus> BuildInitialStatuses(IReadOnlyList<WorkerBridgeProfile> profiles)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return profiles
            .Select(profile => new WorkerBridgeStatus
            {
                WorkerId = profile.WorkerId,
                State = profile.Enabled ? WorkerBridgeState.Stopped : WorkerBridgeState.Disabled,
                Ready = false,
                UpdatedAtUtc = now
            })
            .ToArray();
    }

    public async Task<GmWorkerTaskRunResult> RunTaskAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        CancellationToken cancellationToken = default)
    {
        var statusHistory = new List<WorkerBridgeStatus>();
        WorkerBridgeStatus Track(
            WorkerBridgeState state,
            bool ready,
            string? lastError = null,
            int? processId = null)
        {
            var status = CreateStatus(profile, state, ready, task.TaskId, lastError, processId);
            statusHistory.Add(status);
            return status;
        }

        if (!profile.Enabled)
        {
            var status = Track(WorkerBridgeState.Disabled, ready: false, "Worker profile is disabled.");
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
        {
            var status = Track(WorkerBridgeState.Failed, ready: false, string.Join(Environment.NewLine, taskValidation.Errors));
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }

        var slotAcquisition = await AcquireWorkerSlotAsync(profile, cancellationToken);
        if (slotAcquisition.Lease == null)
        {
            var status = Track(WorkerBridgeState.Failed, ready: false, slotAcquisition.Error);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }

        using var workerSlot = slotAcquisition.Lease;

        var taskPath = GetTaskPacketPath(task.TaskId);
        var proposalInboxPath = GetProposalInboxPath(task.TaskId);
        var taskBytes = EncodeUtf8WithPreamble(GmWorkerJson.Serialize(task));
        if (_hooks?.BeforeTaskReservationAsync != null)
            await _hooks.BeforeTaskReservationAsync();
        var taskReserved = await TryReserveTaskAsync(taskPath, proposalInboxPath, taskBytes);
        if (!taskReserved)
        {
            var message = $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.";
            var status = Track(WorkerBridgeState.Failed, ready: false, message);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }

        var proposalInboxDirectory = Path.GetDirectoryName(_fs.ResolvePath(proposalInboxPath));
        if (!string.IsNullOrWhiteSpace(proposalInboxDirectory))
            Directory.CreateDirectory(proposalInboxDirectory);
        if (_auditLog != null)
            await _auditLog.RecordTaskDispatchedAsync(task);

        Process? process = null;
        var processStarted = false;
        GmWorkerExecutionWorkspace? workspace = null;
        try
        {
            workspace = await GmWorkerExecutionWorkspace.CreateAsync(_fs, task);
            Track(WorkerBridgeState.Starting, ready: false);
            var startInfo = CreateWorkerStartInfo(profile, workspace.GameSessionPath);
            startInfo.Environment[TaskPathEnvironmentVariable] = workspace.TaskPath;
            startInfo.Environment[ProposalPathEnvironmentVariable] = workspace.ProposalPath;
            startInfo.Environment[SessionPathEnvironmentVariable] = workspace.GameSessionPath;

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            if (!process.Start())
            {
                var error = "Worker process did not start.";
                await RecordTerminalEventAsync("task-failed", profile, task, error, []);
                var status = Track(WorkerBridgeState.Failed, ready: false, error);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray()
                };
            }
            processStarted = true;

            var processId = process.Id;
            Track(WorkerBridgeState.Busy, ready: false, processId: processId);
            var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var timeout = TimeSpan.FromSeconds(Math.Max(1, profile.TimeoutSeconds));
            var waitTask = process.WaitForExitAsync(CancellationToken.None);
            var timeoutTask = Task.Delay(timeout, CancellationToken.None);
            var cancellationTask = cancellationToken.CanBeCanceled
                ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                : Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None);
            var completed = await Task.WhenAny(waitTask, timeoutTask, cancellationTask);

            if (completed == cancellationTask)
            {
                await StopProcessTreeAsync(process, waitTask);
                await ReadProcessOutputAsync(outputTask);
                await ReadProcessOutputAsync(errorTask);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (completed == timeoutTask)
            {
                await StopProcessTreeAsync(process, waitTask);
                var output = await ReadProcessOutputAsync(outputTask);
                var stderr = await ReadProcessOutputAsync(errorTask);
                var message = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                var existingProposalResult = await TryReadAndStoreExistingProposalAsync(
                    profile,
                    task,
                    taskPath,
                    taskBytes,
                    proposalInboxPath,
                    workspace);
                if (existingProposalResult.Attempted)
                {
                    var proposalRejected = existingProposalResult.Proposal == null;
                    var state = proposalRejected ? WorkerBridgeState.TimedOut : WorkerBridgeState.Stopped;
                    var terminalMessage = proposalRejected
                        ? $"{message} Proposal handoff was rejected: {existingProposalResult.Result.Status.LastError}"
                        : message;
                    if (proposalRejected)
                        await RecordTerminalEventAsync("task-timed-out", profile, task, terminalMessage, []);
                    var proposalStatus = Track(
                        state,
                        ready: false,
                        terminalMessage,
                        processId);
                    return existingProposalResult.Result with
                    {
                        Status = proposalStatus,
                        StatusHistory = statusHistory.ToArray(),
                        Proposal = existingProposalResult.Proposal,
                        StandardOutput = output,
                        StandardError = stderr,
                        TimedOut = true
                    };
                }

                await RecordTerminalEventAsync("task-timed-out", profile, task, message, stderr.Length == 0 ? [] : [stderr]);
                var status = Track(WorkerBridgeState.TimedOut, ready: false, message, processId);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    StandardOutput = output,
                    StandardError = stderr,
                    TimedOut = true
                };
            }
            await waitTask;

            var standardOutput = await ReadProcessOutputAsync(outputTask);
            var standardError = await ReadProcessOutputAsync(errorTask);
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                var message = $"Worker process exited with code {exitCode}.";
                var existingProposalResult = await TryReadAndStoreExistingProposalAsync(
                    profile,
                    task,
                    taskPath,
                    taskBytes,
                    proposalInboxPath,
                    workspace);
                if (existingProposalResult.Attempted)
                {
                    var state = existingProposalResult.Proposal == null
                        ? existingProposalResult.Result.Status.State
                        : WorkerBridgeState.Stopped;
                    var proposalStatus = Track(
                        state,
                        ready: false,
                        existingProposalResult.Proposal == null ? existingProposalResult.Result.Status.LastError : message,
                        processId);
                    return existingProposalResult.Result with
                    {
                        Status = proposalStatus,
                        StatusHistory = statusHistory.ToArray(),
                        Proposal = existingProposalResult.Proposal,
                        ExitCode = exitCode,
                        StandardOutput = standardOutput,
                        StandardError = standardError
                    };
                }

                await RecordTerminalEventAsync("task-failed", profile, task, message, standardError.Length == 0 ? [] : [standardError]);
                var status = Track(WorkerBridgeState.Failed, ready: false, message, processId);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    ExitCode = exitCode,
                    StandardOutput = standardOutput,
                    StandardError = standardError
                };
            }

            var proposalResult = await ReadAndStoreProposalAsync(
                profile,
                task,
                taskPath,
                taskBytes,
                proposalInboxPath,
                workspace);
            if (proposalResult.Proposal == null)
            {
                var status = Track(
                    proposalResult.Result.Status.State,
                    proposalResult.Result.Status.Ready,
                    proposalResult.Result.Status.LastError,
                    processId);
                return proposalResult.Result with
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    ExitCode = exitCode,
                    StandardOutput = standardOutput,
                    StandardError = standardError
                };
            }

            var stoppedStatus = Track(WorkerBridgeState.Stopped, ready: false, processId: processId);
            return new GmWorkerTaskRunResult
            {
                Status = stoppedStatus,
                StatusHistory = statusHistory.ToArray(),
                Proposal = proposalResult.Proposal,
                ExitCode = exitCode,
                StandardOutput = standardOutput,
                StandardError = standardError
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordTerminalEventAsync("task-failed", profile, task, ex.Message, [ex.GetType().Name]);
            var status = Track(WorkerBridgeState.Failed, ready: false, ex.Message);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }
        finally
        {
            if (processStarted && process is { HasExited: false })
            {
                try
                {
                    await StopProcessTreeAsync(process, process.WaitForExitAsync(CancellationToken.None));
                }
                catch
                {
                    // The primary result remains authoritative; cleanup is best effort here.
                }
            }

            process?.Dispose();
            if (workspace != null)
            {
                try
                {
                    await workspace.DisposeAsync();
                }
                catch (Exception cleanupException) when (
                    cleanupException is IOException or UnauthorizedAccessException)
                {
                    try
                    {
                        await RecordTerminalEventAsync(
                            "workspace-cleanup-failed",
                            profile,
                            task,
                            cleanupException.Message,
                            [cleanupException.GetType().Name]);
                    }
                    catch (Exception auditException) when (
                        auditException is IOException or UnauthorizedAccessException)
                    {
                        // Cleanup diagnostics must not replace the completed worker result either.
                    }
                }
            }
        }
    }

    public static string GetTaskPacketPath(string taskId) =>
        $"{TaskRoot}/{taskId}/task.json";

    public static string GetProposalInboxPath(string taskId) =>
        $"{ProposalInboxRoot}/{taskId}/proposal.json";

    public static ProcessStartInfo CreateWorkerStartInfo(WorkerBridgeProfile profile, string workingDirectory)
    {
        var validation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, validation.Errors), nameof(profile));

        var command = SplitCommandLine(profile.LaunchCommand);
        if (command.Count == 0)
            throw new ArgumentException("Worker launchCommand must contain an executable.", nameof(profile));

        var startInfo = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        for (var i = 1; i < command.Count; i++)
            startInfo.ArgumentList.Add(ResolveLaunchArgument(command, i, workingDirectory));

        return startInfo;
    }

    private static string ResolveLaunchArgument(IReadOnlyList<string> command, int index, string workingDirectory)
    {
        if (index <= 0 || !string.Equals(command[index - 1], "-File", StringComparison.OrdinalIgnoreCase))
            return command[index];

        var path = command[index];
        if (Path.IsPathRooted(path))
            return path;

        foreach (var candidateRoot in EnumerateLaunchPathRoots(workingDirectory))
        {
            var candidate = Path.GetFullPath(Path.Combine(candidateRoot, path));
            if (File.Exists(candidate))
                return candidate;
        }

        return path;
    }

    private static IEnumerable<string> EnumerateLaunchPathRoots(string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            yield return workingDirectory;

        yield return Environment.CurrentDirectory;

        foreach (var root in EnumerateAncestors(AppContext.BaseDirectory))
            yield return root;
    }

    private static IEnumerable<string> EnumerateAncestors(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        while (directory != null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    internal static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
            return result;

        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];
            if (ch == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
            {
                current.Append('"');
                i++;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                AddCurrent();
                continue;
            }

            current.Append(ch);
        }

        AddCurrent();
        return result;

        void AddCurrent()
        {
            if (current.Length == 0)
                return;
            result.Add(current.ToString());
            current.Clear();
        }
    }

    private async Task<(WorkerProposal? Proposal, GmWorkerTaskRunResult Result)> ReadAndStoreProposalAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string taskPath,
        byte[] expectedTaskBytes,
        string proposalInboxPath,
        GmWorkerExecutionWorkspace workspace)
    {
        var proposalBytes = await workspace.ReadProposalBytesAsync();
        var proposalJson = DecodeUtf8(proposalBytes);
        if (string.IsNullOrWhiteSpace(proposalJson))
        {
            const string message = "Worker completed without writing a proposal.";
            await RecordTerminalEventAsync("task-failed", profile, task, message, []);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
            });
        }

        WorkerProposal? proposal;
        try
        {
            proposal = GmWorkerJson.Deserialize<WorkerProposal>(proposalJson);
        }
        catch (Exception ex)
        {
            var message = $"Worker proposal JSON is malformed: {ex.Message}";
            await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [ex.GetType().Name]);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
            });
        }

        var proposalValidation = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);
        if (!proposalValidation.IsValid)
        {
            var message = string.Join(Environment.NewLine, proposalValidation.Errors);
            await RecordTerminalEventAsync("proposal-rejected", profile, task, message, proposalValidation.Errors);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
            });
        }

        var importedContent = new Dictionary<string, byte[]>(GmWorkerContractValidator.CanonicalPathComparer);
        foreach (var changedFile in proposal!.ChangedFiles)
        {
            if (changedFile.ChangeKind == WorkerFileChangeKind.Delete)
                continue;

            var content = await workspace.ReadFileBytesAsync(changedFile.ContentRef!);
            if (content == null)
            {
                var message = $"Worker proposal contentRef is missing from detached execution output: {changedFile.ContentRef}.";
                await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [changedFile.ContentRef!]);
                return (null, new GmWorkerTaskRunResult
                {
                    Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
                });
            }

            var actualAfterSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (!string.Equals(actualAfterSha256, changedFile.AfterSha256, StringComparison.OrdinalIgnoreCase))
            {
                var message =
                    $"Worker proposal contentRef bytes do not match afterSha256: {changedFile.ContentRef}.";
                await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [changedFile.ContentRef!]);
                return (null, new GmWorkerTaskRunResult
                {
                    Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
                });
            }

            importedContent[changedFile.ContentRef!] = content;
        }

        if (_hooks?.BeforeProposalPublicationAsync != null)
            await _hooks.BeforeProposalPublicationAsync();
        var publication = await _proposalStore.PublishBundleAsync(
            proposal!,
            proposalBytes!,
            importedContent,
            taskPath,
            expectedTaskBytes,
            proposalInboxPath,
            _auditLog == null
                ? null
                : lease => _auditLog.RecordProposalReceivedAsync(lease, proposal!));
        if (!publication.Published)
        {
            var message = publication.Error ?? "Worker proposal bundle publication was rejected.";
            await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [proposal.ProposalId]);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
            });
        }

        return (proposal, new GmWorkerTaskRunResult());
    }

    private async Task<bool> TryReserveTaskAsync(
        string taskPath,
        string proposalInboxPath,
        byte[] taskBytes)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (_fs.FileExists(taskPath) || _fs.FileExists(proposalInboxPath))
            return false;

        return await _fs.CompareExchangeFileBytesAsync(
                   writeLease,
                   taskPath,
                   expectedContent: null,
                   desiredContent: taskBytes) == CanonicalFileMutationResult.Applied;
    }

    private async Task<WorkerSlotAcquisition> AcquireWorkerSlotAsync(
        WorkerBridgeProfile profile,
        CancellationToken cancellationToken)
    {
        var sessionPath = Path.GetFullPath(_fs.GameSessionPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var key = $"{sessionPath}|{profile.WorkerId}";
        while (true)
        {
            var gate = WorkerConcurrencyGates.GetOrAdd(
                key,
                _ => new WorkerConcurrencyGate(profile.MaxConcurrentTasks));
            var retry = false;
            var disposeGate = false;
            lock (gate.Sync)
            {
                if (!WorkerConcurrencyGates.TryGetValue(key, out var currentGate) ||
                    !ReferenceEquals(currentGate, gate))
                {
                    retry = true;
                }
                else if (gate.Limit != profile.MaxConcurrentTasks)
                {
                    if (gate.ReferenceCount == 0)
                    {
                        if (WorkerConcurrencyGates.TryRemove(key, out var removed) &&
                            ReferenceEquals(removed, gate))
                        {
                            disposeGate = true;
                        }
                        retry = true;
                    }
                    else
                    {
                        return WorkerSlotAcquisition.Failed(
                            $"Worker profile {profile.WorkerId} changed maxConcurrentTasks from {gate.Limit} " +
                            $"to {profile.MaxConcurrentTasks} while tasks are active.");
                    }
                }
                else
                {
                    gate.ReferenceCount++;
                }
            }

            if (disposeGate)
                gate.Dispose();
            if (retry)
                continue;

            try
            {
                if (_hooks?.BeforeWorkerSlotWaitAsync != null)
                    await _hooks.BeforeWorkerSlotWaitAsync();
                await gate.Semaphore.WaitAsync(cancellationToken);
                return WorkerSlotAcquisition.Acquired(new WorkerSlotLease(key, gate));
            }
            catch
            {
                ReleaseWorkerSlotReference(key, gate, releaseSemaphore: false);
                throw;
            }
        }
    }

    private static byte[] EncodeUtf8WithPreamble(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    private sealed class WorkerConcurrencyGate : IDisposable
    {
        internal WorkerConcurrencyGate(int limit)
        {
            Limit = limit;
            Semaphore = new SemaphoreSlim(limit, limit);
        }

        internal object Sync { get; } = new();
        internal int Limit { get; }
        internal int ReferenceCount { get; set; }
        internal SemaphoreSlim Semaphore { get; }

        public void Dispose() => Semaphore.Dispose();
    }

    private static void ReleaseWorkerSlotReference(
        string key,
        WorkerConcurrencyGate gate,
        bool releaseSemaphore)
    {
        if (releaseSemaphore)
            gate.Semaphore.Release();

        var disposeGate = false;
        lock (gate.Sync)
        {
            gate.ReferenceCount--;
            if (gate.ReferenceCount < 0)
                throw new InvalidOperationException("Worker concurrency gate reference count became negative.");
            if (gate.ReferenceCount == 0 &&
                WorkerConcurrencyGates.TryRemove(key, out var removed) &&
                ReferenceEquals(removed, gate))
            {
                disposeGate = true;
            }
        }

        if (disposeGate)
            gate.Dispose();
    }

    private sealed class WorkerSlotLease(string key, WorkerConcurrencyGate gate) : IDisposable
    {
        private WorkerConcurrencyGate? _gate = gate;

        public void Dispose()
        {
            var ownedGate = Interlocked.Exchange(ref _gate, null);
            if (ownedGate != null)
                ReleaseWorkerSlotReference(key, ownedGate, releaseSemaphore: true);
        }
    }

    private sealed record WorkerSlotAcquisition(WorkerSlotLease? Lease, string? Error)
    {
        internal static WorkerSlotAcquisition Acquired(WorkerSlotLease lease) => new(lease, null);
        internal static WorkerSlotAcquisition Failed(string error) => new(null, error);
    }

    private async Task<(bool Attempted, WorkerProposal? Proposal, GmWorkerTaskRunResult Result)> TryReadAndStoreExistingProposalAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string taskPath,
        byte[] expectedTaskBytes,
        string proposalInboxPath,
        GmWorkerExecutionWorkspace workspace)
    {
        if (await workspace.ReadProposalBytesAsync() == null)
            return (false, null, new GmWorkerTaskRunResult());

        var proposalResult = await ReadAndStoreProposalAsync(
            profile,
            task,
            taskPath,
            expectedTaskBytes,
            proposalInboxPath,
            workspace);
        return (true, proposalResult.Proposal, proposalResult.Result);
    }

    private static string? DecodeUtf8(byte[]? bytes)
    {
        if (bytes == null)
            return null;
        var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble())
            ? Encoding.UTF8.GetPreamble().Length
            : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private static WorkerBridgeStatus CreateStatus(
        WorkerBridgeProfile profile,
        WorkerBridgeState state,
        bool ready,
        string? currentTaskId,
        string? lastError,
        int? processId = null) =>
        new()
        {
            WorkerId = profile.WorkerId,
            State = state,
            Ready = ready,
            ProcessId = processId,
            CurrentTaskId = currentTaskId,
            LastError = lastError,
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };

    private Task RecordTerminalEventAsync(
        string eventType,
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string summary,
        IReadOnlyList<string> details) =>
        _auditLog?.AppendEventAsync(new WorkerAuditEvent
        {
            EventId = GmWorkerAuditEventIdGenerator.Create(),
            EventType = eventType,
            WorkerId = profile.WorkerId,
            TaskId = task.TaskId,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            Summary = summary,
            Details = details.Count == 0
                ? new Dictionary<string, IReadOnlyList<string>>()
                : new Dictionary<string, IReadOnlyList<string>>
                {
                    ["details"] = details
                }
        }) ?? Task.CompletedTask;

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have exited between timeout detection and kill.
        }
    }

    private static async Task StopProcessTreeAsync(Process process, Task waitTask)
    {
        TryKillProcess(process);
        await waitTask;
        if (!process.HasExited)
            throw new IOException("Worker process tree did not exit after termination.");
    }

    private static async Task<string> ReadProcessOutputAsync(Task<string> outputTask)
    {
        try
        {
            return await outputTask;
        }
        catch
        {
            return "";
        }
    }
}
