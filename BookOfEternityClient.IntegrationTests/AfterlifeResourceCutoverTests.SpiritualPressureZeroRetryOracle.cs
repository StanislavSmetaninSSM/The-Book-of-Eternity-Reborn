using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Detached checked attempt DATA and exact scalar bindings; never a retained executable owner.</summary>
    private sealed record PressureZeroRetryAttemptData(JsonObject Envelope, string[] Allocations,
        IReadOnlyDictionary<string, string> Bindings);

    /// <summary>Validates each fresh attempt before permitting its five allocated values and derived hashes to be renamed.</summary>
    private static PressureZeroRetryAttemptData ReadPressureZeroRetryAttempt(PressureGroupCompletedData completed,
        string diagnostic, AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, EffectAcceptedTurnPlan effects)
    {
        var data = JsonNode.Parse(diagnostic)!.AsObject();
        var journal = JsonNode.Parse(data["AllocationJournalJson"]!.GetValue<string>())!.AsArray();
        data["AllocationJournalJson"] = journal;
        var witness = JsonNode.Parse(completed.WitnessJson)!.AsObject();
        var receipt = JsonNode.Parse(completed.ReceiptJson)!.AsObject();
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(), SpiritualWoundSourceWitness.Validate(witness));
        Assert.True(SpiritualWoundOpportunityReceiptState.Parse(completed.ReceiptJson, "test.zero.retry").IsValid);
        Assert.True(JsonNode.DeepEquals(witness, Assert.Single(receipt["strainComparisons"]!.AsArray())!["witness"]));
        Assert.Equal(5, data["AllocationCursor"]!.GetValue<int>());
        Assert.Equal(5, journal.Count);
        Assert.Equal(("conflict_resource_cost", "exchange_pressure_group_46", 0),
            (interval.ConflictId, interval.ExchangeId, interval.Ordinal));
        Assert.Equal(2, interval.AppliedTransitions.Count);
        Assert.Empty(interval.ReplayTransitions);
        Assert.Equal(4, interval.Events.Count);
        Assert.Empty(effects.AllocatedCombatantIds);
        Assert.Empty(effects.AllocatedEffectIds);
        Assert.Single(effects.AllocatedTransitionIds);

        var coordinates = new ResourceCoordinate[]
        {
            new("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, "afterlife_conflict_side_cost", "spiritual_action_points"),
            new("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points")
        };
        var allocations = new string[5];
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var envelope = new JsonObject
        {
            ["WitnessJson"] = witness, ["ReceiptJson"] = receipt,
            ["EffectPayloadFingerprint"] = completed.EffectPayloadFingerprint, ["completed"] = data
        };
        for (var side = 0; side < 2; side++)
        {
            var eventRef = "turn_46:afterlife_conflict:exchange_pressure_group_46:" + (side == 0 ? "opposition" : "player");
            var key = new ResourceOperationKey(eventRef, "afterlife_cost", interval.ExchangeId, coordinates[side], ResourceOperation.Spend);
            var coordinate = SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(key)!.AsObject(), "resource_mutation_key");
            allocations[2 * side] = ReadAllocation(2 * side, "resource_operation", "resources", coordinate);
            allocations[2 * side + 1] = ReadAllocation(2 * side + 1, "resource_transition", "resources", coordinate);
            var transition = interval.AppliedTransitions[side];
            Assert.Equal((allocations[2 * side], allocations[2 * side + 1], eventRef, coordinates[side]),
                (transition.OperationId, transition.TransitionId, transition.EventRef, transition.Coordinate));
            Assert.Equal((ResourceTransitionOperation.Spend, "afterlife_cost", interval.ExchangeId,
                ResourceMutationPhase.DirectCost, 130, side), (transition.Operation, transition.OriginKind,
                transition.OriginId, transition.Phase, transition.Priority, transition.ExecutionSequence));
            Assert.Equal((3m, 3m, 3m, 0m, side == 0 ? 9m : 10m, 46),
                (transition.RequestedAmount, transition.AppliedAmount, transition.BeforeState!.Current,
                    transition.AfterState!.Current, transition.AfterState.Maximum, transition.Turn));
            Assert.Equal(transition.BeforeState! with { Current = 0m }, transition.AfterState);
            Assert.Equal(transition, interval.EffectAfter.ResourceMutations[side].Transition);
            Assert.Equal(key, interval.EffectAfter.ResourceMutations[side].Mutation);
            Assert.Equal(ResourceMutationExecutionKind.Applied, interval.EffectAfter.ResourceMutations[side].ExecutionKind);
            for (var eventIndex = 0; eventIndex < 2; eventIndex++)
            {
                var resourceEvent = interval.Events[2 * side + eventIndex];
                Assert.Equal((allocations[2 * side], eventRef, coordinates[side], 3m, 0m, 3m, 46, side),
                    (resourceEvent.OperationId, resourceEvent.EventRef, resourceEvent.Coordinate,
                        resourceEvent.Before, resourceEvent.After, resourceEvent.AppliedAmount, resourceEvent.Turn, resourceEvent.ExecutionSequence));
                Assert.Equal(eventIndex == 0 ? "resource_spent" : "resource_depleted", resourceEvent.EventKind);
                Bind($"/completed/Interval/Events/{2 * side + eventIndex}/OperationId", allocations[2 * side]);
            }
            Bind($"/completed/Interval/AppliedTransitions/{side}/OperationId", allocations[2 * side]);
            Bind($"/completed/Interval/AppliedTransitions/{side}/TransitionId", allocations[2 * side + 1]);
            Bind($"/completed/Interval/EffectAfter/ResourceMutations/{side}/Transition/OperationId", allocations[2 * side]);
            Bind($"/completed/Interval/EffectAfter/ResourceMutations/{side}/Transition/TransitionId", allocations[2 * side + 1]);
        }

        var activation = Assert.Single(interval.EffectAfter.AcceptedActivations).Activation.Stamp.Identity;
        Assert.Equal(("effect_retained_pressure_owner_1", "on_owner_turn_end", "owner_turn_end",
            "turn_46:lifecycle:owner_turn_end:chaos_sea:player:player_soul"),
            (activation.EffectId, activation.TriggerId, activation.EventKind, activation.TriggerEventRef));
        var effectKey = new EffectIdentityAllocationKey(activation.EventRef, "trigger_execution", activation.EffectId);
        allocations[4] = ReadAllocation(4, "effect_transition", "effects",
            SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(effectKey)!.AsObject(), "allocation_effect_transition"));
        Assert.Equal(allocations[4], Assert.Single(effects.AllocatedTransitionIds));
        Assert.Equal(5, allocations.Distinct(StringComparer.Ordinal).Count());
        Bind("/completed/AllocatedTransitionIds/0", allocations[4]);
        Bind("/completed/ActiveEffects/0/chronology/lastTransitionId", allocations[4]);
        Bind("/completed/CarrierAfterImages/game_state~1meta~1afterlife_entity_profiles.json/profiles/0/activeEffects/0/chronology/lastTransitionId", allocations[4]);
        Bind("/completed/ResourceTriggerCarriers/AfterlifeProfiles/profiles/0/activeEffects/0/chronology/lastTransitionId", allocations[4]);
        Bind("/completed/IdentityIndexAfterImage/entries/0/transitions/1/transitionId", allocations[4]);
        var identityTransition = data["IdentityIndexAfterImage"]!["entries"]![0]!["transitions"]![1]!;
        Assert.Equal(("trigger", 46, activation.EventRef), (identityTransition["kind"]!.GetValue<string>(),
            identityTransition["turn"]!.GetValue<int>(), identityTransition["eventRef"]!.GetValue<string>()));
        Assert.Equal(activation.EffectId, Assert.Single(identityTransition["sourceEffectIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(activation.EffectId, Assert.Single(identityTransition["resultEffectIds"]!.AsArray())!.GetValue<string>());

        // Rebuild both effect prefixes from their exact semantic vectors; do not trust an arbitrary retained digest.
        AssertPressureZeroRetryPrefixFingerprint(interval.EffectBefore, 0);
        AssertPressureZeroRetryPrefixFingerprint(interval.EffectAfter, 2);
        var resourceHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_resource_result", "1", interval.ConflictId, interval.ExchangeId,
            interval.Ordinal.ToString(CultureInfo.InvariantCulture), JsonSerializer.Serialize(interval.AppliedTransitions),
            JsonSerializer.Serialize(interval.ReplayTransitions), JsonSerializer.Serialize(interval.Events)
        });
        Assert.True(effects.IsAcceptedBoundaryComplete);
        Assert.NotNull(effects.AcceptedBoundaryBasePlanFingerprint);
        var effectHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_effect_prefix", "1", effects.AcceptedBoundaryBasePlanFingerprint,
            interval.EffectAfter.Fingerprint
        });
        var sourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
        var planHash = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects);
        Assert.Equal(planHash, effects.AcceptedBoundaryFinalPlanFingerprint);
        Bind("/completed/Interval/EffectAfter/Fingerprint", interval.EffectAfter.Fingerprint);
        Bind("/completed/AcceptedBoundaryFinalPlanFingerprint", planHash);
        Bind("/EffectPayloadFingerprint", planHash);
        foreach (var path in new[] { "/WitnessJson", "/ReceiptJson/strainComparisons/0/witness" })
        {
            Bind(path + "/resourceResultFingerprint", resourceHash);
            Bind(path + "/effectPlanFingerprint", effectHash);
            Bind(path + "/sourceFingerprint", sourceHash);
            Bind(path + "/sourceId", "spiritual_source_" + sourceHash[7..]);
        }
        Assert.Equal(33, bindings.Count);
        return new(envelope, allocations, bindings);

        string ReadAllocation(int ordinal, string kind, string owner, string coordinate)
        {
            var row = journal[ordinal]!.AsObject();
            Assert.Equal(new[] { "coordinate", "kind", "ordinal", "owner", "value" }, row.Select(pair => pair.Key).Order(StringComparer.Ordinal));
            Assert.Equal((ordinal, kind, owner, coordinate), (row["ordinal"]!.GetValue<int>(), row["kind"]!.GetValue<string>(),
                row["owner"]!.GetValue<string>(), row["coordinate"]!.GetValue<string>()));
            var value = row["value"]!.GetValue<string>();
            Assert.Matches("^" + kind + "_[0-9a-f]{32}$", value);
            Bind($"/completed/AllocationJournalJson/{ordinal}/value", value);
            return value;
        }
        void Bind(string path, string expected)
        {
            Assert.Equal(expected, ReadPressureZeroRetryPath(envelope, path)!.GetValue<string>());
            bindings.Add(path, expected);
        }
    }

    /// <summary>Recomputes this fixture's one closed lifecycle boundary followed by zero or two AP mutations as DATA only.</summary>
    private static void AssertPressureZeroRetryPrefixFingerprint(AcceptedEffectBoundaryTranscript.ClosedPrefix prefix, int mutationCount)
    {
        var boundary = Assert.Single(prefix.Boundaries);
        var accepted = Assert.Single(prefix.AcceptedActivations);
        var closure = Assert.Single(prefix.CausalClosures);
        Assert.Single(prefix.BoundaryCloses);
        Assert.Empty(closure.OperationIds);
        Assert.Empty(closure.ReplayStableOperationKeys);
        Assert.Empty(prefix.RejectedActivations);
        Assert.Empty(prefix.AppliedComponentEvidence);
        Assert.Empty(prefix.ReleasedReactions);
        Assert.Empty(prefix.TerminalAvailabilityReservations);
        Assert.Empty(prefix.ExpansionUsage);
        Assert.Equal(mutationCount, prefix.ResourceMutations.Count);
        var builder = new AcceptedEffectBoundaryTranscript.Builder(prefix.PlanAuthority);
        var rebuiltBoundary = builder.OpenBoundary(boundary.ParentBoundaryOrdinal, boundary.Producer, boundary.EventKind,
            boundary.ProducerEventRef, boundary.ProducerTransitionId, boundary.ProducerExecutionSequence,
            boundary.ProducerMechanicsOrdinal, [accepted.Candidate]);
        Assert.Equal(boundary, rebuiltBoundary);
        builder.RecordAccepted(rebuiltBoundary, accepted.Activation, accepted.Candidate);
        Assert.Null(builder.BindCausalClosure(rebuiltBoundary, [], new Dictionary<string, string>(StringComparer.Ordinal)));
        Assert.Null(builder.CloseBoundary(rebuiltBoundary));
        foreach (var mutation in prefix.ResourceMutations)
        {
            Assert.Equal(mutation.MechanicsOrdinal, builder.RecordMutationExecution());
            builder.RecordResourceMutation(mutation.Mutation, mutation.Transition, mutation.ExecutionKind, mutation.MechanicsOrdinal);
        }
        var result = builder.CaptureClosedPrefix();
        Assert.Empty(result.Issues);
        Assert.True(result.IsValid);
        Assert.Equal(prefix.Fingerprint, result.Prefix!.Fingerprint);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(prefix)),
            JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(result.Prefix!))));
    }

    /// <summary>Renames only individually checked journal references and recomputed hash closure, then compares every remaining field.</summary>
    private static void AssertPressureZeroFreshRetryEquivalent(PressureZeroRetryAttemptData first, PressureZeroRetryAttemptData retry)
    {
        Assert.Equal(10, first.Allocations.Concat(retry.Allocations).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Bindings.Keys.Order(StringComparer.Ordinal), retry.Bindings.Keys.Order(StringComparer.Ordinal));
        var translated = first.Envelope.DeepClone().AsObject();
        foreach (var (path, value) in first.Bindings)
        {
            Assert.Equal(value, ReadPressureZeroRetryPath(first.Envelope, path)!.GetValue<string>());
            Assert.Equal(retry.Bindings[path], ReadPressureZeroRetryPath(retry.Envelope, path)!.GetValue<string>());
            SetPressureZeroRetryPath(translated, path, JsonValue.Create(retry.Bindings[path])!);
        }
        Assert.True(JsonNode.DeepEquals(translated, retry.Envelope), "Unexplained difference outside the exact five-allocation bijection and verified hash closure.");
    }

    /// <summary>Exercises four cheap independent corruptions without another owner, fixture, build or runtime matrix.</summary>
    private static void AssertPressureZeroRetryOracleRejectsMutations(PressureZeroRetryAttemptData first, PressureZeroRetryAttemptData retry)
    {
        var mutations = new (string Path, JsonNode Value)[]
        {
            ("/completed/Interval/AppliedTransitions/0/AppliedAmount", JsonValue.Create(2)!),
            ("/completed/AllocationJournalJson/0/coordinate", JsonValue.Create("sha256:" + new string('0', 64))!),
            ("/completed/Interval/Events/0/OperationId", JsonValue.Create(retry.Allocations[2])!),
            ("/WitnessJson/effectPlanFingerprint", JsonValue.Create("sha256:" + new string('0', 64))!)
        };
        foreach (var (path, value) in mutations)
        {
            var changed = retry.Envelope.DeepClone().AsObject();
            SetPressureZeroRetryPath(changed, path, value.DeepClone());
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertPressureZeroFreshRetryEquivalent(first, retry with { Envelope = changed }));
        }
    }

    /// <summary>Reads one explicit RFC6901 test binding, preserving property and array boundaries.</summary>
    private static JsonNode? ReadPressureZeroRetryPath(JsonNode root, string path)
    {
        JsonNode? node = root;
        foreach (var part in path.Split('/').Skip(1))
        {
            var key = part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node is JsonArray array ? array[int.Parse(key, CultureInfo.InvariantCulture)] : node![key];
        }
        return node;
    }

    /// <summary>Replaces one checked scalar in a detached comparison copy, never in runtime or receipt state.</summary>
    private static void SetPressureZeroRetryPath(JsonNode root, string path, JsonNode value)
    {
        var split = path.LastIndexOf('/');
        var parent = ReadPressureZeroRetryPath(root, path[..split])!;
        var key = path[(split + 1)..].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        if (parent is JsonArray array) array[int.Parse(key, CultureInfo.InvariantCulture)] = value;
        else parent[key] = value;
    }
}
