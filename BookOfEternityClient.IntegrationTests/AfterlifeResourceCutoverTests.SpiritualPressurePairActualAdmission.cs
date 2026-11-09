using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Two signed retained III parents must advance ordinary II to one actual IV exchange.</summary>
    [Fact]
    public Task SpiritualPressureGroup_PairOrdinaryTwoAdvancesActualFour() =>
        WithPressureGroupCaseAsync(new("pair_actual_ordinary_two", AdvanceExpected: true), window =>
            AssertPressurePairActualAdmissionAsync(window,
                new(new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2),
                "SpiritualPressureGroup_PairOrdinaryTwoAdvancesActualFour: ordinary II plus two distinct III parents must admit exactly one actual IV interval (trauma 23, maximum IV)."));

    /// <summary>Ordinary III plus two distinct signed retained III parents must clamp the sum5 to actual IV.</summary>
    [Fact]
    public Task SpiritualPressureGroup_PairOrdinaryThreeAdvancesCappedActualFour() =>
        WithPressureGroupCaseAsync(new("pair_actual_ordinary_three", After: "overwhelmed", AdvanceExpected: true), window =>
            AssertPressurePairActualAdmissionAsync(window,
                new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3),
                "SpiritualPressureGroup_PairOrdinaryThreeAdvancesCappedActualFour: ordinary III plus two distinct III parents must clamp sum 5 and admit exactly one actual IV interval (trauma 23, maximum IV)."));

    /// <summary>
    /// Proves real ordinal-zero cold/warm ordinary premises before desired-success Advance.
    /// Failure observation preserves original Issues and frozen pre-revoke DATA without blessing refusal.
    /// Complete signed/current envelopes, roots and first-create History/receipt joins are preflighted
    /// by WithPressureGroupCaseAsync. These parents are retained DATA, not two executed historical births.
    /// </summary>
    private Task AssertPressurePairActualAdmissionAsync(PressureGroupWindow window,
        SpiritualWoundCalculation expectedOrdinaryCalculation, string successMessage) =>
        AssertPressureOwnedGroupActualAdmissionAsync(window, expectedOrdinaryCalculation, successMessage, multiParent: false);

    /// <summary>Shares the exact real owner/C1 proof while keeping v4 defaults and the new closed N3/N4 contour distinct.</summary>
    /// <param name="window">Existing per-case live owners and independently signed complete parent DATA.</param>
    /// <param name="expectedOrdinaryCalculation">Independent full ordinary calculation, never inferred from actual.</param>
    /// <param name="successMessage">Unconditional desired-success diagnosis.</param>
    /// <param name="multiParent">True selects only N3/N4 ordinaryI v6; false preserves exact-two v4.</param>
    private async Task AssertPressureOwnedGroupActualAdmissionAsync(PressureGroupWindow window,
        SpiritualWoundCalculation expectedOrdinaryCalculation, string successMessage, bool multiParent)
    {
        var parentCount = multiParent ? window.Spec.Parents : 2;
        var version = multiParent ? 6 : 4;
        if (multiParent)
        {
            Assert.True(parentCount is 3 or 4);
            Assert.Equal(new PressureGroupCase(window.Spec.Name, Parents: parentCount, After: window.Spec.After, Ranks: window.Spec.Ranks, AdvanceExpected: true), window.Spec);
        }
        else
            Assert.Equal(new PressureGroupCase(window.Spec.Name, After: window.Spec.After, PlayerDie: window.Spec.PlayerDie,
                Resilience: window.Spec.Resilience, Ranks: window.Spec.Ranks, AdvanceExpected: true, LowContour: window.Spec.LowContour), window.Spec);
        Assert.Equal(ExpectedPressureGroupOrdinary(window.Spec), expectedOrdinaryCalculation);
        Assert.Equal(parentCount, window.Spec.Parents);
        Assert.Equal(parentCount, window.ExpectedParentJson.Count);
        var expectedOrdinary = ReadPressurePairAdmissionExchange(window.Spec.After, window.Spec.PlayerDie);
        var expectedActualCalculation = multiParent ? SpiritualPressureMultiParentData.ActualCalculation : ExpectedPressurePairActual(window.Spec);
        var actualStrain = expectedActualCalculation.Input.NewStrain!;
        var expectedActual = ReadPressurePairAdmissionExchange(actualStrain, window.Spec.PlayerDie);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, window.Copy.Ordinary));
        Assert.Equal(window.Spec.After, window.Copy.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(0, window.Frontier.NextOrdinal);
        Assert.True(window.Capture.IsCurrentOwner);
        var originalInventory = window.Capture.ReadC1ImageInventory(window.Lease);
        var before = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((0, 0), (before.OrdinaryEvaluations, before.ActualEvaluations));
        var sourceBefore = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(0, sourceBefore.ResourceBatchComputations);

        var comparisonResult = await ReadPressureGroupComparisonAsync(window);
        if (multiParent)
        {
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "multi_parent_cold_ordinary_after_lawful_signed_preflight",
                window.Spec.Name, parentCount, expectedOrdinaryCalculation, expectedActualCalculation,
                expectedOrdinaryJson = expectedOrdinary.ToJsonString(), expectedActualJson = expectedActual.ToJsonString(),
                expectedParents = window.ExpectedParentJson,
                rawInputBase64 = Convert.ToBase64String(window.Files[AfterlifeSpiritualConflictState.StatePath]),
                comparisonResult.Issues, comparisonReturned = comparisonResult.Comparison is not null,
                counts = new { Queries = window.Capture.ReadExistingStrainReuseCounts().Queries,
                    SignedParentReads = window.Capture.ReadExistingStrainReuseCounts().SignedParentReads,
                    OrdinaryEvaluations = window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations,
                    ActualEvaluations = window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations,
                    FreshInputChecks = window.Capture.ReadExistingStrainReuseCounts().FreshInputChecks,
                    FreshDependencyChecks = window.Capture.ReadExistingStrainReuseCounts().FreshDependencyChecks },
                sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts(),
                window.Frontier.SourceStateJson, window.Frontier.AllocationJournalJson }));
            RequireGuardianJourneyIssues(window.Spec.Name, "multi_parent_cold_ordinary_desired_success", comparisonResult.Issues);
        }
        AssertPressureGroupComparison(window, comparisonResult);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(comparisonResult.Comparison);
        Assert.Equal(expectedOrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.Queries + 1, parentCount, 1, 0),
            (counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.True(counts.FreshInputChecks > before.FreshInputChecks);
        Assert.True(counts.FreshDependencyChecks > before.FreshDependencyChecks);
        var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, parentCount), sourceCounts.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, parentCount), sourceCounts.SignedBatch);
        Assert.Equal(1, sourceCounts.ResourceBatchComputations);
        comparison.ReadOrdinaryExchange()["after"]!["playerSideStrain"] = "broken";
        var warmResult = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, warmResult);
        Assert.Equal(expectedOrdinaryCalculation, warmResult.Comparison!.OrdinaryCalculation);
        var warmCounts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal(counts.Queries + 1, warmCounts.Queries);
        Assert.Equal((counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations),
            (warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations));
        Assert.True(warmCounts.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(warmCounts.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        counts = warmCounts;
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        await window.Capture.RequestExistingPressureActualRefusalObservationAsync(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureOrdinaryFrontiersEqual(window.Frontier,
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));

        if (window.Spec.LowContour is not null)
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "pair_low_independent_ordinary_before_actual", window.Spec.Name, window.Spec.LowContour,
                expectedOrdinaryCalculation, expectedActualCalculation, expectedOrdinaryJson = expectedOrdinary.ToJsonString(),
                expectedActualJson = expectedActual.ToJsonString(), expectedParents = window.ExpectedParentJson,
                rawInputBase64 = Convert.ToBase64String(window.Files[AfterlifeSpiritualConflictState.StatePath]),
                window.Frontier.SourceStateJson, window.Frontier.AllocationJournalJson,
                queries = counts.Queries, signedParentReads = counts.SignedParentReads,
                ordinaryEvaluations = counts.OrdinaryEvaluations, actualEvaluations = counts.ActualEvaluations,
                freshInputChecks = counts.FreshInputChecks, freshDependencyChecks = counts.FreshDependencyChecks, sourceCounts
            }));
        var advanced = await MeasurePressureOwnerPhaseAsync(multiParent ? "multi_parent_actual_admission" : "pair_actual_admission", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        var admitted = advanced.Issues.Count == 0 &&
            advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
        Exception? observationFailure = null;
        if (!admitted)
            observationFailure = await Record.ExceptionAsync(async () =>
            {
                Assert.Null(advanced.Step);
                await AssertPressurePairRefusalObservationAsync(window, advanced.Issues, sourceCounts);
            });
        try
        {
            // Desired success remains unconditional; original Issues, not observation assertions, own causal RED.
            RequireGuardianJourneyIssues(window.Spec.Name, multiParent ? "multi_parent_actual_admission" : "pair_low_actual_admission", advanced.Issues);
            Assert.True(admitted, successMessage);
        }
        catch (Exception primary)
        {
            if (observationFailure is not null) RetainPressureFixtureFailure(primary, "pair_actual_refusal_observation", observationFailure);
            throw;
        }
        Assert.Null(observationFailure);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        Assert.True(window.Capture.IsCurrentOwner);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
        Assert.Equal((0, "exchange_pressure_group_46"), (interval.Ordinal, interval.ExchangeId));
        Assert.Empty(interval.ReplayTransitions);
        Assert.Equal(2, interval.AppliedTransitions.Count);
        foreach (var expected in comparison.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
            var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                    spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
        }
        var closed = Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        Assert.Equal((interval.ConflictId, "exchange_pressure_group_46", 0),
            (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(new[] { window.Spec.PlayerDie, 12 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Null(source.GuaranteedSeverityRank);
        Assert.Null(source.RetraumaWoundId);
        Assert.Equal(expectedActualCalculation, source.Calculation);
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        AssertPressureGroupComparisonSource(window, source, expectedOrdinaryCalculation, expectedOrdinary, version);

        var prefix = window.Capture.ReadClosedResourceCandidatePrefix(window.Lease, interval);
        var expectedRoot = window.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        expectedRoot["activeConflict"]!["playerSideStrain"] = actualStrain;
        var companion = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
        Assert.True(JsonNode.DeepEquals(expectedRoot, companion));
        // The complete literal exchange and this whole-root restoration permit exactly these two JSON paths.
        var restored = companion.DeepClone().AsObject();
        Assert.Equal(actualStrain, restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(actualStrain, restored["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = window.Spec.After;
        restored["activeConflict"]!["playerSideStrain"] = window.Spec.After;
        Assert.True(JsonNode.DeepEquals(window.Copy.Conflict, restored));
        var candidate = await window.Capture.ReadC1CandidateOwnerImagesAsync(window.Lease, interval);
        Assert.True(JsonNode.DeepEquals(expectedRoot,
            JsonNode.Parse(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson),
            JsonNode.Parse(candidate[ResourceMaterializationContract.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson),
            JsonNode.Parse(candidate[ResourceMaterializationContract.HistoryPath].Bytes!)));
        AssertPressureGroupC1ParentsAndInventory(window, originalInventory, candidate);
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson,
            prefix.StateJson, expectedRoot["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((parentCount, 1, 1), (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.True(after.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(after.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts with { ResourceBatchComputations = 2 },
            window.Capture.ReadExistingPressureGroupComputationCounts());
        // The real C1 writer performs fresh continuation probes after this exact admission checkpoint.
        await AssertPressureGroupFirstOfferAsync(window, interval, source, expectedOrdinaryCalculation, expectedOrdinary, version);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
    }

    /// <summary>Checks the real issuer's closed v4 or N3/N4 v6 payload against every independent pre-sign parent and ordinary fact.</summary>
    /// <param name="window">One owned fixture with a complete independently authored parent list.</param>
    /// <param name="source">Actual source returned by the live owner.</param>
    /// <param name="expectedOrdinaryCalculation">Independent full ordinary calculation.</param>
    /// <param name="expectedOrdinary">Independent complete ordinary exchange.</param>
    /// <param name="comparisonVersion">Only unchanged exact-two v4 or the closed N3/N4 v6 contour.</param>
    private static void AssertPressureGroupComparisonSource(PressureGroupWindow window,
        ValidationService.PreparedSpiritualSource source, SpiritualWoundCalculation expectedOrdinaryCalculation,
        JsonObject expectedOrdinary, int comparisonVersion = 4)
    {
        var parentCount = comparisonVersion == 4 ? 2 : window.Spec.Parents;
        Assert.True(comparisonVersion == 4 || comparisonVersion == 6 && parentCount is 3 or 4);
        Assert.Equal(parentCount, window.Spec.Parents);
        var encoded = Assert.IsType<string>(source.StrainPreimageJson);
        var proof = JsonNode.Parse(encoded)!.AsObject();
        Assert.Equal(new[] { "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parents", "version" },
            proof.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(comparisonVersion, proof["version"]!.GetValue<int>());
        Assert.Equal(comparisonVersion == 4 ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1", proof["inputRoleRevision"]!.GetValue<string>());
        var input = proof["originalInput"]!.AsObject();
        Assert.Equal(new[] { "contentFingerprint", "exchangePointer", "existed", "path" },
            input.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, input["path"]!.GetValue<string>());
        Assert.True(input["existed"]!.GetValue<bool>());
        Assert.Equal(new CanonicalBeforeImage(true, window.Files[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
            input["contentFingerprint"]!.GetValue<string>());
        Assert.Equal("/activeConflict/exchangeLog/0", input["exchangePointer"]!.GetValue<string>());
        var ordinaryJson = new UTF8Encoding(false, true).GetString(
            Convert.FromBase64String(proof["ordinaryExchangeJsonBase64"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, JsonNode.Parse(ordinaryJson)));
        Assert.Equal(SpiritualWoundStateJson.Canonical(expectedOrdinary), ordinaryJson);
        var calculation = proof["ordinaryCalculation"]!.AsObject();
        Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" },
            calculation.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(expectedOrdinaryCalculation.TraumaPressure, calculation["traumaPressure"]!.GetValue<long>());
        Assert.Equal(expectedOrdinaryCalculation.MaximumSeverityRank, calculation["maximumSeverityRank"]!.GetValue<int>());
        Assert.Equal(parentCount, window.ExpectedParentJson.Count);
        var rows = proof["parents"]!.AsArray();
        Assert.Equal(parentCount, rows.Count);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]!.AsObject();
            Assert.Equal(new[] { "componentId", "effectId", "parentWoundJsonBase64" },
                row.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal));
            var parentJson = new UTF8Encoding(false, true).GetString(
                Convert.FromBase64String(row["parentWoundJsonBase64"]!.GetValue<string>()));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(window.ExpectedParentJson[index]), JsonNode.Parse(parentJson)));
            var parsed = WoundMaterializationContract.Parse(parentJson, "test.actualPair.issuedParent");
            Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
            var parent = parsed.Wound!;
            Assert.Equal(parentJson, WoundMaterializationContract.SerializeCanonical(parent));
            Assert.Equal("wound_retained_pressure_owner_" + (index + 1), parent.WoundId);
            Assert.Equal((PressureGroupParentRank(window.Spec, index + 1), "active", "spiritual"),
                (parent.Severity.Rank, parent.Lifecycle, parent.Classification.Domain));
            var root = Assert.Single(parent.Consequences.OwnedEffectSources.RootBindings);
            Assert.Equal(root.EffectId, row["effectId"]!.GetValue<string>());
            Assert.Equal("component_001", row["componentId"]!.GetValue<string>());
        }
        Assert.Null(source.OriginalSpecialArtJson);
        Assert.Null(source.GuaranteedSeverityRank);
        Assert.Null(source.RetraumaWoundId);
    }

    /// <summary>Observes frozen pre-revoke DATA only; exact current predicate diagnosis stays in original Issues and no revoked owner is queried as live.</summary>
    private Task AssertPressurePairRefusalObservationAsync(PressureGroupWindow window, IReadOnlyList<ValidationIssue> issues,
        ValidationService.SpiritualPressureGroupComputationCounts sourceCounts)
    {
        Assert.NotEmpty(issues);
        if (window.Spec.LowContour is { } contour)
        {
            var issue = Assert.Single(issues);
            Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
            Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            var expectedDetail = contour switch
            {
                PairLowPressureContour.OnePositive or PairLowPressureContour.OneZero => "actual existing pressure pair requires ordinary II or III",
                PairLowPressureContour.TwoZero or PairLowPressureContour.ThreeZero => "actual existing pressure pair requires a positive ordinary maximum",
                _ => throw new ArgumentOutOfRangeException(nameof(contour))
            };
            Assert.Equal(expectedDetail, issue.Actual);
        }
        var retained = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservationResult>(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Null(retained.UnavailableReason);
        var observed = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservation>(retained.Observation);
        AssertPressureOrdinaryFrontiersEqual(window.Frontier, observed.Frontier);
        Assert.Equal(new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0), observed.Projections);
        Assert.NotNull(retained.Issues); Assert.Equal(issues.Count, retained.Issues.Count);
        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index]; var frozen = retained.Issues[index];
            Assert.Equal((issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint),
                (frozen.FilePath, frozen.Severity, frozen.Message, frozen.Category, frozen.Code, frozen.Actor, frozen.Section, frozen.Expected, frozen.Actual, frozen.RepairHint));
            Assert.Equal(JsonSerializer.Serialize(issue.RepairTargetFiles), frozen.RepairTargetFilesJson);
        }
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((window.Spec.Parents, 1, 0), (counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Copy.Context.FileSystem, window.Lease, out _));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "pair_low_pre_revoke_observation", window.Spec.Name,
            issues, observed.Frontier, observed.Projections, retained.UnavailableReason,
            queries = counts.Queries, signedParentReads = counts.SignedParentReads, ordinaryEvaluations = counts.OrdinaryEvaluations,
            actualEvaluations = counts.ActualEvaluations, freshInputChecks = counts.FreshInputChecks, freshDependencyChecks = counts.FreshDependencyChecks,
            sourceCounts, physicalInputInert = true, preRevocationFrontierInert = true, terminalOwnerRevoked = true }));
        return Task.CompletedTask;
    }

    /// <summary>Fixed complete independent exchange oracle; only the explicitly chosen literal strain varies.</summary>
    private static JsonObject ReadPressurePairAdmissionExchange(string strain, int playerDie = 4) =>
        SpiritualPairLowPressureData.ReadExchange(strain, playerDie);
}
