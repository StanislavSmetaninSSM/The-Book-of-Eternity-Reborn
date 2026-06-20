using System.Diagnostics;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerBridgePool
{
    public const string TaskRoot = "worker_tasks";
    public const string ProposalInboxRoot = "worker_proposals/inbox";
    public const string TaskPathEnvironmentVariable = "BOE_WORKER_TASK_PATH";
    public const string ProposalPathEnvironmentVariable = "BOE_WORKER_PROPOSAL_PATH";
    public const string SessionPathEnvironmentVariable = "BOE_WORKER_SESSION_PATH";

    private readonly FileSystemManager _fs;
    private readonly GmWorkerProposalStore _proposalStore;
    private readonly GmWorkerAuditLog? _auditLog;

    public GmWorkerBridgePool(
        FileSystemManager fs,
        GmWorkerProposalStore? proposalStore = null,
        GmWorkerAuditLog? auditLog = null)
    {
        _fs = fs;
        _proposalStore = proposalStore ?? new GmWorkerProposalStore(fs);
        _auditLog = auditLog;
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

        var taskPath = GetTaskPacketPath(task.TaskId);
        var proposalInboxPath = GetProposalInboxPath(task.TaskId);
        await _fs.WriteFileAtomicAsync(taskPath, GmWorkerJson.Serialize(task));
        var proposalInboxDirectory = Path.GetDirectoryName(_fs.ResolvePath(proposalInboxPath));
        if (!string.IsNullOrWhiteSpace(proposalInboxDirectory))
            Directory.CreateDirectory(proposalInboxDirectory);
        if (_auditLog != null)
            await _auditLog.RecordTaskDispatchedAsync(task);

        Process? process = null;
        try
        {
            Track(WorkerBridgeState.Starting, ready: false);
            var startInfo = CreateWorkerStartInfo(profile, _fs.GameSessionPath);
            startInfo.Environment[TaskPathEnvironmentVariable] = _fs.ResolvePath(taskPath);
            startInfo.Environment[ProposalPathEnvironmentVariable] = _fs.ResolvePath(proposalInboxPath);
            startInfo.Environment[SessionPathEnvironmentVariable] = _fs.GameSessionPath;

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

            var processId = process.Id;
            Track(WorkerBridgeState.Busy, ready: false, processId: processId);
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var timeout = TimeSpan.FromSeconds(Math.Max(1, profile.TimeoutSeconds));
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));

            if (completed != waitTask)
            {
                TryKillProcess(process);
                await WaitForProcessExitAfterKillAsync(waitTask);
                var output = await ReadProcessOutputAsync(outputTask);
                var stderr = await ReadProcessOutputAsync(errorTask);
                var message = $"Worker task timed out after {profile.TimeoutSeconds} seconds.";
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

            var standardOutput = await ReadProcessOutputAsync(outputTask);
            var standardError = await ReadProcessOutputAsync(errorTask);
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                var message = $"Worker process exited with code {exitCode}.";
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

            var proposalResult = await ReadAndStoreProposalAsync(profile, task, proposalInboxPath);
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
            process?.Dispose();
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
        string proposalInboxPath)
    {
        var proposalJson = await _fs.ReadFileAsync(proposalInboxPath);
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

        await _proposalStore.SaveProposalAsync(proposal!);
        if (_auditLog != null)
            await _auditLog.RecordProposalReceivedAsync(proposal!);

        return (proposal, new GmWorkerTaskRunResult());
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
            EventId = "worker_audit_" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"),
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

    private static async Task WaitForProcessExitAfterKillAsync(Task waitTask)
    {
        try
        {
            await waitTask;
        }
        catch
        {
            // Timeout handling returns a timed-out result.
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
}
