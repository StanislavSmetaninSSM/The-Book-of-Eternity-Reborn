using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Immutable checked completed effect DATA reused only for this cold owner's common-plan comparison.</summary>
    /// <param name="CarrierAfterImagesJson">Complete independently checked completed carrier map.</param>
    /// <param name="IdentityAfterJson">Complete checked identity map with only exact owner-end trigger appends.</param>
    /// <param name="PayloadFingerprint">The checked real typed plan's full effect payload fingerprint.</param>
    private sealed record GuardianGroupZeroCompletedEffectData(string CarrierAfterImagesJson,
        string IdentityAfterJson, string PayloadFingerprint);

    /// <summary>Checks both complete retained guardian parents and every root/sibling after actual completion, allowing only exact owner-end trigger chronology and identity appends.</summary>
    /// <param name="setup">Independent signed originals, full authored pre-owner inventory and both complete retained graphs.</param>
    /// <param name="effects">The single real typed effect plan returned by actual ordinary completion.</param>
    /// <param name="resources">The same completed resource result with genuine executions and boundary transcript.</param>
    /// <param name="interval">The already owned single actual interval; no additional source or trigger is executed.</param>
    /// <returns>Immutable independently checked carrier/identity/payload DATA for the final common-plan copy check.</returns>
    private GuardianGroupZeroCompletedEffectData AssertGuardianGroupZeroCompletedEffects(GuardianGroupZeroPressureSetup setup,
        EffectAcceptedTurnPlan effects, AcceptedMechanicsResourcePlanningResult resources,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
    {
        // Each detached map is read once from the real typed result, then reused by the complete oracle and diagnostics.
        var beforeCarriers = effects.CarrierBeforeImages;
        var afterCarriers = effects.CarrierAfterImages;
        var beforeIdentity = effects.IdentityIndexBeforeImage;
        var afterIdentity = effects.IdentityIndexAfterImage;
        var baselines = effects.AcceptedCarrierBaselines;
        var activeEffects = effects.ActiveEffects;
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(resources.EffectBoundaryTranscript);
        var executions = resources.ResourceTriggerExecutions;
        var accepted = transcript.AcceptedActivations;
        var eventInput = effects.EventInput;
        var originalProfiles = ReadGuardianAttributionJson(setup.AuthoredFiles[AfterlifeEntityProfileState.StatePath]);
        var originalIdentity = ReadGuardianAttributionJson(setup.AuthoredFiles[EffectIdentityState.StatePath]);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_completed_effect_operands", setup.Name,
            independentParents = setup.Data.Retained.Parents, independentRoots = setup.Data.Retained.Effects,
            signedProfiles = originalProfiles, signedIdentity = originalIdentity,
            effects.IsAcceptedBoundaryComplete, effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint,
            effects.InputFingerprint, effects.CarrierAuthorityFingerprint, effects.SourceAuthorityFingerprint,
            effects.TargetAuthorityFingerprint, eventInput, effects.Sources, effects.Targets, effects.SourceBindings,
            beforeCarriers, afterCarriers, beforeIdentity, afterIdentity, baselines, activeEffects,
            effects.AllocatedEffectIds, effects.AllocatedTransitionIds, effects.TouchedPaths, effects.DeletedPaths,
            executions, resources.Events, resources.AppliedTransitions, resources.ReplayTransitions,
            intervalEffectBefore = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectBefore),
            intervalEffectAfter = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectAfter),
            transcript = new { transcript.IsComplete, transcript.Fingerprint, transcript.PlanAuthority,
                transcript.Boundaries, transcript.BoundaryCloses, transcript.CausalClosures, transcript.UseProjectionOrdinal,
                transcript.PendingFrontierBoundaryOrdinal, transcript.AppliedComponentEvidence, transcript.ResourceMutations,
                transcript.TerminalAvailabilityReservations, transcript.ExpansionCount,
                expansionUsage = transcript.ExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
                acceptedActivations = accepted.Select(row => new { row.Boundary, row.Activation, row.CandidateFingerprint,
                    row.MechanicsOrdinal, candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate) }).ToArray(),
                rejectedActivations = transcript.RejectedActivations.Select(row => new { row.Boundary, row.CandidateFingerprint,
                    row.Reason, row.BlockedAvailabilityEffectId,
                    candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate) }).ToArray(),
                releasedReactions = transcript.ReleasedReactions.Select(row => new { row.Boundary, row.Activation,
                    row.Stage, row.MechanicsOrdinal, reaction = ReadPressureZeroReactionDiagnosticData(row.Reaction) }).ToArray() } }));
        Assert.True(effects.IsAcceptedBoundaryComplete); Assert.True(transcript.IsComplete);
        Assert.False(effects.HasPressureConflictCarrierCollision());
        Assert.True(JsonNode.DeepEquals(originalIdentity, beforeIdentity));
        Assert.True(JsonNode.DeepEquals(originalProfiles, beforeCarriers[AfterlifeEntityProfileState.StatePath]));
        var expectedProfiles = originalProfiles.DeepClone().AsObject();
        if (!expectedProfiles.ContainsKey("schemaVersion")) expectedProfiles["schemaVersion"] = 1;
        foreach (var profile in expectedProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>())
            foreach (var field in new[] { "activeWounds", "activeEffects" })
                if (!profile.ContainsKey(field)) profile[field] = new JsonArray();
        Assert.True(JsonNode.DeepEquals(expectedProfiles, baselines.AfterlifeProfiles));
        var originalGuardian = ReadGuardianAttributionProfile(originalProfiles);
        var profiles = afterCarriers.TryGetValue(AfterlifeEntityProfileState.StatePath, out var changed)
            ? changed : Assert.IsType<JsonObject>(baselines.AfterlifeProfiles);
        var guardian = ReadGuardianAttributionProfile(profiles);
        var expectedGuardian = ReadGuardianAttributionProfile(expectedProfiles);
        var restoredIdentity = afterIdentity.DeepClone().AsObject();
        var parsedIdentity = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(afterIdentity), EffectIdentityState.StatePath);
        RequireGuardianJourneyIssues(setup.Name, "completed_effect_identity", parsedIdentity.Issues); Assert.NotNull(parsedIdentity.State);
        Assert.Equal(2, guardian["activeWounds"]!.AsArray().Count);
        var roots = setup.Data.Retained.Parents.SelectMany(parent => parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())
            .Select(root => root!["effectId"]!.GetValue<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, roots.Length); Assert.Equal(roots.Length, roots.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(roots, guardian["activeEffects"]!.AsArray().Select(root => root!["effectId"]!.GetValue<string>()).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(roots, activeEffects.Select(root => root["effectId"]!.GetValue<string>()).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(roots, executions.Select(row => row.EffectId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(roots, accepted.Select(row => row.Activation.Stamp.Identity.EffectId).OrderBy(id => id, StringComparer.Ordinal));
        foreach (var expectedParent in setup.Data.Retained.Parents)
        {
            var woundId = expectedParent["woundId"]!.GetValue<string>();
            Assert.True(JsonNode.DeepEquals(expectedParent, Assert.Single(originalGuardian["activeWounds"]!.AsArray(),
                row => row!["woundId"]!.GetValue<string>() == woundId)));
            Assert.True(JsonNode.DeepEquals(expectedParent, Assert.Single(guardian["activeWounds"]!.AsArray(),
                row => row!["woundId"]!.GetValue<string>() == woundId)));
            foreach (var binding in expectedParent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())
            {
                var root = binding!["effectId"]!.GetValue<string>();
                var baselineEffect = Assert.Single(originalGuardian["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
                Assert.True(JsonNode.DeepEquals(Assert.Single(setup.Data.Retained.Effects, row => row["effectId"]!.GetValue<string>() == root), baselineEffect));
                var effect = Assert.Single(guardian["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
                var entry = Assert.Single(afterIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
                var priorEntry = Assert.Single(originalIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
                var transitions = entry["transitions"]!.AsArray(); var priorTransitions = priorEntry["transitions"]!.AsArray();
                Assert.Equal(priorTransitions.Count + 1, transitions.Count);
                for (var index = 0; index < priorTransitions.Count; index++) Assert.True(JsonNode.DeepEquals(priorTransitions[index], transitions[index]));
                var append = Assert.Single(parsedIdentity.State!.Entries, row => row.EffectId == root).Transitions[^1];
                var execution = Assert.Single(executions, row => row.EffectId == root);
                Assert.Equal(("on_owner_turn_end", "owner_turn_end"), (execution.TriggerId, execution.EventKind));
                Assert.Null(execution.RemainingUseBudget);
                Assert.Equal(("trigger", 46, execution.EventRef), (append.Kind, append.Turn, append.EventRef));
                Assert.Equal(new[] { root }, append.SourceEffectIds); Assert.Equal(new[] { root }, append.ResultEffectIds); Assert.Null(append.ReceiptId);
                var activation = Assert.Single(accepted, row => row.Activation.Stamp.Identity.EffectId == root);
                Assert.Equal(new EffectActivationCandidateIdentity(root, execution.TriggerId, execution.EventKind,
                    execution.EventRef, execution.TriggerEventRef!), activation.Activation.Stamp.Identity);
                Assert.False(activation.Activation.Stamp.ConsumesUse); Assert.Null(activation.Activation.Stamp.UsesBefore); Assert.Null(activation.Activation.UsesAfter);
                var lifecycle = Assert.Single(eventInput["lifecycleEvents"]!.AsArray(), row =>
                    row!["phase"]!.GetValue<string>() == "owner_turn_end" &&
                    row["eventRef"]!.GetValue<string>() == execution.TriggerEventRef)!;
                Assert.Equal((46, "guardian", "guardian_frame"), (lifecycle["turn"]!.GetValue<int>(),
                    lifecycle["target"]!["kind"]!.GetValue<string>(), lifecycle["target"]!["targetId"]!.GetValue<string>()));
                var expectedEffect = baselineEffect.DeepClone();
                expectedEffect["chronology"]!["lastTransitionId"] = append.TransitionId;
                expectedEffect["chronology"]!["lastTransitionTurn"] = 46;
                Assert.True(JsonNode.DeepEquals(expectedEffect, effect)); // Includes every selected component and every legal sibling.
                Assert.True(JsonNode.DeepEquals(expectedEffect, Assert.Single(activeEffects, row => row["effectId"]!.GetValue<string>() == root)));
                var expectedRoot = Assert.Single(expectedGuardian["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!;
                expectedRoot["chronology"]!["lastTransitionId"] = append.TransitionId; expectedRoot["chronology"]!["lastTransitionTurn"] = 46;
                Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == root)!["transitions"] = priorTransitions.DeepClone();
            }
        }
        Assert.True(JsonNode.DeepEquals(expectedProfiles, profiles));
        Assert.True(JsonNode.DeepEquals(originalIdentity, restoredIdentity));
        return new(JsonSerializer.Serialize(afterCarriers), afterIdentity.ToJsonString(),
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects));
    }

    /// <summary>Checks the final common-plan carrier and identity copies against the independently checked completed guardian result.</summary>
    /// <param name="name">This independent case's diagnostic identity.</param>
    /// <param name="completed">Immutable checked effect DATA from actual completion.</param>
    /// <param name="plan">The genuine common unpublished plan; this method never registers it.</param>
    private void AssertGuardianGroupZeroCommonEffectCopy(string name, GuardianGroupZeroCompletedEffectData completed, AcceptedMechanicsPlan plan)
    {
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var commonCarriers = effects.CarrierAfterImages; var commonIdentity = effects.IdentityIndexAfterImage;
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_common_effect_copy", name,
            completed.CarrierAfterImagesJson, completed.IdentityAfterJson, plan.EffectCarrierAfterImages, plan.EffectIdentityAfterImage,
            commonCarriers, commonIdentity, effects.IsAcceptedBoundaryComplete,
            effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint }));
        Assert.True(effects.IsAcceptedBoundaryComplete);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.CarrierAfterImagesJson), JsonSerializer.SerializeToNode(plan.EffectCarrierAfterImages)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.CarrierAfterImagesJson), JsonSerializer.SerializeToNode(commonCarriers)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.IdentityAfterJson), plan.EffectIdentityAfterImage));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.IdentityAfterJson), commonIdentity));
        Assert.Equal(completed.PayloadFingerprint, WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects));
    }
}
