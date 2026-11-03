using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Proves the next signed maneuver and real resource headroom when the existing wound targets another operation.</summary>
    [Fact]
    public async Task SpiritualExistingStrain_UnmatchedParentKeepsOrdinaryNextSignedManeuver()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15]);
        await PublishExistingStrainParentAsync(context, "pressure");
        await AppendExistingStrainManeuverAsync(context, corrected: false);
        var preserved = await CaptureExistingStrainCanonicalAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>Requires a real existing III wound to apply one strain step through cold C2, decline-only C3, common C4 and a later signed read.</summary>
    [Fact]
    public async Task SpiritualExistingStrain_RealParentManeuverPublishesOnceAndKeepsSignedHistory()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15]);
        await PublishExistingStrainParentAsync(context, "maneuver");
        var parentBefore = await ReadCombatHealingWoundAsync(context);
        var parentFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parentBefore);
        await AppendExistingStrainManeuverAsync(context, corrected: true);
        var preserved = await CaptureExistingStrainCanonicalAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            AssertNoConflictFrameErrors(advanced.Issues); // Genuine causal boundary before implementation.
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            var committed = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(committed.Issues);
            Assert.Equal("committed", committed.Disposition);
            capture.Dispose();
            var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            var opened = await cold.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(2, choice.Offer!.MaximumSeverityRank);
            var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                { opportunityRef = choice.Offer.OpportunityRef, decision = "none" }), null);
            using var completed = declined.Session;
            AssertNoConflictFrameErrors(declined.Issues);
            Assert.Equal("completed_unpublished", declined.Disposition);
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var expected = await PeekPlanAsync(context);
        Assert.Equal(3, expected.ResourceEvents.Count);
        var spent = expected.ResourceEvents.Where(row => row.EventKind == "resource_spent").ToArray();
        Assert.Equal(2, spent.Length);
        var player = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        var opposition = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal((3m, 0m, 3m), (player.Before, player.After, player.AppliedAmount));
        Assert.Equal((3m, 1m, 2m), (opposition.Before, opposition.After, opposition.AppliedAmount));
        Assert.Equal(player with { EventKind = "resource_depleted" },
            Assert.Single(expected.ResourceEvents, row => row.EventKind == "resource_depleted"));
        Assert.Equal(0m, ResolvePlannedActionPoints(expected, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(1m, ResolvePlannedActionPoints(expected, ResourceOwnerKind.AfterlifeConflictSide).Current);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(expected, published.MechanicsPlan);
        Assert.Equal(parentFingerprint, WoundIdentityState.ComputeSemanticFingerprint(await ReadCombatHealingWoundAsync(context)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(preserved[path], await context.FileSystem.ReadFileBytesAsync(path));
        foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                     .Concat(expected.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
        foreach (var binding in parentBefore.Consequences.OwnedEffectSources.RootBindings)
            Assert.Contains(mechanics.Components, row => row.EffectId == binding.EffectId);
        Assert.True(JsonNode.DeepEquals(expected.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!;
        var exchange = conflict["exchangeLog"]![1]!;
        Assert.Equal("overwhelmed", exchange["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", exchange["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", conflict["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", exchange["after"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("opposition_advantaged", exchange["after"]!["conflictPosition"]!.GetValue<string>());
        var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        Assert.Equal(2, receipts["sources"]!.AsArray().Count);
        Assert.Equal(2, receipts["decisions"]!.AsArray().Count);
        Assert.Equal("none", receipts["decisions"]![1]!["decision"]!.GetValue<string>());
        var witness = receipts["sources"]![1]!["witness"]!;
        var preimage = witness["strainPreimage"]!.AsObject();
        Assert.Equal(2, preimage["version"]!.GetValue<int>());
        Assert.False(preimage.ContainsKey("parentSourceId"));
        var actual = JsonNode.Parse(Convert.FromBase64String(witness["exchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(witness.AsObject(), actual);
        Assert.Equal(parentFingerprint, retained.ParentSemanticFingerprint);
        Assert.Equal("overwhelmed", retained.ReadOrdinaryAfter(actual)["playerSideStrain"]!.GetValue<string>());
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        var ledger = SpiritualWoundOpportunityReceiptState.Parse(receipts.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
        Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 44, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5], requestId: "request_existing_strain_history_44");
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }

    private static async Task PublishExistingStrainParentAsync(ResourceMaterializationTestContext context, string operation)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var first = active["exchangeLog"]![0]!;
        Assert.Single(active["exchangeLog"]!.AsArray());
        first["outcome"] = "setback";
        first["after"]!["playerSideStrain"] = "overwhelmed";
        first["after"]!["oppositionSideStrain"] = "clear";
        first["incomingAction"] = new JsonObject { ["operationType"] = "pressure", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["summary"] = "Хранитель оказывает духовное давление." };
        var dice = first["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 5;
        dice["diceUsed"]![1]!["value"] = 15;
        dice["playerTotal"] = 5;
        dice["oppositionTotal"] = 15;
        dice["margin"] = -10;
        dice["outcomeBand"] = "decisive_opposition_success";
        active["playerSideStrain"] = "overwhelmed";
        active["oppositionSideStrain"] = "clear";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(ordinary.Issues);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
            Assert.Single(source.CheckedExchanges);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
            Assert.Equal(3, Assert.Single(source.Sources).Calculation.MaximumSeverityRank);
            await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var firstChoice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(3, firstChoice.Offer!.MaximumSeverityRank);
            var decision = CreateSpiritualStrainThirdDegreeDecision(firstChoice.Offer.OpportunityRef);
            var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
            consequence["definition"]!["components"]![0]!["payload"]!["operation"] = operation;
            consequence["definition"]!["components"]![1]!["payload"]!["operation"] = "counter";
            consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачное духовное действие сильнее напрягает душу.";
            consequence["root"]!["slots"]![1]!["readableSummary"] = "Контрприём душе даётся труднее.";
            var selected = await firstChoice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            using var complete = selected.Session;
            AssertNoConflictFrameErrors(selected.Issues);
            Assert.Equal("completed_unpublished", selected.Disposition);
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(3m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Same(plan, result.MechanicsPlan);
        Assert.Equal(3, (await ReadCombatHealingWoundAsync(context)).Severity.Rank);
    }

    private static async Task AppendExistingStrainManeuverAsync(ResourceMaterializationTestContext context, bool corrected)
    {
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [4, 12], requestId: "request_existing_strain_maneuver_43");
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        var active = root["activeConflict"]!;
        var log = active["exchangeLog"]!.AsArray();
        Assert.Single(log);
        var exchange = log[0]!.DeepClone();
        exchange["exchangeId"] = "exchange_existing_strain_maneuver_43";
        exchange["turnNumber"] = 43;
        exchange["exchangeAtTurn"] = 43;
        exchange["operationType"] = "maneuver";
        exchange["outcome"] = "setback";
        exchange["summary"] = "Душа не смогла улучшить позицию, пока Хранитель сохраняет защиту.";
        exchange["before"] = log[0]!["after"]!.DeepClone();
        exchange["after"] = exchange["before"]!.DeepClone();
        exchange["after"]!["conflictPosition"] = "opposition_advantaged";
        exchange["after"]!["playerSideStrain"] = corrected ? "broken" : "overwhelmed";
        exchange["incomingAction"] = new JsonObject { ["operationType"] = "guard", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["summary"] = "Хранитель сохраняет защиту." };
        var matchup = exchange["matchupAudit"]!;
        matchup["playerOperation"] = "maneuver";
        matchup["primaryResolutionLane"] = "maneuver";
        matchup["oppositionOperation"] = "guard";
        matchup["riskProfile"] = "position_play";
        matchup["matchupRationale"] = "Неудачный манёвр теряет позицию перед защитой Хранителя.";
        exchange["actionCostAudit"]!["player"] = new JsonObject { ["operationType"] = "maneuver", ["baseCost"] = 3,
            ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 3, ["before"] = 3, ["after"] = 0 };
        exchange["actionCostAudit"]!["opposition"] = new JsonObject { ["operationType"] = "guard", ["baseCost"] = 2,
            ["minCost"] = 1, ["artTier"] = 0, ["effectiveCost"] = 2, ["before"] = 3, ["after"] = 1 };
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 0;
        dice["diceUsed"]![0]!["value"] = 4;
        dice["diceUsed"]![1]!["sourceIndex"] = 1;
        dice["diceUsed"]![1]!["value"] = 12;
        dice["playerTotal"] = 4;
        dice["oppositionTotal"] = 12;
        dice["margin"] = -8;
        dice["outcomeBand"] = "decisive_opposition_success";
        log.Add(exchange);
        active["playerSideStrain"] = corrected ? "broken" : "overwhelmed";
        active["conflictPosition"] = "opposition_advantaged";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = corrected
                ? "Душа не смогла улучшить позицию. Старая рана усилила напряжение души до надлома, пока Хранитель сохраняет защиту."
                : "Душа не смогла улучшить позицию, пока Хранитель сохраняет защиту.",
                ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
    }

    private static async Task<Dictionary<string, byte[]?>> CaptureExistingStrainCanonicalAsync(ResourceMaterializationTestContext context)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { AfterlifeEntityProfileState.StatePath, ValidationService.SpiritualWoundSourceSession.SoulPath, WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath, EffectCarrierCatalog.PlayerPath, EffectIdentityState.StatePath,
                     ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     SpiritualWoundOpportunityReceiptState.StatePath })
            result[path] = await context.FileSystem.ReadFileBytesAsync(path);
        return result;
    }
}
