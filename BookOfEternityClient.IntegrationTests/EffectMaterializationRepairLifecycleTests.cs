using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ReadyThenPartialEffectOnlyCannotDiscardCompleteResponse()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("output/narrative_response.json");
        _fs.DeleteFile("output/interface_updates.json");
        await SeedSingletonEffectRepairAuthorityAsync(turn: 42);

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_complete_replay");
        var request = new TurnRequest
        {
            SessionId = "session_effect_repair_complete_replay",
            RequestId = "request_effect_repair_complete_replay",
            TurnNumber = 42,
            PlayerAction = "Перевязать вновь открывшуюся рану.",
            Timestamp = "2026-08-22T01:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "effect repair complete replay test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        await WriteEffectRepairCommandAsync(includeRequiredAmount: false);
        await WriteAcceptedEffectOutputsAsync("Отвергнутый неполный ответ", "Отвергнутый выбор");

        var readyOnlyRejected = false;
        var partialEffectOnlyRejected = false;
        Exception? gmFailure = null;
        var gmRepair = Task.Run(async () =>
        {
            try
            {
                var firstRequest = await WaitForValidationRepairRequestContainingAsync(
                    "effect_source_parameter_required",
                    TimeSpan.FromSeconds(8));
                Assert.False(_fs.FileExists(EffectAcceptedTurnPlan.CommandPath));
                Assert.False(_fs.FileExists("output/narrative_response.json"));
                Assert.False(_fs.FileExists("output/interface_updates.json"));

                await WriteEffectRepairReadyAsync(request, "Ready alone must not clear the obligation.");
                var secondRequest = await WaitForNewRepairAttemptAsync(firstRequest, 2, TimeSpan.FromSeconds(8));
                readyOnlyRejected = true;

                await WriteEffectRepairCommandAsync(includeRequiredAmount: true);
                await WriteEffectRepairReadyAsync(request, "Effect-only retry remains incomplete.");
                var thirdRequest = await WaitForNewRepairAttemptAsync(secondRequest, 3, TimeSpan.FromSeconds(8));
                partialEffectOnlyRejected = true;

                await WriteEffectRepairCommandAsync(includeRequiredAmount: true);
                await WriteAcceptedEffectOutputsAsync(
                    "Рана перевязана, но кровотечение требует внимания.",
                    "Осмотреть повязку");
                await WriteEffectRepairReadyAsync(request, "Complete coherent response resubmitted.");

                var laterRequest = await TryWaitForNewRepairAttemptAsync(
                    thirdRequest,
                    minimumAttempt: 4,
                    TimeSpan.FromSeconds(4));
                if (laterRequest != null)
                {
                    Assert.Contains(
                        "accepted_turn_stale_player_facing_output_after_canonical_repair",
                        laterRequest,
                        StringComparison.Ordinal);
                    await WriteAcceptedEffectOutputsAsync(
                        "Рана перевязана; новая повязка надёжно удерживает края.",
                        "Продолжить путь");
                    await WriteEffectRepairReadyAsync(request, "Player-facing output regenerated after canonical repair.");
                }
            }
            catch (Exception exception)
            {
                gmFailure = exception;
                await WriteEffectRepairReadyAsync(request, "Abort test worker wait.");
            }
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "effect materialization repair",
            snapshotContext,
            rollbackSnapshot,
            request.TurnNumber,
            request.ProgressionControl);
        await gmRepair;

        Assert.Null(gmFailure);
        Assert.True(readyOnlyRejected);
        Assert.True(partialEffectOnlyRejected);
        Assert.True(accepted);
        Assert.False(_fs.FileExists(EffectAcceptedTurnPlan.CommandPath));
        Assert.True(_fs.FileExists(EffectCarrierCatalog.PlayerPath));
        Assert.True(_fs.FileExists(EffectAcceptedTurnPlan.IdentityIndexPath));
        Assert.True(_fs.FileExists("output/narrative_response.json"));
        Assert.True(_fs.FileExists("output/interface_updates.json"));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ActionableEffectRestoresBaselineBeforeDispatch()
    {
        const string sessionId = "session_effect_repair_baseline";
        const string requestId = "request_effect_repair_baseline";
        const int turnNumber = 42;
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        const string rejectedJson = "{\"description\":\"Непринятая перемена\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "actionable_effect_repair_baseline");
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{trackedPath}",
            baselineJson);
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await _fs.WriteFileAtomicAsync(trackedPath, rejectedJson);

        var issue = CreateEffectRepairIssue();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        string? observedAtDispatch = null;
        JsonElement? observedPacket = null;
        Exception? gmAssertionFailure = null;
        var gmRepair = Task.Run(async () =>
        {
            try
            {
                var requestJson = await WaitForValidationRepairRequestContainingAsync(
                    issue.Code!,
                    TimeSpan.FromSeconds(5));
                observedAtDispatch = await _fs.ReadFileAsync(trackedPath);
                using var document = JsonDocument.Parse(requestJson);
                Assert.True(document.RootElement
                    .GetProperty("fullTurnResubmissionRequired")
                    .GetBoolean());
                observedPacket = document.RootElement
                    .GetProperty("harnessRepairPackets")
                    .EnumerateArray()
                    .Single(packet => packet.GetProperty("kind").GetString() ==
                        "effect_materialization_repair")
                    .Clone();
            }
            catch (Exception exception)
            {
                gmAssertionFailure = exception;
            }
            finally
            {
                await WriteJsonAsync(
                    "game_state/control/validation_repair_ready.json",
                    new
                    {
                        sessionId,
                        requestId,
                        turnNumber,
                        updatedAtUtc = "2026-08-22T00:00:00Z",
                        note = "Baseline observed before bounded effect repair."
                    });
            }
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "actionable effect repair",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);
        await gmRepair;

        Assert.Null(gmAssertionFailure);
        Assert.True(accepted);
        Assert.Equal(baselineJson, observedAtDispatch);
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
        var packet = Assert.IsType<JsonElement>(observedPacket);
        Assert.Equal("effect-apply:effectChanges[0]", packet.GetProperty("actor").GetString());
        Assert.Equal("effectChanges", packet.GetProperty("route").GetString());
        Assert.Equal(
            "effectChanges[0].parameters.amount",
            packet.GetProperty("rawCoordinate").GetString());
        Assert.Equal(
            "wound_test_torn_side",
            packet.GetProperty("expectedSource").GetProperty("sourceId").GetString());
        Assert.Equal(
            "player_current",
            packet.GetProperty("expectedTarget").GetProperty("targetId").GetString());
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ProtectedEffectAuthorityFailsClosedBeforeDispatch()
    {
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]));
        var issue = CreateEffectRepairIssue(
            code: "effect_identity_duplicate_effect_id");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected effect identity",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists(
            "game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists(
            "game_state/control/validation_diagnostic_failure_report.json"));
        var report = await _fs.ReadFileAsync(
            "game_state/control/validation_diagnostic_failure_report.json");
        Assert.Contains(issue.Code!, report, StringComparison.Ordinal);
        Assert.Contains(issue.FilePath, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ActionableEffectWithoutRollbackFailsClosedBeforeDispatch()
    {
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]));
        var issue = CreateEffectRepairIssue();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "effect repair without rollback",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists(
            "game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists(
            "game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ActionableEffectWithoutValidatedPendingSnapshotDoesNotDispatch()
    {
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_missing_snapshot");
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"description\":\"Отклонено\"}");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "effect repair without pending authority",
            new List<ValidationIssue> { CreateEffectRepairIssue() },
            1,
            rollbackSnapshot,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_ready.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_FormattingOnlyJsonDoesNotSatisfySemanticFreshness()
    {
        const string outputPath = "output/narrative_response.json";
        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Исходный рассказ.","timestamp":"2026-08-22T01:00:00Z"}
        """);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_semantic_freshness");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Отвергнутый рассказ.","timestamp":"2026-08-22T01:01:00Z"}
        """);
        var changed = await InvokePrivateTaskResultAsync(
            engine,
            "CaptureChangedRollbackTrackedPathsForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        var requiredPaths = ReadRepairResubmissionPaths(changed);
        Assert.Contains(outputPath, requiredPaths, StringComparer.OrdinalIgnoreCase);

        await InvokePrivateTaskAsync(
            engine,
            "RestorePreTurnBaselineForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        await _fs.WriteFileAtomicAsync(outputPath, """
        {
          "timestamp": "2026-08-22T01:00:00Z",
          "response": "Исходный рассказ."
        }
        """);

        Assert.False(await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            changed,
            repairSessionGeneration));

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Новый согласованный рассказ.","timestamp":"2026-08-22T01:02:00Z"}
        """);
        Assert.True(await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            changed,
            repairSessionGeneration));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_DuplicatePropertyReplayFailsClosedWithoutThrowing()
    {
        const string outputPath = "output/narrative_response.json";
        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Исходный рассказ.","timestamp":"2026-08-22T01:00:00Z"}
        """);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_duplicate_property_replay");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Отвергнутый рассказ.","timestamp":"2026-08-22T01:01:00Z"}
        """);
        var changed = await InvokePrivateTaskResultAsync(
            engine,
            "CaptureChangedRollbackTrackedPathsForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);

        await InvokePrivateTaskAsync(
            engine,
            "RestorePreTurnBaselineForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Новый рассказ.","response":"Подменённый рассказ.","timestamp":"2026-08-22T01:02:00Z"}
        """);

        Assert.False(await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            changed,
            repairSessionGeneration));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_DeletingPreExistingRequiredPathDoesNotSatisfyReplay()
    {
        const string outputPath = "output/narrative_response.json";
        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Исходный рассказ.","timestamp":"2026-08-22T01:00:00Z"}
        """);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_required_path_existence");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Отвергнутый рассказ.","timestamp":"2026-08-22T01:01:00Z"}
        """);
        var changed = await InvokePrivateTaskResultAsync(
            engine,
            "CaptureChangedRollbackTrackedPathsForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        var requiredPaths = ReadRepairResubmissionPaths(changed);
        Assert.Contains(outputPath, requiredPaths, StringComparer.OrdinalIgnoreCase);

        await InvokePrivateTaskAsync(
            engine,
            "RestorePreTurnBaselineForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        _fs.DeleteFile(outputPath);

        Assert.False(await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            changed,
            repairSessionGeneration));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_ReplacedSessionCannotDispatchOrRollback()
    {
        const string trackedPath = "game_state/world/weather.json";
        const string replacementJson = "{\"description\":\"Новая сессия\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, "{\"description\":\"Старая сессия\"}");
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_repair_replaced_session");
        string capturedGeneration;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            capturedGeneration = _fs.GetOrCreateSessionGeneration(lease);
        await using (var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease))
        {
            _fs.RotateSessionGeneration(replacementLease);
            await _fs.WriteFileAtomicAsync(replacementLease, trackedPath, replacementJson);
        }

        var task = InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "stale effect repair",
            new List<ValidationIssue> { CreateEffectRepairIssue() },
            1,
            rollbackSnapshot,
            capturedGeneration);

        await Assert.ThrowsAsync<GmWorkerSessionReplacedException>(() => task);
        Assert.Equal(replacementJson, await _fs.ReadFileAsync(trackedPath));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_WorkerParityFallsBackToMainGmForFullResponseReplay()
    {
        const string sessionId = "session_effect_repair_worker_parity";
        const string requestId = "request_effect_repair_worker_parity";
        const int turnNumber = 42;
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var workerSentinelPath = Path.Combine(_rootPath, "effect-worker-was-invoked.txt");
        var workerScriptPath = Path.Combine(_rootPath, "effect-full-replay-worker.ps1");
        await File.WriteAllTextAsync(
            workerScriptPath,
            $"Set-Content -LiteralPath '{workerSentinelPath.Replace("'", "''")}' -Value 'invoked'; exit 9",
            System.Text.Encoding.UTF8);
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource([]),
            configureSettings: settings =>
        {
            settings.GmWorkerBridgeProfiles.Add(
                GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
                {
                    LaunchCommand =
                        $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{workerScriptPath}\"",
                    TimeoutSeconds = 10
                });
        });
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_worker_parity");
        await _fs.WriteFileAtomicAsync(
            $"game_state/control/pending_turn_snapshot/{trackedPath}",
            baselineJson);
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            trackedPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await _fs.WriteFileAtomicAsync(
            trackedPath,
            "{\"description\":\"Отвергнутый ответ\"}");
        var generation = await GetOrCreateSessionGenerationAsync();
        string? requestJson = null;
        var gmRepair = Task.Run(async () =>
        {
            requestJson = await WaitForValidationRepairRequestContainingAsync(
                "effect_source_parameter_required",
                TimeSpan.FromSeconds(5));
            await WriteJsonAsync(
                "game_state/control/validation_repair_ready.json",
                new
                {
                    sessionId,
                    requestId,
                    turnNumber,
                    updatedAtUtc = "2026-08-22T02:55:00Z",
                    note = "Main GM owns the complete response replay."
                });
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "effect worker parity",
            new List<ValidationIssue> { CreateEffectRepairIssue() },
            1,
            rollbackSnapshot,
            generation);
        await gmRepair;

        Assert.True(accepted);
        Assert.False(File.Exists(workerSentinelPath));
        Assert.False(_fs.FileExists(
            "game_state/control/gm_worker_latest_validation_repair_task.json"));
        Assert.False(_fs.FileExists(GmWorkerAuditLog.AuditLogPath));
        Assert.NotNull(requestJson);
        using var document = JsonDocument.Parse(requestJson!);
        Assert.True(document.RootElement
            .GetProperty("fullTurnResubmissionRequired")
            .GetBoolean());
        Assert.Contains(
            document.RootElement.GetProperty("harnessRepairPackets").EnumerateArray(),
            packet => packet.GetProperty("kind").GetString() ==
                      "effect_materialization_repair");
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_DuplicateCommandPropertyDuringRetryFailsClosed()
    {
        var engine = CreateGameEngine();
        var packet = Assert.Single(EffectRepairPacketBuilder.Build(
            new[] { CreateEffectRepairIssue() },
            rollbackAvailable: true));
        var correction = Assert.Single(packet.ExactFieldCorrections);
        var obligationType = typeof(GameEngine).GetNestedType(
            "EffectRepairRetryObligation",
            BindingFlags.NonPublic) ?? throw new InvalidOperationException(
            "Effect repair obligation type was not found.");
        var constructor = Assert.Single(
            obligationType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => candidate.GetParameters().Length == 7);
        var obligation = constructor.Invoke(new object?[]
        {
            packet.Actor,
            packet.RawCoordinate,
            packet.ExpectedSource.DeepClone().AsObject(),
            packet.ExpectedTarget.DeepClone().AsObject(),
            packet.ExpectedDefinitionKey,
            packet.ExpectedEventRef.DeepClone().AsObject(),
            correction.ExpectedValueJson
        });
        var obligations = Array.CreateInstance(obligationType, 1);
        obligations.SetValue(obligation, 0);
        await _fs.WriteFileAtomicAsync(
            EffectAcceptedTurnPlan.CommandPath,
            "{\"effectChanges\":[],\"effectChanges\":[],\"effectResolutionReceipts\":[]}");

        var result = await InvokePrivateTaskResultAsync(
            engine,
            "HasExactEffectRepairResubmissionAsync",
            obligations);

        Assert.False(Assert.IsType<bool>(result));
    }

    private static ValidationIssue CreateEffectRepairIssue(
        string code = "effect_source_parameter_required")
    {
        const string actor = "effect-apply:effectChanges[0]";
        const string coordinate = "effectChanges[0].parameters.amount";
        var issue = new ValidationIssue(
            coordinate,
            IssueSeverity.Error,
            "Required exact source parameter is missing.",
            code: code,
            actor: actor,
            section: "effect_materialization",
            expected: "one exact source-owned value",
            actual: "missing",
            repairHint: "Resubmit the complete turn with amount=3.",
            repairTargetFiles: new[] { EffectAcceptedTurnPlan.CommandPath });
        issue.EffectRepairContext = new EffectRepairContext(
            actor,
            "effectChanges",
            coordinate,
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_test_torn_side",
                ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
            },
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            EffectMaterializationTestFixture.DefinitionKey,
            new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = "turn_42"
            },
            "3");
        return issue;
    }

    private async Task SeedSingletonEffectRepairAuthorityAsync(int turn)
    {
        var inventoryJson = await _fs.ReadFileAsync("game_state/inventory/items.json");
        if (!string.IsNullOrWhiteSpace(inventoryJson) &&
            JsonNode.Parse(inventoryJson) is JsonObject inventory &&
            inventory["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                item.Remove("durability");
                item.Remove("maxDurability");
            }
            await WriteJsonAsync("game_state/inventory/items.json", inventory);
        }

        var playerStatusJson = await _fs.ReadFileAsync("game_state/core/player_status.json");
        if (!string.IsNullOrWhiteSpace(playerStatusJson) &&
            JsonNode.Parse(playerStatusJson) is JsonObject playerStatus)
        {
            playerStatus.Remove("healthPercentage");
            playerStatus.Remove("energyPercentage");
            playerStatus.Remove("poisePercentage");
            await WriteJsonAsync("game_state/core/player_status.json", playerStatus);
        }

        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["parameterBounds"]!["amount"]!["required"] = true;
        definition["parameterBounds"]!["amount"]!["minimum"] = 3;
        definition["parameterBounds"]!["amount"]!["maximum"] = 3;
        await WriteJsonAsync(
            EffectMaterializationTestContext.PlayerWoundsPath,
            new JsonArray(new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["woundName"] = "Рваная рана в боку",
                ["severity"] = "severe",
                ["description"] = "Края раны снова разошлись.",
                ["activeEffectDefinitions"] = new JsonArray(definition)
            }));

        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: turn - 1,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);
    }

    private Task WriteEffectRepairCommandAsync(bool includeRequiredAmount)
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"] = includeRequiredAmount
            ? new JsonObject { ["amount"] = 3 }
            : new JsonObject();
        return WriteJsonAsync(
            EffectAcceptedTurnPlan.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
    }

    private async Task WriteAcceptedEffectOutputsAsync(string response, string option)
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response,
            timestamp = DateTime.UtcNow.ToString("O")
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { new { text = option, category = "continue" } },
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }

    private Task WriteEffectRepairReadyAsync(TurnRequest request, string note) =>
        WriteJsonAsync("game_state/control/validation_repair_ready.json", new
        {
            sessionId = request.SessionId,
            requestId = request.RequestId,
            turnNumber = request.TurnNumber,
            updatedAtUtc = DateTime.UtcNow.ToString("O"),
            note
        });

    private async Task<string> WaitForNewRepairAttemptAsync(
        string previousRequest,
        int minimumAttempt,
        TimeSpan timeout) =>
        await TryWaitForNewRepairAttemptAsync(previousRequest, minimumAttempt, timeout) ??
        throw new TimeoutException($"Timed out waiting for repair attempt {minimumAttempt}.");

    private async Task<string?> TryWaitForNewRepairAttemptAsync(
        string previousRequest,
        int minimumAttempt,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var json = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
            if (!string.IsNullOrWhiteSpace(json) &&
                !string.Equals(json, previousRequest, StringComparison.Ordinal))
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("revalidationAttempt", out var attempt) &&
                    attempt.GetInt32() >= minimumAttempt)
                {
                    return json;
                }
            }
            await Task.Delay(50);
        }
        return null;
    }

    private static IReadOnlyList<string> ReadRepairResubmissionPaths(object obligations)
    {
        var values = Assert.IsAssignableFrom<IEnumerable>(obligations);
        return values.Cast<object>().Select(value =>
        {
            var property = value.GetType().GetProperty("Path");
            Assert.NotNull(property);
            return Assert.IsType<string>(property!.GetValue(value));
        }).ToArray();
    }
}
