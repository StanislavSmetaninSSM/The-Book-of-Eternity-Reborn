using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>All new rank configurations share one cold ordinary computation and warm DATA reuse while freshness checks continue.</summary>
    [Theory]
    [InlineData(3, 4, "fractured", 13L, 2)]
    [InlineData(3, 4, "overwhelmed", 18L, 3)]
    [InlineData(4, 3, "fractured", 13L, 2)]
    [InlineData(4, 3, "overwhelmed", 18L, 3)]
    [InlineData(4, 4, "fractured", 13L, 2)]
    [InlineData(4, 4, "overwhelmed", 18L, 3)]
    public Task SpiritualPressureGroup_RankDomainColdWarmComparison(int first, int second, string after, long trauma, int maximum) =>
        WithPressureGroupCaseAsync(new($"group_rank_{first}_{second}_{after}", After: after, Ranks: new(first, second)), async window =>
        {
            var cold = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, cold);
            Assert.Equal((trauma, maximum), (cold.Comparison!.OrdinaryCalculation.TraumaPressure,
                cold.Comparison.OrdinaryCalculation.MaximumSeverityRank));
            var beforeWarm = window.Capture.ReadExistingStrainReuseCounts();
            var sourceCounts = window.Capture.ReadExistingPressureGroupComputationCounts();
            Assert.Equal((2, 1, 0), (beforeWarm.SignedParentReads, beforeWarm.OrdinaryEvaluations, beforeWarm.ActualEvaluations));
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sourceCounts.CurrentBatch);
            Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), sourceCounts.SignedBatch);
            Assert.Equal(1, sourceCounts.ResourceBatchComputations);
            cold.Comparison.ReadOrdinaryExchange()["summary"] = "Изменена только отделённая копия.";
            var warm = await ReadPressureGroupComparisonAsync(window);
            AssertPressureGroupComparison(window, warm);
            Assert.Equal(cold.Comparison.OrdinaryCalculation, warm.Comparison!.OrdinaryCalculation);
            var afterWarm = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal((2, 1, 0), (afterWarm.SignedParentReads, afterWarm.OrdinaryEvaluations, afterWarm.ActualEvaluations));
            Assert.Equal(sourceCounts, window.Capture.ReadExistingPressureGroupComputationCounts());
            Assert.True(afterWarm.FreshInputChecks > beforeWarm.FreshInputChecks);
            Assert.True(afterWarm.FreshDependencyChecks > beforeWarm.FreshDependencyChecks);
        });

    /// <summary>The shared group extension must not admit a lone IV parent through either comparison or the actual singleton issuer.</summary>
    [Fact]
    public Task SpiritualPressureGroup_SingleRankFourCannotCompareOrAdvance() =>
        WithPressureGroupCaseAsync(new("group_single_rank_four", Parents: 1, Ranks: new(4, 3)), async window =>
        {
            await AssertPressureGroupSelectionRefusalAsync(window);
            var before = window.Capture.ReadExistingStrainReuseCounts();
            var result = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
            Assert.Null(result.Step);
            Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", Assert.Single(result.Issues).Code);
            var after = window.Capture.ReadExistingStrainReuseCounts();
            Assert.Equal((0, 0), (after.OrdinaryEvaluations, after.ActualEvaluations));
            Assert.Equal((before.OrdinaryEvaluations, before.ActualEvaluations), (after.OrdinaryEvaluations, after.ActualEvaluations));
            AssertExistingPressureInputInert(window.Copy.Context, window.Lease, window.Files, window.Generation);
        });

    /// <summary>Two IV parents never authorize selecting a subset from three complete matching strain sources.</summary>
    [Fact]
    public Task SpiritualPressureGroup_RankFourPairAndThirdStrainRefuseCompleteSet() =>
        WithPressureGroupCaseAsync(new("group_four_four_third", Parents: 3, Ranks: new(4, 4)),
            AssertPressureGroupSelectionRefusalAsync);

    /// <summary>A third pressure cost-only wound remains a disqualifying other source even beside an otherwise eligible IV pair.</summary>
    [Fact]
    public Task SpiritualPressurePair_RankFourPairAndThirdCostRefuseActual() =>
        WithPressureGroupCaseAsync(new("pair_four_four_third_cost", PlayerBefore: 4, PlayerCost: 4,
            OtherCost: true, Ranks: new(4, 4)), window =>
            AssertPressureGroupActualGuardAsync(window, "actual existing pressure excludes another pressure wound source"));

    /// <summary>A full IV pressure art prohibition survives isolation of the two selected strain rows.</summary>
    [Fact]
    public Task SpiritualPressureGroup_RankFourPressureArtRemainsForbidden() =>
        WithPressureGroupCaseAsync(new("group_rank_four_forbidden_art", Ranks: new(3, 4), RankFourPressureArt: true), async window =>
        {
            var result = await ReadPressureGroupComparisonAsync(window);
            Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
            Assert.Equal("spiritual_wound_art_forbidden", Assert.Single(result.Issues).Code);
            Assert.Equal((1, 0), (window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations,
                window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations));
        });

    /// <summary>IV costs remain magnitude3, not III's2; a shape-valid payment5 cannot satisfy base3 plus the IV burden3.</summary>
    [Fact]
    public Task SpiritualPressureGroup_RankFourPressureCostCannotUseRankThreeMagnitude() =>
        WithPressureGroupCaseAsync(new("group_rank_four_wrong_cost", PlayerBefore: 6, PlayerCost: 5,
            SelectedCost: true, Ranks: new(3, 4)), async window =>
        {
            var result = await ReadPressureGroupComparisonAsync(window);
            Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(("afterlife_conflict_action_cost_mismatch", "baseCost=3, minCost=1, effectiveCost=6",
                "baseCost=3, minCost=1, effectiveCost=5"), (issue.Code, issue.Expected, issue.Actual));
            Assert.Equal((1, 0), (window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations,
                window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations));
        });

    /// <summary>An IV pressure-roll sibling still requires complete disadvantage evidence instead of being removed with strain.</summary>
    [Fact]
    public Task SpiritualPressureGroup_RankFourPressureRollStillRequiresAudit() =>
        WithPressureGroupCaseAsync(new("group_rank_four_missing_roll", PressureRoll: true, Ranks: new(3, 4)), async window =>
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var result = await ReadPressureGroupComparisonAsync(window);
                Assert.Null(result.Comparison); Assert.False(result.PrefixBlocked);
                var issue = Assert.Single(result.Issues);
                Assert.Equal(("spiritual_wound_roll_hindrance_missing", "activeConflict.exchangeLog[0].diceAudit.rollMode.player"),
                    (issue.Code, issue.FilePath));
                Assert.Equal((attempt, 0), (window.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations,
                    window.Capture.ReadExistingStrainReuseCounts().ActualEvaluations));
            }
        });

    /// <summary>Actual IV pair projection preserves the IV pressure-cost3 and complete three-die disadvantage audit.</summary>
    [Fact]
    public Task SpiritualPressurePair_RankFourSelectedCostAndRollRemainApplicable() =>
        WithPressureGroupCaseAsync(new("pair_rank_four_selected_siblings", PlayerBefore: 6, PlayerCost: 6,
            SelectedCost: true, PressureRoll: true, Ranks: new(3, 4), AdvanceExpected: true,
            CompletePressureRollAudit: true), async window =>
        {
            var admitted = await AssertPressurePairSuccessfulActualPremiseAsync(window);
            Assert.Equal(6m, Assert.Single(admitted.Prefix.AppliedTransitions,
                row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor).AppliedAmount);
        });

    /// <summary>A complete IV second parent cannot disappear physically after a warm comparison and retain actual authority.</summary>
    [Fact]
    public Task SpiritualPressurePair_WarmRankFourParentDeletionRejectsActual() =>
        AssertPressurePairWarmParentDeletionAsync(new("pair_actual_delete_rank_four", Ranks: new(3, 4)));

    /// <summary>Changed IV physical/source/History bytes and changed raw ordinary input revoke before actual reuse or publication.</summary>
    [Theory]
    [InlineData(AfterlifeEntityProfileState.StatePath)]
    [InlineData(SpiritualWoundOpportunityReceiptState.StatePath)]
    [InlineData(WoundHistoryState.HistoryPath)]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)]
    public Task SpiritualPressurePair_RankFourWarmInputDriftRevokesBeforeActual(string path) =>
        WithPressureGroupCaseAsync(new("pair_rank_four_drift_" + Path.GetFileNameWithoutExtension(path), Ranks: new(3, 4)), async window =>
        {
            await AssertPressurePairOrdinaryPremiseAsync(window, PairActualOrdinaryTwoCalculation());
            var counts = window.Capture.ReadExistingStrainReuseCounts();
            var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
            var original = (await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
            byte[] changed;
            if (path == AfterlifeEntityProfileState.StatePath)
            {
                var profiles = JsonNode.Parse(original)!;
                var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
                    row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
                var parent = Assert.Single(player["activeWounds"]!.AsArray(), row =>
                    row!["woundId"]!.GetValue<string>() == "wound_retained_pressure_owner_2")!;
                Assert.Equal(4, parent["severity"]!["rank"]!.GetValue<int>());
                parent["display"]!["description"] = "Иное полное описание IV раны.";
                var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "integration.pair.changedIV");
                Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
                changed = Encoding.UTF8.GetBytes(profiles.ToJsonString());
            }
            else if (path == SpiritualWoundOpportunityReceiptState.StatePath)
            {
                var receipt = JsonNode.Parse(original)!;
                var source = receipt["sources"]![1]!["witness"]!.AsObject();
                var exchange = SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
                    SpiritualWoundStateJson.DecodeJsonObjectBytes(source["exchangeJsonBase64"])));
                exchange["summary"] = "Иное полное описание исторического источника IV раны.";
                source["exchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));
                SpiritualExistingPressureTestData.Seal(source);
                Assert.Equal(4, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain).MaximumSeverityRank);
                Assert.Null(strain);
                var decision = receipt["decisions"]![1]!.AsObject();
                decision["sourceWitness"] = source.DeepClone();
                decision["sourceId"] = source["sourceId"]!.DeepClone();
                decision["sourceFingerprint"] = source["sourceFingerprint"]!.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
                changed = Encoding.UTF8.GetBytes(receipt.ToJsonString());
            }
            else if (path == WoundHistoryState.HistoryPath)
            {
                var history = JsonNode.Parse(original)!;
                Assert.Equal("wound_retained_pressure_owner_2", history["transitions"]![1]!["woundId"]!.GetValue<string>());
                history["transitions"]![1]!["sourceFingerprint"] = "sha256:" + new string('f', 64);
                var parsed = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
                Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
                changed = Encoding.UTF8.GetBytes(history.ToJsonString());
            }
            else changed = original.Concat(Encoding.UTF8.GetBytes(" \n")).ToArray();
            await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
            var edited = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
            var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
            Assert.Null(refused.Step); AssertPressurePairInputDriftIssue(refused.Issues, path);
            AssertPressurePairNoNewComputations(window, counts, sources);
            await AssertPressurePairTerminalAfterInputDriftAsync(window, path, original, edited);
        });

    /// <summary>Detached rank-domain comparison DATA cannot authorize an actual operation with a foreign or disposed owner/lease.</summary>
    [Fact]
    public Task SpiritualPressurePair_RankFourForeignLeaseAndDisposedOwnerCannotAdvance() =>
        WithPressureGroupCaseAsync(new("pair_rank_four_owner_a", Ranks: new(3, 4)), async a =>
            await WithPressureGroupCaseAsync(new("pair_rank_four_owner_b", Ranks: new(4, 4)), async b =>
            {
                await AssertPressurePairOrdinaryPremiseAsync(a, PairActualOrdinaryTwoCalculation());
                await AssertPressurePairOrdinaryPremiseAsync(b, PairActualOrdinaryTwoCalculation());
                var countsA = a.Capture.ReadExistingStrainReuseCounts();
                var countsB = b.Capture.ReadExistingStrainReuseCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await a.Capture.AdvanceNextResourceExchangeAsync(b.Lease));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await b.Capture.AdvanceNextResourceExchangeAsync(a.Lease));
                Assert.Equal(countsA, a.Capture.ReadExistingStrainReuseCounts());
                Assert.Equal(countsB, b.Capture.ReadExistingStrainReuseCounts());
                await AssertPressureGroupWindowInertAsync(a); await AssertPressureGroupWindowInertAsync(b);
                a.Capture.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await a.Capture.AdvanceNextResourceExchangeAsync(a.Lease));
                AssertExistingPressureInputInert(a.Copy.Context, a.Lease, a.Files, a.Generation);
                Assert.True(b.Capture.IsCurrentOwner);
                AssertPressureGroupComparison(b, await ReadPressureGroupComparisonAsync(b));
                Assert.Equal(countsB.OrdinaryEvaluations, b.Capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations);
            }));

    /// <summary>III+IV boundary uses the full real C1/cold/none/C3/common-C4/signed47 journey.</summary>
    [Fact]
    public Task SpiritualPressurePair_ThreeFourBoundaryPublishesWithColdNoneAndSignedNext() =>
        PublishPressureRankCaseAsync(new("pair_three_four_boundary_publication", Ranks: new(3, 4), AdvanceExpected: true));

    /// <summary>IV+III canonical-ID order remains distinct from severity order through real capped publication.</summary>
    [Fact]
    public Task SpiritualPressurePair_FourThreeOverflowPublishesWithColdNoneAndSignedNext() =>
        PublishPressureRankCaseAsync(new("pair_four_three_overflow_publication", After: "overwhelmed", Ranks: new(4, 3), AdvanceExpected: true));

    /// <summary>The all-IV boundary is an actual publication premise, not inferred from a cheap mixed-pair Cartesian matrix.</summary>
    [Fact]
    public Task SpiritualPressurePair_FourFourBoundaryPublishesWithColdNoneAndSignedNext() =>
        PublishPressureRankCaseAsync(new("pair_four_four_boundary_publication", Ranks: new(4, 4), AdvanceExpected: true));

    /// <summary>The all-IV overflow retains the ordinary18/III input and caps at actual23/IV through signed47.</summary>
    [Fact]
    public Task SpiritualPressurePair_FourFourOverflowPublishesWithColdNoneAndSignedNext() =>
        PublishPressureRankCaseAsync(new("pair_four_four_overflow_publication", After: "overwhelmed", Ranks: new(4, 4), AdvanceExpected: true));

    /// <summary>Threads exact pre-sign ranks through the established publication helper and its complete equality/receipt oracles.</summary>
    private Task PublishPressureRankCaseAsync(PressureGroupCase spec) => WithPressurePairPublicationAsync(spec, async setup =>
    {
        var ordinary = spec.After == "fractured" ? PairActualOrdinaryTwoCalculation() :
            new SpiritualWoundCalculation(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3);
        var completed = await CompletePressurePairColdC3Async(setup, setup.Copy.Context.Validator, ordinary);
        await PublishPressurePairAndReadNextAsync(setup, setup.Copy.Context.Validator, ordinary, completed);
    });
}
