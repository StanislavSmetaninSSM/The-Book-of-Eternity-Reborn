using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Actual joint stages allocate only the new CREATE, for terminal deletion and ordinary treatment alike.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JointCanonicalAnchorsRequireExactSealedUnionAndAllocateOnlyNewCreate(bool terminal)
    {
        await using var fixture = new Fixture(_output,
            nameof(JointCanonicalAnchorsRequireExactSealedUnionAndAllocateOnlyNewCreate) + (terminal ? "-terminal" : "-nonterminal"),
            passiveTimingEvents: true, gzipRecords: true);
        await fixture.InitializeAsync(terminalTreatment: terminal);
        var bundle = await fixture.PrepareJointStagesAsync();
        var beforeImages = await fixture.CaptureCandidateInvocationBeforeImagesAsync();
        var beforeBundle = fixture.CaptureCandidateBundleView(bundle);
        var prepared = bundle.PreparedPlan;
        var final = bundle.FinalPlan;
        Assert.NotNull(prepared.JointRepairContinuationAuthority);
        Assert.True(WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle));
        Assert.True(WoundAcceptedTurnPlanner.TryReadJointRepairFinal(prepared, out var joint));
        Assert.Equal(terminal, joint.Outcome.PreparesTerminalHeal);
        await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(fixture.Files, fixture.Lease,
            prepared.JointRepairContinuationAuthority!);
        var currentMinute = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(await fixture.Files.ReadFileAsync(
            fixture.Lease, EffectAcceptedTurnInputComposer.WorldTimePath));
        Assert.True(currentMinute.HasValue);
        Assert.Equal(WoundAcceptedTurnPlanner.ReadJointRepairAcceptedState(prepared.JointRepairContinuationAuthority!).CurrentGameMinute,
            currentMinute.Value);

        var contribution = Assert.Single(final.CarrierContributions);
        var mutations = contribution.Mutations.ToArray();
        Assert.Equal(2, mutations.Length);
        var acquisition = Assert.Single(mutations, row => row.Operation == "add" && row.BeforeWound is null);
        var created = Assert.IsType<WoundMaterializationEnvelope>(acquisition.AfterWound);
        var treatment = Assert.Single(mutations, row => row.WoundId == joint.Outcome.Before.WoundId);
        var oldBefore = Assert.IsType<WoundMaterializationEnvelope>(treatment.BeforeWound);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(joint.Outcome.Before),
            WoundMaterializationContract.SerializeCanonical(oldBefore));
        Assert.NotEqual(oldBefore.WoundId, created.WoundId);
        Assert.Equal("physical", created.Classification.Domain);
        Assert.Equal("active", created.Lifecycle);
        Assert.Equal("create", created.LastTransition.Kind);
        Assert.Equal(prepared.AllocatedWoundIds[0], created.WoundId);
        Assert.Equal(prepared.AllocatedTransitionIds[0], created.LastTransition.TransitionId);
        Assert.Null(created.Recovery.RecoveryAnchor);
        Assert.Null(created.Recovery.DeteriorationAnchor);

        string? healTransitionId = null;
        if (terminal)
        {
            Assert.Equal("delete", treatment.Operation);
            Assert.Null(treatment.AfterWound);
            Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var terminalView));
            Assert.Same(prepared.JointRepairContinuationAuthority, terminalView.Continuation);
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(oldBefore),
                WoundMaterializationContract.SerializeCanonical(terminalView.Outcome.Before));
            var healIntent = Assert.IsType<MortalWoundHealOutcomeIntent>(terminalView.Outcome.ReadTerminalHealIntent());
            var heal = healIntent.HealChildCoordinates;
            healTransitionId = heal.TransitionId;
            Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared,
                bundle.EffectBatchPlan.EffectPlan, out var stages));
            Assert.Equal(2, stages.Count);
            Assert.Equal(joint.Outcome.TransitionId, stages[0].After.LastTransition.TransitionId);
            Assert.Equal(heal.TransitionId, stages[1].After.LastTransition.TransitionId);
            Assert.Equal("healed", stages[1].After.Lifecycle);
            Assert.Equal("healed", stages[1].After.Care.State);
            var history = WoundHistoryState.Parse(final.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
            var oldRows = history.State!.Transitions.Where(row => row.WoundId == oldBefore.WoundId).TakeLast(2).ToArray();
            Assert.Equal(new[] { joint.Outcome.TransitionId, heal.TransitionId }, oldRows.Select(row => row.TransitionId));
            var last = oldRows[1];
            Assert.Equal("heal", last.Kind);
            Assert.True(last.Terminal);
            Assert.Equal(heal.OperationKey, last.OperationKey);
            Assert.Equal(heal.EventRef, last.EventRef);
            Assert.Equal(prepared.Binding.Turn, last.Turn);
            Assert.Equal(oldBefore.LastTransition.Ordinal + 2, last.WoundTransitionOrdinal);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stages[1].Before), last.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stages[1].After), last.AfterFingerprint);
            var identity = WoundIdentityState.Parse(final.IdentityIndexAfterImage.ToJsonString(), WoundIdentityState.StatePath);
            Assert.True(identity.IsValid, JsonSerializer.Serialize(identity.Issues));
            Assert.True(identity.State!.TryGetEntry(oldBefore.WoundId, out var healed));
            Assert.Equal("healed", healed.Status);
            Assert.Equal(heal.TransitionId, healed.TerminalTransitionId);
        }
        else
        {
            Assert.Equal("update", treatment.Operation);
            var after = Assert.IsType<WoundMaterializationEnvelope>(treatment.AfterWound);
            Assert.Equal(joint.Outcome.TransitionId, after.LastTransition.TransitionId);
            Assert.Equal("active", after.Lifecycle);
        }

        // Every altered graph is detached and independently resealed from the same genuine private stages.
        // A constructor refusal is reported as such; it is never counted as reaching the anchor API.
        var refusals = new List<string>();
        refusals.Add(RequireAlteredAnchorBundleRefusal(fixture, bundle, "extra-union",
            [new(contribution.Owner, contribution.ExpectedWoundCollectionFingerprint, mutations.Append(treatment).ToArray())]));
        refusals.Add(RequireAlteredAnchorBundleRefusal(fixture, bundle, "missing-old-treatment",
            [new(contribution.Owner, contribution.ExpectedWoundCollectionFingerprint,
                mutations.Where(row => row.WoundId != oldBefore.WoundId).ToArray())]));
        var foreignBefore = oldBefore with { Display = oldBefore.Display with { Description = oldBefore.Display.Description + " Чужой исходный образ." } };
        var parsedForeign = WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(foreignBefore), "anchor.foreignBefore");
        Assert.True(parsedForeign.IsValid, JsonSerializer.Serialize(parsedForeign.Issues));
        Assert.NotEqual(WoundMaterializationContract.SerializeCanonical(oldBefore), WoundMaterializationContract.SerializeCanonical(foreignBefore));
        var changedBefore = new WoundCarrierMutation(treatment.Operation, treatment.WoundId, foreignBefore, treatment.AfterWound);
        refusals.Add(RequireAlteredAnchorBundleRefusal(fixture, bundle, "foreign-before",
            [new(contribution.Owner, contribution.ExpectedWoundCollectionFingerprint,
                mutations.Select(row => row.WoundId == oldBefore.WoundId ? changedBefore : row).ToArray())]));
        if (terminal)
        {
            var changedHistory = final.HistoryAfterImage;
            var healRow = Assert.Single(changedHistory["transitions"]!.AsArray().OfType<JsonObject>(),
                row => row["transitionId"]!.GetValue<string>() == healTransitionId);
            const string changedOperation = "foreign_terminal_heal_operation";
            healRow["operationKey"] = changedOperation;
            healRow["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(changedOperation,
                healRow["eventRef"]!.GetValue<string>(), healRow["readableSummary"]!.GetValue<string>());
            var parsedHistory = WoundHistoryState.Parse(changedHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.True(parsedHistory.IsValid, JsonSerializer.Serialize(parsedHistory.Issues));
            refusals.Add(RequireAlteredAnchorBundleRefusal(fixture, bundle, "changed-heal-history", final.CarrierContributions, changedHistory));
        }
        Assert.Equal(beforeBundle, fixture.CaptureCandidateBundleView(bundle));
        var anchors = MortalWoundCanonicalAnchorPlan.CreateJointRepair(fixture.Files, fixture.Lease, bundle);
        Assert.Equal(beforeBundle, fixture.CaptureCandidateBundleView(bundle));
        foreach (var image in beforeImages)
            Assert.Equal(image.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, image.Key));
        _output.WriteLine(JsonSerializer.Serialize(new { terminal, Refusals = refusals }));
        // Intended terminal causal RED is only this actual acceptance oracle, after all checked premises.
        Assert.True(anchors.Success, JsonSerializer.Serialize(anchors.Issues));
        // These allocation assertions are UNREACHED on the old terminal refusal; they require actual GREEN.
        var plan = Assert.IsType<MortalWoundCanonicalAnchorPlan>(anchors.Plan);
        Assert.True(plan.AgreesWith(bundle));
        Assert.Equal(currentMinute.Value, plan.CurrentTimeInMinutes);
        var allocation = Assert.Single(plan.Allocations);
        Assert.Equal(created.WoundId, allocation.WoundId);
        Assert.Equal(created.LastTransition.TransitionId, allocation.TransitionId);
        Assert.Equal("creation", allocation.RecoveryAnchor.AnchorKind);
        Assert.Equal(currentMinute.Value, allocation.RecoveryAnchor.AnchorMinute);
        Assert.Equal(created.LastTransition.TransitionId, allocation.RecoveryAnchor.AnchorTransitionId);
        if (allocation.DeteriorationAnchor is { } deterioration)
        {
            Assert.Equal(currentMinute.Value, deterioration.AnchorMinute);
            Assert.Equal(created.LastTransition.TransitionId, deterioration.AnchorTransitionId);
        }
        Assert.False(plan.TryGetAllocation(oldBefore.WoundId, out _));
    }

    /// <summary>Uses actual detached constructors/replay and then the actual allocator if construction succeeds.</summary>
    /// <param name="fixture">The unchanged genuine stage owner and current lease.</param>
    /// <param name="bundle">The genuine original bundle, never mutated by this control.</param>
    /// <param name="label">A fixed safe variant name.</param>
    /// <param name="contributions">One independent altered union.</param>
    /// <param name="history">An independent altered History or null for the original detached History.</param>
    /// <returns>The real first refusing boundary for this variant.</returns>
    private static string RequireAlteredAnchorBundleRefusal(Fixture fixture, AcceptedMechanicsWoundStageBundle bundle,
        string label, IReadOnlyList<WoundCarrierContribution> contributions, JsonObject? history = null)
    {
        var prepared = bundle.PreparedPlan;
        var effects = bundle.EffectBatchPlan;
        var original = bundle.FinalPlan;
        var index = original.IdentityIndexAfterImage;
        var rows = history ?? original.HistoryAfterImage;
        var intents = original.TransitionIntents;
        var fingerprint = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, effects, contributions, index, rows, intents);
        var final = new WoundAcceptedTurnPlan(original.Binding, original.BindingFingerprint, original.InputFingerprint,
            original.WoundPreparationFingerprint, original.EffectInputFingerprint, original.EffectAcceptedTurnPlanFingerprint,
            fingerprint, original.AllocatedWoundIds, original.AllocatedTransitionIds, contributions, index, rows, intents);
        AcceptedMechanicsWoundStageBundle? altered = null;
        var failure = Record.Exception(() => altered = new(bundle.Input, prepared, effects, final));
        if (failure is not null)
        {
            var refusal = Assert.IsType<ArgumentException>(failure);
            Assert.Equal("finalPlan", refusal.ParamName);
            return label + ":bundle-constructor";
        }
        Assert.NotNull(altered);
        var refused = MortalWoundCanonicalAnchorPlan.CreateJointRepair(fixture.Files, fixture.Lease, altered!);
        Assert.False(refused.Success, label);
        Assert.Null(refused.Plan);
        Assert.NotEmpty(refused.Issues);
        return label + ":anchor:" + string.Join(",", refused.Issues.Select(issue => issue.Code ?? "uncoded-refusal"));
    }
}
