using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.Services;
using BookOfEternityClient.Configuration;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Xunit;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeLifecycleTests
{
    [Fact]
    public void ResolveRuntimeRoot_ConfiguredBaseInsideCanonicalSession_FailsClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var overload = typeof(GmWorkerExecutionWorkspace).GetMethod(
                "ResolveRuntimeRoot",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string)],
                modifiers: null);
            Assert.NotNull(overload);

            foreach (var configuredBase in new[]
                     {
                         fs.GameSessionPath,
                         Path.Combine(fs.GameSessionPath, "worker-runtime")
                     })
            {
                var invocation = Assert.Throws<TargetInvocationException>(
                    () => overload!.Invoke(null, [fs.BasePath, configuredBase]));
                var containmentFailure = Assert.IsType<InvalidOperationException>(invocation.InnerException);
                Assert.Contains("game_session", containmentFailure.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ResolveRuntimeRoot_ConfiguredBaseThroughJunctionIntoCanonicalSession_FailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        var aliasRoot = CreateTempRoot();
        var aliasPath = Path.Combine(aliasRoot, "session-alias");
        try
        {
            var fs = CreateFileSystem(root);
            CreateDirectoryJunction(aliasPath, fs.GameSessionPath);
            var overload = typeof(GmWorkerExecutionWorkspace).GetMethod(
                "ResolveRuntimeRoot",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string)],
                modifiers: null);
            Assert.NotNull(overload);

            var invocation = Assert.Throws<TargetInvocationException>(
                () => overload!.Invoke(null, [fs.BasePath, Path.Combine(aliasPath, "worker-runtime")]));
            var containmentFailure = Assert.IsType<InvalidOperationException>(invocation.InnerException);
            Assert.Contains("game_session", containmentFailure.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(aliasPath))
                Directory.Delete(aliasPath);
            CleanupTempRoot(aliasRoot);
            CleanupTempRoot(root);
        }
    }

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
            Assert.True(
                result.ExitCode == 0,
                $"ExitCode={result.ExitCode?.ToString() ?? "<null>"}; {result.Status.LastError}" +
                $"{Environment.NewLine}STDOUT: {result.StandardOutput}" +
                $"{Environment.NewLine}STDERR: {result.StandardError}");
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
    public async Task RunTaskAsync_WorkerExitDoesNotWaitForDescendantHoldingInheritedOutputPipe()
    {
        var root = CreateTempRoot();
        Process? descendant = null;
        try
        {
            var fs = CreateFileSystem(root);
            var descendantPidPath = Path.Combine(root, "worker-descendant.pid");
            var descendantReadyPath = Path.Combine(root, "worker-descendant.ready");
            var descendantScriptPath = Path.Combine(root, "fake-worker-descendant-output-tail.ps1");
            await File.WriteAllTextAsync(descendantScriptPath, """
                param([int]$WorkerPid, [string]$ReadyPath)
                Set-Content -LiteralPath $ReadyPath -Value 'ready' -Encoding ascii
                while (Get-Process -Id $WorkerPid -ErrorAction SilentlyContinue) {
                    Start-Sleep -Milliseconds 10
                }
                Write-Output 'worker-descendant-tail'
                Start-Sleep -Seconds 60
                """);
            var scriptPath = Path.Combine(root, "fake-worker-descendant-output-pipe.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $descendant = Start-Process powershell.exe -NoNewWindow -ArgumentList @('-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', '{{descendantScriptPath.Replace("'", "''", StringComparison.Ordinal)}}', $PID, '{{descendantReadyPath.Replace("'", "''", StringComparison.Ordinal)}}') -PassThru
                Set-Content -LiteralPath '{{descendantPidPath.Replace("'", "''", StringComparison.Ordinal)}}' -Value $descendant.Id -Encoding ascii
                while (-not (Test-Path -LiteralPath '{{descendantReadyPath.Replace("'", "''", StringComparison.Ordinal)}}')) {
                    Start-Sleep -Milliseconds 10
                }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_descendant_output_pipe'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Worker root completed while a descendant retained inherited output.'
                    changedFiles = @()
                    findings = @()
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('descendant output ownership')
                    }
                    createdAtUtc = '2026-07-23T05:00:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                Write-Output 'worker-root-complete'
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 15
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_descendant_output_pipe",
                    TimeoutSeconds = profile.TimeoutSeconds
                });

            var run = new GmWorkerBridgePool(fs).RunTaskAsync(profile, task);
            descendant = Process.GetProcessById(
                int.Parse((await ReadFileWhenAvailableAsync(
                    descendantPidPath,
                    TimeSpan.FromSeconds(5))).Trim()));
            var result = await run;

            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("worker-root-complete", result.StandardOutput);
            Assert.Contains("worker-descendant-tail", result.StandardOutput);
            Assert.NotNull(result.Proposal);
            await descendant.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(descendant.HasExited);
        }
        finally
        {
            try
            {
                if (descendant is { HasExited: false })
                    descendant.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup for the RED implementation.
            }

            descendant?.Dispose();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WorkerCompletionKeepsHostAliveUntilProcessTreeStopBegins()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-host-liveness.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_host_liveness'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Host remains authoritative through process-tree stop.'
                    changedFiles = @()
                    findings = @()
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('host liveness')
                    }
                    createdAtUtc = '2026-07-23T05:10:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                exit 0
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
                    TaskId = "worker_task_host_liveness",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var processTreeFactory = new RootLivenessProcessTreeFactory();
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks: null,
                processTreeFactory: processTreeFactory);

            var result = await pool.RunTaskAsync(profile, task);

            Assert.False(result.TimedOut);
            Assert.NotNull(result.Proposal);
            Assert.True(processTreeFactory.RootWasAliveWhenStopBegan);
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
            var runtimeRoot = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(fs.BasePath);
            Assert.False(Directory.Exists(runtimeRoot) && Directory.EnumerateFileSystemEntries(runtimeRoot).Any());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_WorkerParentTraversalCannotReachLiveSession()
    {
        var root = CreateTempRoot();
        try
        {
            const string canonicalPath = "game_state/world/weather.json";
            const string original = "{\"weather\":\"live-original\"}";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(canonicalPath, original);
            var scriptPath = Path.Combine(root, "fake-worker-parent-traversal.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $workspaceRoot = Split-Path -Parent $env:BOE_WORKER_SESSION_PATH
                $escapedPath = Join-Path $workspaceRoot '..\..\game_session\game_state\world\weather.json'
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $escapedPath) | Out-Null
                '{"weather":"escaped-worker-mutation"}' | Set-Content -Path $escapedPath -Encoding UTF8
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_parent_traversal'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'The detached workspace must not be adjacent to the live session.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'parent traversal regression'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('attempted only the historical sibling traversal')
                    }
                    createdAtUtc = '2026-07-26T00:00:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.AnalysisTask() with
            {
                TaskId = "worker_task_parent_traversal",
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

            var result = await new GmWorkerBridgePool(fs).RunTaskAsync(profile, task);

            Assert.NotNull(result.Proposal);
            Assert.Equal(original, await fs.ReadFileAsync(canonicalPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ProcessOutputIsDrainedButCaptureIsBounded()
    {
        const int expectedCaptureLimit = 64 * 1024;
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-large-output.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                [Console]::Out.Write(('o' * 131072))
                [Console]::Error.Write(('e' * 131072))
                exit 17
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
                    TaskId = "worker_task_large_output",
                    TimeoutSeconds = profile.TimeoutSeconds
                });

            var result = await new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs)).RunTaskAsync(profile, task);

            Assert.Equal(17, result.ExitCode);
            Assert.InRange(result.StandardOutput.Length, 1, expectedCaptureLimit + 256);
            Assert.InRange(result.StandardError.Length, 1, expectedCaptureLimit + 256);
            Assert.Contains("truncated", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("truncated", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_OversizedProposalIsRejectedBeforeDeserialization()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-oversized-proposal.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $bytes = [System.Text.Encoding]::UTF8.GetBytes('{' + ('x' * 1048576) + '}')
                [System.IO.File]::WriteAllBytes($env:BOE_WORKER_PROPOSAL_PATH, $bytes)
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
                    TaskId = "worker_task_oversized_proposal",
                    TimeoutSeconds = profile.TimeoutSeconds
                });

            var result = await new GmWorkerBridgePool(fs).RunTaskAsync(profile, task);

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("proposal", result.Status.LastError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("limit", result.Status.LastError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_OversizedContentRefIsRejectedBeforePublication()
    {
        var root = CreateTempRoot();
        try
        {
            const string canonicalPath = "game_state/world/weather.json";
            const string proposalId = "worker_proposal_oversized_content";
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-oversized-content.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $contentRef = 'worker_proposals/{{proposalId}}/{{canonicalPath}}'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $contentPath) | Out-Null
                $content = [System.Text.Encoding]::UTF8.GetBytes('x' * 4194305)
                [System.IO.File]::WriteAllBytes($contentPath, $content)
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    $afterSha256 = [System.BitConverter]::ToString(
                        $sha.ComputeHash($content)
                    ).Replace('-', '').ToLowerInvariant()
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
                    summary = 'Oversized content must not enter the durable proposal inbox.'
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
                        notes = @('quota regression')
                    }
                    createdAtUtc = '2026-07-26T00:01:00Z'
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
                    TaskId = "worker_task_oversized_content",
                    TimeoutSeconds = profile.TimeoutSeconds
                });

            var result = await new GmWorkerBridgePool(fs).RunTaskAsync(profile, task);

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("contentRef", result.Status.LastError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("limit", result.Status.LastError, StringComparison.OrdinalIgnoreCase);
            Assert.Null(await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId));
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
            var runtimeRoot = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(fs.BasePath);
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
        string? runtimeRoot = null;
        FileStream? cleanupLock = null;
        try
        {
            var fs = CreateFileSystem(root);
            runtimeRoot = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(fs.BasePath);
            var workerScriptPath = Path.Combine(root, "fake-worker-cleanup-lock.ps1");
            await File.WriteAllTextAsync(workerScriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
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
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkspaceCleanupAsync = workspacePath =>
                {
                    cleanupLock = File.Open(
                        Path.Combine(workspacePath, "worker-lock.bin"),
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                    return Task.CompletedTask;
                }
            };
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit, hooks);

            var result = await pool.RunTaskAsync(profile, task);
            var events = await audit.ReadEventsAsync();

            Assert.NotNull(result.Proposal);
            Assert.Equal("worker_proposal_cleanup_failure", result.Proposal!.ProposalId);
            Assert.Contains(events, item => item.EventType == "workspace-cleanup-failed");
        }
        finally
        {
            cleanupLock?.Dispose();
            if (!string.IsNullOrWhiteSpace(runtimeRoot))
                CleanupTempRoot(runtimeRoot);
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
            var runtimeRoot = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(fs.BasePath);
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
            await GmWorkerBridgeTestFixtures.WriteProposalFixtureAsync(fs, prior);
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
    public async Task RunTaskAsync_LoadDuringWorkerExecutionRejectsProposalFromReplacedSession()
    {
        var root = CreateTempRoot();
        var releasePath = Path.Combine(root, "release-stale-session-worker");
        try
        {
            const string proposalId = "worker_proposal_stale_session";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(
                "game_state/meta/soul_state.json",
                """{"currentRealm":"Mortal World"}""");
            var stateManager = new StateManager(
                fs,
                new GameSettings(),
                NullLogger<StateManager>.Instance);
            await stateManager.RefreshGameStateAsync();
            var saveLoad = new SaveLoadService(
                fs,
                stateManager,
                NullLogger<SaveLoadService>.Instance);
            Assert.True(await saveLoad.SaveGameAsync("pre-worker-session", "session generation regression"));
            var savePath = Directory.GetFiles(fs.ResolvePath("saves/manual_saves"), "*.zip").Single();

            var readyPath = Path.Combine(root, "stale-session-worker-ready");
            var scriptPath = Path.Combine(root, "fake-worker-stale-session.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path '{{readyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                while (-not (Test-Path -LiteralPath '{{releasePath.Replace("'", "''", StringComparison.Ordinal)}}')) { Start-Sleep -Milliseconds 25 }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'This result belongs to the session that was replaced during execution.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'stale session output'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('session generation')
                    }
                    createdAtUtc = '2026-07-23T03:00:00Z'
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
                    TaskId = "worker_task_stale_session",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var runTask = pool.RunTaskAsync(profile, task);
            await WaitForFileAsync(readyPath, TimeSpan.FromSeconds(5));
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(task.TaskId);
            using (var archive = ZipFile.Open(savePath, ZipArchiveMode.Update))
            {
                var taskEntry = archive.CreateEntry(taskPath);
                await using var entryStream = taskEntry.Open();
                var liveTaskBytes = await File.ReadAllBytesAsync(fs.ResolvePath(taskPath));
                await entryStream.WriteAsync(liveTaskBytes);
            }
            Assert.True(await saveLoad.LoadGameAsync(savePath));
            Assert.False(fs.FileExists(taskPath));
            await File.WriteAllTextAsync(releasePath, "release");

            var result = await runTask;

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("session", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Null(await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
        }
        finally
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(releasePath, "release");
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CallerMutationBeforeSlotWaitCannotChangeDurableReservation()
    {
        var root = CreateTempRoot();
        try
        {
            const string weatherPath = "game_state/world/weather.json";
            const string secretPath = "game_state/world/secret.json";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(weatherPath, "{\"weather\":true}");
            await fs.WriteFileAtomicAsync(secretPath, "{\"secret\":true}");
            var mutableAllowedPaths = new[] { weatherPath };
            var scriptPath = Path.Combine(root, "fake-worker-pre-reservation-mutation.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand =
                    $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.ValidationRepairTask() with
                {
                    TaskId = "worker_task_pre_reservation_mutation",
                    TimeoutSeconds = profile.TimeoutSeconds,
                    ContextFiles =
                    [
                        new WorkerFileReference { Path = weatherPath },
                        new WorkerFileReference { Path = secretPath }
                    ],
                    AllowedProposalPaths = mutableAllowedPaths
                });
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                new GmWorkerBridgePoolHooks
                {
                    BeforeWorkerSlotWaitAsync = () =>
                    {
                        mutableAllowedPaths[0] = secretPath;
                        return Task.CompletedTask;
                    }
                });

            _ = await pool.RunTaskAsync(profile, task);

            var reservedJson = await fs.ReadFileAsync(
                GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
            var reservedTask = GmWorkerJson.Deserialize<WorkerTaskPacket>(reservedJson!);
            Assert.NotNull(reservedTask);
            Assert.Equal([weatherPath], reservedTask.AllowedProposalPaths);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CallerMutationAfterReservationCannotExpandAuthoritativeTaskScope()
    {
        var root = CreateTempRoot();
        try
        {
            const string weatherPath = "game_state/world/weather.json";
            const string secretPath = "game_state/world/secret.json";
            const string proposalId = "worker_proposal_mutated_task_scope";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(weatherPath, "{\"weather\":true}");
            await fs.WriteFileAtomicAsync(secretPath, "{\"secret\":true}");
            var mutableAllowedPaths = new[] { weatherPath };
            var scriptPath = Path.Combine(root, "fake-worker-mutated-task-scope.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $contentRef = 'worker_proposals/{{proposalId}}/game_state/world/secret.json'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
                Set-Content -Path $contentPath -Value '{"worker":"expanded scope"}' -Encoding UTF8 -NoNewline
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try { $afterSha256 = ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($contentPath)))).Replace('-', '').ToLowerInvariant() }
                finally { $sha.Dispose() }
                $secretContext = $task.contextFiles | Where-Object { $_.path -eq '{{secretPath}}' } | Select-Object -First 1
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Attempted scope expansion through caller mutation.'
                    changedFiles = @([ordered]@{
                        path = '{{secretPath}}'
                        changeKind = 'replace'
                        beforeSha256 = $secretContext.sha256
                        afterSha256 = $afterSha256
                        contentRef = $contentRef
                    })
                    findings = @()
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('reserved task authority regression')
                    }
                    createdAtUtc = '2026-07-23T03:02:00Z'
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
                    TaskId = "worker_task_mutated_task_scope",
                    TimeoutSeconds = profile.TimeoutSeconds,
                    ContextFiles =
                    [
                        new WorkerFileReference { Path = weatherPath },
                        new WorkerFileReference { Path = secretPath }
                    ],
                    AllowedProposalPaths = mutableAllowedPaths
                });
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                new GmWorkerBridgePoolHooks
                {
                    BeforeProcessTreeAttachAsync = () =>
                    {
                        mutableAllowedPaths[0] = secretPath;
                        return Task.CompletedTask;
                    }
                });

            var result = await pool.RunTaskAsync(profile, task);

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("allowedProposalPaths", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ClearGameStateDuringWorkerExecutionRejectsProposalFromPriorGeneration()
    {
        var root = CreateTempRoot();
        var releasePath = Path.Combine(root, "release-cleared-session-worker");
        try
        {
            const string proposalId = "worker_proposal_cleared_session";
            var fs = CreateFileSystem(root);
            var readyPath = Path.Combine(root, "cleared-session-worker-ready");
            var scriptPath = Path.Combine(root, "fake-worker-cleared-session.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path '{{readyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                while (-not (Test-Path -LiteralPath '{{releasePath.Replace("'", "''", StringComparison.Ordinal)}}')) { Start-Sleep -Milliseconds 25 }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = '{{proposalId}}'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'This result belongs to the session cleared during execution.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'stale cleared-session output'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('session generation')
                    }
                    createdAtUtc = '2026-07-23T03:05:00Z'
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
                    TaskId = "worker_task_cleared_session",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var runTask = pool.RunTaskAsync(profile, task);
            await WaitForFileAsync(readyPath, TimeSpan.FromSeconds(5));
            fs.ClearGameState();
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId)));
            await File.WriteAllTextAsync(releasePath, "release");

            var result = await runTask;

            Assert.Null(result.Proposal);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Contains("session", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.Null(await new GmWorkerProposalStore(fs).ReadProposalAsync(proposalId));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
        }
        finally
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(releasePath, "release");
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
                TimeoutSeconds = 10,
                MaxConcurrentTasks = 2
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_concurrent_reservation",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var reservationBarrier = new AsyncTestBarrier(2);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeTaskReservationAsync = reservationBarrier.SignalAndWaitAsync
            };
            var firstPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            var secondPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var firstRun = firstPool.RunTaskAsync(profile, task);
            var secondRun = secondPool.RunTaskAsync(profile, task);
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
            var slotWaitCount = 0;
            var secondSlotWaitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () =>
                {
                    if (Interlocked.Increment(ref slotWaitCount) == 2)
                        secondSlotWaitStarted.SetResult();
                    return Task.CompletedTask;
                }
            };
            var firstPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            var secondPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            firstRun = firstPool.RunTaskAsync(profile, firstTask);
            await WaitForFileAsync(Path.Combine(markerDirectory, firstTask.TaskId), TimeSpan.FromSeconds(5));
            secondRun = secondPool.RunTaskAsync(profile, secondTask);
            await secondSlotWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

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
    public async Task RunTaskAsync_IdleWorkerGateAcceptsUpdatedConcurrencyLimit()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-idle-limit-change.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = ('worker_proposal_' + $task.taskId)
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Idle gate limit change regression.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'complete'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('idle gate')
                    }
                    createdAtUtc = '2026-07-23T03:10:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var firstProfile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10,
                MaxConcurrentTasks = 1
            };
            var secondProfile = firstProfile with { MaxConcurrentTasks = 2 };
            var firstTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_idle_limit_one",
                    TimeoutSeconds = 10
                });
            var secondTask = firstTask with { TaskId = "worker_task_idle_limit_two" };

            var firstResult = await new GmWorkerBridgePool(fs).RunTaskAsync(firstProfile, firstTask);
            var secondResult = await new GmWorkerBridgePool(fs).RunTaskAsync(secondProfile, secondTask);

            Assert.NotNull(firstResult.Proposal);
            Assert.NotNull(secondResult.Proposal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ActiveWorkerGateLimitChangeReturnsFailedResult()
    {
        var root = CreateTempRoot();
        var releasePath = Path.Combine(root, "release-active-limit-worker");
        Task<GmWorkerTaskRunResult>? firstRun = null;
        try
        {
            var fs = CreateFileSystem(root);
            var readyPath = Path.Combine(root, "active-limit-worker-ready");
            var scriptPath = Path.Combine(root, "fake-worker-active-limit-change.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                New-Item -ItemType File -Force -Path '{{readyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                while (-not (Test-Path -LiteralPath '{{releasePath.Replace("'", "''", StringComparison.Ordinal)}}')) { Start-Sleep -Milliseconds 25 }
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = ('worker_proposal_' + $task.taskId)
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Active gate limit change regression.'
                    changedFiles = @()
                    findings = @()
                    draftText = 'complete'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('active gate')
                    }
                    createdAtUtc = '2026-07-23T03:11:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var activeProfile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10,
                MaxConcurrentTasks = 1
            };
            var changedProfile = activeProfile with { MaxConcurrentTasks = 2 };
            var firstTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_active_limit_one",
                    TimeoutSeconds = 10
                });
            var secondTask = firstTask with { TaskId = "worker_task_active_limit_two" };
            firstRun = new GmWorkerBridgePool(fs).RunTaskAsync(activeProfile, firstTask);
            await WaitForFileAsync(readyPath, TimeSpan.FromSeconds(5));

            var secondResult = await new GmWorkerBridgePool(fs).RunTaskAsync(changedProfile, secondTask);

            Assert.Equal(WorkerBridgeState.Failed, secondResult.Status.State);
            Assert.Contains("maxConcurrentTasks", secondResult.Status.LastError!, StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetTaskPacketPath(secondTask.TaskId)));
        }
        finally
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(releasePath, "release");
            if (firstRun != null)
                await firstRun;
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationKillsProcessBeforeReturningAndReleasingSlot()
    {
        var root = CreateTempRoot();
        Process? leakedProcess = null;
        Process? leakedChildProcess = null;
        try
        {
            var fs = CreateFileSystem(root);
            var pidPath = Path.Combine(root, "cancelled-worker.pid");
            var childPidPath = Path.Combine(root, "cancelled-worker-child.pid");
            var scriptPath = Path.Combine(root, "fake-worker-cancellation.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                Set-Content -LiteralPath '{{pidPath.Replace("'", "''", StringComparison.Ordinal)}}' -Value $PID -Encoding ascii
                $child = Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @('-NoLogo', '-NoProfile', '-Command', 'Start-Sleep -Seconds 30') -PassThru
                Set-Content -LiteralPath '{{childPidPath.Replace("'", "''", StringComparison.Ordinal)}}' -Value $child.Id -Encoding ascii
                Start-Sleep -Seconds 30
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60,
                MaxConcurrentTasks = 1
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_process",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            using var cancellation = new CancellationTokenSource();
            var runTask = new GmWorkerBridgePool(fs).RunTaskAsync(profile, task, cancellation.Token);
            var pid = int.Parse((await ReadFileWhenAvailableAsync(pidPath, TimeSpan.FromSeconds(5))).Trim());
            var childPid = int.Parse((await ReadFileWhenAvailableAsync(childPidPath, TimeSpan.FromSeconds(5))).Trim());
            leakedProcess = Process.GetProcessById(pid);
            leakedChildProcess = Process.GetProcessById(childPid);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            await leakedProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await leakedChildProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(leakedProcess.HasExited);
            Assert.True(leakedChildProcess.HasExited);
        }
        finally
        {
            try
            {
                if (leakedProcess is { HasExited: false })
                    leakedProcess.Kill(entireProcessTree: true);
                if (leakedChildProcess is { HasExited: false })
                    leakedChildProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup for the RED implementation.
            }

            leakedProcess?.Dispose();
            leakedChildProcess?.Dispose();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_DoesNotLaunchWorkerCommandBeforeProcessTreeOwnershipIsAttached()
    {
        var root = CreateTempRoot();
        var releaseAttach = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<GmWorkerTaskRunResult>? runTask = null;
        try
        {
            var fs = CreateFileSystem(root);
            var workerStartedPath = Path.Combine(root, "worker-started-before-tree-attach");
            var scriptPath = Path.Combine(root, "fake-worker-attach-handshake.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                New-Item -ItemType File -Force -Path '{{workerStartedPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                exit 0
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
                    TaskId = "worker_task_attach_handshake",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var releaseGateEntered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkerReleaseAsync = async () =>
                {
                    releaseGateEntered.TrySetResult();
                    await releaseAttach.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            runTask = pool.RunTaskAsync(profile, task);
            await releaseGateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(File.Exists(workerStartedPath));

            releaseAttach.TrySetResult();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(workerStartedPath));
        }
        finally
        {
            releaseAttach.TrySetResult();
            if (runTask != null)
            {
                try
                {
                    await runTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Best effort cleanup for the RED implementation.
                }
            }

            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationDuringOwnershipHandshakeNeverReleasesWorkerCommand()
    {
        var root = CreateTempRoot();
        var releaseAttach = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<GmWorkerTaskRunResult>? runTask = null;
        try
        {
            var fs = CreateFileSystem(root);
            var workerStartedPath = Path.Combine(root, "worker-started-after-cancelled-handshake");
            var scriptPath = Path.Combine(root, "fake-worker-cancelled-handshake.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                New-Item -ItemType File -Force -Path '{{workerStartedPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                exit 0
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
                    TaskId = "worker_task_cancelled_handshake",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var attachEntered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeProcessTreeAttachAsync = async () =>
                {
                    attachEntered.TrySetResult();
                    await releaseAttach.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            using var cancellation = new CancellationTokenSource();

            runTask = pool.RunTaskAsync(profile, task, cancellation.Token);
            await attachEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => runTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(File.Exists(workerStartedPath));
        }
        finally
        {
            releaseAttach.TrySetResult();
            if (runTask != null)
            {
                try
                {
                    await runTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Cancellation is asserted above; cleanup only ensures no host remains.
                }
            }

            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ProfileTimeoutIncludesOwnershipHandshake()
    {
        var root = CreateTempRoot();
        var releaseAttach = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<GmWorkerTaskRunResult>? runTask = null;
        try
        {
            var fs = CreateFileSystem(root);
            var workerStartedPath = Path.Combine(root, "worker-started-after-handshake-timeout");
            var scriptPath = Path.Combine(root, "fake-worker-timeout-handshake.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                New-Item -ItemType File -Force -Path '{{workerStartedPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                exit 0
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 5
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_handshake",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var attachEntered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeProcessTreeAttachAsync = async () =>
                {
                    attachEntered.TrySetResult();
                    await releaseAttach.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            runTask = pool.RunTaskAsync(profile, task);
            await attachEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.False(File.Exists(workerStartedPath));
        }
        finally
        {
            releaseAttach.TrySetResult();
            if (runTask != null)
            {
                try
                {
                    await runTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Best effort cleanup for the RED implementation.
                }
            }

            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationKeepsWorkerSlotUntilWholeProcessTreeReportsStopped()
    {
        var root = CreateTempRoot();
        var processTreeFactory = new ControlledProcessTreeFactory();
        Task<GmWorkerTaskRunResult>? firstRun = null;
        Task<GmWorkerTaskRunResult>? secondRun = null;
        try
        {
            const string firstTaskId = "worker_task_tree_slot_first";
            const string secondTaskId = "worker_task_tree_slot_second";
            var fs = CreateFileSystem(root);
            var firstReadyPath = Path.Combine(root, "tree-slot-first-ready");
            var secondReadyPath = Path.Combine(root, "tree-slot-second-ready");
            var scriptPath = Path.Combine(root, "fake-worker-tree-slot.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                if ($task.taskId -eq '{{firstTaskId}}') {
                    New-Item -ItemType File -Force -Path '{{firstReadyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                    Start-Sleep -Seconds 30
                    exit 0
                }

                New-Item -ItemType File -Force -Path '{{secondReadyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                exit 0
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60,
                MaxConcurrentTasks = 1
            };
            var firstTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = firstTaskId,
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var secondTask = firstTask with { TaskId = secondTaskId };
            var secondSlotWaitStarted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var slotWaitCount = 0;
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () =>
                {
                    if (Interlocked.Increment(ref slotWaitCount) == 2)
                        secondSlotWaitStarted.TrySetResult();
                    return Task.CompletedTask;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks,
                processTreeFactory);
            using var cancellation = new CancellationTokenSource();

            firstRun = pool.RunTaskAsync(profile, firstTask, cancellation.Token);
            await WaitForFileAsync(firstReadyPath, TimeSpan.FromSeconds(5));
            secondRun = pool.RunTaskAsync(profile, secondTask);
            await secondSlotWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            cancellation.Cancel();
            await processTreeFactory.FirstStopStarted.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(firstRun.IsCompleted);
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetTaskPacketPath(secondTaskId)));
            Assert.False(File.Exists(secondReadyPath));

            processTreeFactory.ReleaseFirstTree();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRun);
            await secondRun.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(secondReadyPath));
        }
        finally
        {
            processTreeFactory.ReleaseFirstTree();
            if (firstRun != null)
            {
                try
                {
                    await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // The cancellation result is asserted above; cleanup only ensures no process remains.
                }
            }

            if (secondRun != null)
            {
                try
                {
                    await secondRun.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Best effort cleanup for the RED implementation.
                }
            }

            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_ProcessTreeConfirmationFailureQuarantinesSlotAndStillDisposesTreeOwner()
    {
        var root = CreateTempRoot();
        var processTreeFactory = new ThrowingProcessTreeFactory();
        Task<GmWorkerTaskRunResult>? firstRun = null;
        try
        {
            const string firstTaskId = "worker_task_tree_failure_first";
            const string secondTaskId = "worker_task_tree_failure_second";
            var fs = CreateFileSystem(root);
            var firstReadyPath = Path.Combine(root, "tree-failure-first-ready");
            var secondReadyPath = Path.Combine(root, "tree-failure-second-ready");
            var scriptPath = Path.Combine(root, "fake-worker-tree-failure.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                if ($task.taskId -eq '{{firstTaskId}}') {
                    New-Item -ItemType File -Force -Path '{{firstReadyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                    Start-Sleep -Seconds 30
                    exit 0
                }

                New-Item -ItemType File -Force -Path '{{secondReadyPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                exit 0
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60,
                MaxConcurrentTasks = 1
            };
            var firstTask = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = firstTaskId,
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var secondTask = firstTask with { TaskId = secondTaskId };
            var workspaceCleanupCalls = 0;
            string? retainedWorkspacePath = null;
            var firstPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                new GmWorkerBridgePoolHooks
                {
                    BeforeWorkspaceCleanupAsync = workspacePath =>
                    {
                        retainedWorkspacePath = workspacePath;
                        Interlocked.Increment(ref workspaceCleanupCalls);
                        return Task.CompletedTask;
                    }
                },
                processTreeFactory);
            using var firstCancellation = new CancellationTokenSource();

            firstRun = firstPool.RunTaskAsync(profile, firstTask, firstCancellation.Token);
            await WaitForFileAsync(firstReadyPath, TimeSpan.FromSeconds(5));
            firstCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRun);

            Assert.True(processTreeFactory.DisposeCalled);
            Assert.True(processTreeFactory.HasLiveProcess);
            Assert.Equal(0, Volatile.Read(ref workspaceCleanupCalls));
            Assert.Null(retainedWorkspacePath);
            var runtimeRoot = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(fs.BasePath);
            Assert.True(Directory.Exists(runtimeRoot));
            Assert.NotEmpty(Directory.EnumerateDirectories(runtimeRoot));
            using var secondCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var secondRun = new GmWorkerBridgePool(fs).RunTaskAsync(
                profile,
                secondTask,
                secondCancellation.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondRun);
            Assert.False(File.Exists(secondReadyPath));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetTaskPacketPath(secondTaskId)));
        }
        finally
        {
            await processTreeFactory.ForceCleanupAsync();
            if (firstRun != null)
            {
                try
                {
                    await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // The expected cancellation is asserted above.
                }
            }

            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutRemainsAuthoritativeWhenTreeCleanupIsUnconfirmed()
    {
        var root = CreateTempRoot();
        var processTreeFactory = new ThrowingProcessTreeFactory();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-timeout-cleanup-failure.ps1");
            await File.WriteAllTextAsync(scriptPath, "Start-Sleep -Seconds 30");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 1
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_cleanup_failure",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var audit = new GmWorkerAuditLog(fs);
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                audit,
                hooks: null,
                processTreeFactory);

            var result = await pool.RunTaskAsync(profile, task);
            var auditEvents = await audit.ReadEventsAsync();

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Contains(auditEvents, item => item.EventType == "task-timed-out");
            Assert.Contains(auditEvents, item => item.EventType == "process-tree-cleanup-unconfirmed");
            Assert.True(processTreeFactory.DisposeCalled);
            Assert.True(processTreeFactory.HasLiveProcess);
        }
        finally
        {
            await processTreeFactory.ForceCleanupAsync();
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
            var publicationBarrier = new AsyncTestBarrier(2);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeProposalPublicationAsync = publicationBarrier.SignalAndWaitAsync
            };
            var firstPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            var secondPool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var firstRun = firstPool.RunTaskAsync(firstProfile, firstTask);
            var secondRun = secondPool.RunTaskAsync(secondProfile, secondTask);
            await WaitForFileAsync(Path.Combine(markerDirectory, $"ready-{firstTask.TaskId}"), TimeSpan.FromSeconds(5));
            await WaitForFileAsync(Path.Combine(markerDirectory, $"ready-{secondTask.TaskId}"), TimeSpan.FromSeconds(5));
            await File.WriteAllTextAsync(releasePath, "release");
            await WaitForFileAsync(Path.Combine(markerDirectory, $"done-{firstTask.TaskId}"), TimeSpan.FromSeconds(5));
            await WaitForFileAsync(Path.Combine(markerDirectory, $"done-{secondTask.TaskId}"), TimeSpan.FromSeconds(5));
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
    public async Task RunTaskAsync_WorkerCannotForgeSuccessThroughLegacyHostMarkerFiles()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-forged-host-completion.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $runtimeRoot = Split-Path -Parent $env:BOE_WORKER_SESSION_PATH
                $ready = Get-ChildItem -Path $runtimeRoot -Filter 'worker-host-*.ready' -File -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($null -ne $ready) {
                    $completionPath = [System.IO.Path]::ChangeExtension($ready.FullName, '.completed')
                    '{"SchemaVersion":1,"ExitCode":0}' | Set-Content -Path $completionPath -Encoding UTF8
                    Start-Sleep -Seconds 30
                }
                exit 17
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
                    TaskId = "worker_task_forged_host_completion",
                    TimeoutSeconds = profile.TimeoutSeconds
                });

            var result = await new GmWorkerBridgePool(fs).RunTaskAsync(profile, task);

            Assert.Equal(17, result.ExitCode);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Empty(Directory.EnumerateFiles(
                root,
                "worker-host-*",
                SearchOption.AllDirectories));
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
    public async Task RunTaskAsync_WhenWorkerExitsNonZeroAfterWritingValidProposal_KeepsProposalDiagnosticOnly()
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
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Null(stored);
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
    public async Task RunTaskAsync_WhenWorkerTimesOutAfterWritingValidProposal_KeepsProposalDiagnosticOnly()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var proposalWrittenPath = Path.Combine(root, "proposal-written-before-timeout");
            var scriptPath = Path.Combine(root, "fake-worker-proposal-then-timeout.ps1");
            await File.WriteAllTextAsync(scriptPath, $$"""
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
                New-Item -ItemType File -Force -Path '{{proposalWrittenPath.Replace("'", "''", StringComparison.Ordinal)}}' | Out-Null
                Start-Sleep -Seconds 30
                """);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with { TimeoutSeconds = profile.TimeoutSeconds });
            using var timeoutSignal = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var runTask = pool.RunTaskAsync(profile, task);
            await WaitForFileAsync(proposalWrittenPath, TimeSpan.FromSeconds(30));
            timeoutSignal.Cancel();
            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(30));
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync("worker_proposal_timeout_after_write");
            var auditEvents = await new GmWorkerAuditLog(fs).ReadEventsAsync();

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Null(stored);
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

    [Fact]
    public async Task RunTaskAsync_CancellationBeforeProposalPublicationLeavesNoApplyableBundle()
    {
        var root = CreateTempRoot();
        var releasePublication = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = await WriteValidEmptyProposalWorkerScriptAsync(
                root,
                "worker_proposal_cancel_before_publish");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_before_publish",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var publicationReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeProposalPublicationAsync = async () =>
                {
                    publicationReached.TrySetResult();
                    await releasePublication.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            using var cancellation = new CancellationTokenSource();

            var runTask = pool.RunTaskAsync(profile, task, cancellation.Token);
            await publicationReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            cancellation.Cancel();
            releasePublication.TrySetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => runTask.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Null(await new GmWorkerProposalStore(fs)
                .ReadProposalAsync("worker_proposal_cancel_before_publish"));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
        }
        finally
        {
            releasePublication.TrySetResult();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutBeforeProposalPublicationLeavesNoApplyableBundle()
    {
        var root = CreateTempRoot();
        var releasePublication = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = await WriteValidEmptyProposalWorkerScriptAsync(
                root,
                "worker_proposal_timeout_before_publish");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_before_publish",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var publicationReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeoutSignal = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token,
                BeforeProposalPublicationAsync = async () =>
                {
                    publicationReached.TrySetResult();
                    await releasePublication.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var runTask = pool.RunTaskAsync(profile, task);
            await publicationReached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            timeoutSignal.Cancel();
            releasePublication.TrySetResult();

            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Null(await new GmWorkerProposalStore(fs)
                .ReadProposalAsync("worker_proposal_timeout_before_publish"));
            Assert.False(fs.FileExists(GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)));
        }
        finally
        {
            releasePublication.TrySetResult();
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
            Assert.DoesNotContain(auditEvents, item => item.EventType == "proposal-rejected");
            Assert.Contains(auditEvents, item => item.EventType == "task-timed-out");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationBeforeContextStagingWinsOverContextMismatch()
    {
        var root = CreateTempRoot();
        var releaseDispatchAudit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var fs = CreateFileSystem(root);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_before_context_staging",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var dispatchAuditReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeTaskDispatchAuditAsync = async () =>
                {
                    dispatchAuditReached.TrySetResult();
                    await releaseDispatchAudit.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);
            using var cancellation = new CancellationTokenSource();

            var runTask = pool.RunTaskAsync(profile, task, cancellation.Token);
            await dispatchAuditReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await fs.WriteFileAtomicAsync(task.ContextFiles[0].Path, "{\"changed\":true}");
            releaseDispatchAudit.TrySetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => runTask.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            releaseDispatchAudit.TrySetResult();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutBeforeContextStagingWinsOverContextMismatch()
    {
        var root = CreateTempRoot();
        var releaseDispatchAudit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var fs = CreateFileSystem(root);
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_before_context_staging",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var dispatchAuditReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeoutSignal = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token,
                BeforeTaskDispatchAuditAsync = async () =>
                {
                    dispatchAuditReached.TrySetResult();
                    await releaseDispatchAudit.Task;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var runTask = pool.RunTaskAsync(profile, task);
            await dispatchAuditReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            timeoutSignal.Cancel();
            await fs.WriteFileAtomicAsync(task.ContextFiles[0].Path, "{\"changed\":true}");
            releaseDispatchAudit.TrySetResult();

            var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            releaseDispatchAudit.TrySetResult();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_GenericFaultAfterTimeoutPreservesTimedOutOutcome()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-timeout-before-attach.ps1");
            await File.WriteAllTextAsync(scriptPath, "Start-Sleep -Seconds 30");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_generic_fault_after_timeout",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            using var timeoutSignal = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token,
                BeforeProcessTreeAttachAsync = () =>
                {
                    timeoutSignal.Cancel();
                    return Task.FromException(
                        new InvalidOperationException("Synthetic fault after timeout."));
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var result = await pool.RunTaskAsync(profile, task).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutAfterWorkerExitWinsOverRejectedHandoff()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-empty-handoff.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_before_rejected_handoff",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            using var timeoutSignal = new CancellationTokenSource();
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token
            };
            var processTreeFactory = new TerminalSignalProcessTreeFactory(timeoutSignal.Cancel);
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks,
                processTreeFactory);

            var result = await pool.RunTaskAsync(profile, task).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Null(result.Proposal);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationAfterWorkerExitWinsOverRejectedHandoff()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var scriptPath = Path.Combine(root, "fake-worker-empty-handoff-canceled.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_before_rejected_handoff",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            using var cancellation = new CancellationTokenSource();
            var processTreeFactory = new TerminalSignalProcessTreeFactory(cancellation.Cancel);
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks: null,
                processTreeFactory);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pool.RunTaskAsync(profile, task, cancellation.Token)
                    .WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TerminalAuditFailureDoesNotReplaceTimedOutOutcome()
    {
        var root = CreateTempRoot();
        var auditMutationCount = 0;
        try
        {
            var fs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (string.Equals(
                                path,
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            Interlocked.Increment(ref auditMutationCount) > 1)
                        {
                            throw new InvalidOperationException("Synthetic terminal audit failure.");
                        }

                        return Task.CompletedTask;
                    }
                });
            var scriptPath = Path.Combine(root, "fake-worker-terminal-audit-timeout.ps1");
            await File.WriteAllTextAsync(scriptPath, "Start-Sleep -Seconds 30");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 1
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_terminal_audit_failure",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs));

            var result = await pool.RunTaskAsync(profile, task).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutDuringFailureAuditWinsOverFailedOutcome()
    {
        var root = CreateTempRoot();
        var auditMutationCount = 0;
        using var timeoutSignal = new CancellationTokenSource();
        try
        {
            var fs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (string.Equals(
                                path,
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            Interlocked.Increment(ref auditMutationCount) == 2)
                        {
                            timeoutSignal.Cancel();
                        }

                        return Task.CompletedTask;
                    }
                });
            var scriptPath = Path.Combine(root, "fake-worker-timeout-during-failure-audit.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 7");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_during_failure_audit",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var result = await pool.RunTaskAsync(profile, task).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationDuringRejectedProposalAuditWinsOverFailure()
    {
        var root = CreateTempRoot();
        var auditMutationCount = 0;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var fs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (string.Equals(
                                path,
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            Interlocked.Increment(ref auditMutationCount) == 2)
                        {
                            cancellation.Cancel();
                        }

                        return Task.CompletedTask;
                    }
                });
            var scriptPath = Path.Combine(root, "fake-worker-cancel-during-rejected-audit.ps1");
            await File.WriteAllTextAsync(
                scriptPath,
                "Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Value '{bad json' -Encoding UTF8 -NoNewline");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_during_rejected_audit",
                    TimeoutSeconds = profile.TimeoutSeconds
                });
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pool.RunTaskAsync(profile, task, cancellation.Token)
                    .WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_CancellationAtDuplicateReservationDecisionWinsOverRejection()
    {
        var root = CreateTempRoot();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var fs = CreateFileSystem(root);
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_cancel_duplicate_reservation",
                    TimeoutSeconds = 60
                });
            var taskPath = GmWorkerBridgePool.GetTaskPacketPath(task.TaskId);
            const string priorTaskJson = "{\"prior\":true}";
            await fs.WriteFileAtomicAsync(taskPath, priorTaskJson);
            var scriptPath = Path.Combine(root, "fake-worker-cancel-duplicate-reservation.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeTerminalFailureDecisionAsync = () =>
                {
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pool.RunTaskAsync(profile, task, cancellation.Token));

            Assert.Equal(priorTaskJson, await fs.ReadFileAsync(taskPath));
            Assert.Empty(await new GmWorkerAuditLog(fs).ReadEventsAsync());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RunTaskAsync_TimeoutAtStaleDispatchDecisionWinsOverSessionReplacement()
    {
        var root = CreateTempRoot();
        using var timeoutSignal = new CancellationTokenSource();
        try
        {
            var fs = CreateFileSystem(root);
            var task = await MaterializeTaskContextAsync(
                fs,
                GmWorkerBridgeTestFixtures.AnalysisTask() with
                {
                    TaskId = "worker_task_timeout_stale_dispatch",
                    TimeoutSeconds = 60
                });
            var scriptPath = Path.Combine(root, "fake-worker-timeout-stale-dispatch.ps1");
            await File.WriteAllTextAsync(scriptPath, "exit 0");
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 60
            };
            var terminalDecisionReached = false;
            var hooks = new GmWorkerBridgePoolHooks
            {
                TimeoutSignal = timeoutSignal.Token,
                BeforeTaskDispatchAuditAsync = async () =>
                {
                    await SessionReplacementTestHarness.RotateGenerationAsync(fs);
                },
                BeforeTerminalFailureDecisionAsync = () =>
                {
                    terminalDecisionReached = true;
                    timeoutSignal.Cancel();
                    return Task.CompletedTask;
                }
            };
            var pool = new GmWorkerBridgePool(
                fs,
                new GmWorkerProposalStore(fs),
                new GmWorkerAuditLog(fs),
                hooks);

            var result = await pool.RunTaskAsync(profile, task);

            Assert.True(terminalDecisionReached);
            Assert.True(result.TimedOut);
            Assert.Equal(WorkerBridgeState.TimedOut, result.Status.State);
            Assert.False(result.SessionReplaced);
            Assert.Contains("timed out", result.Status.LastError!, StringComparison.OrdinalIgnoreCase);
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
                TimeoutSeconds = 5
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

    private static FileSystemManager CreateFileSystem(
        string root,
        FileSystemManagerHooks? hooks = null)
    {
        var fs = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        fs.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(fs.SessionGenerationPath)!);
        File.WriteAllText(
            fs.SessionGenerationPath,
            $$"""{"SchemaVersion":1,"GenerationId":"{{GmWorkerBridgeTestFixtures.SessionGeneration}}"}""");
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\""
        });
        if (process == null)
            throw new InvalidOperationException("Failed to start junction helper.");

        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create test junction: exit code {process.ExitCode}.");
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

    private static async Task<string> ReadFileWhenAvailableAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(path))
                    return await File.ReadAllTextAsync(path);
            }
            catch (IOException)
            {
                // The producer has created the synchronization file but has not closed it yet.
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Timed out waiting for readable test synchronization file: {path}");
        return string.Empty;
    }

    private static async Task<string> WriteValidEmptyProposalWorkerScriptAsync(
        string root,
        string proposalId)
    {
        var scriptPath = Path.Combine(root, $"fake-worker-{proposalId}.ps1");
        await File.WriteAllTextAsync(scriptPath, $$"""
            $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $proposal = [ordered]@{
                schemaVersion = 1
                proposalId = '{{proposalId}}'
                taskId = $task.taskId
                workerId = $task.workerId
                status = 'completed'
                summary = 'Worker completed before proposal publication was released.'
                changedFiles = @()
                findings = @()
                draftText = $null
                selfCheck = [ordered]@{
                    scopeReviewed = $true
                    validationExpectedToPass = $true
                    notes = @('publication race fixture')
                }
                createdAtUtc = '2026-07-26T00:00:00Z'
            }
            $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            exit 0
            """);
        return scriptPath;
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

    private sealed class AsyncTestBarrier(int participantCount)
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        internal Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref _arrived) == participantCount)
                _release.SetResult();
            return _release.Task;
        }
    }

    private sealed class ControlledProcessTreeFactory : IGmWorkerProcessTreeFactory
    {
        private readonly TaskCompletionSource _firstStopStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstTree =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attachedTrees;

        internal Task FirstStopStarted => _firstStopStarted.Task;

        public IGmWorkerProcessTree Attach(Process process)
        {
            var blockCompletion = Interlocked.Increment(ref _attachedTrees) == 1;
            return new ControlledProcessTree(
                process,
                blockCompletion ? _firstStopStarted : null,
                blockCompletion ? _releaseFirstTree.Task : Task.CompletedTask);
        }

        internal void ReleaseFirstTree() => _releaseFirstTree.TrySetResult();
    }

    private sealed class ControlledProcessTree(
        Process process,
        TaskCompletionSource? stopStarted,
        Task completionBarrier) : IGmWorkerProcessTree
    {
        private readonly object _sync = new();
        private Task? _stopTask;

        public Task StopAndWaitAsync()
        {
            lock (_sync)
                return _stopTask ??= StopCoreAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task StopCoreAsync()
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            stopStarted?.TrySetResult();
            await completionBarrier;
        }
    }

    private sealed class RootLivenessProcessTreeFactory : IGmWorkerProcessTreeFactory
    {
        private int _rootWasAliveWhenStopBegan;

        internal bool RootWasAliveWhenStopBegan =>
            Volatile.Read(ref _rootWasAliveWhenStopBegan) != 0;

        public IGmWorkerProcessTree Attach(Process process) =>
            new RootLivenessProcessTree(
                process,
                () => Interlocked.Exchange(ref _rootWasAliveWhenStopBegan, 1));
    }

    private sealed class RootLivenessProcessTree(
        Process process,
        Action recordLiveRoot) : IGmWorkerProcessTree
    {
        private readonly object _sync = new();
        private Task? _stopTask;

        public Task StopAndWaitAsync()
        {
            lock (_sync)
                return _stopTask ??= StopCoreAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task StopCoreAsync()
        {
            if (!process.HasExited)
            {
                recordLiveRoot();
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
    }

    private sealed class TerminalSignalProcessTreeFactory(Action signal) : IGmWorkerProcessTreeFactory
    {
        public IGmWorkerProcessTree Attach(Process process) =>
            new TerminalSignalProcessTree(process, signal);
    }

    private sealed class TerminalSignalProcessTree(
        Process process,
        Action signal) : IGmWorkerProcessTree
    {
        private readonly object _sync = new();
        private Task? _stopTask;

        public Task StopAndWaitAsync()
        {
            lock (_sync)
                return _stopTask ??= StopCoreAsync();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task StopCoreAsync()
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            signal();
        }
    }

    private sealed class ThrowingProcessTreeFactory : IGmWorkerProcessTreeFactory
    {
        private int _disposeCalled;
        private int _processId;

        internal bool DisposeCalled => Volatile.Read(ref _disposeCalled) != 0;
        internal bool HasLiveProcess
        {
            get
            {
                try
                {
                    using var process = Process.GetProcessById(Volatile.Read(ref _processId));
                    return !process.HasExited;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }

        public IGmWorkerProcessTree Attach(Process process)
        {
            Volatile.Write(ref _processId, process.Id);
            return new ThrowingProcessTree(() => Interlocked.Exchange(ref _disposeCalled, 1));
        }

        internal async Task ForceCleanupAsync()
        {
            try
            {
                using var process = Process.GetProcessById(Volatile.Read(ref _processId));
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (ArgumentException)
            {
                // The process already exited.
            }
        }
    }

    private sealed class ThrowingProcessTree(Action recordDispose) : IGmWorkerProcessTree
    {
        public Task StopAndWaitAsync() =>
            Task.FromException(new IOException("Synthetic process-tree confirmation failure."));

        public ValueTask DisposeAsync()
        {
            recordDispose();
            return ValueTask.CompletedTask;
        }
    }
}
