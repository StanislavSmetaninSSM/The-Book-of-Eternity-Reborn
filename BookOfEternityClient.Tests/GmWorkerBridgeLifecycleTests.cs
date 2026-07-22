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
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
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
    public async Task RunTaskAsync_DirectWorkerCanonicalWrite_CannotMutateLiveSession()
    {
        var root = CreateTempRoot();
        try
        {
            const string canonicalPath = "game_state/world/weather.json";
            const string original = "{\"weather\":\"live-original\"}";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(canonicalPath, original);
            var scriptPath = Path.Combine(root, "fake-worker-direct-canonical-write.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $canonicalPath = Join-Path $env:BOE_WORKER_SESSION_PATH 'game_state\world\weather.json'
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $canonicalPath) | Out-Null
                '{"weather":"worker-direct-mutation"}' | Set-Content -Path $canonicalPath -Encoding UTF8
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_isolated_direct_write'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'The proposal is valid even though the worker attempted a direct canonical write.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'isolated worker result'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('direct write attempted only in detached worker session')
                    }
                    createdAtUtc = '2026-07-23T00:00:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                Write-Output "worker-session=$env:BOE_WORKER_SESSION_PATH"
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.AnalysisTask() with
            {
                TimeoutSeconds = profile.TimeoutSeconds,
                ContextFiles =
                [
                    new WorkerFileReference
                    {
                        Path = canonicalPath,
                        Sha256 = ComputeFileSha256(fs.ResolvePath(canonicalPath))
                    }
                ]
            };
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);

            Assert.True(
                result.Proposal != null,
                $"{result.Status.LastError}{Environment.NewLine}STDOUT: {result.StandardOutput}{Environment.NewLine}STDERR: {result.StandardError}");
            Assert.Equal(original, await fs.ReadFileAsync(canonicalPath));
            Assert.DoesNotContain($"worker-session={fs.GameSessionPath}", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            var runtimeRoot = Path.Combine(root, ".worker_runtime");
            Assert.False(Directory.Exists(runtimeRoot) && Directory.EnumerateFileSystemEntries(runtimeRoot).Any());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_DeclaredContentRef_IsImportedWithoutApplyingCanonicalChange()
    {
        var root = CreateTempRoot();
        try
        {
            const string canonicalPath = "game_state/world/weather.json";
            const string replacement = "{\"weather\":\"detached-proposal\"}";
            const string proposalId = "worker_proposal_detached_content_ref";
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-detached-content-ref.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $content = '{{replacement}}'
                $contentRef = 'worker_proposals/{{proposalId}}/{{canonicalPath}}'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $contentPath) | Out-Null
                [System.IO.File]::WriteAllText($contentPath, $content, [System.Text.UTF8Encoding]::new($false))
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    $hashBytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($content))
                    $afterSha256 = [System.BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
                }
                finally {
                    $sha.Dispose()
                }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Detached worker returned one declared repair artifact.'
                    changedFiles = @([ordered]@{
                        path = '{{canonicalPath}}'
                        changeKind = 'replace'
                        beforeSha256 = $task.contextFiles[0].sha256
                        afterSha256 = $afterSha256
                        contentRef = $contentRef
                    })
                    findings = @()
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('declared content ref written in detached workspace')
                    }
                    createdAtUtc = '2026-07-23T00:05:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.ValidationRepairTask() with
                {
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var original = await fs.ReadFileAsync(canonicalPath);
            var contentRef = $"worker_proposals/{proposalId}/{canonicalPath}";
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);

            Assert.True(
                result.Proposal != null,
                $"{result.Status.LastError}{Environment.NewLine}STDOUT: {result.StandardOutput}{Environment.NewLine}STDERR: {result.StandardError}");
            Assert.Equal(replacement, await fs.ReadFileAsync(contentRef));
            Assert.Equal(original, await fs.ReadFileAsync(canonicalPath));
            Assert.NotNull(await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId));
            var runtimeRoot = Path.Combine(root, GmWorkerBridgePool.WorkerRuntimeRoot);
            Assert.False(Directory.Exists(runtimeRoot) && Directory.EnumerateFileSystemEntries(runtimeRoot).Any());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenWorkerOmitsProposalStatus_RejectsWithoutStoringProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-missing-status.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_missing_status'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    summary = 'This proposal deliberately omits status.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'Diagnostic draft that must not be stored.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('status omitted')
                    }
                    createdAtUtc = '2026-06-20T00:15:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
            var store = new GmWorkerProposalStore(fs);
            var pool = new GmWorkerBridgePool(fs, store, new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var storedJson = await fs.ReadFileAsync(
                GmWorkerProposalStore.GetProposalPath("worker_proposal_missing_status"));
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Null(storedJson);
            Assert.Contains("status", result.Status.LastError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(auditEvents, audit => audit.EventType == "proposal-rejected");
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
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
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
    public async Task RunTaskAsync_WhenWorkerExitsNonZeroAfterWritingValidProposal_PreservesProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-proposal-then-nonzero.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_nonzero_after_write'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Proposal was written before the worker CLI failed.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'proposal-before-exit'
                        message = 'The proposal file is valid even though the process exits nonzero.'
                    })
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('valid proposal was written first')
                    }
                    createdAtUtc = '2026-06-20T00:20:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                Write-Error 'worker cli failed after writing proposal'
                exit 3
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync("worker_proposal_nonzero_after_write");
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.Equal(3, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.Equal(WorkerBridgeState.Stopped, result.Status.State);
            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_nonzero_after_write", result.Proposal!.ProposalId);
            Assert.NotNull(stored);
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
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.ValidationRepairTask() with { TimeoutSeconds = profile.TimeoutSeconds });
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
    public async Task RunTaskAsync_WhenWorkerTimesOutAfterWritingValidProposal_PreservesProposal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-proposal-then-timeout.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_timeout_after_write'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Proposal was written before the worker process timed out.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'proposal-before-timeout'
                        message = 'The proposal file is valid even though the process keeps running.'
                    })
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('valid proposal was written first')
                    }
                    createdAtUtc = '2026-06-20T00:25:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                Start-Sleep -Seconds 30
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 3
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync("worker_proposal_timeout_after_write");
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.Stopped, result.Status.State);
            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_timeout_after_write", result.Proposal!.ProposalId);
            Assert.NotNull(stored);
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
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.ValidationRepairTask() with { TimeoutSeconds = profile.TimeoutSeconds });
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

    private static string ComputeFileSha256(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();

    private static async Task<WorkerTaskPacket> MaterializeTaskContextAsync(
        FileSystemManager fs,
        WorkerTaskPacket task)
    {
        var contextFiles = new List<WorkerFileReference>();
        foreach (var contextFile in task.ContextFiles)
        {
            if (string.Equals(contextFile.Sha256, "missing", StringComparison.OrdinalIgnoreCase))
            {
                contextFiles.Add(contextFile);
                continue;
            }

            await fs.WriteFileAtomicAsync(
                contextFile.Path,
                $"{{\"fixturePath\":\"{contextFile.Path.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}");
            contextFiles.Add(contextFile with
            {
                Sha256 = ComputeFileSha256(fs.ResolvePath(contextFile.Path))
            });
        }

        return task with { ContextFiles = contextFiles };
    }
}
