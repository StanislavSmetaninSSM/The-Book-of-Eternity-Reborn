using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A real III pressure burden must not add strain to the player's ordinarily successful pressure.</summary>
    [Fact]
    public Task SpiritualStrainBurden_PlayerPressureSuccessKeepsOrdinaryResultAndNextSource() =>
        VerifyPressureStrainSuccessAsync("success", invalidOrdinaryImpact: false);

    /// <summary>A partially successful pressure is successful and must not receive failure-only wound strain.</summary>
    [Fact]
    public Task SpiritualStrainBurden_PlayerPressurePartialSuccessKeepsOrdinaryResult() =>
        VerifyPressureStrainSuccessAsync("partial_success", invalidOrdinaryImpact: false);

    /// <summary>The failure-only wound exemption cannot conceal an invalid ordinary pressure result.</summary>
    [Fact]
    public Task SpiritualStrainBurden_PlayerPressurePartialSuccessRejectsInvalidOrdinaryImpact() =>
        VerifyPressureStrainSuccessAsync("partial_success", invalidOrdinaryImpact: true);

    private static async Task VerifyPressureStrainSuccessAsync(string outcome, bool invalidOrdinaryImpact)
    {
        var playerDie = outcome == "partial_success" ? 8 : 12;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, playerDie, 4]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["outcome"] = outcome;
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["oppositionSideStrain"] = "strained";
        second["diceAudit"]!["diceUsed"]![0]!["value"] = playerDie;
        second["diceAudit"]!["diceUsed"]![1]!["value"] = 4;
        second["diceAudit"]!["playerTotal"] = playerDie;
        second["diceAudit"]!["oppositionTotal"] = 4;
        second["diceAudit"]!["margin"] = playerDie - 4;
        second["diceAudit"]!["outcomeBand"] = outcome == "partial_success" ? "player_success" : "decisive_player_success";
        active["oppositionSideStrain"] = "strained";
        active["conflictPosition"] = "contested";
        if (invalidOrdinaryImpact)
        {
            second["after"]!["oppositionSideStrain"] = "clear";
            active["oppositionSideStrain"] = "clear";
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        if (invalidOrdinaryImpact)
        {
            using var rejectedOwner = ordinary.Session;
            Assert.Contains(ordinary.Issues, issue => issue.Code == "afterlife_conflict_pressure_missing_opposition_strain_delta");
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        AssertNoConflictFrameErrors(ordinary.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Equal(2, source.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);
        Assert.Equal(2, source.Sources.Count);
        Assert.Equal(3, source.Sources[0].Calculation.MaximumSeverityRank);
        Assert.Equal("player", source.Sources[0].AffectedSide);
        Assert.Equal(1, source.Sources[1].Calculation.MaximumSeverityRank);
        Assert.Equal("opposition", source.Sources[1].AffectedSide);
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
        var selected = await first.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(CreateSpiritualStrainThirdDegreeDecision(first.Offer.OpportunityRef)), scene);
        using var selectedOwner = selected.Session;
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.All(selected.Issues, issue => Assert.Equal("spiritual_wound_profile_integration_required", issue.Code));
        Assert.True(selected.Disposition == "offer", FormatC2SubmissionIssues(selected));
        AssertNoConflictFrameErrors(selected.Issues);
        var next = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(selectedOwner);
        Assert.NotEqual(first.Offer.OpportunityRef, next.Offer!.OpportunityRef);
        Assert.Equal(1, next.Offer.MaximumSeverityRank);
        var completed = await next.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            { opportunityRef = next.Offer.OpportunityRef, decision = "none" }), null);
        using var completedOwner = completed.Session;
        AssertNoConflictFrameErrors(completed.Issues);
        Assert.Equal("completed_unpublished", completed.Disposition);
        var packet = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
        Assert.Equal(2, packet["sources"]!.AsArray().Count);
        var choices = packet["stagedDecisions"]!.AsArray();
        Assert.Equal(2, choices.Count);
        Assert.Equal("materialize", choices[0]!["decision"]!.GetValue<string>());
        Assert.Equal(3, choices[0]!["selectedSeverityRank"]!.GetValue<int>());
        Assert.Equal("none", choices[1]!["decision"]!.GetValue<string>());
        var image = Assert.Single(packet["candidateAfterImages"]!.AsArray(),
            row => row!["path"]!.GetValue<string>() == AfterlifeSpiritualConflictState.StatePath);
        var conflict = JsonNode.Parse(Convert.FromBase64String(image!["contentBase64"]!.GetValue<string>()))!["activeConflict"]!;
        Assert.Equal("overwhelmed", conflict["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("strained", conflict["oppositionSideStrain"]!.GetValue<string>());
        Assert.Equal(outcome, conflict["exchangeLog"]![1]!["outcome"]!.GetValue<string>());
        Assert.Equal("overwhelmed", conflict["exchangeLog"]![1]!["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", conflict["exchangeLog"]![1]!["after"]!["playerSideStrain"]!.GetValue<string>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
