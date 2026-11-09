using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Complete capped N2/N3 retains every parent through actual/C1, first C2, explicit cold Open, authenticated none/C3, common C4 and signed47.</summary>
    /// <param name="parents">Only independent N2/N3 normal publication qualification.</param>
    [Theory]
    [InlineData(2)] [InlineData(3)]
    public Task SpiritualCappedGroupPressure_ColdNonePublishesEveryParentAndSignedNext(int parents)
    {
        if (parents is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(parents));
        var spec = new PressureGroupCase("capped_group_publication_" + parents, Parents: parents, After: "broken",
            AdvanceExpected: true, CappedOrdinary: true);
        return WithPressureMultiParentFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            var completed = await CompletePressureGroupColdC3Async(setup, setup.Copy.Context.Validator, SpiritualCappedGroupPressureData.Calculation);
            await PublishPressureGroupAndReadNextAsync(setup, setup.Copy.Context.Validator, SpiritualCappedGroupPressureData.Calculation, completed);
        }));
    }

    /// <summary>Every parent remains a fresh dependency when ordinary and actual are already equal at IV, including the final saturated N4 parent.</summary>
    /// <param name="history">Change the final immutable History anchor instead of removing the last current wound.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SpiritualCappedGroupPressure_WarmFinalParentDriftRefusesActual(bool history) =>
        WithPressureMultiParentFailureAsync("capped_group_final_drift_" + history, () =>
            WithPressureGroupCaseAsync(new("capped_group_final_drift_" + history, Parents: 4, After: "broken",
                AdvanceExpected: true, CappedOrdinary: true), async window =>
            {
                AssertPressureGroupComparison(window, await ReadPressureGroupComparisonAsync(window));
                var cold = window.Capture.ReadExistingStrainReuseCounts();
                var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
                AssertPressureGroupComparison(window, await ReadPressureGroupComparisonAsync(window));
                var warm = window.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal((4, 1, 0), (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
                Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
                    (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
                Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 4), sources.CurrentBatch);
                Assert.Equal(sources.CurrentBatch, sources.SignedBatch); Assert.Equal(1, sources.ResourceBatchComputations);
                Assert.Equal(sources, window.Capture.ReadExistingPressureGroupComputationCounts());
                Assert.True(warm.FreshInputChecks > cold.FreshInputChecks);
                Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
                var path = history ? WoundHistoryState.HistoryPath : AfterlifeEntityProfileState.StatePath;
                var oldBytes = (await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
                var changedRoot = JsonNode.Parse(oldBytes)!;
                if (history)
                {
                    var last = changedRoot["transitions"]![3]!;
                    Assert.Equal("wound_retained_pressure_owner_4", last["woundId"]!.GetValue<string>());
                    last["afterFingerprint"] = "sha256:" + new string('f', 64);
                    var parsed = WoundHistoryState.Parse(changedRoot.ToJsonString(), path);
                    Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
                }
                else
                {
                    var player = Assert.Single(changedRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(),
                        row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
                    var wounds = player["activeWounds"]!.AsArray(); Assert.Equal(4, wounds.Count);
                    Assert.Equal("wound_retained_pressure_owner_4", wounds[3]!["woundId"]!.GetValue<string>());
                    wounds.RemoveAt(3);
                }
                var changed = Encoding.UTF8.GetBytes(changedRoot.ToJsonString()); Assert.False(oldBytes.AsSpan().SequenceEqual(changed));
                await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
                var drifted = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
                var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
                Assert.Null(refused.Step); AssertPressurePairInputDriftIssue(refused.Issues, path);
                AssertPressurePairNoNewComputations(window, warm, sources);
                await AssertPressurePairTerminalAfterInputDriftAsync(window, path, oldBytes, drifted);
            }));

    /// <summary>Equal capped ordinary/actual semantics do not hide raw conflict or signed-request byte drift after genuine N4 actual/C1 admission.</summary>
    /// <param name="path">Exact source input whose physical bytes remain authoritative through completion and C3.</param>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)] [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualCappedGroupPressure_AdmittedRawDriftRevokes(string path) =>
        WithPressureMultiParentFailureAsync("capped_group_admitted_drift_" + Path.GetFileNameWithoutExtension(path), () =>
            WithPressureGroupCaseAsync(new("capped_group_admitted_drift_" + Path.GetFileNameWithoutExtension(path),
                Parents: 4, After: "broken", AdvanceExpected: true, CappedOrdinary: true), async window =>
            {
                await AssertPressureOwnedGroupActualAdmissionAsync(window, SpiritualCappedGroupPressureData.Calculation,
                    "A complete capped N4 actual/C1 success is required before raw-drift qualification.", multiParent: true);
                await AssertPressurePairCompletionInputDriftAsync(window, path);
            }));

    /// <summary>Two genuinely separate capped N4 owners do not exchange lease authority or revive a disposed comparison owner.</summary>
    [Fact]
    public Task SpiritualCappedGroupPressure_ForeignLeaseAndDisposedOwnerRefuse() =>
        WithPressureMultiParentFailureAsync("capped_group_foreign_disposed", () =>
            AssertPressureGroupForeignLeaseAndDisposedOwnerAsync(4, cappedOrdinary: true));

    /// <summary>Run only after both normal publications pass: one real N4 late committed-write cut restores exact signed origin, retires old owners and retries identical bytes once.</summary>
    [Fact]
    public Task SpiritualCappedGroupPressure_LateCutRestoresSignedOriginAndFreshRetry() =>
        WithPressureMultiParentFailureAsync("capped_group_four_rollback", () =>
            AssertPressureGroupLatePublicationCutAsync(new("capped_group_four_rollback", Parents: 4, After: "broken",
                AdvanceExpected: true, CappedOrdinary: true)));
}
