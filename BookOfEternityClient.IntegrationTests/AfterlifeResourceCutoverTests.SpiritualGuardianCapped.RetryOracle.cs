using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Immutable expected fixture DATA. It contains no owner, lease, registration or publication permission.</summary>
    private sealed record GuardianCappedRetryFixture(string Name, int Rank, string ImmutableFirstJson, string ReferenceEnvelopeJson)
    {
        /// <summary>Retains the already derived immutable baseline graph for this invocation only; no live authority is retained.</summary>
        internal GuardianCappedRetryAttemptData? ReferenceGraph { get; set; }
    }
    /// <summary>A complete untrimmed attempt with independently regenerated semantic symbols and all lexical bindings.</summary>
    private sealed record GuardianCappedRetryAttemptData(string EnvelopeJson, GuardianCappedRetryFixture Fixture, string[] Allocations,
        IReadOnlyDictionary<string, string> Symbols, IReadOnlyDictionary<string, string> Bindings, IReadOnlyDictionary<string, string[]> Dependencies);
    /// <summary>Identifies one named detached guard while preserving the complete original exception and diagnostic Data.</summary>
    private sealed class GuardianCappedRetryGuardFailure : Exception
    {
        internal string Guard { get; }
        internal GuardianCappedRetryGuardFailure(string guard, Exception inner) : base("Guardian capped retry guard: " + guard, inner)
        { Guard = guard; foreach (System.Collections.DictionaryEntry row in inner.Data) Data[row.Key] = row.Value; }
    }

    private const string GuardianCappedImmutableFirstFields = "Name Rank Role Version signedOriginalFiles signedOriginalTracked signedOriginalGenerationBase64 ReceiptJson IdentityJson authoredInputs Retained ordinaryRoot actualRoot Ordinary Actual OrdinaryCalculation ActualCalculation Source";
    private const string GuardianCappedEffectFields = "InputFingerprint CarrierAuthorityFingerprint SourceAuthorityFingerprint TargetAuthorityFingerprint SkillScopeFingerprint AllocatedCombatantIds AllocatedEffectIds AllocatedTransitionIds WoundApplicationRootEffectBindings Sources Targets SourceBindings DeferredReactions DeferredRoutingReactions ReactionExpansionCount ReactionExpansionUsage ActiveEffects ResourceTriggerCarriers AcceptedCarrierBaselines EventInput CarrierBeforeImages CarrierAfterImages IdentityIndexBeforeImage IdentityIndexAfterImage TouchedPaths DeletedPaths HasDeferredReactions IsAcceptedBoundaryComplete AcceptedBoundaryBasePlanFingerprint AcceptedBoundaryFinalPlanFingerprint LifecycleExecutionLedger LifecycleExecutionLedgerFingerprint MortalRecoveryExecutionFingerprint SpiritualHealingExecutionFingerprint MortalDiagnosisExecutionFingerprint MortalTreatmentExecutionFingerprint RetainedSourceAuthorityFingerprint RetainedTargetAuthorityFingerprint";

    /// <summary>Checks a real attempt against the unchanged signed/authored fixture without retaining or manufacturing authority.</summary>
    private static GuardianCappedRetryAttemptData ReadGuardianCappedRetryAttempt(GuardianAttributionJourney setup, string json, GuardianCappedRetryAttemptData? firstAttempt = null)
    {
        Assert.True(setup.IsGuardian); Assert.Equal(GuardianSingletonPressureContour.CappedBroken, setup.GuardianContour);
        var root = new GuardianCappedRetryDocument(json).Root.AsObject(); var first = root["First"]!.AsObject();
        Assert.Equal(setup.Name, first["Name"]!.GetValue<string>()); Assert.Equal(setup.ExpectedRank, first["Rank"]!.GetValue<int>());
        GuardianCappedEqual(JsonSerializer.SerializeToNode(ReadGuardianCappedRawImages(setup.Origin.Files.Select(row => new KeyValuePair<string, byte[]?>(row.Key, row.Value))))!, first["signedOriginalFiles"]);
        GuardianCappedEqual(JsonSerializer.SerializeToNode(ReadGuardianCappedRawImages(setup.Origin.Tracked))!, first["signedOriginalTracked"]);
        GuardianCappedEqual(JsonSerializer.SerializeToNode(ReadGuardianCappedRawImages(setup.Authored))!, first["authoredInputs"]);
        Assert.Equal(Convert.ToBase64String(setup.Origin.Generation), first["signedOriginalGenerationBase64"]!.GetValue<string>());
        Assert.Equal(setup.Origin.ReceiptJson, first["ReceiptJson"]!.GetValue<string>());
        Assert.Equal(setup.Origin.IdentityJson, first["IdentityJson"]!.GetValue<string>());
        foreach (var (name, expected) in new[] { ("Retained", JsonSerializer.SerializeToNode(setup.Retained)), ("ordinaryRoot", setup.Copy.Conflict), ("actualRoot", setup.ActualRoot),
            ("Ordinary", setup.Ordinary), ("Actual", setup.Actual), ("OrdinaryCalculation", JsonSerializer.SerializeToNode(setup.OrdinaryCalculation)), ("ActualCalculation", JsonSerializer.SerializeToNode(setup.ActualCalculation)) }) GuardianCappedEqual(expected!, first[name]);
        var fixture = ReadGuardianCappedRetryFixture(root);
        if (firstAttempt is not null)
        {
            Assert.Equal(firstAttempt.Fixture.Name, fixture.Name); Assert.Equal(firstAttempt.Fixture.Rank, fixture.Rank);
            Assert.Equal(firstAttempt.Fixture.ImmutableFirstJson, fixture.ImmutableFirstJson);
            fixture = firstAttempt.Fixture;
        }
        return ReadGuardianCappedRetryAttempt(fixture, json);
    }

    /// <summary>Retains immutable expected fixture operands once from a pinned detached baseline, never from a mutation candidate.</summary>
    private static GuardianCappedRetryFixture ReadGuardianCappedRetryFixture(JsonObject root)
    {
        var first = root["First"]!.AsObject(); var immutable = new JsonObject();
        foreach (var field in GuardianCappedImmutableFirstFields.Split(' ')) immutable[field] = first[field]?.DeepClone();
        return new(root["Name"]!.GetValue<string>(), root["Rank"]!.GetValue<int>(), immutable.ToJsonString(), root.ToJsonString());
    }

    /// <summary>Validates shape, originals, semantic allocations, every producer/hash and all reference joins before creating substitution DATA.</summary>
    private static GuardianCappedRetryAttemptData ReadGuardianCappedRetryAttempt(GuardianCappedRetryFixture fixture, string json)
    {
        var guard = "envelope-json";
        try
        {
            var document = new GuardianCappedRetryDocument(json); var root = document.Root.AsObject();
            guard = "envelope-schema"; GuardianCappedClosed(root, "phase Name Rank First Completed Plan EffectPayloadHashOperands PreparedPlanHashOperands PublicationBinding PublicationInputs SignedRollbackImages CurrentTerminalSeals");
            Assert.Equal("guardian_capped_complete_prepared_data_envelope", root["phase"]!.GetValue<string>());
            Assert.Equal(fixture.Name, root["Name"]!.GetValue<string>()); Assert.Equal(fixture.Rank, root["Rank"]!.GetValue<int>()); Assert.Contains(fixture.Rank, new[] { 3, 4 });
            var first = root["First"]!.AsObject(); var completed = root["Completed"]!.AsObject(); var plan = root["Plan"]!.AsObject();
            GuardianCappedClosed(first, "phase Name Rank Role Version signedOriginalFiles signedOriginalTracked signedOriginalGenerationBase64 ReceiptJson IdentityJson authoredInputs Retained ordinaryRoot actualRoot Ordinary Actual OrdinaryCalculation ActualCalculation Source Witness FirstPendingJson CommittedPendingJson CommittedCheckpointJson Transport AllocationJournalJson AllocationCursor Interval counts captureCurrent leaseActive");
            GuardianCappedClosed(completed, "phase Name Rank ColdScope ColdOfferReplay Transport CompletedPending CompletedCheckpoint NoneDecision AllocationJournal AllocationCursor Reduction CurrentTerminalSeals");
            GuardianCappedClosed(plan, "InputFingerprint PreparedPlanFingerprint DefinitionAfterImage StateAfterImage HistoryAfterImage AuthorityFingerprints AuthorityFingerprintEntries EffectCarrierAfterImages EffectIdentityAfterImage PendingAfterImages PendingGmPacket AwaitsPendingResolution PendingPublicationAuthority OwnerCompanionAfterImages OwnerTransitions ResourceEvents ProjectionInput OwnerAuthorityFingerprint BeforeImages TouchedPaths ConsumedPaths EffectPlan WoundStageBundle LiveWoundProofFingerprint WoundAnchorPlanFingerprint DirectWoundPublicationAuthority TreatmentResourcePublicationAuthority JointRepairCommonAuthority WoundCarrierAfterImages WoundIdentityAfterImage WoundHistoryAfterImage IsMortalReceiptOnlyWoundPublication");
            var reduction = completed["Reduction"]!.AsObject(); var resources = reduction["Resources"]!.AsObject(); var effects = plan["EffectPlan"]!.AsObject();
            GuardianCappedClosed(reduction, "Input InputFingerprint DefinitionAfterImageJson PendingAfterImage OwnerCompanionAfterImages OwnerTransitions HasMortalProducerCompletion RequiresSatisfactionReceipt HasLiveDecisionReceipt HasLiveWoundWork LiveWoundCompletion ReceiptAfterImage ReceiptProofPresent Success Issues Resources EffectPlan EffectPayloadHashOperands EffectPayloadFingerprint");
            GuardianCappedClosed(resources, "StateAfterImageJson HistoryAfterImageJson Events AppliedTransitions ReplayTransitions ResourceTriggerExecutions AcceptedPendingResolutions AcceptedReactionExecutions AcceptedResolvedPendingRequestIds Statistics IsValid Issues FullEffectBoundary");
            GuardianCappedClosed(effects, GuardianCappedEffectFields); GuardianCappedClosed(reduction["EffectPlan"]!.AsObject(), GuardianCappedEffectFields);
            foreach (var phase in new[] { first, completed }) { Assert.Equal(fixture.Name, phase["Name"]!.GetValue<string>()); Assert.Equal(fixture.Rank, phase["Rank"]!.GetValue<int>()); }
            Assert.Equal("guardian_capped_first_c2_data", first["phase"]!.GetValue<string>()); Assert.Equal("guardian_capped_completed_c3_data", completed["phase"]!.GetValue<string>());
            guard = "authority-entry-producer-join";
            GuardianCappedEqual(JsonSerializer.SerializeToNode(ReadGuardianCappedExact<AcceptedMechanicsAuthorityFingerprints>(plan["AuthorityFingerprints"]).Enumerate().ToArray()), plan["AuthorityFingerprintEntries"]);
            guard = "fixture-raw-invariants";
            var expectedFirst = new GuardianCappedRetryDocument(fixture.ImmutableFirstJson).Root.AsObject();
            foreach (var field in GuardianCappedImmutableFirstFields.Split(' ')) GuardianCappedEqual(expectedFirst[field], first[field], field);
            Assert.Equal((0, 5), (first["Role"]!.GetValue<int>(), first["Version"]!.GetValue<int>()));
            var signed = GuardianCappedImages(first["signedOriginalTracked"]!.AsArray()); var authored = GuardianCappedImages(first["authoredInputs"]!.AsArray());
            var signedRoot = ReadGuardianAttributionJson(signed[AfterlifeSpiritualConflictState.StatePath]!);
            var profiles = ReadGuardianAttributionJson(signed[AfterlifeEntityProfileState.StatePath]!);
            var retained = ReadGuardianCappedExact<RetainedPressureGroupData>(first["Retained"]);
            SpiritualGuardianAttributionDataCheck.Require(retained, profiles, signedRoot, fixture.Rank);
            Assert.Empty(signedRoot["activeConflict"]!["exchangeLog"]!.AsArray());
            Assert.True(authored[AfterlifeSpiritualConflictState.StatePath]!.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            GuardianCappedEqual(first["ordinaryRoot"], ReadGuardianAttributionJson(authored[AfterlifeSpiritualConflictState.StatePath]!));
            GuardianCappedEqual(first["ordinaryRoot"], first["actualRoot"]); GuardianCappedEqual(first["Ordinary"], first["Actual"]);
            GuardianCappedEqual(first["Actual"], first["actualRoot"]!["activeConflict"]!["exchangeLog"]![0]);
            guard = "literal-capped-calculation";
            var calculation = first["ActualCalculation"]!; GuardianCappedEqual(first["OrdinaryCalculation"], calculation);
            Assert.Equal((20L, 3, 1, 4, 2), (calculation["TraumaPressure"]!.GetValue<long>(), calculation["MaximumSeverityRank"]!.GetValue<int>(), calculation["PreviousStrainRank"]!.GetValue<int>(), calculation["NewStrainRank"]!.GetValue<int>(), calculation["ExtraJumpSteps"]!.GetValue<int>()));
            guard = "journal-shape-and-prefix";
            var journal = completed["AllocationJournal"]!.AsArray(); var initial = new GuardianCappedRetryDocument(first["AllocationJournalJson"]!.GetValue<string>()).Root.AsArray();
            Assert.Equal(4, initial.Count); Assert.Equal(5, journal.Count); Assert.Equal(initial.Count, first["AllocationCursor"]!.GetValue<int>()); Assert.Equal(journal.Count, completed["AllocationCursor"]!.GetValue<int>());
            for (var index = 0; index < initial.Count; index++) GuardianCappedEqual(initial[index], journal[index]);
            foreach (var row in journal) GuardianCappedClosed(row!.AsObject(), "coordinate kind ordinal owner value");
            var allocations = journal.Select(row => row!["value"]!.GetValue<string>()).ToArray();
            guard = "journal-uniqueness"; Assert.Equal(journal.Count, allocations.Distinct(StringComparer.Ordinal).Count());
            var interval = first["Interval"]!.AsObject(); GuardianCappedClosed(interval, "ConflictId ExchangeId Ordinal AppliedRange ReplayRange EventRange AppliedTransitions ReplayTransitions Events EffectBefore EffectAfter");
            foreach (var (field, end) in new[] { ("AppliedRange", 2), ("ReplayRange", 0), ("EventRange", 4) })
            { GuardianCappedClosed(interval[field]!.AsObject(), "Start End"); Assert.Equal((0, end), (interval[field]!["Start"]!.GetValue<int>(), interval[field]!["End"]!.GetValue<int>())); }
            var transitions = interval["AppliedTransitions"]!.AsArray().Select(ReadGuardianCappedExact<ResourceTransition>).ToArray();
            var events = interval["Events"]!.AsArray().Select(ReadGuardianCappedExact<ResourceAppliedEvent>).ToArray(); Assert.Equal(2, transitions.Length); Assert.Equal(4, events.Length);
            guard = "resource-allocation";
            for (var side = 0; side < 2; side++)
            {
                var transition = transitions[side]; var key = new ResourceOperationKey(transition.EventRef, transition.OriginKind, transition.OriginId, transition.Coordinate, ResourceOperation.Spend);
                var coordinate = SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(key)!.AsObject(), "resource_mutation_key");
                Allocation(2 * side, "resource_operation", "resources", coordinate); Allocation(2 * side + 1, "resource_transition", "resources", coordinate);
                Assert.Equal((allocations[2 * side], allocations[2 * side + 1]), (transition.OperationId, transition.TransitionId));
            }
            guard = "resource-transition-semantics";
            Assert.Equal("conflict_resource_cost", interval["ConflictId"]!.GetValue<string>()); Assert.Equal("exchange_guardian_attribution_46", interval["ExchangeId"]!.GetValue<string>()); Assert.Equal(0, interval["Ordinal"]!.GetValue<int>());
            var oppositionOwner = signedRoot["activeConflict"]!["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
            var coordinates = new[] { new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, oppositionOwner, "spiritual_action_points"), new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points") };
            for (var side = 0; side < 2; side++)
            {
                var t = transitions[side]; Assert.Equal(coordinates[side], t.Coordinate);
                Assert.Equal("turn_46:afterlife_conflict:exchange_guardian_attribution_46:" + (side == 0 ? "opposition" : "player"), t.EventRef);
                Assert.Equal((ResourceTransitionOperation.Spend, "afterlife_cost", "exchange_guardian_attribution_46", ResourceMutationPhase.DirectCost, 130, side), (t.Operation, t.OriginKind, t.OriginId, t.Phase, t.Priority, t.ExecutionSequence));
                Assert.Equal((3m, 3m, 3m, 0m, side == 0 ? 9m : 10m, 46), (t.RequestedAmount, t.AppliedAmount, t.BeforeState!.Current, t.AfterState!.Current, t.AfterState.Maximum, t.Turn)); Assert.Equal(t.BeforeState with { Current = 0m }, t.AfterState);
            }
            guard = "resource-events-and-order";
            for (var side = 0; side < 2; side++) for (var kind = 0; kind < 2; kind++)
            {
                var t = transitions[side]; var ev = events[2 * side + kind];
                Assert.Equal((kind == 0 ? "resource_spent" : "resource_depleted", t.OperationId, t.EventRef, t.Coordinate, 3m, 0m, 3m, 46, side, t.SourceEvidence.AuthorityFingerprint),
                    (ev.EventKind, ev.OperationId, ev.EventRef, ev.Coordinate, ev.Before, ev.After, ev.AppliedAmount, ev.Turn, ev.ExecutionSequence, ev.SourceFingerprint));
            }
            GuardianCappedEqual(interval["AppliedTransitions"], resources["AppliedTransitions"]); GuardianCappedEqual(interval["ReplayTransitions"], resources["ReplayTransitions"]); Assert.Empty(resources["ReplayTransitions"]!.AsArray());
            GuardianCappedEqual(interval["Events"], resources["Events"]);
            Assert.Equal(events.OrderBy(row => row.Turn).ThenBy(row => row.ExecutionSequence).ThenBy(row => row.EventKind, StringComparer.Ordinal).ThenBy(row => row.OperationId, StringComparer.Ordinal), plan["ResourceEvents"]!.AsArray().Select(ReadGuardianCappedExact<ResourceAppliedEvent>));
            guard = "resource-state-and-history"; AssertGuardianCappedRetryResources(signed, root, transitions);
            guard = "effect-prefix-before";
            var boundaryView = new JsonObject { ["Interval"] = interval.DeepClone(), ["FullEffectBoundary"] = resources["FullEffectBoundary"]!.DeepClone() };
            var rootId = Assert.Single(retained.Effects)["effectId"]!.GetValue<string>();
            var activation = AssertGuardianCappedRetryEffectBoundaries(boundaryView, transitions, rootId, effects, value => guard = value);
            guard = "effect-allocation";
            Allocation(4, "effect_transition", "effects", SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(new EffectIdentityAllocationKey(activation.Stamp.Identity.EventRef, "trigger_execution", rootId))!.AsObject(), "allocation_effect_transition"));
            guard = "guardian-carrier-history"; AssertGuardianCappedRetryCarriers(root, signed, retained, allocations[4], activation, value => guard = value);
            guard = "closed-resource-hash";
            var closedHashes = ReadGuardianCappedRetryHashes(document);
            var resourceClaim = Assert.Single(closedHashes.Where(row => row.Path == "/First/Witness/resourceResultFingerprint")); Assert.Equal(resourceClaim.Calculated, resourceClaim.Value);
            guard = "closed-effect-hash";
            var effectClaim = Assert.Single(closedHashes.Where(row => row.Path == "/First/Witness/effectPlanFingerprint")); Assert.Equal(effectClaim.Calculated, effectClaim.Value);
            guard = "source-hash";
            Assert.Equal(SpiritualWoundStateJson.Hash(first["Witness"]!.AsObject(), "source", "sourceId", "sourceFingerprint"), first["Witness"]!["sourceFingerprint"]!.GetValue<string>());
            guard = "positive-packet-checkpoint-none"; AssertGuardianCappedRetryTransport(root, signed, authored);
            guard = "full-copy-joins";
            Assert.Equal(plan["InputFingerprint"]!.GetValue<string>(), reduction["InputFingerprint"]!.GetValue<string>());
            GuardianCappedEqual(effects, reduction["EffectPlan"]); GuardianCappedEqual(root["PublicationBinding"], reduction["Input"]);
            GuardianCappedEqual(plan["BeforeImages"], root["PublicationBinding"]!["BeforeImages"]); GuardianCappedEqual(plan["AuthorityFingerprints"], root["PublicationBinding"]!["AuthorityFingerprints"]);
            GuardianCappedClosed(plan["ProjectionInput"]!.AsObject(), "Definitions State History OwnerAuthorityFingerprint");
            Assert.Equal(plan["OwnerAuthorityFingerprint"]!.GetValue<string>(), plan["ProjectionInput"]!["OwnerAuthorityFingerprint"]!.GetValue<string>());
            Assert.Equal(plan["OwnerAuthorityFingerprint"]!.GetValue<string>(), plan["AuthorityFingerprints"]!["Owners"]!.GetValue<string>());
            Assert.Null(reduction["PendingAfterImage"]);
            var reductionCompanions = reduction["OwnerCompanionAfterImages"]!.AsObject();
            GuardianCappedClosed(reductionCompanions, AfterlifeEntityProfileState.StatePath + " " + AfterlifeSpiritualConflictState.StatePath + " " + SpiritualWoundOpportunityReceiptState.StatePath);
            GuardianCappedClosed(plan["OwnerCompanionAfterImages"]!.AsObject(), AfterlifeSpiritualConflictState.StatePath + " " + SpiritualWoundOpportunityReceiptState.StatePath);
            foreach (var row in plan["OwnerCompanionAfterImages"]!.AsObject()) GuardianCappedEqual(row.Value, reductionCompanions[row.Key]);
            GuardianCappedEqual(reductionCompanions[AfterlifeEntityProfileState.StatePath], effects["AcceptedCarrierBaselines"]!["AfterlifeProfiles"]);
            GuardianCappedEqual(plan["OwnerTransitions"], reduction["OwnerTransitions"]); Assert.Empty(plan["OwnerTransitions"]!.AsArray());
            Assert.Empty(plan["PendingAfterImages"]!.AsObject()); Assert.False(plan["AwaitsPendingResolution"]!.GetValue<bool>()); Assert.False(plan["IsMortalReceiptOnlyWoundPublication"]!.GetValue<bool>());
            foreach (var field in new[] { "PendingGmPacket", "PendingPublicationAuthority", "WoundStageBundle", "LiveWoundProofFingerprint", "WoundAnchorPlanFingerprint", "DirectWoundPublicationAuthority", "TreatmentResourcePublicationAuthority", "JointRepairCommonAuthority", "WoundIdentityAfterImage", "WoundHistoryAfterImage" }) { Assert.True(plan.ContainsKey(field)); Assert.Null(plan[field]); }
            foreach (var field in new[] { "HasMortalProducerCompletion", "HasLiveWoundWork" }) Assert.False(reduction[field]!.GetValue<bool>());
            foreach (var field in new[] { "RequiresSatisfactionReceipt", "HasLiveDecisionReceipt" }) Assert.True(reduction[field]!.GetValue<bool>());
            foreach (var field in new[] { "ResourceTriggerExecutions" }) Assert.Single(resources[field]!.AsArray());
            foreach (var field in new[] { "AcceptedPendingResolutions", "AcceptedReactionExecutions", "AcceptedResolvedPendingRequestIds" }) Assert.Empty(resources[field]!.AsArray());
            Assert.Empty(effects["DeferredRoutingReactions"]!.AsArray()); Assert.False(effects["HasDeferredReactions"]!.GetValue<bool>());
            Assert.Empty(plan["WoundCarrierAfterImages"]!.AsObject()); Assert.Null(reduction["LiveWoundCompletion"]); Assert.True(reduction["Success"]!.GetValue<bool>()); Assert.Empty(reduction["Issues"]!.AsArray()); Assert.True(reduction["ReceiptProofPresent"]!.GetValue<bool>()); Assert.True(resources["IsValid"]!.GetValue<bool>()); Assert.Empty(resources["Issues"]!.AsArray());
            guard = "terminal-current-observations"; AssertGuardianCappedRetrySeals(root);
            guard = "effect-operand-framing";
            var effectFields = ReadGuardianCappedRetryEffectPayloadHashFields(effects); Assert.Equal(effectFields, root["EffectPayloadHashOperands"]!.AsArray().Select(row => row?.GetValue<string>()));
            GuardianCappedEqual(root["EffectPayloadHashOperands"], reduction["EffectPayloadHashOperands"]);
            guard = "prepared-operand-framing";
            var preparedFields = ReadGuardianCappedRetryPreparedPlanHashFields(root, effects["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>());
            Assert.Equal(preparedFields, root["PreparedPlanHashOperands"]!.AsArray().Select(row => row?.GetValue<string>()));
            guard = "effect-payload-hash"; Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(effectFields), effects["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>());
            guard = "prepared-plan-hash"; Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(preparedFields), plan["PreparedPlanFingerprint"]!.GetValue<string>());
            guard = "all-derived-hashes"; var hashes = closedHashes;
            foreach (var hash in hashes) Assert.Equal(hash.Calculated, hash.Value);
            guard = "complete-reference-graph";
            var candidate = BuildGuardianCappedRetryGraph(fixture, json, document, allocations, hashes);
            if (fixture.ReferenceGraph is { } reference)
            {
                guard = "reference-location-inventory";
                Assert.Equal(reference.Bindings.Keys.Order(StringComparer.Ordinal), candidate.Bindings.Keys.Order(StringComparer.Ordinal));
                guard = "full-envelope-invariants"; AssertGuardianCappedGraphEquivalent(reference, candidate);
            }
            else
            {
                GuardianCappedEqual(root, new GuardianCappedRetryDocument(fixture.ReferenceEnvelopeJson).Root);
                fixture.ReferenceGraph = candidate; // Insert only after every complete baseline guard succeeds.
            }
            return candidate;

            void Allocation(int ordinal, string kind, string owner, string coordinate)
            { var row = journal[ordinal]!; Assert.Equal((ordinal, kind, owner, coordinate), (row["ordinal"]!.GetValue<int>(), row["kind"]!.GetValue<string>(), row["owner"]!.GetValue<string>(), row["coordinate"]!.GetValue<string>())); Assert.Matches("^" + kind + "_[0-9a-f]{32}$", allocations[ordinal]); }
        }
        catch (Exception failure) when (failure is not GuardianCappedRetryGuardFailure)
        { failure.Data["GuardianCappedFixtureName"] = fixture.Name; failure.Data["GuardianCappedFixtureRank"] = fixture.Rank; throw new GuardianCappedRetryGuardFailure(guard, failure); }
    }

    /// <summary>Requires every property, including explicit nulls, and rejects all unknown properties.</summary>
    private static void GuardianCappedClosed(JsonObject node, string fields) => Assert.Equal(fields.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal), node.Select(row => row.Key).Order(StringComparer.Ordinal));
    /// <summary>Compares complete detached trees; raw strings and base64 remain exact scalar DATA.</summary>
    private static void GuardianCappedEqual(JsonNode? expected, JsonNode? actual, string? label = null) => Assert.True(JsonNode.DeepEquals(expected, actual), label ?? "Complete detached copy differs.");
    /// <summary>Round-trips existing immutable typed DATA to reject discarded unknown, missing or mistyped fields.</summary>
    private static T ReadGuardianCappedExact<T>(JsonNode? node) where T : class
    { Assert.NotNull(node); var result = JsonSerializer.Deserialize<T>(node!.ToJsonString()); Assert.NotNull(result); GuardianCappedEqual(node, JsonSerializer.SerializeToNode(result)); return result!; }
    /// <summary>Checks path order and exact presence/bytes before exposing a detached image map.</summary>
    private static Dictionary<string, byte[]?> GuardianCappedImages(JsonArray rows)
    {
        Assert.Equal(rows.Select(row => row!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal), rows.Select(row => row!["path"]!.GetValue<string>()));
        return rows.ToDictionary(row => row!["path"]!.GetValue<string>(), row => row!["bytesBase64"] is null ? null : Convert.FromBase64String(row["bytesBase64"]!.GetValue<string>()), StringComparer.Ordinal);
    }
    /// <summary>Reconstructs lifecycle0/1/2, then resource3/4 and terminal5 from complete detached typed evidence.</summary>
    /// <param name="envelope">Full before/after/terminal images.</param>
    /// <param name="transitions">Independently validated complete AP transitions.</param>
    /// <param name="rootId">Exact permanent guardian root.</param>
    /// <param name="effects">Complete detached effect plan whose declared stamp must agree.</param>
    /// <param name="mark">Marks the concrete detached boundary guard without changing authority.</param>
    /// <returns>The single independently reconstructed accepted activation.</returns>
    private static AcceptedEffectActivation AssertGuardianCappedRetryEffectBoundaries(JsonObject envelope,
        ResourceTransition[] transitions, string rootId, JsonObject effects, Action<string> mark)
    {
        mark("effect-prefix-before");
        var before = envelope["Interval"]!["EffectBefore"]!.AsObject(); var after = envelope["Interval"]!["EffectAfter"]!.AsObject(); var terminal = envelope["FullEffectBoundary"]!.AsObject();
        const string prefixKeys = "Fingerprint PlanAuthority Boundaries BoundaryCloses CausalClosures AcceptedActivations RejectedActivations AppliedComponentEvidence ResourceMutations ReleasedReactions TerminalAvailabilityReservations ExpansionUsage";
        GuardianCappedClosed(before, prefixKeys); GuardianCappedClosed(after, prefixKeys); GuardianCappedClosed(terminal, prefixKeys + " ExpansionCount UseProjectionOrdinal PendingFrontierBoundaryOrdinal IsComplete");
        var stamp = ReadGuardianCappedExactData<EffectAcceptedPlanAuthorityStamp>(before["PlanAuthority"]);
        Assert.Equal(new EffectAcceptedPlanAuthorityStamp(effects["InputFingerprint"]!.GetValue<string>(), effects["CarrierAuthorityFingerprint"]!.GetValue<string>(), effects["SourceAuthorityFingerprint"]!.GetValue<string>(), effects["TargetAuthorityFingerprint"]!.GetValue<string>(), effects["SkillScopeFingerprint"]?.GetValue<string>() ?? "none"), stamp);
        var boundary = ReadGuardianCappedExactData<EffectEventBoundaryStamp>(Assert.Single(before["Boundaries"]!.AsArray()));
        var accepted = Assert.Single(before["AcceptedActivations"]!.AsArray())!.AsObject(); GuardianCappedClosed(accepted, "Boundary Activation Candidate CandidateFingerprint MechanicsOrdinal");
        var candidateData = accepted["Candidate"]!.AsObject();
        GuardianCappedClosed(candidateData, "Activation UseSeed EffectAuthority Producer PlannedMutationKeys PlannedComponentIds PlannedComponentIdsByMutation PendingOutputs ReactionOutputs Origin CandidateFingerprint CausalMaterialFingerprint PendingWaveOrdinal ResolvedOutputs");
        foreach (var name in new[] { "UseSeed", "Producer", "CandidateFingerprint", "CausalMaterialFingerprint" }) GuardianCappedExplicitNull(candidateData, name);
        foreach (var name in new[] { "PlannedMutationKeys", "PlannedComponentIds", "PlannedComponentIdsByMutation", "PendingOutputs", "ReactionOutputs", "ResolvedOutputs" }) Assert.Empty(candidateData[name]!.AsArray());
        GuardianCappedClosed(candidateData["Origin"]!.AsObject(), "Mutations ComponentIds ComponentIdsByMutation SourceExports");
        foreach (var row in candidateData["Origin"]!.AsObject()) Assert.Empty(row.Value!.AsArray());
        Assert.Equal(0, candidateData["PendingWaveOrdinal"]!.GetValue<int>());
        var proposedActivation = ReadGuardianCappedExactData<EffectActivationCandidate>(candidateData["Activation"]);
        var authority = new ResourcePendingAuthorityBinding("permanent", rootId);
        Assert.Equal(authority, proposedActivation.EffectAuthority);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(authority), candidateData["EffectAuthority"]));
        var ownerEvent = "turn_46:lifecycle:owner_turn_end:chaos_sea:guardian:" + SpiritualGuardianAttributionTestData.GuardianId;
        var eventRef = EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(ownerEvent, rootId, authority, "on_owner_turn_end");
        Assert.Equal(new EffectActivationCandidate(new(rootId, "on_owner_turn_end", "owner_turn_end", eventRef, ownerEvent), 100, false, authority), proposedActivation);
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(proposedActivation, null, null, [], [],
            new Dictionary<ResourceOperationKey, string>(), [], [], EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(candidate)), candidateData));
        var activation = ReadGuardianCappedExactData<AcceptedEffectActivation>(accepted["Activation"]);
        Assert.Equal(new AcceptedEffectActivation(new(proposedActivation.Identity, authority, 100, false, null, 0), null, false), activation);
        Assert.Equal(1L, accepted["MechanicsOrdinal"]!.GetValue<long>());
        Assert.True(JsonNode.DeepEquals(before["Boundaries"]![0], accepted["Boundary"]));
        var builder = new AcceptedEffectBoundaryTranscript.Builder(stamp);
        var reconstructed = builder.OpenBoundary(boundary.ParentBoundaryOrdinal, boundary.Producer, boundary.EventKind, boundary.ProducerEventRef,
            boundary.ProducerTransitionId, boundary.ProducerExecutionSequence, boundary.ProducerMechanicsOrdinal, [candidate]);
        Assert.Equal(boundary, reconstructed); builder.RecordAccepted(reconstructed, activation, candidate);
        Assert.Null(builder.BindCausalClosure(reconstructed, [], new Dictionary<string, string>(StringComparer.Ordinal))); Assert.Null(builder.CloseBoundary(reconstructed));
        var beforeResult = builder.CaptureClosedPrefix(); Assert.Empty(beforeResult.Issues); Assert.True(beforeResult.IsValid);
        Assert.True(JsonNode.DeepEquals(before, JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(beforeResult.Prefix!))));
        Assert.Empty(before["ResourceMutations"]!.AsArray());
        mark("effect-prefix-after");
        Assert.Equal(2, after["ResourceMutations"]!.AsArray().Count);
        for (var side = 0; side < 2; side++)
        {
            var evidence = ReadGuardianCappedExactData<AcceptedResourceMutationEvidence>(after["ResourceMutations"]![side]);
            var t = transitions[side]; var key = new ResourceOperationKey(t.EventRef, t.OriginKind, t.OriginId, t.Coordinate, ResourceOperation.Spend);
            Assert.Equal((key, t, ResourceMutationExecutionKind.Applied, 3L + side), (evidence.Mutation, evidence.Transition, evidence.ExecutionKind, evidence.MechanicsOrdinal));
            Assert.Equal(evidence.MechanicsOrdinal, builder.RecordMutationExecution());
            builder.RecordResourceMutation(key, t, evidence.ExecutionKind, evidence.MechanicsOrdinal);
        }
        var afterResult = builder.CaptureClosedPrefix(); Assert.Empty(afterResult.Issues); Assert.True(afterResult.IsValid);
        Assert.True(JsonNode.DeepEquals(after, JsonSerializer.SerializeToNode(ReadPressureZeroEffectPrefixDiagnosticData(afterResult.Prefix!))));
        mark("effect-terminal");
        Assert.Equal(5L, builder.SealUseProjection()); var finished = builder.Freeze(); Assert.Empty(finished.Issues); Assert.True(finished.IsValid);
        Assert.True(JsonNode.DeepEquals(terminal, JsonSerializer.SerializeToNode(ReadGuardianZeroTerminalEffectBoundaryDiagnosticData(finished.Transcript!))));
        GuardianCappedExplicitNull(terminal, "PendingFrontierBoundaryOrdinal"); Assert.True(terminal["IsComplete"]!.GetValue<bool>());
        return activation;
    }

    /// <summary>Checks AP3→0 State and immutable History prefix, exact semantic coordinates and all serialized copies.</summary>
    private static void AssertGuardianCappedRetryResources(IReadOnlyDictionary<string, byte[]?> signed, JsonObject root, ResourceTransition[] transitions)
    {
        var plan = root["Plan"]!; var resources = root["Completed"]!["Reduction"]!["Resources"]!;
        var state = plan["StateAfterImage"]!.AsObject(); var history = plan["HistoryAfterImage"]!.AsObject();
        GuardianCappedEqual(state, new GuardianCappedRetryDocument(resources["StateAfterImageJson"]!.GetValue<string>()).Root);
        GuardianCappedEqual(history, new GuardianCappedRetryDocument(resources["HistoryAfterImageJson"]!.GetValue<string>()).Root);
        GuardianCappedEqual(state, plan["ProjectionInput"]!["State"]); GuardianCappedEqual(history, plan["ProjectionInput"]!["History"]);
        GuardianCappedEqual(plan["DefinitionAfterImage"], plan["ProjectionInput"]!["Definitions"]);
        GuardianCappedEqual(plan["DefinitionAfterImage"], new GuardianCappedRetryDocument(root["Completed"]!["Reduction"]!["DefinitionAfterImageJson"]!.GetValue<string>()).Root);
        var oldState = ReadGuardianAttributionJson(signed[ResourceMaterializationContract.StatePath]!); var oldHistory = ReadGuardianAttributionJson(signed[ResourceMaterializationContract.HistoryPath]!);
        Assert.Equal(2, state["entries"]!.AsArray().Count); Assert.Equal(oldHistory["entries"]!.AsArray().Count + transitions.Length, history["entries"]!.AsArray().Count);
        for (var i = 0; i < oldHistory["entries"]!.AsArray().Count; i++) GuardianCappedEqual(oldHistory["entries"]![i], history["entries"]![i]);
        var restored = state.DeepClone();
        for (var i = 0; i < transitions.Length; i++)
        {
            var t = transitions[i]; var index = GuardianCappedIndex(state["entries"]!.AsArray(), row => GuardianCappedCoordinate(row, t.Coordinate));
            var oldIndex = GuardianCappedIndex(oldState["entries"]!.AsArray(), row => GuardianCappedCoordinate(row, t.Coordinate)); Assert.Equal(oldIndex, index);
            var expected = oldState["entries"]![oldIndex]!.DeepClone(); expected["current"] = 0m;
            expected["chronology"]!["lastTransitionId"] = t.TransitionId; expected["chronology"]!["lastEventRef"] = t.EventRef; expected["chronology"]!["lastTransitionTurn"] = 46;
            GuardianCappedEqual(expected, state["entries"]![index]); restored["entries"]![index] = oldState["entries"]![oldIndex]!.DeepClone();
            GuardianCappedEqual(JsonNode.Parse(ResourceHistoryState.SerializeCanonicalTransitions([t]))!["entries"]![0], history["entries"]![oldHistory["entries"]!.AsArray().Count + i]);
        }
        GuardianCappedEqual(oldState, restored);
    }

    /// <summary>Checks the complete retained guardian/root/sibling/history graph and the single new effect-transition join.</summary>
    private static void AssertGuardianCappedRetryCarriers(JsonObject root, IReadOnlyDictionary<string, byte[]?> signed,
        RetainedPressureGroupData retained, string transitionId, AcceptedEffectActivation activation, Action<string> mark)
    {
        var plan = root["Plan"]!; var effects = plan["EffectPlan"]!; var path = AfterlifeEntityProfileState.StatePath;
        var before = effects["CarrierBeforeImages"]!.AsObject();
        var authored = GuardianCappedImages(root["First"]!["authoredInputs"]!.AsArray());
        foreach (var row in before)
        { var bytes = authored.TryGetValue(row.Key, out var replacement) ? replacement : signed.TryGetValue(row.Key, out var original) ? original : null; GuardianCappedEqual(bytes is null ? null : ReadGuardianAttributionJson(bytes), row.Value); }
        GuardianCappedEqual(effects["CarrierAfterImages"], plan["EffectCarrierAfterImages"]); GuardianCappedEqual(effects["IdentityIndexAfterImage"], plan["EffectIdentityAfterImage"]);
        var profileRoot = effects["CarrierAfterImages"]![path]!.AsObject(); var oldProfiles = ReadGuardianAttributionJson(signed[path]!);
        var index = GuardianCappedIndex(profileRoot["profiles"]!.AsArray(), row => row["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId);
        var expectedEffect = Assert.Single(retained.Effects).DeepClone(); expectedEffect["chronology"]!["lastTransitionId"] = transitionId; expectedEffect["chronology"]!["lastTransitionTurn"] = 46;
        GuardianCappedEqual(expectedEffect, Assert.Single(profileRoot["profiles"]![index]!["activeEffects"]!.AsArray())); GuardianCappedEqual(expectedEffect, Assert.Single(effects["ActiveEffects"]!.AsArray()));
        // Raw signed bytes remain anchored separately. The real typed projection supplies required canonical root/profile defaults.
        var expectedProfiles = AfterlifeEntityProfileState.ProjectCanonicalRoot(oldProfiles, previousRoot: null);
        var expectedProfileIndex = GuardianCappedIndex(expectedProfiles["profiles"]!.AsArray(), row => row["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId);
        Assert.Equal(index, expectedProfileIndex);
        Assert.Single(expectedProfiles["profiles"]![expectedProfileIndex]!["activeEffects"]!.AsArray());
        expectedProfiles["profiles"]![expectedProfileIndex]!["activeEffects"]![0] = expectedEffect.DeepClone();
        GuardianCappedEqual(expectedProfiles, profileRoot);
        GuardianCappedEqual(profileRoot, effects["ResourceTriggerCarriers"]!["AfterlifeProfiles"]);
        var carrierPaths = new[] { EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath, path, AfterlifeSpiritualConflictState.StatePath };
        GuardianCappedClosed(before, string.Join(' ', carrierPaths)); GuardianCappedClosed(effects["CarrierAfterImages"]!.AsObject(), path);
        foreach (var field in new[] { "ResourceTriggerCarriers", "AcceptedCarrierBaselines" })
        {
            var carriers = effects[field]!.AsObject(); GuardianCappedClosed(carriers, "PlayerEffects NpcEffects EnemyCombatants AllyCombatants AfterlifeProfiles SpiritualConflict");
            GuardianCappedEqual(field == "ResourceTriggerCarriers" ? profileRoot : root["Completed"]!["Reduction"]!["OwnerCompanionAfterImages"]![path], carriers["AfterlifeProfiles"]);
            GuardianCappedEqual(root["First"]!["actualRoot"], carriers["SpiritualConflict"]);
            foreach (var (key, file) in new[] { ("PlayerEffects", EffectCarrierCatalog.PlayerPath), ("NpcEffects", EffectCarrierCatalog.NpcPath), ("EnemyCombatants", EffectCarrierCatalog.EnemiesPath), ("AllyCombatants", EffectCarrierCatalog.AlliesPath) })
                GuardianCappedEqual(signed.TryGetValue(file, out var bytes) && bytes is not null ? ReadGuardianAttributionJson(bytes) : null, carriers[key]);
        }
        GuardianCappedEqual(effects["IdentityIndexBeforeImage"], ReadGuardianAttributionJson(signed[EffectIdentityState.StatePath]!));
        var oldIdentity = effects["IdentityIndexBeforeImage"]!.DeepClone(); var identity = effects["IdentityIndexAfterImage"]!;
        var oldRows = oldIdentity["entries"]![0]!["transitions"]!.AsArray(); var rows = identity["entries"]![0]!["transitions"]!.AsArray(); Assert.Equal(oldRows.Count + 1, rows.Count);
        for (var i = 0; i < oldRows.Count; i++) GuardianCappedEqual(oldRows[i], rows[i]);
        Assert.Equal((transitionId, activation.Stamp.Identity.EventRef, 46), (rows[^1]!["transitionId"]!.GetValue<string>(), rows[^1]!["eventRef"]!.GetValue<string>(), rows[^1]!["turn"]!.GetValue<int>()));
        Assert.Equal(new[] { transitionId }, effects["AllocatedTransitionIds"]!.AsArray().Select(row => row!.GetValue<string>()));
        mark("effect-identity-current");
        var rootId = expectedEffect["effectId"]!.GetValue<string>();
        var expectedIdentity = effects["IdentityIndexBeforeImage"]!.DeepClone();
        var identityIndex = GuardianCappedIndex(expectedIdentity["entries"]!.AsArray(), row => row["effectId"]!.GetValue<string>() == rootId);
        expectedIdentity["entries"]![identityIndex]!["transitions"]!.AsArray().Add(new JsonObject
        {
            ["transitionId"] = transitionId, ["kind"] = "trigger", ["turn"] = 46, ["eventRef"] = activation.Stamp.Identity.EventRef,
            ["sourceEffectIds"] = new JsonArray(JsonValue.Create(rootId)), ["resultEffectIds"] = new JsonArray(JsonValue.Create(rootId)), ["receiptId"] = null
        });
        GuardianCappedEqual(expectedIdentity, identity);
        var parsedIdentity = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(identity), EffectIdentityState.StatePath); Assert.NotNull(parsedIdentity.State); Assert.Empty(parsedIdentity.Issues);
        mark("effect-resource-trigger");
        var execution = Assert.Single(root["Completed"]!["Reduction"]!["Resources"]!["ResourceTriggerExecutions"]!.AsArray())!;
        GuardianCappedClosed(execution.AsObject(), "EffectId TriggerId EventKind EventRef MutationKeys RemainingUseBudget ComponentIds TriggerEventRef ComponentIdsByMutation");
        Assert.Equal((rootId, "on_owner_turn_end", "owner_turn_end", activation.Stamp.Identity.EventRef, activation.Stamp.Identity.TriggerEventRef),
            (execution["EffectId"]!.GetValue<string>(), execution["TriggerId"]!.GetValue<string>(), execution["EventKind"]!.GetValue<string>(), execution["EventRef"]!.GetValue<string>(), execution["TriggerEventRef"]!.GetValue<string>()));
        GuardianCappedExplicitNull(execution.AsObject(), "RemainingUseBudget");
        foreach (var field in new[] { "MutationKeys", "ComponentIds", "ComponentIdsByMutation" }) Assert.Empty(execution[field]!.AsArray());
        mark("effect-event-input");
        var eventInput = effects["EventInput"]!.AsObject(); GuardianCappedClosed(eventInput, "turn events lifecycleEvents sceneId");
        Assert.Equal(46, eventInput["turn"]!.GetValue<int>()); Assert.Equal("conflict_resource_cost", eventInput["sceneId"]!.GetValue<string>());
        GuardianCappedEqual(new JsonArray(new JsonObject { ["kind"] = "accepted_turn", ["authorityId"] = "turn_46", ["eventRef"] = "turn_46:accepted_effect" }), eventInput["events"]);
        Assert.Equal(3, eventInput["lifecycleEvents"]!.AsArray().Count);
        for (var ordinal = 0; ordinal < 3; ordinal++)
        {
            var owner = ordinal == 0; var side = ordinal == 1 ? "opposition" : "player";
            var expected = new JsonObject
            {
                ["eventRef"] = owner ? activation.Stamp.Identity.TriggerEventRef : "afterlife_exchange:conflict_resource_cost:exchange_guardian_attribution_46:lifecycle:" + side,
                ["turn"] = 46, ["phase"] = owner ? "owner_turn_end" : "afterlife_exchange_end", ["realm"] = "chaos_sea",
                ["target"] = new JsonObject { ["kind"] = owner ? "guardian" : "spiritual_conflict_side", ["targetId"] = owner ? SpiritualGuardianAttributionTestData.GuardianId : "conflict_resource_cost:" + side },
                ["triggerId"] = null, ["currentTime"] = null, ["currentSceneId"] = owner ? null : "conflict_resource_cost", ["sceneClosed"] = false,
                ["sourceSatisfied"] = null, ["conditionSatisfied"] = null, ["targetSatisfied"] = null, ["currentRealm"] = "chaos_sea"
            };
            if (!owner) expected["causalEventRef"] = "afterlife_exchange:conflict_resource_cost:exchange_guardian_attribution_46";
            GuardianCappedEqual(expected, eventInput["lifecycleEvents"]![ordinal]);
        }
        var ledger = effects["LifecycleExecutionLedger"]!; GuardianCappedClosed(ledger.AsObject(), "version inputFingerprint carrierAuthorityFingerprint sourceAuthorityFingerprint targetAuthorityFingerprint eventInputRef initialPlanRef completedPlanRef passes");
        Assert.Equal(1, ledger["version"]!.GetValue<int>());
        foreach (var (field, effectField) in new[] { ("inputFingerprint", "InputFingerprint"), ("carrierAuthorityFingerprint", "CarrierAuthorityFingerprint"), ("sourceAuthorityFingerprint", "SourceAuthorityFingerprint"), ("targetAuthorityFingerprint", "TargetAuthorityFingerprint") })
            Assert.Equal(effects[effectField]!.GetValue<string>(), ledger[field]!.GetValue<string>());
        Assert.Equal(2, ledger["passes"]!.AsArray().Count); Assert.Equal("bound_continuations", ledger["passes"]![0]!["kind"]!.GetValue<string>()); Assert.Equal("final_lifetime", ledger["passes"]![1]!["kind"]!.GetValue<string>());
        Assert.Equal(effects["AcceptedBoundaryBasePlanFingerprint"]!.GetValue<string>(), ledger["initialPlanRef"]!.GetValue<string>()); Assert.Equal(effects["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>(), ledger["completedPlanRef"]!.GetValue<string>());
        Assert.Equal(GuardianCappedLifecycleArtifact("event_input", effects["EventInput"]), ledger["eventInputRef"]!.GetValue<string>());
        mark("lifecycle-event-placement");
        var inputEvents = effects["EventInput"]!["lifecycleEvents"]!.AsArray(); Assert.Equal(3, inputEvents.Count);
        foreach (var pass in ledger["passes"]!.AsArray())
        {
            GuardianCappedClosed(pass!.AsObject(), "kind sourceEventCount eventInputRef sourceAuthorityFingerprint initialRosterRef initialRoster events");
            var isFinal = pass["kind"]!.GetValue<string>() == "final_lifetime";
            var expected = isFinal ? expectedEffect : Assert.Single(retained.Effects);
            var subject = new JsonObject
            {
                ["effectId"] = expected["effectId"]!.DeepClone(), ["filePath"] = path,
                ["jsonPath"] = path + ".profiles[" + index + "].activeEffects[0]",
                ["coordinate"] = new JsonObject { ["kind"] = "afterlife_profile", ["ownerId"] = SpiritualGuardianAttributionTestData.GuardianId, ["path"] = path, ["category"] = null },
                ["beforeImage"] = expected.DeepClone(), ["beforeImageRef"] = GuardianCappedLifecycleArtifact("effect_image", expected)
            };
            GuardianCappedEqual(new JsonArray(subject.DeepClone()), pass["initialRoster"]);
            Assert.Equal(3, pass["sourceEventCount"]!.GetValue<int>()); Assert.Equal(3, pass["events"]!.AsArray().Count);
            Assert.Equal(ledger["eventInputRef"]!.GetValue<string>(), pass["eventInputRef"]!.GetValue<string>());
            if (isFinal) GuardianCappedExplicitNull(pass.AsObject(), "sourceAuthorityFingerprint");
            else Assert.Equal(effects["SourceAuthorityFingerprint"]!.GetValue<string>(), pass["sourceAuthorityFingerprint"]!.GetValue<string>());
            for (var ordinal = 0; ordinal < inputEvents.Count; ordinal++)
            {
                var ev = pass["events"]![ordinal]!; var input = inputEvents[ordinal]!.AsObject();
                GuardianCappedClosed(ev.AsObject(), "inputOrdinal sourceEventRef parsedAuthority rosterRef roster subjects disposition");
                Assert.Equal(ordinal, ev["inputOrdinal"]!.GetValue<int>()); Assert.Equal(input["eventRef"]!.GetValue<string>(), ev["sourceEventRef"]!.GetValue<string>());
                var parsed = input.DeepClone().AsObject(); parsed["causalEventRef"] = input["causalEventRef"]?.DeepClone(); parsed["effectId"] = null;
                GuardianCappedEqual(parsed, ev["parsedAuthority"]);
                if (ordinal != 0)
                {
                    Assert.Empty(ev["roster"]!.AsArray()); Assert.Empty(ev["subjects"]!.AsArray()); Assert.Equal("no_eligible", ev["disposition"]!.GetValue<string>()); continue;
                }
                Assert.Equal(("guardian", SpiritualGuardianAttributionTestData.GuardianId), (parsed["target"]!["kind"]!.GetValue<string>(), parsed["target"]!["targetId"]!.GetValue<string>()));
                GuardianCappedEqual(new JsonArray(subject.DeepClone()), ev["roster"]);
                var expectedSubject = subject.DeepClone().AsObject(); expectedSubject["disposition"] = isFinal ? "other_pass" : "evaluated";
                Assert.Equal(isFinal ? "no_eligible" : "evaluated", ev["disposition"]!.GetValue<string>());
                if (!isFinal)
                {
                    var scheduler = parsed.DeepClone().AsObject(); scheduler.Remove("realm"); scheduler.Remove("target"); scheduler.Remove("effectId");
                    scheduler["eventRef"] = EffectAcceptedTurnPlanner.CreateLifecycleTransitionEventRef(input["eventRef"]!.GetValue<string>(), expected["effectId"]!.GetValue<string>(), new ResourcePendingAuthorityBinding("permanent", expected["effectId"]!.GetValue<string>()));
                    scheduler["sourceSatisfied"] = true;
                    expectedSubject["schedulerEvent"] = scheduler;
                    expectedSubject["resolvedSourceWitness"] = new JsonObject { ["kind"] = "source_catalog", ["catalogFingerprint"] = effects["SourceAuthorityFingerprint"]!.DeepClone(), ["binding"] = expected["source"]!.DeepClone(), ["satisfied"] = true };
                    expectedSubject["outcome"] = "no_change"; expectedSubject["afterImage"] = expected.DeepClone();
                    expectedSubject["afterImageRef"] = GuardianCappedLifecycleArtifact("effect_image", expected); expectedSubject["transitionRefs"] = new JsonArray();
                }
                GuardianCappedEqual(new JsonArray(expectedSubject), ev["subjects"]);
            }
        }

    }

    /// <summary>Checks positive transport phase transitions, complete physical image joins and schema1 receipt, never source-only zero assumptions.</summary>
    private static void AssertGuardianCappedRetryTransport(JsonObject root, IReadOnlyDictionary<string, byte[]?> signed, IReadOnlyDictionary<string, byte[]?> authored)
    {
        var first = root["First"]!; var completed = root["Completed"]!; var reduction = completed["Reduction"]!; var witness = first["Witness"]!.AsObject();
        var initialRoot = new GuardianCappedRetryDocument(first["CommittedPendingJson"]!.GetValue<string>()).Root.AsObject(); var initial = initialRoot["pending"]!.AsObject();
        var initialCheckpointRoot = new GuardianCappedRetryDocument(first["CommittedCheckpointJson"]!.GetValue<string>()).Root.AsObject(); var initialCheckpoint = initialCheckpointRoot["checkpoint"]!;
        var pending = completed["CompletedPending"]!.AsObject(); var checkpoint = completed["CompletedCheckpoint"]!.AsObject();
        Assert.Equal(first["FirstPendingJson"]!.GetValue<string>(), first["CommittedPendingJson"]!.GetValue<string>());
        Assert.Equal(first["CommittedPendingJson"]!.GetValue<string>(), completed["ColdOfferReplay"]!["ReconstructedPendingJson"]!.GetValue<string>());
        GuardianCappedEqual(first["Source"], new GuardianCappedRetryDocument(completed["ColdOfferReplay"]!["ActualSourceDataJson"]!.GetValue<string>()).Root);
        GuardianCappedEqual(new GuardianCappedRetryDocument(first["AllocationJournalJson"]!.GetValue<string>()).Root, new GuardianCappedRetryDocument(completed["ColdOfferReplay"]!["AllocationJournalJson"]!.GetValue<string>()).Root);
        Assert.Equal(first["AllocationCursor"]!.GetValue<int>(), completed["ColdOfferReplay"]!["AllocationCursor"]!.GetValue<int>());
        Assert.Equal(Convert.ToBase64String(authored[AfterlifeSpiritualConflictState.StatePath]!), completed["ColdOfferReplay"]!["OriginalConflictBytesBase64"]!.GetValue<string>());
        Assert.Equal(new CanonicalBeforeImage(true, authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, completed["ColdOfferReplay"]!["OriginalConflictFingerprint"]!.GetValue<string>());
        GuardianCappedEqual(first["Actual"], new GuardianCappedRetryDocument(first["Source"]!["ExchangeJson"]!.GetValue<string>()).Root);
        GuardianCappedClosed(first["Source"]!.AsObject(), "Coordinate ConflictId ExchangeId AffectedSide ActingActor AffectedActor Operation OriginalSpecialArtJson ExchangeJson Calculation GuaranteedSeverityRank RetraumaWoundId RetraumaWoundJson StrainPreimageJson");
        foreach (var field in new[] { "OriginalSpecialArtJson", "GuaranteedSeverityRank", "RetraumaWoundId", "RetraumaWoundJson" }) GuardianCappedExplicitNull(first["Source"]!.AsObject(), field);
        Assert.Equal(SpiritualWoundStateJson.Canonical(first["Actual"]), Encoding.UTF8.GetString(Convert.FromBase64String(witness["exchangeJsonBase64"]!.GetValue<string>())));
        GuardianCappedEqual(first["ActualCalculation"], first["Source"]!["Calculation"]);
        GuardianCappedEqual(witness["strainPreimage"], new GuardianCappedRetryDocument(first["Source"]!["StrainPreimageJson"]!.GetValue<string>()).Root);
        var preimage = witness["strainPreimage"]!;
        Assert.Equal(new CanonicalBeforeImage(true, authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, preimage["originalInput"]!["contentFingerprint"]!.GetValue<string>());
        Assert.Equal("/activeConflict/exchangeLog/0", preimage["originalInput"]!["exchangePointer"]!.GetValue<string>());
        GuardianCappedEqual(first["Ordinary"], ReadGuardianAttributionJson(Convert.FromBase64String(preimage["ordinaryExchangeJsonBase64"]!.GetValue<string>())));
        GuardianCappedEqual(Assert.Single(first["Retained"]!["Parents"]!.AsArray()), ReadGuardianAttributionJson(Convert.FromBase64String(preimage["parentWoundJsonBase64"]!.GetValue<string>())));
        _ = SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain); Assert.NotNull(strain); Assert.Equal(5, preimage["version"]!.GetValue<int>());
        var registeredPaths = initial["beforeImages"]!.AsArray().Select(row => row!["path"]!.GetValue<string>()).ToArray();
        var draftPaths = checkpoint["originalDraftImages"]!.AsArray().Select(row => row!["path"]!.GetValue<string>()).ToArray();
        foreach (var packet in new[] { initialRoot, new JsonObject { ["schemaVersion"] = 1, ["pending"] = pending.DeepClone() } })
        { var result = SpiritualWoundDecisionPendingState.Parse(packet.ToJsonString(), SpiritualWoundDecisionPendingState.StatePath, registeredPaths); Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues)); }
        foreach (var cp in new[] { initialCheckpointRoot, new JsonObject { ["schemaVersion"] = 1, ["checkpoint"] = checkpoint.DeepClone() } })
        { var result = SpiritualWoundCaptureCheckpointState.Parse(cp.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, draftPaths); Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues)); }
        GuardianCappedEqual(initial["beforeImages"], pending["beforeImages"]);
        var firstCandidates = initial["candidateAfterImages"]!.AsArray(); var completedCandidates = pending["candidateAfterImages"]!.AsArray();
        Assert.Equal(firstCandidates.Count, completedCandidates.Count);
        for (var index = 0; index < firstCandidates.Count; index++)
        {
            var image = firstCandidates[index]!; var next = completedCandidates[index]!; Assert.Equal(image["path"]!.GetValue<string>(), next["path"]!.GetValue<string>());
            if (image["path"]!.GetValue<string>() != AcceptedMechanicsPlan.WoundCommandPath) GuardianCappedEqual(image, next);
        }
        foreach (var packet in new[] { initial, pending })
            foreach (var (path, field) in new[] { (ResourceMaterializationContract.StatePath, "StateAfterImageJson"), (ResourceMaterializationContract.HistoryPath, "HistoryAfterImageJson") })
            {
                var image = Assert.Single(packet["candidateAfterImages"]!.AsArray(), row => row!["path"]!.GetValue<string>() == path)!;
                Assert.Equal(Encoding.UTF8.GetBytes(reduction["Resources"]![field]!.GetValue<string>()), Convert.FromBase64String(image["contentBase64"]!.GetValue<string>()));
            }
        GuardianCappedEqual(witness, Assert.Single(initial["sources"]!.AsArray())); GuardianCappedEqual(witness, Assert.Single(pending["sources"]!.AsArray()));
        Assert.Empty(initial["stagedDecisions"]!.AsArray()); GuardianCappedEqual(completed["NoneDecision"], Assert.Single(pending["stagedDecisions"]!.AsArray()));
        Assert.Equal("none", completed["NoneDecision"]!["decision"]!.GetValue<string>()); foreach (var field in new[] { "selectedSeverityRank", "woundDraftBase64", "woundDraftFingerprint" }) Assert.Null(completed["NoneDecision"]![field]);
        Assert.Equal((0, 1, 4, 4), (initialCheckpoint["committedAdvance"]!.GetValue<int>(), checkpoint["committedAdvance"]!.GetValue<int>(), initialCheckpoint["initialAllocationCount"]!.GetValue<int>(), checkpoint["initialAllocationCount"]!.GetValue<int>()));
        GuardianCappedEqual(new GuardianCappedRetryDocument(first["AllocationJournalJson"]!.GetValue<string>()).Root, initialCheckpoint["allocations"]);
        GuardianCappedEqual(completed["AllocationJournal"], checkpoint["allocations"]); GuardianCappedEqual(initialCheckpoint["physicalWitnesses"], checkpoint["physicalWitnesses"]); GuardianCappedEqual(initialCheckpoint["originalDraftImages"], checkpoint["originalDraftImages"]);
        Assert.Equal(initial["packetFingerprint"]!.GetValue<string>(), checkpoint["initialPendingPacketFingerprint"]!.GetValue<string>()); Assert.Equal(pending["packetFingerprint"]!.GetValue<string>(), checkpoint["expectedPendingPacketFingerprint"]!.GetValue<string>());
        var advance = Assert.Single(checkpoint["advances"]!.AsArray())!; Assert.Equal(5, advance["allocationCount"]!.GetValue<int>()); Assert.Equal(1, advance["ordinal"]!.GetValue<int>());
        Assert.Equal(completed["NoneDecision"]!["decisionFingerprint"]!.GetValue<string>(), Assert.Single(advance["newDecisionFingerprints"]!.AsArray())!.GetValue<string>());
        GuardianCappedEqual(root["Plan"]!["OwnerCompanionAfterImages"]![AfterlifeSpiritualConflictState.StatePath], first["actualRoot"]);
        var receipt = reduction["ReceiptAfterImage"]!.AsObject(); Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>());
        var parsedReceipt = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath); Assert.True(parsedReceipt.IsValid, JsonSerializer.Serialize(parsedReceipt.Issues));
        GuardianCappedEqual(receipt, root["Plan"]!["OwnerCompanionAfterImages"]![SpiritualWoundOpportunityReceiptState.StatePath]); GuardianCappedEqual(receipt, reduction["OwnerCompanionAfterImages"]![SpiritualWoundOpportunityReceiptState.StatePath]);
        var prior = ReadGuardianAttributionJson(signed[SpiritualWoundOpportunityReceiptState.StatePath]!);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
        {
            var old = prior[field]!.AsArray(); var current = receipt[field]!.AsArray(); Assert.Equal(old.Count + (field is "instances" or "sources" or "decisions" ? 1 : 0), current.Count);
            for (var i = 0; i < old.Count; i++) GuardianCappedEqual(old[i], current[i]);
        }
        // This positive first offer creates exactly one lawful instance from the signed history, not another injury/source.
        foreach (var field in new[] { "realm", "sessionId", "requestId", "snapshotToken", "turn" })
        { GuardianCappedEqual(initial[field], pending[field]); GuardianCappedEqual(pending[field], witness["turnEvidence"]![field]); }
        var receiptBytes = signed[SpiritualWoundOpportunityReceiptState.StatePath]; var conflictBytes = signed[AfterlifeSpiritualConflictState.StatePath];
        var resolvedInstance = SpiritualWoundFirstOfferInstanceResolver.Resolve(new CanonicalBeforeImage(receiptBytes is not null, receiptBytes),
            new CanonicalBeforeImage(conflictBytes is not null, conflictBytes), pending["realm"]!.GetValue<string>(), pending["sessionId"]!.GetValue<string>(),
            pending["requestId"]!.GetValue<string>(), pending["snapshotToken"]!.GetValue<string>(), pending["turn"]!.GetValue<int>());
        Assert.Empty(resolvedInstance.Issues); Assert.NotNull(resolvedInstance.InstanceId); Assert.NotNull(resolvedInstance.ProposedRow);
        GuardianCappedEqual(resolvedInstance.ProposedRow, receipt["instances"]!.AsArray()[^1]);
        Assert.Equal(resolvedInstance.InstanceId, initial["conflictInstanceRef"]!.GetValue<string>());
        Assert.Equal(resolvedInstance.InstanceId, pending["conflictInstanceRef"]!.GetValue<string>());
        Assert.Equal(resolvedInstance.InstanceId, receipt["sources"]!.AsArray()[^1]!["instanceId"]!.GetValue<string>());
        Assert.Equal(resolvedInstance.InstanceId, receipt["decisions"]!.AsArray()[^1]!["instanceId"]!.GetValue<string>());
        Assert.Equal(prior["nextInstanceOrdinal"]!.GetValue<int>() + 1, receipt["nextInstanceOrdinal"]!.GetValue<int>());
        GuardianCappedEqual(witness, receipt["sources"]!.AsArray()[^1]!["witness"]); GuardianCappedEqual(witness, receipt["decisions"]!.AsArray()[^1]!["sourceWitness"]);
        Assert.Equal("none", receipt["decisions"]!.AsArray()[^1]!["decision"]!.GetValue<string>());
        foreach (var (phase, expectedPending, expectedCheckpoint) in new[] { (first, (JsonNode)initial, initialCheckpoint), (completed, (JsonNode)pending, (JsonNode)checkpoint) })
        {
            var transport = phase["Transport"]!.AsArray(); Assert.Equal(new[] { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath }, transport.Select(row => row!["path"]!.GetValue<string>()));
            GuardianCappedEqual(expectedCheckpoint, ReadGuardianCappedTransportRoot(transport, SpiritualWoundCaptureCheckpointState.StatePath)["checkpoint"]);
            GuardianCappedEqual(expectedPending, ReadGuardianCappedTransportRoot(transport, SpiritualWoundDecisionPendingState.StatePath)["pending"]);
        }
        var publication = GuardianCappedImages(root["PublicationInputs"]!.AsArray()); var transportImages = completed["Transport"]!.AsArray();
        foreach (var row in transportImages) Assert.Equal(Convert.FromBase64String(row!["bytesBase64"]!.GetValue<string>()), publication[row["path"]!.GetValue<string>()]);
        GuardianCappedEqual(root["SignedRollbackImages"], first["signedOriginalTracked"]);
    }

    /// <summary>Requires every terminal/current Boolean and its full diagnostic list, while retaining these as observations only.</summary>
    private static void AssertGuardianCappedRetrySeals(JsonObject root)
    {
        var first = root["First"]!; Assert.True(first["captureCurrent"]!.GetValue<bool>()); Assert.True(first["leaseActive"]!.GetValue<bool>());
        var cold = root["Completed"]!["ColdScope"]!.AsObject(); GuardianCappedClosed(cold, "warmCaptureCurrent warmLeaseActive freshValidator freshLease");
        Assert.False(cold["warmCaptureCurrent"]!.GetValue<bool>()); Assert.False(cold["warmLeaseActive"]!.GetValue<bool>()); Assert.True(cold["freshValidator"]!.GetValue<bool>()); Assert.True(cold["freshLease"]!.GetValue<bool>());
        var effects = root["Plan"]!["EffectPlan"]!; Assert.True(effects["IsAcceptedBoundaryComplete"]!.GetValue<bool>());
        Assert.True(root["Completed"]!["Reduction"]!["EffectPlan"]!["IsAcceptedBoundaryComplete"]!.GetValue<bool>());
        var completed = root["Completed"]!["CurrentTerminalSeals"]!.AsObject(); var current = root["CurrentTerminalSeals"]!.AsObject();
        GuardianCappedClosed(completed, "Fingerprint IsComplete UseProjectionOrdinal PendingFrontierBoundaryOrdinal IsAcceptedBoundaryComplete AcceptedBoundaryBasePlanFingerprint AcceptedBoundaryFinalPlanFingerprint leaseActive publicationRegistered");
        GuardianCappedClosed(current, "validSeal currentInputsAgree CurrentInputIssues publicationRegistered registeredExactPlan leaseActive IsAcceptedBoundaryComplete AcceptedBoundaryBasePlanFingerprint AcceptedBoundaryFinalPlanFingerprint LiveWoundCompletionPresent");
        foreach (var field in new[] { "validSeal", "currentInputsAgree", "publicationRegistered", "registeredExactPlan", "leaseActive", "IsAcceptedBoundaryComplete" }) Assert.True(current[field]!.GetValue<bool>());
        Assert.Empty(current["CurrentInputIssues"]!.AsArray()); Assert.False(current["LiveWoundCompletionPresent"]!.GetValue<bool>());
        var boundary = root["Completed"]!["Reduction"]!["Resources"]!["FullEffectBoundary"]!;
        GuardianCappedEqual(boundary["UseProjectionOrdinal"], completed["UseProjectionOrdinal"]); GuardianCappedEqual(boundary["PendingFrontierBoundaryOrdinal"], completed["PendingFrontierBoundaryOrdinal"]);
        Assert.Equal(effects["IsAcceptedBoundaryComplete"]!.GetValue<bool>(), completed["IsAcceptedBoundaryComplete"]!.GetValue<bool>());
        Assert.True(completed["IsComplete"]!.GetValue<bool>()); Assert.True(completed["leaseActive"]!.GetValue<bool>()); Assert.False(completed["publicationRegistered"]!.GetValue<bool>());
        foreach (var seals in new[] { completed, current }) foreach (var field in new[] { "AcceptedBoundaryBasePlanFingerprint", "AcceptedBoundaryFinalPlanFingerprint" }) Assert.Equal(effects[field]!.GetValue<string>(), seals[field]!.GetValue<string>());
        Assert.Equal(root["Completed"]!["Reduction"]!["Resources"]!["FullEffectBoundary"]!["Fingerprint"]!.GetValue<string>(), completed["Fingerprint"]!.GetValue<string>());
    }

    /// <summary>Finds exactly one complete semantic resource or actor coordinate.</summary>
    private static int GuardianCappedIndex(JsonArray rows, Func<JsonObject, bool> predicate) => Assert.Single(rows.Select((row, index) => (row: row!.AsObject(), index)).Where(pair => predicate(pair.row))).index;
    /// <summary>Matches all resource-coordinate fields using the real owner-kind token.</summary>
    private static bool GuardianCappedCoordinate(JsonObject row, ResourceCoordinate coordinate) => row["realm"]!.GetValue<string>() == coordinate.Realm && row["ownerKind"]!.GetValue<string>() == ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind) && row["resourceOwnerId"]!.GetValue<string>() == coordinate.ResourceOwnerId && row["resourceKey"]!.GetValue<string>() == coordinate.ResourceKey;
    /// <summary>Requires an explicitly present null, not a missing property.</summary>
    private static void GuardianCappedExplicitNull(JsonObject row, string field) { Assert.True(row.ContainsKey(field)); Assert.Null(row[field]); }
    /// <summary>Round-trips exact immutable positional evidence used by the boundary transcript producer.</summary>
    private static T ReadGuardianCappedExactData<T>(JsonNode? node) where T : class => ReadGuardianCappedExact<T>(node);
}
