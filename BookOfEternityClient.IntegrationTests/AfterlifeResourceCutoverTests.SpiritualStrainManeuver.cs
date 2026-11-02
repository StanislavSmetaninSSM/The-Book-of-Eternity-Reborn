using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A real III maneuver burden preserves successful maneuver against guard, without creating another wound source.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_PlayerManeuverSuccessKeepsPositionAndCompletesWithoutNewSource()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 12, 4]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["operationType"] = "maneuver";
        second["outcome"] = "success";
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["conflictPosition"] = "player_advantaged";
        second["incomingAction"]!["operationType"] = "guard";
        second["incomingAction"]!["summary"] = "Хранитель удерживает защиту, пока душа меняет позицию.";
        second["matchupAudit"]!["playerOperation"] = "maneuver";
        second["matchupAudit"]!["primaryResolutionLane"] = "maneuver";
        second["matchupAudit"]!["riskProfile"] = "position_play";
        second["matchupAudit"]!["oppositionOperation"] = "guard";
        second["matchupAudit"]!["matchupRationale"] = "Успешный манёвр обходит пассивную защиту и меняет только позицию.";
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
        second["diceAudit"]!["diceUsed"]![0]!["value"] = 12;
        second["diceAudit"]!["diceUsed"]![1]!["value"] = 4;
        second["diceAudit"]!["playerTotal"] = 12;
        second["diceAudit"]!["oppositionTotal"] = 4;
        second["diceAudit"]!["margin"] = 8;
        second["diceAudit"]!["outcomeBand"] = "decisive_player_success";
        active["conflictPosition"] = "player_advantaged";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души. Рана не мешает душе успешно обойти защиту Хранителя.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(ordinary.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Equal(2, source.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        var originalSource = Assert.Single(source.Sources);
        Assert.Equal("player", originalSource.AffectedSide);
        Assert.Equal(3, originalSource.Calculation.MaximumSeverityRank);
        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeEntityProfileState.StatePath })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Equal(3, first.Offer!.MaximumSeverityRank);
        var decision = CreateSpiritualStrainThirdDegreeDecision(first.Offer.OpportunityRef);
        var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
        consequence["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
        consequence["definition"]!["components"]![1]!["payload"]!["operation"] = "counter";
        consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачный манёвр сильнее напрягает душу.";
        consequence["root"]!["slots"]![1]!["readableSummary"] = "Контрприём душе даётся труднее.";
        var selected = await first.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
        using var selectedOwner = selected.Session;
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.All(selected.Issues, issue => Assert.Equal("spiritual_wound_profile_integration_required", issue.Code));
        Assert.True(selected.Disposition == "completed_unpublished", FormatC2SubmissionIssues(selected));
        AssertNoConflictFrameErrors(selected.Issues);
        selectedOwner?.Dispose();
        var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        using var reopenedOwner = reopened.Session;
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        var packet = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
        Assert.Single(packet["sources"]!.AsArray());
        var choice = Assert.Single(packet["stagedDecisions"]!.AsArray())!;
        Assert.Equal("materialize", choice["decision"]!.GetValue<string>());
        Assert.Equal(3, choice["selectedSeverityRank"]!.GetValue<int>());
        var image = Assert.Single(packet["candidateAfterImages"]!.AsArray(),
            row => row!["path"]!.GetValue<string>() == AfterlifeSpiritualConflictState.StatePath);
        var conflict = JsonNode.Parse(Convert.FromBase64String(image!["contentBase64"]!.GetValue<string>()))!["activeConflict"]!;
        var actual = conflict["exchangeLog"]![1]!;
        Assert.Equal("maneuver", actual["operationType"]!.GetValue<string>());
        Assert.Equal("success", actual["outcome"]!.GetValue<string>());
        Assert.Equal("overwhelmed", conflict["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", conflict["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", actual["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", actual["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", actual["before"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", actual["after"]!["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal("contested", actual["before"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal("player_advantaged", actual["after"]!["conflictPosition"]!.GetValue<string>());
        Assert.Equal("player_advantaged", conflict["conflictPosition"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(actual["before"]!["controlState"], actual["after"]!["controlState"]));
        Assert.Equal(0, actual["actionCostAudit"]!["player"]!["after"]!.GetValue<int>());
        Assert.Equal(1, actual["actionCostAudit"]!["opposition"]!["after"]!.GetValue<int>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
