using System.Text.Json;
using System.Text.Json.Nodes;
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
        var input = bundle.Input; var prepared = bundle.PreparedPlan;
        var accepted = bundle.EffectBatchPlan; var finalPlan = bundle.FinalPlan;
        var frame = input.Binding; var effectPlan = accepted.EffectPlan; var effectInput = accepted.EffectInput;
        var applications = accepted.ApplicationResults;
        var bytes = await fixture.CaptureCandidateInvocationBeforeImagesAsync();
        Assert.NotNull(prepared.JointRepairContinuationAuthority);
        Assert.Null(prepared.TreatmentContinuationAuthority);
        Assert.True(WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle));
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var terminal));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared,
            effectPlan, out var stages));
        Assert.Equal(2, stages.Count);
        Assert.Equal(4, stages[0].Before.Severity.Rank); Assert.Equal(1, stages[0].After.Severity.Rank);
        Assert.Equal("healed", stages[1].After.Lifecycle);
        var originalRoot = input.PreTurnHistory;
        var finalRoot = finalPlan.HistoryAfterImage;
        var original = WoundHistoryState.Parse(originalRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(original.IsValid, JsonSerializer.Serialize(original.Issues));
        var history = WoundHistoryState.Parse(finalRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        var originalRows = originalRoot["transitions"]!.AsArray();
        var finalRows = finalRoot["transitions"]!.AsArray();
        Assert.Equal(originalRows.Count + 3, finalRows.Count);
        for (var index = 0; index < originalRows.Count; index++)
            Assert.True(JsonNode.DeepEquals(originalRows[index], finalRows[index]), "original joint History prefix row " + index);
        var rows = history.State!.Transitions.Skip(originalRows.Count).ToArray();
        Assert.Equal(new[] { "create", "treat", "heal" }, rows.Select(row => row.Kind));
        Assert.Equal(original.State!.NextOrdinal + 3, history.State.NextOrdinal);
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(original.State.NextOrdinal + index, rows[index].Ordinal);
            Assert.Equal(frame.Turn, rows[index].Turn);
            Assert.Contains(rows[index].TransitionId, prepared.AllocatedTransitionIds);
        }
        Assert.Equal(Assert.Single(input.Transitions).OperationKey, rows[0].OperationKey);
        Assert.Contains(frame.AcceptedEvents, row => row.EventRef == rows[0].EventRef);
        Assert.Equal(1, rows[0].WoundTransitionOrdinal);
        var healIntent = Assert.IsType<MortalWoundHealOutcomeIntent>(terminal.Resolution.OutcomeIntents[^1]);
        Assert.Equal(terminal.Request.Coordinates.WoundId, rows[1].WoundId);
        Assert.Equal(terminal.Request.Coordinates.OperationKey, rows[1].OperationKey);
        Assert.Equal(terminal.Request.Coordinates.EventRef, rows[1].EventRef);
        Assert.Equal(healIntent.HealChildCoordinates.OperationKey, rows[2].OperationKey);
        Assert.Equal(healIntent.HealChildCoordinates.EventRef, rows[2].EventRef);
        for (var index = 0; index < stages.Count; index++)
        {
            var row = rows[index + 1]; var stage = stages[index];
            Assert.Equal(terminal.Outcome.Before.LastTransition.Ordinal + index + 1, row.WoundTransitionOrdinal);
            Assert.Equal(stage.After.LastTransition.TransitionId, row.TransitionId);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.Before), row.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.After), row.AfterFingerprint);
            Assert.Equal(terminal.Request.RequestFingerprint, row.SourceFingerprint); Assert.Equal(index == 1, row.Terminal);
        }
        Assert.NotEqual(rows[0].WoundId, rows[1].WoundId); Assert.Equal(rows[1].WoundId, rows[2].WoundId);
        Assert.Equal(rows[1].AfterFingerprint, rows[2].BeforeFingerprint);
        Assert.Equal(rows[1].WoundTransitionOrdinal + 1, rows[2].WoundTransitionOrdinal);
        Assert.Equal(rows[0].Ordinal + 1, rows[1].Ordinal); Assert.Equal(rows[1].Ordinal + 1, rows[2].Ordinal);
        var finalCarriers = input.PreTurnCarriers;
        foreach (var contribution in finalPlan.CarrierContributions)
        {
            var collection = WoundCarrierCollectionAuthority.Resolve(finalCarriers, contribution.Owner);
            foreach (var mutation in contribution.Mutations)
                Assert.Null(AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, contribution.Owner, mutation));
        }
        var finalCatalog = WoundCarrierCatalog.Build(finalCarriers); Assert.Empty(finalCatalog.Issues);
        var finalIdentity = WoundIdentityState.Parse(finalPlan.IdentityIndexAfterImage.ToJsonString(), WoundIdentityState.StatePath);
        Assert.True(finalIdentity.IsValid, JsonSerializer.Serialize(finalIdentity.Issues));
        Assert.Empty(history.State.ValidateAgreement(finalIdentity.State!, finalCatalog));
        Assert.True(finalIdentity.State!.TryGetEntry(terminal.Request.Coordinates.WoundId, out var healed));
        Assert.Equal("healed", healed.Status); Assert.Equal(rows[2].TransitionId, healed.TerminalTransitionId);
        Assert.Equal(2, terminal.Legacies.EffectOperationBatches.Count);
        var acquisitionRefs = prepared.EffectOperationBatches[0].RootApplications.Select(row => row.ApplicationRef).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(acquisitionRefs);
        var withoutAcquisition = applications.Where(row => !acquisitionRefs.Contains(row.ApplicationRef)).ToArray();
        Assert.Equal(applications.Count - acquisitionRefs.Count, withoutAcquisition.Length);
        var incomplete = WoundEffectBatchAcceptedPlan.Create(prepared, effectInput, effectPlan,
            withoutAcquisition, accepted.TerminationResults);
        var rejected = WoundAcceptedTurnPlannerCore.Finalize(prepared, new(incomplete, Array.Empty<ValidationIssue>()));
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Issues, issue => issue.Code == "mortal_treatment_execution_invalid");
        var output = WoundPlayerNotification.ComposeAcceptedTurn(bundle, TreatmentNarration + " " + AcquisitionNarration);
        Assert.True(output.Success, JsonSerializer.Serialize(output.Issues));
        Assert.Contains(output.Notifications, row => row.Text.PlainText.StartsWith("Исцелена рана:", StringComparison.Ordinal));
        Assert.All(output.Notifications, row => Assert.Equal("/раны", row.DetailCommand));
        foreach (var row in bytes) Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
    }
}
