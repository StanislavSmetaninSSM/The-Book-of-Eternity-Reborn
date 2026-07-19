using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerValidationRepairDelegatorTests
{
    private const string WeatherPath = "game_state/world/weather.json";
    private const string ReadyPath = "game_state/control/validation_repair_ready.json";
    private const string LatestTaskPath = "game_state/control/gm_worker_latest_validation_repair_task.json";

    [Fact]
    public async Task TryRunAsync_NoEnabledValidationRepairWorker_LeavesLegacyRepairLoopWaiting()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
            var delegator = CreateDelegator(fs);

            var result = await delegator.TryRunAsync(
                [],
                [MissingWeatherDescriptionIssue()],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 1);

            Assert.Equal(GmWorkerValidationRepairOutcome.SkippedNoWorker, result.Outcome);
            Assert.False(result.ReadySignalCreated);
            Assert.False(fs.FileExists(ReadyPath));
            Assert.False(fs.FileExists(LatestTaskPath));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync(WeatherPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TryRunAsync_FakeWorkerProposalAccepted_AppliesRepairAndCreatesReadySignal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
            var audit = new GmWorkerAuditLog(fs);
            var profile = BuildProfile(root, "fake-validation-repair-success.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposalId = 'worker_proposal_live_delegator_success'
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
                        beforeSha256 = 'example-before'
                        afterSha256 = 'example-after'
                        contentRef = $contentRef
                    })
                    findings = @()
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('delegator success')
                    }
                    createdAtUtc = '2026-06-20T01:00:05Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var delegator = CreateDelegator(fs, audit: audit);

            var result = await delegator.TryRunAsync(
                [profile],
                [MissingWeatherDescriptionIssue()],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 2);
            var readyJson = await fs.ReadFileAsync(ReadyPath);
            var latestTaskJson = await fs.ReadFileAsync(LatestTaskPath);
            var events = await audit.ReadEventsAsync();

            Assert.Equal(GmWorkerValidationRepairOutcome.Applied, result.Outcome);
            Assert.Equal(ApplyGateResult.Accepted, result.ApplyDecision?.Result);
            Assert.True(result.ReadySignalCreated);
            Assert.Equal("{\"after\":true}", await fs.ReadFileAsync(WeatherPath));
            Assert.Contains("\"sessionId\": \"test-session\"", readyJson);
            Assert.Contains("\"requestId\": \"test-request\"", readyJson);
            Assert.Contains("\"turnNumber\": 12", readyJson);
            Assert.Contains("\"taskId\": \"worker_task_validation_repair_0002\"", latestTaskJson);
            Assert.Contains(events, e => e.EventType == "task-dispatched");
            Assert.Contains(events, e => e.EventType == "proposal-received");
            Assert.Contains(events, e => e.EventType == "proposal-applied");
            Assert.Contains(events, e => e.EventType == "validation-repair-ready-created");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TryRunAsync_WorkerFailure_FallsBackWithoutReadySignal()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
            var profile = BuildProfile(root, "fake-validation-repair-failure.ps1", "exit 7");
            var delegator = CreateDelegator(fs);

            var result = await delegator.TryRunAsync(
                [profile],
                [MissingWeatherDescriptionIssue()],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 3);

            Assert.Equal(GmWorkerValidationRepairOutcome.WorkerFailed, result.Outcome);
            Assert.False(result.ReadySignalCreated);
            Assert.False(fs.FileExists(ReadyPath));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync(WeatherPath));
            Assert.Contains("exited with code 7", result.FallbackReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TryRunAsync_CoordinateBearingActorIssue_HashesCanonicalContextFile()
    {
        var root = CreateTempRoot();
        try
        {
            const string npcPath = "game_state/npcs/npc_core.json";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(npcPath, "{\"UpdateNPCs\":[],\"NPCsInScene\":[]}");
            var profile = BuildProfile(root, "fake-coordinate-repair-worker.ps1", "exit 7");
            var delegator = CreateDelegator(fs);
            var issue = new ValidationIssue(
                $"{npcPath}.NPCsInScene[0].materialization.sections.inventory",
                IssueSeverity.Error,
                "Первичная материализация не объясняет секцию inventory.",
                code: "actor_materialization_section_missing",
                actor: "mortal_npc:npc_coordinate_target",
                section: "inventory");

            var result = await delegator.TryRunAsync(
                [profile],
                [issue],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 31);

            Assert.Equal(GmWorkerValidationRepairOutcome.WorkerFailed, result.Outcome);
            var context = Assert.Single(Assert.IsType<WorkerTaskPacket>(result.Task).ContextFiles);
            Assert.Equal(npcPath, context.Path);
            Assert.Matches("^[0-9a-f]{64}$", context.Sha256);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TryRunAsync_AfterlifeActorRepair_BuildsRealmBoundTaskForCanonicalSource()
    {
        var root = CreateTempRoot();
        try
        {
            const string guardianPath = "game_state/meta/guardians.json";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(
                "game_state/meta/soul_state.json",
                "{\"currentRealm\":\"Chaos Sea\"}");
            await fs.WriteFileAtomicAsync(
                guardianPath,
                "{\"schemaVersion\":1,\"guardians\":[{\"guardianId\":\"guardian_repair_target\",\"musings\":[]}]}");
            var profile = BuildProfile(root, "fake-afterlife-repair-worker.ps1", "exit 7");
            var delegator = CreateDelegator(fs);
            var issue = new ValidationIssue(
                $"{guardianPath}.guardians[0]",
                IssueSeverity.Error,
                "Первичная материализация Хранителя не инициализировала память.",
                code: "afterlife_actor_materialization_memory_missing",
                actor: "guardian:guardian_repair_target",
                section: "ActorMemory");

            var result = await delegator.TryRunAsync(
                [profile],
                [issue],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 32);

            Assert.Equal(GmWorkerValidationRepairOutcome.WorkerFailed, result.Outcome);
            var task = Assert.IsType<WorkerTaskPacket>(result.Task);
            var afterlife = Assert.IsType<WorkerAfterlifeTaskContract>(task.AfterlifeContract);
            Assert.Equal(WorkerAfterlifeRealmGate.ChaosSea, afterlife.RealmGate);
            Assert.Equal("Chaos Sea", afterlife.CurrentRealm);
            Assert.Equal([guardianPath], afterlife.AllowedAfterlifeSurfaces);
            var context = Assert.Single(task.ContextFiles);
            Assert.Equal(guardianPath, context.Path);
            Assert.Matches("^[0-9a-f]{64}$", context.Sha256);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task TryRunAsync_ApplyGateValidationFails_RollsBackAndLeavesLegacyRepairLoopWaiting()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
            var profile = BuildProfile(root, "fake-validation-repair-validation-fails.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposalId = 'worker_proposal_live_delegator_validation_fails'
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
                    summary = 'Fake repair still fails validation.'
                    changedFiles = @([ordered]@{
                        path = 'game_state/world/weather.json'
                        changeKind = 'replace'
                        beforeSha256 = 'example-before'
                        afterSha256 = 'example-after'
                        contentRef = $contentRef
                    })
                    findings = @()
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $false
                        notes = @('delegator validation failure')
                    }
                    createdAtUtc = '2026-06-20T01:00:05Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var delegator = CreateDelegator(
                fs,
                validate: () => Task.FromResult<IReadOnlyList<ValidationIssue>>(
                    [new ValidationIssue(WeatherPath, IssueSeverity.Error, "Weather is still invalid.")]));

            var result = await delegator.TryRunAsync(
                [profile],
                [MissingWeatherDescriptionIssue()],
                TurnReference(),
                "2026-06-20T01:00:00Z",
                attempt: 4);

            Assert.Equal(GmWorkerValidationRepairOutcome.ApplyRejected, result.Outcome);
            Assert.Equal(ApplyGateResult.ValidationFailed, result.ApplyDecision?.Result);
            Assert.False(result.ReadySignalCreated);
            Assert.False(fs.FileExists(ReadyPath));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync(WeatherPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static GmWorkerValidationRepairDelegator CreateDelegator(
        FileSystemManager fs,
        Func<Task<IReadOnlyList<ValidationIssue>>>? validate = null,
        GmWorkerAuditLog? audit = null)
    {
        audit ??= new GmWorkerAuditLog(fs);
        return new GmWorkerValidationRepairDelegator(
            fs,
            new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit),
            new GmWorkerApplyGate(fs, validate ?? (() => Task.FromResult<IReadOnlyList<ValidationIssue>>([])), audit),
            audit);
    }

    private static WorkerBridgeProfile BuildProfile(string root, string fileName, string script)
    {
        var scriptPath = Path.Combine(root, fileName);
        File.WriteAllText(scriptPath, script);
        return GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            TimeoutSeconds = 10
        };
    }

    private static ValidationIssue MissingWeatherDescriptionIssue() =>
        new(
            WeatherPath,
            IssueSeverity.Error,
            "normalizedWeatherState.description is required.",
            code: "normalized_weather_missing_description",
            repairHint: "Add a player-facing weather description.");

    private static WorkerTurnReference TurnReference() => new()
    {
        SessionId = "test-session",
        RequestId = "test-request",
        TurnNumber = 12
    };

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-delegator-" + Guid.NewGuid().ToString("N"));
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
