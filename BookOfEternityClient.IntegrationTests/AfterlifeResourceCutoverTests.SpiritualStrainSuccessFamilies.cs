using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Failure-only strain preserves lawful successful control acquisition, without bypassing ordinary control rules.</summary>
    [Theory]
    [InlineData("binding", "success", null)]
    [InlineData("binding", "partial_success", null)]
    [InlineData("force_binding", "success", null)]
    [InlineData("force_binding", "partial_success", null)]
    [InlineData("binding", "success", "control")]
    [InlineData("force_binding", "success", "width")]
    public Task SpiritualStrainBurden_PlayerSuccessfulBindingKeepsOrdinaryResult(string operation, string outcome, string? invalid) =>
        VerifySuccessfulStrainFamilyAsync(operation, outcome, invalid);

    /// <summary>Failure-only strain preserves exact ordinary recovery gains, including partial success, and cannot hide bad arithmetic.</summary>
    [Theory]
    [InlineData("success", null)]
    [InlineData("partial_success", null)]
    [InlineData("partial_success", "gain")]
    public Task SpiritualStrainBurden_PlayerSuccessfulRecoveryKeepsOrdinaryResult(string outcome, string? invalid) =>
        VerifySuccessfulStrainFamilyAsync("recover_spiritual_power", outcome, invalid);

    /// <summary>Uses independent signed original state, then the real III acquisition and cold C2 reader for each admitted operation.</summary>
    private static async Task VerifySuccessfulStrainFamilyAsync(string operation, string outcome, string? invalid)
    {
        var recovery = operation == "recover_spiritual_power";
        var playerDie = outcome == "partial_success" ? 8 : 12;
        var playerAfter = recovery ? outcome == "partial_success" ? 5 : 6 : 1;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: recovery ? SeedOriginalIntakeBaselinesAsync : async fixture =>
            {
                await SeedOriginalIntakeBaselinesAsync(fixture);
                var soul = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync("game_state/meta/soul_state.json"));
                soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![operation] = 3;
                await fixture.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
                var profiles = Assert.IsType<JsonObject>(await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
                var player = profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
                player["standardArts"]![operation] = 3;
                await fixture.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            }, signedDice: [5, 15, playerDie, 4]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["operationType"] = operation;
        second["outcome"] = outcome;
        second["after"] = second["before"]!.DeepClone();
        second["matchupAudit"]!["playerOperation"] = operation;
        second["matchupAudit"]!["primaryResolutionLane"] = operation;
        second["matchupAudit"]!["riskProfile"] = recovery ? "recovery_timing" : "control_leverage";
        second["matchupAudit"]!["matchupRationale"] = recovery
            ? "Обе стороны собирают духовные силы без встречного давления."
            : "Душа использует готовую подготовку для успешных оков против давления Хранителя.";
        if (recovery)
        {
            second["incomingAction"]!["operationType"] = "recover_spiritual_power";
            second["incomingAction"]!["summary"] = "Хранитель также собирает духовные силы.";
            second["matchupAudit"]!["oppositionOperation"] = "recover_spiritual_power";
            second["actionCostAudit"] = new JsonObject
            {
                ["player"] = CostAudit(operation, 0, 3, invalid == "gain" ? 6 : playerAfter),
                ["opposition"] = CostAudit(operation, 0, 3, 6)
            };
        }
        else
        {
            second["bindingSetup"] = "ready";
            second["before"]!["controlState"] = new JsonObject { ["level"] = "none" };
            second["after"]!["controlState"] = new JsonObject
            {
                ["level"] = "hindered", ["controllerSide"] = "player", ["controlId"] = "strain_success_control",
                ["sourceOperation"] = operation,
                ["restrictedOperations"] = invalid == "width" ? new JsonArray("maneuver") : new JsonArray("maneuver", "binding"),
                ["summary"] = "Подготовленные оковы ограничивают действия Хранителя."
            };
            if (invalid == "control") second["after"]!["controlState"] = second["before"]!["controlState"]?.DeepClone();
            active["controlState"] = second["after"]!["controlState"]?.DeepClone();
            var cost = second["actionCostAudit"]!["player"]!;
            cost["operationType"] = operation;
            cost["baseCost"] = operation == "binding" ? 4 : 5;
            cost["minCost"] = 2;
            cost["artTier"] = 3;
            cost["effectiveCost"] = 2;
            cost["after"] = 1;
        }
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = playerDie;
        dice["diceUsed"]![1]!["value"] = 4;
        dice["playerTotal"] = playerDie;
        dice["oppositionTotal"] = 4;
        dice["margin"] = playerDie - 4;
        dice["outcomeBand"] = outcome == "partial_success" ? "player_success" : "decisive_player_success";
        active["conflictPosition"] = second["after"]!["conflictPosition"]!.DeepClone();
        var ordinarySecond = second.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души. Следующее действие души успешно.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeEntityProfileState.StatePath })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (invalid != null)
        {
            var expected = invalid switch
            {
                "control" => "afterlife_conflict_binding_missing_control_delta",
                "width" => "afterlife_conflict_force_binding_without_broad_control_payoff",
                _ => "afterlife_conflict_action_recovery_delta_mismatch"
            };
            Assert.Contains(ordinary.Issues, issue => issue.Code == expected);
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
        var acquisition = Assert.Single(source.Sources);
        Assert.True(source.Owns(acquisition));
        Assert.Equal("player", acquisition.AffectedSide);
        Assert.Equal(3, acquisition.Calculation.MaximumSeverityRank);
        Assert.True(JsonNode.DeepEquals(ordinarySecond, JsonNode.Parse(source.CheckedExchanges[1])));
        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Equal(3, first.Offer!.MaximumSeverityRank);
        var decision = CreateSpiritualStrainThirdDegreeDecision(first.Offer.OpportunityRef);
        var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
        consequence["definition"]!["components"]![0]!["payload"]!["operation"] = operation;
        consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачное выбранное действие сильнее напрягает душу.";
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
        Assert.Equal(playerAfter, ordinarySecond["actionCostAudit"]!["player"]!["after"]!.Deserialize<decimal>());
        Assert.Equal(recovery ? 6 : 0, ordinarySecond["actionCostAudit"]!["opposition"]!["after"]!.Deserialize<decimal>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
