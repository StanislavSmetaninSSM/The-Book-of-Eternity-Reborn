using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires one bounded strain correction after a real rank-III C2 selection, without granting publication.</summary>
    [Fact]
    public async Task SpiritualStrainBurden_RealThirdDegreeSelectionRequiresExactlyOneOwnedStrainCorrection()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15, 4, 12]);
        await WriteSpiritualStrainBaselineDraftAsync(context);
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(ordinary.Issues);
        var sourceSession = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Equal(2, sourceSession.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, sourceSession.ClaimedDice);
        Assert.Equal(3, Assert.Single(sourceSession.Sources).Calculation.MaximumSeverityRank);

        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                     WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                     SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeEntityProfileState.StatePath })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var offerSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Equal(3, offerSession.Offer!.MaximumSeverityRank);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offerSession.Offer.OpportunityRef,
            "spiritual_strain_burden", "pressure", "player").GetRawText())!.AsObject();
        decision["proposal"]!["severity"] = "III";
        var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
        var definition = consequence["definition"]!;
        definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
            "spiritual_roll_hindrance", "component_002", "maneuver"));
        definition["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
            "spiritual_action_cost_burden", "component_003", "guard", JsonValue.Create(2)));
        definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_002");
        definition["triggers"]![0]!["componentIds"]!.AsArray().Add("component_003");
        consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "spiritual_roll_hindrance", ["readableSummary"] = "Маневрировать душе труднее." });
        consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "spiritual_action_cost_burden", ["readableSummary"] = "Защита требует больше духовных сил." });

        var selected = await offerSession.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
        using var selectedSession = selected.Session;
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.All(selected.Issues, issue => Assert.Contains(issue.Code,
            new[] { "spiritual_wound_profile_integration_required", "spiritual_wound_strain_correction_required" }));
        Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));

        var restored = await context.Validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("dependent_continuation", restored.Disposition);
        using var restoredSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
        var dependent = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
            await restoredSession.ReadDependentContextAsync(lease));
        Assert.Equal(new[] { "/activeConflict/exchangeLog/1/after/playerSideStrain" },
            dependent.DependentDraftFields.Select(field => field.JsonPointer));
        var policy = Assert.IsType<SpiritualWoundDependentDraftPolicy>(dependent.DraftPolicy);
        var baseline = await ReadProjectedSourceContinuationCandidateAsync(context);
        Assert.True(policy.Allows(baseline));
        var corrected = baseline.DeepClone().AsObject();
        corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["playerSideStrain"] = "broken";
        Assert.True(policy.Allows(corrected));
        corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["oppositionSideStrain"] = "strained";
        Assert.False(policy.Allows(corrected));
        corrected = baseline.DeepClone().AsObject();
        corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["playerSideStrain"] = "fractured";
        Assert.False(policy.Allows(corrected));
        corrected = baseline.DeepClone().AsObject();
        corrected["activeConflict"]!["playerSideStrain"] = "broken";
        Assert.False(policy.Allows(corrected));
    }
}
