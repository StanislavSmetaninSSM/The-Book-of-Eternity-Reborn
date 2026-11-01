using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task StandaloneTerminalCopyCandidateRetainsGuardsWithoutReassemblingFinalGraphs()
    {
        const string candidateName = "DetachedTerminalCopyCandidateForComponent";
        var productionSources = Directory.EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories);
        Assert.Equal(1, productionSources.Sum(path => File.ReadAllText(path).Split(candidateName + "(", StringSplitOptions.None).Length - 1));
        var timings = new OperationTimingAccumulator();
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true,
            physicalMode: "guaranteed", hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings },
            preparePhysicalWound: wound => PrepareTerminalPhysicalWound(wound, "guaranteed", removeThenHeal: false));
        var files = fixture.Files;
        Assert.True(fixture.Authority.HasCurrentAdmissionAuthority());
        Assert.Equal(1, fixture.Authority.CurrentWound.Severity.Rank);
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(fixture.Authority.ReadGuidedTreatmentOriginalInput()).Frame;
        var admitted = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame);
        var request = admitted.Request;
        var reduced = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request,
            new WoundHistoryParseResult(admitted.Owner.History, []), admitted.Owner.CurrentWound, admitted.Owner);
        Assert.Equal("Resolved", reduced.Disposition);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(reduced.Resolution);
        Assert.Equal("AcceptedTerminal", resolution.AttemptDisposition);
        Assert.IsType<MortalWoundHealOutcomeIntent>(resolution.OutcomeIntents[^1]);
        var composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(files, fixture.Lease,
            new GameResponse(), admitted.Owner, request, resolution);
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        var source = Assert.IsType<AcceptedMechanicsWoundStageBundle>(composed.Plan!.WoundStageBundle);
        var input = source.Input; var prepared = source.PreparedPlan; var effect = source.EffectBatchPlan; var final = source.FinalPlan;
        Assert.NotNull(prepared.TreatmentContinuationAuthority); Assert.Null(prepared.JointRepairContinuationAuthority);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var terminal));
        Assert.Empty(terminal.Legacies.LegacyDraftBindings);
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared, effect.EffectPlan, out var stages));
        Assert.Equal(2, stages.Count); Assert.Equal("healed", stages[^1].After.Lifecycle);
        var expected = JointBundleComponentTests.ReadDetachedBundleForCopyControl(source);

        // Aggregate-only small-operation controls: no broad ambient context during setup/Compose.
        var reference = Observe("TerminalCopy.FullConstructorReference", () => new AcceptedMechanicsWoundStageBundle(input, prepared, effect, final));
        var candidate = Observe("TerminalCopy.DirectCandidate", source.DetachedTerminalCopyCandidateForComponent);
        var second = Observe("TerminalCopy.CandidateOfCandidate", candidate.Bundle.DetachedTerminalCopyCandidateForComponent);
        foreach (var result in new[] { reference, candidate, second })
        {
            Assert.NotSame(source, result.Bundle);
            Assert.Equal(expected, JointBundleComponentTests.ReadDetachedBundleForCopyControl(result.Bundle));
            Assert.True(Count(result.Snapshot, "WoundPlanCache.ValidatePreparedResult") > 0);
            Assert.True(Count(result.Snapshot, "WoundEffect.AcceptEffectResult") > 0);
            Assert.True(Count(result.Snapshot, "WoundPlanner.FinalizationHandoff") > 0);
        }
        Assert.NotSame(candidate.Bundle, second.Bundle);
        foreach (var bundle in new[] { source, candidate.Bundle, second.Bundle })
            JointBundleComponentTests.MutateDetachedBundleForCopyControl(bundle);
        foreach (var bundle in new[] { source, reference.Bundle, candidate.Bundle, second.Bundle })
            Assert.Equal(expected, JointBundleComponentTests.ReadDetachedBundleForCopyControl(bundle));
        Assert.True(Count(reference.Snapshot, "WoundPlanCache.FinalizationReplay") > 0);
        Assert.True(Count(reference.Snapshot, "WoundPlanner.AssembleWoundContributions") > 0);
        // Intentional causal RED: dormant candidate still delegates to real full replay/assembly.
        Assert.Equal(0, Count(candidate.Snapshot, "WoundPlanner.AssembleWoundContributions"));
        Assert.Equal(0, Count(second.Snapshot, "WoundPlanner.AssembleWoundContributions"));
        Assert.Equal(0, Count(candidate.Snapshot, "WoundPlanCache.FinalizationReplay"));
        Assert.Equal(0, Count(second.Snapshot, "WoundPlanCache.FinalizationReplay"));

        (AcceptedMechanicsWoundStageBundle Bundle, OperationTimingAccumulator.Snapshot Snapshot) Observe(
            string name, Func<AcceptedMechanicsWoundStageBundle> operation)
        {
            using var window = timings.StartWindow();
            AcceptedMechanicsWoundStageBundle value;
            using (files.EnterOperationTimingContext())
            using (timings.Measure(name)) value = operation();
            window.Dispose();
            var snapshot = window.Capture();
            Assert.True(snapshot.Complete); Assert.Equal(0, snapshot.OpenScopes); Assert.Equal(0, snapshot.LateCompletions);
            output.WriteLine("TERMINAL_COPY_MEASUREMENT " + JsonSerializer.Serialize(new { Name = name, snapshot.ElapsedMilliseconds, snapshot.Rows }));
            return (value, snapshot);
        }
        static long Count(OperationTimingAccumulator.Snapshot snapshot, string name) =>
            snapshot.Rows.SingleOrDefault(row => row.Name == name)?.Started ?? 0;
    }
}
