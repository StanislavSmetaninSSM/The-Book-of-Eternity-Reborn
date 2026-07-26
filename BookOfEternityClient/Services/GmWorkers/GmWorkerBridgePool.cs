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
    internal Func<Task>? BeforeTaskDispatchAuditAsync { get; init; }
    internal Func<Task>? BeforeProposalPublicationAsync { get; init; }
    internal Func<Task>? BeforeProcessTreeAttachAsync { get; init; }
    internal Func<Task>? BeforeWorkerReleaseAsync { get; init; }
    internal Func<string, Task>? BeforeWorkspaceCleanupAsync { get; init; }
    internal CancellationToken TimeoutSignal { get; init; }
}

internal enum GmWorkerProcessCompletionOutcomeKind
{
    Completed,
    Canceled,
    TimedOut,
    HostExited
}

internal readonly record struct GmWorkerProcessCompletionOutcome(
    GmWorkerProcessCompletionOutcomeKind Kind,
    int? ExitCode = null);

internal static class GmWorkerProcessCompletionArbiter
{
    internal static async Task<GmWorkerProcessCompletionOutcome> WaitAsync(
        Task<int> workerCompletionTask,
        Func<CancellationToken, Task> waitForOutputDrainAsync,
        Task hostExitTask,
        CancellationToken timeoutToken,
        CancellationToken cancellationToken)
    {
        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutToken);
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        _ = await Task.WhenAny(
            workerCompletionTask,
            hostExitTask,
            timeoutTask,
            cancellationTask);

