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
    /// Proves the real ordinal-zero ordinary owner premises before Advance. Only the exact closed
    /// singleton count guard enters the baseline refusal branch; both branches share the success oracle.
    /// Complete signed/current envelopes, roots and first-create History/receipt joins are preflighted
    /// by WithPressureGroupCaseAsync. These parents are retained DATA, not two executed historical births.
    /// </summary>
    private async Task AssertPressurePairActualAdmissionAsync(PressureGroupWindow window,
        SpiritualWoundCalculation expectedOrdinaryCalculation, string successMessage)
    {
        Assert.Equal(new PressureGroupCase(window.Spec.Name, After: window.Spec.After, Ranks: window.Spec.Ranks,
            AdvanceExpected: true), window.Spec);
        Assert.Equal(2, window.Spec.Parents);
        var expectedOrdinary = ReadPressurePairAdmissionExchange(window.Spec.After);
        var expectedActual = ReadPressurePairAdmissionExchange("broken");
        var expectedActualCalculation = new SpiritualWoundCalculation(
            new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4);
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
        AssertPressureGroupComparison(window, comparisonResult);
        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(comparisonResult.Comparison);
        Assert.Equal(expectedOrdinaryCalculation, comparison.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        var counts = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((before.Queries + 1, 2, 1, 0),
            (counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.True(counts.FreshInputChecks > before.FreshInputChecks);
        Assert.True(counts.FreshDependencyChecks > before.FreshDependencyChecks);
        var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sourceCounts.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sourceCounts.SignedBatch);
        Assert.Equal(1, sourceCounts.ResourceBatchComputations);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        await window.Capture.RequestExistingPressureActualRefusalObservationAsync(window.Lease);
        Assert.Null(window.Capture.ReadExistingPressureActualRefusalObservation());
        Assert.Equal(counts, window.Capture.ReadExistingStrainReuseCounts());
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        AssertPressureOrdinaryFrontiersEqual(window.Frontier,
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));

        var advanced = await MeasurePressureOwnerPhaseAsync("pair_actual_admission", window.Spec.Name,
            () => window.Capture.AdvanceNextResourceExchangeAsync(window.Lease));
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(window.Copy.Context.FileSystem, window.Lease));
        var admitted = advanced.Issues.Count == 0 &&
            advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
        if (!admitted)
        {
            Assert.Null(advanced.Step);
            await AssertPressurePairBaselineCountRefusalAsync(window, Assert.Single(advanced.Issues), sourceCounts);
        }

        // This is deliberately unconditional: future GREEN must never require count refusal/revocation.
        Assert.True(admitted, successMessage);
        AssertNoConflictFrameErrors(advanced.Issues);
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
        Assert.Equal(new[] { 4, 12 }, closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", "player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Null(source.GuaranteedSeverityRank);
        Assert.Null(source.RetraumaWoundId);
        Assert.Equal(expectedActualCalculation, source.Calculation);
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        AssertPressurePairV4Source(window, source, expectedOrdinaryCalculation, expectedOrdinary);

        var prefix = window.Capture.ReadClosedResourceCandidatePrefix(window.Lease, interval);
        var expectedRoot = window.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone();
        expectedRoot["activeConflict"]!["playerSideStrain"] = "broken";
        var companion = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
        Assert.True(JsonNode.DeepEquals(expectedRoot, companion));
        // The complete literal exchange and this whole-root restoration permit exactly these two JSON paths.
        var restored = companion.DeepClone().AsObject();
        Assert.Equal("broken", restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("broken", restored["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
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
        AssertPressurePairC1ParentsAndInventory(window, originalInventory, candidate);
        var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson,
            prefix.StateJson, expectedRoot["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(projected.Issues);
        Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
        var after = window.Capture.ReadExistingStrainReuseCounts();
        Assert.Equal((2, 1, 1), (after.SignedParentReads, after.OrdinaryEvaluations, after.ActualEvaluations));
        Assert.True(after.FreshInputChecks > counts.FreshInputChecks);
        Assert.True(after.FreshDependencyChecks > counts.FreshDependencyChecks);
        Assert.Equal(sourceCounts with { ResourceBatchComputations = 2 },
            window.Capture.ReadExistingPressureGroupComputationCounts());
        // The real C1 writer performs fresh continuation probes after this exact admission checkpoint.
        await AssertPressurePairFirstOfferAsync(window, interval, source, expectedOrdinaryCalculation, expectedOrdinary);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
    }

    /// <summary>Checks the real issuer's complete v4 payload against independent pre-sign parent and ordinary DATA.</summary>
    private static void AssertPressurePairV4Source(PressureGroupWindow window,
        ValidationService.PreparedSpiritualSource source, SpiritualWoundCalculation expectedOrdinaryCalculation,
        JsonObject expectedOrdinary)
    {
        var encoded = Assert.IsType<string>(source.StrainPreimageJson);
        var proof = JsonNode.Parse(encoded)!.AsObject();
        Assert.Equal(new[] { "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parents", "version" },
            proof.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(4, proof["version"]!.GetValue<int>());
        Assert.Equal("existing_pressure_ordinary_pair_v1", proof["inputRoleRevision"]!.GetValue<string>());
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
        Assert.Equal(2, window.ExpectedParentJson.Count);
        var rows = proof["parents"]!.AsArray();
        Assert.Equal(2, rows.Count);
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

    /// <summary>Checks only the known initial count refusal, inert pre-revocation frontier and terminal cleanup.</summary>
    private async Task AssertPressurePairBaselineCountRefusalAsync(PressureGroupWindow window, ValidationIssue issue,
        ValidationService.SpiritualPressureGroupComputationCounts sourceCounts)
    {
        Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("actual existing pressure requires exactly one comparison parent", issue.Actual);
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
        Assert.Equal((2, 1, 0), (counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations));
        Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
        Assert.False(window.Capture.IsCurrentOwner);
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        AssertPressureNoPrivatePair(window.Copy.Context, window.Lease);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.ReadExistingPressureOrdinaryProbeStateAsync(window.Lease));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await window.Capture.ReadExistingPressureOrdinaryComparisonAsync(window.Lease));
        AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        window.Capture.Dispose();
        Assert.Same(retained, window.Capture.ReadExistingPressureActualRefusalObservation());
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "pair_actual_baseline_count_refusal",
            window.Spec.Name, ordinary = window.Spec.After, issue.Code, issue.Actual,
            ordinaryEvaluations = counts.OrdinaryEvaluations, actualEvaluations = counts.ActualEvaluations,
            sourceCounts, physicalInputInert = true, preRevocationFrontierInert = true, terminalOwnerRevoked = true }));
    }

    /// <summary>Fixed complete independent exchange oracle; only the explicitly chosen literal strain varies.</summary>
    private static JsonObject ReadPressurePairAdmissionExchange(string strain) => JsonNode.Parse($$$$"""
        {"exchangeId":"exchange_pressure_group_46","turnNumber":46,"exchangeAtTurn":46,
         "operationType":"pressure","outcome":"setback","summary":"Душа встречает давление Хранителя.",
         "before":{"playerSideStrain":"clear","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
         "after":{"playerSideStrain":"{{{{strain}}}}","oppositionSideStrain":"clear","conflictPosition":"contested","controlState":null,"tempoAdvantage":null},
         "incomingAction":{"operationType":"pressure","actorType":"guardian","actorId":"guardian_frame","summary":"Хранитель оказывает давление."},
         "matchupAudit":{"playerOperation":"pressure","oppositionOperation":"pressure","primaryResolutionLane":"pressure","riskProfile":"offensive_pressure","matchupRationale":"Встречное давление."},
         "actionCostAudit":{
           "player":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":10},
           "opposition":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":3,"after":0,"max":9}},
         "diceAudit":{"formulaVersion":"afterlife_spiritual_conflict_v1","diceSource":"input/turn_request.json.preGeneratedDices1d20",
           "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":4},{"side":"opposition","sourceIndex":1,"sides":20,"value":12}],
           "playerTotal":4,"oppositionTotal":12,"margin":-8,"outcomeBand":"decisive_opposition_success","modifierBreakdown":{"player":[],"opposition":[]}}}
        """)!.AsObject();
}
