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
    public async Task RunTaskAsync_WorkerCreatedJunction_IsRemovedWithoutTouchingExternalTargetOrMaskingResult()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        var outsideRoot = CreateTempRoot();
        var outsideFile = Path.Combine(outsideRoot, "outside-readonly.txt");
        try
        {
            await File.WriteAllTextAsync(outsideFile, "outside");
            File.SetAttributes(outsideFile, File.GetAttributes(outsideFile) | FileAttributes.ReadOnly);
            var fs = CreateFileSystem(root);
            var escapedOutsideRoot = outsideRoot.Replace("'", "''", StringComparison.Ordinal);
            var scriptPath = Path.Combine(root, "fake-worker-junction.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $junctionPath = Join-Path $env:BOE_WORKER_SESSION_PATH 'worker-created-junction'
                New-Item -ItemType Junction -Path $junctionPath -Target '{{escapedOutsideRoot}}' | Out-Null
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_junction_cleanup'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Valid proposal with an unrelated worker-created junction.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'junction cleanup regression'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('cleanup must not traverse junctions')
                    }
                    createdAtUtc = '2026-07-23T00:11:00Z'
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
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);

            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_junction_cleanup", result.Proposal!.ProposalId);
            Assert.True(File.Exists(outsideFile));
            Assert.True((File.GetAttributes(outsideFile) & FileAttributes.ReadOnly) != 0);
            var runtimeRoot = Path.Combine(root, GmWorkerBridgePool.WorkerRuntimeRoot);
            Assert.False(Directory.Exists(runtimeRoot) && Directory.EnumerateFileSystemEntries(runtimeRoot).Any());
        }
        finally
        {
            if (File.Exists(outsideFile))
                File.SetAttributes(outsideFile, File.GetAttributes(outsideFile) & ~FileAttributes.ReadOnly);
            CleanupTempRoot(root);
            CleanupTempRoot(outsideRoot);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WorkspaceCleanupFailure_DoesNotReplaceCompletedWorkerResult()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        var childPidPath = Path.Combine(root, "cleanup-locker.pid");
        try
        {
            var fs = CreateFileSystem(root);
            var lockerScriptPath = Path.Combine(root, "hold-worker-file-lock.ps1");
            await File.WriteAllTextAsync(lockerScriptPath, """
                param([Parameter(Mandatory = $true)][string]$LockedPath)
                $stream = [System.IO.File]::Open(
                    $LockedPath,
                    [System.IO.FileMode]::OpenOrCreate,
                    [System.IO.FileAccess]::ReadWrite,
                    [System.IO.FileShare]::None)
                try { Start-Sleep -Seconds 30 }
                finally { $stream.Dispose() }
                """);
            var escapedLockerScript = lockerScriptPath.Replace("'", "''", StringComparison.Ordinal);
            var escapedChildPidPath = childPidPath.Replace("'", "''", StringComparison.Ordinal);
            var workerScriptPath = Path.Combine(root, "fake-worker-cleanup-lock.ps1");
            await File.WriteAllTextAsync(workerScriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $lockedPath = Join-Path $env:BOE_WORKER_SESSION_PATH 'worker-lock.bin'
                $child = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
                    '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                    '-File', '{{escapedLockerScript}}', $lockedPath
                ) -WindowStyle Hidden -PassThru
                [System.IO.File]::WriteAllText('{{escapedChildPidPath}}', $child.Id.ToString())
                Start-Sleep -Seconds 1
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_cleanup_failure'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Valid proposal survives a detached workspace cleanup failure.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'cleanup failure regression'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('cleanup failure must be diagnostic only')
                    }
                    createdAtUtc = '2026-07-23T00:12:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{workerScriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
            var audit = new GmWorkerAuditLog(fs);
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit);

            var result = await pool.RunTaskAsync(profile, task);
            var events = await audit.ReadEventsAsync();

            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_cleanup_failure", result.Proposal!.ProposalId);
            Assert.Contains(events, item => item.EventType == "workspace-cleanup-failed");
        }
        finally
        {
            if (File.Exists(childPidPath) &&
                int.TryParse(await File.ReadAllTextAsync(childPidPath), out var childPid))
            {
                try
                {
                    using var child = System.Diagnostics.Process.GetProcessById(childPid);
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                }
                catch (ArgumentException)
                {
                    // The child already exited.
                }
            }

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
    public async Task RunTaskAsync_ContentRefDigestMismatch_RejectsBeforeImportingAnyArtifact()
    {
        var root = CreateTempRoot();
        try
        {
            const string canonicalPath = "game_state/world/weather.json";
            const string proposalId = "worker_proposal_wrong_content_digest";
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-wrong-content-digest.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $contentRef = 'worker_proposals/{{proposalId}}/{{canonicalPath}}'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $contentPath) | Out-Null
                [System.IO.File]::WriteAllText($contentPath, '{"weather":"tampered"}', [System.Text.UTF8Encoding]::new($false))
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Digest deliberately does not match detached content.'
                    changedFiles = @([ordered]@{
                        path = '{{canonicalPath}}'
                        changeKind = 'replace'
                        beforeSha256 = $task.contextFiles[0].sha256
                        afterSha256 = ('0' * 64)
                        contentRef = $contentRef
                    })
                    findings = @()
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $false
                        notes = @('digest mismatch regression')
                    }
                    createdAtUtc = '2026-07-23T00:06:00Z'
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
            var contentRef = $"worker_proposals/{proposalId}/{canonicalPath}";
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var events = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("afterSha256", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists(contentRef));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
            Assert.Null(await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId));
            Assert.Contains(events, item => item.EventType == "proposal-rejected");
            Assert.DoesNotContain(events, item => item.EventType == "proposal-received");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ExistingProposalId_RejectsWithoutOverwritingPriorReviewArtifact()
    {
        var root = CreateTempRoot();
        try
        {
            const string proposalId = "worker_proposal_collision";
            var fs = CreateFileSystem(root);
            var store = new GmWorkerProposalStore(fs);
            var prior = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
            {
                ProposalId = proposalId,
                Summary = "Prior proposal must remain immutable."
            };
            await store.SaveProposalAsync(prior);
            var scriptPath = Path.Combine(root, "fake-worker-proposal-collision.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Attempted replacement proposal.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'Must not replace the prior artifact.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('proposal id collision regression')
                    }
                    createdAtUtc = '2026-07-23T00:07:00Z'
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
            var pool = new GmWorkerBridgePool(fs, store, new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);
            var preserved = await store.ReadProposalAsync(proposalId);
            var events = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("already exists", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(prior.Summary, preserved!.Summary);
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
            Assert.Contains(events, item => item.EventType == "proposal-rejected");
            Assert.DoesNotContain(events, item => item.EventType == "proposal-received");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ConcurrentPoolsWithSameTaskId_LaunchExactlyOneWorker()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var markerDirectory = Path.Combine(root, "task-race-markers");
            Directory.CreateDirectory(markerDirectory);
            var escapedMarkerDirectory = markerDirectory.Replace("'", "''", StringComparison.Ordinal);
            var scriptPath = Path.Combine(root, "fake-worker-task-id-race.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path (Join-Path '{{escapedMarkerDirectory}}' ($task.taskId + '-' + $PID)) | Out-Null
                $proposalId = 'worker_proposal_task_race_' + $PID
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = $proposalId
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Concurrent task-id reservation regression.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'Only one worker may launch for an immutable task id.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('task-id race')
                    }
                    createdAtUtc = '2026-07-23T02:00:00Z'
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
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_concurrent_reservation",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var firstPool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));
            var secondPool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
            var firstRun = firstPool.RunTaskAsync(profile, task);
            var secondRun = secondPool.RunTaskAsync(profile, task);
            await writeLease.DisposeAsync();
            var results = await Task.WhenAll(firstRun, secondRun);

            Assert.Single(results, result => result.Proposal != null);
            Assert.Single(results, result =>
                result.Status.State == WorkerBridgeState.Failed &&
                result.Status.LastError?.Contains("task id already exists", StringComparison.OrdinalIgnoreCase) == true);
            Assert.Single(Directory.GetFiles(markerDirectory));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_MaxConcurrentTasksOne_DoesNotLaunchSecondWorkerUntilFirstCompletes()
    {
        var root = CreateTempRoot();
        Task<GmWorkerTaskRunResult>? firstRun = null;
        Task<GmWorkerTaskRunResult>? secondRun = null;
        var releasePath = Path.Combine(root, "release-workers");
        try
        {
            var fs = CreateFileSystem(root);
            var markerDirectory = Path.Combine(root, "concurrency-markers");
            Directory.CreateDirectory(markerDirectory);
            var escapedMarkerDirectory = markerDirectory.Replace("'", "''", StringComparison.Ordinal);
            var escapedReleasePath = releasePath.Replace("'", "''", StringComparison.Ordinal);
            var scriptPath = Path.Combine(root, "fake-worker-concurrency-limit.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path (Join-Path '{{escapedMarkerDirectory}}' $task.taskId) | Out-Null
                while (-not (Test-Path -LiteralPath '{{escapedReleasePath}}')) { Start-Sleep -Milliseconds 25 }
                $proposalId = 'worker_proposal_' + $task.taskId
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = $proposalId
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Worker concurrency limit regression.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'Bounded worker result.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('max concurrency')
                    }
                    createdAtUtc = '2026-07-23T02:01:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10,
                MaxConcurrentTasks = 1
            };
            var firstTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_concurrency_first",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var secondTask = firstTask with { TaskId = "worker_task_concurrency_second" };
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            firstRun = pool.RunTaskAsync(profile, firstTask);
            await WaitForFileAsync(Path.Combine(markerDirectory, firstTask.TaskId), TimeSpan.FromSeconds(5));
            secondRun = pool.RunTaskAsync(profile, secondTask);
            await Task.Delay(500);

            Assert.False(fs.FileExists(GmWorkerBridgePool.GetTaskPacketPath(secondTask.TaskId)));
            Assert.False(File.Exists(Path.Combine(markerDirectory, secondTask.TaskId)));
        }
        finally
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(releasePath, "release");
            if (firstRun != null && secondRun != null)
                await Task.WhenAll(firstRun, secondRun);
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ConcurrentWorkersWithSameProposalId_PublishExactlyOneProposal()
    {
        var root = CreateTempRoot();
        try
        {
            const string proposalId = "worker_proposal_concurrent_reservation";
            var fs = CreateFileSystem(root);
            var markerDirectory = Path.Combine(root, "proposal-race-markers");
            Directory.CreateDirectory(markerDirectory);
            var releasePath = Path.Combine(root, "release-proposal-workers");
            var escapedMarkerDirectory = markerDirectory.Replace("'", "''", StringComparison.Ordinal);
            var escapedReleasePath = releasePath.Replace("'", "''", StringComparison.Ordinal);
            var scriptPath = Path.Combine(root, "fake-worker-proposal-id-race.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path (Join-Path '{{escapedMarkerDirectory}}' ('ready-' + $task.taskId)) | Out-Null
                while (-not (Test-Path -LiteralPath '{{escapedReleasePath}}')) { Start-Sleep -Milliseconds 25 }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Concurrent proposal-id reservation regression.'
                    changedFiles = @()
                    findings = @()
                    draftText = $task.taskId
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('proposal-id race')
                    }
                    createdAtUtc = '2026-07-23T02:02:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                New-Item -ItemType File -Force -Path (Join-Path '{{escapedMarkerDirectory}}' ('done-' + $task.taskId)) | Out-Null
                """);
            var firstProfile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                WorkerId = "analysis_codex_proposal_race_one",
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var secondProfile = firstProfile with { WorkerId = "analysis_codex_proposal_race_two" };
            var baseTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = 10 });
            var firstTask = baseTask with
            {
                TaskId = "worker_task_proposal_race_one",
                WorkerId = firstProfile.WorkerId
            };
            var secondTask = baseTask with
            {
                TaskId = "worker_task_proposal_race_two",
                WorkerId = secondProfile.WorkerId
            };
            var firstPool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));
            var secondPool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var firstRun = firstPool.RunTaskAsync(firstProfile, firstTask);
            var secondRun = secondPool.RunTaskAsync(secondProfile, secondTask);
            await WaitForFileAsync(Path.Combine(markerDirectory, $"ready-{firstTask.TaskId}"), TimeSpan.FromSeconds(5));
            await WaitForFileAsync(Path.Combine(markerDirectory, $"ready-{secondTask.TaskId}"), TimeSpan.FromSeconds(5));
            var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
            await File.WriteAllTextAsync(releasePath, "release");
            await WaitForFileAsync(Path.Combine(markerDirectory, $"done-{firstTask.TaskId}"), TimeSpan.FromSeconds(5));
            await WaitForFileAsync(Path.Combine(markerDirectory, $"done-{secondTask.TaskId}"), TimeSpan.FromSeconds(5));
            await Task.Delay(500);
            await writeLease.DisposeAsync();
            var results = await Task.WhenAll(firstRun, secondRun);

            Assert.Single(results, result => result.Proposal != null);
            Assert.Single(results, result =>
                result.Status.State == WorkerBridgeState.Failed &&
                result.Status.LastError?.Contains("proposal id already exists", StringComparison.OrdinalIgnoreCase) == true);
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId);
            Assert.NotNull(stored);
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
    public async Task RunTaskAsync_WhenWorkerTimesOutAfterWritingMalformedProposal_PreservesTimeoutTruth()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-malformed-proposal-then-timeout.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                [System.IO.File]::WriteAllText(
                    $env:BOE_WORKER_PROPOSAL_PATH,
                    '{not valid json',
                    [System.Text.UTF8Encoding]::new($false))
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
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("malformed", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(auditEvents, item => item.EventType == "proposal-rejected");
            Assert.Contains(auditEvents, item => item.EventType == "task-timed-out");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ExistingTaskId_RejectsWithoutOverwritingPriorDispatchArtifact()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = 10 });
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(task.TaskId);
            const string priorTaskJson = "{\"prior\":true}";
            await fs.WriteFileAtomicAsync(taskPath, priorTaskJson);
            var scriptPath = Path.Combine(root, "fake-worker-existing-task-id.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task);

            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("task id already exists", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(priorTaskJson, await fs.ReadFileAsync(taskPath));
            Assert.Empty(await new GmWorkerAuditLog(fs).ReadEventsAsync());
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

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
                return;
            await Task.Delay(25);
        }

        Assert.Fail($"Timed out waiting for test synchronization file: {path}");
    }

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
