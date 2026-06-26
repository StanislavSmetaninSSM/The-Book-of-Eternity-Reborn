using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerLiveSmokeTests
{
    [Fact]
    public async Task FakeValidationRepairWorker_ReturnsProposalThatApplyGateCanAccept()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync("game_state/world/weather.json", "{\"before\":true}");
            var scriptPath = Path.Combine(root, "fake-validation-repair-worker.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposalId = 'worker_proposal_live_repair'
                $contentRef = 'worker_proposals/' + $proposalId + '/game_state/world/weather.json'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
                Set-Content -Path $contentPath -Value '{"after":true}' -Encoding UTF8 -NoNewline
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = $proposalId
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Fake repair updated weather.'
                    changedFiles = @([ordered]@{
                        path = 'game_state/world/weather.json'
                        changeKind = 'replace'
                        beforeSha256 = 'example'
                        afterSha256 = 'example-after'
                        contentRef = $contentRef
                    })
                    findings = @()
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('fake repair smoke test')
                    }
                    createdAtUtc = '2026-06-20T00:20:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with { TimeoutSeconds = profile.TimeoutSeconds };
            var audit = new GmWorkerAuditLog(fs);
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit);

            var run = await pool.RunTaskAsync(profile, task);
            Assert.True(
                run.Status.State == WorkerBridgeState.Stopped,
                $"{run.Status.LastError}{Environment.NewLine}STDOUT: {run.StandardOutput}{Environment.NewLine}STDERR: {run.StandardError}");
            Assert.NotNull(run.Proposal);
            var gate = new GmWorkerApplyGate(
                fs,
                () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]),
                audit);
            var decision = await gate.ApplyAsync(run.Proposal!, task, profile);
            var events = await audit.ReadEventsAsync();

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            Assert.Equal("{\"after\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
            Assert.Contains(events, e => e.EventType == "task-dispatched");
            Assert.Contains(events, e => e.EventType == "proposal-received");
            Assert.Contains(events, e => e.EventType == "proposal-applied");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task FakeNarrativeWorker_ReturnsDraftWithoutChangingCanonicalState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync("game_state/world/current_location.json", "{\"before\":true}");
            var scriptPath = Path.Combine(root, "fake-narrative-worker.ps1");
            await File.WriteAllTextAsync(scriptPath, """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_live_narrative'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Fake narrative draft is ready for main GM review.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'continuity-note'
                        message = 'Draft does not resolve player action.'
                    })
                    draftText = 'A draft visible only to the main GM.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('proposal-only narrative smoke test')
                    }
                    createdAtUtc = '2026-06-20T00:25:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            };
            var task = GmWorkerBridgeTestFixtures.NarrativeDraftTask() with { TimeoutSeconds = profile.TimeoutSeconds };
            var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), new GmWorkerAuditLog(fs));

            var run = await pool.RunTaskAsync(profile, task);
            var stored = await new GmWorkerProposalStore(fs).ReadProposalAsync("worker_proposal_live_narrative");

            Assert.True(
                run.Status.State == WorkerBridgeState.Stopped,
                $"{run.Status.LastError}{Environment.NewLine}STDOUT: {run.StandardOutput}{Environment.NewLine}STDERR: {run.StandardError}");
            Assert.NotNull(run.Proposal);
            Assert.NotNull(stored);
            Assert.Equal("A draft visible only to the main GM.", stored!.DraftText);
            Assert.Empty(stored.ChangedFiles);
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync("game_state/world/current_location.json"));
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
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-live-" + Guid.NewGuid().ToString("N"));
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
