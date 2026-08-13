using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalLocationErrors_UsesExactBoundedPacket()
    {
        const string actor = "mortal_location:new:locref_turn_18_drowned_gate";
        var context = new MortalLocationRepairContext(
            "worldMapUpdates.newLocations[2]",
            "mortal_location",
            "locref_turn_18_drowned_gate",
            "mlocmat_turn_18_drowned_gate",
            new[] { "description", "coordinates" },
            ExpectedSourceTurn: 18,
            ExpectedSourceAuthorityKind: "turn_outcome",
            ExpectedSourceAuthorityId: "turn_18");
        var missing = CreateMortalLocationIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[2].description",
            "mortal_location_materialization_governed_field_missing",
            actor,
            "complete description",
            "missing",
            context,
            MortalLocationMaterializationContract.WorldMapPath);
        var conflict = CreateMortalLocationIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[2].coordinates",
            "mortal_location_materialization_coordinate_collision",
            actor,
            "unique exact coordinate",
            "x=4,y=1,z=0 already used",
            context,
            MortalLocationMaterializationContract.WorldMapPath);
        var itemOwned = new ValidationIssue(
            "game_state/inventory/items.json.UpdateInventory[0].description",
            IssueSeverity.Error,
            "Malformed item.",
            code: "mortal_item_materialization_complete_field_missing",
            actor: "mortal_item:new:itemref_drowned_gate",
            section: "MortalItemMaterialization",
            expected: "description",
            actual: "missing",
            repairTargetFiles: new[] { "game_state/inventory/items.json" });

        var engine = CreateGameEngine();
        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[]
            {
                "raw Mortal location validation",
                new List<ValidationIssue> { missing, conflict, itemOwned },
                1
            })!);

        await task;

        var requestJson = await _fs.ReadFileAsync(
            "game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var document = JsonDocument.Parse(requestJson!);
        var packets = document.RootElement
            .GetProperty("harnessRepairPackets")
            .EnumerateArray()
            .ToArray();
        var packet = Assert.Single(packets, candidate =>
            candidate.GetProperty("kind").GetString() ==
            "mortal_location_materialization_repair");

        Assert.Equal("blocking", packet.GetProperty("priority").GetString());
        Assert.Equal("world_map_creation", packet.GetProperty("transitionClass").GetString());
        Assert.Equal("world_map_creation", packet.GetProperty("route").GetString());
        Assert.Equal("worldMapUpdates", packet.GetProperty("rawCarrier").GetString());
        Assert.Equal(
            "worldMapUpdates.newLocations[2]",
            packet.GetProperty("rawCoordinate").GetString());
        Assert.Equal(
            new[] { actor },
            packet.GetProperty("canonicalActorNames")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            packet.GetProperty("targetFiles")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(
            new[] { missing.FilePath },
            packet.GetProperty("missingFields")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(
            new[] { conflict.FilePath },
            packet.GetProperty("conflicts")
                .EnumerateArray()
                .Select(value => value.GetString()));
        var expectedAuthority = packet.GetProperty("expectedAuthority")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains("sourceTurn=18", expectedAuthority);
        Assert.Contains("sourceAuthority=turn_outcome:turn_18", expectedAuthority);
        Assert.DoesNotContain(
            packet.GetProperty("targetFiles").EnumerateArray(),
            value => value.GetString() == "game_state/inventory/items.json");
        Assert.DoesNotContain(
            packet.GetProperty("exactFieldCorrections").EnumerateArray(),
            correction => correction.GetProperty("code").GetString()?.StartsWith(
                "mortal_item_",
                StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            packets,
            candidate => candidate.GetProperty("kind").GetString() is
                "mortal_location_transition_repair" or
                "mortal_world_map_adjacency_repair");
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ProtectedMortalLocationAuthorityFailsClosedBeforeGmDispatch()
    {
        var issue = new ValidationIssue(
            MortalLocationIdentityState.StatePath,
            IssueSeverity.Error,
            "The GM changed client-owned location identity authority.",
            code: "mortal_location_materialization_gm_authored_client_field",
            actor: "mortal_location:index",
            section: "MortalLocationMaterialization",
            expected: "validated pre-turn index",
            actual: "forged entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalLocationIdentityState.StatePath });
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected Mortal location authority",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
        var reportJson = await _fs.ReadFileAsync(
            "game_state/control/validation_diagnostic_failure_report.json");
        Assert.False(string.IsNullOrWhiteSpace(reportJson));
        using var report = JsonDocument.Parse(reportJson!);
        var diagnostic = Assert.Single(report.RootElement.GetProperty("errors").EnumerateArray());
        Assert.Equal(issue.Code, diagnostic.GetProperty("code").GetString());
        Assert.Equal(issue.FilePath, diagnostic.GetProperty("filePath").GetString());
        Assert.Equal(issue.Actor, diagnostic.GetProperty("actor").GetString());
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ActionableMortalLocationRestoresBaselineBeforeDispatch()
    {
        const string sessionId = "session_location_repair_baseline";
        const string requestId = "request_location_repair_baseline";
        const int turnNumber = 42;
        const string trackedPath = "game_state/world/weather.json";
        const string baselineJson = "{\"description\":\"До хода\"}";
        const string rejectedJson = "{\"description\":\"Непринятая перемена\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baselineJson);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "actionable_location_repair_baseline");
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

        var issue = CreateMortalLocationIssue(
            "game_state/world/current_location.json.currentLocationData.description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_repair_baseline",
            "complete description",
            "missing",
            new MortalLocationRepairContext(
                "currentLocationData",
                "mortal_location",
                "locref_repair_baseline",
                "mlocmat_repair_baseline",
                new[] { "description" }),
            MortalLocationMaterializationContract.CurrentLocationPath);
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        string? observedAtDispatch = null;
        var gmRepair = Task.Run(async () =>
        {
            await WaitForValidationRepairRequestContainingAsync(
                issue.Code!,
                TimeSpan.FromSeconds(5));
            observedAtDispatch = await _fs.ReadFileAsync(trackedPath);
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId,
                requestId,
                turnNumber,
                updatedAtUtc = "2026-08-12T00:00:00Z",
                note = "Baseline observed before repair dispatch."
            });
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "actionable Mortal location repair",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);
        await gmRepair;

        Assert.True(accepted);
        Assert.Equal(baselineJson, observedAtDispatch);
        Assert.Equal(baselineJson, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ActionableMortalLocationWithoutRollbackFailsClosedBeforeDispatch()
    {
        var engine = CreateGameEngine();
        var issue = CreateMortalLocationIssue(
            "game_state/world/current_location.json.currentLocationData.description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_repair_without_baseline",
            "complete description",
            "missing",
            new MortalLocationRepairContext(
                "currentLocationData",
                "mortal_location",
                "locref_repair_without_baseline",
                "mlocmat_repair_without_baseline",
                new[] { "description" }),
            MortalLocationMaterializationContract.CurrentLocationPath);
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "actionable Mortal location repair without baseline",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task AcceptedTurnRepairLoop_ActionableMortalLocationWithoutRollbackFailsClosedBeforeCaptureOrDispatch()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        await WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locationId"] = null,
                ["state"] = "pending_materialization"
            });
        await WriteJsonAsync(MortalLocationIdentityState.StatePath, MortalLocationIdentityState.CreateEmptyRoot());

        var request = new TurnRequest
        {
            SessionId = "session_location_repair_without_rollback",
            RequestId = "request_location_repair_without_rollback",
            TurnNumber = 42,
            PlayerAction = "Открыть путь к Чёрному броду.",
            Timestamp = "2026-08-12T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            null,
            "location repair without rollback test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var rejected = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        rejected.Remove("description");
        await WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = rejected });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "обработки хода без rollback snapshot",
            snapshotContext,
            null,
            request.TurnNumber,
            request.ProgressionControl);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task AcceptedTurnRepairLoop_ReadyWithoutExactLocationResubmissionCannotBecomeNoOp()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("output/narrative_response.json");
        _fs.DeleteFile("output/interface_updates.json");
        var emptyMap = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var pendingCurrent = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };
        await WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, emptyMap);
        await WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath, pendingCurrent);
        await WriteJsonAsync(MortalLocationIdentityState.StatePath, MortalLocationIdentityState.CreateEmptyRoot());

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "location_repair_retry_obligation");
        var request = new TurnRequest
        {
            SessionId = "session_location_retry_obligation",
            RequestId = "request_location_retry_obligation",
            TurnNumber = 42,
            PlayerAction = "Открыть путь к Чёрному броду.",
            Timestamp = "2026-08-12T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "location repair retry obligation test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var rejected = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        rejected.Remove("description");
        await WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = rejected });
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Неполный ответ не должен быть принят.",
            timestamp = "2026-08-12T00:01:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = Array.Empty<object>(),
            timestamp = "2026-08-12T00:01:00Z"
        });

        var secondRequestObserved = false;
        var gmRepair = Task.Run(async () =>
        {
            var initialRepairRequest = await WaitForValidationRepairRequestContainingAsync(
                "mortal_location_materialization_governed_field_missing",
                TimeSpan.FromSeconds(5));
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                updatedAtUtc = "2026-08-12T00:02:00Z",
                note = "Ready without resubmission must not clear the obligation."
            });

            string? exactResubmissionRequest = null;
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                var json = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
                if (!string.IsNullOrWhiteSpace(json) &&
                    !string.Equals(json, initialRepairRequest, StringComparison.Ordinal))
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("revalidationAttempt", out var attempt) &&
                        attempt.GetInt32() >= 2)
                    {
                        secondRequestObserved = true;
                        exactResubmissionRequest = json;
                        break;
                    }
                }
                await Task.Delay(50);
            }
            if (!secondRequestObserved)
                return;

            var corrected = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
            await WriteJsonAsync(
                MortalLocationMaterializationContract.CurrentLocationPath,
                new JsonObject { ["currentLocationData"] = corrected });
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Чёрный брод проступает из холодного тумана.",
                timestamp = "2026-08-12T00:03:00Z"
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = new[] { new { text = "Ступить на брод", category = "travel" } },
                timestamp = "2026-08-12T00:03:00Z"
            });
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                updatedAtUtc = "2026-08-12T00:03:01Z",
                note = "The exact raw candidate was resubmitted."
            });

            await WaitForUpdatedValidationRepairRequestContainingAsync(
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                exactResubmissionRequest!,
                TimeSpan.FromSeconds(8));
            await WriteJsonAsync("output/narrative_response.json", new
            {
                response = "Чёрный брод проступает из холодного тумана; путь теперь устойчив.",
                timestamp = "2026-08-12T00:04:00Z"
            });
            await WriteJsonAsync("output/interface_updates.json", new
            {
                dialogueOptions = new[] { new { text = "Ступить на устойчивый брод", category = "travel" } },
                timestamp = "2026-08-12T00:04:00Z"
            });
            await WriteJsonAsync("game_state/control/validation_repair_ready.json", new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                updatedAtUtc = "2026-08-12T00:04:01Z",
                note = "Player-facing output was regenerated after canonical repair."
            });
        });

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "обработки хода",
            snapshotContext,
            rollbackSnapshot,
            request.TurnNumber,
            request.ProgressionControl);
        await gmRepair;

        Assert.True(secondRequestObserved);
        Assert.True(accepted);
        var finalMap = JsonNode.Parse((await _fs.ReadFileAsync(
            MortalLocationMaterializationContract.WorldMapPath))!)!.AsObject();
        Assert.Single(finalMap["locations"]!.AsArray());
    }

    [Fact]
    public async Task RepairResubmission_FormattingOnlyJsonDoesNotSatisfyChangedPathObligation()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        const string outputPath = "output/narrative_response.json";
        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Исходный рассказ.","timestamp":"2026-08-12T00:00:00Z"}
        """);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "semantic_resubmission_baseline");
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Отклонённый рассказ.","timestamp":"2026-08-12T00:01:00Z"}
        """);
        var changedResult = await InvokePrivateTaskResultAsync(
            engine,
            "CaptureChangedRollbackTrackedPathsForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        var requiredPaths = Assert.IsAssignableFrom<IReadOnlyList<string>>(changedResult);
        Assert.Contains(outputPath, requiredPaths, StringComparer.OrdinalIgnoreCase);

        await InvokePrivateTaskAsync(
            engine,
            "RestorePreTurnBaselineForRepairSessionAsync",
            rollbackSnapshot,
            repairSessionGeneration);
        await _fs.WriteFileAtomicAsync(outputPath, """
        {
          "timestamp": "2026-08-12T00:00:00Z",
          "response": "Исходный рассказ."
        }
        """);

        var formattingOnlyAccepted = await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            requiredPaths,
            repairSessionGeneration);
        Assert.False(formattingOnlyAccepted);

        await _fs.WriteFileAtomicAsync(outputPath, """
        {"response":"Действительно новый рассказ.","timestamp":"2026-08-12T00:02:00Z"}
        """);
        var semanticChangeAccepted = await InvokePrivateAsync<bool>(
            engine,
            "AreRollbackTrackedPathsResubmittedForRepairSessionAsync",
            rollbackSnapshot,
            requiredPaths,
            repairSessionGeneration);
        Assert.True(semanticChangeAccepted);
    }

    [Fact]
    public async Task ProtectedMortalLocationDiagnostic_SurvivesCallerOwnedAcceptedTurnRollback()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тихий свидетель",
                currentRealm = "Mortal World",
                currentIncarnation = 1
            });
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "protected_location_diagnostic");
        var issue = new ValidationIssue(
            MortalLocationIdentityState.StatePath,
            IssueSeverity.Error,
            "The GM changed client-owned location identity authority.",
            code: "mortal_location_materialization_gm_authored_client_field",
            actor: "mortal_location:index",
            section: "MortalLocationMaterialization",
            expected: "validated pre-turn index",
            actual: "forged entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalLocationIdentityState.StatePath });
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected Mortal location authority",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);
        Assert.False(accepted);
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
        var reportJson = await _fs.ReadFileAsync(
            "game_state/control/validation_diagnostic_failure_report.json");
        Assert.Contains(issue.Code!, reportJson, StringComparison.Ordinal);
        Assert.Contains(issue.FilePath, reportJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnosticOnlyFailure_LeavesOneUsableRollbackForCallerAndPreservesReport()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тихий свидетель",
                currentRealm = "Mortal World",
                currentIncarnation = 1
            });
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "diagnostic_only_caller_rollback");
        await WriteJsonAsync(
            "game_state/world/world_events.json",
            new { events = new[] { new { name = "Непринятое событие" } } });
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        var issue = new ValidationIssue(
            "game_state/world/world_events.json.events[0]",
            IssueSeverity.Error,
            "Canonical validation cannot continue without usable pending metadata.",
            code: "accepted_turn_invalid_snapshot_baseline");

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "FailClosedDiagnosticOnlyValidationRepairAsync",
            "diagnostic-only caller rollback",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);
        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task AcceptedTurnCanonicalWriteFailure_ReturnsToCallerForOneFullRollback()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("output/narrative_response.json");
        _fs.DeleteFile("output/interface_updates.json");
        var emptyMap = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var pendingCurrent = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };
        var emptyLocationIndex = MortalLocationIdentityState.CreateEmptyRoot();
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            emptyMap.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            pendingCurrent.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            emptyLocationIndex.ToJsonString());

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "accepted_location_write_failure");
        var request = new TurnRequest
        {
            SessionId = "session_location_write_failure",
            RequestId = "request_location_write_failure",
            TurnNumber = 42,
            PlayerAction = "Материализовать Чёрный брод.",
            Timestamp = "2026-08-12T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "accepted location write failure test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        await WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });
        await WriteJsonAsync(
            "output/narrative_response.json",
            new
            {
                response = "Чёрный брод уже принят, хотя запись индекса ещё не завершилась.",
                timestamp = "2026-08-12T00:01:00Z"
            });
        await WriteJsonAsync(
            "output/interface_updates.json",
            new
            {
                dialogueOptions = new[] { new { text = "Перейти мост", category = "travel" } },
                timestamp = "2026-08-12T00:01:00Z"
            });
        ArmCanonicalWriteFailure(MortalLocationIdentityState.StatePath);

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "обработки хода",
            snapshotContext,
            rollbackSnapshot,
            request.TurnNumber,
            request.ProgressionControl);

        Assert.False(accepted);
        InvokePrivate(engine, "ClearTransientOutputFiles");
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.True(JsonNode.DeepEquals(
            emptyMap,
            JsonNode.Parse((await _fs.ReadFileAsync(
                MortalLocationMaterializationContract.WorldMapPath))!)));
        Assert.True(JsonNode.DeepEquals(
            pendingCurrent,
            JsonNode.Parse((await _fs.ReadFileAsync(
                MortalLocationMaterializationContract.CurrentLocationPath))!)));
        Assert.True(JsonNode.DeepEquals(
            emptyLocationIndex,
            JsonNode.Parse((await _fs.ReadFileAsync(
                MortalLocationIdentityState.StatePath))!)));
        Assert.False(_fs.FileExists("output/narrative_response.json"));
        Assert.False(_fs.FileExists("output/interface_updates.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
        var report = await _fs.ReadFileAsync(
            "game_state/control/validation_diagnostic_failure_report.json");
        Assert.Contains(MortalLocationIdentityState.StatePath, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AcceptedTurnCanonicalRefreshFailure_DiagnosticWriteFailureStillLeavesRollbackToCaller()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тихий свидетель",
                currentRealm = "Mortal World",
                currentIncarnation = 1
            });
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "canonical_refresh_diagnostic_write_failure");
        await WriteJsonAsync(
            "game_state/world/world_events.json",
            new { events = new[] { new { name = "Непринятое событие" } } });
        ArmCanonicalWriteFailure("game_state/control/validation_diagnostic_failure_report.json");

        await InvokePrivateTaskAsync(
            engine,
            "FailClosedAcceptedTurnCanonicalRefreshAsync",
            "injected diagnostic write failure",
            new IOException("Injected canonical refresh failure."),
            rollbackSnapshot);

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    [Fact]
    public async Task ProtectedMortalLocationDiagnosticWriteFailure_StillLeavesRollbackToCaller()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тихий свидетель",
                currentRealm = "Mortal World",
                currentIncarnation = 1
            });
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "protected_location_diagnostic_write_failure");
        await WriteJsonAsync(
            "game_state/world/world_events.json",
            new { events = new[] { new { name = "Непринятое событие" } } });
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        var issue = new ValidationIssue(
            MortalLocationIdentityState.StatePath,
            IssueSeverity.Error,
            "The GM changed client-owned location identity authority.",
            code: "mortal_location_materialization_gm_authored_client_field",
            actor: "mortal_location:index",
            section: "MortalLocationMaterialization",
            expected: "validated pre-turn index",
            actual: "forged entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalLocationIdentityState.StatePath });
        ArmCanonicalWriteFailure("game_state/control/validation_diagnostic_failure_report.json");

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected diagnostic write failure",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);

        Assert.False(accepted);
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);
        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    [Fact]
    public async Task DiagnosticOnlyReportWriteFailure_StillLeavesRollbackToCaller()
    {
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new
            {
                soulName = "Тихий свидетель",
                currentRealm = "Mortal World",
                currentIncarnation = 1
            });
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "diagnostic_only_report_write_failure");
        await WriteJsonAsync(
            "game_state/world/world_events.json",
            new { events = new[] { new { name = "Непринятое событие" } } });
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();
        var issue = new ValidationIssue(
            "game_state/world/world_events.json.events[0]",
            IssueSeverity.Error,
            "Canonical validation cannot continue without usable pending metadata.",
            code: "accepted_turn_invalid_snapshot_baseline");
        ArmCanonicalWriteFailure("game_state/control/validation_diagnostic_failure_report.json");

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "FailClosedDiagnosticOnlyValidationRepairAsync",
            "diagnostic-only report write failure",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            repairSessionGeneration);

        Assert.False(accepted);
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);
        Assert.False(_fs.FileExists("game_state/world/world_events.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    [Theory]
    [InlineData(
        "mortal_location_materialization_historical_replay",
        "game_state/world/current_location.json.currentLocationData.initialId",
        "mortal_location:new:locref_replayed")]
    [InlineData(
        "mortal_location_materialization_duplicate_creation_route",
        "game_state/world/current_location.json.currentLocationData.initialId",
        "mortal_location:new:locref_duplicate")]
    [InlineData(
        "mortal_location_materialization_confusable_canonical_identity",
        "game_state/world/current_location.json.currentLocationData.locationId",
        "mortal_location:existing:loc_case_variant")]
    [InlineData(
        "mortal_location_materialization_gm_authored_client_field",
        "game_state/world/current_location.json.currentLocationData.requestId",
        "mortal_location:new:locref_forged_request")]
    [InlineData(
        "mortal_location_materialization_receipt_mismatch",
        "game_state/world/current_location.json.currentLocationData.materializationReceipt",
        "mortal_location:existing:loc_receipt")]
    [InlineData(
        "mortal_location_materialization_seal_mismatch",
        "game_state/world/current_location.json.currentLocationData.seal",
        "mortal_location:existing:loc_seal")]
    [InlineData(
        "mortal_location_materialization_index_mismatch",
        "game_state/world/location_identity_index.json.locationEntries[0]",
        "mortal_location:index")]
    public async Task WaitForContractRepairAsync_UnsafeMortalLocationAuthorityFailsClosedBeforeGmDispatch(
        string code,
        string path,
        string actor)
    {
        var issue = CreateMortalLocationIssue(
            path,
            code,
            actor,
            "validated client-owned authority",
            "unsafe or ambiguous evidence",
            new MortalLocationRepairContext(
                "currentLocationData",
                "mortal_location",
                "locref_guarded",
                "mlocmat_guarded",
                Array.Empty<string>()),
            MortalLocationMaterializationContract.CurrentLocationPath);
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "unsafe Mortal location authority",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task WaitForContractRepairAsync_UnresolvedBootstrapLocationFailsClosedBeforeGmDispatch()
    {
        var issue = new ValidationIssue(
            "currentLocationData",
            IssueSeverity.Error,
            "The exact reserved starting location is missing.",
            code: "mortal_bootstrap_location_start_required",
            section: "mortal_bootstrap",
            expected: "one exact reserved currentLocationData candidate",
            actual: "missing",
            repairHint: "Do not invent a different reservation or permanent identity.");
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "unresolved Mortal bootstrap location",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Theory]
    [InlineData("mortal_location:unknown")]
    [InlineData("mortal_location_link:unknown")]
    [InlineData("mortal_location:unresolved:shared")]
    public async Task WaitForContractRepairAsync_UnresolvedMortalLocationFailsClosedBeforeGmDispatch(
        string actor)
    {
        var issue = new ValidationIssue(
            MortalLocationMaterializationContract.WorldMapPath,
            IssueSeverity.Error,
            "The raw location coordinate cannot be resolved exactly.",
            code: "mortal_location_materialization_invalid_root",
            actor: actor,
            section: "MortalLocationMaterialization",
            expected: "one exact raw candidate",
            actual: "unresolved",
            repairHint: "Fail closed instead of broad repair.",
            repairTargetFiles: new[] { MortalLocationMaterializationContract.WorldMapPath });
        issue.MortalLocationRepairContext = new MortalLocationRepairContext(
            "worldMapUpdates",
            "mortal_location",
            null,
            null,
            Array.Empty<string>());
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "unresolved Mortal location",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.True(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    private static ValidationIssue CreateMortalLocationIssue(
        string path,
        string code,
        string actor,
        string expected,
        string actual,
        MortalLocationRepairContext context,
        params string[] repairTargets)
    {
        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Malformed Mortal location package.",
            code: code,
            actor: actor,
            section: "MortalLocationMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only this exact GM-owned field.",
            repairTargetFiles: repairTargets);
        issue.MortalLocationRepairContext = context;
        return issue;
    }
}
