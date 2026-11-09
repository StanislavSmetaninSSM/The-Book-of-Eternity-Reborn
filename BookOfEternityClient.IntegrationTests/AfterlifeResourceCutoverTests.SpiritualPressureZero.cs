using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Desired-success ordinal-zero owner regression for independently signed retained III/IV DATA.
    /// Synthetic parent creation is not a native birth. Source/C1/no-offer assertions are UNREACHED on baseline.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public Task SpiritualPressureZero_RealOwnerAdvancesWithoutOffer(int parentRank)
    {
        var name = "singleton_pressure_zero_rank_" + parentRank;
        return WithSingletonIvJourneyFailureAsync(name, () =>
            WithPressureGroupCaseAsync(new(name, Parents: 1, After: "strained", Resilience: 3,
                Ranks: new(parentRank, 3), AdvanceExpected: true), async window =>
                { _ = await AssertPressureZeroActualAdmissionAsync(window); }));
    }

    /// <summary>Proves signed/current ordinary prerequisites, then demands a real actual interval without inventing a decision.</summary>
    private async Task<AcceptedMechanicsPlanner.SpiritualExchangeInterval> AssertPressureZeroActualAdmissionAsync(PressureGroupWindow window)
    {
        Assert.Equal(1, window.Spec.Parents);
        Assert.Equal(0, window.Frontier.NextOrdinal);
        Assert.True(window.Capture.IsCurrentOwner);
        var expectedOrdinaryCalculation = SpiritualPressureZeroTestData.OrdinaryCalculation();
        var expectedActualCalculation = SpiritualPressureZeroTestData.ActualCalculation();
        var expectedOrdinary = ReadPressurePairAdmissionExchange("strained");
        var expectedActual = ReadPressurePairAdmissionExchange("fractured");
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, window.Copy.Ordinary));
        Assert.Equal("strained", window.Copy.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
        var expectedParentJson = Assert.Single(window.ExpectedParentJson);
        var expectedParent = WoundMaterializationContract.Parse(expectedParentJson, "test.pressureZero.expectedParent");
        Assert.True(expectedParent.IsValid, string.Join("\n", expectedParent.Issues));
        Assert.Equal((PressureGroupParentRank(window.Spec, 1), "active", "spiritual", "player_soul"),
            (expectedParent.Wound!.Severity.Rank, expectedParent.Wound.Lifecycle,
                expectedParent.Wound.Classification.Domain, expectedParent.Wound.Owner.OwnerId));
        var inventoryBefore = window.Capture.ReadC1ImageInventory(window.Lease);
        var before = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((0, 0), (before.OrdinaryEvaluations, before.ActualEvaluations));
        Assert.Equal(0, window.Capture.ReadExistingPressureGroupComputationCounts().ResourceBatchComputations);
        var comparisonResult = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, comparisonResult);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(comparisonResult.Comparison);
        Assert.Equal(expectedOrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        await AssertPressureGroupWindowInertAsync(window);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.Queries + 1, 1, 1, 0),
            (counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.True(counts.FreshInputChecks > before.FreshInputChecks);
        Assert.True(counts.FreshDependencyChecks > before.FreshDependencyChecks);
        var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), sourceCounts.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), sourceCounts.SignedBatch);
        Assert.Equal(1, sourceCounts.ResourceBatchComputations);
        comparison.ReadOrdinaryExchange()["after"]!["playerSideStrain"] = "broken";
        var warm = await ReadPressureGroupComparisonAsync(window);
        AssertPressureGroupComparison(window, warm);
        Assert.Equal(expectedOrdinaryCalculation, warm.Comparison!.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, warm.Comparison.ReadOrdinaryExchange()));
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
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "pressure_zero_independent_ordinary_before_actual", window.Spec.Name,
            parentRank = expectedParent.Wound.Severity.Rank, expectedOrdinaryCalculation, expectedActualCalculation,
            expectedOrdinaryJson = expectedOrdinary.ToJsonString(), expectedActualJson = expectedActual.ToJsonString(),
            expectedParentJson, rawInputBase64 = Convert.ToBase64String(window.Files[AfterlifeSpiritualConflictState.StatePath]),
            window.Frontier.SourceStateJson, window.Frontier.AllocationJournalJson,
            queries = counts.Queries, signedParentReads = counts.SignedParentReads,
            ordinaryEvaluations = counts.OrdinaryEvaluations, actualEvaluations = counts.ActualEvaluations,
            freshInputChecks = counts.FreshInputChecks, freshDependencyChecks = counts.FreshDependencyChecks, sourceCounts
        }));

        var advanced = await MeasurePressureOwnerPhaseAsync("pressure_zero_actual_admission", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        var admitted = advanced.Issues.Count == 0 &&
            advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
        Exception? observationFailure = null;
        if (!admitted)
            observationFailure = await Record.ExceptionAsync(async () =>
            {
                Assert.Null(advanced.Step);
                await AssertPressureZeroRefusalObservationAsync(window, advanced.Issues, sourceCounts);
            });
        try
        {
            // Original Issues own the causal RED. Observation never converts unsupported behavior into PASS.
            RequireGuardianJourneyIssues(window.Spec.Name, "pressure_zero_actual_admission", advanced.Issues);
            Assert.True(admitted, "A lawful singleton III/IV pressure actual7/max0 must advance without a wound offer.");
        }
        catch (Exception primary)
        {
            if (observationFailure is not null)
                RetainPressureFixtureFailure(primary, "pressure_zero_refusal_observation", observationFailure);
            throw;
        }
        Assert.Null(observationFailure);

        // UNREACHED on baseline: complete intended actual/resource/source/C1/no-offer behavior.
        Assert.True(window.Capture.IsCurrentOwner);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
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
        Assert.Equal(new[] { 4, 12 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal(expectedActualCalculation, source.Calculation);
        Assert.Null(source.GuaranteedSeverityRank);
        Assert.Null(source.RetraumaWoundId);
        Assert.Null(source.OriginalSpecialArtJson);
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        AssertPressureZeroIssuedComparison(window, source, expectedOrdinary, expectedParentJson);
        var prefix = window.Capture.ReadClosedResourceCandidatePrefix(window.Lease, interval);
        var expectedRoot = window.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        expectedRoot["activeConflict"]!["playerSideStrain"] = "fractured";
        var companion = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
        Assert.True(JsonNode.DeepEquals(expectedRoot, companion));
        var restored = companion.DeepClone().AsObject();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "strained";
        restored["activeConflict"]!["playerSideStrain"] = "strained";
        Assert.True(JsonNode.DeepEquals(window.Copy.Conflict, restored));
        var candidate = await window.Capture.ReadC1CandidateOwnerImagesAsync(window.Lease, interval);
        Assert.True(JsonNode.DeepEquals(expectedRoot, JsonNode.Parse(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(candidate[ResourceMaterializationContract.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(candidate[ResourceMaterializationContract.HistoryPath].Bytes!)));
        AssertPressureZeroC1ParentsAndInventory(window, inventoryBefore, candidate);
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson,
            prefix.StateJson, expectedRoot["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((1, 1, 1), (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.True(after.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(after.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts with { ResourceBatchComputations = 2 }, window.Capture.ReadExistingPressureGroupComputationCounts());

        // Do not call ReadWoundOpportunity or manufacture a none decision for a comparison-only source.
        var offered = await window.Capture.ComposeC1FirstOfferAsync(window.Lease, interval);
        Assert.Empty(offered.Issues);
        Assert.Null(offered.Pending);
        Assert.Null(offered.ProposedInstanceRow);
        var checkpoint = await window.Capture.ComposeC2FirstCheckpointDraftAsync(window.Lease, interval);
        Assert.Empty(checkpoint.Issues);
        Assert.Null(checkpoint.Checkpoint);
        Assert.Null(checkpoint.Pending);
        var transport = await window.Capture.CommitC2FirstTransportAsync(window.Lease, interval);
        Assert.Empty(transport.Issues);
        Assert.Equal("no_offer", transport.Disposition);
        Assert.Null(transport.Pending);
        Assert.Null(transport.Checkpoint);
        Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease));
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Copy.Context.FileSystem, window.Lease, out _));
        return interval;
    }

    /// <summary>Retains exact old max0 diagnosis and frozen pre-revoke state; no live query is made on a revoked owner.</summary>
    private Task AssertPressureZeroRefusalObservationAsync(PressureGroupWindow window, IReadOnlyList<ValidationIssue> issues,
        ValidationService.SpiritualPressureGroupComputationCounts sourceCounts)
    {
        var issue = Assert.Single(issues);
        Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("actual existing pressure requires a positive hypothetical actual maximum", issue.Actual);
        var retained = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservationResult>(
            window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Null(retained.UnavailableReason);
        var observed = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservation>(retained.Observation);
        AssertPressureOrdinaryFrontiersEqual(window.Frontier, observed.Frontier);
        Assert.Equal(new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0), observed.Projections);
        Assert.NotNull(retained.Issues);
        var frozen = Assert.Single(retained.Issues);
        Assert.Equal((issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code,
            issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint),
            (frozen.FilePath, frozen.Severity, frozen.Message, frozen.Category, frozen.Code,
                frozen.Actor, frozen.Section, frozen.Expected, frozen.Actual, frozen.RepairHint));
        Assert.Equal(JsonSerializer.Serialize(issue.RepairTargetFiles), frozen.RepairTargetFilesJson);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((1, 1, 0), (counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(window.Copy.Context.FileSystem, window.Lease, out _));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "pressure_zero_pre_revoke_observation", window.Spec.Name, issues,
            observed.Frontier, observed.Projections, retained.UnavailableReason,
            queries = counts.Queries, signedParentReads = counts.SignedParentReads,
            ordinaryEvaluations = counts.OrdinaryEvaluations, actualEvaluations = counts.ActualEvaluations,
            freshInputChecks = counts.FreshInputChecks, freshDependencyChecks = counts.FreshDependencyChecks,
            sourceCounts, physicalInputInert = true, preRevocationFrontierInert = true,
            terminalOwnerRevoked = true, downstreamSourceC1NoOffer = "UNREACHED"
        }));
        return Task.CompletedTask;
    }

    /// <summary>Checks all eight v3 fields against pre-sign parent DATA, exact raw ordinary input and literal calculation2/max0.</summary>
    private static void AssertPressureZeroIssuedComparison(PressureGroupWindow window,
        ValidationService.PreparedSpiritualSource source, JsonObject expectedOrdinary, string expectedParentJson)
    {
        var proof = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject();
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parentWoundJsonBase64", "version" },
            proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(3, proof["version"]!.GetValue<int>());
        Assert.Equal("existing_pressure_ordinary_v1", proof["inputRoleRevision"]!.GetValue<string>());
        var input = proof["originalInput"]!.AsObject();
        Assert.Equal(new[] { "contentFingerprint", "exchangePointer", "existed", "path" },
            input.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, input["path"]!.GetValue<string>());
        Assert.True(input["existed"]!.GetValue<bool>());
        Assert.Equal(new CanonicalBeforeImage(true, window.Files[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
            input["contentFingerprint"]!.GetValue<string>());
        Assert.Equal("/activeConflict/exchangeLog/0", input["exchangePointer"]!.GetValue<string>());
        var ordinaryJson = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(proof["ordinaryExchangeJsonBase64"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, JsonNode.Parse(ordinaryJson)));
        Assert.Equal(SpiritualWoundStateJson.Canonical(expectedOrdinary), ordinaryJson);
        var calculation = proof["ordinaryCalculation"]!.AsObject();
        Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" }, calculation.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal((2L, 0), (calculation["traumaPressure"]!.GetValue<long>(), calculation["maximumSeverityRank"]!.GetValue<int>()));
        var parentJson = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(proof["parentWoundJsonBase64"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedParentJson), JsonNode.Parse(parentJson)));
        var parent = WoundMaterializationContract.Parse(parentJson, "test.pressureZero.issuedParent");
        Assert.True(parent.IsValid, string.Join("\n", parent.Issues));
        Assert.Equal(parentJson, WoundMaterializationContract.SerializeCanonical(parent.Wound!));
        Assert.Equal("wound_retained_pressure_owner_1", parent.Wound!.WoundId);
        Assert.Equal(PressureGroupParentRank(window.Spec, 1), parent.Wound.Severity.Rank);
        Assert.Equal(Assert.Single(parent.Wound.Consequences.OwnedEffectSources.RootBindings).EffectId, proof["effectId"]!.GetValue<string>());
        Assert.Equal("component_001", proof["componentId"]!.GetValue<string>());
    }

    /// <summary>Checks complete C1 parent/History/identity preservation and unchanged registered before-images.</summary>
    private static void AssertPressureZeroC1ParentsAndInventory(PressureGroupWindow window,
        ValidationService.SpiritualC1ImageInventory before, IReadOnlyDictionary<string, CanonicalBeforeImage> candidate)
    {
        var after = window.Capture.ReadC1ImageInventory(window.Lease);
        Assert.Equal(before.RegisteredPaths, after.RegisteredPaths);
        Assert.Equal(before.BeforeImages.Keys.OrderBy(path => path, StringComparer.Ordinal), after.BeforeImages.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var path in before.RegisteredPaths)
        {
            Assert.True(before.BeforeImages.ContainsKey(path), path);
            Assert.Equal(before.BeforeImages[path].Existed, after.BeforeImages[path].Existed);
            Assert.Equal(before.BeforeImages[path].Bytes, after.BeforeImages[path].Bytes);
            Assert.Equal(before.BeforeImages[path].Fingerprint, after.BeforeImages[path].Fingerprint);
        }
        var profiles = JsonNode.Parse(candidate[AfterlifeEntityProfileState.StatePath].Bytes!)!;
        var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Assert.Single(window.ExpectedParentJson)), Assert.Single(player["activeWounds"]!.AsArray())));
        foreach (var path in new[] { AfterlifeEntityProfileState.StatePath, WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath, EffectIdentityState.StatePath })
        {
            var baseline = window.Files.Single(row => row.Key.Replace('\\', '/') == path).Value;
            var expectedRoot = JsonNode.Parse(baseline)!.DeepClone();
            if (path == AfterlifeEntityProfileState.StatePath)
            {
                Assert.False(expectedRoot.AsObject().ContainsKey("schemaVersion"));
                expectedRoot["schemaVersion"] = 1;
                var expectedProfiles = expectedRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
                Assert.Equal(2, expectedProfiles.Count);
                var guardian = Assert.Single(expectedProfiles, row =>
                    row!["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == "guardian_frame")!.AsObject();
                Assert.False(guardian.ContainsKey("activeWounds"));
                Assert.False(guardian.ContainsKey("activeEffects"));
                guardian["activeWounds"] = new JsonArray();
                guardian["activeEffects"] = new JsonArray();
            }
            Assert.True(candidate[path].Existed);
            Assert.True(JsonNode.DeepEquals(expectedRoot, JsonNode.Parse(candidate[path].Bytes!)), path);
        }
    }
}
