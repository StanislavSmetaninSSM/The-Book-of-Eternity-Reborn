using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>A lawful detached N3 subset cannot replace the current signed N4 set or its final immutable History anchor.</summary>
    /// <param name="history">Drift the final History anchor instead of the final current wound.</param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task SpiritualGroupZero_WarmFinalParentDriftRefusesActual(bool history) =>
        WithPairLowFailureAsync("group_zero_final_drift_" + history, () =>
            WithPressureGroupCaseAsync(GroupZeroCase("group_zero_final_drift_" + history, GroupZeroPressureContour.Capped), async window =>
            {
                AssertPressureGroupComparison(window, await ReadPressureGroupComparisonAsync(window));
                var cold = window.Capture.ReadExistingStrainReuseCounts(); var sources = window.Capture.ReadExistingPressureGroupComputationCounts();
                AssertPressureGroupComparison(window, await ReadPressureGroupComparisonAsync(window));
                var warm = window.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal((4, 1, 0), (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
                Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
                    (warm.SignedParentReads, warm.OrdinaryEvaluations, warm.ActualEvaluations));
                Assert.Equal(sources, window.Capture.ReadExistingPressureGroupComputationCounts());
                Assert.True(warm.FreshInputChecks > cold.FreshInputChecks); Assert.True(warm.FreshDependencyChecks > cold.FreshDependencyChecks);
                var path = history ? WoundHistoryState.HistoryPath : AfterlifeEntityProfileState.StatePath;
                var original = (await window.Copy.Context.FileSystem.ReadFileBytesAsync(window.Lease, path))!;
                var changed = JsonNode.Parse(original)!;
                if (history)
                {
                    var last = changed["transitions"]![3]!; Assert.Equal("wound_retained_pressure_owner_4", last["woundId"]!.GetValue<string>());
                    last["afterFingerprint"] = "sha256:" + new string('f', 64);
                    Assert.True(WoundHistoryState.Parse(changed.ToJsonString(), path).IsValid);
                }
                else
                {
                    var player = Assert.Single(changed[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row => row!["actorType"]!.GetValue<string>() == "player_soul")!;
                    var wounds = player["activeWounds"]!.AsArray(); Assert.Equal(4, wounds.Count);
                    Assert.Equal("wound_retained_pressure_owner_4", wounds[3]!["woundId"]!.GetValue<string>()); wounds.RemoveAt(3);
                }
                var bytes = Encoding.UTF8.GetBytes(changed.ToJsonString()); Assert.False(original.AsSpan().SequenceEqual(bytes));
                await window.Copy.Context.FileSystem.WriteFileAtomicBytesAsync(window.Lease, path, bytes);
                var edited = CaptureExactSpiritualFiles(window.Copy.Context.FileSystem.GameSessionPath);
                var refused = await window.Capture.AdvanceNextResourceExchangeAsync(window.Lease);
                Assert.Null(refused.Step); AssertPressurePairInputDriftIssue(refused.Issues, path);
                AssertPressurePairNoNewComputations(window, warm, sources);
                await AssertPressurePairTerminalAfterInputDriftAsync(window, path, original, edited);
            }));

    /// <summary>Real N4 actual-zero admission cannot hide exact raw conflict or original-request drift at completion.</summary>
    /// <param name="path">Exact physical original conflict or request input.</param>
    [Theory]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)] [InlineData(LiveTurnPreparationService.TurnRequestPath)]
    public Task SpiritualGroupZero_AdmittedRawDriftRevokes(string path) =>
        WithPairLowFailureAsync("group_zero_raw_drift_" + Path.GetFileNameWithoutExtension(path), () =>
            WithPressureGroupCaseAsync(GroupZeroCase("group_zero_raw_drift_" + Path.GetFileNameWithoutExtension(path), GroupZeroPressureContour.Capped), async window =>
            {
                _ = await AssertGroupZeroActualAdmissionAsync(window);
                await AssertPressurePairCompletionInputDriftAsync(window, path);
            }));

    /// <summary>Only this leaf needs two independent live contexts to prove foreign leases and disposed ownership cannot cross.</summary>
    [Fact]
    public Task SpiritualGroupZero_ForeignLeaseAndDisposedOwnerRefuse() =>
        WithPressureGroupCaseAsync(GroupZeroCase("group_zero_foreign_a", GroupZeroPressureContour.Capped), async a =>
            await WithPressureGroupCaseAsync(GroupZeroCase("group_zero_foreign_b", GroupZeroPressureContour.Capped), async b =>
            {
                Assert.NotEqual(a.Copy.Generation, b.Copy.Generation);
                var beforeA = a.Capture.ReadExistingStrainReuseCounts(); var beforeB = b.Capture.ReadExistingStrainReuseCounts();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await a.Capture.ReadExistingPressureOrdinaryComparisonAsync(b.Lease));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await b.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                Assert.Equal(beforeA, a.Capture.ReadExistingStrainReuseCounts()); Assert.Equal(beforeB, b.Capture.ReadExistingStrainReuseCounts());
                AssertPressureGroupComparison(a, await ReadPressureGroupComparisonAsync(a));
                AssertPressureGroupComparison(b, await ReadPressureGroupComparisonAsync(b));
                await AssertPressureGroupWindowInertAsync(a); await AssertPressureGroupWindowInertAsync(b);
                a.Capture.Dispose(); Assert.False(a.Capture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await a.Capture.ReadExistingPressureOrdinaryComparisonAsync(a.Lease));
                AssertExistingPressureInputInert(a.Copy.Context, a.Lease, a.Files, a.Generation);
                var beforeWarm = b.Capture.ReadExistingStrainReuseCounts();
                AssertPressureGroupComparison(b, await ReadPressureGroupComparisonAsync(b));
                var afterWarm = b.Capture.ReadExistingStrainReuseCounts();
                Assert.Equal((beforeWarm.SignedParentReads, beforeWarm.OrdinaryEvaluations, beforeWarm.ActualEvaluations),
                    (afterWarm.SignedParentReads, afterWarm.OrdinaryEvaluations, afterWarm.ActualEvaluations));
                Assert.True(afterWarm.FreshInputChecks > beforeWarm.FreshInputChecks); Assert.True(afterWarm.FreshDependencyChecks > beforeWarm.FreshDependencyChecks);
                await AssertPressureGroupWindowInertAsync(b);
            }));

    /// <summary>Genuine C3 may register only to prove common pre-take refusal leaves edited bytes intact without committed writes.</summary>
    [Fact]
    public Task SpiritualGroupZero_PreTakeDriftPreservesAuthoredInput()
    {
        var armed = false; var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path => { if (armed) writes.Add(path); return Task.CompletedTask; }
        };
        var spec = GroupZeroCase("group_zero_pre_take", GroupZeroPressureContour.Low);
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            var context = setup.Copy.Context;
            _ = await CompleteGroupZeroSourceOnlyC3Async(setup, registerPublication: true);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var backups = await ReadSpiritualC4BackupsAsync(context);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? authority = null;
            IReadOnlyDictionary<string, byte[]>? edited = null; byte[]? generation = null;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out authority));
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                await AppendPressureRawWhitespaceAsync(context, lease);
                edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), edited.Keys.OrderBy(path => path, StringComparer.Ordinal));
                foreach (var (path, bytes) in before.Where(row => row.Key.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath)) Assert.Equal(bytes, edited[path]);
            });
            armed = true;
            try
            {
                var error = await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
                Assert.Contains("spiritual_c4_publication_input_changed", error.Message); Assert.Contains(AfterlifeSpiritualConflictState.StatePath, error.Message);
            }
            finally { armed = false; }
            Assert.Empty(writes);
            await ExecuteGuardianLeaseStagesAsync(context, lease =>
            {
                AssertExistingPressureInputInert(context, lease, edited!, generation!);
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority!, out _));
                AssertPressureNoPrivatePair(context, lease); AssertExistingPressureInputInert(context, lease, edited!, generation!);
                return Task.CompletedTask;
            });
        }, hooks));
    }
}
