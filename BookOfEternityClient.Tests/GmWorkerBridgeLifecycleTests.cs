using BookOfEternityClient.Services.GmWorkers;
using Xunit;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeLifecycleTests
{
    [Fact]
    public void BuildInitialStatuses_ReportsDisabledAndStoppedWorkersWithoutLaunchingVisibleWindows()
    {
        var enabled = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var disabled = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile() with { Enabled = false };

        var statuses = GmWorkerBridgePool.BuildInitialStatuses([enabled, disabled]);

        var enabledStatus = Assert.Single(statuses, status => status.WorkerId == enabled.WorkerId);
        var disabledStatus = Assert.Single(statuses, status => status.WorkerId == disabled.WorkerId);
        Assert.Equal(WorkerBridgeState.Stopped, enabledStatus.State);
        Assert.False(enabledStatus.Ready);
        Assert.Equal(WorkerBridgeState.Disabled, disabledStatus.State);
        Assert.False(disabledStatus.Ready);
    }

    [Fact]
    public async Task RunTaskAsync_WhenWorkerWritesValidProposal_RecordsLifecycleAndStoresProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-success.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_lifecycle_success'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Fake worker returned a valid proposal.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'smoke'
                        message = 'Task path and proposal path were provided.'
                    })
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('fake worker completed')
                    }
                    createdAtUtc = '2026-06-20T00:10:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                Write-Output 'fake-worker-ready'
                """);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync("worker_proposal_lifecycle_success");
            var taskJson = await fs.ReadFileAsync(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.True(
                result.Status.State == WorkerBridgeState.Stopped,
                $"{result.Status.LastError}{Environment.NewLine}STDOUT: {result.StandardOutput}{Environment.NewLine}STDERR: {result.StandardError}");
            Assert.False(result.Status.Ready);
            Assert.Equal(task.TaskId, result.Status.CurrentTaskId);
            Assert.Contains("fake-worker-ready", result.StandardOutput);
            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_lifecycle_success", result.Proposal!.ProposalId);
            Assert.Equal(
                [
                    WorkerBridgeState.Starting,
                    WorkerBridgeState.Busy,
                    WorkerBridgeState.Stopped
                ],
                result.StatusHistory.Select(status => status.State).ToArray());
            Assert.NotNull(stored);
            Assert.Contains(task.TaskId, taskJson);
            Assert.Collection(
                auditEvents,
                first => Assert.Equal("task-dispatched", first.EventType),
                second => Assert.Equal("proposal-received", second.EventType));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenWorkerExitsNonZero_ReportsFailedStatusAndAudit()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-failure.ps1");
            await File.WriteAllTextAsync(scriptPath, "Write-Error 'fake worker failed'; exit 7");
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.Equal(7, result.ExitCode);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("fake worker failed", result.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                [
                    WorkerBridgeState.Starting,
                    WorkerBridgeState.Busy,
                    WorkerBridgeState.Failed
                ],
                result.StatusHistory.Select(status => status.State).ToArray());
            Assert.Collection(
                auditEvents,
                first => Assert.Equal("task-dispatched", first.EventType),
                second => Assert.Equal("task-failed", second.EventType));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenWorkerExitsBeforeProtocolAcceptance_NeverReportsReady()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-exits-before-protocol.ps1");
            await File.WriteAllTextAsync(scriptPath, "Write-Error 'worker cli exited before accepting task'; exit 13");
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);

            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.False(result.Status.Ready);
            Assert.DoesNotContain(result.StatusHistory, status => status.Ready);
            Assert.DoesNotContain(result.StatusHistory, status => status.State == WorkerBridgeState.Ready);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenWorkerTimesOut_KillsProcessAndReportsTimedOutStatus()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-timeout.ps1");
            await File.WriteAllTextAsync(scriptPath, "Start-Sleep -Seconds 30");
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 1
            };
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.False(result.Status.Ready);
            Assert.Equal(task.TaskId, result.Status.CurrentTaskId);
            Assert.Null(result.Proposal);
            Assert.Equal(
                [
                    WorkerBridgeState.Starting,
                    WorkerBridgeState.Busy,
                    WorkerBridgeState.TimedOut
                ],
                result.StatusHistory.Select(status => status.State).ToArray());
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Collection(
                auditEvents,
                first => Assert.Equal("task-dispatched", first.EventType),
                second => Assert.Equal("task-timed-out", second.EventType));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
