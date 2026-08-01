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
    internal Func<Task>? BeforeTerminalFailureDecisionAsync { get; init; }
    internal Func<Task>? BeforeProposalPublicationAsync { get; init; }
    internal Func<Task>? BeforeProcessTreeAttachAsync { get; init; }
    internal Func<Task>? BeforeWorkerReleaseAsync { get; init; }
    internal Func<string, Task>? BeforeWorkspaceCleanupAsync { get; init; }
    internal Func<string, Task>? AfterQuarantineAuditTempCreatedAsync { get; init; }
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

internal enum GmWorkerPrePublicationTerminalOutcome
{
    Failed,
    Canceled,
    TimedOut
}

internal sealed class GmWorkerPrePublicationTerminalAuthority : IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenRegistration _timeoutRegistration;
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private TerminalState _state;
    private GmWorkerPrePublicationTerminalOutcome _completedOutcome;

    internal GmWorkerPrePublicationTerminalAuthority(
        CancellationToken cancellationToken,
        CancellationToken timeoutToken)
    {
        _timeoutRegistration = timeoutToken.Register(
            static state => ((GmWorkerPrePublicationTerminalAuthority)state!).ObserveTimeout(),
            this);
        _cancellationRegistration = cancellationToken.Register(
            static state => ((GmWorkerPrePublicationTerminalAuthority)state!).ObserveCancellation(),
            this);
    }

    internal GmWorkerPrePublicationTerminalOutcome CompleteFailure()
    {
        lock (_sync)
        {
            if (_state == TerminalState.Completed)
                return _completedOutcome;

            _completedOutcome = _state switch
            {
                TerminalState.Canceled => GmWorkerPrePublicationTerminalOutcome.Canceled,
                TerminalState.TimedOut => GmWorkerPrePublicationTerminalOutcome.TimedOut,
                _ => GmWorkerPrePublicationTerminalOutcome.Failed
            };
            _state = TerminalState.Completed;
            return _completedOutcome;
        }
    }

    public void Dispose()
    {
        _cancellationRegistration.Dispose();
        _timeoutRegistration.Dispose();
    }

    private void ObserveCancellation()
    {
        lock (_sync)
        {
            if (_state is TerminalState.Pending or TerminalState.TimedOut)
                _state = TerminalState.Canceled;
        }
    }

    private void ObserveTimeout()
    {
        lock (_sync)
        {
            if (_state == TerminalState.Pending)
                _state = TerminalState.TimedOut;
        }
    }

    private enum TerminalState
    {
        Pending,
        Canceled,
        TimedOut,
        Completed
    }
}

internal sealed class GmWorkerProposalHandoffException : Exception
{
    internal GmWorkerProposalHandoffException(
        string eventType,
        string message,
        IReadOnlyList<string> details,
        bool sessionReplaced = false)
        : base(message)
    {
        EventType = eventType;
        Details = details;
        SessionReplaced = sessionReplaced;
    }

