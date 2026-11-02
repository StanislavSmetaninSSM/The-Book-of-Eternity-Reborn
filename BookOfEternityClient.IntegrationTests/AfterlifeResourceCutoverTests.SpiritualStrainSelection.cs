using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
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

        restoredSession.Dispose();
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var issued = await validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("dependent_draft", issued.Disposition);
        var a = Assert.IsType<SpiritualWoundContinuationRequest>(issued.Request);
        var responseA = CreateSpiritualStagedEmptyResponse(a);
        corrected = baseline.DeepClone().AsObject();
        corrected["activeConflict"]!["exchangeLog"]![1]!["after"]!["playerSideStrain"] = "broken";
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        var evaluatedA = await validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, a, responseA);
        Assert.True(evaluatedA.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Advanced,
            evaluatedA.Disposition + ": " + string.Join("\n", evaluatedA.Issues));
        var committedA = await validator.CommitSpiritualWoundDependentProgressAsync(lease, a, responseA);
        Assert.Equal("committed", committedA.Disposition);
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(committedA.NextRequest);
        Assert.Equal("/activeConflict/playerSideStrain", Assert.Single(b.DependentDraftFields).JsonPointer);
        Assert.NotEqual(a.ContinuationId, b.ContinuationId);

        var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var recovered = await cold.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal(JsonSerializer.Serialize(b), JsonSerializer.Serialize(recovered.Request));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(recovered.AcceptedRequest));
        var responseB = CreateSpiritualStagedEmptyResponse(b);
        var beforeRejected = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        var damagedA = corrected.DeepClone().AsObject();
        damagedA["activeConflict"]!["exchangeLog"]![1]!["after"]!["playerSideStrain"] = "overwhelmed";
        damagedA["activeConflict"]!["playerSideStrain"] = "broken";
        Assert.NotEmpty(await cold.ValidateSpiritualWoundContinuationDraftAsync(lease, b, responseB,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] =
                Encoding.UTF8.GetBytes(damagedA.ToJsonString()) }));
        Assert.Equal(beforeRejected, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        corrected["activeConflict"]!["playerSideStrain"] = "broken";
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        var evaluatedB = await cold.EvaluateSpiritualWoundContinuationDraftAsync(lease, b, responseB);
        Assert.True(evaluatedB.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Resolved,
            evaluatedB.Disposition + ": " + string.Join("\n", evaluatedB.Issues));
        var ready = await cold.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("dependent_continuation", ready.Disposition);
        using var readySession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(ready.Session);
        var next = await readySession.ResumeDependentContinuationAsync(lease);
        AssertNoConflictFrameErrors(next.Issues);
        Assert.Equal("offer", next.Disposition); // Extra strain is a real new harmful source, not silently discarded.
        using var nextSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(next.Session);
        Assert.Equal(2, nextSession.Offer!.MaximumSeverityRank); // Actual margin8 + broken destination6.
        var declined = await nextSession.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            { opportunityRef = nextSession.Offer.OpportunityRef, decision = "none" }), null);
        using var declinedSession = declined.Session;
        AssertNoConflictFrameErrors(declined.Issues);
        Assert.Equal("completed_unpublished", declined.Disposition);
        declinedSession?.Dispose();
        var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        using var reopenedSession = reopened.Session;
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        var packet = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
        Assert.Equal(2, packet["sources"]!.AsArray().Count);
        var choices = packet["stagedDecisions"]!.AsArray();
        Assert.Equal(2, choices.Count);
        Assert.Equal("materialize", choices[0]!["decision"]!.GetValue<string>());
        Assert.Equal(3, choices[0]!["selectedSeverityRank"]!.GetValue<int>());
        Assert.Equal("none", choices[1]!["decision"]!.GetValue<string>());
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