        if (cancellationToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.Canceled);
        if (timeoutToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.TimedOut);
        if (!workerCompletionTask.IsCompleted && hostExitTask.IsCompleted)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.HostExited);

        var exitCode = await workerCompletionTask;
        if (cancellationToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.Canceled);
        if (timeoutToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.TimedOut);

        using var drainCancellation = new CancellationTokenSource();
        var drainTask = waitForOutputDrainAsync(drainCancellation.Token);
        _ = await Task.WhenAny(
            drainTask,
            hostExitTask,
            timeoutTask,
            cancellationTask);

        if (cancellationToken.IsCancellationRequested)
        {
            CancelAndObserve(drainCancellation, drainTask);
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.Canceled);
        }
        if (timeoutToken.IsCancellationRequested)
        {
            CancelAndObserve(drainCancellation, drainTask);
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.TimedOut);
        }
        if (!drainTask.IsCompleted && hostExitTask.IsCompleted)
        {
            CancelAndObserve(drainCancellation, drainTask);
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.HostExited);
        }

        await drainTask;
        if (cancellationToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.Canceled);
        if (timeoutToken.IsCancellationRequested)
            return new GmWorkerProcessCompletionOutcome(GmWorkerProcessCompletionOutcomeKind.TimedOut);
        return new GmWorkerProcessCompletionOutcome(
            GmWorkerProcessCompletionOutcomeKind.Completed,
            exitCode);
    }

    private static void CancelAndObserve(CancellationTokenSource cancellation, Task task)
    {
        cancellation.Cancel();
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

public sealed class GmWorkerBridgePool
{
    public const string TaskRoot = "worker_tasks";
    public const string ProposalInboxRoot = "worker_proposals/inbox";
    public const string WorkerRuntimeRoot = ".worker_runtime";
    public const string TaskPathEnvironmentVariable = "BOE_WORKER_TASK_PATH";
    public const string ProposalPathEnvironmentVariable = "BOE_WORKER_PROPOSAL_PATH";
    public const string SessionPathEnvironmentVariable = "BOE_WORKER_SESSION_PATH";
    public const string WorkerRuntimeBaseEnvironmentVariable = "BOE_WORKER_RUNTIME_BASE_PATH";
    internal const int MaxCapturedProcessOutputCharacters = 64 * 1024;
    internal const int MaxProposalBytes = 1024 * 1024;
    internal const int MaxContentRefBytes = 4 * 1024 * 1024;
    internal const int MaxImportedContentBytes = 16 * 1024 * 1024;

    private readonly FileSystemManager _fs;
    private readonly GmWorkerProposalStore _proposalStore;
    private readonly GmWorkerAuditLog? _auditLog;
    private readonly GmWorkerBridgePoolHooks? _hooks;
    private readonly IGmWorkerProcessTreeFactory _processTreeFactory;
    private static readonly ConcurrentDictionary<string, WorkerConcurrencyGate> WorkerConcurrencyGates =
        new(StringComparer.OrdinalIgnoreCase);

    public GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore = null,
        GmWorkerAuditLog? auditLog = null)
        : this(fs, proposalStore, auditLog, hooks: null, GmWorkerProcessTreeFactory.Instance)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks)
        : this(fs, proposalStore, auditLog, hooks, GmWorkerProcessTreeFactory.Instance)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks,
        IGmWorkerProcessTreeFactory processTreeFactory)
    {
        _fs = fs;
        _proposalStore = proposalStore ?? new GmWorkerProposalStore(fs);
        _auditLog = auditLog;
        _hooks = hooks;
        _processTreeFactory = processTreeFactory;
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
        if (_hooks?.BeforeTaskReservationAsync != null)
            await _hooks.BeforeTaskReservationAsync();
        var reservation = await TryReserveTaskAsync(task, taskPath, proposalInboxPath);
        if (!reservation.Reserved)
        {
            var message = reservation.Error ??
                          $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.";
            var status = Track(WorkerBridgeState.Failed, ready: false, message);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray(),
                SessionReplaced = reservation.SessionReplaced
            };
        }

        task = reservation.Task!;
        var taskBytes = reservation.TaskBytes!;

        if (_hooks?.BeforeTaskDispatchAuditAsync != null)
            await _hooks.BeforeTaskDispatchAuditAsync();
        if (_auditLog != null &&
            !await _auditLog.RecordTaskDispatchedIfCurrentSessionAsync(task))
        {
            const string message =
                "Worker task context does not belong to the current game session generation.";
            var status = Track(WorkerBridgeState.Failed, ready: false, message);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray(),
                BoundTask = task,
                SessionReplaced = true
            };
        }

        Process? process = null;
        IGmWorkerProcessTree? processTree = null;
        GmWorkerProcessHostLaunch? processHostLaunch = null;
        var processStarted = false;
        GmWorkerExecutionWorkspace? workspace = null;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, profile.TimeoutSeconds));
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _hooks?.TimeoutSignal ?? CancellationToken.None);
        timeoutCancellation.CancelAfter(timeout);
        using var lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        using var completionWaitCancellation = new CancellationTokenSource();
        Task<int>? workerCompletionTask = null;
        try
        {
            workspace = await GmWorkerExecutionWorkspace.CreateAsync(_fs, task);
            Track(WorkerBridgeState.Starting, ready: false);
            var workerStartInfo = CreateWorkerStartInfo(profile, workspace.GameSessionPath);
            workerStartInfo.Environment[TaskPathEnvironmentVariable] = workspace.TaskPath;
            workerStartInfo.Environment[ProposalPathEnvironmentVariable] = workspace.ProposalPath;
            workerStartInfo.Environment[SessionPathEnvironmentVariable] = workspace.GameSessionPath;
            processHostLaunch = GmWorkerProcessHostLaunch.Create(
                workerStartInfo,
                Path.GetDirectoryName(workspace.GameSessionPath)!);

            process = new Process
            {
                StartInfo = processHostLaunch.StartInfo,
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
            try
            {
                if (_hooks?.BeforeProcessTreeAttachAsync != null)
                {
                    await _hooks.BeforeProcessTreeAttachAsync()
                        .WaitAsync(lifecycleCancellation.Token);
                }
                processTree = _processTreeFactory.Attach(process);
                await processHostLaunch.WaitUntilReadyAsync(
                    process,
                    lifecycleCancellation.Token);
                if (_hooks?.BeforeWorkerReleaseAsync != null)
                {
                    await _hooks.BeforeWorkerReleaseAsync()
                        .WaitAsync(lifecycleCancellation.Token);
                }
                await processHostLaunch.ReleaseAsync(lifecycleCancellation.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                var message = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                await RecordTerminalEventAsync("task-timed-out", profile, task, message, []);
                var status = Track(WorkerBridgeState.TimedOut, ready: false, message, process.Id);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    BoundTask = task,
                    TimedOut = true
                };
            }

            var processId = process.Id;
            Track(WorkerBridgeState.Busy, ready: false, processId: processId);
            var outputTask = CaptureProcessOutputAsync(process.StandardOutput);
            var errorTask = CaptureProcessOutputAsync(process.StandardError);
            var waitTask = process.WaitForExitAsync(CancellationToken.None);
            workerCompletionTask = processHostLaunch.WaitForWorkerCompletionAsync(
                process,
                completionWaitCancellation.Token);
            var completionOutcome = await GmWorkerProcessCompletionArbiter.WaitAsync(
                workerCompletionTask,
                token => processHostLaunch.WaitForOutputDrainAsync(process, token),
                waitTask,
                timeoutCancellation.Token,
                cancellationToken);

            if (completionOutcome.Kind == GmWorkerProcessCompletionOutcomeKind.Canceled)
            {
                completionWaitCancellation.Cancel();
                try
                {
                    await processTree.StopAndWaitAsync();
                }
                catch (Exception)
                {
                    workerSlot.Quarantine();
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (completionOutcome.Kind == GmWorkerProcessCompletionOutcomeKind.TimedOut)
            {
                completionWaitCancellation.Cancel();
                var cleanupConfirmed = true;
                try
                {
                    await processTree.StopAndWaitAsync();
                }
                catch (Exception)
                {
                    workerSlot.Quarantine();
                    cleanupConfirmed = false;
                }
                var output = cleanupConfirmed ? await ReadProcessOutputAsync(outputTask) : "";
                var stderr = cleanupConfirmed ? await ReadProcessOutputAsync(errorTask) : "";
                var message = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                await RecordTerminalEventAsync("task-timed-out", profile, task, message, stderr.Length == 0 ? [] : [stderr]);
                var status = Track(WorkerBridgeState.TimedOut, ready: false, message, processId);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    BoundTask = task,
                    StandardOutput = output,
                    StandardError = stderr,
                    TimedOut = true
                };
            }
            if (completionOutcome.Kind == GmWorkerProcessCompletionOutcomeKind.HostExited)
            {
                await waitTask;
                throw new InvalidOperationException(
                    $"Worker process host exited before worker completion with code {process.ExitCode}.");
            }

            var exitCode = completionOutcome.ExitCode!.Value;
            await processTree.StopAndWaitAsync();
            await waitTask;

            var standardOutput = await ReadProcessOutputAsync(outputTask);
            var standardError = await ReadProcessOutputAsync(errorTask);
            if (exitCode != 0)
            {
                var message = $"Worker process exited with code {exitCode}.";
                await RecordTerminalEventAsync("task-failed", profile, task, message, standardError.Length == 0 ? [] : [standardError]);
                var status = Track(WorkerBridgeState.Failed, ready: false, message, processId);
                return new GmWorkerTaskRunResult
                {
                    Status = status,
                    StatusHistory = statusHistory.ToArray(),
                    BoundTask = task,
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
                workspace,
                lifecycleCancellation.Token);
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
                BoundTask = task,
                ExitCode = exitCode,
                StandardOutput = standardOutput,
                StandardError = standardError
            };
        }
        catch (OperationCanceledException) when (
            timeoutCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            var message = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
            await RecordTerminalEventAsync("task-timed-out", profile, task, message, []);
            var status = Track(WorkerBridgeState.TimedOut, ready: false, message);
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray(),
                BoundTask = task,
                TimedOut = true
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
            completionWaitCancellation.Cancel();
            Exception? processCleanupFailure = null;
            if (processStarted)
            {
                try
                {
                    if (processTree != null)
                        await processTree.StopAndWaitAsync();
                    else if (process != null)
                        await StopUnattachedProcessTreeAsync(process);
                }
                catch (Exception ex)
                {
                    workerSlot.Quarantine();
                    processCleanupFailure = ex;
                }
            }

            if (processTree != null)
            {
                try
                {
                    await processTree.DisposeAsync();
                }
                catch (Exception ex)
                {
                    workerSlot.Quarantine();
                    processCleanupFailure ??= ex;
                }
            }

            if (workerCompletionTask != null)
            {
                try
                {
                    await workerCompletionTask;
                }
                catch (Exception)
                {
                    // Completion observation is subordinate to process-tree cleanup and result state.
                }
            }

            process?.Dispose();
            if (processHostLaunch != null)
                await processHostLaunch.DisposeAsync();
            if (workspace != null)
            {
                try
                {
                    if (_hooks?.BeforeWorkspaceCleanupAsync != null)
                        await _hooks.BeforeWorkspaceCleanupAsync(workspace.GameSessionPath);
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

            if (processCleanupFailure != null)
            {
                try
                {
                    await RecordTerminalEventAsync(
                        "process-tree-cleanup-unconfirmed",
                        profile,
                        task,
                        processCleanupFailure.Message,
                        [processCleanupFailure.GetType().Name]);
                }
                catch (Exception auditException) when (
                    auditException is IOException or UnauthorizedAccessException)
                {
                    // Cleanup uncertainty remains subordinate to the authoritative task outcome.
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
        GmWorkerExecutionWorkspace workspace,
        CancellationToken cancellationToken)
    {
        byte[]? proposalBytes;
        try
        {
            proposalBytes = await workspace.ReadProposalBytesAsync(cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            await RecordTerminalEventAsync("proposal-rejected", profile, task, ex.Message, []);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, ex.Message)
            });
        }
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
        long importedContentBytes = 0;
        foreach (var changedFile in proposal!.ChangedFiles)
        {
            if (changedFile.ChangeKind == WorkerFileChangeKind.Delete)
                continue;

            byte[]? content;
            try
            {
                content = await workspace.ReadFileBytesAsync(
                    changedFile.ContentRef!,
                    cancellationToken);
            }
            catch (InvalidDataException ex)
            {
                await RecordTerminalEventAsync("proposal-rejected", profile, task, ex.Message, []);
                return (null, new GmWorkerTaskRunResult
                {
                    Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, ex.Message)
                });
            }
            if (content == null)
            {
                var message = $"Worker proposal contentRef is missing from detached execution output: {changedFile.ContentRef}.";
                await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [changedFile.ContentRef!]);
                return (null, new GmWorkerTaskRunResult
                {
                    Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message)
                });
            }

            importedContentBytes += content.LongLength;
            if (importedContentBytes > MaxImportedContentBytes)
            {
                var message =
                    $"Worker proposal contentRef bundle exceeds the {MaxImportedContentBytes}-byte aggregate import limit.";
                await RecordTerminalEventAsync("proposal-rejected", profile, task, message, []);
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
        {
            await _hooks.BeforeProposalPublicationAsync()
                .WaitAsync(cancellationToken);
        }
        var publication = await _proposalStore.PublishBundleAsync(
            proposal!,
            proposalBytes!,
            importedContent,
            taskPath,
            expectedTaskBytes,
            task.SessionGeneration,
            proposalInboxPath,
            _auditLog == null
                ? null
                : lease => _auditLog.RecordProposalReceivedAsync(lease, proposal!),
            cancellationToken);
        if (!publication.Published)
        {
            var message = publication.Error ?? "Worker proposal bundle publication was rejected.";
            if (!publication.SessionReplaced)
                await RecordTerminalEventAsync("proposal-rejected", profile, task, message, [proposal.ProposalId]);
            return (null, new GmWorkerTaskRunResult
            {
                Status = CreateStatus(profile, WorkerBridgeState.Failed, ready: false, task.TaskId, message),
                BoundTask = task,
                SessionReplaced = publication.SessionReplaced
            });
        }

        return (proposal, new GmWorkerTaskRunResult());
    }

    private async Task<WorkerTaskReservation> TryReserveTaskAsync(
        WorkerTaskPacket task,
        string taskPath,
        string proposalInboxPath)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (_fs.FileExists(taskPath) || _fs.FileExists(proposalInboxPath))
            return WorkerTaskReservation.Reject(
                $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.");

        if (!_fs.IsCurrentSessionGeneration(writeLease, task.SessionGeneration))
        {
            return WorkerTaskReservation.SessionWasReplaced(
                "Worker task context does not belong to the current game session generation.");
        }

        var taskBytes = EncodeUtf8WithPreamble(GmWorkerJson.Serialize(task));
        var reservedTask = GmWorkerJson.Deserialize<WorkerTaskPacket>(DecodeUtf8(taskBytes)!);
        if (reservedTask == null)
            throw new InvalidDataException("Serialized worker task reservation could not be read back.");

        var reserved = await _fs.CompareExchangeFileBytesAsync(
                           writeLease,
                           taskPath,
                           expectedContent: null,
                           desiredContent: taskBytes) == CanonicalFileMutationResult.Applied;
        return reserved
            ? new WorkerTaskReservation(true, reservedTask, taskBytes, null, false)
            : WorkerTaskReservation.Reject(
                $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.");
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

        internal void Quarantine()
        {
            // Retain the acquired semaphore and reference count permanently. Releasing either
            // would permit another worker to start while the prior process tree is unconfirmed.
            Interlocked.Exchange(ref _gate, null);
        }

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

    private sealed record WorkerTaskReservation(
        bool Reserved,
        WorkerTaskPacket? Task,
        byte[]? TaskBytes,
        string? Error,
        bool SessionReplaced)
    {
        internal static WorkerTaskReservation Reject(string error) => new(false, null, null, error, false);
        internal static WorkerTaskReservation SessionWasReplaced(string error) => new(false, null, null, error, true);
    }

    private async Task<(bool Attempted, WorkerProposal? Proposal, GmWorkerTaskRunResult Result)> TryReadAndStoreExistingProposalAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string taskPath,
        byte[] expectedTaskBytes,
        string proposalInboxPath,
        GmWorkerExecutionWorkspace workspace,
        CancellationToken cancellationToken)
    {
        if (!workspace.ProposalExists())
            return (false, null, new GmWorkerTaskRunResult());

        var proposalResult = await ReadAndStoreProposalAsync(
            profile,
            task,
            taskPath,
            expectedTaskBytes,
            proposalInboxPath,
            workspace,
            cancellationToken);
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

    private async Task RecordTerminalEventAsync(
        string eventType,
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string summary,
        IReadOnlyList<string> details)
    {
        if (_auditLog == null)
            return;

        _ = await _auditLog.AppendEventIfCurrentSessionAsync(
            task.SessionGeneration,
            new WorkerAuditEvent
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
        });
    }

    internal static async Task StopUnattachedProcessTreeAsync(
        Process process,
        TimeSpan? timeout = null,
        Func<Process, CancellationToken, Task>? waitForExitAsync = null)
    {
        var confirmationTimeout = timeout ?? ProcessTreeTerminationConfirmation.DefaultTimeout;
        if (confirmationTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
        using var cancellation = new CancellationTokenSource(confirmationTimeout);
        try
        {
            var wait = waitForExitAsync ??
                       ((Process target, CancellationToken token) => target.WaitForExitAsync(token));
            await wait(process, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Unattached worker process tree did not confirm termination before the ownership deadline.");
        }
        if (!process.HasExited)
        {
            throw new TimeoutException(
                "Unattached worker process tree did not confirm termination before the ownership deadline.");
        }
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

    private static async Task<string> CaptureProcessOutputAsync(StreamReader reader)
    {
        const int bufferSize = 4096;
        var buffer = new char[bufferSize];
        var captured = new StringBuilder(MaxCapturedProcessOutputCharacters);
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer);
            if (read == 0)
                break;

            var remaining = MaxCapturedProcessOutputCharacters - captured.Length;
            if (remaining > 0)
                captured.Append(buffer, 0, Math.Min(remaining, read));
            if (read > remaining)
                truncated = true;
        }

        if (truncated)
        {
            captured.Append(
                $"{Environment.NewLine}[worker output truncated after {MaxCapturedProcessOutputCharacters} characters]");
        }

        return captured.ToString();
    }
}
