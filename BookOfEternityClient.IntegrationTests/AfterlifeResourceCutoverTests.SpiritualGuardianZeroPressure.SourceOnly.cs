using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires one genuine cold source-only v5 comparison after complete warm disposal, inspecting only an unpublished C4 plan.</summary>
    [Fact]
    public Task SpiritualGuardianZero_SourceOnlyC3RetainsComparison() =>
        WithGuardianZeroPressureSetupAsync("guardian_zero_source_only_c3", SpiritualGuardianZeroPressureTestData.Create(3),
            CompleteGuardianZeroSourceOnlyForInspectionAsync);

    /// <summary>Preserves the original rankIII unpublished inspection contract without registering or publishing.</summary>
    /// <param name="setup">Independent signed guardian originals and unchanged authored ordinary DATA.</param>
    private async Task CompleteGuardianZeroSourceOnlyForInspectionAsync(GuardianZeroPressureSetup setup)
    {
        Assert.Equal((3, 4), (setup.Data.Rank, setup.Data.Resilience));
        _ = await CompleteGuardianZeroSourceOnlyC3Async(setup);
    }

    /// <summary>Reuses complete warm/cold source-only proof; only an explicit normal caller may register after every unpublished assertion passes.</summary>
    /// <param name="setup">Closed rankIII/IV resilience4 signed originals and identical ordinary bytes.</param>
    /// <param name="register">Optional normal-only registration action under the current lease; no live capture or source-only issuance key is exposed.</param>
    /// <param name="observePrepared">Optional detached DATA observer after complete hash validation and before registration.</param>
    /// <param name="retainColdCapture">Optional local observation of the real cold capture solely to verify its later disposal.</param>
    /// <returns>Detached completed resource/effect/actual/source/receipt DATA, never a capture or lease.</returns>
    private async Task<PressureGroupCompletedData> CompleteGuardianZeroSourceOnlyC3Async(GuardianZeroPressureSetup setup,
        Func<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority, FileSystemManager.CanonicalWriteLease, Task>? register = null,
        Action<string>? observePrepared = null, Action<ValidationService.SpiritualOriginalTurnCapture>? retainColdCapture = null)
    {
        var context = setup.Context;
        Assert.True(setup.Data.Rank is 3 or 4); Assert.Equal(4, setup.Data.Resilience);
        PressureGroupCompletedData? completedData = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? registeredAuthority = null;
        var warmValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        ValidationService.SpiritualOriginalTurnCapture? warmCapture = null;
        FileSystemManager.CanonicalWriteLease? warmLease = null;
        string? warmExchange = null; string? warmPreimage = null;
        await WithGuardianZeroPressureCaptureAsync(setup, warmValidator, "warm_no_offer", async window =>
        {
            await AssertGuardianZeroPressureActualAndC1Async(window);
            var source = Assert.Single(Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease)).Sources);
            warmExchange = source.ExchangeJson; warmPreimage = source.StrainPreimageJson;
            warmCapture = window.Capture; warmLease = window.Lease;
            Assert.Equal(setup.RawOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(window.Lease, AfterlifeSpiritualConflictState.StatePath));
        });
        Assert.NotNull(warmCapture); Assert.NotNull(warmLease);
        Assert.False(warmCapture!.IsCurrentOwner); Assert.False(warmLease!.IsActive);
        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        Assert.NotSame(warmValidator, coldValidator);
        await WithGuardianZeroPressureCaptureAsync(setup, coldValidator, "cold_source_only", async window =>
        {
            Assert.NotSame(warmCapture, window.Capture); Assert.NotSame(warmLease, window.Lease);
            Assert.False(warmCapture.IsCurrentOwner); Assert.False(warmLease.IsActive);
            var interval = await AssertGuardianZeroPressureActualAndC1Async(window);
            var capture = window.Capture; var lease = window.Lease;
            var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
            Assert.Equal(warmExchange, source.ExchangeJson); Assert.Equal(warmPreimage, source.StrainPreimageJson);
            Assert.Equal(setup.Data.ActualCalculation, source.Calculation);
            var proof = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject();
            Assert.Equal(5, proof["version"]!.GetValue<int>());
            Assert.Equal((2L, 0), (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(),
                proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
            var completion = await MeasurePressureOwnerPhaseAsync("guardian_zero_source_only_completion", setup.Name,
                () => capture.CompleteOrdinaryReductionAsync(lease));
            RequireGuardianJourneyIssues(setup.Name, "guardian_zero_source_only_completion", completion.Issues);
            Assert.True(completion.Success);
            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completion.Reduction);
            Assert.True(reduction.RequiresSatisfactionReceipt);
            Assert.False(reduction.HasLiveDecisionReceipt); Assert.False(reduction.HasLiveWoundWork); Assert.Null(reduction.LiveWoundCompletion);
            Assert.Empty(reduction.Resources.ReplayTransitions); Assert.Equal(2, reduction.Resources.AppliedTransitions.Count);
            foreach (var kind in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var spend = Assert.Single(reduction.Resources.AppliedTransitions, row => row.Coordinate.OwnerKind == kind);
                var maximum = kind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
                var ownerId = kind == ResourceOwnerKind.AfterlifeActor ? "player_soul" : setup.OrdinaryRoot["activeConflict"]!
                    [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                Assert.Equal(new ResourceCoordinate("chaos_sea", kind, ownerId, "spiritual_action_points"), spend.Coordinate);
                Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
                Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                    (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                        spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
            }
            var actualRoot = reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
            Assert.True(JsonNode.DeepEquals(window.ActualRoot, actualRoot));
            Assert.NotNull(reduction.Resources.StateAfterImage); Assert.NotNull(reduction.Resources.HistoryAfterImage);
            var definitions = (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!;
            var projected = AfterlifeConflictActionPointProjectionService.Resolve(definitions,
                reduction.Resources.StateAfterImage.ToCanonicalJson(), actualRoot["activeConflict"]!.AsObject());
            RequireGuardianJourneyIssues(setup.Name, "guardian_zero_source_only_ap", projected.Issues);
            Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
            var journal = capture.ReadAllocationJournal(lease).ToJsonString(); var cursor = capture.ReadAllocationCursor(lease);
            var beforeRepeated = capture.ReadExistingStrainReuseCounts();
            var repeated = await capture.CompleteOrdinaryReductionAsync(lease);
            Assert.Empty(repeated.Issues); Assert.Same(completion, repeated);
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.True(capture.ReadExistingStrainReuseCounts().FreshInputChecks > beforeRepeated.FreshInputChecks);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "guardian_zero_source_only_preparation_prerequisites", setup.Name,
                warmCaptureCurrent = warmCapture.IsCurrentOwner, warmLeaseActive = warmLease.IsActive,
                freshValidatorCaptureLease = true, ordinary = setup.Data.OrdinaryCalculation, actual = setup.Data.ActualCalculation,
                rawOrdinaryBytesBase64 = Convert.ToBase64String(setup.RawOrdinaryBytes), source.ExchangeJson, source.StrainPreimageJson,
                priorReceiptJson = ReadGuardianAttributionJson(setup.OriginalReceiptBytes).ToJsonString(),
                originalHistoryJson = ReadGuardianAttributionJson(setup.OriginalHistoryBytes).ToJsonString(),
                actualRootJson = actualRoot.ToJsonString(), resourceState = reduction.Resources.StateAfterImage.ToCanonicalJson(),
                resourceHistory = reduction.Resources.HistoryAfterImage.ToCanonicalJson(), journal, cursor,
                reduction.RequiresSatisfactionReceipt, reduction.HasLiveDecisionReceipt, reduction.HasLiveWoundWork,
                counters = ReadGuardianZeroPressureCounts(capture)
            }));
            // This existing call internally performs genuine source-only C3; the returned plan remains unregistered and unwritten.
            var prepared = await MeasurePressureOwnerPhaseAsync("guardian_zero_source_only_unpublished_preparation", setup.Name,
                () => capture.PrepareC4PublicationAsync(lease));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "guardian_zero_source_only_unpublished_preparation_result", setup.Name,
                allPrecedingPrerequisitesAccepted = true, prepared.Issues, authorityReturned = prepared.Authority is not null,
                publicationRegistered = AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease),
                counters = ReadGuardianZeroPressureCounts(capture)
            }));
            RequireGuardianJourneyIssues(setup.Name, "guardian_zero_source_only_unpublished_preparation_desired_success", prepared.Issues);
            var authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_source_only_unpublished_receipt_desired_success",
                setup.Name, receiptJson = receipt.ToJsonString() }));
            AssertGuardianZeroSourceOnlyReceipt(setup, receipt, window.ActualRoot);
            Assert.True(JsonNode.DeepEquals(actualRoot, authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(setup.RawOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertExistingPressureInputInert(context, lease, window.Files, window.Generation);
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            var witness = Assert.Single(receipt["strainComparisons"]!.AsArray())!["witness"]!.AsObject();
            completedData = new(actualRoot.ToJsonString(), witness.ToJsonString(), receipt.ToJsonString(), reduction.Resources,
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(reduction.Effects!));
            if (register is not null)
            {
                var envelopeJson = WriteGuardianZeroPreparedEnvelope(setup, authority.Plan, reduction, interval, journal, cursor, source, witness, receipt);
                observePrepared?.Invoke(envelopeJson);
                await register(authority, lease);
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var retained));
                Assert.Same(authority, retained); registeredAuthority = authority;
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                AssertPressureZeroCaptureInert(context, lease, window.Files, window.Generation, registeredAuthority);
            }
            retainColdCapture?.Invoke(capture);
        }, expectedPublication: () => registeredAuthority);
        return Assert.IsType<PressureGroupCompletedData>(completedData);
    }

    /// <summary>Checks the complete unpublished schema3 result, immutable prefixes and exact guardian parent/History/log joins.</summary>
    /// <param name="setup">Exact original signed images, not a current draft substitute.</param>
    /// <param name="receipt">The real unregistered source-only plan's receipt candidate.</param>
    /// <param name="actualRoot">The complete actual conflict already independently asserted by the cold owner.</param>
    private static void AssertGuardianZeroSourceOnlyReceipt(GuardianZeroPressureSetup setup, JsonObject receipt, JsonObject actualRoot)
    {
        var before = ReadGuardianAttributionJson(setup.OriginalReceiptBytes);
        Assert.Equal(3, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var (field, node) in before)
            if (field is not ("schemaVersion" or "instances" or "nextInstanceOrdinal"))
                Assert.True(JsonNode.DeepEquals(node, receipt[field]), field);
        var oldInstances = before["instances"]!.AsArray(); var instances = receipt["instances"]!.AsArray();
        Assert.Equal(oldInstances.Count + 1, instances.Count);
        for (var index = 0; index < oldInstances.Count; index++) Assert.True(JsonNode.DeepEquals(oldInstances[index], instances[index]));
        Assert.Empty(receipt["healingSources"]!.AsArray()); Assert.Empty(receipt["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (receipt["nextHealingSourceOrdinal"]!.GetValue<int>(), receipt["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        var row = Assert.Single(receipt["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), receipt["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        var witness = row["witness"]!.AsObject();
        Assert.Equal(setup.Data.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var intrinsic));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(intrinsic);
        Assert.Equal(5, comparison.Version); Assert.Single(comparison.Parents); Assert.Null(comparison.ParentSourceId);
        Assert.Equal(setup.Data.OrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(setup.Data.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.ParentWound))));
        Assert.Equal((setup.Data.Retained.Effects.Single()["effectId"]!.GetValue<string>(), "component_001"), (comparison.EffectId, comparison.ComponentId));
        Assert.Equal(new CanonicalBeforeImage(true, setup.RawOrdinaryBytes).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(setup.Data.Actual, comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(setup.Data.Ordinary["after"], comparison.ReadOrdinaryAfter(comparison.ReadActualExchange())));
        Assert.Equal(new[] { 12, 4 }, witness["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, witness["turnEvidence"]!["diceClaims"]!.AsArray().Select(value => value!["sourceIndex"]!.GetValue<int>()));
        var evidence = witness["turnEvidence"]!;
        Assert.Equal((46, 0, 0), (evidence["turn"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>(), witness["exchangeOrdinal"]!.GetValue<int>()));
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(true, setup.OriginalReceiptBytes),
            new CanonicalBeforeImage(true, setup.OriginalConflictBytes), "chaos_sea", evidence["sessionId"]!.GetValue<string>(),
            evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        Assert.True(JsonNode.DeepEquals(resolved.ProposedRow, instances[^1])); Assert.Equal(resolved.InstanceId, row["instanceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var prior = SpiritualWoundOpportunityReceiptState.Parse(before.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.OriginalHistoryBytes), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.True(prior.IsValid, JsonSerializer.Serialize(prior.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.True(parsed.State!.HasRetainedPressureComparisons); Assert.True(parsed.State.HasExactStrainHistory(history.State!));
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(prior.State!, parsed.State).Disposition);
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(parsed.State, parsed.State).Disposition);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
            parsed.State.MatchRetainedPressureHistory("chaos_sea", actualRoot["activeConflict"]!.AsObject(), 46));
    }
}
