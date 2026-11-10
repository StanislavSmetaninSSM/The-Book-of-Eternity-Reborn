using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Complete detached attempt DATA and checked path bindings, with no executable owner or publication key.</summary>
    private sealed record GroupZeroRetryAttemptData(JsonObject Envelope, string[] Allocations,
        IReadOnlyDictionary<string, string> Bindings);

    /// <summary>Checks this N4 attempt before allowing exactly eight fresh IDs and their eleven derived bindings to differ.</summary>
    /// <param name="setup">Independent permanent parent IDs and signed original resource/identity prefixes.</param>
    /// <param name="completed">Already checked source-only result; its full resource images and actual root are retained.</param>
    /// <param name="journalJson">Exact completed capture journal, read before observation and registration.</param>
    /// <param name="cursor">Exact committed allocation cursor.</param>
    /// <param name="interval">Actual closed resource interval, not a projection or fabricated receipt.</param>
    /// <param name="effects">Already completed effect DATA whose full payload and closed prefixes are rehashed.</param>
    /// <returns>Detached full envelope and precisely resolved scalar bindings.</returns>
    private static GroupZeroRetryAttemptData ReadGroupZeroRetryAttempt(PressureGroupPublicationSetup setup,
        PressureGroupCompletedData completed, string journalJson, int cursor,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, EffectAcceptedTurnPlan effects)
    {
        Assert.Equal(GroupZeroCase(setup.Spec.Name, GroupZeroPressureContour.Capped), setup.Spec);
        var parents = setup.Parents.Select(json => JsonNode.Parse(json)!.AsObject()).ToArray();
        Assert.Equal(4, parents.Length);
        var roots = parents.Select(parent => Assert.Single(parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())!
            ["effectId"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(4, roots.Distinct(StringComparer.Ordinal).Count());
        var data = ReadGroupZeroRetryCompletedData(journalJson, cursor, interval, effects);
        var journal = data["AllocationJournalJson"]!.AsArray();
        var witness = JsonNode.Parse(completed.WitnessJson)!.AsObject();
        var receipt = JsonNode.Parse(completed.ReceiptJson)!.AsObject();
        var actual = JsonNode.Parse(completed.ActualRootJson)!.AsObject();
        AssertGroupZeroSourceOnlyReceipt(setup, receipt);
        Assert.True(JsonNode.DeepEquals(witness, Assert.Single(receipt["strainComparisons"]!.AsArray())!["witness"]));
        Assert.Equal(SpiritualGroupZeroPressureData.Expected(GroupZeroPressureContour.Capped).Actual,
            SpiritualWoundSourceWitness.Validate(witness));
        var expectedRoot = setup.Copy.Conflict.DeepClone().AsObject();
        expectedRoot["activeConflict"]!["exchangeLog"]![0] = SpiritualGroupZeroPressureData.ReadExchange(GroupZeroPressureContour.Capped, actual: true);
        expectedRoot["activeConflict"]!["playerSideStrain"] = "broken";
        Assert.True(JsonNode.DeepEquals(expectedRoot, actual));
        Assert.Equal((8, 8), (cursor, journal.Count));
        Assert.Equal(("conflict_resource_cost", "exchange_pressure_group_46", 0),
            (interval.ConflictId, interval.ExchangeId, interval.Ordinal));
        Assert.Equal(2, interval.AppliedTransitions.Count); Assert.Empty(interval.ReplayTransitions); Assert.Equal(4, interval.Events.Count);
        Assert.Empty(effects.AllocatedCombatantIds); Assert.Empty(effects.AllocatedEffectIds);
        Assert.Equal(4, effects.AllocatedTransitionIds.Count);
        Assert.True(effects.IsAcceptedBoundaryComplete); Assert.NotNull(effects.AcceptedBoundaryBasePlanFingerprint);

        var envelope = new JsonObject
        {
            ["WitnessJson"] = witness, ["ReceiptJson"] = receipt, ["ActualRootJson"] = actual,
            ["ResourceStateAfterImage"] = JsonNode.Parse(completed.Resources.StateAfterImage!.ToCanonicalJson()),
            ["ResourceHistoryAfterImage"] = JsonNode.Parse(completed.Resources.HistoryAfterImage!.ToCanonicalJson()),
            ["EffectPayloadFingerprint"] = completed.EffectPayloadFingerprint, ["completed"] = data
        };
        var allocations = new string[8];
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var state = envelope["ResourceStateAfterImage"]!["entries"]!.AsArray();
        var history = envelope["ResourceHistoryAfterImage"]!["entries"]!.AsArray();
        var priorState = ReadGuardianAttributionJson(setup.Origin.Tracked[ResourceMaterializationContract.StatePath]!)["entries"]!.AsArray();
        var priorHistory = ReadGuardianAttributionJson(setup.Origin.Tracked[ResourceMaterializationContract.HistoryPath]!)["entries"]!.AsArray();
        Assert.Equal(2, priorState.Count); // Two independently identified AP owners, never positional owner inference.
        Assert.Equal(4, priorHistory.Count); // BuildActionPointState signs two initializations and two fixture spends.
        Assert.Equal(priorHistory.Count + 2, history.Count);
        for (var i = 0; i < priorHistory.Count; i++) Assert.True(JsonNode.DeepEquals(priorHistory[i], history[i]), "resource History prefix " + i);
        Assert.Equal(priorState.Count, state.Count);
        var restoredState = state.DeepClone().AsArray();
        var coordinates = new ResourceCoordinate[]
        {
            new("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, "afterlife_conflict_side_cost", "spiritual_action_points"),
            new("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points")
        };
        // Sixteen interval/journal references plus six references in the complete canonical resource images.
        for (var side = 0; side < 2; side++)
        {
            var eventRef = "turn_46:afterlife_conflict:exchange_pressure_group_46:" + (side == 0 ? "opposition" : "player");
            var key = new ResourceOperationKey(eventRef, "afterlife_cost", interval.ExchangeId, coordinates[side], ResourceOperation.Spend);
            var coordinateHash = SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(key)!.AsObject(), "resource_mutation_key");
            allocations[2 * side] = ReadAllocation(2 * side, "resource_operation", "resources", coordinateHash);
            allocations[2 * side + 1] = ReadAllocation(2 * side + 1, "resource_transition", "resources", coordinateHash);
            var transition = interval.AppliedTransitions[side];
            Assert.Equal((allocations[2 * side], allocations[2 * side + 1], eventRef, coordinates[side]),
                (transition.OperationId, transition.TransitionId, transition.EventRef, transition.Coordinate));
            Assert.Equal((ResourceTransitionOperation.Spend, "afterlife_cost", interval.ExchangeId, ResourceMutationPhase.DirectCost, 130, side),
                (transition.Operation, transition.OriginKind, transition.OriginId, transition.Phase, transition.Priority, transition.ExecutionSequence));
            Assert.Equal((3m, 3m, 3m, 0m, side == 0 ? 9m : 10m, 46),
                (transition.RequestedAmount, transition.AppliedAmount, transition.BeforeState!.Current, transition.AfterState!.Current, transition.AfterState.Maximum, transition.Turn));
            Assert.Equal(transition.BeforeState! with { Current = 0m }, transition.AfterState);
            Assert.Equal(transition, interval.EffectAfter.ResourceMutations[side].Transition);
            Assert.Equal(key, interval.EffectAfter.ResourceMutations[side].Mutation);
            Assert.Equal(ResourceMutationExecutionKind.Applied, interval.EffectAfter.ResourceMutations[side].ExecutionKind);
            for (var eventIndex = 0; eventIndex < 2; eventIndex++)
            {
                var ev = interval.Events[2 * side + eventIndex];
                Assert.Equal((allocations[2 * side], eventRef, coordinates[side], 3m, 0m, 3m, 46, side),
                    (ev.OperationId, ev.EventRef, ev.Coordinate, ev.Before, ev.After, ev.AppliedAmount, ev.Turn, ev.ExecutionSequence));
                Assert.Equal(eventIndex == 0 ? "resource_spent" : "resource_depleted", ev.EventKind);
                Bind($"/completed/Interval/Events/{2 * side + eventIndex}/OperationId", allocations[2 * side]);
            }
            Bind($"/completed/Interval/AppliedTransitions/{side}/OperationId", allocations[2 * side]);
            Bind($"/completed/Interval/AppliedTransitions/{side}/TransitionId", allocations[2 * side + 1]);
            Bind($"/completed/Interval/EffectAfter/ResourceMutations/{side}/Transition/OperationId", allocations[2 * side]);
            Bind($"/completed/Interval/EffectAfter/ResourceMutations/{side}/Transition/TransitionId", allocations[2 * side + 1]);
            var stateIndex = FindGroupZeroRetryIndex(state, row => MatchesCoordinate(row, coordinates[side]));
            var priorStateIndex = FindGroupZeroRetryIndex(priorState, row => MatchesCoordinate(row, coordinates[side]));
            Assert.Equal(stateIndex, priorStateIndex);
            Assert.Equal((0m, side == 0 ? 9m : 10m, eventRef, 46),
                (state[stateIndex]!["current"]!.GetValue<decimal>(), state[stateIndex]!["maximum"]!.GetValue<decimal>(),
                    state[stateIndex]!["chronology"]!["lastEventRef"]!.GetValue<string>(), state[stateIndex]!["chronology"]!["lastTransitionTurn"]!.GetValue<int>()));
            Bind($"/ResourceStateAfterImage/entries/{stateIndex}/chronology/lastTransitionId", allocations[2 * side + 1]);
            var expectedState = priorState[priorStateIndex]!.DeepClone().AsObject();
            expectedState["current"] = 0m;
            expectedState["chronology"]!["lastTransitionId"] = allocations[2 * side + 1];
            expectedState["chronology"]!["lastEventRef"] = eventRef;
            expectedState["chronology"]!["lastTransitionTurn"] = 46;
            Assert.True(JsonNode.DeepEquals(expectedState, state[stateIndex]), "Complete resource after-entry " + eventRef);
            restoredState[stateIndex] = priorState[priorStateIndex]!.DeepClone();
            var historyIndex = FindGroupZeroRetryIndex(history, row => row["eventRef"]!.GetValue<string>() == eventRef &&
                MatchesCoordinate(row["coordinate"]!.AsObject(), coordinates[side]));
            Assert.Equal(priorHistory.Count + side, historyIndex);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ResourceHistoryState.SerializeCanonicalTransitions([transition]))!["entries"]![0], history[historyIndex]));
            Bind($"/ResourceHistoryAfterImage/entries/{historyIndex}/operationId", allocations[2 * side]);
            Bind($"/ResourceHistoryAfterImage/entries/{historyIndex}/transitionId", allocations[2 * side + 1]);
        }
        Assert.True(JsonNode.DeepEquals(priorState, restoredState), "All other resource entries remain exact.");

        var beforeIdentity = JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject();
        Assert.True(JsonNode.DeepEquals(beforeIdentity, data["IdentityIndexBeforeImage"]));
        var afterIdentity = data["IdentityIndexAfterImage"]!["entries"]!.AsArray();
        var priorIdentity = beforeIdentity["entries"]!.AsArray();
        Assert.Equal(4, priorIdentity.Count); Assert.Equal(priorIdentity.Count, afterIdentity.Count);
        var restoredIdentity = data["IdentityIndexAfterImage"]!.DeepClone().AsObject();
        var activeEffects = data["ActiveEffects"]!.AsArray();
        Assert.Equal(roots, activeEffects.Select(row => row!["effectId"]!.GetValue<string>()));
        var carrierPath = "/completed/CarrierAfterImages/" + EscapeGroupZeroRetryPath(AfterlifeEntityProfileState.StatePath);
        var carrierProfile = ReadPressureZeroRetryPath(envelope, carrierPath)!["profiles"]!.AsArray();
        var triggerProfiles = data["ResourceTriggerCarriers"]!["AfterlifeProfiles"]!["profiles"]!.AsArray();
        var profileIndex = FindGroupZeroRetryIndex(carrierProfile, IsPlayer);
        var triggerProfileIndex = FindGroupZeroRetryIndex(triggerProfiles, IsPlayer);
        var carrierEffects = carrierProfile[profileIndex]!["activeEffects"]!.AsArray();
        var triggerEffects = triggerProfiles[triggerProfileIndex]!["activeEffects"]!.AsArray();
        Assert.Equal(roots, carrierEffects.Select(row => row!["effectId"]!.GetValue<string>()));
        Assert.Equal(roots, triggerEffects.Select(row => row!["effectId"]!.GetValue<string>()));
        // Six per root: allocation journal plus five distinct full-plan/identity/carrier references.
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var root = roots[rootIndex];
            var accepted = Assert.Single(interval.EffectAfter.AcceptedActivations, row => row.Activation.Stamp.Identity.EffectId == root);
            var activation = accepted.Activation.Stamp.Identity;
            Assert.Equal(("on_owner_turn_end", "owner_turn_end", "turn_46:lifecycle:owner_turn_end:chaos_sea:player:player_soul"),
                (activation.TriggerId, activation.EventKind, activation.TriggerEventRef));
            Assert.Equal(new ResourcePendingAuthorityBinding("permanent", root), accepted.Activation.Stamp.EffectAuthority);
            Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(activation.TriggerEventRef, root,
                accepted.Activation.Stamp.EffectAuthority, activation.TriggerId), activation.EventRef);
            Assert.False(accepted.Activation.Stamp.ConsumesUse); Assert.Null(accepted.Activation.Stamp.UsesBefore);
            Assert.Null(accepted.Activation.UsesAfter); Assert.False(accepted.Activation.EffectTerminal);
            var key = new EffectIdentityAllocationKey(activation.EventRef, "trigger_execution", root);
            allocations[4 + rootIndex] = ReadAllocation(4 + rootIndex, "effect_transition", "effects",
                SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(key)!.AsObject(), "allocation_effect_transition"));
            Assert.Equal(allocations[4 + rootIndex], effects.AllocatedTransitionIds[rootIndex]);
            Bind($"/completed/AllocatedTransitionIds/{rootIndex}", allocations[4 + rootIndex]);
            var activeIndex = FindGroupZeroRetryIndex(activeEffects, row => row["effectId"]!.GetValue<string>() == root);
            var carrierIndex = FindGroupZeroRetryIndex(carrierEffects, row => row["effectId"]!.GetValue<string>() == root);
            var triggerIndex = FindGroupZeroRetryIndex(triggerEffects, row => row["effectId"]!.GetValue<string>() == root);
            Bind($"/completed/ActiveEffects/{activeIndex}/chronology/lastTransitionId", allocations[4 + rootIndex]);
            Bind(carrierPath + $"/profiles/{profileIndex}/activeEffects/{carrierIndex}/chronology/lastTransitionId", allocations[4 + rootIndex]);
            Bind($"/completed/ResourceTriggerCarriers/AfterlifeProfiles/profiles/{triggerProfileIndex}/activeEffects/{triggerIndex}/chronology/lastTransitionId", allocations[4 + rootIndex]);
            var identityIndex = FindGroupZeroRetryIndex(afterIdentity, row => row["effectId"]!.GetValue<string>() == root);
            var priorIndex = FindGroupZeroRetryIndex(priorIdentity, row => row["effectId"]!.GetValue<string>() == root);
            Assert.Equal(identityIndex, priorIndex);
            var oldTransitions = priorIdentity[priorIndex]!["transitions"]!.AsArray();
            var transitions = afterIdentity[identityIndex]!["transitions"]!.AsArray();
            Assert.Equal(oldTransitions.Count + 1, transitions.Count);
            for (var i = 0; i < oldTransitions.Count; i++) Assert.True(JsonNode.DeepEquals(oldTransitions[i], transitions[i]), root + " historical transition " + i);
            var appended = transitions[^1]!;
            Assert.Equal(("trigger", 46, activation.EventRef),
                (appended["kind"]!.GetValue<string>(), appended["turn"]!.GetValue<int>(), appended["eventRef"]!.GetValue<string>()));
            Assert.Equal(root, Assert.Single(appended["sourceEffectIds"]!.AsArray())!.GetValue<string>());
            Assert.Equal(root, Assert.Single(appended["resultEffectIds"]!.AsArray())!.GetValue<string>());
            Assert.Null(appended["receiptId"]);
            Bind($"/completed/IdentityIndexAfterImage/entries/{identityIndex}/transitions/{oldTransitions.Count}/transitionId", allocations[4 + rootIndex]);
            restoredIdentity["entries"]![identityIndex]!["transitions"] = oldTransitions.DeepClone();
        }
        Assert.True(JsonNode.DeepEquals(beforeIdentity, restoredIdentity));
        Assert.Equal(8, allocations.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(46, bindings.Count);

        // Rebuild the single four-candidate boundary, then both AP mutations; no retained digest is trusted alone.
        AssertGroupZeroRetryPrefixFingerprint(interval.EffectBefore, roots, mutationCount: 0);
        AssertGroupZeroRetryPrefixFingerprint(interval.EffectAfter, roots, mutationCount: 2);
        var resourceHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_resource_result", "1", interval.ConflictId, interval.ExchangeId,
            interval.Ordinal.ToString(CultureInfo.InvariantCulture), JsonSerializer.Serialize(interval.AppliedTransitions),
            JsonSerializer.Serialize(interval.ReplayTransitions), JsonSerializer.Serialize(interval.Events)
        });
        var effectHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_effect_prefix", "1", effects.AcceptedBoundaryBasePlanFingerprint, interval.EffectAfter.Fingerprint
        });
        var sourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
        var planHash = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects);
        Assert.Equal(planHash, effects.AcceptedBoundaryFinalPlanFingerprint);
        Bind("/completed/Interval/EffectAfter/Fingerprint", interval.EffectAfter.Fingerprint);
        Bind("/completed/AcceptedBoundaryFinalPlanFingerprint", planHash);
        Bind("/EffectPayloadFingerprint", planHash);
        foreach (var path in new[] { "/WitnessJson", "/ReceiptJson/strainComparisons/0/witness" })
        {
            Bind(path + "/resourceResultFingerprint", resourceHash); Bind(path + "/effectPlanFingerprint", effectHash);
            Bind(path + "/sourceFingerprint", sourceHash); Bind(path + "/sourceId", "spiritual_source_" + sourceHash[7..]);
        }
        Assert.Equal(57, bindings.Count);
        return new(envelope, allocations, bindings);

        string ReadAllocation(int ordinal, string kind, string owner, string coordinate)
        {
            var row = journal[ordinal]!.AsObject();
            Assert.Equal(new[] { "coordinate", "kind", "ordinal", "owner", "value" }, row.Select(pair => pair.Key).Order(StringComparer.Ordinal));
            Assert.Equal((ordinal, kind, owner, coordinate), (row["ordinal"]!.GetValue<int>(), row["kind"]!.GetValue<string>(),
                row["owner"]!.GetValue<string>(), row["coordinate"]!.GetValue<string>()));
            var value = row["value"]!.GetValue<string>(); Assert.Matches("^" + kind + "_[0-9a-f]{32}$", value);
            Bind($"/completed/AllocationJournalJson/{ordinal}/value", value); return value;
        }
        void Bind(string path, string expected)
        {
            Assert.Equal(expected, ReadPressureZeroRetryPath(envelope, path)!.GetValue<string>()); bindings.Add(path, expected);
        }
        static bool IsPlayer(JsonObject row) => row["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul";
        static bool MatchesCoordinate(JsonObject row, ResourceCoordinate coordinate) => row["realm"]!.GetValue<string>() == coordinate.Realm &&
            row["ownerKind"]!.GetValue<string>() == ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind) &&
            row["resourceOwnerId"]!.GetValue<string>() == coordinate.ResourceOwnerId && row["resourceKey"]!.GetValue<string>() == coordinate.ResourceKey;
    }

    /// <summary>Projects every completed effect/interval field using existing explicit internal-record diagnostics, without retaining the plan.</summary>
    /// <param name="journalJson">Exact completed allocation stream, parsed without discarding any row.</param>
    /// <param name="cursor">Exact committed cursor.</param>
    /// <param name="interval">Existing immutable resource interval.</param>
    /// <param name="effects">Existing completed effect DATA, not a publication capability.</param>
    /// <returns>Independent JSON DATA whose full fields participate in retry equality.</returns>
    private static JsonObject ReadGroupZeroRetryCompletedData(string journalJson, int cursor,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, EffectAcceptedTurnPlan effects)
    {
        return JsonSerializer.SerializeToNode(new
        {
            AllocationJournalJson = JsonNode.Parse(journalJson), AllocationCursor = cursor,
            Interval = new
            {
                interval.ConflictId, interval.ExchangeId, interval.Ordinal,
                interval.AppliedRange, interval.ReplayRange, interval.EventRange,
                interval.AppliedTransitions, interval.ReplayTransitions, interval.Events,
                EffectBefore = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectBefore),
                EffectAfter = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectAfter)
            },
            effects.InputFingerprint, effects.CarrierAuthorityFingerprint,
            effects.SourceAuthorityFingerprint, effects.TargetAuthorityFingerprint,
            SkillScopeFingerprint = effects.SkillScopeAuthority?.Fingerprint,
            effects.AllocatedCombatantIds, effects.AllocatedEffectIds, effects.AllocatedTransitionIds,
            effects.WoundApplicationRootEffectBindings, effects.Sources, effects.Targets, effects.SourceBindings,
            DeferredReactions = effects.DeferredReactions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(),
            effects.ReactionExpansionCount,
            ReactionExpansionUsage = effects.ReactionExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
            effects.ActiveEffects, effects.ResourceTriggerCarriers, effects.AcceptedCarrierBaselines,
            RetainedSourceAuthorityFingerprint = effects.SourceAuthority.Fingerprint,
            RetainedTargetAuthorityFingerprint = effects.TargetAuthority.Fingerprint,
            effects.EventInput, effects.CarrierBeforeImages, effects.CarrierAfterImages,
            effects.IdentityIndexBeforeImage, effects.IdentityIndexAfterImage, effects.TouchedPaths, effects.DeletedPaths,
            effects.IsAcceptedBoundaryComplete, effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint,
            MortalRecoveryExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalRecoveryExecutionFingerprint(effects),
            SpiritualHealingExecutionFingerprint = EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(effects),
            MortalDiagnosisExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalDiagnosisExecutionFingerprint(effects),
            MortalTreatmentExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalTreatmentExecutionFingerprint(effects)
        })!.AsObject();
    }

    /// <summary>Reconstructs one complete batch, preserving candidate order, all activation stamps and the closed semantic vectors.</summary>
    private static void AssertGroupZeroRetryPrefixFingerprint(AcceptedEffectBoundaryTranscript.ClosedPrefix prefix,
        IReadOnlyList<string> roots, int mutationCount)
    {
        var boundary = Assert.Single(prefix.Boundaries); var closure = Assert.Single(prefix.CausalClosures);
        Assert.Single(prefix.BoundaryCloses); Assert.Empty(closure.OperationIds); Assert.Empty(closure.ReplayStableOperationKeys);
        Assert.Equal(roots, prefix.AcceptedActivations.Select(row => row.Activation.Stamp.Identity.EffectId));
        Assert.Empty(prefix.RejectedActivations); Assert.Empty(prefix.AppliedComponentEvidence); Assert.Empty(prefix.ReleasedReactions);
        Assert.Empty(prefix.TerminalAvailabilityReservations); Assert.Empty(prefix.ExpansionUsage);
        Assert.Equal(mutationCount, prefix.ResourceMutations.Count);
        var builder = new AcceptedEffectBoundaryTranscript.Builder(prefix.PlanAuthority);
        var rebuiltBoundary = builder.OpenBoundary(boundary.ParentBoundaryOrdinal, boundary.Producer, boundary.EventKind,
            boundary.ProducerEventRef, boundary.ProducerTransitionId, boundary.ProducerExecutionSequence,
            boundary.ProducerMechanicsOrdinal, prefix.AcceptedActivations.Select(row => row.Candidate).ToArray());
        Assert.Equal(boundary, rebuiltBoundary);
        foreach (var accepted in prefix.AcceptedActivations)
        {
            Assert.Equal(boundary, accepted.Boundary); builder.RecordAccepted(rebuiltBoundary, accepted.Activation, accepted.Candidate);
        }
        Assert.Null(builder.BindCausalClosure(rebuiltBoundary, [], new Dictionary<string, string>(StringComparer.Ordinal)));
        Assert.Null(builder.CloseBoundary(rebuiltBoundary));
        foreach (var mutation in prefix.ResourceMutations)
        {
            Assert.Equal(mutation.MechanicsOrdinal, builder.RecordMutationExecution());
            builder.RecordResourceMutation(mutation.Mutation, mutation.Transition, mutation.ExecutionKind, mutation.MechanicsOrdinal);
        }
        var result = builder.CaptureClosedPrefix(); Assert.Empty(result.Issues); Assert.True(result.IsValid);
        Assert.Equal(prefix.Fingerprint, result.Prefix!.Fingerprint);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(prefix)),
            JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(result.Prefix!))));
    }

    /// <summary>Substitutes only prechecked fresh-ID references and eleven recomputed bindings, comparing every other retained field.</summary>
    private static void AssertGroupZeroFreshRetryEquivalent(GroupZeroRetryAttemptData first, GroupZeroRetryAttemptData retry)
    {
        Assert.Equal((8, 8, 57, 57), (first.Allocations.Length, retry.Allocations.Length, first.Bindings.Count, retry.Bindings.Count));
        Assert.Equal(16, first.Allocations.Concat(retry.Allocations).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Bindings.Keys.Order(StringComparer.Ordinal), retry.Bindings.Keys.Order(StringComparer.Ordinal));
        Assert.True(JsonNode.DeepEquals(first.Envelope["completed"]!["Interval"]!["EffectBefore"], retry.Envelope["completed"]!["Interval"]!["EffectBefore"]));
        Assert.Equal(first.Envelope["completed"]!["AcceptedBoundaryBasePlanFingerprint"]!.GetValue<string>(),
            retry.Envelope["completed"]!["AcceptedBoundaryBasePlanFingerprint"]!.GetValue<string>());
        var translated = first.Envelope.DeepClone().AsObject();
        foreach (var (path, value) in first.Bindings)
        {
            Assert.Equal(value, ReadPressureZeroRetryPath(first.Envelope, path)!.GetValue<string>());
            Assert.Equal(retry.Bindings[path], ReadPressureZeroRetryPath(retry.Envelope, path)!.GetValue<string>());
            SetPressureZeroRetryPath(translated, path, JsonValue.Create(retry.Bindings[path])!);
        }
        Assert.True(JsonNode.DeepEquals(translated, retry.Envelope), "Difference outside the exact eight-ID/57-binding bijection and independently checked hash closure.");
    }

    /// <summary>Seven detached corruptions exercise this oracle in the same live case, without another world or runtime row.</summary>
    private static void AssertGroupZeroRetryOracleRejectsMutations(GroupZeroRetryAttemptData first, GroupZeroRetryAttemptData retry)
    {
        var changed = retry.Envelope.DeepClone().AsObject(); changed["completed"]!["Interval"]!["AppliedTransitions"]![0]!["AppliedAmount"] = 2;
        Reject("amount", changed);
        changed = retry.Envelope.DeepClone().AsObject(); changed["completed"]!["AllocationJournalJson"]![0]!["coordinate"] = "sha256:" + new string('0', 64);
        Reject("journal coordinate", changed);
        var parentPath = retry.Bindings.Single(row => row.Key.StartsWith("/completed/ActiveEffects/", StringComparison.Ordinal) && row.Value == retry.Allocations[7]).Key;
        changed = retry.Envelope.DeepClone().AsObject(); SetPressureZeroRetryPath(changed, parentPath, JsonValue.Create(retry.Allocations[4])!);
        Reject("cross-parent transition", changed);
        changed = retry.Envelope.DeepClone().AsObject(); var parents = changed["WitnessJson"]!["strainPreimage"]!["parents"]!.AsArray();
        Assert.Equal(4, parents.Count); parents.RemoveAt(3); Reject("last parent loss", changed);
        changed = retry.Envelope.DeepClone().AsObject(); changed["ResourceHistoryAfterImage"]!["entries"]![0]!["eventRef"] = "changed_retained_history_prefix";
        Reject("History prefix", changed);
        changed = retry.Envelope.DeepClone().AsObject(); changed["completed"]!["Interval"]!["EffectBefore"]!["Fingerprint"] = "sha256:" + new string('0', 64);
        Reject("EffectBefore", changed);
        changed = retry.Envelope.DeepClone().AsObject(); changed["WitnessJson"]!["effectPlanFingerprint"] = "sha256:" + new string('0', 64);
        Reject("derived hash", changed);
        return;
        void Reject(string name, JsonObject mutation)
        {
            Assert.False(JsonNode.DeepEquals(retry.Envelope, mutation), name + " must actually change DATA.");
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertGroupZeroFreshRetryEquivalent(first, retry with { Envelope = mutation }));
        }
    }

    /// <summary>Finds exactly one independent permanent-ID/coordinate join before constructing its RFC6901 binding.</summary>
    private static int FindGroupZeroRetryIndex(JsonArray rows, Func<JsonObject, bool> predicate)
    {
        var indexes = rows.Select((row, index) => (Row: row!.AsObject(), Index: index)).Where(row => predicate(row.Row)).ToArray();
        return Assert.Single(indexes).Index;
    }

    /// <summary>Escapes a literal property name rather than treating a carrier path as nested JSON properties.</summary>
    private static string EscapeGroupZeroRetryPath(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
