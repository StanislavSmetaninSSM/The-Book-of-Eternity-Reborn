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
    /// <summary>Real active-conflict healing and later explicit re-trauma share one chronological publication and replay.</summary>
    [Fact]
    public async Task CombatHealingThenLaterRetraumaPublishesOneExactHistoryAndReplays()
    {
        ProgressionControl control = null!;
        JsonObject intent = null!;
        string snapshotToken = "";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                await SeedCombatHealingOriginalAsync(original, tier: 3, rank: 2);
                await original.WriteExactBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath,
                    await File.ReadAllBytesAsync(Path.Combine(TestRepoPaths.BaseSessionRoot,
                        EffectAcceptedTurnInputComposer.WorldTimePath)));
            },
            captureOriginalSnapshot: async original =>
            {
                var scheduler = new ProgressionScheduleService(original.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
                await scheduler.EnsureInitializedAsync();
                Assert.True(ProgressionScheduleService.TryBuildSpiritualCombatControl(
                    (await original.ReadJsonAsync(ProgressionScheduleService.SchedulePath))!.AsObject(),
                    await original.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath), out _));
                var wound = await ReadCombatHealingWoundAsync(original);
                var conflict = (await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.AsObject();
                var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
                    { Combat = new(conflict["conflictId"]!.GetValue<string>(), SpiritualCombatHealingIntent.ConflictFingerprint(conflict), 1) };
                JsonObject projection;
                await using (var lease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync())
                {
                    var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerCombatTurnAsync(original.FileSystem, lease,
                        scheduler, "session_resource_materialization", "request_resource_materialization", 42, selection);
                    Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
                    control = Assert.IsType<ProgressionControl>(staged.Control);
                    projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
                    intent = JsonNode.Parse((await original.FileSystem.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
                }
                await original.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                    preGeneratedDices1d20: [15, 5, 5, 15], progressionControl: control, spiritualHealing: projection);
                snapshotToken = (await original.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!["manifestPayloadHash"]!.GetValue<string>();
            });
        var before = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(2, before.Severity.Rank);
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!["profiles"]!.AsArray();
        foreach (var actorId in new[] { "player_soul", "guardian_frame" })
            Assert.Equal(2, profiles.Single(profile => profile!["actorId"]!.GetValue<string>() == actorId)!["standardArts"]!["pressure"]!.GetValue<int>());
        await WriteCombatHealingExchangeAsync(context, "next_boundary", playerDie: 15, oppositionDie: 5, tier: 3);
        await WriteLaterRetraumaExchangeAsync(context);
        var graphs = AuthorCombatHealingGraphs(before, new JsonArray(new JsonObject
        {
            ["stageRef"] = SpiritualHealingRequestProjection.GraphRef(intent["intentFingerprint"]!.GetValue<string>(), before.WoundId, 1),
            ["severityRank"] = 1,
            ["complications"] = SpiritualHealingRequestProjection.Complications(intent["intentFingerprint"]!.GetValue<string>(), before)
        }));
        var response = ((await context.ReadJsonAsync("output/interface_updates.json")) ?? new JsonObject()).AsObject();
        response["timestamp"] = DateTime.UtcNow.ToString("O"); response["spiritualHealingGraphs"] = graphs;
        await context.WriteExactJsonAsync("output/interface_updates.json", response.ToJsonString());
        const string scene = "Душа ослабила старую трещину исцелением, затем встречный натиск Хранителя вновь углубил её.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = DateTime.UtcNow.ToString("O") }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        var preserved = await CaptureExistingStrainCanonicalAsync(context);
        WoundMaterializationEnvelope healed;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            var committed = await capture.CommitC2FirstTransportAsync(lease, first.Step!.Interval!);
            AssertNoConflictFrameErrors(committed.Issues); Assert.Equal("committed", committed.Disposition);
            var firstPacket = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(committed.Pending!))!;
            var healingWitness = firstPacket["pending"]!["healingSources"]![0]!["witness"]!.AsObject();
            var healingSource = SpiritualCombatHealingPersistedResult.ReadSource(healingWitness);
            Assert.Equal(7, healingSource.Calculation.Margin);
            Assert.Equal(SpiritualHealingResultBand.ReduceOne, healingSource.Calculation.ResultBand);
            Assert.Equal(1, healingSource.Calculation.ResultingSeverityRank);
            Assert.False(healingSource.Calculation.HealsWound);
            var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues); Assert.Equal("offer", opened.Disposition);
            using var offered = opened.Session!;
            var packet = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
            Assert.True(JsonNode.DeepEquals(firstPacket["pending"]!["healingSources"]![0], packet["healingSources"]![0]));
            Assert.Equal(new[] { 0, 1, 2, 3 }, packet["diceClaims"]!.AsArray().Select(claim => claim!["sourceIndex"]!.GetValue<int>()));
            Assert.Equal(new[] { 0, 1 }, packet["healingSources"]![0]!["turnEvidence"]!["diceClaims"]!.AsArray()
                .Select(claim => claim!["sourceIndex"]!.GetValue<int>()));
            Assert.Equal(2, packet["continuationGeneration"]!.GetValue<int>());
            Assert.Equal(1, packet["cursor"]!["waveOrdinal"]!.GetValue<int>());
            var source = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
            Assert.Equal(new[] { 2, 3 }, source["turnEvidence"]!["diceClaims"]!.AsArray().Select(claim => claim!["sourceIndex"]!.GetValue<int>()));
            Assert.Equal(1, source["exchangeOrdinal"]!.GetValue<int>());
            Assert.Equal(before.WoundId, source["retraumaWoundId"]!.GetValue<string>());
            var resultIssues = new List<ValidationIssue>();
            var healing = SpiritualCombatHealingPersistedResult.Parse(
                JsonSerializer.SerializeToElement(packet["healingDecisions"]![0]!["result"]), "test.stagedHealing", resultIssues);
            AssertNoConflictFrameErrors(resultIssues); Assert.NotNull(healing);
            healed = healing!.Stages[^1].After;
            Assert.Equal("active", healed.Lifecycle); Assert.Equal(1, healed.Severity.Rank);
            var retained = WoundMaterializationContract.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
                Convert.FromBase64String(source["retraumaWoundJsonBase64"]!.GetValue<string>())), "test.laterOriginal");
            Assert.True(retained.IsValid);
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(healed), WoundMaterializationContract.SerializeCanonical(retained.Wound!));
            Assert.NotEqual(WoundIdentityState.ComputeSemanticFingerprint(before), WoundIdentityState.ComputeSemanticFingerprint(healed));
            var proposal = JsonNode.Parse(CreateSpiritualWorseningLifecycleDecision(offered.Offer!.OpportunityRef,
                worsening: true, targetKind: "player").GetRawText())!;
            var retainedRoot = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(healed))!;
            proposal["proposal"]!["classification"] = new JsonObject
            {
                ["woundType"] = retainedRoot["classification"]!["woundType"]!.DeepClone(),
                ["locationProfile"] = retainedRoot["classification"]!["locationProfile"]!.DeepClone()
            };
            var recovery = retainedRoot["recovery"]!.DeepClone(); recovery["currentStepProgress"] = 0; recovery["currentStepThreshold"] = 4;
            recovery["blockers"] = JsonSerializer.SerializeToNode(WoundTransitionReducer.DeriveWorseningBlockers(healed));
            proposal["proposal"]!["recovery"] = recovery;
            proposal["proposal"]!["display"]!["acquisitionNarration"] = scene;
            var selected = await offered.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(proposal), scene);
            AssertNoConflictFrameErrors(selected.Issues); Assert.Equal("completed_unpublished", selected.Disposition); selected.Session?.Dispose();
            foreach (var pair in preserved) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues); Assert.NotNull(published.MechanicsPlan);
        var spends = published.MechanicsPlan!.ResourceEvents.Where(row => row.EventKind == "resource_spent").ToArray();
        Assert.Equal(4, spends.Length);
        var playerSpends = spends.Where(row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor).ToArray();
        var oppositionSpends = spends.Where(row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide).ToArray();
        Assert.Equal(new[] { 2m, 1m }, playerSpends.Select(row => row.AppliedAmount));
        Assert.Equal(new[] { 2m, 1m }, oppositionSpends.Select(row => row.AppliedAmount));
        Assert.Equal(3m, playerSpends[^1].After); Assert.Equal(3m, oppositionSpends[^1].After);
        var final = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(before.WoundId, final.WoundId); Assert.Equal(2, final.Severity.Rank);
        Assert.Equal(before.Recovery.LastTickKey, final.Recovery.LastTickKey);
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid);
        Assert.Equal(new[] { "create", "treat", "worsen" }, history.State!.Transitions.Select(row => row.Kind));
        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(healed), history.State.Transitions[^1].BeforeFingerprint);
        var originalRoots = before.Consequences.OwnedEffectSources.RootBindings.Select(root => root.EffectId).ToHashSet(StringComparer.Ordinal);
        var healedRoots = healed.Consequences.OwnedEffectSources.RootBindings.Select(root => root.EffectId).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(healedRoots, originalRoots.Contains);
        Assert.DoesNotContain(final.Consequences.OwnedEffectSources.RootBindings, root => healedRoots.Contains(root.EffectId) || originalRoots.Contains(root.EffectId));
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!;
        Assert.Equal(1, decision["binding"]!["waveOrdinal"]!.GetValue<int>());
        Assert.Equal(2, decision["binding"]!["continuationGeneration"]!.GetValue<int>());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 42,
                path => context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
            await AssertHealingRetraumaReplayRefusalsAsync(context, lease, control, intent, snapshotToken, history.State!, receipt.AsObject());
        }
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5], requestId: "request_healing_retrauma_history43");
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }

    /// <summary>After-valid detached changes cannot substitute witness hashes, omit receipts or move the accepted original.</summary>
    private static async Task AssertHealingRetraumaReplayRefusalsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, ProgressionControl control, JsonObject intent,
        string snapshotToken, WoundHistoryState history, JsonObject receipt)
    {
        var later = Assert.Single(history.Transitions, row => row.Kind == "worsen");
        var digest = receipt["decisions"]![0]!["sourceFingerprint"]!.GetValue<string>();
        Assert.NotEqual(later.SourceFingerprint, digest);
        var forged = WoundHistoryState.CreateValidated(history.NextOrdinal, history.Transitions.Select(row =>
            row.TransitionId == later.TransitionId ? row with { SourceFingerprint = digest } : row));
        Assert.True(forged.IsValid);
        var forgedBytes = Encoding.UTF8.GetBytes(WoundHistoryState.SerializeCanonical(forged.State!));
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await ProgressionScheduleService.ReadPublishedSpiritualCombatCoreAsync(control,
                "session_resource_materialization", "request_resource_materialization", 42,
                path => path == WoundHistoryState.HistoryPath ? Task.FromResult<byte[]?>(forgedBytes)
                    : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
        Assert.Contains("receipted re-trauma chain", error.Message, StringComparison.Ordinal);
        foreach (var change in new[] { "missing_decision", "foreign_original" })
        {
            var detached = receipt.DeepClone().AsObject();
            if (change == "missing_decision")
            {
                detached["sources"]!.AsArray().Clear(); detached["decisions"]!.AsArray().Clear();
                detached["nextSourceOrdinal"] = 1; detached["nextDecisionOrdinal"] = 1;
            }
            else
            {
                var source = detached["sources"]![0]!["witness"]!.AsObject();
                source["turnEvidence"]!["requestId"] = "request_foreign_retrauma";
                var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
                source["sourceId"] = "spiritual_source_" + hash[7..]; source["sourceFingerprint"] = hash;
                var decision = detached["decisions"]![0]!.AsObject();
                decision["binding"]!["requestId"] = "request_foreign_retrauma";
                decision["sourceId"] = source["sourceId"]!.DeepClone(); decision["sourceFingerprint"] = hash;
                decision["sourceWitness"] = source.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
            }
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(detached.ToJsonString(), "test." + change);
            Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
            var bytes = Encoding.UTF8.GetBytes(detached.ToJsonString());
            error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await ProgressionScheduleService.ReadPublishedSpiritualCombatCoreAsync(control,
                    "session_resource_materialization", "request_resource_materialization", 42,
                    path => path == SpiritualWoundOpportunityReceiptState.StatePath ? Task.FromResult<byte[]?>(bytes)
                        : context.FileSystem.ReadFileBytesAsync(lease, path), snapshotToken, intent));
            Assert.Contains("exact receipted transition set", error.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Uses the existing decisive pressure rules and the next signed pair; no safe cycle or new combat rule.</summary>
    private static async Task WriteLaterRetraumaExchangeAsync(ResourceMaterializationTestContext context)
    {
        var candidate = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var active = candidate["activeConflict"]!;
        var exchange = active["exchangeLog"]![1]!.AsObject();
        exchange["operationType"] = "pressure"; exchange["outcome"] = "setback";
        exchange["after"]!["playerSideStrain"] = "fractured";
        exchange["incomingAction"] = new JsonObject { ["operationType"] = "pressure",
            ["spiritualWoundTarget"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_soul", ["retraumaWoundRef"] = CombatHealingWoundId } };
        var matchup = exchange["matchupAudit"]!;
        matchup["playerOperation"] = "pressure"; matchup["oppositionOperation"] = "pressure";
        matchup["primaryResolutionLane"] = "pressure"; matchup["riskProfile"] = "offensive_pressure";
        matchup["matchupRationale"] = "После исцеления натиск души уступает встречному натиску Хранителя, который вновь травмирует старую трещину.";
        exchange["actionCostAudit"]!["player"] = new JsonObject { ["operationType"] = "pressure", ["baseCost"] = 3,
            ["minCost"] = 1, ["artTier"] = 2, ["effectiveCost"] = 1, ["before"] = 4, ["after"] = 3 };
        exchange["actionCostAudit"]!["opposition"] = new JsonObject { ["operationType"] = "pressure", ["baseCost"] = 3,
            ["minCost"] = 1, ["artTier"] = 2, ["effectiveCost"] = 1, ["before"] = 4, ["after"] = 3 };
        var first = active["exchangeLog"]![0]!["diceAudit"]!.DeepClone();
        first["diceUsed"]![0]!["sourceIndex"] = 2; first["diceUsed"]![0]!["value"] = 5;
        first["diceUsed"]![1]!["sourceIndex"] = 3; first["diceUsed"]![1]!["value"] = 15;
        first["playerTotal"] = 5; first["oppositionTotal"] = 15; first["margin"] = -10;
        first["outcomeBand"] = "decisive_opposition_success"; exchange["diceAudit"] = first;
        active["playerSideStrain"] = "fractured";
        var shapeIssues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "test.laterRetrauma", shapeIssues);
        AssertNoConflictFrameErrors(shapeIssues);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }
}
