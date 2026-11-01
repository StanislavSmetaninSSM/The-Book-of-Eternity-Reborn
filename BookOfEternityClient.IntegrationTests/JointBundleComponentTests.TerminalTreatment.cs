using System.Text.Json;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    [Fact]
    public async Task GenuineJointTerminalTreatmentKeepsAcquisitionAndLinkedHealStages()
    {
        await using var fixture = new Fixture(_output, nameof(GenuineJointTerminalTreatmentKeepsAcquisitionAndLinkedHealStages),
            passiveTimingEvents: true, gzipRecords: true);
        await fixture.InitializeAsync(terminalTreatment: true);
        var bundle = await fixture.PrepareJointStagesAsync();
        var bytes = await fixture.CaptureCandidateInvocationBeforeImagesAsync();
        Assert.NotNull(bundle.PreparedPlan.JointRepairContinuationAuthority);
        Assert.Null(bundle.PreparedPlan.TreatmentContinuationAuthority);
        Assert.True(WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle));
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(bundle.PreparedPlan, out var terminal));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(bundle.PreparedPlan,
            bundle.EffectBatchPlan.EffectPlan, out var stages));
        Assert.Equal(2, stages.Count);
        Assert.Equal(4, stages[0].Before.Severity.Rank); Assert.Equal(1, stages[0].After.Severity.Rank);
        Assert.Equal("healed", stages[1].After.Lifecycle);
        var history = WoundHistoryState.Parse(bundle.FinalPlan.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        var rows = history.State!.Transitions.Where(row => row.Turn == bundle.Input.Binding.Turn).ToArray();
        Assert.Equal(new[] { "create", "treat", "heal" }, rows.Select(row => row.Kind));
        Assert.NotEqual(rows[0].WoundId, rows[1].WoundId); Assert.Equal(rows[1].WoundId, rows[2].WoundId);
        Assert.Equal(rows[1].AfterFingerprint, rows[2].BeforeFingerprint);
        Assert.Equal(rows[1].WoundTransitionOrdinal + 1, rows[2].WoundTransitionOrdinal);
        Assert.Equal(rows[0].Ordinal + 1, rows[1].Ordinal); Assert.Equal(rows[1].Ordinal + 1, rows[2].Ordinal);
        Assert.Equal(2, terminal.Legacies.EffectOperationBatches.Count);
        var accepted = bundle.EffectBatchPlan;
        var acquisitionRefs = bundle.PreparedPlan.EffectOperationBatches[0].RootApplications.Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(acquisitionRefs);
        var withoutAcquisition = accepted.ApplicationResults.Where(row => !acquisitionRefs.Contains(row.ApplicationRef)).ToArray();
        Assert.Equal(accepted.ApplicationResults.Count - acquisitionRefs.Count, withoutAcquisition.Length);
        var incomplete = WoundEffectBatchAcceptedPlan.Create(bundle.PreparedPlan, accepted.EffectInput, accepted.EffectPlan,
            withoutAcquisition, accepted.TerminationResults);
        var rejected = WoundAcceptedTurnPlannerCore.Finalize(bundle.PreparedPlan, new(incomplete, Array.Empty<ValidationIssue>()));
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Issues, issue => issue.Code == "mortal_treatment_execution_invalid");
        var output = WoundPlayerNotification.ComposeAcceptedTurn(bundle, TreatmentNarration + " " + AcquisitionNarration);
        Assert.True(output.Success, JsonSerializer.Serialize(output.Issues));
        Assert.Contains(output.Notifications, row => row.Text.PlainText.StartsWith("Исцелена рана:", StringComparison.Ordinal));
        Assert.All(output.Notifications, row => Assert.Equal("/раны", row.DetailCommand));
        foreach (var row in bytes) Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
    }
}
