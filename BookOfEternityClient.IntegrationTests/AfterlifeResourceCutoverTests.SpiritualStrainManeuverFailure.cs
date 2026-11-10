using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Proves the real failed-maneuver entry before any durable ordinary-strain comparison can be issued.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_PlayerManeuverSetbackRequestsOnlyOwnedStrainCorrection()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        await ConfigureFailedManeuverStrainDraftAsync(context);
        const string scene = "Чужое давление надломило волю души. Душа не смогла улучшить позицию.";
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
        var checkedSecond = JsonNode.Parse(source.CheckedExchanges[1])!;
        Assert.Equal("maneuver", checkedSecond["operationType"]!.GetValue<string>());
        Assert.Equal("setback", checkedSecond["outcome"]!.GetValue<string>());
        Assert.Equal(-8, checkedSecond["diceAudit"]!["margin"]!.GetValue<int>());
        Assert.Equal("overwhelmed", checkedSecond["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("overwhelmed", checkedSecond["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("opposition_advantaged", checkedSecond["after"]!["conflictPosition"]!.GetValue<string>());
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
        Assert.All(selected.Issues, issue => Assert.Contains(issue.Code,
            new[] { "spiritual_wound_profile_integration_required", "spiritual_wound_strain_correction_required" }));
        Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
        var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(selectedOwner);
        var dependent = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
            await owner.ReadDependentContextAsync(lease));
        Assert.Equal(new[] { "/activeConflict/exchangeLog/1/after/playerSideStrain" },
            dependent.DependentDraftFields.Select(field => field.JsonPointer));
        var policy = Assert.IsType<SpiritualWoundDependentDraftPolicy>(dependent.DraftPolicy);
        var baseline = await ReadProjectedSourceContinuationCandidateAsync(context);
        Assert.True(policy.Allows(baseline));
        var corrected = baseline.DeepClone().AsObject();
        corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["playerSideStrain"] = "broken";
        Assert.True(policy.Allows(corrected));
        var foreign = corrected.DeepClone().AsObject();
        foreign["activeConflict"]!["exchangeLog"]![1]!["after"]!["oppositionSideStrain"] = "strained";
        Assert.False(policy.Allows(foreign));
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
