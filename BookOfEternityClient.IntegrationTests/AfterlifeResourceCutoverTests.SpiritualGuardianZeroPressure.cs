using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires real rankIII/IV actual maximum-zero admission, C1 no offer and first C2 no_offer, without a private decision.</summary>
    /// <param name="rank">Independent expected retained parent rank, fixed before original signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianZero_RealOwnerAdvancesWithoutOffer(int rank) =>
        WithGuardianZeroPressureOwnerAsync("guardian_zero_owner_" + rank, SpiritualGuardianZeroPressureTestData.Create(rank),
            AssertGuardianZeroPressureActualAndC1Async);

    /// <summary>Isolates ordinary positivity while actual11/maxI remains positive; one C1 offer is required and no C2 decision is taken.</summary>
    [Fact]
    public Task SpiritualGuardianZero_OrdinaryZeroPositiveActualOffersOnce() =>
        WithGuardianZeroPressureOwnerAsync("guardian_zero_positive_actual_sentinel",
            SpiritualGuardianZeroPressureTestData.CreateOrdinaryZeroPositiveActual(), AssertGuardianZeroPressureActualAndC1Async);

    /// <summary>Exercises the actual owner, ordinary DATA reuse, one real admission and the appropriate zero or positive C1 result.</summary>
    /// <param name="window">Genuine independent signed/current fixture with original tiers and exact ordinary raw input already proved.</param>
    /// <returns>The same already checked owned interval; returning it adds no computation or authority.</returns>
    private async Task<AcceptedMechanicsPlanner.SpiritualExchangeInterval> AssertGuardianZeroPressureActualAndC1Async(GuardianZeroPressureWindow window)
    {
        var capture = window.Capture; var lease = window.Lease;
        var before = capture.ReadExistingStrainReuseCounts(); var sourceBefore = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal((0, 0, 0, 0), (before.Queries, before.SignedParentReads, before.OrdinaryEvaluations, before.ActualEvaluations));
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(0, 0, 0, 0, 0), sourceBefore.CurrentBatch);
        Assert.Equal(sourceBefore.CurrentBatch, sourceBefore.SignedBatch); Assert.Equal(0, sourceBefore.ResourceBatchComputations);
        var comparison = await Query("cold");
        var cold = capture.ReadExistingStrainReuseCounts(); var sourceCold = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal((before.Queries + 1, before.SignedParentReads + 1, before.OrdinaryEvaluations + 1, before.ActualEvaluations),
            (cold.Queries, cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations));
        Assert.True(cold.FreshInputChecks > before.FreshInputChecks); Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), sourceCold.CurrentBatch);
        Assert.Equal(sourceCold.CurrentBatch, sourceCold.SignedBatch); Assert.Equal(1, sourceCold.ResourceBatchComputations);
        var detached = comparison.ReadOrdinaryExchange();
        detached[SpiritualWoundFailedAction.Property]!["actorId"] = "guardian_unrelated";
        detached["after"]!["oppositionSideStrain"] = "broken";
        var repeated = await Query("warm");
        var warm = capture.ReadExistingStrainReuseCounts();
        Assert.Equal(cold.Queries + 1, warm.Queries);
        Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
            (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
        Assert.True(warm.FreshInputChecks > cold.FreshInputChecks); Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
        Assert.Equal(sourceCold, capture.ReadExistingPressureGroupComputationCounts());
        Assert.Equal(comparison.OrdinaryExchangeJson, repeated.OrdinaryExchangeJson);
        Assert.Equal(comparison.OrdinaryCalculation, repeated.OrdinaryCalculation);
        Assert.Equal(comparison.ExpectedTransitions, repeated.ExpectedTransitions);
        Assert.Equal((comparison.RawInputFingerprint, comparison.ExchangePointer), (repeated.RawInputFingerprint, repeated.ExchangePointer));
        var inventory = capture.ReadC1ImageInventory(lease);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_lawful_ordinary_before_actual", window.Name,
            window.Data.Rank, window.Data.Resilience, ordinary = window.Data.OrdinaryCalculation, actual = window.Data.ActualCalculation,
            comparison.OrdinaryExchangeJson, comparison.RawInputFingerprint, comparison.ExchangePointer,
            counters = ReadGuardianZeroPressureCounts(capture) }));
        var advanced = await MeasurePressureOwnerPhaseAsync("guardian_zero_actual_admission", window.Name,
            () => capture.AdvanceNextResourceExchangeAsync(lease));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_actual_desired_success", window.Name,
            advanced.Issues, intervalReturned = advanced.Step?.Interval is not null,
            counters = ReadGuardianZeroPressureCounts(capture) }));
        RequireGuardianJourneyIssues(window.Name, "guardian_zero_actual_admission", advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        Assert.True(capture.IsCurrentOwner); Assert.Equal((0, "exchange_guardian_zero_46"), (interval.Ordinal, interval.ExchangeId));
        Assert.Empty(interval.ReplayTransitions); Assert.Equal(2, interval.AppliedTransitions.Count);
        foreach (var expected in comparison.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
            var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                    spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
        }
        var afterActual = capture.ReadExistingStrainReuseCounts();
        Assert.Equal((warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations + 1),
            (afterActual.SignedParentReads, afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations));
        Assert.True(afterActual.FreshInputChecks > warm.FreshInputChecks); Assert.True(afterActual.FreshDependencyChecks > warm.FreshDependencyChecks);
        Assert.Equal(sourceCold with { ResourceBatchComputations = sourceCold.ResourceBatchComputations + 1 },
            capture.ReadExistingPressureGroupComputationCounts());
        var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal((interval.ConflictId, interval.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { 12, 4 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(window.Data.Actual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", "player_soul:player_soul", "guardian:" + SpiritualGuardianAttributionTestData.GuardianId),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal(window.Data.ActualCalculation, source.Calculation);
        Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank); Assert.Null(source.RetraumaWoundId);
        Assert.True(JsonNode.DeepEquals(window.Data.Actual, JsonNode.Parse(source.ExchangeJson)));
        var proof = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject();
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parentWoundJsonBase64", "version" },
            proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(5, proof["version"]!.GetValue<int>());
        Assert.Equal(SpiritualGuardianAttributionTestData.Role, proof["inputRoleRevision"]!.GetValue<string>());
        Assert.Equal((window.Data.OrdinaryCalculation.TraumaPressure, 0),
            (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
        Assert.Equal(comparison.RawInputFingerprint, proof["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.Equal("/activeConflict/exchangeLog/0", proof["originalInput"]!["exchangePointer"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(window.Data.Retained.Parents.Single(), SpiritualExistingPressureTestData.ReadParent(
            new JsonObject { ["strainPreimage"] = proof.DeepClone() })));
        Assert.Equal(window.Data.Retained.Effects.Single()["effectId"]!.GetValue<string>(), proof["effectId"]!.GetValue<string>());
        Assert.Equal("component_001", proof["componentId"]!.GetValue<string>());
        var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
        Assert.True(JsonNode.DeepEquals(window.ActualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var restored = window.ActualRoot;
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "fractured";
        restored["activeConflict"]!["oppositionSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(window.OrdinaryRoot, restored));
        var candidate = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        Assert.True(JsonNode.DeepEquals(window.ActualRoot, ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(window.Files[path], candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);
        var profileImage = candidate.TryGetValue(AfterlifeEntityProfileState.StatePath, out var profiles)
            ? profiles.Bytes : inventory.BeforeImages[AfterlifeEntityProfileState.StatePath].Bytes;
        var guardian = ReadGuardianAttributionProfile(ReadGuardianAttributionJson(profileImage!));
        Assert.True(JsonNode.DeepEquals(new JsonArray(window.Data.Retained.Parents.Single().DeepClone()), guardian["activeWounds"]));
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson, window.ActualRoot["activeConflict"]!.AsObject());
        RequireGuardianJourneyIssues(window.Name, "guardian_zero_actual_ap", projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var offered = await MeasurePressureOwnerPhaseAsync("guardian_zero_first_c1", window.Name,
            () => capture.ComposeC1FirstOfferAsync(lease, interval));
        RequireGuardianJourneyIssues(window.Name, "guardian_zero_first_c1", offered.Issues);
        if (window.Data.Resilience == 4)
        {
            Assert.Equal(0, source.Calculation.MaximumSeverityRank);
            Assert.Null(offered.Pending); Assert.Null(offered.ProposedInstanceRow);
            var checkpoint = await capture.ComposeC2FirstCheckpointDraftAsync(lease, interval);
            RequireGuardianJourneyIssues(window.Name, "guardian_zero_first_c2_draft", checkpoint.Issues);
            Assert.Null(checkpoint.Pending); Assert.Null(checkpoint.Checkpoint);
            var transport = await MeasurePressureOwnerPhaseAsync("guardian_zero_first_c2_no_offer", window.Name,
                () => capture.CommitC2FirstTransportAsync(lease, interval));
            RequireGuardianJourneyIssues(window.Name, "guardian_zero_first_c2_no_offer", transport.Issues);
            Assert.Equal("no_offer", transport.Disposition); Assert.Null(transport.Pending); Assert.Null(transport.Checkpoint);
        }
        else
        {
            Assert.Equal((3, 2, 1), (window.Data.Rank, window.Data.Resilience, source.Calculation.MaximumSeverityRank));
            var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending);
            var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(pending))!["pending"]!.AsObject();
            var witness = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
            Assert.Equal(("opposition", 1), (witness["affectedSide"]!.GetValue<string>(), witness["maximumSeverityRank"]!.GetValue<int>()));
            Assert.Equal(0, witness["strainPreimage"]!["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>());
            Assert.Empty(packet["stagedDecisions"]!.AsArray());
            Assert.Equal(window.Data.ActualCalculation, SpiritualWoundSourceWitness.Validate(witness));
            // The sentinel stops here: no checkpoint commit, C2 decision, source-only completion or publication.
        }
        Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        AssertExistingPressureInputInert(window.Context, lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Context, lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Context.FileSystem, lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Context.FileSystem, lease, out _));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_actual_c1_contract_complete", window.Name,
            window.Data.Rank, window.Data.Resilience, expectedMaximum = window.Data.ActualCalculation.MaximumSeverityRank,
            noOffer = offered.Pending is null, proof, counters = ReadGuardianZeroPressureCounts(capture) }));

        return interval;

        // Reads the real ordinary owner and preserves its exact result/counters before a desired-success assertion.
        async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> Query(string phase)
        {
            var beforeQuery = ReadGuardianZeroPressureCounts(capture);
            var result = await ExecutePressureGroupComparisonStagesAsync(
                () => MeasurePressureOwnerPhaseAsync("guardian_zero_ordinary_" + phase, window.Name,
                    () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease)), () => capture.IsCurrentOwner,
                async () =>
                {
                    AssertPressureOrdinaryFrontiersEqual(window.Frontier, await ReadPressureOwnerSnapshotAsync(capture, lease, window.Name + "_" + phase));
                    AssertExistingPressureInputInert(window.Context, lease, window.Files, window.Generation);
                    AssertPressureNoPrivatePair(window.Context, lease);
                });
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_ordinary_desired_success", query = phase, window.Name,
                window.Data.Rank, window.Data.Resilience, result.Issues, result.PrefixBlocked, comparisonReturned = result.Comparison is not null,
                beforeQuery, afterQuery = ReadGuardianZeroPressureCounts(capture) }));
            RequireGuardianJourneyIssues(window.Name, "guardian_zero_ordinary_" + phase, result.Issues);
            Assert.False(result.PrefixBlocked); Assert.True(capture.IsCurrentOwner);
            var value = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
            Assert.True(JsonNode.DeepEquals(window.Data.Ordinary, value.ReadOrdinaryExchange()));
            Assert.Equal(window.Data.OrdinaryCalculation, value.OrdinaryCalculation);
            Assert.Equal(new CanonicalBeforeImage(true, window.RawOrdinaryBytes).Fingerprint, value.RawInputFingerprint);
            Assert.Equal("/activeConflict/exchangeLog/0", value.ExchangePointer); Assert.Equal(2, value.ExpectedTransitions.Count);
            foreach (var kind in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var expected = Assert.Single(value.ExpectedTransitions, row => row.Coordinate.OwnerKind == kind);
                var side = kind == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                var ownerId = side == "player" ? "player_soul" : window.OrdinaryRoot["activeConflict"]!
                    [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                    "turn_46:afterlife_conflict:exchange_guardian_zero_46:" + side, "afterlife_cost", "exchange_guardian_zero_46",
                    new("chaos_sea", kind, ownerId, "spiritual_action_points"), ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m), expected);
            }
            return value;
        }
    }
}
