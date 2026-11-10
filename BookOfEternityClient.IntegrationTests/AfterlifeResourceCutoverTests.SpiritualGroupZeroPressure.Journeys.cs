using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Two separately owned retained groups publish exactly one source-only comparison and validate the actual signed next frame.</summary>
    /// <param name="contour">N2 low=0 or N3 before-II near-cap=1; no training/native or late retry claim.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public Task SpiritualGroupZero_ColdSourceOnlyPublishesEveryParentAndSignedNext(int contour)
    {
        if (contour is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(contour));
        var spec = GroupZeroCase("group_zero_normal_" + contour, SpiritualGroupZeroPressureData.Contour(contour));
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            var completed = await CompleteGroupZeroSourceOnlyC3Async(setup, registerPublication: true);
            await PublishGroupZeroAndReadNextAsync(setup, completed);
        }));
    }

    /// <summary>Uses the registered common C4 plan only after warm/cold source captures and their leases have been disposed.</summary>
    /// <param name="setup">Independent signed origin, exact ordinary input and complete parents.</param>
    /// <param name="completed">Actual cold source-only C3 result, including its one-comparison receipt.</param>
    private async Task PublishGroupZeroAndReadNextAsync(PressureGroupPublicationSetup setup, PressureGroupCompletedData completed)
    {
        var context = setup.Copy.Context;
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var started = Stopwatch.GetTimestamp();
        AssertNoConflictFrameErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        await AssertPressureGroupRegisteredPlanAsync(setup, completed, plan);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues); Assert.Same(plan, published.MechanicsPlan);
        await AssertGroupZeroPublishedAsync(setup, completed, plan);
        WritePressureOwnerTiming("group_zero_common_c4", setup.Spec.Name, started);

        // Signing the next request may write its own snapshot/control files, but cannot charge or alter these durable game results.
        var durablePaths = plan.WoundCarrierAfterImages.Keys.Concat(plan.EffectCarrierAfterImages.Keys)
            .Concat(plan.OwnerCompanionAfterImages.Keys).Concat(new[]
            {
                ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeSpiritualConflictState.StatePath
            }).Distinct(StringComparer.Ordinal).ToArray();
        var durable = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in durablePaths) durable[path] = await context.FileSystem.ReadFileBytesAsync(path);
        await ReadPressureGroupSignedNextAsync(setup, validator);
        foreach (var pair in durable) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        var actual = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!.AsObject();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            parsed.State!.MatchRetainedPressureHistory("chaos_sea", actual, 47));
        AssertGroupZeroSourceOnlyReceipt(setup, receipt);
        foreach (var path in GroupZeroPrivatePaths) Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
    }

    /// <summary>Checks every complete disk after-image, retained parent and immutable prefix with once-only resources and root activation.</summary>
    /// <param name="setup">Independent full signed receipt/History/identity and ordinary raw inputs.</param>
    /// <param name="completed">The one cold completed reduction, not a new evaluator or authority.</param>
    /// <param name="plan">The exact common plan returned by successful C4 publication.</param>
    private async Task AssertGroupZeroPublishedAsync(PressureGroupPublicationSetup setup, PressureGroupCompletedData completed,
        AcceptedMechanicsPlan plan)
    {
        var context = setup.Copy.Context;
        var actualRoot = JsonNode.Parse(completed.ActualRootJson)!.AsObject();
        AssertPressureExistingResourcePlan(plan, actualRoot); // Exactly two spends and two depletion events; both AP3->0 once.
        foreach (var row in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(row.Value, await context.ReadJsonAsync(row.Key)), row.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(actualRoot, await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))![AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var player = Assert.Single(profiles, row => row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Equal(setup.Spec.Parents, setup.Parents.Count); Assert.Equal(setup.Parents.Count, player["activeWounds"]!.AsArray().Count);
        var roots = new List<string>();
        foreach (var parentJson in setup.Parents)
        {
            var parent = JsonNode.Parse(parentJson)!;
            Assert.True(JsonNode.DeepEquals(parent, Assert.Single(player["activeWounds"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == parent["woundId"]!.GetValue<string>())));
            roots.Add(Assert.Single(parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())!["effectId"]!.GetValue<string>());
        }
        Assert.Equal(setup.Spec.Parents, roots.Distinct(StringComparer.Ordinal).Count());
        AssertPressureGuardRecoveryIdentityAppend(JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject(),
            (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject(), roots, completed.Resources, plan, turn: 46);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), receipt));
        var witness = AssertGroupZeroSourceOnlyReceipt(setup, receipt);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.WitnessJson), witness));
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!;
        Assert.Equal(before["sources"]!.AsArray().Count, receipt["sources"]!.AsArray().Count);
        Assert.Equal(before["decisions"]!.AsArray().Count, receipt["decisions"]!.AsArray().Count);
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(mechanics.IsAccepted, JsonSerializer.Serialize(mechanics.Issues));
        foreach (var root in roots) Assert.Contains(mechanics.Components, component => component.EffectId == root);
        foreach (var path in GroupZeroPrivatePaths)
        { Assert.Contains(path, plan.ConsumedPaths); Assert.Null(await context.FileSystem.ReadFileBytesAsync(path)); }
        await ExecuteGuardianLeaseStagesAsync(context, lease =>
        {
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            return Task.CompletedTask;
        });
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "group_zero_common_c4_published", setup.Spec.Name, parents = setup.Spec.Parents,
            comparisonRows = receipt["strainComparisons"]!.AsArray().Count,
            newInjurySourceRows = receipt["sources"]!.AsArray().Count - before["sources"]!.AsArray().Count,
            newInjuryDecisionRows = receipt["decisions"]!.AsArray().Count - before["decisions"]!.AsArray().Count,
            resourceSpentEvents = plan.ResourceEvents.Count(row => row.EventKind == "resource_spent"),
            resourceDepletedEvents = plan.ResourceEvents.Count(row => row.EventKind == "resource_depleted"),
            resourceReplayTransitions = completed.Resources.ReplayTransitions.Count,
            checkedOwnerTurnEndRootTransitions = roots.Count,
            actualRoot = actualRoot.ToJsonString(), receipt = receipt.ToJsonString()
        }));
    }

    /// <summary>The same three private surfaces must stay absent before and after signed-next validation.</summary>
    private static IReadOnlyList<string> GroupZeroPrivatePaths =>
        [SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath];
}
