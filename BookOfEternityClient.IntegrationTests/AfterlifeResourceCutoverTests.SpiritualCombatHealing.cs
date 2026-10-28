using System.Text;
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
    private const string CombatHealingWoundId = "wound_combat_healing";

    [Theory]
    [InlineData("healed")]
    [InlineData("no_improvement")]
    [InlineData("blocked_worsen")]
    [InlineData("next_boundary")]
    public async Task CombatHealingUsesAnActualPairAndDurableOutcomeWithoutASafeCycle(string mode)
    {
        var tier = mode == "no_improvement" ? 1 : 3;
        var playerDie = mode is "healed" or "next_boundary" ? 19 : mode == "no_improvement" ? 2 : 5;
        var oppositionDie = mode == "blocked_worsen" ? 15 : mode == "no_improvement" ? 2 : 5;
        ProgressionControl? control = null;
        JsonObject? intent = null;
        string? snapshotToken = null;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                await SeedCombatHealingOriginalAsync(original, tier, rank: mode == "next_boundary" ? 2 : 1);
            },
            captureOriginalSnapshot: async original =>
            {
                var scheduler = new ProgressionScheduleService(original.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
                await scheduler.EnsureInitializedAsync();
                var wound = await ReadCombatHealingWoundAsync(original);
                var conflict = (await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.AsObject();
                var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                { Combat = new(conflict["conflictId"]!.GetValue<string>(), SpiritualCombatHealingIntent.ConflictFingerprint(conflict), 1) };
                JsonObject projection;
                await using (var lease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync())
                {
                    var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerCombatTurnAsync(original.FileSystem, lease, scheduler,
                        "session_resource_materialization", "request_resource_materialization", 42, selection);
                    Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
                    control = Assert.IsType<ProgressionControl>(staged.Control);
                    projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
                    intent = JsonNode.Parse((await original.FileSystem.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                }
                await original.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                    preGeneratedDices1d20: [playerDie, oppositionDie, 12, 8], progressionControl: control, spiritualHealing: projection);
                snapshotToken = (await original.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!["manifestPayloadHash"]!.GetValue<string>();
            });
        var before = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(mode == "next_boundary" ? 2 : 1, before.Severity.Rank);
        Assert.Equal(1, before.Recovery.CurrentStepProgress);
        var originalTick = before.Recovery.LastTickKey;
        var schedule = await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        var world = await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        Assert.Equal(control!.CurrentChaosSeaTurnOrdinal, control.NextChaosSeaTurnOrdinal);
        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);

        await WriteCombatHealingExchangeAsync(context, mode, playerDie, oppositionDie, tier);
        await WriteOriginalIntakeDraftAsync(context);
        if (mode == "next_boundary")
        {
            var graphs = AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(before, new JsonArray(new JsonObject
            {
                ["stageRef"] = SpiritualHealingRequestProjection.GraphRef(intent!["intentFingerprint"]!.GetValue<string>(), before.WoundId, 1),
                ["severityRank"] = 1, ["complications"] = SpiritualHealingRequestProjection.Complications(intent["intentFingerprint"]!.GetValue<string>(), before)
            }));
            var response = (await context.ReadJsonAsync("output/interface_updates.json")) ?? new JsonObject();
            response["spiritualHealingGraphs"] = graphs;
            await context.WriteExactJsonAsync("output/interface_updates.json", response.ToJsonString());
        }
        var scene = mode == "blocked_worsen" ? "Встречный натиск углубил трещину духовной целостности души."
            : "Душа направила искусство исцеления на собственную трещину и завершила духовное противостояние.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
        { ["response"] = scene, ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var step = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(step.Step?.Interval);
            var first = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(first.Issues); Assert.Equal("committed", first.Disposition);
            var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(first.Pending!))!;
            Assert.Equal(2, packet["schemaVersion"]!.GetValue<int>());
            Assert.Single(packet["pending"]!["healingSources"]!.AsArray());
            Assert.Empty(packet["pending"]!["healingDecisions"]!.AsArray());
            Assert.Equal(0, packet["pending"]!["cursor"]!["nextHealingSourceOrdinal"]!.GetValue<int>());
            // The real first-pair producer itself requires WoundReadVersion0;
            // its candidate History must still be the unmodified seed.
            var candidateImages = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
            Assert.True(JsonNode.DeepEquals(await context.ReadJsonAsync(WoundHistoryState.HistoryPath),
                JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(candidateImages[WoundHistoryState.HistoryPath].Bytes!))));
            if (mode != "blocked_worsen") Assert.Empty(packet["pending"]!["sources"]!.AsArray());
            capture.Dispose();
            var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            var opened = await adapter.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            if (mode == "next_boundary")
            {
                Assert.Equal("dependent_continuation", opened.Disposition);
                using var selected = opened.Session!;
                var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!)!;
                Assert.Equal("combat_healing", checkpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!["family"]!.GetValue<string>());
                Assert.Null(checkpoint["checkpoint"]!["pendingSubmission"]!["command"]);
                var retainedDecision = checkpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!.DeepClone();
                var dependent = await selected.ReadDependentContextAsync(lease);
                Assert.NotNull(dependent);
                Assert.Contains(dependent!.DependentDraftFields, field => field.JsonPointer.EndsWith("/actionCostAudit/player/effectiveCost", StringComparison.Ordinal));
                var current = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
                current["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["effectiveCost"] = 2;
                current["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!["after"] = 2;
                // The exact allowed correction is raw input, not an accepted afterimage.
                await context.FileSystem.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath, current.ToJsonString());
                var resumed = await selected.ResumeDependentContinuationAsync(lease);
                AssertNoConflictFrameErrors(resumed.Issues); Assert.Equal("completed_unpublished", resumed.Disposition); resumed.Session?.Dispose();
                Assert.Equal(retainedDecision["decisionFingerprint"]!.GetValue<string>(),
                    JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!["healingDecisions"]![0]!["decisionFingerprint"]!.GetValue<string>());
            }
            else if (mode == "blocked_worsen")
            {
                Assert.Equal("offer", opened.Disposition);
                using var offered = opened.Session!;
                var proposal = JsonNode.Parse(CreateSpiritualWorseningLifecycleDecision(offered.Offer!.OpportunityRef, true).GetRawText())!;
                var retained = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(before))!;
                proposal["proposal"]!["classification"] = new JsonObject
                { ["woundType"] = retained["classification"]!["woundType"]!.DeepClone(), ["locationProfile"] = retained["classification"]!["locationProfile"]!.DeepClone() };
                var recovery = retained["recovery"]!.DeepClone(); recovery["currentStepProgress"] = 0; recovery["currentStepThreshold"] = 4;
                recovery["blockers"] = JsonSerializer.SerializeToNode(WoundTransitionReducer.DeriveWorseningBlockers(before));
                proposal["proposal"]!["recovery"] = recovery;
                proposal["proposal"]!["display"]!["acquisitionNarration"] = scene;
                var decided = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(proposal), scene);
                AssertNoConflictFrameErrors(decided.Issues); Assert.Equal("completed_unpublished", decided.Disposition); decided.Session?.Dispose();
            }
            else { Assert.Equal("completed_unpublished", opened.Disposition); opened.Session?.Dispose(); }
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(before), WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context, lease)));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues); Assert.NotNull(published.MechanicsPlan);
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath).State!;
        var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        if (mode == "next_boundary") Assert.NotNull((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
        else Assert.Null((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
        Assert.Single(receipts["healingSources"]!.AsArray()); Assert.Single(receipts["healingDecisions"]!.AsArray());
        Assert.Equal(0, receipts["healingDecisions"]![0]!["outcome"]!["waveOrdinal"]!.GetValue<int>());
        if (mode == "blocked_worsen")
        {
            var wound = await ReadCombatHealingWoundAsync(context);
            Assert.Equal(2, wound.Severity.Rank); Assert.Equal(0, wound.Recovery.CurrentStepProgress); Assert.Equal(originalTick, wound.Recovery.LastTickKey);
            Assert.Empty(history.Transitions.Where(t => t.TransitionResult is SpiritualCombatHealingPersistedResult));
            Assert.Equal(new[] { "create", "worsen" }, history.Transitions.Select(t => t.Kind));
            Assert.Equal(1, receipts["decisions"]![0]!["binding"]!["continuationGeneration"]!.GetValue<int>());
            Assert.Equal(0, receipts["decisions"]![0]!["binding"]!["waveOrdinal"]!.GetValue<int>());
            Assert.Null(receipts["healingDecisions"]![0]!["outcome"]!["result"]);
        }
        else
        {
            var result = Assert.Single(history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>());
            Assert.All(result.Stages, stage => Assert.Equal(originalTick, stage.After.Recovery.LastTickKey));
            if (mode is "healed" or "next_boundary") { Assert.Equal(new[] { "create", "treat", "heal" }, history.Transitions.Select(t => t.Kind)); Assert.Equal("healed", result.Stages[^1].After.Lifecycle); }
            else { Assert.Equal(new[] { "create", "treat" }, history.Transitions.Select(t => t.Kind)); Assert.Equal(1, (await ReadCombatHealingWoundAsync(context)).Recovery.CurrentStepProgress); }
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control, "session_resource_materialization", "request_resource_materialization", 42,
                path => context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
        var schedulerAfter = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        AssertNoConflictFrameErrors(await schedulerAfter.ValidateAcceptedTurnOutcomeAsync(control));
        await schedulerAfter.ApplyAcceptedTurnOutcomeAsync(control); await schedulerAfter.ApplyAcceptedTurnOutcomeAsync(control);
        Assert.Equal(schedule, await context.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
        Assert.Equal(world, await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
        var complete = SpiritualWoundOpportunityReceiptState.Parse(receipts.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(complete.IsValid);
        var omitted = receipts.DeepClone(); omitted["healingDecisions"]!.AsArray().Clear(); omitted["nextHealingDecisionOrdinal"] = 1;
        Assert.False(SpiritualWoundOpportunityReceiptState.Parse(omitted.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath).IsValid);
        var definitions = ResourceDefinitionCatalog.ParseCanonical((await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(), allowMissingPristine: false).Catalog!;
        var resourceHistory = ResourceHistoryState.ParseCanonical((await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!.ToJsonString(), definitions, allowMissingPristine: false).History!;
        complete.State!.RequireCombatHistoryResourceAgreement(history, resourceHistory);
        var familyOmitted = receipts.DeepClone().AsObject();
        foreach (var field in new[] { "healingSources", "healingDecisions", "nextHealingSourceOrdinal", "nextHealingDecisionOrdinal" }) familyOmitted.Remove(field);
        familyOmitted["schemaVersion"] = 1;
        var noFamily = SpiritualWoundOpportunityReceiptState.Parse(familyOmitted.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(noFamily.IsValid); // Both are current closed forms; this is a cross-root omission.
        if (mode != "blocked_worsen")
        {
            var error = Assert.Throws<FormatException>(() => noFamily.State!.RequireCombatHistoryResourceAgreement(history, resourceHistory));
            Assert.Contains("mandatory durable healing family", error.Message);
            var removedResult = (await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.DeepClone();
            Assert.Single(removedResult["transitions"]!.AsArray().OfType<JsonObject>(), row =>
                row["transitionResult"]?["kind"]?.GetValue<string>() == "spiritual_combat_healing").Remove("transitionResult");
            var withoutResult = WoundHistoryState.Parse(removedResult.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.True(withoutResult.IsValid); // No row/FP/identity is rewritten.
            var missing = Assert.Throws<FormatException>(() => complete.State.RequireCombatHistoryResourceAgreement(withoutResult.State!, resourceHistory));
            Assert.Contains("actual wound History result", missing.Message);
        }
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var refused = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 42,
                path => path == SpiritualWoundOpportunityReceiptState.StatePath
                    ? Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(familyOmitted.ToJsonString()))
                    : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
            Assert.Contains(mode == "blocked_worsen" ? "mandatory accepted healing outcome" : "mandatory durable healing family", refused.Message);
        }
        var witness = receipts["healingSources"]![0]!["witness"]!.DeepClone().AsObject();
        _ = SpiritualCombatHealingPersistedResult.ReadSource(witness); // Valid standalone baseline first.
        foreach (var marker in new[] { "discarded", "unknown_marker" })
        {
            var forged = witness.DeepClone().AsObject();
            forged["diceClaim"]![0]!["selection"] = marker;
            forged["exchange"]!["diceAudit"]!["diceUsed"]![0]!["selection"] = marker;
            RehashCombatWitness(forged);
            var refusal = Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingPersistedResult.ReadSource(forged));
            Assert.Contains("select exactly one die", refusal.Message);
        }
    }

    internal static async Task WriteCombatHealingExchangeAsync(ResourceMaterializationTestContext context, string mode,
        int playerDie, int oppositionDie, int tier)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var exchange = active["exchangeLog"]![0]!;
        exchange["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["outcome"] = mode == "blocked_worsen" ? "countered" : mode == "no_improvement" ? "partial_success" : "success";
        exchange["after"] = exchange["before"]!.DeepClone();
        active["oppositionSideStrain"] = "clear";
        exchange["matchupAudit"]!["playerOperation"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["matchupAudit"]!["primaryResolutionLane"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        exchange["matchupAudit"]!["riskProfile"] = "healing_timing";
        exchange["matchupAudit"]!["oppositionOperation"] = mode == "blocked_worsen" ? "pressure" : "guard";
        var cost = Math.Max(2, 5 - tier);
        exchange["actionCostAudit"]!["player"] = new JsonObject
        { ["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation, ["baseCost"] = 5, ["minCost"] = 2,
            ["artTier"] = tier, ["effectiveCost"] = cost, ["before"] = 6, ["after"] = 6 - cost };
        exchange["actionCostAudit"]!["opposition"] = new JsonObject
        { ["operationType"] = mode == "blocked_worsen" ? "pressure" : "guard", ["baseCost"] = mode == "blocked_worsen" ? 3 : 2,
            ["minCost"] = 1, ["artTier"] = mode == "blocked_worsen" ? 2 : 0,
            ["effectiveCost"] = mode == "blocked_worsen" ? 1 : 2, ["before"] = 6, ["after"] = mode == "blocked_worsen" ? 5 : 4 };
        exchange["diceAudit"]!["diceUsed"]![0]!["side"] = "PLAYERSIDE";
        exchange["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        exchange["diceAudit"]!["diceUsed"]![0]!["selection"] = "selected";
        exchange["diceAudit"]!["diceUsed"]![1]!["value"] = oppositionDie;
        exchange["diceAudit"]!["playerTotal"] = playerDie;
        exchange["diceAudit"]!["oppositionTotal"] = oppositionDie;
        exchange["diceAudit"]!["margin"] = playerDie - oppositionDie;
        exchange["diceAudit"]!["outcomeBand"] = mode == "blocked_worsen" ? "decisive_opposition_success"
            : mode == "no_improvement" ? "mixed_or_no_effect" : "decisive_player_success";
        if (mode == "blocked_worsen")
        {
            exchange["after"]!["playerSideStrain"] = "fractured"; active["playerSideStrain"] = "fractured";
            exchange["incomingAction"] = new JsonObject
            {
                ["operationType"] = "pressure", ["spiritualWoundTarget"] = new JsonObject
                { ["actorType"] = "player", ["actorId"] = "player_soul", ["retraumaWoundRef"] = CombatHealingWoundId }
            };
        }
        if (mode == "next_boundary")
        {
            var next = exchange.DeepClone().AsObject(); next["exchangeId"] = "exchange_after_combat_heal";
            next["operationType"] = "guard"; next["outcome"] = "no_effect"; next.Remove("diceAudit");
            next["before"] = exchange["after"]!.DeepClone(); next["after"] = next["before"]!.DeepClone();
            next["matchupAudit"]!["playerOperation"] = "guard"; next["matchupAudit"]!["primaryResolutionLane"] = "guard";
            next["matchupAudit"]!["riskProfile"] = "safe_defense";
            next["actionCostAudit"]!["player"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
                ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 3, ["before"] = 4, ["after"] = 1 };
            next["actionCostAudit"]!["opposition"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
                ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 2, ["before"] = 4, ["after"] = 2 };
            active["exchangeLog"]!.AsArray().Add(next);
        }
        else
        {
            ResolveTerminalFixture(candidate);
            candidate["recentConflicts"]![0]!["operationType"] = AfterlifeActionCostRules.SpiritualHealingOperation;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }

    private static async Task<WoundMaterializationEnvelope> ReadCombatHealingWoundAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease? lease = null)
    {
        var json = lease is null ? await context.FileSystem.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
            : await context.FileSystem.ReadFileAsync(lease, AfterlifeEntityProfileState.StatePath);
        var player = JsonNode.Parse(json!)!["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "player_soul")!;
        var parsed = WoundMaterializationContract.Parse(Assert.Single(player["activeWounds"]!.AsArray())!.ToJsonString(), "combat.fixture.wound");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues)); return parsed.Wound!;
    }

    internal static async Task SeedCombatHealingOriginalAsync(ResourceMaterializationTestContext context, int tier, int rank = 1)
    {
        await SeedSpiritualWorseningTierAuthorityAsync(context);
        var raw = WoundContractTestData.CreateSpiritualActiveWound(CombatHealingWoundId, "chaos_sea", "player_soul", "player_soul", AfterlifeEntityProfileState.StatePath);
        Assert.Contains(rank, new[] { 1, 2 });
        raw["severity"]!["rank"] = rank; raw["severity"]!["value"] = rank == 1 ? "I" : "II"; raw["severity"]!["maximumAtCreation"] = rank == 1 ? "I" : "II";
        raw["recovery"]!["currentStepThreshold"] = 2 * rank; raw["recovery"]!["currentStepProgress"] = 1;
        raw["recovery"]!["lastTickKey"] = "fixture_prior_safe_cycle";
        var consequences = raw["consequences"]!; consequences["slotBudget"] = rank; consequences["slotsUsed"] = rank;
        var definitions = consequences["ownedEffectSources"]!["definitions"]!.AsArray();
        if (rank == 1)
        {
            consequences["entries"]!.AsArray().RemoveAt(0); consequences["entries"]![0]!["slot"] = 1;
            definitions.RemoveAt(0); consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(0);
        }
        else
        {
            var old = definitions[0]!.AsObject(); var replacement = definitions[1]!.DeepClone().AsObject();
            replacement["definitionKey"] = old["definitionKey"]!.DeepClone(); replacement["stacking"] = old["stacking"]!.DeepClone();
            replacement["components"]![0]!["payload"]!["operation"] = "maneuver";
            definitions[0] = replacement;
            consequences["entries"]![0]!["profileKey"] = "spiritual_action_cost_burden";
        }
        definitions[^1]!["components"]![0]!["payload"]!["operation"] = "guard";
        var seed = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", raw);
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!;
        var player = profiles["profiles"]!.AsArray().Single(p => p!["actorId"]!.GetValue<string>() == "player_soul")!;
        player["standardArts"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = tier;
        player["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        var wound = Assert.Single(WoundCarrierCatalog.Build(seed.Carriers).Occurrences).Wound;
        var effects = new List<JsonObject>();
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            var definition = definitions.OfType<JsonObject>().Single(d => d["definitionKey"]!.GetValue<string>() == binding.DefinitionKey);
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect("spiritual_action_cost_burden", "player", "chaos_sea", CombatHealingWoundId);
            effect["effectId"] = binding.EffectId; effect["target"]!["targetId"] = "player_soul";
            effect["source"]!["definitionKey"] = binding.DefinitionKey;
            foreach (var part in new[] { "display", "components", "triggers", "removal", "links" }) effect[part] = definition[part]!.DeepClone();
            effect["display"]!["sourceLabel"] = wound.Display.Name;
            effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
            effect["chronology"]!["lastTransitionId"] = "seed_transition_" + binding.EffectId;
            effect["chronology"]!["createdEventRef"] = "seed_event_" + binding.EffectId;
            effects.Add(effect);
        }
        player["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = AfterlifeEntityProfileState.StatePath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        var soul = (await context.ReadJsonAsync("game_state/meta/soul_state.json"))!;
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId] = tier;
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles.AsObject(), soul.AsObject(), null);
        Assert.Empty(seed.History.ValidateAgreement(seed.Identities, WoundCarrierCatalog.Build(seed.Carriers with { AfterlifeProfiles = profiles.AsObject() })));
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(seed.Identities));
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(seed.History));
        await context.WriteExactJsonAsync(EffectIdentityState.StatePath, index.ToJsonString());
    }


    private static void RehashCombatWitness(JsonObject witness)
    {
        witness["exchangeFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness["exchange"]!) });
        witness.Remove("sourceFingerprint");
        witness["sourceFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.witness", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness) });
    }

}
