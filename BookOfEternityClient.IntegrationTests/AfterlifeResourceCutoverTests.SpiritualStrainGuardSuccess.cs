using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Acquiring a failure-only guard burden must preserve a lawful successful guard, not conceal ordinary errors.</summary>
    [Theory]
    [InlineData("success", null)]
    [InlineData("partial_success", null)]
    [InlineData("success", "strain")]
    [InlineData("success", "tempo")]
    public async Task SpiritualStrainBurden_PlayerSuccessfulGuardKeepsOrdinaryResult(string outcome, string? invalid)
    {
        var playerDie = outcome == "partial_success" ? 8 : 12;
        var resultingStrain = outcome == "partial_success" ? "fractured" : "overwhelmed";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, playerDie, 4]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["operationType"] = "guard";
        second["outcome"] = outcome;
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["playerSideStrain"] = invalid == "strain" ? "broken" : resultingStrain;
        second["matchupAudit"]!["playerOperation"] = "guard";
        second["matchupAudit"]!["primaryResolutionLane"] = "guard";
        second["matchupAudit"]!["riskProfile"] = "safe_defense";
        second["matchupAudit"]!["matchupRationale"] = "Душа успешно удерживает защиту против давления Хранителя.";
        var cost = second["actionCostAudit"]!["player"]!;
        cost["operationType"] = "guard";
        cost["baseCost"] = 2;
        cost["effectiveCost"] = 2;
        cost["after"] = 1;
        second["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        second["diceAudit"]!["diceUsed"]![1]!["value"] = 4;
        second["diceAudit"]!["playerTotal"] = playerDie;
        second["diceAudit"]!["oppositionTotal"] = 4;
        second["diceAudit"]!["margin"] = playerDie - 4;
        second["diceAudit"]!["outcomeBand"] = outcome == "partial_success" ? "player_success" : "decisive_player_success";
        if (outcome == "success" && invalid != "tempo")
        {
            var tempo = new JsonObject
            {
                ["advantageId"] = "guard_success_window", ["status"] = "available", ["level"] = "advantage",
                ["ownerSide"] = "player", ["sourceOperation"] = "guard",
                ["sourceExchangeId"] = second["exchangeId"]!.DeepClone(),
                ["summary"] = "Успешная защита даёт душе темповое окно."
            };
            second["after"]!["tempoAdvantage"] = tempo;
            active["tempoAdvantage"] = tempo.DeepClone();
        }
        active["playerSideStrain"] = second["after"]!["playerSideStrain"]!.DeepClone();
        active["conflictPosition"] = second["after"]!["conflictPosition"]!.DeepClone();
        var ordinarySecond = second.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души. Душа успешно защищается от следующего давления Хранителя.";
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
            Assert.Contains(ordinary.Issues, issue => issue.Code == (invalid == "strain"
                ? "afterlife_conflict_guard_worsens_player_strain" : "afterlife_conflict_guard_missing_tempo_advantage"));
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
        consequence["definition"]!["components"]![0]!["payload"]!["operation"] = "guard";
        consequence["definition"]!["components"]![2]!["payload"]!["operation"] = "pressure";
        consequence["root"]!["slots"]![0]!["readableSummary"] = "Неудачная защита сильнее напрягает душу.";
        consequence["root"]!["slots"]![2]!["readableSummary"] = "Давление требует больше духовных сил.";
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
        Assert.Equal(resultingStrain, conflict["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", conflict["oppositionSideStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(ordinarySecond["after"]!["tempoAdvantage"], conflict["tempoAdvantage"]));
        Assert.Equal(1, ordinarySecond["actionCostAudit"]!["player"]!["after"]!.GetValue<int>());
        Assert.Equal(0, ordinarySecond["actionCostAudit"]!["opposition"]!["after"]!.GetValue<int>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
