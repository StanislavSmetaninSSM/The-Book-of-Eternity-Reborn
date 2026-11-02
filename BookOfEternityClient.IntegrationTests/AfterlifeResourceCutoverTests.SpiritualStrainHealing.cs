using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(0, 6)]
    [InlineData(2, 8)]
    public void SpiritualStrainHealing_InitialCapacityUsesItsRealFormulaAndHistory(int focusTier, int maximum)
    {
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(ResourceBootstrapStateBuilder.BuildPristine().Definitions);
        var profiles = Profiles(PlayerSoulProfile(), GuardianProfile("guardian_frame"));
        var (state, history) = BuildActionPointState(definitions, profiles,
            ActiveConflict("conflict_resource_cost"), SoulState(focusTier), maximum, maximum, focusTier, maximum);
        Assert.Equal(2, state.Entries.Count);
        Assert.All(state.Entries, entry => { Assert.Equal((decimal)maximum, entry.Current); Assert.Equal((decimal)maximum, entry.Maximum); });
        Assert.Empty(history.ValidateStateAgreement(state));
        Assert.Equal(2, history.ToCanonicalJson()["entries"]!.AsArray().Count);
    }

    [Fact]
    public void SpiritualStrainHealing_NondefaultCapacityCannotBorrowTheSixPointTemplate()
    {
        Assert.NotNull(ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, null, 6m, 6m, null, null));
        Assert.Null(ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, null, 6m, 6m, null, null,
            spiritFocusTier: 2));
        Assert.Null(ReadTrustedConflictFrameKey(null, SeedOriginalIntakeBaselinesAsync, null, 6m, 6m, null, null,
            oppositionMaximum: 8m));
    }

    /// <summary>Real creation/publication, separate combat healing and replacement roots, then signed old-history consumption.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_ActualCombatWeakeningRetiresRootsAndKeepsSignedHistory()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedStrainHealingOriginalAsync, signedDice: [5, 15, 4, 12],
            playerCurrent: 8m, oppositionCurrent: 8m, spiritFocusTier: 2, oppositionMaximum: 8m);
        await CompleteSpiritualStrainC2Async(context, maneuver: true, initialActionPoints: 8m);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var first = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(first.Issues);
        var before = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(3, before.Severity.Rank);
        var oldRoots = before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray();
        Assert.NotEmpty(oldRoots);
        var originalHistory = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(originalHistory.IsValid, string.Join("\n", originalHistory.Issues));
        var create = Assert.Single(originalHistory.State!.Transitions, row => row.Kind == "create");
        var originalReceipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.DeepClone();
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.AsObject();
        var ordinal = conflict["exchangeLog"]!.AsArray().Count + 1;
        var selected = new SpiritualSelfTreatmentSelection(before.WoundId, WoundIdentityState.ComputeSemanticFingerprint(before))
        { Combat = new(conflict["conflictId"]!.GetValue<string>(), SpiritualCombatHealingIntent.ConflictFingerprint(conflict), ordinal) };
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        ProgressionControl control;
        JsonObject projection;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerCombatTurnAsync(context.FileSystem, lease, scheduler,
                "session_resource_materialization", "request_resource_materialization", 43, selected);
            Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
            control = Assert.IsType<ProgressionControl>(staged.Control);
            projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
        }
        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);
        var intent = (await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath))!.AsObject();
        var request = Assert.Single(projection["possibleDestinationGraphs"]!.AsArray(), row => row!["severityRank"]!.GetValue<int>() == 1)!;
        var authored = AuthorStrainHealingGraph(before, request["stageRef"]!.GetValue<string>());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [19, 5, 12, 8], progressionControl: control, spiritualHealing: projection);
        var snapshotToken = (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!["manifestPayloadHash"]!.GetValue<string>();
        var worldBefore = await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        var scheduleBefore = await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        await AppendStrainHealingExchangeAsync(context, ordinal);
        await context.WriteExactJsonAsync("output/interface_updates.json", new JsonObject
        { ["timestamp"] = DateTime.UtcNow.ToString("O"), ["spiritualHealingGraphs"] = new JsonArray(authored) }.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
        { ["response"] = "Душа лечит прежнюю духовную трещину, ослабляя её с III до I степени, пока Хранитель сохраняет защиту.",
            ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var next = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(next.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(next.Step?.Interval);
            var pair = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(pair.Issues);
            Assert.Equal("committed", pair.Disposition);
            capture.Dispose();
            var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            using var owner = opened.Session;
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("completed_unpublished", opened.Disposition);
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(before),
                WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context, lease)));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var healed = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(healed.Issues);
        var healedPlan = Assert.IsType<AcceptedMechanicsPlan>(healed.MechanicsPlan);
        Assert.Equal(0m, ResolvePlannedActionPoints(healedPlan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(1m, ResolvePlannedActionPoints(healedPlan, ResourceOwnerKind.AfterlifeConflictSide).Current);
        foreach (var pair in healedPlan.WoundCarrierAfterImages.Concat(healedPlan.EffectCarrierAfterImages)
                     .Concat(healedPlan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        Assert.True(JsonNode.DeepEquals(healedPlan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(healedPlan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(healed.SpiritualWoundOutput);
        await output.RequireCurrentAsync(context.FileSystem);
        var after = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(1, after.Severity.Rank);
        Assert.Equal(before.WoundId, after.WoundId);
        Assert.Equal("active", after.Lifecycle);
        Assert.DoesNotContain(after.Consequences.OwnedEffectSources.RootBindings, row => oldRoots.Contains(row.EffectId));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        var player = Assert.Single(profiles["profiles"]!.AsArray(), row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.DoesNotContain(player["activeEffects"]!.AsArray(), row => oldRoots.Contains(row!["effectId"]!.GetValue<string>()));
        Assert.DoesNotContain(after.Consequences.OwnedEffectSources.Definitions,
            definition => definition.GetProperty("components").EnumerateArray().Any(component =>
                component.GetProperty("profile").GetString() == "spiritual_strain_burden"));
        var identity = EffectIdentityState.Parse((await context.ReadJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath))!.ToJsonString(), EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.True(identity.IsValid, string.Join("\n", identity.Issues));
        Assert.All(oldRoots, id => Assert.Equal("expired", Assert.Single(identity.State!.Entries, row => row.EffectId == id).State));
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        var historyState = Assert.IsType<WoundHistoryState>(history.State);
        Assert.Equal(create.AfterFingerprint, Assert.Single(historyState.Transitions, row => row.TransitionId == create.TransitionId).AfterFingerprint);
        Assert.DoesNotContain(historyState.Transitions, row => row.Terminal);
        var result = Assert.Single(historyState.Transitions.Select(row => row.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>());
        Assert.Equal(3, result.Stages[0].Before.Severity.Rank);
        Assert.Equal(1, result.Stages[^1].After.Severity.Rank);
        Assert.Equal(2m, result.Spend.AppliedAmount);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        Assert.True(JsonNode.DeepEquals(originalReceipt!["sources"], receipt["sources"]));
        Assert.True(JsonNode.DeepEquals(originalReceipt["decisions"], receipt["decisions"]));
        var parsedReceipt = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsedReceipt.IsValid, string.Join("\n", parsedReceipt.Issues));
        Assert.True(parsedReceipt.State!.HasExactStrainHistory(historyState));
        Assert.Equal(worldBefore, await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
        Assert.Equal(scheduleBefore, await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 43,
                path => context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 44, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }

    private static async Task SeedStrainHealingOriginalAsync(ResourceMaterializationTestContext context)
    {
        await SeedOriginalIntakeBaselinesAsync(context);
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        var player = Assert.Single(profiles["profiles"]!.AsArray(), row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
        player["standardArts"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = 3;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        var soul = (await context.ReadJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath))!;
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = 3;
        await context.WriteExactJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath, soul.ToJsonString());
        await context.WriteExactBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath,
            await File.ReadAllBytesAsync(Path.Combine(TestRepoPaths.BaseSessionRoot, EffectAcceptedTurnInputComposer.WorldTimePath)));
        await new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance).EnsureInitializedAsync();
    }

    private static JsonObject AuthorStrainHealingGraph(WoundMaterializationEnvelope wound, string stageRef)
    {
        var definition = JsonNode.Parse(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions).GetRawText())!.AsObject();
        var component = Assert.Single(definition["components"]!.AsArray(), row => row!["profile"]!.GetValue<string>() == "spiritual_action_cost_burden")!.DeepClone();
        component["payload"]!["magnitude"] = 1;
        definition["components"] = new JsonArray(component);
        var trigger = Assert.Single(definition["triggers"]!.AsArray())!.DeepClone();
        trigger["componentIds"] = new JsonArray(component["componentId"]!.DeepClone());
        definition["triggers"] = new JsonArray(trigger);
        definition["links"] = new JsonArray();
        return new JsonObject
        {
            ["stageRef"] = stageRef,
            ["consequenceDefinitions"] = new JsonArray(new JsonObject
            {
                ["definitionRef"] = definition["definitionKey"]!.DeepClone(), ["definition"] = definition,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                    ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = "spiritual_action_cost_burden",
                        ["readableSummary"] = "Ослабленная духовная трещина немного затрудняет защиту." })
                }
            })
        };
    }

    private static async Task AppendStrainHealingExchangeAsync(ResourceMaterializationTestContext context, int ordinal)
    {
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var active = root["activeConflict"]!;
        var log = active["exchangeLog"]!.AsArray();
        Assert.Equal(ordinal - 1, log.Count);
        var exchange = log[0]!.DeepClone();
        exchange["exchangeId"] = "exchange_strain_healing_43";
        exchange["turnNumber"] = 43;
        exchange["exchangeAtTurn"] = 43;
        exchange["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["outcome"] = "success";
        exchange["summary"] = "Душа лечит свою прежнюю трещину при встречной защите Хранителя.";
        exchange["before"] = log[^1]!["after"]!.DeepClone();
        exchange["after"] = exchange["before"]!.DeepClone();
        exchange["incomingAction"] = new JsonObject { ["operationType"] = "guard", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["summary"] = "Хранитель сохраняет защиту." };
        var matchup = exchange["matchupAudit"]!;
        matchup["playerOperation"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        matchup["primaryResolutionLane"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        matchup["oppositionOperation"] = "guard";
        matchup["riskProfile"] = "healing_timing";
        matchup["matchupRationale"] = "Хранитель не прерывает собственное лечение души встречной атакой.";
        exchange["actionCostAudit"]!["player"] = new JsonObject { ["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation,
            ["baseCost"] = 5, ["minCost"] = 2, ["artTier"] = 3, ["effectiveCost"] = 2, ["before"] = 2, ["after"] = 0 };
        exchange["actionCostAudit"]!["opposition"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
            ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 2, ["before"] = 3, ["after"] = 1 };
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 19;
        dice["diceUsed"]![0]!["selection"] = "selected";
        dice["diceUsed"]![1]!["value"] = 5;
        dice["modifierBreakdown"]!["player"] = new JsonArray();
        dice["modifierBreakdown"]!["opposition"] = new JsonArray(new JsonObject
        { ["modifierType"] = "conflict_position", ["source"] = "conflictPosition", ["position"] = "opposition_advantaged", ["value"] = 2 });
        dice["playerTotal"] = 19;
        dice["oppositionTotal"] = 7;
        dice["margin"] = 12;
        dice["outcomeBand"] = "decisive_player_success";
        log.Add(exchange);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }
}
