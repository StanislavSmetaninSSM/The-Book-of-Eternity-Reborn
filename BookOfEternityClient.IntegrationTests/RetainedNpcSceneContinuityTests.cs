using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class RetainedNpcSceneContinuityTests(ITestOutputHelper output)
{
    private const string Core = "game_state/npcs/npc_core.json";
    private const string Current = "game_state/world/current_location.json";
    private const string Map = "game_state/world/world_map.json";
    private const string A = "loc_history_room";
    private const string B = "loc_current_court";
    private const string InitialB = "locref_current_court";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenuineOriginalRetainedActorRemainsOffscreenAcrossSceneChange(bool newScene)
    {
        await using var context = await PrepareAsync(includeKnownB: !newScene);
        var originalCore = (await context.ReadJsonAsync(Core))!.AsObject();
        var originalActor = originalCore["NPCsInScene"]![0]!.DeepClone();
        await SetSceneAsync(context, newScene);
        var currentCore = originalCore.DeepClone().AsObject();
        currentCore["UpdateNPCs"] = new JsonArray(NewActor(newScene));
        await context.WriteJsonAsync(Core, currentCore);
        var before = File.ReadAllBytes(context.FileSystem.ResolvePath(Core));
        // Same-turn location references are resolved from this response envelope,
        // not from the separately written current-location fixture.
        var response = currentCore.DeepClone().AsObject();
        if (newScene)
            response["currentLocationData"] = (await context.ReadJsonAsync(Current))!["currentLocationData"]!.DeepClone();
        using var document = JsonDocument.Parse(response.ToJsonString());
        var issues = context.Validator.ValidateResponse(document.RootElement);
        Report(issues);
        Assert.Equal(before, File.ReadAllBytes(context.FileSystem.ResolvePath(Core)));
        Assert.True(JsonNode.DeepEquals(originalActor, (await context.ReadJsonAsync(Core))!["NPCsInScene"]![0]));
        Assert.Empty(currentCore["NPCsInScene"]!.AsArray().Skip(1));

        // Separate canonical read-consumer proof, not a claim that raw B was accepted:
        // install the existing canonical fixture for B and resolve actual production scope.
        await InstallCanonicalBAsync(context);
        var scope = await new LocalInteractionScopeService(context.FileSystem).ResolveAsync("Mortal World");
        Assert.True(scope.IsResolved, scope.UnavailableReason);
        Assert.Equal(B, scope.LocationId);
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(scope, originalActor.AsObject()));
        Assert.True(JsonNode.DeepEquals(originalActor, (await context.ReadJsonAsync(Core))!["NPCsInScene"]![0]));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error &&
            (issue.FilePath.Contains("UpdateNPCs[0]", StringComparison.Ordinal) ||
             issue.Actor == "mortal_npc:npc_court_steward"));
        Assert.DoesNotContain(issues, issue => IsSceneAnchorIssue(issue) &&
            issue.FilePath.Contains("NPCsInScene[0]", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("changed-row")]
    [InlineData("manifest-removed-active-request")]
    [InlineData("malformed-original")]
    [InlineData("fully-cleared-original")]
    [InlineData("new-wrong-anchor")]
    public async Task HistoricalCarrierDoesNotExemptUnprovenOrChangedRows(string mode)
    {
        await using var context = await PrepareAsync(includeKnownB: false);
        var currentCore = (await context.ReadJsonAsync(Core))!.AsObject();
        await SetSceneAsync(context, newScene: true);
        if (mode == "changed-row")
            currentCore["NPCsInScene"]![0]!["history"] = "A newly authored history is not exact retained DATA.";
        if (mode == "manifest-removed-active-request")
            context.FileSystem.DeleteFile(LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        if (mode == "manifest-removed-active-request")
        {
            Assert.False(context.FileSystem.FileExists(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            Assert.True(context.FileSystem.FileExists(LiveTurnPreparationService.TurnRequestPath));
        }
        if (mode == "malformed-original")
        {
            await context.WriteJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, new JsonObject());
            Assert.Equal("{}", await context.FileSystem.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        }
        if (mode == "fully-cleared-original") await ClearOriginalAsync(context);
        if (mode == "new-wrong-anchor")
        {
            var newcomer = NewActor(newScene: true);
            newcomer["initialLocationId"] = "locref_other_scene";
            currentCore["NPCsInScene"] = new JsonArray(newcomer);
        }
        await context.WriteJsonAsync(Core, currentCore);
        var before = File.ReadAllBytes(context.FileSystem.ResolvePath(Core));
        using var document = JsonDocument.Parse(currentCore.ToJsonString());
        var issues = context.Validator.ValidateResponse(document.RootElement);
        Report(issues);
        Assert.Contains(issues, i => i.Code == (mode == "new-wrong-anchor"
            ? "npc_scene_initial_location_mismatch" : "npc_scene_missing_initial_location_id"));
        Assert.Equal(before, File.ReadAllBytes(context.FileSystem.ResolvePath(Core)));
    }

    [Fact]
    public async Task IdleCanonicalInspectionDoesNotRequireHistoricalActorToMoveWithPlayer()
    {
        await using var context = await PrepareAsync(includeKnownB: true);
        await InstallCanonicalBAsync(context);
        await ClearOriginalAsync(context);
        var before = File.ReadAllBytes(context.FileSystem.ResolvePath(Core));
        var selection = new GameStateValidationSelection(GameStateValidationPhase.NpcStateFiles, new[] { Core });
        var issues = await context.Validator.InspectGameStateAsync(selection);
        // Verify non-leakage before the desired inspection assertion (also reached on RED).
        Assert.Contains(await context.Validator.ValidateGameStateAsync(selection), IsSceneAnchorIssue);
        using var response = JsonDocument.Parse((await context.ReadJsonAsync(Core))!.ToJsonString());
        Assert.Contains(context.Validator.ValidateResponse(response.RootElement), IsSceneAnchorIssue);
        Report(issues);
        Assert.Equal(before, File.ReadAllBytes(context.FileSystem.ResolvePath(Core)));
        Assert.False(context.FileSystem.FileExists(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        Assert.DoesNotContain(issues, IsSceneAnchorIssue);
    }

    [Theory]
    [InlineData("orphan-active-request")]
    [InlineData("unknown-historical-location")]
    [InlineData("repair-active")]
    [InlineData("ready-active")]
    [InlineData("invalid-history-envelope")]
    [InlineData("invalid-map")]
    public async Task InspectionDoesNotAuthorizeActiveOrUnanchoredHistoricalRows(string mode)
    {
        await using var context = await PrepareAsync(includeKnownB: true);
        var request = (await context.ReadJsonAsync(LiveTurnPreparationService.TurnRequestPath))!;
        await InstallCanonicalBAsync(context);
        await ClearOriginalAsync(context);
        if (mode == "orphan-active-request")
            await context.WriteJsonAsync(LiveTurnPreparationService.TurnRequestPath, request);
        else if (mode == "repair-active" || mode == "ready-active")
            await context.WriteJsonAsync(mode == "repair-active"
                ? "game_state/control/validation_repair_request.json" : "ready/turn_complete.json", new JsonObject());
        else if (mode == "invalid-map")
            await context.WriteJsonAsync(Map, new JsonObject());
        else
        {
            var root = (await context.ReadJsonAsync(Core))!;
            if (mode == "invalid-history-envelope") root["NPCsInScene"]![0]!["materialization"] = new JsonObject();
            else root["NPCsInScene"]![0]!["currentLocationId"] = "loc_not_in_current_world";
            await context.WriteJsonAsync(Core, root);
        }
        var before = File.ReadAllBytes(context.FileSystem.ResolvePath(Core));
        var issues = await context.Validator.InspectGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { Core }));
        Report(issues);
        Assert.Contains(issues, IsSceneAnchorIssue);
        Assert.Equal(before, File.ReadAllBytes(context.FileSystem.ResolvePath(Core)));
    }

    [Fact]
    public async Task UnrelatedNpcSidecarInspectionDoesNotReadIdleHistoricalDependencies()
    {
        var armed = false;
        var markerReads = 0;
        await using var context = await PrepareAsync(true, new FileSystemManagerHooks
        {
            AfterCanonicalReadAttemptAsync = path =>
            {
                if (armed && path == "game_state/control/validation_repair_ready.json") markerReads++;
                return Task.CompletedTask;
            }
        });
        await InstallCanonicalBAsync(context);
        await ClearOriginalAsync(context);
        const string inventory = "game_state/npcs/npc_inventory.json";
        await context.WriteJsonAsync(inventory, new JsonObject());
        armed = true;
        await context.Validator.InspectGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { inventory }));
        Assert.Equal(0, markerReads);
        // The same original root's actual historical carrier does reach the read once.
        Assert.DoesNotContain(await context.Validator.InspectGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.NpcStateFiles, new[] { Core })), IsSceneAnchorIssue);
        Assert.Equal(1, markerReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectionPurposeDoesNotAuthorizeNestedResponseOrSurviveReadFailure(bool failRead)
    {
        var armed = false;
        var reached = false;
        EffectMaterializationTestContext? current = null;
        JsonElement response = default;
        var sentinel = new InvalidOperationException("Inspection marker read failed.");
        await using var context = await PrepareAsync(true, new FileSystemManagerHooks
        {
            AfterCanonicalReadAttemptAsync = path =>
            {
                if (armed && !reached && path == "game_state/control/validation_repair_request.json")
                {
                    reached = true;
                    if (failRead) throw sentinel;
                    Assert.Contains(current!.Validator.ValidateResponse(response), IsSceneAnchorIssue);
                }
                return Task.CompletedTask;
            }
        });
        current = context;
        await InstallCanonicalBAsync(context);
        await ClearOriginalAsync(context);
        using var document = JsonDocument.Parse((await context.ReadJsonAsync(Core))!.ToJsonString());
        response = document.RootElement;
        armed = true;
        var selection = new GameStateValidationSelection(GameStateValidationPhase.NpcStateFiles, new[] { Core });
        if (failRead)
            Assert.Same(sentinel, await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Validator.InspectGameStateAsync(selection)));
        else
            Assert.DoesNotContain(await context.Validator.InspectGameStateAsync(selection), IsSceneAnchorIssue);
        Assert.True(reached);
        Assert.Contains(await context.Validator.ValidateGameStateAsync(selection), IsSceneAnchorIssue);
        Assert.Contains(context.Validator.ValidateResponse(response), IsSceneAnchorIssue);
    }

    private static async Task ClearOriginalAsync(EffectMaterializationTestContext context)
    {
        // Explicit owned-fixture teardown, not a simulated accepted turn or permission grant.
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath,
                     LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                     PendingTurnSnapshotAuthority.AuthorityPath, PendingTurnStateService.PendingDiceStatePath })
        {
            context.FileSystem.DeleteFile(lease, path);
            Assert.False(context.FileSystem.FileExists(lease, path));
        }
        context.FileSystem.DeleteDirectoryTree(lease, LiveTurnPreparationService.PendingTurnSnapshotDirectory);
    }

    private static async Task<EffectMaterializationTestContext> PrepareAsync(bool includeKnownB, FileSystemManagerHooks? hooks = null)
    {
        var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        try
        {
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                context.FileSystem.GetOrCreateSessionGeneration(lease);
            await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
            var first = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(A, "Historical room");
            var second = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(B, "Current court", x: 1);
            await context.WriteJsonAsync(Map, includeKnownB
                ? MortalLocationTestFixture.CreateWorldMap(first.DeepClone().AsObject(), second.DeepClone().AsObject())
                : MortalLocationTestFixture.CreateWorldMap(first.DeepClone().AsObject()));
            await context.WriteJsonAsync(Current, MortalLocationTestFixture.CreateCurrentProjection(first));
            var index = MortalLocationTestFixture.CreateIdentityIndex(first);
            if (includeKnownB) index["locationEntries"]!.AsArray().Add(MortalLocationTestFixture.CreateIdentityIndex(second)["locationEntries"]![0]!.DeepClone());
            await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
            var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor("npc_retained_observer");
            actor["NPCId"] = "npc_retained_observer";
            actor.Remove("initialId");
            actor["name"] = "Mira";
            actor["currentLocationId"] = A;
            actor["currentLocationName"] = "Historical room";
            actor["initialLocationId"] = null;
            actor["materialization"]!["materializedAtTurn"] = 40;
            actor["currentActivity"] = null;
            actor["completedActivities"] = new JsonArray(new JsonObject
            {
                ["activityName"] = "Earlier survey", ["completionTurn"] = 40,
                ["finalOutcome"] = "Success", ["narrativeSummary"] = "Retained original history."
            });
            await context.WriteJsonAsync(Core, new JsonObject { ["NPCsInScene"] = new JsonArray(actor), ["UpdateNPCs"] = new JsonArray() });
            await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "retained-scene", RequestId = "retained-scene-turn42", TurnNumber = 42,
                CurrentRealm = "Mortal World", PlayerAction = "Walk to the court without moving Mira.",
                PreGeneratedDices1d20 = [7]
            });
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, new[] { Core });
                Assert.True(signed.Success, string.Join("\n", signed.Issues.Select(i => i.Message)));
                Assert.Equal("retained-scene-turn42", signed.Snapshot!.RequestId);
            }
            // Baseline actor/scene structure must already validate before the tested transition.
            using var baseline = JsonDocument.Parse((await context.ReadJsonAsync(Core))!.ToJsonString());
            var baselineIssues = context.Validator.ValidateResponse(baseline.RootElement);
            Assert.DoesNotContain(baselineIssues, issue => issue.Severity == IssueSeverity.Error);
            return context;
        }
        catch { await context.DisposeAsync(); throw; }
    }

    private static JsonObject NewActor(bool newScene)
    {
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor("npc_court_steward");
        actor["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "none" };
        actor["experience"] = 0;
        actor["experienceForNextLevel"] = 0;
        actor["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 60 })
        };
        actor["currentLocationId"] = newScene ? null : B;
        actor["currentLocationName"] = "Current court";
        actor["initialLocationId"] = newScene ? InitialB : null;
        return actor;
    }

    private static Task SetSceneAsync(EffectMaterializationTestContext context, bool newScene)
    {
        if (!newScene) return InstallCanonicalBAsync(context);
        var scene = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        scene["initialId"] = InitialB;
        scene["materialization"]!["initialId"] = InitialB;
        return context.WriteJsonAsync(Current, new JsonObject { ["currentLocationData"] = scene });
    }

    private static async Task InstallCanonicalBAsync(EffectMaterializationTestContext context)
    {
        var first = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(A, "Historical room");
        var second = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(B, "Current court", x: 1);
        await context.WriteJsonAsync(Map, MortalLocationTestFixture.CreateWorldMap(first, second));
        var index = MortalLocationTestFixture.CreateIdentityIndex(first);
        index["locationEntries"]!.AsArray().Add(MortalLocationTestFixture.CreateIdentityIndex(second)["locationEntries"]![0]!.DeepClone());
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
        await context.WriteJsonAsync(Current, MortalLocationTestFixture.CreateCurrentProjection(second));
    }

    private static bool IsSceneAnchorIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("npc_scene_", StringComparison.Ordinal) == true;

    private void Report(IReadOnlyList<ValidationIssue> issues) => output.WriteLine(JsonSerializer.Serialize(
        issues.Select(i => new { i.Code, i.FilePath, i.Message, i.Actor, i.Expected, i.Actual })));
}
