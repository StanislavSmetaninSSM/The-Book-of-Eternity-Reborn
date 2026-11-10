using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires one genuine cold source-only v7 all-parent comparison after complete warm disposal, inspecting only an unpublished C4 plan.</summary>
    [Fact]
    public Task SpiritualGuardianGroupZero_N2SourceOnlyPreparesUnpublished() =>
        WithGuardianGroupZeroPressureSetupAsync("guardian_group_zero_source_only_c3", new GuardianGroupZeroPressureData(SpiritualGuardianGroupPressureTestData.Create(GuardianGroupPressureCase.LowMarginZeroPair)),
            CompleteGuardianGroupZeroSourceOnlyC3Async);

    /// <summary>Disposes the warm no-offer owner and physical lease, recaptures identical inputs cold, then inspects genuine source-only C3 without registration.</summary>
    /// <param name="setup">One independently signed low-margin N2 original and its exact post-sign ordinary bytes.</param>
    private async Task CompleteGuardianGroupZeroSourceOnlyC3Async(GuardianGroupZeroPressureSetup setup)
    {
        var context = setup.Context;
        Assert.Equal(5, setup.Data.Resilience);
        var warmValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        ValidationService.SpiritualOriginalTurnCapture? warmCapture = null;
        FileSystemManager.CanonicalWriteLease? warmLease = null;
        string? warmExchange = null; string? warmPreimage = null;
        IReadOnlyDictionary<string, byte[]>? warmFiles = null; byte[]? warmGeneration = null;
        await WithGuardianGroupZeroPressureCaptureAsync(setup, warmValidator, "warm_no_offer", async window =>
        {
            warmCapture = window.Capture; warmLease = window.Lease;
            warmFiles = window.Files; warmGeneration = window.Generation;
            await AssertGuardianGroupZeroPressureActualAndC1Async(window);
            var source = Assert.Single(Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease)).Sources);
            warmExchange = source.ExchangeJson; warmPreimage = source.StrainPreimageJson;
            Assert.Equal(setup.RawOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(window.Lease, AfterlifeSpiritualConflictState.StatePath));
        });
        Assert.NotNull(warmCapture); Assert.NotNull(warmLease);
        Assert.False(warmCapture!.IsCurrentOwner); Assert.False(warmLease!.IsActive);
        Assert.NotNull(warmFiles); Assert.NotNull(warmGeneration);
        AssertGuardianGroupReceiptExactFiles(context, warmFiles!, warmGeneration!);
        var retiredFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(setup.RawOrdinaryBytes, retiredFiles[AfterlifeSpiritualConflictState.StatePath]);
        foreach (var pair in setup.Authored)
            Assert.Equal(pair.Value, retiredFiles.TryGetValue(pair.Key, out var bytes) ? bytes : null);
        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        Assert.NotSame(warmValidator, coldValidator);
        await WithGuardianGroupZeroPressureCaptureAsync(setup, coldValidator, "cold_source_only", async window =>
        {
            Assert.NotSame(warmCapture, window.Capture); Assert.NotSame(warmLease, window.Lease);
            AssertGuardianGroupReceiptExactFiles(context, retiredFiles, warmGeneration!);
            Assert.Equal(warmGeneration, window.Generation);
            Assert.False(warmCapture.IsCurrentOwner); Assert.False(warmLease.IsActive);
            var interval = await AssertGuardianGroupZeroPressureActualAndC1Async(window);
            var capture = window.Capture; var lease = window.Lease;
            var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
            Assert.Equal(warmExchange, source.ExchangeJson); Assert.Equal(warmPreimage, source.StrainPreimageJson);
            Assert.Equal(setup.Data.ActualCalculation, source.Calculation);
            var proof = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject();
            Assert.Equal(7, proof["version"]!.GetValue<int>());
            Assert.Equal((-4L, 0), (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(),
                proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
            var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
            var beforeCompletionCounts = capture.ReadExistingStrainReuseCounts();
            var completion = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_source_only_completion", setup.Name,
                () => capture.CompleteOrdinaryReductionAsync(lease));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_completion_result", setup.Name,
                completion.Issues, completion.Success, beforeCompletionCounts = new { beforeCompletionCounts.Queries,
                    beforeCompletionCounts.SignedParentReads, beforeCompletionCounts.OrdinaryEvaluations,
                    beforeCompletionCounts.ActualEvaluations, beforeCompletionCounts.FreshInputChecks, beforeCompletionCounts.FreshDependencyChecks },
                counters = ReadGroupZeroMeasurementCounters(capture) }));
            RequireGuardianJourneyIssues(setup.Name, "guardian_group_zero_source_only_completion", completion.Issues);
            Assert.True(completion.Success);
            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completion.Reduction);
            Assert.True(reduction.RequiresSatisfactionReceipt);
            Assert.False(reduction.HasLiveDecisionReceipt); Assert.False(reduction.HasLiveWoundWork); Assert.Null(reduction.LiveWoundCompletion);
            Assert.Empty(reduction.Resources.ReplayTransitions); Assert.Equal(2, reduction.Resources.AppliedTransitions.Count);
            Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
            var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
            var checkedEffects = AssertGuardianGroupZeroCompletedEffects(setup, effects, reduction.Resources, interval);
            var effectPayload = checkedEffects.PayloadFingerprint;
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
            var definitions = prefix.DefinitionsJson;
            var projected = AfterlifeConflictActionPointProjectionService.Resolve(definitions,
                reduction.Resources.StateAfterImage.ToCanonicalJson(), actualRoot["activeConflict"]!.AsObject());
            RequireGuardianJourneyIssues(setup.Name, "guardian_group_zero_source_only_ap", projected.Issues);
            Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
            var journal = capture.ReadAllocationJournal(lease).ToJsonString(); var cursor = capture.ReadAllocationCursor(lease);
            var beforeRepeated = capture.ReadExistingStrainReuseCounts();
            var beforeRepeatedCounts = ReadGroupZeroMeasurementCounters(capture);
            var repeated = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_repeated_completion", setup.Name,
                () => capture.CompleteOrdinaryReductionAsync(lease));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_repeated_completion_result", setup.Name,
                repeated.Issues, repeated.Success, beforeRepeatedCounts, afterRepeatedCounts = ReadGroupZeroMeasurementCounters(capture) }));
            Assert.Empty(repeated.Issues); Assert.Same(completion, repeated);
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.True(capture.ReadExistingStrainReuseCounts().FreshInputChecks > beforeRepeated.FreshInputChecks);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "guardian_group_zero_source_only_preparation_prerequisites", setup.Name,
                warmCaptureCurrent = warmCapture.IsCurrentOwner, warmLeaseActive = warmLease.IsActive,
                freshValidatorCaptureLease = true, ordinary = setup.Data.OrdinaryCalculation, actual = setup.Data.ActualCalculation,
                rawOrdinaryBytesBase64 = Convert.ToBase64String(setup.RawOrdinaryBytes), source.ExchangeJson, source.StrainPreimageJson,
                priorReceiptJson = ReadGuardianAttributionJson(setup.OriginalReceiptBytes).ToJsonString(),
                originalHistoryJson = ReadGuardianAttributionJson(setup.OriginalHistoryBytes).ToJsonString(),
                actualRootJson = actualRoot.ToJsonString(), resourceState = reduction.Resources.StateAfterImage.ToCanonicalJson(),
                resourceHistory = reduction.Resources.HistoryAfterImage.ToCanonicalJson(), journal, cursor,
                reduction.RequiresSatisfactionReceipt, reduction.HasLiveDecisionReceipt, reduction.HasLiveWoundWork,
                counters = ReadGroupZeroMeasurementCounters(capture)
            }));
            // This existing call internally performs genuine source-only C3; the returned plan remains unregistered and unwritten.
            var prepared = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_source_only_unpublished_preparation", setup.Name,
                () => capture.PrepareC4PublicationAsync(lease));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "guardian_group_zero_source_only_unpublished_preparation_result", setup.Name,
                allPrecedingPrerequisitesAccepted = true, prepared.Issues, authorityReturned = prepared.Authority is not null,
                publicationRegistered = AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease),
                counters = ReadGroupZeroMeasurementCounters(capture)
            }));
            RequireGuardianJourneyIssues(setup.Name, "guardian_group_zero_source_only_unpublished_preparation_desired_success", prepared.Issues);
            var authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_source_only_unpublished_receipt_desired_success",
                setup.Name, receiptJson = receipt.ToJsonString() }));
            AssertGuardianGroupZeroSourceOnlyReceipt(setup, receipt, window.ActualRoot, window.SignedSnapshotFingerprint);
            AssertPressureExistingResourcePlan(authority.Plan, actualRoot);
            Assert.True(JsonNode.DeepEquals(authority.Plan.StateAfterImage, JsonNode.Parse(reduction.Resources.StateAfterImage.ToCanonicalJson())));
            Assert.True(JsonNode.DeepEquals(authority.Plan.HistoryAfterImage, JsonNode.Parse(reduction.Resources.HistoryAfterImage.ToCanonicalJson())));
            Assert.Equal(reduction.Resources.Events.OrderBy(row => row.Turn).ThenBy(row => row.ExecutionSequence)
                .ThenBy(row => row.EventKind, StringComparer.Ordinal).ThenBy(row => row.OperationId, StringComparer.Ordinal), authority.Plan.ResourceEvents);
            Assert.False(authority.Plan.EffectPlan!.HasPressureConflictCarrierCollision());
            AssertGuardianGroupZeroCommonEffectCopy(setup.Name, checkedEffects, authority.Plan);
            Assert.True(JsonNode.DeepEquals(actualRoot, authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(setup.RawOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertExistingPressureInputInert(context, lease, window.Files, window.Generation);
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_unpublished_complete", setup.Name,
                receipt, authority.Plan.OwnerCompanionAfterImages, authority.Plan.StateAfterImage, authority.Plan.HistoryAfterImage,
                effectPayload, journal, cursor, counters = ReadGroupZeroMeasurementCounters(capture) }));
        });
    }

}
