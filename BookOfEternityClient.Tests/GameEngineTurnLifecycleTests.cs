using System.Security.Cryptography;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Reflection;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineTurnLifecycleTests : IDisposable
{
    private sealed class PendingTurnSnapshotManifestPayload
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GameEngineTurnLifecycleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-gameengine-turnlifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_MortalPreTurnAndMortalCurrent_IsReadable()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Mortal World",
            out var failureDescription);

        Assert.False(invalid);
        Assert.Equal(string.Empty, failureDescription);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_AfterlifeCurrentRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Chaos Sea",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("currentRealm", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_UnreadablePreTurnRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            null,
            "Mortal World",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("pre-turn mortal realm authority", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckLevelUpAsync_DoesNotAwardAlreadyProcessedLevelAfterEngineRestart()
    {
        await WriteJsonAsync("game_state/player/experience.json", new
        {
            playerLevel = 2,
            level = 2,
            currentExperience = 49,
            experience = 49,
            totalExperience = 149,
            experienceForNextLevel = 150
        });
        await WriteJsonAsync("game_state/player/stat_points.json", new
        {
            unspentStatPoints = 0
        });
        await WriteJsonAsync("game_state/misc/characteristics.json", new
        {
            strength = 2,
            dexterity = 4,
            constitution = 2,
            intelligence = 4,
            wisdom = 1,
            faith = 1,
            attractiveness = 1,
            trade = 1,
            persuasion = 2,
            perception = 4,
            luck = 2,
            speed = 1
        });
        await WriteJsonAsync("game_state/player/computed_characteristics.json", new
        {
            playerLevel = 1,
            unspentStatPoints = 0,
            characteristics = new { strength = 2 }
        });

        var firstLevelUpInput = new QueuedConsoleInputSource(
            Enumerable.Repeat(Key(ConsoleKey.RightArrow), 5).Append(Key(ConsoleKey.Enter)));
        var firstEngine = CreateGameEngine(firstLevelUpInput);
        await InvokePrivateTaskAsync(firstEngine, "CheckLevelUpAsync");

        var afterFirstBaseStats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        Assert.Equal(7, afterFirstBaseStats["strength"]!.GetValue<int>());
        var afterFirstComputed = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/computed_characteristics.json"))!)!.AsObject();
        Assert.Equal(7, afterFirstComputed["characteristics"]!["strength"]!.GetValue<int>());

        var restartedInput = new QueuedConsoleInputSource(
            Enumerable.Repeat(Key(ConsoleKey.RightArrow), 5).Append(Key(ConsoleKey.Enter)));
        var restartedEngine = CreateGameEngine(restartedInput);
        await InvokePrivateTaskAsync(restartedEngine, "CheckLevelUpAsync");

        var afterRestartBaseStats = JsonNode.Parse((await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!)!.AsObject();
        Assert.Equal(7, afterRestartBaseStats["strength"]!.GetValue<int>());
        var statPoints = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/stat_points.json"))!)!.AsObject();
        Assert.Equal(0, statPoints["unspentStatPoints"]!.GetValue<int>());
        var afterRestartComputed = JsonNode.Parse((await _fs.ReadFileAsync("game_state/player/computed_characteristics.json"))!)!.AsObject();
        Assert.Equal(7, afterRestartComputed["characteristics"]!["strength"]!.GetValue<int>());
    }

    [Fact]
    public async Task HandleInvalidTriggerLifeEndRuntimeFailure_DeletesSignalAndWritesErrorLog()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        var exception = new GameEngine.TriggerLifeEndRuntimeContextException(
            "Canonical TriggerLifeEnd runtime flow requires mortal pre-turn realm authority.");

        GameEngine.HandleInvalidTriggerLifeEndRuntimeFailure(_fs, exception);

        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));

        var logPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.Contains("TriggerLifeEndRuntimeContextException", log, StringComparison.Ordinal);
        Assert.Contains("mortal pre-turn realm authority", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowTurnErrorMessageAsync_PublishesAgentConsoleTimeoutRecoveryScreen()
    {
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);
        await _fs.WriteFileAtomicAsync("ready/turn_error.json", """
        {
          "sessionId": "session-timeout",
          "requestId": "request-timeout",
          "turnNumber": 15,
          "timestamp": "2026-06-30T23:43:36Z",
          "status": "error",
          "error": "Timeout after 900s"
        }
        """);

        var method = typeof(GameEngine).GetMethod(
            "ShowTurnErrorMessageAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(engine, new object[] { "ready/turn_error.json" })!);

        var snapshot = await WaitForAgentConsoleSnapshotAsync(store, "gm-turn-error");

        Assert.Equal(AgentConsoleMode.Error, snapshot.Mode);
        Assert.True(snapshot.AwaitingInput);
        Assert.Equal(AgentConsoleInputKind.Key, snapshot.InputKind);
        Assert.Contains("Timeout after 900s", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Contains("Действие не было применено", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Contains(snapshot.Diagnostics, diagnostic =>
            string.Equals(diagnostic.Code, "gm-turn-error", StringComparison.OrdinalIgnoreCase));

        input.EnqueueKey(Key(ConsoleKey.Enter));
        await task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CollectAcceptedTurnRawStateIssuesAsync_DirectNpcCoreMutation_IsRejectedBeforeNormalization()
    {
        const string npcCorePath = "game_state/npcs/npc_core.json";
        const string sessionId = "session-npc-core-raw-lifecycle";
        const string requestId = "request-npc-core-raw-lifecycle";
        const int turnNumber = 12;

        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: turnNumber - 1,
            characterDescription: "Архивист пограничного города.",
            worldDescription: "Город архивов и сигнальных башен.",
            startingCircumstances: "Наставник предлагает первый урок чтения печатей.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-22T00:00:00Z"));
        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var preTurnRoot = files[npcCorePath].DeepClone().AsObject();
        var currentRoot = preTurnRoot.DeepClone().AsObject();
        var currentActor = Assert.Single(currentRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        currentActor["worldview"] = "Несанкционированная подмена убеждений.";
        await _fs.WriteFileAtomicAsync(npcCorePath, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{npcCorePath}",
            preTurnRoot.ToJsonString());
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, npcCorePath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            playerAction = "game-engine-turn-lifecycle-test"
        });
        var engine = CreateGameEngine();

        var issues = await InvokePrivateAsync<List<ValidationIssue>>(
            engine,
            "CollectAcceptedTurnRawStateIssuesAsync");

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.FilePath == $"{npcCorePath}.NPCsInScene[0]");
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_WithEnabledWorkerProfile_WritesWorkerTaskAndAudit()
    {
        const string sessionId = "session-worker-repair";
        const string requestId = "request-worker-repair";
        const int turnNumber = 3;
        const string trackedPath = "game_state/world/weather.json";

        await _fs.WriteFileAtomicAsync(trackedPath, "{\"before\":true}");
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", "{\"before\":true}");
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("game_state/control/validation_repair_request.json", new { stale = true });
        await WriteJsonAsync("game_state/control/validation_repair_ready.json", new { stale = true });
        await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new { stale = true });

        var scriptPath = Path.Combine(_rootPath, "fake-validation-repair-worker.ps1");
        await File.WriteAllTextAsync(scriptPath, """
            $control = Join-Path $env:BOE_WORKER_SESSION_PATH 'game_state/control'
            if (Test-Path (Join-Path $control 'validation_repair_request.json')) { exit 71 }
            if (Test-Path (Join-Path $control 'validation_repair_ready.json')) { exit 72 }
            if (Test-Path (Join-Path $control 'gm_validation_repair_artifact_stall_report.json')) { exit 73 }
            exit 7
            """, Encoding.UTF8);
        var engine = CreateGameEngine(configureSettings: settings =>
        {
            settings.GmWorkerBridgeProfiles.Add(GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            });
        });
        var issue = new ValidationIssue(
            "game_state/world/weather.json",
            IssueSeverity.Error,
            "normalizedWeatherState.description is required.",
            code: "normalized_weather_missing_description");

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "state validation", new List<ValidationIssue> { issue }, 1 })!);

        await task;

        Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));
        Assert.False(_fs.FileExists("game_state/control/gm_validation_repair_artifact_stall_report.json"));
        var workerTaskJson = await _fs.ReadFileAsync("game_state/control/gm_worker_latest_validation_repair_task.json");
        Assert.False(string.IsNullOrWhiteSpace(workerTaskJson));
        var workerTask = GmWorkerJson.Deserialize<WorkerTaskPacket>(workerTaskJson!);
        Assert.NotNull(workerTask);
        Assert.Equal("validation_repair_codex", workerTask!.WorkerId);
        Assert.Equal(WorkerTaskType.ValidationRepair, workerTask.TaskType);
        Assert.Contains("game_state/world/weather.json", workerTask.AllowedProposalPaths);

        var audit = new GmWorkerAuditLog(_fs);
        var events = await audit.ReadEventsAsync();
        var dispatch = Assert.Single(events, evt => evt.EventType == "task-dispatched");
        Assert.Equal(workerTask.TaskId, dispatch.TaskId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteValidationRepairRequestAsync_WhenWorkerApplies_DoesNotExposeLegacyRepairRequest(
        bool auditPathWritable)
    {
        const string sessionId = "session-worker-repair-applied";
        const string requestId = "request-worker-repair-applied";
        const int turnNumber = 4;
        const string trackedPath = "game_state/world/weather.json";
        const string weatherJson = "{\"tendency\":\"NO_CHANGE\",\"description\":\"Погода не меняется.\"}";

        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("game_state/control/validation_repair_request.json");
        _fs.DeleteFile("game_state/control/validation_repair_ready.json");
        await _fs.WriteFileAtomicAsync(trackedPath, weatherJson);
        var snapshotRoot = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotRoot))
            Directory.Delete(snapshotRoot, recursive: true);
        var trackedPaths = Directory.GetFiles(_fs.GameSessionPath, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_fs.GameSessionPath, path).Replace('\\', '/'))
            .Where(path =>
                (path.StartsWith("game_state/", StringComparison.Ordinal) &&
                 !path.StartsWith("game_state/control/", StringComparison.Ordinal)) ||
                path.StartsWith("lore/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        foreach (var path in trackedPaths)
        {
            var destination = _fs.ResolvePath($"game_state/control/pending_turn_snapshot/{path}");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(_fs.ResolvePath(path), destination, overwrite: true);
        }
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            trackedPaths);
        await WriteJsonAsync("input/turn_request.json", new { sessionId, requestId, turnNumber });
        if (!auditPathWritable)
            Directory.CreateDirectory(_fs.ResolvePath(GmWorkerAuditLog.AuditLogPath));

        var scriptPath = Path.Combine(_rootPath, "fake-validation-repair-worker-applied.ps1");
        await File.WriteAllTextAsync(scriptPath, """
            $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $proposalId = 'worker_proposal_game_engine_applied'
            $contentRef = 'worker_proposals/' + $proposalId + '/game_state/world/weather.json'
            $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
            $canonicalPath = Join-Path $env:BOE_WORKER_SESSION_PATH 'game_state/world/weather.json'
            New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
            Copy-Item -Path $canonicalPath -Destination $contentPath -Force
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try { $afterSha256 = ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($contentPath)))).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose() }
            $proposal = [ordered]@{
                schemaVersion = 1
                proposalId = $proposalId
                taskId = $task.taskId
                workerId = $task.workerId
                status = 'completed'
                summary = 'No-op repair used to verify exclusive worker ownership.'
                changedFiles = @([ordered]@{
                    path = 'game_state/world/weather.json'
                    changeKind = 'replace'
                    beforeSha256 = $task.contextFiles[0].sha256
                    afterSha256 = $afterSha256
                    contentRef = $contentRef
                })
                findings = @()
                selfCheck = [ordered]@{
                    scopeReviewed = $true
                    validationExpectedToPass = $true
                    notes = @('exclusive repair flow')
                }
                createdAtUtc = '2026-06-20T01:00:05Z'
            }
            $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """, Encoding.UTF8);
        var engine = CreateGameEngine(configureSettings: settings =>
        {
            settings.GmWorkerBridgeProfiles.Add(GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
            {
                LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                TimeoutSeconds = 10
            });
        });
        var issue = new ValidationIssue(
            trackedPath,
            IssueSeverity.Error,
            "Synthetic repair request for worker ownership test.",
            code: "normalized_weather_missing_description");

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "state validation", new List<ValidationIssue> { issue }, 1 })!);

        await task;

        var dispatch = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(dispatch);
        Assert.True((bool)dispatch!.GetType().GetProperty("WorkerApplyAccepted")!.GetValue(dispatch)!);
        Assert.True((bool)dispatch.GetType().GetProperty("ReadySignalCreated")!.GetValue(dispatch)!);

        var auditDiagnostic = auditPathWritable
            ? await _fs.ReadFileAsync(GmWorkerAuditLog.AuditLogPath) ?? "<missing audit>"
            : "audit path intentionally unavailable";
        Assert.True(_fs.FileExists("game_state/control/validation_repair_ready.json"), auditDiagnostic);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_WithoutWorkerProfiles_PreservesSingleGmRepairFlow()
    {
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issue = new ValidationIssue(
            "game_state/world/weather.json",
            IssueSeverity.Error,
            "normalizedWeatherState.description is required.",
            code: "normalized_weather_missing_description");

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "state validation", new List<ValidationIssue> { issue }, 1 })!);

        await task;

        Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/gm_worker_latest_validation_repair_task.json"));
        Assert.False(_fs.FileExists(GmWorkerAuditLog.AuditLogPath));
        Assert.False(Directory.Exists(_fs.ResolvePath(GmWorkerBridgePool.TaskRoot)));
        Assert.False(Directory.Exists(_fs.ResolvePath(GmWorkerBridgePool.ProposalInboxRoot)));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_WithAgentConsole_PublishesRepairProgressSnapshot()
    {
        const string sessionId = "session-repair-console";
        const string requestId = "request-repair-console";
        const int turnNumber = 12;
        const string trackedPath = "game_state/world/current_location.json";

        await WriteJsonAsync(trackedPath, new
        {
            locationId = "loc_gate"
        });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new
        {
            locationId = "loc_gate"
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);
        var issue = new ValidationIssue(
            "game_state/world/current_location.json.locationId",
            IssueSeverity.Error,
            "Current location references an unknown location id.",
            code: "current_location_unknown_location_id",
            category: IssueCategory.StateConsistency,
            expected: "known location id",
            actual: "loc_gate");

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", new List<ValidationIssue> { issue }, 2 })!);

        await task;

        var snapshot = store.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("gm-validation-repair", snapshot!.ScreenId);
        Assert.Equal(AgentConsoleMode.Loading, snapshot.Mode);
        Assert.False(snapshot.AwaitingInput);
        Assert.Contains("Ремонт данных", snapshot.Title, StringComparison.Ordinal);
        Assert.Contains("ход 12", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("попытка 2", snapshot.PlainText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current_location_unknown_location_id", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Contains("game_state/world/current_location.json.locationId", snapshot.PlainText, StringComparison.Ordinal);
        Assert.Contains(snapshot.Diagnostics, diagnostic =>
            diagnostic.Severity == AgentConsoleDiagnosticSeverity.Warning &&
            string.Equals(diagnostic.Code, "validation-repair-progress", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizePendingRepairArtifactsAsync_ValidationRepairArtifactStall_WritesTerminalError()
    {
        const string sessionId = "session-repair-stall";
        const string requestId = "request-repair-stall";
        const int turnNumber = 5;
        const string trackedPath = "game_state/world/current_location.json";

        await WriteJsonAsync(trackedPath, new { locationId = "loc_stable" });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new { locationId = "loc_stable" });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            status = "success",
            timestamp = "2026-07-05T02:00:00Z"
        });
        await WriteJsonAsync("game_state/control/validation_repair_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            revalidationAttempt = 2
        });
        await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
        {
            isStalled = true,
            elapsedSeconds = 180,
            bridgeCleanup = new
            {
                reason = "gm_validation_repair_artifact_stall",
                status = "fallback-stopped",
                ok = true
            }
        });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));

        await InvokePrivateTaskAsync(engine, "NormalizePendingRepairArtifactsAsync");

        Assert.True(_fs.FileExists("ready/turn_error.json"));
        var errorJson = await _fs.ReadFileAsync("ready/turn_error.json");
        Assert.NotNull(errorJson);
        using var errorDoc = JsonDocument.Parse(errorJson!);
        var root = errorDoc.RootElement;
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, root.GetProperty("turnNumber").GetInt32());
        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("gm_validation_repair_artifact_stall", root.GetProperty("harnessSource").GetString());
        Assert.Contains("validation repair", root.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
    }

    [Fact]
    public async Task NormalizePendingRepairArtifactsAsync_ValidationRepairArtifactStallWithoutTurnComplete_WritesTerminalError()
    {
        const string sessionId = "session-repair-stall-no-complete";
        const string requestId = "request-repair-stall-no-complete";
        const int turnNumber = 15;
        const string trackedPath = "game_state/world/current_location.json";

        await WriteJsonAsync(trackedPath, new { locationId = "loc_stable" });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new { locationId = "loc_stable" });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("game_state/control/validation_repair_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            revalidationAttempt = 3
        });
        await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
        {
            isStalled = true,
            elapsedSeconds = 180,
            bridgeCleanup = new
            {
                reason = "gm_validation_repair_artifact_stall",
                status = "graceful-stopped",
                ok = true
            }
        });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));

        await InvokePrivateTaskAsync(engine, "NormalizePendingRepairArtifactsAsync");

        Assert.True(_fs.FileExists("ready/turn_error.json"));
        var errorJson = await _fs.ReadFileAsync("ready/turn_error.json");
        Assert.NotNull(errorJson);
        using var errorDoc = JsonDocument.Parse(errorJson!);
        var root = errorDoc.RootElement;
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, root.GetProperty("turnNumber").GetInt32());
        Assert.Equal("gm_validation_repair_artifact_stall", root.GetProperty("harnessSource").GetString());
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ValidationRepairArtifactStall_ExitsWithTerminalError()
    {
        const string sessionId = "session-active-repair-stall";
        const string requestId = "request-active-repair-stall";
        const int turnNumber = 6;
        const string trackedPath = "game_state/meta/guardians.json";

        await WriteJsonAsync(trackedPath, new { guardians = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new { guardians = Array.Empty<object>() });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            status = "success",
            timestamp = "2026-07-05T03:00:00Z"
        });

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians",
                IssueSeverity.Error,
                "Current guardians[] must match kernel authority.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians")
        };

        var repairTask = InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "active validation repair stall test",
            issues,
            1,
            null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_fs.FileExists("game_state/control/validation_repair_request.json") && DateTime.UtcNow < deadline)
            await Task.Delay(100);

        Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
        await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
        {
            isStalled = true,
            elapsedSeconds = 180,
            bridgeCleanup = new
            {
                reason = "gm_validation_repair_artifact_stall",
                status = "fallback-stopped",
                ok = true
            }
        });

        var accepted = await repairTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(accepted);
        Assert.True(_fs.FileExists("ready/turn_error.json"));
        var errorJson = await _fs.ReadFileAsync("ready/turn_error.json");
        Assert.NotNull(errorJson);
        using var errorDoc = JsonDocument.Parse(errorJson!);
        var root = errorDoc.RootElement;
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, root.GetProperty("turnNumber").GetInt32());
        Assert.Equal("gm_validation_repair_artifact_stall", root.GetProperty("harnessSource").GetString());
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ValidationRepairArtifactStallWithoutTurnComplete_ExitsWithTerminalError()
    {
        const string sessionId = "session-active-repair-stall-no-complete";
        const string requestId = "request-active-repair-stall-no-complete";
        const int turnNumber = 16;
        const string trackedPath = "game_state/meta/guardians.json";

        await WriteJsonAsync(trackedPath, new { guardians = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new { guardians = Array.Empty<object>() });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians",
                IssueSeverity.Error,
                "Current guardians[] must match kernel authority.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians")
        };

        var repairTask = InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "active validation repair stall test without turn_complete",
            issues,
            1,
            null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_fs.FileExists("game_state/control/validation_repair_request.json") && DateTime.UtcNow < deadline)
            await Task.Delay(100);

        Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
        await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
        {
            isStalled = true,
            elapsedSeconds = 180,
            bridgeCleanup = new
            {
                reason = "gm_validation_repair_artifact_stall",
                status = "graceful-stopped",
                ok = true
            }
        });

        var accepted = await repairTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(accepted);
        Assert.True(_fs.FileExists("ready/turn_error.json"));
        var errorJson = await _fs.ReadFileAsync("ready/turn_error.json");
        Assert.NotNull(errorJson);
        using var errorDoc = JsonDocument.Parse(errorJson!);
        var root = errorDoc.RootElement;
        Assert.Equal(sessionId, root.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, root.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, root.GetProperty("turnNumber").GetInt32());
        Assert.Equal("gm_validation_repair_artifact_stall", root.GetProperty("harnessSource").GetString());
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_AcceptedRepairReady_WritesAcceptedTrajectoryRecord()
    {
        const string sessionId = "session-repair-ledger";
        const string requestId = "request-repair-ledger";
        const int turnNumber = 7;
        const string trackedPath = "game_state/meta/soul_state.json";

        await WriteJsonAsync(trackedPath, new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issue = new ValidationIssue(
            "game_state/world/current_location.json.locationId",
            IssueSeverity.Error,
            "Current location references an unknown location id.",
            code: "current_location_unknown_location_id");

        var gmRepair = Task.Run(async () =>
        {
            await WaitForValidationRepairRequestContainingAsync(
                "current_location_unknown_location_id",
                TimeSpan.FromSeconds(5));
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-06-27T00:00:00Z",
                note = "Repair accepted by test."
            });
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "обработки хода",
            new List<ValidationIssue> { issue },
            2,
            null);

        await gmRepair;

        Assert.True(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));

        var ledgerJson = await _fs.ReadFileAsync("game_state/control/gm_trajectory_ledger.jsonl");
        Assert.False(string.IsNullOrWhiteSpace(ledgerJson));
        using var document = JsonDocument.Parse(ledgerJson!.Trim());
        var record = document.RootElement;
        Assert.Equal("repair", record.GetProperty("kind").GetString());
        Assert.Equal(sessionId, record.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, record.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, record.GetProperty("turnNumber").GetInt32());
        Assert.Equal("validation_repair", record.GetProperty("mode").GetString());
        var validation = record.GetProperty("validation");
        Assert.Equal("accepted", validation.GetProperty("status").GetString());
        Assert.Equal("обработки хода", validation.GetProperty("source").GetString());
        Assert.Equal("correlated_repair_ready", validation.GetProperty("acceptanceScope").GetString());
        Assert.False(validation.GetProperty("fullCanonicalStateAccepted").GetBoolean());
        Assert.Contains(
            "current_location_unknown_location_id",
            validation.GetProperty("issueKinds")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Contains(
            "mortal_location_transition_repair",
            validation.GetProperty("repairPacketRefs")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Equal(2, record.GetProperty("repair").GetProperty("attempts").GetInt32());
        Assert.Equal("accepted", record.GetProperty("repair").GetProperty("status").GetString());
        Assert.Equal("validation_repair_ready", record.GetProperty("terminal").GetProperty("kind").GetString());
        Assert.Equal(
            "game_state/control/validation_repair_ready.json",
            record.GetProperty("terminal").GetProperty("signalPath").GetString());
        Assert.True(record.GetProperty("rubric").GetProperty("validTurn").GetBoolean());
    }

    [Fact]
    public async Task ReportRejectedRepairReadyAsync_ReturnsExclusiveDispatchStateToCaller()
    {
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issue = new ValidationIssue(
            "game_state/control/validation_repair_ready.json",
            IssueSeverity.Error,
            "Synthetic rejected ready signal.",
            code: "invalid_repair_ready_json");
        var method = typeof(GameEngine).GetMethod(
            "ReportRejectedRepairReadyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[]
            {
                "state validation",
                new List<ValidationIssue> { issue },
                1,
                "invalid_repair_ready_json",
                "Malformed ready signal.",
                "Valid correlated ready signal",
                "Malformed JSON",
                "Publish a fresh correlated ready signal."
            })!);

        await task;

        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);
        var dispatch = result!.GetType().GetField("Item1")?.GetValue(result);
        Assert.NotNull(dispatch);
        Assert.NotNull(dispatch!.GetType().GetProperty("WorkerApplyAccepted"));
        Assert.NotNull(dispatch.GetType().GetProperty("ReadySignalCreated"));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_RejectedRepairReadyWithLostSnapshot_FailsClosedInsteadOfWaitingForever()
    {
        const string sessionId = "session-repair-lost-snapshot";
        const string requestId = "request-repair-lost-snapshot";
        const int turnNumber = 11;
        const string trackedPath = "game_state/meta/soul_state.json";

        await WriteJsonAsync(trackedPath, new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var gmRepair = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!_fs.FileExists("game_state/control/validation_repair_request.json") &&
                   DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
            await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.authority.json", "{}");
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-06-27T00:00:00Z",
                note = "Repair ready becomes unusable because snapshot authority was lost."
            });
        });

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var issue = new ValidationIssue(
            "game_state/world/current_location.json.locationId",
            IssueSeverity.Error,
            "Current location references an unknown location id.",
            code: "current_location_unknown_location_id");

        var repairTask = InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "обработки хода",
            new List<ValidationIssue> { issue },
            2,
            null);

        var completed = await Task.WhenAny(repairTask, Task.Delay(TimeSpan.FromSeconds(8)));

        await gmRepair;
        Assert.Same(repairTask, completed);
        Assert.False(await repairTask);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));

        var reportJson = await _fs.ReadFileAsync("game_state/control/validation_diagnostic_failure_report.json");
        Assert.Contains("Diagnostic-only validation repair request cannot be completed by GM", reportJson);
        Assert.Contains("mismatched_repair_ready_context", reportJson);
    }

    [Fact]
    public async Task ValidateCurrentGameStateOrShowErrorsAsync_AfterRepairClearsValidation_WritesTerminalAcceptedTrajectoryRecord()
    {
        const string sessionId = "session-repair-cleared-ledger";
        const string requestId = "request-repair-cleared-ledger";
        const int turnNumber = 8;
        const string trackedPath = "game_state/meta/soul_state.json";

        await WriteJsonAsync(trackedPath, new
        {
            soulName = "Тихая Искра",
            currentRealm = "Chaos Sea",
            currentIncarnation = 0,
            soulFormDescription = new { invalid = true }
        });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new
        {
            soulName = "Тихая Искра",
            currentRealm = "Chaos Sea",
            currentIncarnation = 0,
            soulFormDescription = new { invalid = true }
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var gmRepair = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!_fs.FileExists("game_state/control/validation_repair_request.json") &&
                   DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
            var repairRequestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
            var repairRequestNode = JsonNode.Parse(repairRequestJson!)!.AsObject();
            repairRequestNode["harnessRepairPackets"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "soul_form_shape_repair"
                }
            };
            await _fs.WriteFileAtomicAsync(
                "game_state/control/validation_repair_request.json",
                repairRequestNode.ToJsonString(SnapshotHashJsonOpts));

            await WriteJsonAsync(trackedPath, new
            {
                soulName = "Тихая Искра",
                currentRealm = "Chaos Sea",
                currentIncarnation = 0,
                soulFormDescription = "Женский силуэт из серебристого пепла."
            });
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-06-27T00:00:00Z",
                note = "Repair cleared by test."
            });
        });

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateCurrentGameStateOrShowErrorsAsync",
            "state validation",
            null,
            null,
            true);

        await gmRepair;

        Assert.True(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));

        var ledgerJson = await _fs.ReadFileAsync("game_state/control/gm_trajectory_ledger.jsonl");
        Assert.False(string.IsNullOrWhiteSpace(ledgerJson));
        var records = ledgerJson!
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToList();

        Assert.Equal(2, records.Count);
        var repairReady = records[0].RootElement;
        Assert.Equal("correlated_repair_ready", repairReady.GetProperty("validation").GetProperty("acceptanceScope").GetString());
        Assert.False(repairReady.GetProperty("validation").GetProperty("fullCanonicalStateAccepted").GetBoolean());
        Assert.Contains(
            "soul_form_shape_repair",
            repairReady.GetProperty("validation").GetProperty("repairPacketRefs")
                .EnumerateArray()
                .Select(item => item.GetString()));

        var cleared = records[1].RootElement;
        Assert.Equal("repair", cleared.GetProperty("kind").GetString());
        Assert.Equal(sessionId, cleared.GetProperty("sessionId").GetString());
        Assert.Equal(requestId, cleared.GetProperty("requestId").GetString());
        Assert.Equal(turnNumber, cleared.GetProperty("turnNumber").GetInt32());
        Assert.Equal("validation_repair", cleared.GetProperty("mode").GetString());
        var validation = cleared.GetProperty("validation");
        Assert.Equal("accepted", validation.GetProperty("status").GetString());
        Assert.Equal("state validation", validation.GetProperty("source").GetString());
        Assert.Equal("full_canonical_state_after_repair", validation.GetProperty("acceptanceScope").GetString());
        Assert.True(validation.GetProperty("fullCanonicalStateAccepted").GetBoolean());
        Assert.Contains(
            "soul_form_description_invalid_shape",
            validation.GetProperty("issueKinds")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Contains(
            "soul_form_shape_repair",
            validation.GetProperty("repairPacketRefs")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Equal(1, cleared.GetProperty("repair").GetProperty("attempts").GetInt32());
        Assert.Equal("cleared", cleared.GetProperty("repair").GetProperty("status").GetString());
        Assert.Equal("validation_repair_cleared", cleared.GetProperty("terminal").GetProperty("kind").GetString());
        Assert.True(cleared.GetProperty("rubric").GetProperty("validTurn").GetBoolean());

        foreach (var record in records)
            record.Dispose();
    }

    [Fact]
    public async Task ValidateCurrentGameStateOrShowErrorsAsync_CanonicalRepairRequiresFreshPlayerFacingOutput()
    {
        const string sessionId = "session-canonical-repair-refreshes-output";
        const string requestId = "request-canonical-repair-refreshes-output";
        const int turnNumber = 35;
        const string trackedPath = "game_state/meta/soul_state.json";

        await WriteJsonAsync(trackedPath, new
        {
            soulName = "Искра Испытаний",
            currentRealm = "Chaos Sea",
            currentIncarnation = 0,
            soulFormDescription = new { invalid = true }
        });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{trackedPath}", new
        {
            soulName = "Искра Испытаний",
            currentRealm = "Chaos Sea",
            currentIncarnation = 0,
            soulFormDescription = new { invalid = true }
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "До ремонта canonical state: Средоточие возвращает 1 ОД, теперь 3/6.",
            timestamp = "2026-07-04T05:18:22Z"
        });
        await WriteJsonAsync("output/debug_logs.json", new
        {
            timestamp = "2026-07-04T05:18:22Z",
            gm_thoughts_markdown = "## Охват NPC-анализа\nРежим: Guardian-centric\nРелевантные акторы: Иларион Архивный Свет\nПочему они релевантны: он ведёт учебный духовный обмен.\nАкторы вне охвата: нет\nПочему они вне охвата: все видимые акторы учтены.\n\n## Размышления акторов\n### Иларион Архивный Свет\n- Текущая локация: Архив Лучистых Тишин\n- Ситуация: учебный духовный обмен\n- Мысли: проверить темп восстановления\n- Действия: удерживает безопасное давление"
        });
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/narrative_response.json"),
            DateTime.UtcNow.AddMinutes(-10));

        string[]? outputRepairTargetFiles = null;
        var gmRepair = Task.Run(async () =>
        {
            var firstRequest = await WaitForValidationRepairRequestContainingAsync(
                "soul_form_description_invalid_shape",
                TimeSpan.FromSeconds(5));
            Assert.Contains("game_state/meta/soul_state.json", firstRequest, StringComparison.OrdinalIgnoreCase);

            var prematureOutputWrittenAtUtc = DateTime.UtcNow;
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Ответ переписан после запроса, но до фактического ремонта canonical state: Средоточие возвращает 1 ОД, теперь 3/6.",
                timestamp = "2026-07-04T05:18:45Z"
            });
            File.SetLastWriteTimeUtc(
                _fs.ResolvePath("output/narrative_response.json"),
                prematureOutputWrittenAtUtc);

            await WriteJsonAsync(trackedPath, new
            {
                soulName = "Искра Испытаний",
                currentRealm = "Chaos Sea",
                currentIncarnation = 0,
                soulFormDescription = "Женский силуэт из серебристого пепла."
            });
            var canonicalStateWrittenAtUtc = prematureOutputWrittenAtUtc.AddSeconds(2);
            File.SetLastWriteTimeUtc(
                _fs.ResolvePath(trackedPath),
                canonicalStateWrittenAtUtc);
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-07-04T05:19:00Z",
                note = "Canonical state repaired, but player-facing output intentionally left stale."
            });

            var secondRequest = await WaitForValidationRepairRequestContainingAsync(
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                TimeSpan.FromSeconds(5));
            Assert.Contains("output/narrative_response.json", secondRequest, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("accepted_turn_output_artifact_repair", secondRequest, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonical state repair", secondRequest, StringComparison.OrdinalIgnoreCase);
            using (var repairDocument = JsonDocument.Parse(secondRequest))
            {
                var packet = Assert.Single(
                    repairDocument.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
                outputRepairTargetFiles = packet
                    .GetProperty("targetFiles")
                    .EnumerateArray()
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray();
            }

            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "После первого ремонта canonical state: Средоточие не восстановило ОД, теперь 2/6.",
                timestamp = "2026-07-04T05:20:00Z"
            });
            var firstRefreshedOutputWrittenAtUtc = canonicalStateWrittenAtUtc.AddSeconds(2);
            File.SetLastWriteTimeUtc(
                _fs.ResolvePath("output/narrative_response.json"),
                firstRefreshedOutputWrittenAtUtc);
            await WriteJsonAsync(trackedPath, new
            {
                soulName = "Искра Испытаний",
                currentRealm = "Chaos Sea",
                currentIncarnation = 0,
                soulFormDescription = "Женский силуэт из серебристого пепла с новой светлой отметиной."
            });
            var secondCanonicalStateWrittenAtUtc = canonicalStateWrittenAtUtc.AddSeconds(4);
            File.SetLastWriteTimeUtc(
                _fs.ResolvePath(trackedPath),
                secondCanonicalStateWrittenAtUtc);
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-07-04T05:20:05Z",
                note = "Player-facing output refreshed, then canonical state was rewritten again."
            });

            var thirdRequest = await WaitForUpdatedValidationRepairRequestContainingAsync(
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                secondRequest,
                TimeSpan.FromSeconds(5));
            Assert.Contains("output/narrative_response.json", thirdRequest, StringComparison.OrdinalIgnoreCase);

            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "После окончательного canonical state: Средоточие не восстановило ОД, теперь 2/6.",
                timestamp = "2026-07-04T05:20:10Z"
            });
            File.SetLastWriteTimeUtc(
                _fs.ResolvePath("output/narrative_response.json"),
                secondCanonicalStateWrittenAtUtc.AddSeconds(2));
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-07-04T05:20:15Z",
                note = "Player-facing output refreshed after the latest canonical rewrite."
            });
        });

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateCurrentGameStateOrShowErrorsAsync",
            "state validation",
            null,
            null,
            true);

        await gmRepair;

        Assert.True(accepted);
        Assert.NotNull(outputRepairTargetFiles);
        Assert.DoesNotContain("output/debug_logs.json", outputRepairTargetFiles);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));

        var narrativeJson = await _fs.ReadFileAsync("output/narrative_response.json");
        Assert.Contains("2/6", narrativeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("3/6", narrativeJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues_ActorMemoryOnlyRepair_DoesNotRequireNarrativeRewrite()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Элиара объясняет, как сохранить память.",
            timestamp = "2026-07-10T07:30:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[]
            {
                new { text = "Задать следующий вопрос", inputValue = "Спросить Элиару ещё раз." }
            },
            timestamp = "2026-07-10T07:30:00Z"
        });
        await WriteJsonAsync("game_state/control/validation_repair_request.json", new
        {
            sessionId = "actor-memory-test",
            requestId = "actor-memory-repair",
            turnNumber = 2
        });

        var outputWrittenAt = new DateTime(2026, 7, 10, 7, 30, 0, DateTimeKind.Utc);
        var requestWrittenAt = outputWrittenAt.AddMinutes(1);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/narrative_response.json"), outputWrittenAt);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/interface_updates.json"), outputWrittenAt);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("game_state/control/validation_repair_request.json"), requestWrittenAt);

        var repairedErrors = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardian_thought_journal.json",
                IssueSeverity.Error,
                "Значимая реакция Хранителя осталась только в прозе.",
                code: "guardian_relevant_actor_missing_thought_journal_delta",
                actor: "Хранительница Элиара Карт Невозвращения",
                section: "actor_memory")
        };
        var engine = CreateGameEngine();
        var method = typeof(GameEngine).GetMethod(
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var issues = Assert.IsType<List<ValidationIssue>>(method!.Invoke(
            engine,
            new object[] { repairedErrors, requestWrittenAt }));

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("actor_thought_journal_not_first_person", "game_state/meta/guardian_thought_journal.json.entries[0].summary")]
    [InlineData("flexible_state_unknown_top_level_key", "game_state/meta/guardian_thought_journal.json.schemaVersion")]
    public async Task CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues_OtherActorMemoryRepairsDoNotRequireNarrativeRewrite(
        string issueCode,
        string issuePath)
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Элиара объясняет, как сохранить память.",
            timestamp = "2026-07-10T07:30:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[]
            {
                new { text = "Задать следующий вопрос", inputValue = "Спросить Элиару ещё раз." }
            },
            timestamp = "2026-07-10T07:30:00Z"
        });
        await WriteJsonAsync("game_state/control/validation_repair_request.json", new
        {
            sessionId = "actor-memory-test",
            requestId = "actor-memory-repair",
            turnNumber = 2
        });

        var outputWrittenAt = new DateTime(2026, 7, 10, 7, 30, 0, DateTimeKind.Utc);
        var requestWrittenAt = outputWrittenAt.AddMinutes(1);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/narrative_response.json"), outputWrittenAt);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/interface_updates.json"), outputWrittenAt);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("game_state/control/validation_repair_request.json"), requestWrittenAt);

        var repairedErrors = new List<ValidationIssue>
        {
            new(
                issuePath,
                IssueSeverity.Error,
                "Внутренняя память Хранителя требует узкой правки.",
                code: issueCode,
                actor: "Хранительница Элиара Карт Невозвращения",
                section: "actor_memory")
        };
        var engine = CreateGameEngine();
        var method = typeof(GameEngine).GetMethod(
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var issues = Assert.IsType<List<ValidationIssue>>(method!.Invoke(
            engine,
            new object[] { repairedErrors, requestWrittenAt }));

        Assert.Empty(issues);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_GuardianScopeErrors_AddsConcreteHarnessRepairPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians",
                IssueSeverity.Error,
                "Current guardians[] must match kernel-authoritative guardian state reconstructed from validated pre-turn baseline and authorized same-turn guardian mutations.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians",
                expected: "kernel-authoritative guardians[] only",
                actual: "materialized current guardians[] diverges from kernel authority view"),
            new(
                "game_state/meta/guardians.json.activeGuardian",
                IssueSeverity.Error,
                "Current activeGuardian must match kernel-authoritative guardian mirror state.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians",
                expected: "kernel-authoritative activeGuardian only",
                actual: "materialized current activeGuardian diverges from kernel authority view"),
            new(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Структурированное обновление Guardian 'Азалия' не покрыто declared relevant actors",
                code: "structured_guardian_update_out_of_scope",
                actor: "Азалия",
                section: "UpdateGuardians",
                expected: "'Азалия' declared in Relevant actors",
                actual: "UpdateGuardians changed actor outside declared scope"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Отсутствует reasoning block для Guardian 'Азалия' в gm_thoughts_markdown",
                code: "missing_actor_block",
                actor: "Азалия",
                section: "npc_reasoning",
                expected: "### Азалия block",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "ответа GM", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_scope_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains("Азалия", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("output/debug_logs.json.gm_thoughts_markdown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Relevant actors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("### Азалия", StringComparison.OrdinalIgnoreCase));

        var template = packet.GetProperty("debugLogTemplate").GetString();
        Assert.Contains("## Охват NPC-анализа", template, StringComparison.Ordinal);
        Assert.Contains("Релевантные акторы: Азалия", template, StringComparison.Ordinal);
        Assert.Contains("### Азалия", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_GuardianAuthorityErrors_UsesCurrentGuardianNameAndProjectTargets()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "displayName": "Азалия"
          },
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "displayName": "Азалия"
            }
          ]
        }
        """);
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardian_projects.json.activeProjects",
                IssueSeverity.Error,
                "Current activeProjects must match kernel-authoritative guardian project tracker state reconstructed from validated pre-turn baseline and same-turn project commands.",
                code: "guardian_project_materialized_state_outside_authority",
                section: "GuardianProjects",
                expected: "kernel-authoritative activeProjects only",
                actual: "materialized current activeProjects diverges from kernel authority view"),
            new(
                "game_state/meta/guardians.json.activeGuardian",
                IssueSeverity.Error,
                "Current activeGuardian must match kernel-authoritative guardian mirror state.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians",
                expected: "kernel-authoritative activeGuardian only",
                actual: "materialized current activeGuardian diverges from kernel authority view")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_scope_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Азалия", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/meta/guardian_projects.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("implementation code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_GuardianScopeErrors_DebugTemplateIncludesEveryListedGuardian()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[0]",
                IssueSeverity.Error,
                "Structured Guardian update is outside declared scope.",
                code: "structured_guardian_update_out_of_scope",
                actor: "Азалия",
                section: "UpdateGuardians",
                expected: "Азалия declared in Relevant actors",
                actual: "missing"),
            new(
                "game_state/meta/guardians.json.guardians[1]",
                IssueSeverity.Error,
                "Structured Guardian update is outside declared scope.",
                code: "structured_guardian_update_out_of_scope",
                actor: "Эфон",
                section: "UpdateGuardians",
                expected: "Эфон declared in Relevant actors",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_scope_repair", packet.GetProperty("kind").GetString());

        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("Релевантные акторы: Азалия, Эфон", template, StringComparison.Ordinal);
        Assert.Contains("### Азалия", template, StringComparison.Ordinal);
        Assert.Contains("### Эфон", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_GuardianTradeInventoryResolutionErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/control/pending_guardian_trade_request.json",
                IssueSeverity.Error,
                "pending_guardian_trade_request из pre-turn snapshot не привёл к matching guardian.tradeInventory",
                code: "guardian_trade_request_missing_inventory_resolution",
                section: "GuardianTrade",
                expected: "guardian.tradeInventory matching requestId/tradeCycleId/returnCycleId",
                actual: "missing tradeInventory",
                repairHint: "На accepted turn обязательно materialize-ь guardian.tradeInventory по exact client-authored request contract; не игнорируй request и не закрывай его частично совпадающей витриной."),
            new(
                "game_state/control/pending_guardian_trade_request.json",
                IssueSeverity.Error,
                "pending_guardian_trade_request из pre-turn snapshot не был закрыт canonical tradeInventory receipt",
                code: "guardian_trade_request_missing_receipt_resolution",
                section: "GuardianTrade",
                expected: "matching guardianTradeInventoryReceipts[] entry",
                actual: "missing receipt")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 3 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_trade_inventory_resolution_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/control/pending_guardian_trade_request.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("guardian.tradeInventory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("requestId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("tradeCycleId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("tradeInventoryReceipts", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("pending_guardian_trade_request.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Read-BoeJson", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("tradeInventory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("new turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("pending_guardian_trade_request.json", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("rewrite", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_PendingGuardianCreationMaterializationErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[0].guardianId",
                IssueSeverity.Error,
                "Fresh startup Guardian was materialized without the supported create surface.",
                code: "guardian_materialized_without_create_surface",
                section: "Guardians",
                expected: "UpdateGuardians.create with full canonical Guardian shape",
                actual: "direct materialized guardian object"),
            new(
                "game_state/meta/guardians.json.pendingGuardianCreation",
                IssueSeverity.Error,
                "pendingGuardianCreation remains after Guardian materialization.",
                code: "stale_pending_guardian_creation_after_materialization",
                section: "Guardians",
                expected: "pendingGuardianCreation removed after canonical guardians[] and activeGuardian materialization",
                actual: "pending request still present"),
            new(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Structured Guardian create update is missing canonical identity.",
                code: "structured_guardian_update_missing_canonical_identity",
                section: "UpdateGuardians",
                expected: "canonical guardianId/displayName/title/domain/abode identity",
                actual: "partial startup guardian")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "первого хода после создания души", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_pending_creation_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        var skeleton = packet.GetProperty("canonicalCreateSkeleton");
        Assert.True(skeleton.TryGetProperty("UpdateGuardians", out var updateGuardians));
        Assert.Equal(JsonValueKind.Array, updateGuardians.ValueKind);
        Assert.False(skeleton.TryGetProperty("updateGuardians", out _));
        var createCommand = Assert.Single(updateGuardians.EnumerateArray());
        Assert.Equal("create", createCommand.GetProperty("command").GetString());
        Assert.True(createCommand.TryGetProperty("data", out var createData));
        Assert.True(createData.TryGetProperty("guardianId", out _));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("pendingGuardianCreation", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("UpdateGuardians.create", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("UpdateGuardians", StringComparison.Ordinal) &&
                                               item.Contains("command", StringComparison.OrdinalIgnoreCase) &&
                                               item.Contains("create", StringComparison.OrdinalIgnoreCase) &&
                                               item.Contains("data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("guardians[]", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("activeGuardian", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("chaosSeaNavigation", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("remove pendingGuardianCreation", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("validation_repair_request.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Read-BoeJson", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("pendingGuardianCreation", StringComparison.Ordinal));
        Assert.Contains(steps, step => step.Contains("UpdateGuardians.create", StringComparison.Ordinal));
        Assert.Contains(steps, step => step.Contains("do not repair", StringComparison.OrdinalIgnoreCase) &&
                                       step.Contains("guardians[]", StringComparison.Ordinal) &&
                                       step.Contains("activeGuardian", StringComparison.Ordinal));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("delete pendingGuardianCreation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("direct materialized", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("guardians[]", StringComparison.Ordinal) &&
                                         item.Contains("activeGuardian", StringComparison.Ordinal) &&
                                         item.Contains("without UpdateGuardians", StringComparison.OrdinalIgnoreCase));

        var safeCorrectionRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeCorrectionRules, item => item.Contains("pending-only", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(safeCorrectionRules, item => item.Contains("materialization was not intended", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_PendingGuardianCreationRepair_IncludesBoundedCreateSkeletonAndEnums()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[0].guardianId",
                IssueSeverity.Error,
                "Fresh startup Guardian was materialized without the supported create surface.",
                code: "guardian_materialized_without_create_surface",
                section: "Guardians",
                expected: "UpdateGuardians.create with full canonical Guardian shape",
                actual: "direct materialized guardian object"),
            new(
                "game_state/meta/guardians.json.guardians[0].loreFragments",
                IssueSeverity.Error,
                "Canonical guardian state должен хранить как минимум 7 pre-planned lore fragments",
                code: "guardian_state_lore_fragments_below_minimum",
                section: "Guardians",
                expected: ">= 7 lore fragments",
                actual: "2"),
            new(
                "game_state/meta/guardians.json.guardians[0].musings[0].topic",
                IssueSeverity.Error,
                "Guardian musing.topic должен быть одним из canonical topic enums",
                code: "guardian_musing_invalid_topic",
                section: "Guardians",
                expected: "soul_assessment | domain_insight | guardian_politics | chaos_sea | personal_reflection | quest_planning",
                actual: "mirror_memory"),
            new(
                "game_state/meta/guardians.json.guardians[0].relationshipData.guardianRoleToPlayer",
                IssueSeverity.Error,
                "guardianRoleToPlayer поддерживает только canonical значение former_patron в v1 foundation branch",
                code: "guardian_relationship_invalid_role_to_player",
                section: "Guardians",
                expected: "former_patron or omit field",
                actual: "mentor")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "первого хода после создания души", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("guardian_pending_creation_materialization_repair", packet.GetProperty("kind").GetString());

        var skeleton = packet.GetProperty("canonicalCreateSkeleton");
        Assert.Equal("UpdateGuardians.create", skeleton.GetProperty("authoritySurface").GetString());
        Assert.True(skeleton.TryGetProperty("UpdateGuardians", out var updateGuardians));
        Assert.Equal(JsonValueKind.Array, updateGuardians.ValueKind);
        Assert.False(skeleton.TryGetProperty("updateGuardians", out _));
        var createCommand = updateGuardians.EnumerateArray().Single();
        Assert.Equal("create", createCommand.GetProperty("command").GetString());
        var data = createCommand.GetProperty("data");
        Assert.True(data.TryGetProperty("guardianId", out _));
        Assert.True(data.TryGetProperty("canonicalName", out _));
        Assert.True(data.TryGetProperty("manifestation", out _));
        Assert.True(data.TryGetProperty("abode", out _));
        Assert.True(data.TryGetProperty("relationshipData", out var relationshipData));
        Assert.False(relationshipData.TryGetProperty("guardianRoleToPlayer", out _));
        Assert.True(relationshipData.TryGetProperty("lastInteraction", out var lastInteraction));
        Assert.Equal(JsonValueKind.Null, lastInteraction.ValueKind);
        Assert.True(data.TryGetProperty("abodePower", out var abodePower));
        Assert.Equal(10, abodePower.GetProperty("currentPower").GetInt32());
        Assert.Equal("Угасающая", abodePower.GetProperty("tier").GetString());
        Assert.DoesNotContain("startup_turn", data.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(data.TryGetProperty("mood", out var mood));
        Assert.Equal("focused", mood.GetProperty("current").GetString());
        Assert.True(data.TryGetProperty("loreFragments", out var loreFragments));
        Assert.Equal(7, loreFragments.GetArrayLength());
        Assert.True(data.TryGetProperty("musings", out var musings));
        Assert.Equal("soul_assessment", musings.EnumerateArray().Single().GetProperty("topic").GetString());

        var allowedEnums = packet.GetProperty("allowedEnums");
        Assert.Contains("soul_assessment", allowedEnums.GetProperty("guardianMusingTopics").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("contemplative", allowedEnums.GetProperty("guardianMusingMoods").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("focused", allowedEnums.GetProperty("guardianMoodCurrent").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("personal_history", allowedEnums.GetProperty("guardianLoreFragmentCategories").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("230", allowedEnums.GetProperty("guardianLoreFragmentRequiredReputation").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("former_patron", allowedEnums.GetProperty("guardianRoleToPlayerV1").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("canonicalCreateSkeleton", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("7 loreFragments", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("guardianRoleToPlayer", StringComparison.Ordinal) &&
                                               item.Contains("omit", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("invent", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("guardianRoleToPlayer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_UnresolvedStartupPendingGuardianCreation_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.pendingGuardianCreation",
                IssueSeverity.Error,
                "Стартовый freeform pendingGuardianCreation нельзя оставлять неразрешенным после принятого первого хода.",
                code: "pending_guardian_creation_unresolved_after_startup_turn",
                section: "Guardians",
                expected: "startup freeform Guardian materialized into canonical guardians[] + activeGuardian + chaosSeaNavigation",
                actual: "pending-only state")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "первого хода после создания души", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());

        Assert.Equal("guardian_pending_creation_materialization_repair", packet.GetProperty("kind").GetString());
        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("UpdateGuardians.create", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("command", StringComparison.OrdinalIgnoreCase) &&
                                               item.Contains("create", StringComparison.OrdinalIgnoreCase) &&
                                               item.Contains("data", StringComparison.OrdinalIgnoreCase));
        var safeCorrectionRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeCorrectionRules, item => item.Contains("pending-only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_ActorReasoningSubpointErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Для Guardian 'Азалия' отсутствует подпункт ситуации/current situation",
                code: "missing_actor_situation",
                actor: "Азалия",
                section: "npc_reasoning",
                expected: "Situation / Current situation",
                actual: "missing"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Для Guardian 'Азалия' отсутствует подпункт мыслей/internal thoughts",
                code: "missing_actor_thoughts",
                actor: "Азалия",
                section: "npc_reasoning",
                expected: "Thoughts / Internal thoughts",
                actual: "missing"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Для Guardian 'Азалия' отсутствует подпункт действий/intended actions",
                code: "missing_actor_actions",
                actor: "Азалия",
                section: "npc_reasoning",
                expected: "Actions / Intended actions",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 4 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_reasoning_subpoint_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Азалия", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("### Азалия", template, StringComparison.Ordinal);
        Assert.Contains("- Ситуация:", template, StringComparison.Ordinal);
        Assert.Contains("- Мысли:", template, StringComparison.Ordinal);
        Assert.Contains("- Действия:", template, StringComparison.Ordinal);
        Assert.Contains(
            packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("Complete-BoeValidationRepair", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_FullActorBrainErrors_AddsDecisionAuditTemplate()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Отсутствуют данные профиля",
                code: "actor_brain_missing_profile_inputs",
                actor: "Иветта",
                section: "npc_reasoning",
                expected: "Profile inputs / Данные профиля",
                actual: "missing"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Отсутствует сравнение стратегий",
                code: "actor_brain_missing_strategy_tradeoffs",
                actor: "Иветта",
                section: "npc_reasoning",
                expected: "two strategies with Benefit/Risk",
                actual: "missing"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Отсутствует выбранная стратегия",
                code: "actor_brain_missing_chosen_strategy",
                actor: "Иветта",
                section: "npc_reasoning",
                expected: "Chosen strategy",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_reasoning_subpoint_repair", packet.GetProperty("kind").GetString());
        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("- Данные профиля:", template, StringComparison.Ordinal);
        Assert.Contains("- Мотивация:", template, StringComparison.Ordinal);
        Assert.Contains("- Ограничения:", template, StringComparison.Ordinal);
        Assert.Contains("- Варианты стратегий:", template, StringComparison.Ordinal);
        Assert.Contains("Выгода:", template, StringComparison.Ordinal);
        Assert.Contains("Риск:", template, StringComparison.Ordinal);
        Assert.Contains("- Выбранная стратегия:", template, StringComparison.Ordinal);
        Assert.Contains("- Почему альтернативы отвергнуты:", template, StringComparison.Ordinal);
        Assert.Contains("- Изменения состояния:", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_ActorMemoryErrors_AddsNarrowPersistencePacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[guard_elara].musings",
                IssueSeverity.Error,
                "Хранитель не записал мысль",
                code: "guardian_relevant_actor_missing_thought_journal_delta",
                actor: "Элиара",
                section: "actor_memory"),
            new(
                "game_state/npcs/npc_journals.json",
                IssueSeverity.Error,
                "NPC не записал мысль",
                code: "mortal_npc_relevant_actor_missing_thought_journal_delta",
                actor: "Иветта",
                section: "actor_memory"),
            new(
                "game_state/meta/guardian_abode_residents.json",
                IssueSeverity.Error,
                "Житель не записал мысль",
                code: "afterlife_resident_relevant_actor_missing_thought_journal_delta",
                actor: "Лиора",
                section: "actor_memory")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_memory_persistence_repair", packet.GetProperty("kind").GetString());
        var targetFiles = packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(GuardianThoughtJournalState.StatePath, targetFiles);
        Assert.DoesNotContain("game_state/meta/guardians.json", targetFiles);
        Assert.Contains("game_state/npcs/npc_journals.json", targetFiles);
        Assert.Contains("game_state/meta/guardian_abode_residents.json", targetFiles);
        Assert.Contains("output/debug_logs.json", targetFiles);
        Assert.DoesNotContain("output/narrative_response.json", targetFiles);
        Assert.Contains(
            packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("Изменения состояния", StringComparison.OrdinalIgnoreCase) &&
                    item.Contains("journal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("first-person", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("\"entries\"", StringComparison.Ordinal) &&
                    item.Contains("\"guardianId\"", StringComparison.Ordinal));
        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("schemaVersion", StringComparison.Ordinal) &&
                    item.Contains("guardianThoughtJournalUpdates", StringComparison.Ordinal));
        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("unrelated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_GuardianJournalShapeErrors_StayInActorMemoryPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardian_thought_journal.json",
                IssueSeverity.Error,
                "Файл не содержит ни одного допустимого top-level ключа для своего контракта",
                code: "strict_state_missing_allowed_top_level_key",
                section: "StateFiles",
                expected: "entries",
                actual: "schemaVersion, guardianThoughtJournalUpdates"),
            new(
                "game_state/meta/guardian_thought_journal.json.schemaVersion",
                IssueSeverity.Error,
                "Недопустимый top-level ключ: schemaVersion",
                code: "flexible_state_unknown_top_level_key",
                section: "StateFiles",
                expected: "entries, guardianThoughtJournalUpdates",
                actual: "schemaVersion")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 2 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_memory_persistence_repair", packet.GetProperty("kind").GetString());
        var targetFiles = packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(GuardianThoughtJournalState.StatePath, targetFiles);
        Assert.Contains("output/debug_logs.json", targetFiles);
        Assert.DoesNotContain("game_state/control/mortal_bootstrap_scaffold.json", targetFiles);
        Assert.DoesNotContain(targetFiles, path => path?.Contains("schemaVersion", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_ShiningFactionMemoryError_TargetsFactionMemorySurface()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Сияющая фракция не обновила стратегическую память",
                code: "shining_faction_relevant_actor_missing_strategic_memory_delta",
                actor: "Орден Рассвета",
                section: "actor_memory")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_memory_persistence_repair", packet.GetProperty("kind").GetString());
        var targetFiles = packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(ShiningAbodeState.StatePath, targetFiles);
        Assert.Contains("output/debug_logs.json", targetFiles);
        Assert.Equal(2, targetFiles.Length);
        Assert.Contains(
            packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains(ShiningAbodeState.FactionChronicleUpdatesProperty, StringComparison.Ordinal) &&
                    !item.Contains(ShiningAbodeState.FactionStrategicMemoryUpdatesProperty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeActorWithoutMemoryOwner_OffersMaterializeOrDescopeRepair()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Актор не имеет canonical memory owner",
                code: "afterlife_relevant_actor_missing_canonical_memory_owner",
                actor: "Безымянный Советник",
                section: "actor_memory")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_entity_profile_scaffold_repair", packet.GetProperty("kind").GetString());
        Assert.Contains(
            AfterlifeEntityProfileState.StatePath,
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(
            packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("material", StringComparison.OrdinalIgnoreCase) &&
                    item.Contains("relevant actors", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_DirectlyAddressedActorScopeError_UsesFullActorBrainScopePacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Прямо названный NPC отсутствует в scope",
                code: "directly_addressed_actor_missing_from_scope",
                actor: "Иветта",
                section: "npc_scope")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("npc_scope_declaration_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Иветта", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("- Данные профиля:", template, StringComparison.Ordinal);
        Assert.Contains("- Варианты стратегий:", template, StringComparison.Ordinal);
        Assert.Contains("Выгода:", template, StringComparison.Ordinal);
        Assert.Contains("Риск:", template, StringComparison.Ordinal);
        Assert.Contains("- Изменения состояния:", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AcceptedTurnOutputArtifactErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/narrative_response.json",
                IssueSeverity.Error,
                "Accepted GM turn должен содержать свежий output/narrative_response.json с непустым response",
                code: "accepted_turn_missing_narrative_response",
                section: "Narrative",
                expected: "output/narrative_response.json with non-empty response",
                actual: "missing or empty",
                repairHint: "Запиши output/narrative_response.json с полем response для текущего accepted turn."),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("accepted_turn_output_artifact_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains("output/narrative_response.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("\"response\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("gm_thoughts_markdown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("## Охват NPC-анализа", StringComparison.Ordinal));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("output/narrative_response.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("output/debug_logs.json.gm_thoughts_markdown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("new turn", StringComparison.OrdinalIgnoreCase));

        var debugTemplate = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("- Данные профиля:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("- Варианты стратегий:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("Выгода:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("Риск:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("- Изменения состояния:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("NPCJournals[].journalEntries[]", string.Join("\n", steps), StringComparison.Ordinal);
        Assert.Contains("guardianThoughtJournalUpdates", string.Join("\n", steps), StringComparison.Ordinal);
        Assert.Contains("residentThoughtJournalUpdates", string.Join("\n", steps), StringComparison.Ordinal);
        Assert.Contains("ledger/progressionLedger", string.Join("\n", steps), StringComparison.Ordinal);
        Assert.Contains("shiningFactionChronicleUpdates", string.Join("\n", steps), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_NarrativeTimestampError_AddsAcceptedTurnOutputPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/narrative_response.json.timestamp",
                IssueSeverity.Error,
                "output/narrative_response.json must include timestamp",
                code: "narrative_response_missing_timestamp",
                section: "Narrative",
                expected: "ISO-8601 UTC timestamp",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("accepted_turn_output_artifact_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("output/narrative_response.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(
            packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("\"timestamp\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("Do not rewrite output/debug_logs.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_OutputUnknownFieldCodes_WhitelistCanonicalTargetsWhenPathsAreMalformed()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "unexpected/narrative-path.json",
                IssueSeverity.Error,
                "Narrative output contains an unsupported field.",
                code: "narrative_response_unknown_field",
                section: "Narrative"),
            new(
                string.Empty,
                IssueSeverity.Error,
                "Interface output is empty.",
                code: "accepted_turn_empty_interface_updates",
                section: "Interface"),
            new(
                "unexpected/debug-path.json",
                IssueSeverity.Error,
                "Debug output contains an unsupported field.",
                code: "debug_logs_unknown_field",
                section: "gm_thoughts_markdown")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse(
            (await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("accepted_turn_output_artifact_repair", packet.GetProperty("kind").GetString());

        var targets = packet.GetProperty("targetFiles")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(3, targets.Length);
        Assert.Contains("output/narrative_response.json", targets);
        Assert.Contains("output/interface_updates.json", targets);
        Assert.Contains("output/debug_logs.json", targets);
        Assert.DoesNotContain(targets, path => string.IsNullOrWhiteSpace(path));
        Assert.DoesNotContain(targets, path => path.StartsWith("unexpected/", StringComparison.OrdinalIgnoreCase));

        var debugTemplate = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("- Данные профиля:", debugTemplate, StringComparison.Ordinal);
        Assert.Contains("- Изменения состояния:", debugTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_UnroutableStaleOutputDiagnostic_DoesNotEmitEmptyHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "unexpected/player-facing-output.json",
                IssueSeverity.Error,
                "A player-facing artifact is stale, but its canonical output file is unknown.",
                code: "accepted_turn_stale_player_facing_output_after_canonical_repair",
                section: "PlayerFacingOutput")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse(
            (await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        Assert.Empty(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MissingNpcActorBlock_AddsActorReasoningPacketNotGuardianPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Relevant actor 'Ирен Соль' has no reasoning block.",
                code: "missing_actor_block",
                actor: "Ирен Соль.",
                section: "npc_reasoning",
                expected: "### Ирен Соль reasoning block",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_reasoning_subpoint_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Ирен Соль", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("game_state/meta/guardians.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("create a missing reasoning block", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("current location", StringComparison.OrdinalIgnoreCase) || step.Contains("Текущ", StringComparison.OrdinalIgnoreCase));

        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("### Ирен Соль", template, StringComparison.Ordinal);
        Assert.Contains("- Текущая локация:", template, StringComparison.Ordinal);
        Assert.Contains("- Ситуация:", template, StringComparison.Ordinal);
        Assert.Contains("- Мысли:", template, StringComparison.Ordinal);
        Assert.Contains("- Действия:", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MixedGuardianAndNpcReasoningErrors_KeepsSeparatePackets()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[0]",
                IssueSeverity.Error,
                "Structured Guardian update is outside declared scope.",
                code: "structured_guardian_update_out_of_scope",
                actor: "Эфон",
                section: "UpdateGuardians",
                expected: "Эфон declared in Relevant actors",
                actual: "missing"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Relevant actor 'Ирен Соль' has no reasoning block.",
                code: "missing_actor_block",
                actor: "Ирен Соль.",
                section: "npc_reasoning",
                expected: "### Ирен Соль reasoning block",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packets = doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray().ToArray();
        Assert.Equal(2, packets.Length);

        var guardianPacket = Assert.Single(
            packets,
            packet => packet.GetProperty("kind").GetString() == "guardian_scope_repair");
        Assert.Contains("Эфон", guardianPacket.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("Ирен Соль", guardianPacket.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var actorPacket = Assert.Single(
            packets,
            packet => packet.GetProperty("kind").GetString() == "actor_reasoning_subpoint_repair");
        Assert.Contains("Ирен Соль", actorPacket.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("Эфон", actorPacket.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_FactionIdentityErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/factions/faction_core.json.factions[0].factionId",
                IssueSeverity.Error,
                "Full faction object references an unknown permanent factionId.",
                code: "faction_full_object_unknown_faction_id",
                section: "Factions",
                expected: "existing permanent factionId from faction_core.json",
                actual: "faction_merchant_guild"),
            new(
                "game_state/factions/faction_core.json.factions[0].initialId",
                IssueSeverity.Error,
                "Full faction object uses initialId for an existing faction.",
                code: "faction_full_object_existing_requires_faction_id",
                section: "Factions",
                expected: "permanent factionId for existing faction",
                actual: "initialId=temp-faction-merchant-guild-eternia"),
            new(
                "game_state/factions/faction_custom.json.entries[0]",
                IssueSeverity.Error,
                "Canonical faction sidecar entry requires a permanent factionId.",
                code: "canonical_faction_sidecar_requires_permanent_faction_id",
                section: "Factions",
                expected: "existing permanent factionId from faction_core.json",
                actual: "Null")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 3 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("faction_identity_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/factions/faction_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/factions/faction_custom.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("pending_turn_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("factionId = null", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("initialId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("isNewFaction = true", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md",
            packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("factions[]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("factionId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("sidecar", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("create", StringComparison.OrdinalIgnoreCase) && item.Contains("missing faction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("remove", StringComparison.OrdinalIgnoreCase) && item.Contains("sidecar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("existing canonical factionId", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty),
            item => item.Contains("invent a permanent factionId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_FactionResourceEntryShapeErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/factions/faction_resources.json.entries[0]",
                IssueSeverity.Error,
                "Canonical faction resource entries require full object shape.",
                code: "canonical_faction_resource_entry_missing_required_fields",
                section: "Factions",
                expected: "resourceId/name/displayName/type/value/source/visibility and upkeep fields for metaResources",
                actual: "missing value, source, visibility, upkeep")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_faction_resource_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/factions/faction_resources.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("canonical_faction_resource_entry_missing_required_fields", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("metaResources", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("strategicGoods", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("MORTAL_FACTION_UPDATE_TEMPLATE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("full resource object", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalLocationTransitionErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/world/current_location.json.locationId",
                IssueSeverity.Error,
                "Current location references an unknown location id.",
                code: "current_location_unknown_location_id",
                section: "WorldMap",
                expected: "current location id must exist in world map",
                actual: "loc_family_library"),
            new(
                "game_state/npcs/npc_core.json.NPCs[0].currentLocationId",
                IssueSeverity.Error,
                "NPC currentLocationId references an unknown location id.",
                code: "npc_unknown_current_location_id",
                actor: "Мариус де Гран",
                section: "NPC",
                expected: "known world map location id",
                actual: "loc_family_library"),
            new(
                "game_state/world/world_map.json.newLocations[1].coordinates",
                IssueSeverity.Error,
                "Two same-turn new locations use duplicate coordinates.",
                code: "world_map_new_location_coordinates_duplicate_same_turn",
                section: "WorldMap",
                expected: "unique coordinates for each same-turn new location",
                actual: "x=2,y=1,z=0")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_location_transition_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/world/current_location.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/world/world_map.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Мариус де Гран", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("world_map", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("current_location", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("coordinates", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("register", StringComparison.OrdinalIgnoreCase) && item.Contains("world_map", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("duplicate coordinates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("narrative color", StringComparison.OrdinalIgnoreCase) && item.Contains("unchanged", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalLocationShapeErrors_AddsExecutableHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/world/world_map.json.newLocations[0].activeThreats",
                IssueSeverity.Error,
                "Location is missing activeThreats array.",
                code: "location_missing_active_threat_array",
                section: "WorldMap",
                expected: "activeThreats as array",
                actual: "missing"),
            new(
                "game_state/world/world_map.json.newLocations[0].adjacencyMap",
                IssueSeverity.Error,
                "Location is missing adjacencyMap array.",
                code: "location_missing_adjacency_array",
                section: "WorldMap",
                expected: "adjacencyMap as array",
                actual: "missing"),
            new(
                "game_state/world/world_map.json.newLocations[0].internalDifficultyProfile",
                IssueSeverity.Error,
                "Location is missing internal difficulty profile.",
                code: "location_missing_difficulty_profile",
                section: "WorldMap",
                expected: "internalDifficultyProfile and externalDifficultyProfile",
                actual: "missing"),
            new(
                "game_state/world/world_map.json.newLocations[0].locationStorages",
                IssueSeverity.Error,
                "Location is missing locationStorages array.",
                code: "location_missing_storage_array",
                section: "WorldMap",
                expected: "locationStorages as array",
                actual: "missing"),
            new(
                "game_state/world/world_map.json.newLinks[0]",
                IssueSeverity.Error,
                "World-map new link is missing preview fields.",
                code: "world_map_new_link_missing_required_fields",
                section: "WorldMap",
                expected: "targetName, targetCoordinates, estimatedInternalDifficultyProfile, estimatedExternalDifficultyProfile",
                actual: "missing estimated profiles")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_location_transition_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/world/world_map.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("location_missing_active_threat_array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("location_missing_adjacency_array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("location_missing_difficulty_profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("location_missing_storage_array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("world_map_new_link_missing_required_fields", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("activeThreats", StringComparison.Ordinal) && item.Contains("locationStorages", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("internalDifficultyProfile", StringComparison.Ordinal) && item.Contains("externalDifficultyProfile", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("estimatedInternalDifficultyProfile", StringComparison.Ordinal) && item.Contains("estimatedExternalDifficultyProfile", StringComparison.Ordinal));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("required arrays", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("difficulty profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("link preview", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalWorldMapAdjacencyErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/world/world_map.json.adjacency[1].targetLocationId",
                IssueSeverity.Error,
                "World map adjacency points to an unknown target location.",
                code: "world_map_adjacency_unknown_target",
                section: "WorldMap",
                expected: "targetLocationId must reference an existing locationId or a same-turn newLocations.initialId",
                actual: "loc_salt_awning"),
            new(
                "game_state/world/world_map.json.linkUpdates[0].sourceLocationId",
                IssueSeverity.Error,
                "World map link update source is unknown.",
                code: "world_map_link_update_unknown_source",
                section: "WorldMap",
                expected: "sourceLocationId must reference an existing locationId",
                actual: "loc_hidden_panel")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_world_map_adjacency_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/world/world_map.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/world/current_location.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("world_map_adjacency_unknown_target", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("existing locationId", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("materialize", StringComparison.OrdinalIgnoreCase) && item.Contains("location", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("remove", StringComparison.OrdinalIgnoreCase) && item.Contains("link", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("unknown target", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalOutdoorBiomeErrors_AddsLocationTransitionHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/world/current_location.json.biome",
                IssueSeverity.Error,
                "Outdoor location обязан содержать biome.",
                code: "location_outdoor_biome_missing",
                section: "Location",
                expected: "TemperateForest | ColdForest | Swamp | Urban | Plains | Mountains | Desert | Coast | Unique",
                actual: "missing",
                repairHint: "Choose the canonical biome that matches the scene.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_location_transition_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/world/current_location.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("location_outdoor_biome_missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("outdoor", StringComparison.OrdinalIgnoreCase) && item.Contains("canonical biome", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("TemperateForest", StringComparison.Ordinal) && item.Contains("Unique", StringComparison.Ordinal));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("biome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcLocationErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.NPCsInScene[0].currentLocationId",
                IssueSeverity.Error,
                "Same-turn scene NPC must not use currentLocationId.",
                code: "npc_same_turn_initial_location_requires_null_current_location",
                actor: "Мариус де Гран",
                section: "UpdateNPCs",
                expected: "initialLocationId=current location and currentLocationId=null",
                actual: "currentLocationId=loc_library"),
            new(
                "game_state/npcs/npc_core.json.NPCsInScene[0].initialLocationId",
                IssueSeverity.Error,
                "Current location scene NPC is missing the same-turn initial location id.",
                code: "current_location_new_scene_missing_initial_id_for_npc_scene",
                actor: "Мариус де Гран",
                section: "UpdateNPCs",
                expected: "initialLocationId from current location",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_location_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Мариус де Гран", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("initialLocationId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("currentLocationId", StringComparison.OrdinalIgnoreCase) && step.Contains("null", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("NPCsInScene", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcScopeErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.UpdateNPCs[1]",
                IssueSeverity.Error,
                "Structured NPC update is outside declared relevant actors.",
                code: "structured_npc_update_out_of_scope",
                actor: "Мальчишка-посыльный дома Виренто",
                section: "UpdateNPCs",
                expected: "'Мальчишка-посыльный дома Виренто' declared in Relevant actors",
                actual: "UpdateNPCs changed actor outside declared scope")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_scope_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Мальчишка-посыльный дома Виренто", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("Relevant actors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("reasoning", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("add", StringComparison.OrdinalIgnoreCase) && item.Contains("Relevant actors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("remove", StringComparison.OrdinalIgnoreCase) && item.Contains("structured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalRelevantActorMissingPersistence_AddsNpcSpecificHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Mortal World relevant actor 'Матео' declared in NPC scope but has no persistent Mortal surface",
                code: "mortal_relevant_actor_missing_persistence",
                actor: "Матео",
                section: "npc_scope",
                expected: "matching NPC/faction/quest/inventory persistence in canonical state or structured same-turn updates",
                actual: "actor appears only in gm_thoughts_markdown / narrative reasoning")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_scope_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Матео", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step =>
            step.Contains("persistent", StringComparison.OrdinalIgnoreCase) &&
            step.Contains("NPC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_TrainingShowcaseStaleSnapshot_AddsExactHashHarnessPacket()
    {
        var engine = CreateGameEngine();
        var expectedHash = "401ba6b8c2d057a629cf1bf97820fb050c6b1ec25e89e7ee853e85f0c3324079";
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.UpdateNPCs[0].trainingShowcase.sourceActorSnapshotHash",
                IssueSeverity.Error,
                "Витрина обучения устарела: sourceActorSnapshotHash не совпадает с текущим профилем источника.",
                code: "training_showcase_stale_source_actor_snapshot",
                actor: "Селина Орвейн",
                section: "trainingShowcase",
                expected: expectedHash,
                actual: "stale-hash",
                repairHint: "Запиши свежий sourceActorSnapshotHash.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("training_showcase_snapshot_hash_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/control/pending_training_showcase_requests.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Examples/E_CLI_Training_Showcases.txt", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Селина Орвейн", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var correction = Assert.Single(packet.GetProperty("exactFieldCorrections").EnumerateArray());
        Assert.Equal("game_state/npcs/npc_core.json.UpdateNPCs[0].trainingShowcase.sourceActorSnapshotHash", correction.GetProperty("path").GetString());
        Assert.Equal(expectedHash, correction.GetProperty("expected").GetString());
        Assert.Equal("stale-hash", correction.GetProperty("actual").GetString());

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, item => item.Contains("pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_PendingPassiveTrainingSkillEvolution_AddsTargetKindHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/player/skill_mastery.json.skillMasteryChanges[0].skillName",
                IssueSeverity.Error,
                "skillMasteryChanges не может ссылаться на навык, которого нет в canonical active skills state",
                code: "skill_mastery_unknown_active_skill",
                actor: "Чтение следов",
                section: "Skills.Active",
                expected: "existing skillName from game_state/player/skills_active.json",
                actual: "Чтение следов",
                repairHint: "Сохраняй mastery только для реально существующих active skills.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_training_skill_evolution_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/control/pending_training_showcase_requests.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skills_passive.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skill_mastery.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Examples/E_CLI_Training_Showcases.txt", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("details.targetKind", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("passive_skill_mastery", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("passiveSkillChanges", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("Do not charge", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("structuredBonuses", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, item => item.Contains("pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("details.targetKind", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("remove", StringComparison.OrdinalIgnoreCase) && item.Contains("skillMasteryChanges", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalSkillProgressionShapeErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/player/skills_active.json.activeSkillChanges",
                IssueSeverity.Error,
                "activeSkillChanges должен быть массивом объектов.",
                code: "expected_array_of_objects",
                actor: "Ножевой бой",
                section: "Skills.Active",
                expected: "array<object>",
                actual: "Object",
                repairHint: "Запиши изменение навыка как массив объектов, даже если изменение одно."),
            new(
                "game_state/player/skill_mastery.json.skillMasteryChanges",
                IssueSeverity.Error,
                "skillMasteryChanges должен быть массивом.",
                code: "expected_array",
                actor: "Ножевой бой",
                section: "Skills.Mastery",
                expected: "JSON array",
                actual: "Object",
                repairHint: "Запиши изменение мастерства как массив, даже если изменение одно.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_skill_progression_shape_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/control/pending_training_showcase_requests.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skills_active.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skills_passive.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skill_mastery.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Ножевой бой", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("activeSkillChanges", StringComparison.OrdinalIgnoreCase) && item.Contains("array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("skillMasteryChanges", StringComparison.OrdinalIgnoreCase) && item.Contains("array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, item => item.Contains("Do not charge", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, item => item.Contains("single object", StringComparison.OrdinalIgnoreCase) && item.Contains("array", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, item => item.Contains("pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("wrap", StringComparison.OrdinalIgnoreCase) && item.Contains("array", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, item => item.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_EmptyRelevantActorsScopeError_AddsExecutableHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Mode 'Mixed' requires at least one relevant actor.",
                code: "empty_relevant_actors_for_mode",
                section: "npc_scope",
                expected: "At least one relevant actor",
                actual: "empty relevant actor list")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("npc_scope_declaration_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("World-progression", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("Mixed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("Scene-local", StringComparison.OrdinalIgnoreCase));

        var template = packet.GetProperty("debugLogTemplate").GetString() ?? string.Empty;
        Assert.Contains("## NPC Scope", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Relevant actors", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Why relevant", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Actors outside scope", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("### <actor name>", template, StringComparison.OrdinalIgnoreCase);

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("new turn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcKnownSceneLocationErrors_AddsExecutableHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.NPCsInScene[0].currentLocationId",
                IssueSeverity.Error,
                "NPCsInScene entry for a known current scene location must carry currentLocationId.",
                code: "npc_scene_missing_current_location_id",
                actor: "Лира Нериль",
                section: "NPCsInScene",
                expected: "currentLocationId = loc_life_001_start",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_location_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Лира Нериль", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("known current scene location", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("currentLocationId = loc_life_001_start", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(steps, step => step.Contains("currentLocationId to JSON null", StringComparison.OrdinalIgnoreCase));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("known current location", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("same-turn new location", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcExistingInventoryResend_DistinguishesAllLifecycleCases()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.UpdateNPCs[0].inventory",
                IssueSeverity.Error,
                "UpdateNPCs must not resend inventory for existing NPC.",
                code: "npc_existing_inventory_resend_forbidden",
                actor: "Миртан Велор",
                section: "NPCInventory",
                expected: "Use NPCInventoryAdds/Updates/Removals for existing NPC inventory changes",
                actual: "inventory: []")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_inventory_update_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Миртан Велор", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var guidance = string.Join(
            Environment.NewLine,
            new[] { "expectedShape", "safeCorrectionRules", "steps", "doNotDo" }
                .SelectMany(property => packet.GetProperty(property).EnumerateArray())
                .Select(item => item.GetString() ?? string.Empty));
        Assert.DoesNotContain(
            "remove only the forbidden inventory resend while preserving any other validated update fields",
            guidance,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "remove the forbidden inventory resend or remove the whole",
            guidance,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("genuinely new NPC", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ordinary existing NPC", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("true legacy promotion", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remove the whole ordinary-existing full-object resend", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skill, inventory, relationship, journal, activity, equipment/resource", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("main-GM rollback/repair path", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact semantically unchanged validated pre-turn inventory snapshot", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPCInventoryAdds", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not remove the schema-required inventory", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove inventory from UpdateNPCs for every existing NPC", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Do not keep inventory: []", guidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcReasoningLocationErrors_TargetsDebugLog()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "NPC reasoning block is missing current location.",
                code: "missing_actor_current_location",
                actor: "Ирен Соль",
                section: "npc_reasoning",
                expected: "Current location / Текущая локация / currentLocationId line inside actor block",
                actual: "missing")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_location_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("output/debug_logs.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Ирен Соль", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("output/debug_logs.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("current location", StringComparison.OrdinalIgnoreCase) || step.Contains("Текущ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcJournalReferenceErrors_AddsReferencePacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "output/structured_state_updates.json.npcJournals[0]",
                IssueSeverity.Error,
                "NPC journal references an unknown NPC.",
                code: "npc_journal_unknown_npc_reference",
                actor: "Ночной посыльный",
                section: "NPCJournals",
                expected: "existing NPCId/NPCName from pre-turn/current npc_core.json",
                actual: "npc_night_messenger")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_reference_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Ночной посыльный", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("NPCJournals", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("existing NPC", StringComparison.OrdinalIgnoreCase) || step.Contains("full NPC", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcFullObjectErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.NPCs[0]",
                IssueSeverity.Error,
                "Full NPC object is missing required fields.",
                code: "npc_full_object_missing_required_fields",
                actor: "Ирен Соль",
                section: "UpdateNPCs",
                expected: "full NPC object with profile, social, location, relationshipLock, goals, personalityTraits, attitude, culturalStance",
                actual: "missing worldview, race, class, appearanceDescription, inventory")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_full_object_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Ирен Соль", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("inventory", packet.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("worldview", packet.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("full NPC object", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("relationshipLock", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("goals", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("personalityTraits", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("inventory", StringComparison.OrdinalIgnoreCase) && step.Contains("[]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues_UsesExplicitBoundaryWithoutLegacyRequest()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Старый ответ до ремонта.",
            timestamp = "2026-07-10T07:30:00Z"
        });
        var boundary = DateTime.UtcNow.AddMinutes(1);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/narrative_response.json"), boundary.AddMinutes(-1));
        _fs.DeleteFile("game_state/control/validation_repair_request.json");
        var repairedErrors = new List<ValidationIssue>
        {
            new(
                "game_state/world/weather.json",
                IssueSeverity.Error,
                "Canonical weather was repaired.",
                code: "normalized_weather_missing_description")
        };
        var engine = CreateGameEngine();
        var method = typeof(GameEngine).GetMethod(
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var issues = Assert.IsType<List<ValidationIssue>>(method!.Invoke(
            engine,
            new object[] { repairedErrors, boundary }));

        Assert.Contains(issues, issue =>
            string.Equals(
                issue.Code,
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues_EqualBoundaryIsStale()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Ответ записан одновременно с canonical repair.",
            timestamp = "2026-07-10T07:30:00Z"
        });
        var boundary = new DateTime(2026, 7, 10, 7, 31, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(_fs.ResolvePath("output/narrative_response.json"), boundary);
        var repairedErrors = new List<ValidationIssue>
        {
            new(
                "game_state/world/weather.json",
                IssueSeverity.Error,
                "Canonical weather was repaired.",
                code: "normalized_weather_missing_description")
        };
        var engine = CreateGameEngine();

        var issues = InvokePrivateValue<List<ValidationIssue>>(
            engine,
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            repairedErrors,
            boundary);

        Assert.Contains(issues, issue =>
            string.Equals(
                issue.Code,
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveCanonicalRepairOutputFreshnessBoundaryUtc_UsesLatestActualTargetWrite()
    {
        const string firstTarget = "game_state/world/weather.json";
        const string secondTarget = "game_state/meta/soul_state.json";
        await WriteJsonAsync(firstTarget, new { description = "Туман рассеялся." });
        await WriteJsonAsync(secondTarget, new { soulFormDescription = "Серебристый силуэт." });

        var repairStartedAtUtc = DateTime.UtcNow.AddMinutes(-3);
        var firstWriteUtc = repairStartedAtUtc.AddMinutes(1);
        var latestWriteUtc = repairStartedAtUtc.AddMinutes(2);
        File.SetLastWriteTimeUtc(_fs.ResolvePath(firstTarget), firstWriteUtc);
        File.SetLastWriteTimeUtc(_fs.ResolvePath(secondTarget), latestWriteUtc);

        var repairedErrors = new List<ValidationIssue>
        {
            new(
                $"{firstTarget}.description",
                IssueSeverity.Error,
                "Weather repaired.",
                code: "normalized_weather_missing_description"),
            new(
                $"{secondTarget}.soulFormDescription",
                IssueSeverity.Error,
                "Soul form repaired.",
                code: "soul_form_description_invalid_shape")
        };

        var boundary = InvokePrivateValue<DateTime>(
            CreateGameEngine(),
            "ResolveCanonicalRepairOutputFreshnessBoundaryUtc",
            repairedErrors,
            repairStartedAtUtc);

        Assert.Equal(latestWriteUtc, boundary);
    }

    [Fact]
    public async Task ResolveCanonicalRepairOutputFreshnessBoundaryUtc_UnobservableTargetFailsOutputFreshnessClosed()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Ответ создан до ненаблюдаемого canonical repair.",
            timestamp = "2026-07-10T07:30:00Z"
        });
        var repairStartedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/narrative_response.json"),
            repairStartedAtUtc);
        var repairedErrors = new List<ValidationIssue>
        {
            new(
                "game_state/world/missing_canonical_target.json.value",
                IssueSeverity.Error,
                "Canonical target repair could not be observed.",
                code: "missing_canonical_target_repaired_elsewhere")
        };
        var engine = CreateGameEngine();

        var boundary = InvokePrivateValue<DateTime>(
            engine,
            "ResolveCanonicalRepairOutputFreshnessBoundaryUtc",
            repairedErrors,
            repairStartedAtUtc);
        var issues = InvokePrivateValue<List<ValidationIssue>>(
            engine,
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            repairedErrors,
            boundary);

        Assert.True(boundary > repairStartedAtUtc);
        Assert.Contains(issues, issue =>
            string.Equals(
                issue.Code,
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_ActorMaterializationErrors_AddsBoundedHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.UpdateNPCs[0].materialization.sections.inventory",
                IssueSeverity.Error,
                "Первичная материализация не объясняет секцию inventory.",
                code: "actor_materialization_section_missing",
                actor: "mortal_npc:npc_iren_sol",
                section: "inventory",
                expected: "populated or empty_by_design with reason",
                actual: "missing"),
            new(
                "game_state/meta/guardians.json.guardians[0]",
                IssueSeverity.Error,
                "Новая значимая сущность посмертия не связана с точным common profile.",
                code: "afterlife_actor_materialization_profile_missing",
                actor: "guardian:guardian_selena",
                section: "ActorMaterialization",
                expected: "exact guardian:guardian_selena profile in game_state/meta/afterlife_entity_profiles.json",
                actual: "no exact actorType + actorId profile"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].materialization.sections.fateCards",
                IssueSeverity.Error,
                "Disposition секции fateCards противоречит её каноническому содержимому.",
                code: "actor_materialization_section_content_mismatch",
                actor: "guardian:guardian_selena",
                section: "fateCards",
                expected: "empty_by_design with reason",
                actual: "populated")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Equal(
            new[]
            {
                "game_state/meta/afterlife_entity_profiles.json",
                "game_state/npcs/npc_core.json"
            },
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "guardian:guardian_selena", "mortal_npc:npc_iren_sol" },
            packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()).OrderBy(item => item, StringComparer.Ordinal));

        var missingFields = packet.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(missingFields, item => item.Contains("mortal_npc:npc_iren_sol", StringComparison.Ordinal) &&
                                               item.Contains("inventory", StringComparison.Ordinal));
        Assert.Contains(missingFields, item => item.Contains("guardian:guardian_selena", StringComparison.Ordinal) &&
                                               item.Contains("profile", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(missingFields, item => item.Contains("fateCards", StringComparison.Ordinal));

        var corrections = packet.GetProperty("exactFieldCorrections").EnumerateArray().ToArray();
        Assert.Equal(3, corrections.Length);
        Assert.Contains(corrections, correction =>
            correction.GetProperty("code").GetString() == "actor_materialization_section_content_mismatch" &&
            correction.GetProperty("path").GetString()!.EndsWith("sections.fateCards", StringComparison.Ordinal));

        var safeRules = packet.GetProperty("safeCorrectionRules").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(safeRules, rule => rule.Contains("only", StringComparison.OrdinalIgnoreCase) &&
                                          rule.Contains("listed", StringComparison.OrdinalIgnoreCase) &&
                                          rule.Contains("section", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(safeRules, rule => rule.Contains("preserve", StringComparison.OrdinalIgnoreCase) &&
                                          rule.Contains("valid", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("implementation code", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("whole actor", StringComparison.OrdinalIgnoreCase) ||
                                         item.Contains("entire actor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("delete", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("name", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("prose", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("genre", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("client", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("invent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_UnusableActorMaterializationPreTurnAuthority_DoesNotAskGmToRewriteActors()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_entity_profiles.json",
                IssueSeverity.Error,
                "Validated pre-turn authority отсутствует или повреждена.",
                code: "actor_materialization_pre_turn_authority_unusable",
                section: "ActorMaterialization",
                expected: "readable validated pre-turn afterlife actor authority",
                actual: "missing, unreadable, hash-invalid, or ambiguous source authority")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        Assert.Empty(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
    }

    [Theory]
    [InlineData("actor_materialization_missing")]
    [InlineData("actor_materialization_invalid_envelope")]
    [InlineData("actor_materialization_actor_binding_mismatch")]
    [InlineData("actor_materialization_duplicate_id")]
    [InlineData("actor_materialization_duplicate_property")]
    [InlineData("actor_materialization_invalid_actor_type")]
    [InlineData("actor_materialization_inventory_reference_mismatch")]
    [InlineData("actor_materialization_section_missing")]
    [InlineData("actor_materialization_section_content_mismatch")]
    [InlineData("actor_materialization_capability_mismatch")]
    [InlineData("actor_materialization_existing_resend_forbidden")]
    [InlineData("actor_materialization_historical_envelope_changed")]
    [InlineData("afterlife_actor_materialization_profile_missing")]
    [InlineData("afterlife_actor_materialization_profile_ambiguous")]
    [InlineData("afterlife_actor_materialization_memory_missing")]
    public async Task WriteValidationRepairRequestAsync_ActionableActorMaterializationCode_IsClassified(
        string code)
    {
        var isAfterlife = code.StartsWith("afterlife_", StringComparison.Ordinal);
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                isAfterlife
                    ? "game_state/meta/guardians.json.guardians[0]"
                    : "game_state/npcs/npc_core.json.UpdateNPCs[0].materialization",
                IssueSeverity.Error,
                "Actor Materialization validation error.",
                code: code,
                actor: isAfterlife ? "guardian:guardian_classification" : "mortal_npc:npc_classification",
                section: "ActorMaterialization",
                expected: "valid bounded target",
                actual: "invalid")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 1 })!);

        await task;

        using var doc = JsonDocument.Parse(
            (await _fs.ReadFileAsync("game_state/control/validation_repair_request.json"))!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("actor_materialization_repair", packet.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcSkillStringShapeErrors_AddsFullObjectHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.NPCsInScene[0].activeSkills[0]",
                IssueSeverity.Error,
                "Элемент должен быть объектом",
                code: "expected_object",
                actor: "Старый Мирон",
                section: "NPC",
                expected: "JSON object",
                actual: "String",
                repairHint: "Исправь элемент до JSON object перед заполнением его обязательных полей."),
            new(
                "game_state/npcs/npc_core.json.NPCsInScene[0].passiveSkills[0]",
                IssueSeverity.Error,
                "Элемент должен быть объектом",
                code: "expected_object",
                actor: "Старый Мирон",
                section: "NPC",
                expected: "JSON object",
                actual: "String",
                repairHint: "Исправь элемент до JSON object перед заполнением его обязательных полей.")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_full_object_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Старый Мирон", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("activeSkills/passiveSkills", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("full skill objects", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("activeSkills/passiveSkills", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("string names", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalBootstrapMaterializationErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "lore/codex_entries.json",
                IssueSeverity.Error,
                "Opening Mortal bootstrap must include current-world codex entries.",
                code: "bootstrap_codex_missing_current_world_entries",
                section: "MortalBootstrap"),
            new(
                "game_state/inventory/items.json.items[0]",
                IssueSeverity.Error,
                "Readable document item has no detail authority.",
                code: "readable_document_missing_detail_authority",
                section: "Inventory",
                actual: "item_letter_black_seal"),
            new(
                "game_state/inventory/items.json.items[1].quality",
                IssueSeverity.Error,
                "Item quality is not canonical.",
                code: "item_invalid_quality",
                section: "Inventory",
                actual: "обычное"),
            new(
                "game_state/factions/faction_core.json.factions[0]",
                IssueSeverity.Error,
                "Faction sidecar is missing required fields.",
                code: "canonical_faction_custom_state_missing_required_fields",
                section: "Faction"),
            new(
                "game_state/world/current_location.json.coordinates",
                IssueSeverity.Error,
                "Current location coordinates differ from world map.",
                code: "current_location_coordinates_mismatch",
                section: "Location"),
            new(
                "game_state/world/current_location.json.factionControl",
                IssueSeverity.Error,
                "Location faction control must be object-shaped.",
                code: "location_faction_control_invalid_type",
                section: "Location"),
            new(
                "lore/current_world/history.json",
                IssueSeverity.Error,
                "Mortal bootstrap reused previous world lore.",
                code: "mortal_bootstrap_reused_previous_world_lore",
                section: "MortalBootstrap"),
            new(
                "game_state/npcs/npc_core.json",
                IssueSeverity.Error,
                "Mortal bootstrap promised training but has no usable teacherProfile.",
                code: "mortal_bootstrap_requested_teacher_missing",
                section: "MortalBootstrap"),
            new(
                "game_state/player/skills_active.json.activeSkillChanges",
                IssueSeverity.Error,
                "Mortal bootstrap lost an explicit starter competency.",
                code: "mortal_bootstrap_explicit_competency_missing",
                section: "MortalBootstrap"),
            new(
                "game_state/world/world_events.json.worldEventsLog",
                IssueSeverity.Error,
                "Mortal bootstrap lost its opening world event.",
                code: "mortal_bootstrap_world_event_missing",
                section: "MortalBootstrap")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_bootstrap_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/control/mortal_bootstrap_scaffold.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("lore/codex_entries.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/inventory/items.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/factions/faction_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/world/current_location.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/player/skills_active.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("game_state/world/world_events.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("bootstrap_codex_missing_current_world_entries", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("readable_document_missing_detail_authority", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("canonical_faction_custom_state_missing_required_fields", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("teacherProfile.canTeach=true", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("starterCompetencyRequirements", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("worldEventRequirements", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("mortal_bootstrap_scaffold.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("current-world", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("readable", StringComparison.OrdinalIgnoreCase) && step.Contains("document", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("requested training", StringComparison.OrdinalIgnoreCase) && step.Contains("teacherProfile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("starter competencies", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("opening world news", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalBootstrapTradeAnchorMissing_AddsMerchantHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json",
                IssueSeverity.Error,
                "Mortal bootstrap promised trade but has no usable merchant NPC.",
                code: "mortal_bootstrap_requested_trade_missing",
                section: "MortalBootstrap",
                expected: "NPCsInScene/UpdateNPCs entry with tradeState.canTrade=true and a valid merchant profile",
                actual: "missing usable tradeState")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_bootstrap_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("mortal_bootstrap_requested_trade_missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("tradeState.canTrade=true", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("merchantProfile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("tradeInventory", StringComparison.OrdinalIgnoreCase) && item.Contains("object", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("requested trade", StringComparison.OrdinalIgnoreCase) && step.Contains("tradeState", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("tradeBlockedReason", StringComparison.OrdinalIgnoreCase) && step.Contains("string", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("do not include inventory", StringComparison.OrdinalIgnoreCase) && step.Contains("UpdateNPCs", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("tradeInventory", StringComparison.OrdinalIgnoreCase) && item.Contains("scalar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("promised trader", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalBootstrapItemShapeAndActorPersistenceErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/inventory/items.json.items[0].image_prompt",
                IssueSeverity.Error,
                "Отсутствует обязательное строковое поле: image_prompt",
                code: "missing_required_string",
                expected: "image_prompt as non-empty string",
                actual: "missing"),
            new(
                "game_state/inventory/items.json.items[0].isContainer",
                IssueSeverity.Error,
                "Отсутствует обязательное boolean поле: isContainer",
                code: "missing_required_boolean_field",
                expected: "boolean",
                actual: "missing"),
            new(
                "game_state/inventory/items.json.items[0].durability",
                IssueSeverity.Error,
                "durability должен быть percentage string",
                code: "validation_error"),
            new(
                "game_state/inventory/items.json.items[0].journalEntries[0]",
                IssueSeverity.Error,
                "Элемент должен быть непустой строкой",
                code: "invalid_string_array_item",
                section: "Inventory",
                expected: "non-empty string",
                actual: "Object"),
            new(
                "game_state/inventory/items.json.items[0].equipmentSlot",
                IssueSeverity.Error,
                "equipmentSlot содержит неизвестный слот.",
                code: "item_invalid_equipment_slot",
                section: "Inventory",
                expected: "valid equipment slot",
                actual: "Pocket"),
            new(
                "game_state/inventory/items.json.items[0].accessoryForSlot",
                IssueSeverity.Error,
                "accessoryForSlot содержит неизвестный слот.",
                code: "item_invalid_accessory_slot",
                section: "Inventory",
                expected: "valid accessory slot",
                actual: "Hands"),
            new(
                "lore/codex_entries.json.entries[0].tags",
                IssueSeverity.Error,
                "tags должен быть массивом строк.",
                code: "expected_string_array",
                section: "Codex",
                expected: "string[]",
                actual: "String"),
            new(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Mortal World relevant actor 'Дом Вальмонт' declared in NPC scope but has no persistent NPC surface",
                code: "mortal_relevant_actor_missing_persistence",
                actor: "Дом Вальмонт",
                section: "npc_scope",
                expected: "matching NPC persistence",
                actual: "actor appears only in gm_thoughts_markdown / narrative reasoning")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "ответа GM", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packets = doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray().ToArray();
        Assert.Equal(2, packets.Length);
        var packet = Assert.Single(packets, item =>
            string.Equals(item.GetProperty("kind").GetString(), "mortal_bootstrap_materialization_repair", StringComparison.Ordinal));
        Assert.Equal("mortal_bootstrap_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/inventory/items.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("lore/codex_entries.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("missing_required_string", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("missing_required_boolean_field", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("item_invalid_accessory_slot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item =>
            item.Contains("durability", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("percentage string", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("100%", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item =>
            item.Contains("journalEntries", StringComparison.Ordinal) &&
            item.Contains("non-empty string", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("not objects", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("image_prompt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("isContainer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("existedId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step =>
            step.Contains("durability", StringComparison.OrdinalIgnoreCase) &&
            step.Contains("100%", StringComparison.Ordinal));
        Assert.Contains(steps, step =>
            step.Contains("journalEntries", StringComparison.Ordinal) &&
            step.Contains("array", StringComparison.OrdinalIgnoreCase) &&
            step.Contains("strings", StringComparison.OrdinalIgnoreCase) &&
            step.Contains("not objects", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Relevant actors", StringComparison.OrdinalIgnoreCase));

        var npcPacket = Assert.Single(packets, item =>
            string.Equals(item.GetProperty("kind").GetString(), "mortal_npc_scope_repair", StringComparison.Ordinal));
        Assert.Contains("game_state/npcs/npc_core.json", npcPacket.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("output/debug_logs.json", npcPacket.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Дом Вальмонт", npcPacket.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
        var npcSteps = npcPacket.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(npcSteps, step => step.Contains("mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalNpcRelationshipAndCulturalErrors_AddsHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/npcs/npc_core.json.NPCs[0].attitude",
                IssueSeverity.Error,
                "NPC attitude must match relationship tier.",
                code: "npc_attitude_relationship_tier_mismatch",
                actor: "Ренар",
                section: "UpdateNPCs",
                expected: "attitude derived from relationshipLevel",
                actual: "Friendly"),
            new(
                "game_state/npcs/npc_core.json.NPCs[0].culturalStance",
                IssueSeverity.Error,
                "NPC culturalStance must be canonical.",
                code: "npc_invalid_cultural_stance",
                actor: "Ренар",
                section: "UpdateNPCs",
                expected: "Conformist|Pragmatist|Dissident",
                actual: "Loyalist")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "обработки хода", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("mortal_npc_relationship_enum_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("game_state/npcs/npc_core.json", packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Templates/MORTAL_NPC_UPDATE_TEMPLATE.md", packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Ренар", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("Непримиримый Враг", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Conformist", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Pragmatist", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Dissident", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeActionCostSequenceErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog[2].actionCostAudit.opposition.before",
                IssueSeverity.Error,
                "Последовательные текущие обмены должны расходовать/восстанавливать ОД от результата предыдущего обмена.",
                code: "afterlife_conflict_action_cost_sequence_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: "2",
                actual: "4"),
            new(
                "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog[2].actionCostAudit.player.before",
                IssueSeverity.Error,
                "Последовательные текущие обмены должны расходовать/восстанавливать ОД от результата предыдущего обмена.",
                code: "afterlife_conflict_action_cost_sequence_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: "0",
                actual: "3"),
            new(
                "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog[2].actionCostAudit.player.after",
                IssueSeverity.Error,
                "actionCostAudit.player.after должен точно равняться before - effectiveCost.",
                code: "afterlife_conflict_action_cost_delta_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: "0",
                actual: "4"),
            new(
                "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict.exchangeLog[2].actionCostAudit.opposition.after",
                IssueSeverity.Error,
                "actionCostAudit.opposition.after должен точно равняться before - effectiveCost.",
                code: "afterlife_conflict_opposition_action_cost_delta_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: "2",
                actual: "3")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 4 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_spiritual_conflict_action_cost_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains(
            "game_state/meta/afterlife_spiritual_conflict_state.json",
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("exchangeLog[2]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("opposition.before", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("expected 2", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("actual 4", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("player.before", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("expected 0", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("actual 3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("player.after", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("expected 0", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("actual 4", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("opposition.after", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("expected 2", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("actual 3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));

        var exactFieldCorrections = packet.GetProperty("exactFieldCorrections").EnumerateArray().ToArray();
        Assert.Contains(exactFieldCorrections, correction =>
            correction.GetProperty("path").GetString()!.Contains("exchangeLog[2].actionCostAudit.player.after", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("expected").GetString(), "0", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("actual").GetString(), "4", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("code").GetString(), "afterlife_conflict_action_cost_delta_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(exactFieldCorrections, correction =>
            correction.GetProperty("path").GetString()!.Contains("exchangeLog[2].actionCostAudit.opposition.after", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("expected").GetString(), "2", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("actual").GetString(), "3", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(correction.GetProperty("code").GetString(), "afterlife_conflict_opposition_action_cost_delta_mismatch", StringComparison.OrdinalIgnoreCase));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("actionCostAudit.<side>.before", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("before - effectiveCost", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("Do not create a new turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("pending_turn_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeChronicleStringArrayErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_chronicles.json.chronicles[0].persistentConsequences[0]",
                IssueSeverity.Error,
                "persistentConsequences entries должны быть непустыми строками.",
                code: "afterlife_chronicle_persistent_consequences_entry_invalid",
                section: "AfterlifeChronicles",
                expected: "non-empty string",
                actual: "Object"),
            new(
                "game_state/meta/afterlife_chronicles.json.chronicles[0].openThreads[0]",
                IssueSeverity.Error,
                "openThreads entries должны быть непустыми строками.",
                code: "afterlife_chronicle_open_threads_entry_invalid",
                section: "AfterlifeChronicles",
                expected: "non-empty string",
                actual: "Object")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "ответа GM", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_chronicle_string_array_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("high", packet.GetProperty("priority").GetString());
        Assert.Contains(
            "game_state/meta/afterlife_chronicles.json",
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(
            "Examples/E_CLI_Afterlife_Turns.txt",
            packet.GetProperty("templateRefs").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("afterlifeChronicleUpdates[]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("persistentConsequences[]", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("openThreads[]", StringComparison.Ordinal));
        Assert.Contains(expectedShape, item => item.Contains("array of non-empty strings", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("persistentConsequences[0]", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("Object", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("openThreads[0]", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("Object", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Remove afterlifeChronicleUpdates from output/narrative_response.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(steps, step => step.Contains("keep the same shape there too", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("validation_repair_ready.json", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("Do not create a new turn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("eventDescriptions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("afterlifeChronicleUpdates", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("narrative_response.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeConflictRewardNotAllowed_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_spiritual_conflict_state.json.recentConflicts[0].rewardAudit",
                IssueSeverity.Error,
                "Этот terminal afterlife conflict outcome не может выдавать currency reward.",
                code: "afterlife_conflict_reward_not_allowed",
                section: "AfterlifeSpiritualConflict",
                expected: "resolved contested player victory with diceAudit.outcomeBand=player_success|decisive_player_success",
                actual: "terminalOutcome=negotiated_training; diceAudit.outcomeBand=negotiated")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_spiritual_conflict_reward_repair", packet.GetProperty("kind").GetString());
        Assert.Contains(
            "game_state/meta/afterlife_spiritual_conflict_state.json",
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("player_success", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("decisive_player_success", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("negotiated", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("rewardAudit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("remove", StringComparison.OrdinalIgnoreCase) &&
                                      step.Contains("currency reward", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("upgrade", StringComparison.OrdinalIgnoreCase) &&
                                         item.Contains("negotiated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("new turn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeEntityProfileScaffoldErrors_AddsConcreteHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].goals",
                IssueSeverity.Error,
                "goals профиля духовной сущности должен быть object.",
                code: "afterlife_entity_profile_agency_goals_not_object",
                actor: "Душа игрока",
                section: "AfterlifeEntityProfiles",
                expected: "object",
                actual: "Array"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].progressionStrategy",
                IssueSeverity.Error,
                "Профиль должен явно хранить progressionStrategy.",
                code: "afterlife_entity_profile_missing_progression_strategy",
                actor: "Душа игрока",
                section: "AfterlifeEntityProfiles",
                expected: "object with strategyId/summary/priorityOrder/resourceReserve/allowedSpends/forbiddenSpends"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].ledger",
                IssueSeverity.Error,
                "Профиль должен явно хранить ledger.",
                code: "afterlife_entity_profile_missing_ledger",
                actor: "Душа игрока",
                section: "AfterlifeEntityProfiles",
                expected: "array"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profileCommands.specialArtLearningReceipts[0]",
                IssueSeverity.Error,
                "specialArtLearningReceipts entry incomplete.",
                code: "incomplete_special_art_learning_receipt",
                actor: "Душа игрока",
                section: "AfterlifeEntityProfiles",
                expected: "receiptId/artId/teacherActorType/teacherActorId/playerActorId/trainingConditionSatisfied/learnedAtTurn/roleplayEvidence/summary")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "повторной проверки repair", issues, 2 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_entity_profile_scaffold_repair", packet.GetProperty("kind").GetString());
        Assert.Contains(
            "game_state/meta/afterlife_entity_profiles.json",
            packet.GetProperty("targetFiles").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Душа игрока", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));

        var expectedShape = packet.GetProperty("expectedShape").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(expectedShape, item => item.Contains("goals", StringComparison.OrdinalIgnoreCase) &&
                                              item.Contains("goalId", StringComparison.OrdinalIgnoreCase) &&
                                              item.Contains("updatedAtTurn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("progressionStrategy", StringComparison.OrdinalIgnoreCase) &&
                                              item.Contains("priorityOrder", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("ledger", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(expectedShape, item => item.Contains("specialArtLearningReceipts", StringComparison.OrdinalIgnoreCase) &&
                                              item.Contains("initialTier", StringComparison.OrdinalIgnoreCase));

        var steps = packet.GetProperty("steps").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(steps, step => step.Contains("minimum profile scaffold", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("profileCommands.specialArtLearningReceipts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(steps, step => step.Contains("Complete-BoeValidationRepair", StringComparison.OrdinalIgnoreCase));

        var doNotDo = packet.GetProperty("doNotDo").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Contains(doNotDo, item => item.Contains("Mortal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(doNotDo, item => item.Contains("initialTier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteValidationRepairRequestAsync_AfterlifeEntityProfileActivityAndRelationshipErrors_AddsScaffoldHarnessPacket()
    {
        var engine = CreateGameEngine();
        var issues = new List<ValidationIssue>
        {
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].currentActivity.activityType",
                IssueSeverity.Error,
                "currentActivity должен явно хранить тип активности.",
                code: "afterlife_entity_profile_agency_activity_missing_type",
                actor: "Хранительница Селена",
                section: "AfterlifeEntityProfiles",
                expected: "activityType"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].currentActivity.linkedQuestId",
                IssueSeverity.Error,
                "currentActivity должен ссылаться на квест.",
                code: "afterlife_entity_profile_agency_activity_missing_linked_quest_id",
                actor: "Хранительница Селена",
                section: "AfterlifeEntityProfiles",
                expected: "linkedQuestId"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].relationshipLock",
                IssueSeverity.Error,
                "relationshipLock должен хранить направление, evidence и reason.",
                code: "afterlife_entity_profile_relationship_lock_missing_direction",
                actor: "Хранительница Селена",
                section: "AfterlifeEntityProfiles",
                expected: "direction/evidence/reason"),
            new(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0].relationships[0].updatedAtTurn",
                IssueSeverity.Error,
                "relationship turn должен быть числом.",
                code: "afterlife_entity_profile_relationship_invalid_turn",
                actor: "Хранительница Селена",
                section: "AfterlifeEntityProfiles",
                expected: "non-negative integer")
        };

        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[] { "первого хода после создания души", issues, 1 })!);

        await task;

        var requestJson = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var doc = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(doc.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal("afterlife_entity_profile_scaffold_repair", packet.GetProperty("kind").GetString());
        Assert.Contains("Хранительница Селена", packet.GetProperty("canonicalActorNames").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task TryAutoRepairStartupGuardianDirectMaterializationAsync_AddsCreateAuthorityAndClearsPending()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_freeform_selena",
              "canonicalName": "Хранительница Селена",
              "originType": "freeform",
              "domain": "Забытые библиотеки",
              "nameVariants": { "default": "Хранительница Селена", "feminine": "Хранительница Селена", "masculine": "Хранитель Селен", "neutral": "Селена" },
              "manifestation": {
                "currentDisplayName": "Хранительница Селена",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Женщина в тёмном плаще архивариуса."
              },
              "manifestationHistory": [],
              "abode": { "abodeId": "abode_selena_archive", "name": "Башня архивов", "isDiscovered": true },
              "personalityProfile": { "archetype": "Строгая наставница", "speechPattern": "сухо и точно", "coreValues": [ "память", "осторожность", "правда" ] },
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 10, "tier": "Угасающая", "lastUpdatedAt": "2026-07-06T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] },
              "mood": { "current": "focused", "intensity": 40, "reason": "Первая встреча.", "since": 1 },
              "loreFragments": [
                { "fragmentId": "selena_lore_1", "category": "personal_history", "title": "Первый урок", "content": "Селена не скрывает цену знания.", "requiredReputation": 0 },
                { "fragmentId": "selena_lore_2", "category": "domain_mastery", "title": "Архивы", "content": "Её домен связан с забытыми библиотеками.", "requiredReputation": 0 },
                { "fragmentId": "selena_lore_3", "category": "soul_mechanics", "title": "Память души", "content": null, "requiredReputation": 50 },
                { "fragmentId": "selena_lore_4", "category": "lost_world", "title": "Серый берег", "content": null, "requiredReputation": 50 },
                { "fragmentId": "selena_lore_5", "category": "other_guardians", "title": "Долги", "content": null, "requiredReputation": 130 },
                { "fragmentId": "selena_lore_6", "category": "cosmic_secret", "title": "Последняя полка", "content": null, "requiredReputation": 130 },
                { "fragmentId": "selena_lore_7", "category": "personal_history", "title": "Скрытое имя", "content": null, "requiredReputation": 230 }
              ],
              "musings": [
                { "turn": 1, "topic": "first_soul_assessment", "mood": "contemplative", "thought": "Новая душа требует осторожного первого урока." }
              ]
            }
          ],
          "activeGuardian": { "guardianId": "guard_freeform_selena" },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_selena_archive",
            "currentGuardianId": "guard_freeform_selena",
            "discoveredAbodes": [ "abode_selena_archive" ]
          },
          "pendingGuardianCreation": {
            "mode": "freeform",
            "soulName": "Искра Перед Рассветом",
            "description": "Хранительница Селена, покровительница забытых библиотек."
          }
        }
        """);
        var engine = CreateGameEngine();
        var errors = new List<ValidationIssue>
        {
            new(
                "game_state/meta/guardians.json.guardians[0].guardianId",
                IssueSeverity.Error,
                "Fresh startup Guardian was materialized without the supported create surface.",
                code: "guardian_materialized_without_create_surface",
                section: "Guardians",
                expected: "UpdateGuardians.create",
                actual: "direct guardians[] entry"),
            new(
                "game_state/meta/guardians.json.pendingGuardianCreation",
                IssueSeverity.Error,
                "pendingGuardianCreation remains after Guardian materialization.",
                code: "stale_pending_guardian_creation_after_materialization",
                section: "Guardians",
                expected: "pendingGuardianCreation removed",
                actual: "pending request still present")
        };

        var repaired = await InvokePrivateAsync<bool>(
            engine,
            "TryAutoRepairStartupGuardianDirectMaterializationAsync",
            "первого хода после создания души",
            errors);

        Assert.True(repaired);
        var repairedJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.False(string.IsNullOrWhiteSpace(repairedJson));
        using var doc = JsonDocument.Parse(repairedJson!);
        var root = doc.RootElement;
        var createCommand = Assert.Single(root.GetProperty("UpdateGuardians").EnumerateArray());
        Assert.Equal("create", createCommand.GetProperty("command").GetString());
        Assert.Equal(
            "guard_freeform_selena",
            createCommand.GetProperty("data").GetProperty("guardianId").GetString());
        Assert.Equal(
            "soul_assessment",
            createCommand.GetProperty("data").GetProperty("musings")[0].GetProperty("topic").GetString());
        Assert.False(root.TryGetProperty("pendingGuardianCreation", out var pending) && pending.ValueKind != JsonValueKind.Null);
        Assert.Equal(
            "guard_freeform_selena",
            root.GetProperty("activeGuardian").GetProperty("guardianId").GetString());
    }


    [Theory]
    [InlineData("[ABODE_OFFERING] Игрок подносит Реликвию Души.", true)]
    [InlineData("[INK_FEATHER_ACTION: ABODE_OFFERING] Игрок подносит 100 Чернильных Перьев.", true)]
    [InlineData("[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] Игрок жертвует 60 Чернильных Перьев.", false)]
    public void IsPendingAbodeOfferingTurnAction_DetectsPlainAndInkFeatherOfferingTags(string action, bool expected)
    {
        Assert.Equal(expected, GameEngine.IsPendingAbodeOfferingTurnAction(action));
    }

    [Fact]
    public async Task ProcessPlayerTurn_UnresolvedRealm_DoesNotCreatePendingDiceState()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        var engine = CreateGameEngine();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Тестовый ход", null));

        Assert.Contains("currentRealm", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_InvalidCurrentState_BlocksBeforeDispatchingGm()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            inkFeathers = new { current = 50 },
            afterlifeArchive = new
            {
                stored = new object[]
                {
                    new
                    {
                        archiveId = "archive_missing_source_life",
                        entryType = AfterlifeArchiveState.EntryTypeLoreFragment,
                        title = "Запись без жизни-источника",
                        summary = "Эта запись должна блокировать ход до вызова GM.",
                        rarity = "Rare",
                        sourceKind = AfterlifeArchiveState.SourceKindCodex,
                        acquiredAtUtc = "2026-06-18T00:00:00Z"
                    }
                },
                actionReceipts = Array.Empty<object>()
            }
        });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]));

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Тестовый ход", null);

        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_TerminalError_CleansTurnRequestAndPendingSnapshot()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmErrorTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                error = "GM bridge did not accept dispatch before the dispatch timeout."
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Тестовый ход, который завершится terminal error", null);
        await gmErrorTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_RepairStalledAcceptedTurn_RestoresPreTurnSnapshot()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        const string passiveSkillsPath = "game_state/player/skills_passive.json";
        const string activeSkillsPath = "game_state/player/skills_active.json";
        const string skillMasteryPath = "game_state/player/skill_mastery.json";

        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmOutputTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var outputTimestamp = DateTime.UtcNow.ToString("o");
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Наставница показывает печать, но запись урока намеренно повреждена для проверки rollback.",
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = new[]
                {
                    new
                    {
                        text = "Вернуться к уроку позже.",
                        category = "neutral"
                    }
                },
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/debug_logs.json", new
            {
                timestamp = outputTimestamp,
                gm_thoughts_markdown = string.Join(
                    "\n",
                    "## NPC Scope",
                    "- Mode: Scene-local",
                    "- Relevant actors: наставница архива",
                    "- Why relevant: Проверяется провал repair loop после paid training.",
                    "- Actors outside scope: нет",
                    "- Why outside scope: Ход не меняет других NPC.",
                    "",
                    "## Reasoning",
                    "- The test intentionally writes a passive skill and an invalid active mastery entry.")
            });
            await WriteJsonAsync(passiveSkillsPath, new
            {
                passiveSkillChanges = new[]
                {
                    new
                    {
                        skillId = "skill_life_001_seal_reading",
                        skillName = "Чтение печатей",
                        skillKind = "passive_skill_mastery",
                        description = "Распознавание родовых и торговых печатей.",
                        rarity = "Common",
                        category = "knowledge"
                    }
                }
            });
            await WriteJsonAsync(activeSkillsPath, new
            {
                activeSkillChanges = Array.Empty<object>()
            });
            await WriteJsonAsync(skillMasteryPath, new
            {
                skillMasteryChanges = new[]
                {
                    new
                    {
                        skillId = "skill_life_001_seal_reading",
                        skillName = "Чтение печатей",
                        targetKind = "passive_skill_mastery",
                        newMasteryLevel = 1,
                        newCurrentMasteryProgress = 0,
                        newMasteryProgressNeeded = 100,
                        masteryLeveledUp = true
                    }
                }
            });
            await WriteJsonAsync("ready/turn_complete.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "success",
                filesModified = new[]
                {
                    "output/narrative_response.json",
                    "output/interface_updates.json",
                    "output/debug_logs.json",
                    passiveSkillsPath,
                    activeSkillsPath,
                    skillMasteryPath
                }
            });

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!_fs.FileExists("game_state/control/validation_repair_request.json") && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.True(_fs.FileExists("game_state/control/validation_repair_request.json"));
            await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
            {
                isStalled = true,
                elapsedSeconds = 180,
                bridgeCleanup = new
                {
                    reason = "gm_validation_repair_artifact_stall",
                    status = "fallback-stopped",
                    ok = true
                }
            });
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Купить урок чтения печатей.", null);
        await gmOutputTask.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.False(_fs.FileExists(passiveSkillsPath));
        Assert.False(_fs.FileExists(activeSkillsPath));
        Assert.False(_fs.FileExists(skillMasteryPath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_IdleBridgeErrorWithFreshOutputArtifacts_RecoversAsAcceptedTurn()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmOutputTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var outputTimestamp = DateTime.UtcNow.ToString("o");
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Книга напоминает: первые Чернильные Перья обычно приходят после завершения первой смертной жизни.",
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = new[]
                {
                    new
                    {
                        text = "Перейти к выбору первой смертной жизни.",
                        category = "neutral"
                    }
                },
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/debug_logs.json", new
            {
                timestamp = outputTimestamp,
                gm_thoughts_markdown = string.Join(
                    "\n",
                    "## NPC Scope",
                    "- Mode: Scene-local",
                    "- Relevant actors: нет",
                    "- Why relevant: Ход отвечает на системный вопрос игрока без действий NPC, Хранителей или фракций.",
                    "- Actors outside scope: нет",
                    "- Why outside scope: В сцене нет акторов, получающих структурное состояние.",
                    "",
                    "## Reasoning",
                    "- Structured actor reasoning is not required for this actor-free explanatory turn.")
            });
            await WriteJsonAsync("ready/turn_error.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                harnessSource = "gm_bridge_idle_without_terminal_signal",
                error = "GM bridge returned to idle without a correlated terminal signal."
            });
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Спросить, как заработать первые Чернильные Перья.", null);
        await gmOutputTask.WaitAsync(TimeSpan.FromSeconds(5));

        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        Assert.Equal(1, gameLoop.TurnNumber);
        var lastResponse = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains("Чернильные Перья", lastResponse.Response, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_OutputWithoutTerminalErrorWithFreshOutputArtifacts_RecoversAsAcceptedTurn()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmOutputTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var outputTimestamp = DateTime.UtcNow.ToString("o");
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Книга принимает записанный ответ, даже если daemon восстановил пропущенный terminal signal.",
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = new[]
                {
                    new
                    {
                        text = "Продолжить путь.",
                        category = "neutral"
                    }
                },
                timestamp = outputTimestamp
            });
            await WriteJsonAsync("output/debug_logs.json", new
            {
                timestamp = outputTimestamp,
                gm_thoughts_markdown = string.Join(
                    "\n",
                    "## NPC Scope",
                    "- Mode: Scene-local",
                    "- Relevant actors: нет",
                    "- Why relevant: Ход проверяет восстановление свежего payload после daemon output-stall.",
                    "- Actors outside scope: нет",
                    "- Why outside scope: Нет акторов со структурным состоянием.",
                    "",
                    "## Reasoning",
                    "- The client can validate fresh output artifacts after daemon reports gm_output_without_terminal_signal.")
            });
            await WriteJsonAsync("ready/turn_error.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                harnessSource = "gm_output_without_terminal_signal",
                error = "GM wrote turn payload files without a correlated terminal signal.",
                changedFiles = new[]
                {
                    "output/narrative_response.json",
                    "output/interface_updates.json",
                    "output/debug_logs.json"
                }
            });
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Проверить восстановление свежего ответа GM.", null);
        await gmOutputTask.WaitAsync(TimeSpan.FromSeconds(5));

        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        Assert.Equal(1, gameLoop.TurnNumber);
        var lastResponse = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains("пропущенный terminal signal", lastResponse.Response, StringComparison.Ordinal);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_IdleBridgeErrorWithoutOutputArtifacts_RemainsFailClosed()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmErrorTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            await WriteJsonAsync("ready/turn_error.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                harnessSource = "gm_bridge_idle_without_terminal_signal",
                error = "GM bridge returned to idle without a correlated terminal signal."
            });
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Спросить, как заработать первые Чернильные Перья.", null);
        await gmErrorTask.WaitAsync(TimeSpan.FromSeconds(5));

        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        Assert.Equal(0, gameLoop.TurnNumber);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task ProcessPlayerTurn_IdleBridgeErrorWithStaleOutputTimestamps_RemainsFailClosed()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var gmOutputTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var staleOutputTimestamp = DateTime.UtcNow.AddMinutes(-10).ToString("o");
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Старый ответ ГМа не должен быть принят новым ходом.",
                timestamp = staleOutputTimestamp
            });
            await WriteJsonAsync("output/debug_logs.json", new
            {
                timestamp = staleOutputTimestamp,
                gm_thoughts_markdown = string.Join(
                    "\n",
                    "## NPC Scope",
                    "- Mode: Scene-local",
                    "- Relevant actors: нет",
                    "- Why relevant: Старый артефакт не относится к текущему ходу.",
                    "- Actors outside scope: нет",
                    "- Why outside scope: Проверяется только свежесть GM output.",
                    "",
                    "## Reasoning",
                    "- Recovery must reject output artifacts whose internal timestamp predates the active turn request.")
            });
            await WriteJsonAsync("ready/turn_error.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                harnessSource = "gm_bridge_idle_without_terminal_signal",
                error = "GM bridge returned to idle without a correlated terminal signal."
            });
        });

        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Спросить, как заработать первые Чернильные Перья.", null);
        await gmOutputTask.WaitAsync(TimeSpan.FromSeconds(5));

        var gameLoop = GetPrivateField<GameLoop>(engine, "_gameLoop");
        Assert.Equal(0, gameLoop.TurnNumber);
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("ready/turn_error.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }

    [Fact]
    public async Task HasCurrentSessionAsync_TerminalSoulDissipation_BlocksContinueSession()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Развеянная Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 3,
            terminalGameOver = new
            {
                state = "soul_dispersed",
                message = AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage,
                conflictId = "afterlife_conflict_terminal_001",
                proofId = "soul_dissipation_proof_terminal_001"
            }
        });
        var engine = CreateGameEngine();

        var hasCurrentSession = await InvokePrivateAsync<bool>(engine, "HasCurrentSessionAsync");

        Assert.False(hasCurrentSession);
        var warning = GetPrivateField<string>(engine, "_mainMenuSessionWarning");
        Assert.Contains(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage, warning, StringComparison.Ordinal);
        Assert.Contains("загрузите сохранение", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDescribeMalformedPendingWorldSetup_RejectsNullWorldDirectives()
    {
        var method = typeof(GameEngine).GetMethod(
            "TryDescribeMalformedPendingWorldSetup",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] args =
        {
            """
            {
              "mode": "manual",
              "worldDirectives": null
            }
            """,
            string.Empty
        };

        var malformed = Assert.IsType<bool>(method!.Invoke(null, args));

        Assert.True(malformed);
        var description = Assert.IsType<string>(args[1]);
        Assert.Contains("worldDirectives", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-null JSON object", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_SystemGuardianAttractionBlocksIncarnation()
    {
        await WriteJsonAsync(SystemGuardianLibraryService.AttractionRequestPath, new
        {
            mode = "system_guardian_attraction",
            targetPresetId = "eternal_tide_001",
            targetPresetDisplayName = "Прилив Памяти",
            targetPresetVersion = "1.0",
            sourceLibrary = "built_in",
            targetSummary = "Извечный Хранитель памяти.",
            renderedPromptPackage = "Досье Хранителя.",
            _lastUpdated = "2026-04-27T12:00:00Z"
        });

        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains("притяжение к извечному Хранителю", StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("отмените attraction contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_MalformedSystemGuardianAttractionBlocksIncarnation()
    {
        await _fs.WriteFileAtomicAsync(SystemGuardianLibraryService.AttractionRequestPath, "{ malformed");

        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains("system_guardian_attraction.json повреждён", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ResidentTransferEnumeratesEveryPendingRequest()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "transfer_alpha",
                    residentId = "resident_alpha",
                    sourceGuardianId = "guardian_old_alpha",
                    sourceAbodeId = "abode_old_alpha",
                    targetGuardianId = "guardian_new_alpha",
                    targetAbodeId = "abode_new_alpha",
                    createdAtTurn = 12
                },
                new
                {
                    requestId = "transfer_beta",
                    residentId = "resident_beta",
                    sourceGuardianId = "guardian_old_beta",
                    sourceAbodeId = "abode_old_beta",
                    targetGuardianId = "guardian_new_beta",
                    targetAbodeId = "abode_new_beta",
                    createdAtTurn = 13
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("pending_guardian_abode_resident_transfers.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request[0]:", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=transfer_alpha", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("targetAbodeId=abode_new_alpha", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request[1]:", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=transfer_beta", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceGuardianId=guardian_old_beta", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full payload:", blocker, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_AbodeOfferingShowsGenericPayloadAndClosure()
    {
        await WriteJsonAsync(GuardianAbodeOfferingState.PendingRequestPath, new
        {
            requestId = "offering_blocker_001",
            guardianId = "guardian_offering_001",
            abodeId = "abode_offering_001",
            offeringType = "ink_feathers",
            inkFeathersOffered = 100,
            powerGain = 20,
            createdAtTurn = 14
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("pending_abode_offering.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=offering_blocker_001", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offeringType=ink_feathers", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inkFeathersOffered=100", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("output/ink_feather_action_result.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"powerGain\": 20", blocker, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_NpcSocialAndTradeBlockSoulGates()
    {
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "npc_social_alpha",
                    npcId = "npc_mira",
                    npcName = "Мира",
                    interactionType = "talk",
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        await WriteJsonAsync(NpcTradeRequestState.PendingRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "npc_trade_alpha",
                    npcId = "npc_mira",
                    npcName = "Мира",
                    merchantProfile = "local_merchant",
                    tradeCycleId = "mortal_trade_14",
                    derivedTradeSlotCount = 4,
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("npc_social_alpha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockers, blocker =>
            blocker.Contains(NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("npc_trade_alpha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_EmptyGuardianSocialRequestsDoNotBlockSoulGates()
    {
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new
        {
            requests = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Theory]
    [InlineData(GuardianAbodeResidentRequestState.PendingResidentsRequestPath)]
    [InlineData(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath)]
    [InlineData(GuardianAbodeResidentRequestState.PendingTransfersRequestPath)]
    public async Task CollectIncarnationBlockersAsync_EmptyResidentRequestBundlesDoNotBlockSoulGates(string pendingPath)
    {
        await WriteJsonAsync(pendingPath, new
        {
            requests = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.False(_fs.FileExists(pendingPath));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ValidManifestationRequestDoesNotBlockSoulGates()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_next_life",
                    manifestationSource = "resident_relic",
                    relicId = "relic_echo",
                    relicName = "Эхо Лиоры",
                    sourceResidentId = "resident_liora",
                    sourceGuardianId = "guardian_azalia",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 5,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Следующая смертная жизнь.",
                    futureCompanionPrompt = "Лиора проявится как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь закреплена через реликвию резидента.",
                    coreTraits = new[] { "loyal" },
                    archetypeHints = new[] { "guide" },
                    appearanceMotifs = new[] { "dawn" },
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.True(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ShiningArrayPayloadIsSafeForSoulGatesPanel()
    {
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_blocker_array_001",
                    actionType = "prepare_incarnation_package",
                    selectedCardIds = new[] { "card_alpha", "card_beta" },
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-28T00:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("core_blocker_array_001", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("selectedCardIds=[card_alpha, card_beta]", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"selectedCardIds\": [", blocker, StringComparison.OrdinalIgnoreCase);

        var panelBody = string.Join("\n", new[]
        {
            "Нельзя войти в новую смертную жизнь, пока остаются незакрытые загробные контракты.",
            string.Empty,
            string.Join("\n", blockers.Select(item => $"• {item}")),
            string.Empty,
            "Сначала дождитесь явного закрытия GM или почините повреждённый pending contract."
        });
        var ex = Record.Exception(() => new Panel(GameInterface.SafeMarkup(panelBody)));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_SourceOfLightPendingBlocksSoulGates()
    {
        await WriteJsonAsync(
            SourceOfLightCapstoneState.PendingRequestPath,
            SourceOfLightCapstoneState.CreateRequest(12, 580, 4));
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(SourceOfLightCapstoneState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains(SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains(SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ActiveSpiritualConflictBlocksSoulGates()
    {
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = new
            {
                conflictId = "afterlife_conflict_active_gate_blocker",
                realm = "Chaos Sea",
                operationType = "pressure",
                resolutionState = "active"
            },
            recentConflicts = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("activeConflict", StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("resolve", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChaosSeaToSoulGatesJourney_HygieneBlockersAndSnapshotsRemainConsistent()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 40 }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new { requests = Array.Empty<object>() });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new { requests = Array.Empty<object>() });
        Directory.CreateDirectory(_fs.ResolvePath("game_state/control/pending_turn_snapshot"));
        await WriteJsonAsync(SystemGuardianLibraryService.AttractionRequestPath, new
        {
            mode = "system_guardian_attraction",
            targetPresetId = "eternal_tide_001",
            targetPresetDisplayName = "Прилив Памяти",
            targetPresetVersion = "1.0",
            sourceLibrary = "built_in",
            targetSummary = "Извечный Хранитель памяти.",
            renderedPromptPackage = "Досье Хранителя.",
            _lastUpdated = "2026-04-27T12:00:00Z"
        });
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = new
            {
                conflictId = "afterlife_conflict_gate_journey",
                realm = "Chaos Sea",
                operationType = "pressure",
                resolutionState = "active"
            },
            recentConflicts = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var initialBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(initialBlockers, blocker =>
            blocker.Contains("притяжение к извечному Хранителю", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(initialBlockers, blocker =>
            blocker.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("activeConflict", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(initialBlockers, blocker =>
            blocker.Contains("pending_turn_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));

        _fs.DeleteFile(SystemGuardianLibraryService.AttractionRequestPath);
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = (object?)null,
            recentConflicts = new[]
            {
                new
                {
                    conflictId = "afterlife_conflict_gate_journey",
                    realm = "Chaos Sea",
                    resolutionState = "repair_cancelled",
                    resolvedAtTurn = 40,
                    repairReason = "test cleanup before Soul Gates"
                }
            }
        });
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{ malformed");

        var malformedBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(malformedBlockers, blocker =>
            blocker.Contains(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("повреждённый", StringComparison.OrdinalIgnoreCase));

        _fs.DeleteFile(GuardianTradeRequestState.PendingRequestPath);
        var clearBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");
        Assert.Empty(clearBlockers);

        var request = new TurnRequest
        {
            SessionId = "session_chaos_soul_gates_journey",
            RequestId = "request_chaos_soul_gates_journey",
            TurnNumber = 41,
            PlayerAction = "Soul Gates prep after Chaos Sea journey",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "chaos-to-soul-gates-journey");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var snapshotHashes = Assert.IsType<JsonObject>(manifest["snapshotFileHashes"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"])
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey("game_state/meta/soul_state.json"));
        Assert.True(files.ContainsKey(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(files.ContainsKey(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.False(files.ContainsKey(GuardianTradeRequestState.PendingRequestPath));
        Assert.Contains("game_state/meta/soul_state.json", rollbackBaselineFiles);
        Assert.Contains(AfterlifeSpiritualConflictState.StatePath, rollbackBaselineFiles);
        Assert.DoesNotContain(SystemGuardianLibraryService.AttractionRequestPath, rollbackBaselineFiles);

        foreach (var fileEntry in files)
        {
            var snapshotPath = fileEntry.Value?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(snapshotPath));
            Assert.True(_fs.FileExists(snapshotPath!), $"{snapshotPath} should exist for {fileEntry.Key}.");
            Assert.True(snapshotHashes.TryGetPropertyValue(fileEntry.Key, out var hashNode));
            var expectedHash = hashNode?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(expectedHash));
            var snapshotContent = await _fs.ReadFileAsync(snapshotPath!);
            Assert.NotNull(snapshotContent);
            Assert.Equal(expectedHash, ComputeSha256(snapshotContent!), ignoreCase: true);
        }
    }

    [Fact]
    public async Task CleanupAfterCancelledChaosSeaMarkerTurn_PreservesSystemGuardianAttractionForLateResponse()
    {
        await WriteJsonAsync(SystemGuardianLibraryService.AttractionRequestPath, new
        {
            mode = "system_guardian_attraction",
            targetPresetId = "eternal_tide_001",
            targetPresetDisplayName = "Прилив Памяти",
            targetPresetVersion = "1.0",
            sourceLibrary = "built_in",
            targetSummary = "Извечный Хранитель памяти.",
            renderedPromptPackage = "Досье Хранителя.",
            _lastUpdated = "2026-04-27T12:00:00Z"
        });
        var engine = CreateGameEngine();

        InvokePrivate(
            engine,
            "CleanupAfterCancelledChaosSeaMarkerTurn",
            "[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: eternal_tide_001] Игрок слышит зов.");

        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
        var json = await _fs.ReadFileAsync(SystemGuardianLibraryService.AttractionRequestPath);
        Assert.Contains("eternal_tide_001", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeRuntimeUiArtifactsAsync_PreservesResolvedPendingContractsBeforeValidation_AndAcceptedCleanupClearsThem()
    {
        const string sessionId = "session-terminal-validation";
        const string requestId = "request-terminal-validation";
        const int turnNumber = 21;
        var pendingRequest = new
        {
            requestId = "guardian_trade_late_response",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_21",
            currentReputation = 110,
            derivedTradeSlotCount = 1,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-04-27T00:00:00Z",
            createdAtTurn = turnNumber
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    tradeInventory = new
                    {
                        tradeCycleId = "return_21",
                        generatedAtUtc = "2026-04-27T01:00:00Z",
                        generationReputationTier = "Friendly",
                        pricingReputationTier = "Friendly",
                        effectiveRarityCeilingBonusSteps = 0,
                        projectBonusSignature = "0|0|0",
                        items = new[]
                        {
                            new { slotId = "slot_guardian_trade_late_response_001" }
                        }
                    },
                    tradeInventoryReceipts = new[]
                    {
                        new
                        {
                            requestId = "guardian_trade_late_response",
                            guardianId = "guardian_alpha",
                            guardianName = "Азалия",
                            abodeId = "abode_alpha",
                            tradeCycleId = "return_21",
                            status = "ready",
                            itemCount = 1,
                            resolvedAtTurn = turnNumber,
                            resolvedAtUtc = "2026-04-27T01:01:00Z"
                        }
                    }
                }
            }
        });
        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, pendingRequest);
        await WriteJsonAsync(
            $"game_state/control/pending_turn_snapshot/{GuardianTradeRequestState.PendingRequestPath}",
            pendingRequest);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            GuardianTradeRequestState.PendingRequestPath);

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task EnsureClientOwnedSystemFilesHealthyAsync_RemovesOrphanedTurnRequestBeforeValidation()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 1
        });
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "session-stale-request",
            requestId = "request-stale-request",
            turnNumber = 7,
            playerAction = "Старый ход после аварийного завершения"
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");

        Assert.False(_fs.FileExists("input/turn_request.json"));
    }

    [Fact]
    public async Task ResolveTerminalSignalTimeoutSecondsAsync_ActiveDaemonWithDisabledTimeoutUsesBridgeSafeMinimum()
    {
        await WriteJsonAsync("game_state/control/gm_daemon_status.json", new
        {
            status = "running",
            pid = Environment.ProcessId,
            sessionPath = _rootPath,
            turnTimeoutSeconds = 0,
            heartbeatAtUtc = DateTime.UtcNow.ToString("o")
        });

        var engine = CreateGameEngine(configureSettings: settings =>
        {
            settings.GmTimeoutSeconds = 300;
            settings.GmBridgeEnabled = true;
            settings.GmBridgeBackend = "ConPTYBridge";
        });

        var timeoutSeconds = await InvokePrivateAsync<int>(engine, "ResolveTerminalSignalTimeoutSecondsAsync");

        Assert.True(timeoutSeconds >= 900);
    }

    [Fact]
    public async Task CleanupAcceptedTurnTerminalArtifactsAsync_PreservesSnapshotButNotReadyForIncarnationTrigger()
    {
        const string sessionId = "session-late-incarnation";
        const string requestId = "request-late-incarnation";
        const int turnNumber = 23;
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("game_state/control/incarnation_trigger.json", new
        {
            worldDescription = "Тестовый смертный мир.",
            characterDescription = "Тестовая душа.",
            circumstances = "Проверка late accepted trigger.",
            source = "test"
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.True(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.True(_fs.FileExists("game_state/control/incarnation_trigger.json"));
    }

    [Fact]
    public async Task CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesTerminalContext()
    {
        const string sessionId = "session-normal-cleanup";
        const string requestId = "request-normal-cleanup";
        const int turnNumber = 24;
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
    }

    [Fact]
    public async Task CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesAcceptedTurnRequest()
    {
        const string sessionId = "session-accepted-input-cleanup";
        const string requestId = "request-accepted-input-cleanup";
        const int turnNumber = 26;
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            playerAction = "Проверка очистки принятого хода."
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
    }

    [Fact]
    public async Task CheckGmIncarnationTrigger_InputOnlySnapshotWithoutAcceptedContext_DoesNotDispatch()
    {
        const string sessionId = "session-input-only-incarnation";
        const string requestId = "request-input-only-incarnation";
        const int turnNumber = 25;
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 12 }
        });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            playerAction = "Обычный ожидающий ход без accepted ready signal."
        });
        await WriteJsonAsync("game_state/control/incarnation_trigger.json", new
        {
            worldDescription = "Тестовый смертный мир.",
            characterDescription = "Тестовая душа.",
            circumstances = "Этот trigger не подтверждён accepted turn.",
            source = "test"
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var dispatched = await InvokePrivateAsync<bool>(engine, "CheckGmIncarnationTrigger", new object?[] { null });

        Assert.False(dispatched);
        Assert.False(_fs.FileExists("game_state/control/incarnation_trigger.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        var inputTurn = await _fs.ReadFileAsync("input/turn_request.json");
        Assert.NotNull(inputTurn);
        Assert.Contains(requestId, inputTurn, StringComparison.Ordinal);
        Assert.DoesNotContain("Тестовый смертный мир", inputTurn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckGmIncarnationTrigger_BootstrapRepairStall_ReturnsFalseAfterRollback()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        const string acceptedSessionId = "session-incarnation-trigger-stall";
        const string acceptedRequestId = "request-incarnation-trigger-stall";
        const int acceptedTurnNumber = 12;
        var chaosSoul = new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 0,
            inkFeathers = new { current = 0, total = 0 }
        };
        await WriteJsonAsync("game_state/meta/soul_state.json", chaosSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", chaosSoul);
        await WritePendingTurnSnapshotManifestAsync(
            acceptedSessionId,
            acceptedRequestId,
            acceptedTurnNumber,
            "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            status = "success",
            timestamp = "2026-07-09T01:00:00Z",
            filesModified = new[] { "game_state/control/incarnation_trigger.json" }
        });
        await WriteJsonAsync("game_state/control/incarnation_trigger.json", new
        {
            worldDescription = "Портовый город Аргенвик с купеческими каналами.",
            characterDescription = "Писарь Нерий Сольвейн.",
            circumstances = "Наставница Селена и торговец Мирко ждут в канцелярии.",
            source = "test"
        });

        var inputKeys = new List<ConsoleKeyInfo> { Key(ConsoleKey.Enter) };
        inputKeys.AddRange(Enumerable.Repeat(Key(ConsoleKey.RightArrow), 8));
        inputKeys.Add(Key(ConsoleKey.Enter));
        inputKeys.Add(Key(ConsoleKey.Enter));
        inputKeys.Add(Key(ConsoleKey.Enter));
        var engine = CreateGameEngine(new QueuedConsoleInputSource(inputKeys));
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();
        SetPrivateField(engine, "_lastResponse", new GameResponse
        {
            Response = "Серет открывает Врата Души. Следующее пробуждение уже принадлежит смертной жизни Нерия Сольвейна."
        });
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var acceptedSnapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var gmOutputTask = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            await WriteJsonAsync("output/narrative_response.json", new
            {
                narrative = "Повреждённый bootstrap без response и timestamp."
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = Array.Empty<object>(),
                timestamp = DateTime.UtcNow.ToString("o")
            });
            await WriteJsonAsync("output/debug_logs.json", new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                gm_thoughts_markdown = "## Охват NPC-анализа\n- Режим / Mode: Scene-local\n- Релевантные акторы / Relevant actors: Селена, Мирко\n- Почему они релевантны / Why they are relevant: Тестовый bootstrap.\n- Акторы вне охвата / Actors outside scope: нет\n- Почему они вне охвата / Why outside scope: нет\n\n## Reasoning / Размышления NPC\n- Bootstrap intentionally malformed for rollback test."
            });
            await WriteJsonAsync("ready/turn_complete.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                status = "success",
                timestamp = DateTime.UtcNow.ToString("o"),
                filesModified = new[]
                {
                    "output/narrative_response.json",
                    "output/interface_updates.json",
                    "output/debug_logs.json"
                }
            });

            await WaitForValidationRepairRequestContainingAsync("narrative_response", TimeSpan.FromSeconds(5));
            await WriteJsonAsync("game_state/control/gm_validation_repair_artifact_stall_report.json", new
            {
                isStalled = true,
                completed = false,
                elapsedSeconds = 180,
                noProgressSeconds = 180,
                harnessSource = "gm_validation_repair_artifact_stall",
                bridgeCleanup = new
                {
                    reason = "gm_validation_repair_artifact_stall",
                    status = "fallback-stopped",
                    ok = true
                }
            });
        });

        var dispatched = await InvokePrivateAsync<bool>(
            engine,
            "CheckGmIncarnationTrigger",
            new[] { acceptedSnapshotContext });
        await gmOutputTask.WaitAsync(TimeSpan.FromSeconds(8));

        Assert.False(dispatched);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal("Chaos Sea", soul["currentRealm"]!.GetValue<string>());
        Assert.Equal(0, soul["currentIncarnation"]!.GetValue<int>());
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        var lastResponse = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.DoesNotContain("Следующее пробуждение уже принадлежит смертной жизни", lastResponse.Response, StringComparison.Ordinal);
        Assert.Contains("не удалось подготовить смертную жизнь", lastResponse.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.True(resolution.IsAuthorized);
        Assert.Equal("authorized", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_GachaManifest_Authorizes()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(
            "test-session",
            "test-request",
            14,
            new JsonObject
            {
                ["baseRarity"] = "Rare",
                ["formula"] = "test-gacha-roll"
            },
            "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.True(resolution.IsAuthorized);
        Assert.Equal("authorized", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_InactiveManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("stale-session", "stale-request", 99, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("inactive_manifest", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_StructurallyInvalidManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        manifest["snapshotFileHashes"] = new JsonObject();
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(JsonSerializer.Deserialize<PendingTurnSnapshotManifestPayload>(
            manifest.ToJsonString(),
            SnapshotHashJsonOpts)!);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("invalid_manifest", resolution.Code);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ResetsEnlightenmentAndPreservesInkFeathers()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            },
            soulProgression = new
            {
                tier = 4,
                tierName = "Transcendence",
                progressPercent = 100,
                totalExperience = 999,
                experienceInCurrentTier = 999
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.True(completed);

        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();

        Assert.Equal("Chaos Sea", soulRoot["currentRealm"]?.GetValue<string>());
        var enlightenment = Assert.IsType<JsonObject>(soulRoot["enlightenment"]);
        Assert.Equal("Новичок", enlightenment["currentTier"]?.GetValue<string>());
        Assert.Equal(0, enlightenment["experience"]?.GetValue<int>());
        Assert.Equal(0, enlightenment["level"]?.GetValue<int>());
        Assert.Equal(0, enlightenment["progressPercent"]?.GetValue<int>());
        var soulProgression = Assert.IsType<JsonObject>(soulRoot["soulProgression"]);
        Assert.Equal(0, soulProgression["tier"]?.GetValue<int>());
        Assert.Equal("Новичок", soulProgression["tierName"]?.GetValue<string>());
        Assert.Equal(0, soulProgression["progressPercent"]?.GetValue<int>());
        Assert.Equal(0, soulProgression["totalExperience"]?.GetValue<int>());
        Assert.Equal(0, soulProgression["experienceInCurrentTier"]?.GetValue<int>());
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(7, inkFeathers["current"]?.GetValue<int>());
        Assert.Equal(31, inkFeathers["total"]?.GetValue<int>());
        Assert.Equal(ShiningAbodeState.AvailabilitySealedUntilNextAscension, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Chaos Sea", stateManager.CurrentState.CurrentRealm);
        Assert.Equal("Новичок", stateManager.CurrentState.EnlightenmentTier);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksWhenPendingShiningRequestsExist()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core-request-1",
            ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningTradeRequestState.WriteRequestAsync(_fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            RequestId = "trade-request-1",
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            TradeCycleId = "cycle-alpha",
            DerivedTradeTier = 2,
            DerivedTradeSlotCount = 3,
            DerivedRarityCeiling = "legendary",
            DerivedServiceMultiplier = 1.15,
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteFoundingRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
        {
            RequestId = "founding-request-1",
            ProposedFactionId = "faction-founded",
            ProposedHallId = "hall-founded",
            ProposedHallName = "Зал Основания",
            ProposedHallDescription = "Первый зал новой фракции.",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteRealignmentRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
        {
            RequestId = "realignment-request-1",
            ResidentId = "resident-alpha",
            ResidentName = "Альфа",
            SourceFactionId = "faction-alpha",
            SourceFactionName = "Фракция Альфа",
            TargetFactionId = "faction-beta",
            TargetFactionName = "Фракция Бета",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
        {
            RequestId = "leadership-request-1",
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            IncumbentHeadActorType = ShiningAbodeState.HeadActorTypeGuardian,
            IncumbentHeadActorId = "guardian-alpha",
            CandidateHeadActorType = ShiningAbodeState.HeadActorTypePlayerSoul,
            CandidateHeadActorId = "player",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });

        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));

        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();

        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Shining Abode", stateManager.CurrentState.CurrentRealm);
        Assert.True(stateManager.CurrentState.IsInShiningAbode);
    }

    [Fact]
    public async Task GetBlockingShiningPendingContractPathsAsync_DeletesExplicitEmptyFilesButKeepsMalformedAndActive()
    {
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = Array.Empty<object>()
        });
        await WriteJsonAsync(ShiningTradeRequestState.PendingRequestsPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "trade-request-1",
                    factionId = "faction-alpha",
                    tradeCycleId = "shining_return_4",
                    createdAtTurn = 12
                }
            }
        });
        await _fs.WriteFileAtomicAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, "{ malformed");
        await WriteJsonAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath, new { });
        await WriteJsonAsync(
            SourceOfLightCapstoneState.PendingRequestPath,
            SourceOfLightCapstoneState.CreateRequest(12, 580, 4));

        var engine = CreateGameEngine();

        var blockingPaths = await InvokePrivateAsync<IReadOnlyList<string>>(engine, "GetBlockingShiningPendingContractPathsAsync");

        Assert.DoesNotContain(ShiningCoreActionRequestState.PendingActionsRequestPath, blockingPaths);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.Contains(blockingPaths, item =>
            item.Contains(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains("trade-request-1", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("shining_return_4", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockingPaths, item =>
            item.Contains(SourceOfLightCapstoneState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains(SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase) &&
            item.Contains(SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockingPaths, item =>
            item.Contains(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains("malformed", StringComparison.OrdinalIgnoreCase));
        var wrongShapeBlocker = Assert.Single(
            blockingPaths,
            item => item.Contains(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("missing requests[] array", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repair", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active Shining pending contract", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("root full payload", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
    }

    [Fact]
    public async Task NormalizeRuntimeUiArtifactsAsync_PreservesResolvedShiningPendingRequestDuringActiveSnapshot()
    {
        var requestRoot = new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_req_open_gates",
                    actionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    factionId = "",
                    factionName = "",
                    projectId = "",
                    projectDisplayName = "",
                    radianceTierAtRequest = 1,
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    sourceDraftVersion = 0,
                    selectedCardIds = Array.Empty<string>(),
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-30T00:00:00Z"
                }
            }
        };
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Shining Abode",
            currentIncarnation = 3,
            inkFeathers = new { current = 40 }
        });
        await WriteJsonAsync(ShiningAbodeState.StatePath, new
        {
            availability = ShiningAbodeState.AvailabilityActive,
            radiance = new { experience = 120, tier = 1 },
            lightSparks = 12,
            gates = new
            {
                draftVersion = 2,
                hasOpenDraft = true,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            coreActionReceipts = new[]
            {
                new
                {
                    requestId = "core_req_open_gates",
                    actionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    status = ShiningCoreActionRequestState.RequestStatusAccepted,
                    generatedDraftVersion = 2,
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-04-30T00:01:00Z",
                    reason = "gates_opened"
                }
            }
        });
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WriteJsonAsync(
            $"game_state/control/pending_turn_snapshot/{ShiningCoreActionRequestState.PendingActionsRequestPath}",
            requestRoot);
        await WritePendingTurnSnapshotManifestAsync(
            "test-session",
            "test-request",
            12,
            ShiningCoreActionRequestState.PendingActionsRequestPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12,
            accepted = true
        });
        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync");

        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksLegacyPendingNativeDiscovery()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 32, total = 64 }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 68,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            pendingNativeFactionDiscovery = new
            {
                requestId = "discover_native_faction:0041",
                createdAtTurn = 41,
                createdAtUtc = "2026-04-19T00:00:00Z",
                radianceTierAtRequest = 3,
                costFeathers = 25,
                costLightSparks = 20
            },
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.NotNull(shiningRoot["pendingNativeFactionDiscovery"]);
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Shining Abode", stateManager.CurrentState.CurrentRealm);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksMalformedNonNullLegacyPendingNativeDiscovery()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 32, total = 64 }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 68,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            pendingNativeFactionDiscovery = "malformed_contract",
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.Equal("malformed_contract", shiningRoot["pendingNativeFactionDiscovery"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
    }

    [Fact]
    public async Task UpdateSoulStateRealm_WriteFailureReturnsFalseAndLeavesSoulStateUnchanged()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 }
        });

        var engine = CreateGameEngine();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        using var soulLock = File.Open(
            _fs.ResolvePath("game_state/meta/soul_state.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var updated = await InvokePrivateAsync<bool>(engine, "UpdateSoulStateRealm", "Shining Abode", null, false);

        Assert.False(updated);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task UpdateSoulStateRealm_MortalLifeEnd_WithSparseLiveFixtureMovesSoulToChaosSea()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Пепельная Искра",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
            inkFeathers = new { current = 0, total = 0 },
            soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() },
            afterlifeArchive = new { stored = Array.Empty<object>() },
            livesHistory = Array.Empty<object>(),
            pendingMemoryLegacy = (object?)null
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            activeGuardian = (object?)null,
            guardians = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var updated = await InvokePrivateAsync<bool>(
            engine,
            "UpdateSoulStateRealm",
            "Chaos Sea",
            "Ходов прожито: 1. Заметка игрока: live regression.",
            false);

        Assert.True(updated);
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal("Chaos Sea", soulRoot["currentRealm"]?.GetValue<string>());
        var livesHistory = Assert.IsType<JsonArray>(soulRoot["livesHistory"]);
        Assert.Single(livesHistory);
    }

    [Fact]
    public async Task UpdateSoulStateRealm_MortalLifeEnd_WithPendingScenarioCoreAndCurrentWorldLoreMovesSoulToChaosSea()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Пепельная Искра",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
            inkFeathers = new { current = 0, total = 0 },
            soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() },
            afterlifeArchive = new { stored = Array.Empty<object>() },
            livesHistory = Array.Empty<object>(),
            pendingMemoryLegacy = (object?)null
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            activeGuardian = (object?)null,
            guardians = Array.Empty<object>(),
            chaosSeaNavigation = new { currentAbodeId = (string?)null }
        });
        await WriteJsonAsync(WorldDirectiveService.PendingSetupPath, new
        {
            mode = "mixed",
            profileId = "victorian-occult-capital",
            profileName = "Викторианская оккультная столица",
            worldDirectives = new { worldTitle = "Серебряный Город" }
        });
        await WriteJsonAsync(ScenarioCoreService.ManifestPath, new
        {
            sourcePath = WorldDirectiveService.PendingSetupPath,
            sourceLastUpdated = "2026-03-13T00:00:00Z",
            lastExtractedAt = "2026-06-19T01:00:00Z",
            candidateAssertions = Array.Empty<object>(),
            scenarioCoreAssertions = new[]
            {
                new
                {
                    assertionId = "core_world",
                    category = "world_premise",
                    value = "Серебряный Город",
                    @explicit = true,
                    source = "structured_field"
                }
            },
            openCorrectionSlots = Array.Empty<object>()
        });
        await WriteJsonAsync("lore/current_world/world_setting.json", new
        {
            title = "Серебряный Город"
        });
        await WriteJsonAsync("lore/current_world/history/era.json", new
        {
            era = "Late 19th century"
        });
        var engine = CreateGameEngine();

        var updated = await InvokePrivateAsync<bool>(
            engine,
            "UpdateSoulStateRealm",
            "Chaos Sea",
            "Ходов прожито: 1. Заметка игрока: live regression.",
            false);

        Assert.True(updated);
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal("Chaos Sea", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists("lore/current_world/world_setting.json"));
        Assert.False(_fs.FileExists("lore/current_world/history/era.json"));
    }

    [Fact]
    public async Task CheckLifeTransitions_VoluntaryEnd_DispatchesAutomaticLifeEvaluationInsteadOfReturningToMortalWorld()
    {
        const string acceptedSessionId = "session_voluntary_end";
        const string acceptedRequestId = "request_voluntary_end";
        const int acceptedTurnNumber = 2;
        var preTurnSoul = new
        {
            soulName = "Пепельная Искра",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
            inkFeathers = new { current = 0, total = 0 },
            soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() },
            afterlifeArchive = new { stored = Array.Empty<object>() },
            livesHistory = Array.Empty<object>(),
            pendingMemoryLegacy = (object?)null
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            activeGuardian = (object?)null,
            guardians = Array.Empty<object>(),
            chaosSeaNavigation = new { currentAbodeId = (string?)null }
        });
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            playerAction = "Я осознанно завершаю эту смертную жизнь."
        });
        await WritePendingTurnSnapshotManifestAsync(
            acceptedSessionId,
            acceptedRequestId,
            acceptedTurnNumber,
            "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            status = "success",
            timestamp = "2026-06-19T01:00:00Z",
            filesModified = new[] { "game_state/control/life_transitions.json", "output/narrative_response.json" }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Voluntary",
            summary = "Добровольное завершение тестовой смертной жизни."
        });
        var input = new QueuedConsoleInputSource(Enumerable.Repeat(Key(ConsoleKey.Enter), 4));
        var engine = CreateGameEngine(input);
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var acceptedSnapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var sawEvaluationRequest = false;

        var responder = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
                if (!string.IsNullOrWhiteSpace(requestJson) &&
                    requestJson.Contains("Оценка Жизни", StringComparison.Ordinal))
                {
                    sawEvaluationRequest = true;
                    using var requestDoc = JsonDocument.Parse(requestJson);
                    var requestRoot = requestDoc.RootElement;
                    var sessionId = requestRoot.GetProperty("sessionId").GetString();
                    var requestId = requestRoot.GetProperty("requestId").GetString();
                    var turnNumber = requestRoot.GetProperty("turnNumber").GetInt32();

                    await WriteAcceptedLifeEvaluationResponseAsync(sessionId, requestId, turnNumber);
                    return;
                }

                await Task.Delay(50);
            }
        });
        var lifecycleTask = InvokePrivateTaskAsync(engine, "CheckLifeTransitions", acceptedSnapshotContext);

        var completed = await Task.WhenAny(lifecycleTask, Task.Delay(TimeSpan.FromSeconds(12)));
        await responder;

        if (!ReferenceEquals(lifecycleTask, completed))
        {
            var repairRequest = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json") ?? "<missing>";
            var turnRequest = await _fs.ReadFileAsync("input/turn_request.json") ?? "<missing>";
            var errorLogPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
            var errorLog = File.Exists(errorLogPath) ? await File.ReadAllTextAsync(errorLogPath) : "<missing>";
            Assert.Fail(
                "CheckLifeTransitions did not complete within 12 seconds." +
                Environment.NewLine + "validation_repair_request.json:" + Environment.NewLine + repairRequest +
                Environment.NewLine + "input/turn_request.json:" + Environment.NewLine + turnRequest +
                Environment.NewLine + "error_log.txt:" + Environment.NewLine + errorLog);
        }

        Assert.Same(lifecycleTask, completed);
        await lifecycleTask;
        Assert.True(sawEvaluationRequest, "CheckLifeTransitions must dispatch a separate automatic Life Evaluation request after a valid TriggerLifeEnd.");
        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal("Chaos Sea", soul["currentRealm"]?.GetValue<string>());
    }

    [Fact]
    public async Task CheckLifeTransitions_LifeEvaluationDispatchFailure_WritesErrorLog()
    {
        const string acceptedSessionId = "session_life_eval_dispatch_failure";
        const string acceptedRequestId = "request_life_eval_dispatch_failure";
        const int acceptedTurnNumber = 3;
        var preTurnSoul = new
        {
            soulName = "Пепельная Искра",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
            inkFeathers = new { current = 0, total = 0 },
            soulRelics = new { equipped = Array.Empty<object>(), stored = Array.Empty<object>() },
            afterlifeArchive = new { stored = Array.Empty<object>() },
            livesHistory = Array.Empty<object>(),
            pendingMemoryLegacy = (object?)null
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            activeGuardian = (object?)null,
            guardians = Array.Empty<object>(),
            chaosSeaNavigation = new { currentAbodeId = (string?)null }
        });
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            playerAction = "Я осознанно завершаю эту смертную жизнь."
        });
        await WritePendingTurnSnapshotManifestAsync(
            acceptedSessionId,
            acceptedRequestId,
            acceptedTurnNumber,
            "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            status = "success",
            timestamp = "2026-06-19T01:00:00Z",
            filesModified = new[] { "game_state/control/life_transitions.json" }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Voluntary",
            summary = "Добровольное завершение тестовой смертной жизни."
        });

        var input = new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Enter) });
        var engine = CreateGameEngine(input);
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var acceptedSnapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        _fs.DeleteFile("input/turn_request.json");
        Directory.CreateDirectory(Path.Combine(_fs.GameSessionPath, "input", "turn_request.json"));

        await InvokePrivateTaskAsync(engine, "CheckLifeTransitions", acceptedSnapshotContext);

        var logPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
        Assert.True(File.Exists(logPath), "Lifecycle dispatch exceptions must be visible in error_log.txt.");
        var log = await File.ReadAllTextAsync(logPath, Encoding.UTF8);
        Assert.Contains("UnauthorizedAccessException", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckLifeTransitions_FileSystemExampleState_DispatchesAutomaticLifeEvaluation()
    {
        const string acceptedSessionId = "session_filesystem_example_voluntary_end";
        const string acceptedRequestId = "request_filesystem_example_voluntary_end";
        const int acceptedTurnNumber = 3;

        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);

        var preTurnSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.False(string.IsNullOrWhiteSpace(preTurnSoulJson));
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json",
            preTurnSoulJson!);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            playerAction = "Я осознанно завершаю эту смертную жизнь."
        });
        await WritePendingTurnSnapshotManifestAsync(
            acceptedSessionId,
            acceptedRequestId,
            acceptedTurnNumber,
            "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = acceptedSessionId,
            requestId = acceptedRequestId,
            turnNumber = acceptedTurnNumber,
            status = "success",
            timestamp = "2026-06-19T01:00:00Z",
            filesModified = new[] { "game_state/control/life_transitions.json", "output/narrative_response.json" }
        });
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Оценка Жизни в этом ходе не проводится; дальнейший lifecycle-переход передан клиенту.",
            timestamp = "2026-06-19T01:00:00Z"
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Voluntary",
            summary = "Добровольное завершение тестовой смертной жизни."
        });

        var input = new QueuedConsoleInputSource(Enumerable.Repeat(Key(ConsoleKey.Enter), 4));
        var engine = CreateGameEngine(input);
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var acceptedSnapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var sawEvaluationRequest = false;

        var responder = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
                if (!string.IsNullOrWhiteSpace(requestJson) &&
                    requestJson.Contains("Оценка Жизни", StringComparison.Ordinal))
                {
                    sawEvaluationRequest = true;
                    using var requestDoc = JsonDocument.Parse(requestJson);
                    var requestRoot = requestDoc.RootElement;
                    var sessionId = requestRoot.GetProperty("sessionId").GetString();
                    var requestId = requestRoot.GetProperty("requestId").GetString();
                    var turnNumber = requestRoot.GetProperty("turnNumber").GetInt32();

                    await WriteAcceptedLifeEvaluationResponseAsync(sessionId, requestId, turnNumber);
                    return;
                }

                await Task.Delay(50);
            }
        });
        var lifecycleTask = InvokePrivateTaskAsync(engine, "CheckLifeTransitions", acceptedSnapshotContext);

        var completed = await Task.WhenAny(lifecycleTask, Task.Delay(TimeSpan.FromSeconds(12)));
        await responder;

        if (!ReferenceEquals(lifecycleTask, completed))
        {
            var repairRequest = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json") ?? "<missing>";
            var turnRequest = await _fs.ReadFileAsync("input/turn_request.json") ?? "<missing>";
            var timeoutErrorLogPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
            var timeoutErrorLog = File.Exists(timeoutErrorLogPath) ? await File.ReadAllTextAsync(timeoutErrorLogPath) : "<missing>";
            Assert.Fail(
                "CheckLifeTransitions did not complete within 12 seconds." +
                Environment.NewLine + "validation_repair_request.json:" + Environment.NewLine + repairRequest +
                Environment.NewLine + "input/turn_request.json:" + Environment.NewLine + turnRequest +
                Environment.NewLine + "error_log.txt:" + Environment.NewLine + timeoutErrorLog);
        }

        Assert.Same(lifecycleTask, completed);
        await lifecycleTask;
        var errorLogPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
        var errorLog = File.Exists(errorLogPath) ? await File.ReadAllTextAsync(errorLogPath, Encoding.UTF8) : string.Empty;
        Assert.True(sawEvaluationRequest, $"CheckLifeTransitions did not dispatch Life Evaluation. error_log.txt: {errorLog}");
        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal("Chaos Sea", soul["currentRealm"]?.GetValue<string>());
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_SoulWriteFailureRestoresShiningStateAndReturnsFalse()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json");
        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();
        using var soulLock = File.Open(
            _fs.ResolvePath("game_state/meta/soul_state.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"));
    }

    [Fact]
    public async Task IncarnationLocalPrepRollbackSnapshot_SurvivesCurrentWorldLoreClear()
    {
        const string worldSettingPath = "lore/current_world/world_setting.json";
        const string nestedLorePath = "lore/current_world/history/era.json";
        const string worldSettingJson = """{ "worldName": "Old World" }""";
        const string nestedLoreJson = """{ "era": "Before Gates" }""";

        await _fs.WriteFileAtomicAsync(worldSettingPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(nestedLorePath, nestedLoreJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");

        Assert.Contains(worldSettingPath, rollbackFiles);
        Assert.Contains(nestedLorePath, rollbackFiles);

        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "explorer_rollback_filter");
        var baselineFilesValue = rollbackSnapshot.GetType().GetProperty("BaselineFiles")?.GetValue(rollbackSnapshot);
        var baselineFiles = Assert.IsAssignableFrom<IEnumerable>(baselineFilesValue);
        Assert.DoesNotContain(
            baselineFiles.Cast<object>().Select(value => value?.ToString() ?? string.Empty),
            path => path.StartsWith("game_state/control/explorer_local_turn_rollback/", StringComparison.OrdinalIgnoreCase));

        _fs.ClearCurrentWorldLore();

        Assert.False(_fs.FileExists(worldSettingPath));
        Assert.False(_fs.FileExists(nestedLorePath));

        await explorer.RestoreStagedLocalTurnRollbackSnapshotAsync();

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(worldSettingPath));
        Assert.Equal(nestedLoreJson, await _fs.ReadFileAsync(nestedLorePath));
        Assert.False(Directory.Exists(_fs.ResolvePath("game_state/control/explorer_local_turn_rollback")));
    }

    [Fact]
    public async Task IncarnationLocalPrepNewSetupFiles_AreSnapshottedButStillRollbackDeleted()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";
        const string pendingSetupJson = """{ "mode": "manual", "worldDirectives": { "settingSummary": "New setup" } }""";
        const string scenarioCoreJson = """{ "scenarioCore": { "summary": "New scenario" } }""";

        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");

        Assert.Contains(WorldDirectiveService.PendingSetupPath, rollbackFiles);
        Assert.Contains(ScenarioCoreService.ManifestPath, rollbackFiles);

        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, pendingSetupJson);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, scenarioCoreJson);
        explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(
            WorldDirectiveService.PendingSetupPath,
            ScenarioCoreService.ManifestPath);

        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "incarnation_setup_validation_snapshot");
        var stagedSnapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot();
        InvokePrivate(engine, "OverlayExplorerLocalRollbackSnapshot", rollbackSnapshot, stagedSnapshot);

        var request = new TurnRequest
        {
            SessionId = "session_incarnation_setup_snapshot",
            RequestId = "request_incarnation_setup_snapshot",
            TurnNumber = 42,
            PlayerAction = "incarnation setup snapshot test",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey(WorldDirectiveService.PendingSetupPath));
        Assert.True(files.ContainsKey(ScenarioCoreService.ManifestPath));
        Assert.DoesNotContain(WorldDirectiveService.PendingSetupPath, rollbackBaselineSet);
        Assert.DoesNotContain(ScenarioCoreService.ManifestPath, rollbackBaselineSet);

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.False(_fs.FileExists(WorldDirectiveService.PendingSetupPath));
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_GmBridgeStatus_IsExcludedFromRollbackAndValidationSnapshot()
    {
        const string bridgeStatusPath = "game_state/control/gm_bridge_status.json";
        await _fs.WriteFileAtomicAsync(bridgeStatusPath, """
        {
          "backend": "ConPTYBridge",
          "state": "Ready",
          "updatedAtUtc": "2026-06-19T01:00:00Z"
        }
        """);

        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "gm_bridge_status_runtime");
        var request = new TurnRequest
        {
            SessionId = "session_gm_bridge_status_runtime",
            RequestId = "request_gm_bridge_status_runtime",
            TurnNumber = 42,
            PlayerAction = "ordinary turn while GM bridge status is changing",
            Timestamp = "2026-06-19T01:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBackups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.False(files.ContainsKey(bridgeStatusPath));
        Assert.False(rollbackBackups.ContainsKey(bridgeStatusPath));
        Assert.DoesNotContain(bridgeStatusPath, rollbackBaselineSet);
    }

    [Fact]
    public async Task LoadCanonicalBaselineSnapshotAsync_AbsentCanonicalFiles_DoNotInvalidateExistingBaseline()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string absentQuestPath = "game_state/quests/regular_quests.json";
        await _fs.WriteFileAtomicAsync(soulStatePath, """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        _fs.DeleteFile(absentQuestPath);

        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_absent_canonical_baseline",
            RequestId = "request_absent_canonical_baseline",
            TurnNumber = 42,
            PlayerAction = "ordinary turn with sparse canonical state",
            Timestamp = "2026-06-19T01:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_absent_canonical_baseline",
          "requestId": "request_absent_canonical_baseline",
          "turnNumber": 42,
          "timestamp": "2026-06-19T01:01:00Z",
          "status": "success"
        }
        """);

        var snapshotObject = await InvokePrivateTaskResultAsync(engine, "LoadCanonicalBaselineSnapshotAsync", 42, null);
        var snapshot = Assert.IsAssignableFrom<IDictionary<string, string>>(snapshotObject);

        Assert.Contains(soulStatePath, snapshot.Keys);
        Assert.DoesNotContain(absentQuestPath, snapshot.Keys);
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_OrdinaryTurnStagingFiles_AreValidationSnapshottedWithoutRollbackBaseline()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";
        const string scenarioCoreJson = """{ "scenarioCore": { "summary": "Prepared by client before GM dispatch." } }""";
        const string pendingDiceJson = """{ "rolls": [{ "kind": "1d20", "value": 12 }] }""";

        var engine = CreateGameEngine();
        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);
        _fs.DeleteFile(ScenarioCoreService.ManifestPath);
        _fs.DeleteFile(PendingTurnStateService.PendingDiceStatePath);
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "ordinary_turn_staging");
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, scenarioCoreJson);
        await _fs.WriteFileAtomicAsync(PendingTurnStateService.PendingDiceStatePath, pendingDiceJson);
        InvokePrivate(engine, "RegisterOrdinaryTurnStagingValidationSnapshotFiles", rollbackSnapshot);

        var request = new TurnRequest
        {
            SessionId = "session_ordinary_staging_snapshot",
            RequestId = "request_ordinary_staging_snapshot",
            TurnNumber = 42,
            PlayerAction = "ordinary turn with client staging files",
            Timestamp = "2026-06-19T01:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey(ScenarioCoreService.ManifestPath));
        Assert.True(files.ContainsKey(PendingTurnStateService.PendingDiceStatePath));
        Assert.DoesNotContain(ScenarioCoreService.ManifestPath, rollbackBaselineSet);
        Assert.DoesNotContain(PendingTurnStateService.PendingDiceStatePath, rollbackBaselineSet);
    }

    [Fact]
    public async Task LifeEvaluationSnapshot_ClientOwnedCurrentWorldDirectivesUseRollbackBaselineWhenCurrentFileIsCleared()
    {
        const string directivesPath = WorldDirectiveService.ActiveDirectivesPath;
        const string directivesJson = """
        {
          "settingSummary": "Предыдущий смертный мир должен остаться проверяемым baseline.",
          "lastUpdated": "2026-07-02T04:20:00Z"
        }
        """;

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Искра Странствий",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync(directivesPath, directivesJson);

        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "life_eval_current_world_directives");

        _fs.DeleteFile(directivesPath);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Искра Странствий",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        var request = new TurnRequest
        {
            SessionId = "session_life_eval_current_world_directives",
            RequestId = "request_life_eval_current_world_directives",
            TurnNumber = 22,
            PlayerAction = "life evaluation",
            Timestamp = "2026-07-02T04:22:32Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "автоматической оценки жизни");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var snapshotFileHashes = Assert.IsType<JsonObject>(manifest["snapshotFileHashes"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(directivesPath, rollbackBaselineSet);
        Assert.True(files.ContainsKey(directivesPath));
        Assert.True(snapshotFileHashes.ContainsKey(directivesPath));
    }

    [Fact]
    public async Task MortalBootstrapBaselineFiles_AreValidationSnapshottedButRollbackDeletedWhenTurnFails()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";

        var engine = CreateGameEngine();
        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);

        var bootstrapPaths = new[]
        {
            "game_state/world/current_location.json",
            "game_state/world/world_map.json",
            "game_state/factions/faction_core.json",
            "game_state/factions/faction_resources.json",
            "game_state/quests/regular_quests.json",
            "lore/codex_entries.json"
        };

        foreach (var path in bootstrapPaths)
            Assert.False(_fs.FileExists(path));

        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "mortal_bootstrap_timeout");

        await InvokePrivateTaskAsync(
            engine,
            "WriteMortalBootstrapBaselineAsync",
            rollbackSnapshot,
            1,
            4,
            "Мирон, архивариус",
            "Город у болот",
            "Башня архива ночью");

        var request = new TurnRequest
        {
            SessionId = "session_mortal_bootstrap_timeout",
            RequestId = "request_mortal_bootstrap_timeout",
            TurnNumber = 4,
            PlayerAction = "first mortal bootstrap",
            Timestamp = "2026-06-29T08:26:03Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in bootstrapPaths)
        {
            Assert.True(files.ContainsKey(path), path);
            Assert.DoesNotContain(path, rollbackBaselineSet);
        }

        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """{ "locationId": "gm_mutated_before_timeout" }""");

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        foreach (var path in bootstrapPaths)
            Assert.False(_fs.FileExists(path), path);
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_AbsentSourceOfLightPending_IsNotRollbackBaseline()
    {
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_no_source_pending_snapshot",
            RequestId = "request_no_source_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "ordinary turn without Source pending",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.False(files.ContainsKey(SourceOfLightCapstoneState.PendingRequestPath));
        Assert.DoesNotContain(SourceOfLightCapstoneState.PendingRequestPath, rollbackBaselineSet);
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_AbsentAfterlifePendingContracts_AreNotRollbackBaseline()
    {
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_no_afterlife_pending_snapshot",
            RequestId = "request_no_afterlife_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "ordinary turn without afterlife pending contracts",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var optionalPendingPaths = new[]
        {
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath
        };

        foreach (var pendingPath in optionalPendingPaths)
        {
            Assert.False(files.ContainsKey(pendingPath), $"{pendingPath} should not have a snapshot entry when absent.");
            Assert.DoesNotContain(pendingPath, rollbackBaselineSet);
        }
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_PresentSourceOfLightPending_IsRollbackBaseline()
    {
        var sourceRequest = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await SourceOfLightCapstoneState.WriteRequestAsync(_fs, sourceRequest);
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_source_pending_snapshot",
            RequestId = "request_source_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "Source pending snapshot",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Shining Abode" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey(SourceOfLightCapstoneState.PendingRequestPath));
        Assert.Contains(SourceOfLightCapstoneState.PendingRequestPath, rollbackBaselineSet);
    }

    [Fact]
    public async Task ValidatedRollbackSnapshot_PreservesExplorerLocalTurnRollbackBackups()
    {
        const string sessionId = "session_explorer_rollback_restart_001";
        const string requestId = "request_explorer_rollback_restart_001";
        const int turnNumber = 77;
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string trackedPath = "lore/current_world/world_setting.json";
        const string snapshotPath = "game_state/control/pending_turn_snapshot/lore/current_world/world_setting.json";
        const string backupPath = "game_state/control/explorer_local_turn_rollback/restart/world_setting.json.rollback.001";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";
        const string worldSettingJson = """{ "worldName": "Old World" }""";

        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);
        await _fs.WriteFileAtomicAsync(soulSnapshotPath, soulStateJson);
        await _fs.WriteFileAtomicAsync(trackedPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(snapshotPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(backupPath, worldSettingJson);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "restart rollback test",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [soulStatePath] = soulSnapshotPath,
                [trackedPath] = snapshotPath
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [soulStatePath] = ComputeSha256(soulStateJson),
                [trackedPath] = ComputeSha256(worldSettingJson)
            },
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [trackedPath] = backupPath
            },
            RollbackBaselineFiles = new List<string> { soulStatePath, trackedPath },
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);

        _fs.DeleteFile(trackedPath);
        var engine = CreateGameEngine();
        var loadedManifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", loadedManifest);
        var backupFilesValue = rollbackSnapshot.GetType().GetProperty("BackupFiles")?.GetValue(rollbackSnapshot);
        var backupFiles = Assert.IsAssignableFrom<Dictionary<string, string>>(backupFilesValue);

        Assert.True(backupFiles.TryGetValue(trackedPath, out var restoredBackupPath));
        Assert.Equal(backupPath, restoredBackupPath);

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task CreatePreTurnBackup_BrowserDirectGachaRollbackEvidenceOverridesPostSpendSoulBackup()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string browserRollbackPath = "game_state/control/explorer_local_turn_rollback/browser_direct_gacha/game_state_meta_soul_state.json.rollback.test";
        const string preSpendSoulJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 18, "total": 55 },
          "soulRelics": { "stored": [], "equipped": [] }
        }
        """;
        const string postSpendSoulJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 11, "total": 55 },
          "soulRelics": { "stored": [], "equipped": [] }
        }
        """;

        await _fs.WriteFileAtomicAsync(soulStatePath, postSpendSoulJson);
        await _fs.WriteFileAtomicAsync(browserRollbackPath, preSpendSoulJson);
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_browser_gacha_rollback",
            RequestId = "request_browser_gacha_rollback",
            TurnNumber = 42,
            PlayerAction = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 7 Чернильных Перьев.",
            Timestamp = "2026-06-02T00:00:00Z",
            PreGeneratedDices1d20 = Enumerable.Range(1, 20).ToArray(),
            GachaBaseResult = new GachaResult
            {
                DiceUsed = [18, 18, 18, 18],
                BaseScore = 72,
                BaseRarity = "Rare",
                Formula = "client-computed gacha base (range 4-80)"
            },
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "browser_direct_gacha");
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "browser-direct-gacha-test");

        var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        var rollbackBackups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]);
        var rollbackBackupPath = rollbackBackups[soulStatePath]!.GetValue<string>();
        Assert.StartsWith("game_state/control/explorer_local_turn_rollback/", rollbackBackupPath, StringComparison.OrdinalIgnoreCase);
        var rollbackSoul = JsonNode.Parse((await _fs.ReadFileAsync(rollbackBackupPath))!)!.AsObject();
        Assert.Equal(18, rollbackSoul["inkFeathers"]!["current"]!.GetValue<int>());
    }

    [Fact]
    public async Task ConsumedIncarnationLocalPrepRollback_RestorePathRestoresOriginalFiles()
    {
        const string worldSettingPath = "lore/current_world/world_setting.json";
        const string worldSettingJson = """{ "worldName": "Old World" }""";
        const string pendingSetupJson = """{ "mode": "manual", "worldDirectives": { "settingSummary": "Old setup" } }""";
        const string scenarioCoreJson = """{ "scenarioCore": { "summary": "Old scenario" } }""";

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        await _fs.WriteFileAtomicAsync(worldSettingPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, pendingSetupJson);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, scenarioCoreJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);

        await _fs.WriteFileAtomicAsync(worldSettingPath, """{ "worldName": "Changed World" }""");
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, """{ "mode": "manual", "worldDirectives": { "settingSummary": "Changed setup" } }""");
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """{ "scenarioCore": { "summary": "Changed scenario" } }""");

        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "incarnation_local_prep_restore");
        var stagedSnapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot();
        InvokePrivate(engine, "OverlayExplorerLocalRollbackSnapshot", rollbackSnapshot, stagedSnapshot);

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);
        await explorer.RestoreConsumedLocalTurnRollbackSnapshotAsync(stagedSnapshot);
        InvokePrivate(engine, "CleanupBackup", rollbackSnapshot);

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(worldSettingPath));
        Assert.Equal(pendingSetupJson, await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath));
        Assert.Equal(scenarioCoreJson, await _fs.ReadFileAsync(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(Directory.Exists(_fs.ResolvePath("game_state/control/explorer_local_turn_rollback")));
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(
            relativePath,
            JsonSerializer.Serialize(payload, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WriteAcceptedLifeEvaluationResponseAsync(string? sessionId, string? requestId, int turnNumber)
    {
        await EnsureChaosSeaLifeEvaluationBootstrapAsync();

        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var inkFeathers = soulRoot["inkFeathers"] as JsonObject ?? new JsonObject();
        var currentInk = ReadInt(inkFeathers["current"]);
        var totalInk = ReadInt(inkFeathers["total"]);
        inkFeathers["current"] = currentInk + 10;
        inkFeathers["total"] = Math.Max(totalInk + 10, currentInk + 10);
        soulRoot["inkFeathers"] = inkFeathers;

        var soulRelics = soulRoot["soulRelics"] as JsonObject ?? new JsonObject();
        var storedRelics = soulRelics["stored"] as JsonArray ?? new JsonArray();
        soulRelics["stored"] = storedRelics;
        soulRelics["equipped"] ??= new JsonArray();
        if (!storedRelics.OfType<JsonObject>().Any(relic =>
                string.Equals(relic["relicId"]?.GetValue<string>(), "relic_life_evaluation_test", StringComparison.OrdinalIgnoreCase)))
        {
            storedRelics.Add(new JsonObject
            {
                ["relicId"] = "relic_life_evaluation_test",
                ["name"] = "След завершенной жизни",
                ["rarity"] = "Common",
                ["quality"] = "Common",
                ["description"] = "Тихая реликвия, оставшаяся после добровольно завершенной тестовой жизни.",
                ["source"] = "LifeEvaluation"
            });
        }
        soulRoot["soulRelics"] = soulRelics;
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            soulRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        await WriteJsonAsync("lore/chaos_sea/player_chronicle.json", new
        {
            entries = new[]
            {
                new
                {
                    entryId = "life_eval_test_001",
                    title = "Добровольное завершение жизни",
                    summary = "Душа завершила тестовую смертную жизнь и вернулась в Море Хаоса с минимальной наградой.",
                    recordedAtTurn = turnNumber
                }
            }
        });
        await WriteJsonAsync("game_state/control/progression_report.json", new
        {
            progressionProcessingReport = new
            {
                sessionId,
                requestId,
                turnNumber,
                worldCyclesProcessed = 0,
                factionCyclesProcessed = 0,
                chaosSeaCyclesProcessed = 1,
                guardianProjectCyclesProcessed = 1,
                residentAgencyCyclesProcessed = 1,
                shiningAbodeCyclesProcessed = 0,
                shiningFactionCyclesProcessed = 0,
                shiningTradeCyclesProcessed = 0,
                newLastChaosSeaSimulationOrdinal = 1,
                newLastGuardianProjectCycleOrdinal = 1,
                newLastResidentAgencyCycleOrdinal = 1,
                afterlifeCatchupProcessed = false,
                afterlifeCatchupSummaryEventsProcessed = 0
            }
        });
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Море Хаоса принимает душу после оценки прожитой жизни.",
            timestamp = "2026-06-19T01:01:00Z"
        });
        await WriteJsonAsync("output/debug_logs.json", new
        {
            timestamp = "2026-06-19T01:01:00Z",
            gm_thoughts_markdown = "## Охват NPC-анализа\n- Режим / Mode: Scene-local\n- Релевантные акторы / Relevant actors: нет\n- Почему они релевантны / Why they are relevant: Оценка Жизни закрывает системный переход без участия NPC.\n- Акторы вне охвата / Actors outside scope: Хранители, резиденты Обители\n- Почему они вне охвата / Why they are outside scope: Тестовая оценка не изменяет их состояние.\n\n## Reasoning / Размышления NPC / Guardian Thoughts\n- Structured actor reasoning is not required for this test life evaluation response."
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            status = "success",
            timestamp = "2026-06-19T01:01:00Z",
            filesModified = new[]
            {
                "game_state/meta/soul_state.json",
                "lore/chaos_sea/player_chronicle.json",
                "game_state/control/progression_report.json",
                "output/narrative_response.json",
                "output/debug_logs.json"
            }
        });
    }

    private async Task EnsureChaosSeaLifeEvaluationBootstrapAsync()
    {
        await WriteJsonIfMissingAsync("game_state/meta/achievements.json", new
        {
            unlockedAchievements = Array.Empty<object>(),
            trackedProgress = Array.Empty<object>(),
            stats = new
            {
                totalUnlocked = 0,
                byCategory = new { combat = 0, exploration = 0, story = 0, social = 0, crafting = 0, meta = 0, death = 0, secret = 0 },
                byRarity = new { common = 0, uncommon = 0, rare = 0, epic = 0, legendary = 0 }
            }
        });
        await WriteJsonIfMissingAsync("lore/codex_entries.json", new
        {
            entries = Array.Empty<object>(),
            totalEntries = 0,
            categories = new { cosmology = 0, geography = 0, history = 0, cultures = 0, creatures = 0, characters = 0, artifacts = 0, factions = 0, magic = 0, other = 0 }
        });
        await WriteJsonIfMissingAsync("lore/chaos_sea/cosmology.json", new
        {
            summary = "Море Хаоса связывает души между смертными жизнями."
        });
        await WriteJsonIfMissingAsync("lore/chaos_sea/soul_system_lore.json", new
        {
            summary = "Душа сохраняет опыт, Чернильные Перья и Реликвии Души между жизнями."
        });
        await WriteJsonIfMissingAsync("lore/chaos_sea/guardians_lore.json", new
        {
            entries = Array.Empty<object>()
        });
    }

    private async Task WriteJsonIfMissingAsync(string relativePath, object payload)
    {
        if (_fs.FileExists(relativePath))
            return;

        await WriteJsonAsync(relativePath, payload);
    }

    private async Task<string> WaitForValidationRepairRequestContainingAsync(string expectedText, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastRequest = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_fs.FileExists("game_state/control/validation_repair_request.json"))
            {
                lastRequest = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
                if (lastRequest?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true)
                    return lastRequest;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Timed out waiting for validation_repair_request.json containing '{expectedText}'. Last request: {lastRequest ?? "<missing>"}");
    }

    private async Task<string> WaitForUpdatedValidationRepairRequestContainingAsync(
        string expectedText,
        string previousRequest,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastRequest = null;

        while (DateTime.UtcNow < deadline)
        {
            if (_fs.FileExists("game_state/control/validation_repair_request.json"))
            {
                lastRequest = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
                if (!string.Equals(lastRequest, previousRequest, StringComparison.Ordinal) &&
                    lastRequest?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return lastRequest;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Timed out waiting for an updated validation_repair_request.json containing '{expectedText}'. Last request: {lastRequest ?? "<missing>"}");
    }

    private static int ReadInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
            return intValue;
        if (node is JsonValue longValue && longValue.TryGetValue<long>(out var longResult) && longResult is >= 0 and <= int.MaxValue)
            return (int)longResult;
        return 0;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        params string[] trackedPaths)
    {
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, gachaBaseResult: null, trackedPaths);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        JsonObject? gachaBaseResult,
        params string[] trackedPaths)
    {
        var files = trackedPaths.ToDictionary(
            path => path,
            path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = trackedPaths.ToDictionary(
            path => path,
            path =>
            {
                var snapshotPath = _fs.ResolvePath($"game_state/control/pending_turn_snapshot/{path}");
                return ComputeSha256(File.ReadAllText(snapshotPath, Encoding.UTF8));
            },
            StringComparer.OrdinalIgnoreCase);

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "game-engine-turn-lifecycle-test",
            GachaBaseResult = gachaBaseResult,
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = trackedPaths.ToList(),
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifestPayload manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = string.Empty;
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private GameEngine CreateGameEngine(
        IConsoleInputSource? inputSource = null,
        Action<GameSettings>? configureSettings = null)
    {
        var settings = new GameSettings();
        configureSettings?.Invoke(settings);
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var localization = new LocalizationManager { CurrentLanguage = "ru" };
        var gameLoop = new GameLoop();
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var progressionSchedule = new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance);
        var gameInterface = new GameInterface(localization, settings);
        var clipboardService = new TestClipboardService();
        var explorer = new ExplorerMode(stateManager, _fs, localization, clipboardService: clipboardService, console: new TestExplorerConsole());
        var saveLoad = new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
        var imageService = new ImageService(_fs, settings, localization, NullLogger<ImageService>.Instance);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var characteristicsService = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        var storyService = new StoryService(_fs, NullLogger<StoryService>.Instance);
        var actorMemoryService = new ActorMemoryService(_fs, NullLogger<ActorMemoryService>.Instance);
        var audioService = new AudioService(_fs, settings, NullLogger<AudioService>.Instance);
        var consoleAppearance = new ConsoleAppearanceService(settings, NullLogger<ConsoleAppearanceService>.Instance);
        var systemModService = new SystemModService(_fs, settings, NullLogger<SystemModService>.Instance);
        var systemGuardianLibraryService = new SystemGuardianLibraryService(_fs, NullLogger<SystemGuardianLibraryService>.Instance);
        var criticalStateHealth = new CriticalStateHealthService(_fs, NullLogger<CriticalStateHealthService>.Instance);
        var worldDirectiveService = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
        var scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        var afterlifeArchiveCandidateService = new AfterlifeArchiveCandidateService(_fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
        var afterlifeReturnGuardService = new AfterlifeReturnGuardService(_fs, NullLogger<AfterlifeReturnGuardService>.Instance);
        var rivalSoulArcService = new RivalSoulArcService(_fs, NullLogger<RivalSoulArcService>.Instance);
        var guardianCorrectionService = new GuardianCorrectionService(_fs, scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
        var pendingTurnState = new PendingTurnStateService(_fs, NullLogger<PendingTurnStateService>.Instance);
        var stateDistributor = new StateDistributor(_fs, NullLogger<StateDistributor>.Instance);
        var qteSceneService = new QteSceneService(
            _fs,
            settings,
            characteristicsService,
            imageService,
            audioService,
            stateDistributor,
            validator,
            normalizer,
            stateManager,
            NullLogger<QteSceneService>.Instance);

        return new GameEngine(
            _fs,
            stateManager,
            gameLoop,
            normalizer,
            progressionSchedule,
            gameInterface,
            explorer,
            localization,
            saveLoad,
            imageService,
            validator,
            characteristicsService,
            storyService,
            actorMemoryService,
            audioService,
            consoleAppearance,
            systemModService,
            systemGuardianLibraryService,
            criticalStateHealth,
            worldDirectiveService,
            scenarioCoreService,
            afterlifeArchiveCandidateService,
            afterlifeReturnGuardService,
            rivalSoulArcService,
            guardianCorrectionService,
            pendingTurnState,
            qteSceneService,
            clipboardService,
            NullLogger<GameEngine>.Instance,
            inputSource);
    }

    private static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var keyChar = key == ConsoleKey.Enter
            ? '\r'
            : key == ConsoleKey.Spacebar
                ? ' '
                : char.ToLowerInvariant(key.ToString()[0]);
        return new ConsoleKeyInfo(keyChar, key, shift: false, alt: false, control: false);
    }

    private static async Task<AgentConsoleSnapshot> WaitForAgentConsoleSnapshotAsync(
        AgentConsoleStateStore store,
        string screenId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = store.GetSnapshot();
            if (snapshot != null && string.Equals(snapshot.ScreenId, screenId, StringComparison.OrdinalIgnoreCase))
                return snapshot;

            await Task.Delay(25);
        }

        throw new TimeoutException($"Agent Console snapshot '{screenId}' was not published.");
    }

    private async Task<TurnRequest> WaitForTurnRequestAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var json = await _fs.ReadFileAsync("input/turn_request.json");
            if (!string.IsNullOrWhiteSpace(json))
                return JsonSerializer.Deserialize<TurnRequest>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                       ?? throw new InvalidOperationException("turn_request.json could not be deserialized.");

            await Task.Delay(25);
        }

        throw new TimeoutException("input/turn_request.json was not created.");
    }

    private sealed class QueuedConsoleInputSource : IConsoleInputSource
    {
        private readonly Queue<ConsoleKeyInfo> _keys;

        public QueuedConsoleInputSource(IEnumerable<ConsoleKeyInfo> keys)
        {
            _keys = new Queue<ConsoleKeyInfo>(keys);
        }

        public bool IsScripted => true;

        public bool KeyAvailable => _keys.Count > 0;

        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            return _keys.Count > 0 ? _keys.Dequeue() : Key(ConsoleKey.Enter);
        }

        public string? ReadLine() => string.Empty;

        public void AssertCompleted()
        {
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(instance) as T;
        Assert.NotNull(value);
        return value!;
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static async Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, BuildPrivateInvocationArguments(method, args)) as Task<T>;
        Assert.NotNull(task);
        return await task!;
    }

    private static async Task InvokePrivateTaskAsync(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, BuildPrivateInvocationArguments(method, args)) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private static void InvokePrivate(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, BuildPrivateInvocationArguments(method, args));
    }

    private static T InvokePrivateValue<T>(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var value = method!.Invoke(instance, BuildPrivateInvocationArguments(method, args));
        Assert.IsType<T>(value);
        return (T)value!;
    }

    private static async Task<object> InvokePrivateTaskResultAsync(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, BuildPrivateInvocationArguments(method, args)) as Task;
        Assert.NotNull(task);
        await task!;
        var resultProperty = task.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var result = resultProperty!.GetValue(task);
        Assert.NotNull(result);
        return result!;
    }

    private static object?[]? BuildPrivateInvocationArguments(MethodInfo method, object?[]? args)
    {
        var supplied = args ?? Array.Empty<object?>();
        var parameters = method.GetParameters();
        if (supplied.Length == parameters.Length)
            return supplied;

        if (supplied.Length > parameters.Length)
            return supplied;

        var completed = new object?[parameters.Length];
        Array.Copy(supplied, completed, supplied.Length);
        for (var i = supplied.Length; i < parameters.Length; i++)
        {
            if (!parameters[i].HasDefaultValue)
                return supplied;

            completed[i] = parameters[i].DefaultValue;
        }

        return completed;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