    internal string EventType { get; }
    internal IReadOnlyList<string> Details { get; }
    internal bool SessionReplaced { get; }
}

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
    private readonly GmWorkerQuarantineReaper _quarantineReaper;
    private static readonly ConcurrentDictionary<string, WorkerConcurrencyGate> WorkerConcurrencyGates =
        new(StringComparer.OrdinalIgnoreCase);

    public GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore = null,
        GmWorkerAuditLog? auditLog = null)
        : this(
            fs,
            proposalStore,
            auditLog,
            hooks: null,
            GmWorkerProcessTreeFactory.Instance,
            GmWorkerQuarantineReaper.Shared)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks)
        : this(
            fs,
            proposalStore,
            auditLog,
            hooks,
            GmWorkerProcessTreeFactory.Instance,
            GmWorkerQuarantineReaper.Shared)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks,
        IGmWorkerProcessTreeFactory processTreeFactory)
        : this(
            fs,
            proposalStore,
            auditLog,
            hooks,
            processTreeFactory,
            GmWorkerQuarantineReaper.Shared)
    {
    }

    internal GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore,
        GmWorkerAuditLog? auditLog,
        GmWorkerBridgePoolHooks? hooks,
        IGmWorkerProcessTreeFactory processTreeFactory,
        GmWorkerQuarantineReaper quarantineReaper)
    {
        _fs = fs;
        _proposalStore = proposalStore ?? new GmWorkerProposalStore(fs);
        _auditLog = auditLog;
        _hooks = hooks;
        _processTreeFactory = processTreeFactory;
        _quarantineReaper = quarantineReaper;
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
        var taskSnapshot = CaptureTaskSnapshot(task);
        task = taskSnapshot.Task;
        var taskBytes = taskSnapshot.Bytes;
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
        using var quarantineReservation =
            _quarantineReaper.TryReserve();
        if (quarantineReservation == null)
        {
            var status = Track(
                WorkerBridgeState.Failed,
                ready: false,
                "Worker quarantine capacity is exhausted; no additional worker can start safely.");
            return new GmWorkerTaskRunResult
            {
                Status = status,
                StatusHistory = statusHistory.ToArray()
            };
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(1, profile.TimeoutSeconds));
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _hooks?.TimeoutSignal ?? CancellationToken.None);
        timeoutCancellation.CancelAfter(timeout);
        using var lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        using var terminalAuthority = new GmWorkerPrePublicationTerminalAuthority(
            cancellationToken,
            timeoutCancellation.Token);

        async Task<GmWorkerTaskRunResult> CompleteEarlyFailureAsync(
            string message,
            bool sessionReplaced = false,
            string? eventType = null,
            IReadOnlyList<string>? details = null)
        {
            if (!string.IsNullOrWhiteSpace(eventType))
            {
                await RecordTerminalEventAsync(
                    eventType,
                    profile,
                    task,
                    message,
                    details ?? [],
                    lifecycleCancellation.Token);
            }

            if (_hooks?.BeforeTerminalFailureDecisionAsync != null)
                await _hooks.BeforeTerminalFailureDecisionAsync();
            var outcome = terminalAuthority.CompleteFailure();
            if (outcome == GmWorkerPrePublicationTerminalOutcome.Canceled)
            {
                throw new OperationCanceledException(
                    "Worker task was canceled before proposal publication.",
                    innerException: null,
                    cancellationToken);
            }

            if (outcome == GmWorkerPrePublicationTerminalOutcome.TimedOut)
            {
                var timeoutMessage = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                await RecordTerminalEventAsync(
                    "task-timed-out",
                    profile,
                    task,
                    timeoutMessage,
                    []);
                var timeoutStatus = Track(
                    WorkerBridgeState.TimedOut,
                    ready: false,
                    timeoutMessage);
                return new GmWorkerTaskRunResult
                {
                    Status = timeoutStatus,
                    StatusHistory = statusHistory.ToArray(),
                    BoundTask = task,
                    TimedOut = true
                };
            }

            var failedStatus = Track(WorkerBridgeState.Failed, ready: false, message);
            return new GmWorkerTaskRunResult
            {
                Status = failedStatus,
                StatusHistory = statusHistory.ToArray(),
                BoundTask = task,
                SessionReplaced = sessionReplaced
            };
        }

        var taskPath = GetTaskPacketPath(task.TaskId);
        var proposalInboxPath = GetProposalInboxPath(task.TaskId);
        WorkerTaskReservation reservation;
        try
        {
            if (_hooks?.BeforeTaskReservationAsync != null)
            {
                await _hooks.BeforeTaskReservationAsync()
                    .WaitAsync(lifecycleCancellation.Token);
            }
            reservation = await TryReserveTaskAsync(
                task,
                taskBytes,
                taskPath,
                proposalInboxPath,
                lifecycleCancellation.Token);
        }
        catch (OperationCanceledException ex)
        {
            var result = await CompleteEarlyFailureAsync(ex.Message);
            return result;
        }
        catch (Exception ex)
        {
            return await CompleteEarlyFailureAsync(
                ex.Message,
                eventType: "task-failed",
                details: [ex.GetType().Name]);
        }
        if (!reservation.Reserved)
        {
            var message = reservation.Error ??
                          $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.";
            return await CompleteEarlyFailureAsync(
                message,
                reservation.SessionReplaced);
        }

        task = reservation.Task!;
        taskBytes = reservation.TaskBytes!;

        try
        {
            if (_hooks?.BeforeTaskDispatchAuditAsync != null)
            {
                await _hooks.BeforeTaskDispatchAuditAsync()
                    .WaitAsync(lifecycleCancellation.Token);
            }
            if (_auditLog != null &&
                !await _auditLog.RecordTaskDispatchedIfCurrentSessionAsync(
                    task,
                    lifecycleCancellation.Token))
            {
                const string message =
                    "Worker task context does not belong to the current game session generation.";
                return await CompleteEarlyFailureAsync(
                    message,
                    sessionReplaced: true);
            }
        }
        catch (OperationCanceledException ex)
        {
            return await CompleteEarlyFailureAsync(ex.Message);
        }
        catch (Exception ex)
        {
            return await CompleteEarlyFailureAsync(
                ex.Message,
                eventType: "task-failed",
                details: [ex.GetType().Name]);
        }

        Process? process = null;
        IGmWorkerProcessTree? processTree = null;
        GmWorkerProcessHostLaunch? processHostLaunch = null;
        var processStarted = false;
        GmWorkerExecutionWorkspace? workspace = null;
        using var completionWaitCancellation = new CancellationTokenSource();
        Task<int>? workerCompletionTask = null;
        Task<string>? outputCaptureTask = null;
        Task<string>? errorCaptureTask = null;
        int? launchedProcessId = null;
        int? completedExitCode = null;
        string completedStandardOutput = "";
        string completedStandardError = "";
        var executionCleanupCompleted = false;
        async Task CleanupExecutionAsync()
        {
            if (executionCleanupCompleted)
                return;
            executionCleanupCompleted = true;

            completionWaitCancellation.Cancel();
            var deathConfirmed = !processStarted;
            Exception? processAuthorityFailure = null;
            if (processStarted)
            {
                try
                {
                    if (processTree != null)
                        await processTree.StopAndWaitAsync();
                    else if (process != null)
                        await StopUnattachedProcessTreeAsync(process);
                    deathConfirmed = true;
                }
                catch (Exception ex)
                {
                    processAuthorityFailure = ex;
                }
            }

            if (processAuthorityFailure == null &&
                processTree != null)
            {
                try
                {
                    await processTree.DisposeAsync();
                    processTree = null;
                }
                catch (Exception ex)
                {
                    processAuthorityFailure = ex;
                }
            }

            if (processAuthorityFailure != null)
            {
                ObserveFault(workerCompletionTask);
                ObserveFault(outputCaptureTask);
                ObserveFault(errorCaptureTask);

                if (workspace != null)
                {
                    await RecordTerminalEventAsync(
                        "workspace-cleanup-deferred",
                        profile,
                        task,
                        "Detached worker workspace was retained because process-tree authority cleanup could not be confirmed.",
                        [workspace.GameSessionPath]);
                }

                await RecordTerminalEventAsync(
                    "process-tree-cleanup-unconfirmed",
                    profile,
                    task,
                    processAuthorityFailure.Message,
                    [processAuthorityFailure.GetType().Name]);

                var retainedSlot = workerSlot.TransferOwnership();
                var retainedWorkspacePath =
                    workspace?.GameSessionPath;
                var cleanupConfirmedAuditEvent = CreateTerminalEvent(
                    "process-tree-cleanup-confirmed",
                    profile,
                    task,
                    "A quarantined worker process tree was later confirmed stopped and its retained workspace was cleaned.",
                    retainedWorkspacePath == null
                        ? []
                        : [retainedWorkspacePath]);
                var quarantineOwner = new GmWorkerQuarantinedExecution(
                    $"{profile.WorkerId}/{task.TaskId}/{launchedProcessId?.ToString() ?? "unattached"}",
                    deathConfirmed,
                    processTree,
                    process,
                    processHostLaunch,
                    workspace,
                    retainedSlot,
                    workerCompletionTask,
                    outputCaptureTask,
                    errorCaptureTask,
                    _hooks?.BeforeWorkspaceCleanupAsync,
                    task.SessionGeneration,
                    cleanupConfirmedAuditEvent,
                    () => RecordRequiredTerminalEventOnceAsync(
                        task.SessionGeneration,
                        cleanupConfirmedAuditEvent),
                    failure => RecordTerminalEventAsync(
                        "process-tree-cleanup-retry-failed",
                        profile,
                        task,
                        failure.Message,
                        [failure.GetType().Name]));

                processTree = null;
                process = null;
                processHostLaunch = null;
                workspace = null;
                processStarted = false;
                workerCompletionTask = null;
                outputCaptureTask = null;
                errorCaptureTask = null;
                quarantineReservation.Transfer(quarantineOwner);
                return;
            }

            Exception? processCleanupFailure = null;
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

            if (processCleanupFailure == null)
            {
                if (outputCaptureTask != null)
                    completedStandardOutput = await ReadProcessOutputAsync(outputCaptureTask);
                if (errorCaptureTask != null)
                    completedStandardError = await ReadProcessOutputAsync(errorCaptureTask);
            }
            else
            {
                ObserveFault(outputCaptureTask);
                ObserveFault(errorCaptureTask);
            }

            try
            {
                process?.Dispose();
            }
            catch (Exception ex)
            {
                processCleanupFailure ??= ex;
            }
            if (processHostLaunch != null)
            {
                try
                {
                    await processHostLaunch.DisposeAsync();
                }
                catch (Exception ex)
                {
                    processCleanupFailure ??= ex;
                }
            }
            if (workspace != null)
            {
                try
                {
                    if (_hooks?.BeforeWorkspaceCleanupAsync != null)
                        await _hooks.BeforeWorkspaceCleanupAsync(workspace.GameSessionPath);
                    await workspace.DisposeAsync();
                }
                catch (Exception cleanupException)
                {
                    await RecordTerminalEventAsync(
                        "workspace-cleanup-failed",
                        profile,
                        task,
                        cleanupException.Message,
                        [cleanupException.GetType().Name]);
                }
            }

            if (processCleanupFailure != null)
            {
                await RecordTerminalEventAsync(
                    "process-tree-cleanup-failed",
                    profile,
                    task,
                    processCleanupFailure.Message,
                    [processCleanupFailure.GetType().Name]);
            }
        }

        async Task<GmWorkerPrePublicationTerminalOutcome> CompleteFailureAsync(
            string? eventType,
            string message,
            IReadOnlyList<string> details)
        {
            await CleanupExecutionAsync();
            if (!string.IsNullOrWhiteSpace(eventType))
            {
                await RecordTerminalEventAsync(
                    eventType,
                    profile,
                    task,
                    message,
                    details,
                    lifecycleCancellation.Token);
            }

            if (_hooks?.BeforeTerminalFailureDecisionAsync != null)
                await _hooks.BeforeTerminalFailureDecisionAsync();
            var outcome = terminalAuthority.CompleteFailure();
            if (outcome == GmWorkerPrePublicationTerminalOutcome.TimedOut)
            {
                var timeoutMessage = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                await RecordTerminalEventAsync(
                    "task-timed-out",
                    profile,
                    task,
                    timeoutMessage,
                    completedStandardError.Length == 0 ? [] : [completedStandardError]);
            }

            return outcome;
        }

        GmWorkerTaskRunResult BuildTerminalResult(
            GmWorkerPrePublicationTerminalOutcome outcome,
            string failureMessage,
            bool sessionReplaced = false)
        {
            if (outcome == GmWorkerPrePublicationTerminalOutcome.Canceled)
            {
                throw new OperationCanceledException(
                    "Worker task was canceled before proposal publication.",
                    innerException: null,
                    cancellationToken);
            }

            if (outcome == GmWorkerPrePublicationTerminalOutcome.TimedOut)
            {
                var timeoutMessage = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
                var timeoutStatus = Track(
                    WorkerBridgeState.TimedOut,
                    ready: false,
                    timeoutMessage,
                    launchedProcessId);
                return new GmWorkerTaskRunResult
                {
                    Status = timeoutStatus,
                    StatusHistory = statusHistory.ToArray(),
                    BoundTask = task,
                    ExitCode = completedExitCode,
                    StandardOutput = completedStandardOutput,
                    StandardError = completedStandardError,
                    TimedOut = true
                };
            }

            var failedStatus = Track(
                WorkerBridgeState.Failed,
                ready: false,
                failureMessage,
                launchedProcessId);
            return new GmWorkerTaskRunResult
            {
                Status = failedStatus,
                StatusHistory = statusHistory.ToArray(),
                BoundTask = task,
                ExitCode = completedExitCode,
                StandardOutput = completedStandardOutput,
                StandardError = completedStandardError,
                SessionReplaced = sessionReplaced
            };
        }

        try
        {
            var workspaceHooks =
                _hooks?.AfterQuarantineAuditTempCreatedAsync == null
                    ? null
                    : new GmWorkerExecutionWorkspaceHooks
                    {
                        AfterQuarantineAuditTempCreatedAsync =
                            _hooks.AfterQuarantineAuditTempCreatedAsync
                    };
            workspace = await GmWorkerExecutionWorkspace.CreateAsync(
                _fs,
                task,
                lifecycleCancellation.Token,
                workspaceHooks);
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
                throw new GmWorkerProposalHandoffException(
                    "task-failed",
                    "Worker process did not start.",
                    []);
            }
            processStarted = true;
            launchedProcessId = process.Id;
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
                throw;
            }

            var processId = process.Id;
            Track(WorkerBridgeState.Busy, ready: false, processId: processId);
            outputCaptureTask = CaptureProcessOutputAsync(process.StandardOutput);
            errorCaptureTask = CaptureProcessOutputAsync(process.StandardError);
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
                throw new OperationCanceledException(cancellationToken);
            }

            if (completionOutcome.Kind == GmWorkerProcessCompletionOutcomeKind.TimedOut)
            {
                throw new OperationCanceledException(timeoutCancellation.Token);
            }
            if (completionOutcome.Kind == GmWorkerProcessCompletionOutcomeKind.HostExited)
            {
                await waitTask;
                throw new InvalidOperationException(
                    $"Worker process host exited before worker completion with code {process.ExitCode}.");
            }

            completedExitCode = completionOutcome.ExitCode!.Value;
            await processTree.StopAndWaitAsync();
            await waitTask;

            completedStandardOutput = await ReadProcessOutputAsync(outputCaptureTask);
            completedStandardError = await ReadProcessOutputAsync(errorCaptureTask);
            if (completedExitCode != 0)
            {
                var message = $"Worker process exited with code {completedExitCode}.";
                throw new GmWorkerProposalHandoffException(
                    "task-failed",
                    message,
                    completedStandardError.Length == 0 ? [] : [completedStandardError]);
            }

            var proposal = await ReadAndStoreProposalAsync(
                profile,
                task,
                taskPath,
                taskBytes,
                proposalInboxPath,
                workspace,
                lifecycleCancellation.Token);

            var stoppedStatus = Track(WorkerBridgeState.Stopped, ready: false, processId: processId);
            return new GmWorkerTaskRunResult
            {
                Status = stoppedStatus,
                StatusHistory = statusHistory.ToArray(),
                Proposal = proposal,
                BoundTask = task,
                ExitCode = completedExitCode,
                StandardOutput = completedStandardOutput,
                StandardError = completedStandardError
            };
        }
        catch (GmWorkerProposalHandoffException ex)
        {
            var outcome = await CompleteFailureAsync(
                ex.SessionReplaced ? null : ex.EventType,
                ex.Message,
                ex.Details);
            return BuildTerminalResult(outcome, ex.Message, ex.SessionReplaced);
        }
        catch (OperationCanceledException ex)
        {
            var outcome = await CompleteFailureAsync(
                eventType: null,
                ex.Message,
                []);
            if (outcome == GmWorkerPrePublicationTerminalOutcome.Failed)
                throw;
            return BuildTerminalResult(outcome, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var outcome = await CompleteFailureAsync(
                "task-failed",
                ex.Message,
                [ex.GetType().Name]);
            return BuildTerminalResult(outcome, ex.Message);
        }
        finally
        {
            await CleanupExecutionAsync();
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

    private async Task<WorkerProposal> ReadAndStoreProposalAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string taskPath,
        byte[] expectedTaskBytes,
        string proposalInboxPath,
        GmWorkerExecutionWorkspace workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? proposalBytes;
        try
        {
            proposalBytes = await workspace.ReadProposalBytesAsync(cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new GmWorkerProposalHandoffException(
                "proposal-rejected",
                ex.Message,
                []);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var proposalJson = DecodeUtf8(proposalBytes);
        if (string.IsNullOrWhiteSpace(proposalJson))
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string message = "Worker completed without writing a proposal.";
            throw new GmWorkerProposalHandoffException(
                "task-failed",
                message,
                []);
        }

        WorkerProposal? proposal;
        try
        {
            proposal = GmWorkerJson.Deserialize<WorkerProposal>(proposalJson);
        }
        catch (Exception ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = $"Worker proposal JSON is malformed: {ex.Message}";
            throw new GmWorkerProposalHandoffException(
                "proposal-rejected",
                message,
                [ex.GetType().Name]);
        }

        var proposalValidation = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);
        cancellationToken.ThrowIfCancellationRequested();
        if (!proposalValidation.IsValid)
        {
            var message = string.Join(Environment.NewLine, proposalValidation.Errors);
            throw new GmWorkerProposalHandoffException(
                "proposal-rejected",
                message,
                proposalValidation.Errors);
        }

        var importedContent = new Dictionary<string, byte[]>(GmWorkerContractValidator.CanonicalPathComparer);
        long importedContentBytes = 0;
        foreach (var changedFile in proposal!.ChangedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                throw new GmWorkerProposalHandoffException(
                    "proposal-rejected",
                    ex.Message,
                    []);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (content == null)
            {
                var message = $"Worker proposal contentRef is missing from detached execution output: {changedFile.ContentRef}.";
                throw new GmWorkerProposalHandoffException(
                    "proposal-rejected",
                    message,
                    [changedFile.ContentRef!]);
            }

            importedContentBytes += content.LongLength;
            if (importedContentBytes > MaxImportedContentBytes)
            {
                var message =
                    $"Worker proposal contentRef bundle exceeds the {MaxImportedContentBytes}-byte aggregate import limit.";
                throw new GmWorkerProposalHandoffException(
                    "proposal-rejected",
                    message,
                    []);
            }

            var actualAfterSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (!string.Equals(actualAfterSha256, changedFile.AfterSha256, StringComparison.OrdinalIgnoreCase))
            {
                var message =
                    $"Worker proposal contentRef bytes do not match afterSha256: {changedFile.ContentRef}.";
                throw new GmWorkerProposalHandoffException(
                    "proposal-rejected",
                    message,
                    [changedFile.ContentRef!]);
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
            cancellationToken.ThrowIfCancellationRequested();
            var message = publication.Error ?? "Worker proposal bundle publication was rejected.";
            throw new GmWorkerProposalHandoffException(
                "proposal-rejected",
                message,
                [proposal.ProposalId],
                publication.SessionReplaced);
        }

        return proposal;
    }

    private async Task<WorkerTaskReservation> TryReserveTaskAsync(
        WorkerTaskPacket task,
        byte[] taskBytes,
        string taskPath,
        string proposalInboxPath,
        CancellationToken cancellationToken)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_fs.FileExists(taskPath) || _fs.FileExists(proposalInboxPath))
            return WorkerTaskReservation.Reject(
                $"Worker task id already exists and cannot overwrite prior dispatch artifacts: {task.TaskId}.");

        if (!_fs.IsCurrentSessionGeneration(writeLease, task.SessionGeneration))
        {
            return WorkerTaskReservation.SessionWasReplaced(
                "Worker task context does not belong to the current game session generation.");
        }

        var reservedTask = GmWorkerJson.Deserialize<WorkerTaskPacket>(DecodeUtf8(taskBytes)!);
        if (reservedTask == null)
            throw new InvalidDataException("Serialized worker task reservation could not be read back.");

        cancellationToken.ThrowIfCancellationRequested();
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

    private static WorkerTaskSnapshot CaptureTaskSnapshot(WorkerTaskPacket task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var bytes = EncodeUtf8WithPreamble(GmWorkerJson.Serialize(task));
        var snapshot = GmWorkerJson.Deserialize<WorkerTaskPacket>(DecodeUtf8(bytes)!);
        if (snapshot == null)
            throw new InvalidDataException("Serialized worker task snapshot could not be read back.");
        return new WorkerTaskSnapshot(snapshot, bytes);
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

        internal IDisposable TransferOwnership()
        {
            var ownedGate = Interlocked.Exchange(
                ref _gate,
                null)
                ?? throw new InvalidOperationException(
                    "Worker slot ownership was already released or transferred.");
            return new WorkerSlotOwnership(
                key,
                ownedGate);
        }

        public void Dispose()
        {
            var ownedGate = Interlocked.Exchange(ref _gate, null);
            if (ownedGate != null)
                ReleaseWorkerSlotReference(key, ownedGate, releaseSemaphore: true);
        }
    }

    private sealed class WorkerSlotOwnership(
        string key,
        WorkerConcurrencyGate gate) : IDisposable
    {
        private WorkerConcurrencyGate? _gate = gate;

        public void Dispose()
        {
            var ownedGate = Interlocked.Exchange(
                ref _gate,
                null);
            if (ownedGate != null)
            {
                ReleaseWorkerSlotReference(
                    key,
                    ownedGate,
                    releaseSemaphore: true);
            }
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

    private sealed record WorkerTaskSnapshot(WorkerTaskPacket Task, byte[] Bytes);

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
        IReadOnlyList<string> details,
        CancellationToken cancellationToken = default)
    {
        if (_auditLog == null)
            return;

        try
        {
            _ = await _auditLog.AppendEventIfCurrentSessionAsync(
                task.SessionGeneration,
                CreateTerminalEvent(
                    eventType,
                    profile,
                    task,
                    summary,
                    details),
                cancellationToken);
        }
        catch (Exception)
        {
            // Terminal telemetry is subordinate to the already-decided worker outcome.
        }
    }

    private async Task<GmWorkerAuditAppendDisposition>
        RecordRequiredTerminalEventOnceAsync(
            string sessionGeneration,
            WorkerAuditEvent auditEvent)
    {
        if (_auditLog == null)
        {
            return GmWorkerAuditAppendDisposition
                .CanonicalAuditUnavailable;
        }

        return await _auditLog
            .AppendRequiredEventOnceIfCurrentSessionAsync(
                sessionGeneration,
                auditEvent);
    }

    private static WorkerAuditEvent CreateTerminalEvent(
        string eventType,
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string summary,
        IReadOnlyList<string> details) =>
        new()
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
        };

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

    private static void ObserveFault(Task? task)
    {
        if (task == null)
            return;
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
