using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Reuses the proved complete ordinary maneuver fixture; no post-acceptance setup or owner escapes.</summary>
    private static async Task ConfigureFailedManeuverStrainDraftAsync(ResourceMaterializationTestContext context)
    {
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["turnNumber"] = 42;
        second["operationType"] = "maneuver";
        second["incomingAction"]!["operationType"] = "guard";
        second["incomingAction"]!["summary"] = "Хранитель сохраняет защиту, пока душа неудачно меняет позицию.";
        second["matchupAudit"]!["playerOperation"] = "maneuver";
        second["matchupAudit"]!["primaryResolutionLane"] = "maneuver";
        second["matchupAudit"]!["riskProfile"] = "position_play";
        second["matchupAudit"]!["oppositionOperation"] = "guard";
        second["matchupAudit"]!["matchupRationale"] = "Неудачный манёвр уступает позицию без обычного изменения напряжения.";
        var playerCost = second["actionCostAudit"]!["player"]!;
        playerCost["operationType"] = "maneuver";
        playerCost["baseCost"] = 3;
        playerCost["minCost"] = 1;
        playerCost["artTier"] = 0;
        playerCost["effectiveCost"] = 3;
        playerCost["before"] = 3;
        playerCost["after"] = 0;
        var guardCost = second["actionCostAudit"]!["opposition"]!;
        guardCost["operationType"] = "guard";
        guardCost["baseCost"] = 2;
        guardCost["minCost"] = 1;
        guardCost["artTier"] = 0;
        guardCost["effectiveCost"] = 2;
        guardCost["before"] = 3;
        guardCost["after"] = 1;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
    }

    /// <summary>Requires real cold A/B, parent source/history joins, common C4 publication and a signed next-turn historical comparison.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_ColdManeuverPublishesAndKeepsSignedHistoricalValidation()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await CompleteSpiritualStrainC2Async(context, maneuver: true);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var expected = await PeekPlanAsync(context);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(expected, published.MechanicsPlan);
        var comparison = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation>(
            published.SpiritualConflictValidation);
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(comparison))
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Single(output.Notifications);
        await output.RequireCurrentAsync(context.FileSystem);
        foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                     .Concat(expected.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var exchange = root["activeConflict"]!["exchangeLog"]![1]!;
        Assert.Equal("maneuver", exchange["operationType"]!.GetValue<string>());
        Assert.Equal("overwhelmed", exchange["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", exchange["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", root["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(3, exchange["actionCostAudit"]!["player"]!["effectiveCost"]!.GetValue<int>());
        Assert.Equal(2, exchange["actionCostAudit"]!["opposition"]!["effectiveCost"]!.GetValue<int>());
        var receiptRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(parsed.State!.HasExactStrainHistory(history.State!));
        Assert.Equal(receiptRoot["sources"]![0]!["witness"]!["sourceId"]!.GetValue<string>(),
            receiptRoot["sources"]![1]!["witness"]!["strainPreimage"]!["parentSourceId"]!.GetValue<string>());
        Assert.Equal("materialize", receiptRoot["decisions"]![0]!["decision"]!.GetValue<string>());
        Assert.Equal("none", receiptRoot["decisions"]![1]!["decision"]!.GetValue<string>());
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        var summary = root.DeepClone().AsObject();
        summary["activeConflict"]!["exchangeLog"]![1]!["summary"] = "Поздняя сводка прежнего неудачного манёвра.";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, summary.ToJsonString());
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        var changed = summary.DeepClone().AsObject();
        changed["activeConflict"]!["exchangeLog"]![1]!["after"]!["oppositionSideStrain"] = "broken";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
        Assert.Contains(await ValidatePublishedConflictPhaseAsync(context), issue => issue.Severity == IssueSeverity.Error);
        changed = summary.DeepClone().AsObject();
        changed["activeConflict"]!["exchangeLog"]![1]!["turnNumber"] = 43;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
        Assert.Contains(await ValidatePublishedConflictPhaseAsync(context), issue => issue.Severity == IssueSeverity.Error);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(comparison))
            Assert.Contains(await ValidatePublishedConflictPhaseAsync(context), issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
    }
}
