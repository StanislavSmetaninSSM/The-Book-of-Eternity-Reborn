using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Complete N3/N4 provenance survives the actual/C1, first C2, explicit cold Open, none/C3, common C4 and signed47 journey.</summary>
    /// <param name="parents">Independent retained count; the fourth parent remains required after saturation.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public Task SpiritualPressureMultiParent_ColdNonePublishesEveryParentAndSignedNext(int parents) =>
        WithPressureMultiParentFailureAsync("multi_parent_publication_" + parents, () =>
            WithPressureGroupPublicationAsync(new("multi_parent_publication_" + parents, Parents: parents,
                After: "strained", AdvanceExpected: true), async setup =>
            {
                var ordinary = SpiritualPressureMultiParentData.OrdinaryCalculation;
                var completed = await CompletePressureGroupColdC3Async(setup, setup.Copy.Context.Validator, ordinary);
                await PublishPressureGroupAndReadNextAsync(setup, setup.Copy.Context.Validator, ordinary, completed);
            }));

    /// <summary>The saturated fourth parent remains a fresh dependency after cold and warm ordinary DATA are available.</summary>
    /// <param name="history">Change the last full History anchor instead of deleting the last current wound.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SpiritualPressureMultiParent_WarmFinalParentDriftRefusesActual(bool history) =>
        WithPressureGroupCaseAsync(new("multi_parent_final_drift_" + history, Parents: 4, After: "strained",
            AdvanceExpected: true), async window =>
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
            Assert.Equal(sources.CurrentBatch, sources.SignedBatch);
            Assert.Equal(1, sources.ResourceBatchComputations);
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
                var wounds = player["activeWounds"]!.AsArray();
                Assert.Equal(4, wounds.Count);
                Assert.Equal("wound_retained_pressure_owner_4", wounds[3]!["woundId"]!.GetValue<string>());
                wounds.RemoveAt(3);
            }
            var changed = Encoding.UTF8.GetBytes(changedRoot.ToJsonString());
            Assert.False(oldBytes.AsSpan().SequenceEqual(changed));
            await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, changed);
            var drifted = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
            var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
            Assert.Null(refused.Step);
            AssertPressurePairInputDriftIssue(refused.Issues, path);
            AssertPressurePairNoNewComputations(window, warm, sources);
            await AssertPressurePairTerminalAfterInputDriftAsync(window, path, oldBytes, drifted);
        });

    /// <summary>A foreign lease or disposed owner never becomes permission to reuse detached four-parent DATA.</summary>
    [Fact]
    public Task SpiritualPressureMultiParent_ForeignLeaseAndDisposedOwnerRefuse() =>
        AssertPressureGroupForeignLeaseAndDisposedOwnerAsync(4);

    /// <summary>Complete three-parent strain capture cannot silently remove a fourth pressure-cost-only source.</summary>
    [Fact]
    public Task SpiritualPressureMultiParent_AdditionalPressureSourceRefusesActual() =>
        WithPressureGroupCaseAsync(new("multi_parent_other_pressure_source", Parents: 3, After: "strained",
            PlayerBefore: 4, PlayerCost: 4, OtherCost: true), window =>
            AssertPressureGroupActualGuardAsync(window, "actual existing pressure excludes another pressure wound source"));

    /// <summary>Changed original conflict or signed request bytes revoke an already admitted complete group before C3.</summary>
    /// <param name="parents">The request drift is N3; saturated conflict drift is N4.</param>
    /// <param name="path">One exact original input whose physical bytes remain authoritative.</param>
    [Theory]
    [InlineData(3, LiveTurnPreparationService.TurnRequestPath)]
    [InlineData(4, AfterlifeSpiritualConflictState.StatePath)]
    public Task SpiritualPressureMultiParent_AdmittedRawDriftRevokes(int parents, string path) =>
        WithPressureGroupCaseAsync(new("multi_parent_admitted_drift_" + parents, Parents: parents, After: "strained",
            AdvanceExpected: true), async window =>
        {
            await AssertPressureOwnedGroupActualAdmissionAsync(window, SpiritualPressureMultiParentData.OrdinaryCalculation,
                "Complete multiparent admission must succeed before raw-drift qualification.", multiParent: true);
            await AssertPressurePairCompletionInputDriftAsync(window, path);
        });

    /// <summary>A third parent cannot make a failed ordinary roll check cacheable or remove the selected sibling.</summary>
    [Fact]
    public Task SpiritualPressureMultiParent_MissingRollFailureReevaluates() =>
        AssertPressureGroupMissingRollReevaluatesAsync(3);

    /// <summary>One saturated four-parent C4 cut witnesses all three real committed writes, exact rollback and fresh same-byte retry.</summary>
    [Fact]
    public Task SpiritualPressureMultiParent_LateCutRestoresSignedOriginAndFreshRetry() =>
        WithPressureMultiParentFailureAsync("multi_parent_four_rollback", () =>
            AssertPressureGroupLatePublicationCutAsync(new("multi_parent_four_rollback", Parents: 4,
                After: "strained", AdvanceExpected: true)));
}
