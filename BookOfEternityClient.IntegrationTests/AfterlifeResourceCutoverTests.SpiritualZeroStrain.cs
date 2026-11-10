using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Publishes an actual zero-ceiling strain comparison with no offer/decision, then validates signed history.</summary>
    [Fact]
    public async Task SpiritualExistingStrain_ZeroCeilingPublishesComparisonOnlyAndKeepsSignedHistory()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15]);
        await PublishExistingStrainParentAsync(context, "maneuver");
        var parent = await ReadCombatHealingWoundAsync(context);
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(parent);
        await AppendExistingStrainManeuverAsync(context, corrected: true, oppositionDie: 5);
        var preserved = await CaptureExistingStrainCanonicalAsync(context);
        var receiptBefore = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        AcceptedMechanicsPlan expected;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
            Assert.Equal(0, source.Calculation.MaximumSeverityRank);
            Assert.True(source.Calculation.HasIncreasingStrain);
            Assert.Equal(7, source.Calculation.TraumaPressure);
            Assert.Equal(1, source.Calculation.Input.HarmfulMargin);
            Assert.Null(source.GuaranteedSeverityRank);
            Assert.NotNull(source.StrainPreimageJson);
            var noOffer = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(noOffer.Issues);
            Assert.Equal("no_offer", noOffer.Disposition);
            Assert.Null(noOffer.Pending); Assert.Null(noOffer.Checkpoint);
            var completed = await capture.CompleteOrdinaryReductionAsync(lease);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.True(completed.Success); Assert.NotNull(completed.Reduction);
            Assert.Throws<InvalidOperationException>(() => completed.Reduction!.WithOwnerCompanionAfterImage(
                SpiritualWoundOpportunityReceiptState.StatePath,
                new CanonicalBeforeImage(true, preserved[SpiritualWoundOpportunityReceiptState.StatePath]), receiptBefore));
            var prepared = await capture.PrepareC4PublicationAsync(lease);
            Assert.True(prepared.Authority is not null, string.Join("\n", prepared.Issues));
            AssertNoConflictFrameErrors(prepared.Issues);
            var authority = prepared.Authority!; expected = authority.Plan;
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = expected.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            Assert.Equal(3, receipt["schemaVersion"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(receiptBefore["sources"], receipt["sources"]));
            Assert.True(JsonNode.DeepEquals(receiptBefore["decisions"], receipt["decisions"]));
            var comparison = Assert.Single(receipt["strainComparisons"]!.AsArray())!;
            Assert.Equal(0, comparison["witness"]!["maximumSeverityRank"]!.GetValue<int>());
            Assert.Null(comparison["witness"]!["guaranteedSeverityRank"]);
            Assert.Empty(receipt["healingSources"]!.AsArray()); Assert.Empty(receipt["healingDecisions"]!.AsArray());
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), "native.zeroReceipt");
            Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues)); Assert.False(parsed.State!.HasCombatHealing);
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            foreach (var (path, bytes) in preserved)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        Assert.Same(expected, await PeekPlanAsync(context));
        Assert.Equal(3, expected.ResourceEvents.Count);
        var spent = expected.ResourceEvents.Where(row => row.EventKind == "resource_spent").ToArray();
        Assert.Equal(2, spent.Length);
        var player = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        var opposition = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal((3m, 0m, 3m), (player.Before, player.After, player.AppliedAmount));
        Assert.Equal((3m, 1m, 2m), (opposition.Before, opposition.After, opposition.AppliedAmount));
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues); Assert.Same(expected, published.MechanicsPlan);
        Assert.Equal(fingerprint, WoundIdentityState.ComputeSemanticFingerprint(await ReadCombatHealingWoundAsync(context)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(preserved[path], await context.FileSystem.ReadFileBytesAsync(path));
        foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                     .Concat(expected.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        var ledgerRoot = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        var ledger = SpiritualWoundOpportunityReceiptState.Parse(ledgerRoot.ToJsonString(), "native.zeroPublished");
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(ledger.IsValid); Assert.True(history.IsValid); Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 44, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5], requestId: "request_zero_strain_history_44");
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
    }
}
