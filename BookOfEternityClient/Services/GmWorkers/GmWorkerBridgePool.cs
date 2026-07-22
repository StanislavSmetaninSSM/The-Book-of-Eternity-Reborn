using System.Diagnostics;
using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

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
                var existingProposalResult = await TryReadAndStoreExistingProposalAsync(
                    profile,
                    task,
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
                        StandardOutput = output,
                        StandardError = stderr,
                        TimedOut = existingProposalResult.Proposal != null
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

            var standardOutput = await ReadProcessOutputAsync(outputTask);
            var standardError = await ReadProcessOutputAsync(errorTask);
            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                var message = $"Worker process exited with code {exitCode}.";
                var existingProposalResult = await TryReadAndStoreExistingProposalAsync(
                    profile,
                    task,
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
            process?.Dispose();
            if (workspace != null)
                await workspace.DisposeAsync();
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

        var importedContent = new Dictionary<string, byte[]>(StringComparer.Ordinal);
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

            importedContent[changedFile.ContentRef!] = content;
        }

        foreach (var (contentRef, content) in importedContent)
            await _fs.WriteFileAtomicBytesAsync(contentRef, content);
        await _fs.WriteFileAtomicBytesAsync(proposalInboxPath, proposalBytes!);

        await _proposalStore.SaveProposalAsync(proposal!);
        if (_auditLog != null)
            await _auditLog.RecordProposalReceivedAsync(proposal!);

        return (proposal, new GmWorkerTaskRunResult());
    }

    private async Task<(bool Attempted, WorkerProposal? Proposal, GmWorkerTaskRunResult Result)> TryReadAndStoreExistingProposalAsync(
        WorkerBridgeProfile profile,
        WorkerTaskPacket task,
        string proposalInboxPath,
        GmWorkerExecutionWorkspace workspace)
    {
        if (await workspace.ReadProposalBytesAsync() == null)
            return (false, null, new GmWorkerTaskRunResult());

        var proposalResult = await ReadAndStoreProposalAsync(profile, task, proposalInboxPath, workspace);
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
