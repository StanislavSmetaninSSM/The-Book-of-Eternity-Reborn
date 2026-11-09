using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Catches rejection of real owned N2 low/N3 near-cap actual-zero consequences after complete signed/current preflight.</summary>
    /// <param name="contour">Only N2 low=0 and N3 near cap=1, each with independent owners and cleanup.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public Task SpiritualGroupZero_RealOwnerAdvancesWithoutOffer(int contour)
    {
        if (contour is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(contour));
        var kind = SpiritualGroupZeroPressureData.Contour(contour);
        var spec = GroupZeroCase("group_zero_owner_" + contour, kind);
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupCaseAsync(spec, async window =>
        {
            _ = await AssertGroupZeroActualAdmissionAsync(window);
        }));
    }

    /// <summary>Maps complete player-zero owner contours before signing; training remains detached DATA only.</summary>
    /// <param name="name">Independent fixture session/request label.</param>
    /// <param name="contour">Closed low, before-II near-cap or before-III resilience4 capped input.</param>
    private static PressureGroupCase GroupZeroCase(string name, GroupZeroPressureContour contour)
    {
        if (contour is not (GroupZeroPressureContour.Low or GroupZeroPressureContour.NearCap or GroupZeroPressureContour.Capped))
            throw new ArgumentOutOfRangeException(nameof(contour));
        var expected = SpiritualGroupZeroPressureData.Expected(contour);
        return new(name, Parents: expected.Parents, After: expected.Ordinary.Input.NewStrain!,
            PlayerDie: expected.PlayerDie, Resilience: expected.Resilience,
            Ranks: contour == GroupZeroPressureContour.Low ? new(3, 4) : null,
            AdvanceExpected: true, ZeroContour: contour);
    }

    /// <summary>Resolves only independent pre-sign fixture geometry, preserving every previous caller's default.</summary>
    /// <param name="spec">Complete closed per-case inputs, not a production comparison.</param>
    private static string PressureGroupPreviousStrain(PressureGroupCase spec) => spec.ZeroContour is { } zero
        ? SpiritualGroupZeroPressureData.Expected(zero).Ordinary.Input.PreviousStrain!
        : spec.CappedOrdinary ? "overwhelmed" : "clear";

    /// <summary>Proves cold/warm ordinary DATA, then demands actual/resource/source/C1/no-offer without creating an injury decision.</summary>
    /// <param name="window">One independently signed/current complete retained group with a real source/resource owner.</param>
    /// <returns>The admitted interval for later source-only consumers, never an independent publication capability.</returns>
    private async Task<AcceptedMechanicsPlanner.SpiritualExchangeInterval> AssertGroupZeroActualAdmissionAsync(PressureGroupWindow window)
    {
        var kind = Assert.IsType<GroupZeroPressureContour>(window.Spec.ZeroContour);
        Assert.Equal(GroupZeroCase(window.Spec.Name, kind), window.Spec);
        var expected = SpiritualGroupZeroPressureData.Expected(kind);
        Assert.Equal(expected.Parents, window.Spec.Parents);
        Assert.Equal(expected.Parents, window.ExpectedParentJson.Count);
        var expectedOrdinary = SpiritualGroupZeroPressureData.ReadExchange(kind);
        var expectedActual = SpiritualGroupZeroPressureData.ReadExchange(kind, actual: true);
        Assert.Equal(expected.Ordinary, ExpectedPressureGroupOrdinary(window.Spec));
        Assert.Equal(expected.Actual, ExpectedPressureOwnedGroupActual(window.Spec));
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, window.Copy.Ordinary));
        Assert.Equal(expected.Ordinary.Input.NewStrain, window.Copy.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(0, window.Frontier.NextOrdinal);
        Assert.True(window.Capture.IsCurrentOwner);
        var inventory = window.Capture.ReadC1ImageInventory(window.Lease);
        var before = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((0, 0), (before.OrdinaryEvaluations, before.ActualEvaluations));
        Assert.Equal(0, window.Capture.ReadExistingPressureGroupComputationCounts().ResourceBatchComputations);
        var result = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, result);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
        Assert.Equal(expected.Ordinary, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.Queries + 1, expected.Parents, 1, 0),
            (counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.True(counts.FreshInputChecks > before.FreshInputChecks);
        Assert.True(counts.FreshDependencyChecks > before.FreshDependencyChecks);
        var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
        var expectedBatch = new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, expected.Parents);
        Assert.Equal(expectedBatch, sourceCounts.CurrentBatch); Assert.Equal(expectedBatch, sourceCounts.SignedBatch);
        Assert.Equal(1, sourceCounts.ResourceBatchComputations);
        var detached = comparison.ReadOrdinaryExchange();
        Assert.NotEqual("clear", detached["after"]!["playerSideStrain"]!.GetValue<string>());
        detached["after"]!["playerSideStrain"] = "clear";
        Assert.Equal("clear", detached["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
        var warm = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, warm);
        Assert.Equal(expected.Ordinary, warm.Comparison!.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, warm.Comparison.ReadOrdinaryExchange()));
        var warmCounts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(counts.Queries + 1, warmCounts.Queries);
        Assert.Equal((counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations),
            (warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations));
        Assert.True(warmCounts.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(warmCounts.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        counts = warmCounts;
        await AssertPressureGroupWindowInertAsync(window);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        await window.Capture.RequestExistingPressureActualRefusalObservationAsync(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureOrdinaryFrontiersEqual(window.Frontier,
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "group_zero_lawful_ordinary_before_actual", window.Spec.Name, kind, expected,
            expectedOrdinaryJson = expectedOrdinary.ToJsonString(), expectedActualJson = expectedActual.ToJsonString(),
            expectedParents = window.ExpectedParentJson,
            rawInputBase64 = Convert.ToBase64String(window.Files[AfterlifeSpiritualConflictState.StatePath]),
            window.Frontier.SourceStateJson, window.Frontier.AllocationJournalJson,
            queries = counts.Queries, signedParentReads = counts.SignedParentReads,
            ordinaryEvaluations = counts.OrdinaryEvaluations, actualEvaluations = counts.ActualEvaluations,
            freshInputChecks = counts.FreshInputChecks, freshDependencyChecks = counts.FreshDependencyChecks, sourceCounts
        }));

        var advanced = await MeasurePressureOwnerPhaseAsync("group_zero_actual_admission", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        var admitted = advanced.Issues.Count == 0 && advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
        Exception? observationFailure = null;
        if (!admitted)
            observationFailure = await Record.ExceptionAsync(async () =>
            {
                Assert.Null(advanced.Step);
                var issue = Assert.Single(advanced.Issues);
                Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
                Assert.Equal("actual existing pressure requires a positive hypothetical actual maximum", issue.Actual);
                // This helper reads frozen refusal/counter DATA and physical input only, never a normal operation on the revoked owner.
                await AssertPressurePairRefusalObservationAsync(window, advanced.Issues, sourceCounts);
            });
        try
        {
            RequireGuardianJourneyIssues(window.Spec.Name, "group_zero_actual_admission", advanced.Issues);
            Assert.True(admitted, "Every complete lawful player group must apply actual-zero strain without an injury offer.");
        }
        catch (Exception primary)
        {
            if (observationFailure is not null) RetainPressureFixtureFailure(primary, "group_zero_refusal_observation", observationFailure);
            throw;
        }
        Assert.Null(observationFailure);
        Assert.True(window.Capture.IsCurrentOwner);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
        Assert.Equal((0, "exchange_pressure_group_46"), (interval.Ordinal, interval.ExchangeId));
        Assert.Empty(interval.ReplayTransitions); Assert.Equal(2, interval.AppliedTransitions.Count);
        foreach (var transition in comparison.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == transition.Coordinate);
            var maximum = transition.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                    spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
        }
        var closed = Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        Assert.Equal((interval.ConflictId, "exchange_pressure_group_46", 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { expected.PlayerDie, 12 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal(expected.Actual, source.Calculation);
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        AssertPressureGroupComparisonSource(window, source, expected.Ordinary, expectedOrdinary, expected.Parents == 2 ? 4 : 6);
        var prefix = window.Capture.ReadClosedResourceCandidatePrefix(window.Lease, interval);
        var expectedRoot = window.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        expectedRoot["activeConflict"]!["playerSideStrain"] = expected.Actual.Input.NewStrain;
        var companion = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
        Assert.True(JsonNode.DeepEquals(expectedRoot, companion));
        var restored = companion.DeepClone();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = expected.Ordinary.Input.NewStrain;
        restored["activeConflict"]!["playerSideStrain"] = expected.Ordinary.Input.NewStrain;
        Assert.True(JsonNode.DeepEquals(window.Copy.Conflict, restored));
        var candidate = await window.Capture.ReadC1CandidateOwnerImagesAsync(window.Lease, interval);
        Assert.True(JsonNode.DeepEquals(expectedRoot, JsonNode.Parse(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(candidate[ResourceMaterializationContract.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(candidate[ResourceMaterializationContract.HistoryPath].Bytes!)));
        AssertPressureGroupC1ParentsAndInventory(window, inventory, candidate);
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson, expectedRoot["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((expected.Parents, 1, 1), (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.True(after.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(after.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts with { ResourceBatchComputations = 2 }, window.Capture.ReadExistingPressureGroupComputationCounts());

        var offered = await window.Capture.ComposeC1FirstOfferAsync(window.Lease, interval);
        Assert.Empty(offered.Issues); Assert.Null(offered.Pending); Assert.Null(offered.ProposedInstanceRow);
        var checkpoint = await window.Capture.ComposeC2FirstCheckpointDraftAsync(window.Lease, interval);
        Assert.Empty(checkpoint.Issues); Assert.Null(checkpoint.Checkpoint); Assert.Null(checkpoint.Pending);
        var transport = await window.Capture.CommitC2FirstTransportAsync(window.Lease, interval);
        Assert.Empty(transport.Issues); Assert.Equal("no_offer", transport.Disposition);
        Assert.Null(transport.Pending); Assert.Null(transport.Checkpoint);
        Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Copy.Context.FileSystem, window.Lease, out _));
        return interval;
    }
}
