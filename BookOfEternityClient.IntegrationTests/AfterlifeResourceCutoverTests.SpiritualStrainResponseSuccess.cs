using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Failure-only strain preserves successful counters and authentic control removal after complete ordinary-source admission.</summary>
    [Theory]
    [InlineData("counter", "success", null)]
    [InlineData("counter", "partial_success", null)]
    [InlineData("break_binding", "success", null)]
    [InlineData("break_binding", "partial_success", null)]
    [InlineData("incarnation_resistance", "success", null)]
    [InlineData("incarnation_resistance", "partial_success", null)]
    [InlineData("counter", "success", "payoff")]
    [InlineData("break_binding", "success", "control")]
    [InlineData("incarnation_resistance", "success", "kind")]
    [InlineData("incarnation_resistance", "success", "before")]
    public async Task SpiritualStrainBurden_PlayerSuccessfulResponseKeepsOwnedOrdinaryResult(
        string operation, string outcome, string? invalid)
    {
        var counter = operation == "counter";
        var playerDie = outcome == "partial_success" ? 8 : 12;
        var expectedControl = counter ? null : new JsonObject
        {
            ["level"] = "bound", ["controllerSide"] = "opposition", ["controlId"] = "strain_response_prior_control",
            ["sourceOperation"] = operation == "break_binding" || invalid == "kind" ? "binding" : "force_incarnation",
            ["restrictedOperations"] = new JsonArray("maneuver", "binding"),
            ["summary"] = "Душа уже находится под духовным контролем Хранителя."
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async fixture =>
            {
                await SeedOriginalIntakeBaselinesAsync(fixture);
                if (counter)
                {
                    var soul = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync("game_state/meta/soul_state.json"));
                    soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![operation] = 1;
                    await fixture.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
                    var profiles = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                    var player = profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
                    player["standardArts"]![operation] = 1;
                    await fixture.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
                }
                var original = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                original["activeConflict"]!["controlState"] = expectedControl?.DeepClone();
                await fixture.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
            }, signedDice: [5, 15, playerDie, 4]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var firstExchange = active["exchangeLog"]![0]!;
        firstExchange["before"]!["controlState"] = expectedControl?.DeepClone();
        firstExchange["after"]!["controlState"] = expectedControl?.DeepClone();
        var second = active["exchangeLog"]![1]!;
        second["operationType"] = operation;
        second["outcome"] = outcome;
        second["before"] = firstExchange["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["matchupAudit"]!["playerOperation"] = operation;
        second["matchupAudit"]!["primaryResolutionLane"] = operation;
        second["matchupAudit"]!["riskProfile"] = counter ? "risky_reversal" : "anti_control";
        second["matchupAudit"]!["matchupRationale"] = counter
            ? "Душа обращает встречное давление в позиционное преимущество."
            : "Душа снимает существующий духовный контроль, сохраняя защиту от встречного давления.";
        if (counter)
        {
            if (invalid == "payoff") second["after"]!["playerSideStrain"] = "fractured";
            else second["after"]!["conflictPosition"] = "player_advantaged";
        }
        else if (invalid == "control")
            second["after"]!["conflictPosition"] = "player_advantaged";
        else
            second["after"]!["controlState"] = null;
        if (invalid == "before") second["before"]!["controlState"]!["controlId"] = "foreign_control";
        var cost = second["actionCostAudit"]!["player"]!;
        cost["operationType"] = operation;
        cost["baseCost"] = counter ? 4 : 3;
        cost["minCost"] = counter ? 2 : 1;
        cost["artTier"] = counter ? 1 : 0;
        cost["effectiveCost"] = 3;
        cost["after"] = 0;
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = playerDie;
        dice["diceUsed"]![1]!["value"] = 4;
        dice["playerTotal"] = playerDie;
        dice["oppositionTotal"] = 4;
        dice["margin"] = playerDie - 4;
        dice["outcomeBand"] = outcome == "partial_success" ? "player_success" : "decisive_player_success";
        foreach (var field in new[] { "playerSideStrain", "oppositionSideStrain", "conflictPosition", "controlState" })
            active[field] = second["after"]![field]?.DeepClone();
        var ordinarySecond = second.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души. Следующий ответ души успешен.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeEntityProfileState.StatePath,
                     "game_state/meta/soul_state.json" })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (invalid != null)
        {
            var expected = invalid switch
            {
                "payoff" => "afterlife_conflict_counter_missing_payoff",
                "control" => "afterlife_conflict_break_binding_missing_control_delta",
                "kind" => "afterlife_conflict_incarnation_resistance_without_force",
                _ => "afterlife_conflict_control_snapshot_mismatch"
            };
            Assert.Contains(ordinary.Issues, issue => issue.Code == expected);
            if (invalid == "kind")
                Assert.Contains(ordinary.Issues, issue => issue.Code == "afterlife_conflict_incarnation_resistance_clears_non_force_control");
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        AssertNoConflictFrameErrors(ordinary.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Empty(source.PendingRequirements);
        Assert.Equal(2, source.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        var signedOriginal = JsonNode.Parse(source.ReadOriginal(AfterlifeSpiritualConflictState.StatePath)!)!["activeConflict"]!;
        Assert.True(JsonNode.DeepEquals(expectedControl, signedOriginal["controlState"]));
        var signedSoul = JsonNode.Parse(source.ReadOriginal("game_state/meta/soul_state.json")!)!;
        Assert.Equal(counter ? 1 : 0, signedSoul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![operation]!.GetValue<int>());
        var signedPlayer = JsonNode.Parse(source.ReadOriginal(AfterlifeEntityProfileState.StatePath)!)!["profiles"]!.AsArray()
            .Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(counter ? 1 : 0, signedPlayer["standardArts"]![operation]!.GetValue<int>());
        var checkedFirst = JsonNode.Parse(source.CheckedExchanges[0])!;
        Assert.True(JsonNode.DeepEquals(expectedControl, checkedFirst["before"]!["controlState"]));
        Assert.True(JsonNode.DeepEquals(expectedControl, checkedFirst["after"]!["controlState"]));
        Assert.True(JsonNode.DeepEquals(checkedFirst["after"], ordinarySecond["before"]));
        Assert.True(JsonNode.DeepEquals(ordinarySecond, JsonNode.Parse(source.CheckedExchanges[1])));
        var acquisition = Assert.Single(source.Sources);
        Assert.True(source.Owns(acquisition));
        Assert.Equal("player", acquisition.AffectedSide);
        Assert.Equal(3, acquisition.Calculation.MaximumSeverityRank);
        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Equal(3, first.Offer!.MaximumSeverityRank);
        var decision = CreateSpiritualStrainThirdDegreeDecision(first.Offer.OpportunityRef);
        var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
        consequence["definition"]!["components"]![0]!["payload"]!["operation"] = operation;
        consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачный выбранный ответ сильнее напрягает душу.";
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
        Assert.True(JsonNode.DeepEquals(ordinarySecond, conflict["exchangeLog"]![1]));
        Assert.Equal("overwhelmed", conflict["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", conflict["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal(counter ? "player_advantaged" : "contested", conflict["conflictPosition"]!.GetValue<string>());
        Assert.Null(conflict["controlState"]);
        Assert.Equal(0, ordinarySecond["actionCostAudit"]!["player"]!["after"]!.GetValue<int>());
        Assert.Equal(0, ordinarySecond["actionCostAudit"]!["opposition"]!["after"]!.GetValue<int>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
