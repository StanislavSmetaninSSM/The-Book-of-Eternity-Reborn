using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One scalar location, with an optional structural pointer inside an exactly retained JSON string.</summary>
    /// <param name="Outer">Exact outer RFC6901 pointer.</param>
    /// <param name="Inner">Optional exact pointer inside one explicitly encoded JSON string.</param>
    private sealed record GuardianZeroRetryLocation(string Outer, string? Inner = null);

    /// <summary>Immutable complete attempt DATA; reported slots and bindings are rederived before they are trusted.</summary>
    /// <param name="EnvelopeJson">Complete immutable serialized DATA, without timing labels or owner handles.</param>
    /// <param name="Allocations">Reported semantic allocation slots, revalidated from the envelope.</param>
    /// <param name="Bindings">Reported scalar bindings, never accepted without regeneration.</param>
    private sealed record GuardianZeroRetryAttemptData(string EnvelopeJson, string[] Allocations,
        IReadOnlyDictionary<GuardianZeroRetryLocation, string> Bindings);

    /// <summary>Identifies the exact detached contract guard while retaining its complete original diagnostic.</summary>
    private sealed class GuardianZeroRetryGuardFailure : Exception
    {
        /// <summary>Preserves the complete original contract exception and every Data entry under one exact guard.</summary>
        /// <param name="guard">Concrete validation guard which rejected the candidate.</param>
        /// <param name="inner">Original contract exception, without truncation or filtering.</param>
        internal GuardianZeroRetryGuardFailure(string guard, Exception inner) : base("Guardian retry guard: " + guard, inner)
        {
            Guard = guard;
            foreach (System.Collections.DictionaryEntry entry in inner.Data) Data[entry.Key] = entry.Value;
        }

        /// <summary>Gets the exact reader guard; this is diagnostic DATA, not source authority.</summary>
        internal string Guard { get; }
    }

    /// <summary>Validates a full genuine parent-IV envelope, all original operands and every changing scalar before constructing its map.</summary>
    /// <param name="setup">The same signed/authored fixture for both attempts, never a replacement session.</param>
    /// <param name="origin">Complete physical signed origin captured before authoring.</param>
    /// <param name="json">Complete immutable prepared envelope, not a caller-supplied map.</param>
    /// <returns>Full untrimmed DATA and the independently checked five-ID/seventy-four-position map.</returns>
    private static GuardianZeroRetryAttemptData ReadGuardianZeroRetryAttempt(GuardianZeroPressureSetup setup,
        PressureGroupSignedOrigin origin, string json)
    {
        var activeGuard = "fixture-scope";
        try
        {
            Assert.Equal((4, 4), (setup.Data.Rank, setup.Data.Resilience));
            activeGuard = "envelope-json";
            var envelope = ReadGuardianZeroStrictJson(json).AsObject();
            activeGuard = "envelope-schema";
            GuardianZeroClosed(envelope, "phase Name Rank AllocationJournalJson AllocationCursor ordinaryBytesBase64 originalConflictBytesBase64 originalReceiptBytesBase64 originalHistoryBytesBase64 actualRoot witness receipt Coordinate ConflictId ExchangeId AffectedSide ActingActor AffectedActor Operation OriginalSpecialArtJson ExchangeJson Calculation GuaranteedSeverityRank RetraumaWoundId RetraumaWoundJson StrainPreimageJson Interval ResourceStateAfterImage ResourceHistoryAfterImage AppliedTransitions ReplayTransitions Events ResourceTriggerExecutions FullEffectBoundary EffectPlan InputFingerprint PreparedPlanFingerprint TouchedPaths ConsumedPaths BeforeImages WoundCarrierAfterImages EffectCarrierAfterImages OwnerCompanionAfterImages EffectIdentityAfterImage StateAfterImage HistoryAfterImage ResourceEvents PreparedHashOperands");
            foreach (var row in envelope)
                if (row.Key is not ("OriginalSpecialArtJson" or "GuaranteedSeverityRank" or "RetraumaWoundId" or "RetraumaWoundJson")) Assert.NotNull(row.Value);
            Assert.Equal("guardian_zero_complete_prepared_data_envelope", envelope["phase"]!.GetValue<string>());
            Assert.Equal(setup.Name, envelope["Name"]!.GetValue<string>()); Assert.Equal(4, envelope["Rank"]!.GetValue<int>());
            activeGuard = "original-raw";
            foreach (var (name, bytes) in new[] { ("ordinaryBytesBase64", setup.RawOrdinaryBytes), ("originalConflictBytesBase64", setup.OriginalConflictBytes),
                ("originalReceiptBytesBase64", setup.OriginalReceiptBytes), ("originalHistoryBytesBase64", setup.OriginalHistoryBytes) })
                Assert.Equal(Convert.ToBase64String(bytes), envelope[name]!.GetValue<string>());
            activeGuard = "actual-root";
            var actual = setup.OrdinaryRoot.DeepClone().AsObject();
            actual["activeConflict"]!["exchangeLog"]![0] = setup.Data.Actual.DeepClone(); actual["activeConflict"]!["oppositionSideStrain"] = "overwhelmed";
            Assert.True(JsonNode.DeepEquals(actual, envelope["actualRoot"]));
            Assert.True(JsonNode.DeepEquals(setup.Data.Actual, ReadGuardianZeroStrictJson(envelope["ExchangeJson"]!.GetValue<string>())));
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(setup.Data.ActualCalculation), envelope["Calculation"]));
            foreach (var name in new[] { "OriginalSpecialArtJson", "GuaranteedSeverityRank", "RetraumaWoundId", "RetraumaWoundJson" }) GuardianZeroExplicitNull(envelope, name);
            Assert.Equal(("activeConflict.exchangeLog[0]", "conflict_resource_cost", "exchange_guardian_zero_46", "opposition", "player_soul:player_soul", "guardian:guardian_frame", "pressure"),
                (envelope["Coordinate"]!.GetValue<string>(), envelope["ConflictId"]!.GetValue<string>(), envelope["ExchangeId"]!.GetValue<string>(), envelope["AffectedSide"]!.GetValue<string>(),
                    envelope["ActingActor"]!.GetValue<string>(), envelope["AffectedActor"]!.GetValue<string>(), envelope["Operation"]!.GetValue<string>()));
            var receipt = envelope["receipt"]!.AsObject(); var witness = envelope["witness"]!.AsObject();
            activeGuard = "source-hash";
            Assert.NotNull(witness["sourceFingerprint"]); Assert.NotNull(witness["sourceId"]);
            var checkedSourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
            Assert.Equal(checkedSourceHash, witness["sourceFingerprint"]!.GetValue<string>());
            Assert.Equal("spiritual_source_" + checkedSourceHash[7..], witness["sourceId"]!.GetValue<string>());
            activeGuard = "receipt-source";
            AssertGuardianZeroSourceOnlyReceipt(setup, receipt, actual);
            Assert.True(JsonNode.DeepEquals(witness, Assert.Single(receipt["strainComparisons"]!.AsArray())!["witness"]));
            Assert.True(JsonNode.DeepEquals(witness["strainPreimage"], ReadGuardianZeroStrictJson(envelope["StrainPreimageJson"]!.GetValue<string>())));
            Assert.Equal(SpiritualWoundStateJson.Hash(ReadGuardianAttributionJson(setup.OriginalConflictBytes), "closed_conflict"), witness["conflictBeforeFingerprint"]!.GetValue<string>());
            Assert.Equal(SpiritualWoundStateJson.Hash(actual, "closed_conflict"), witness["conflictAfterFingerprint"]!.GetValue<string>());
            Assert.Equal(SpiritualWoundStateJson.Hash(new JsonObject { ["dangerMode"] = setup.OriginalConflict["activeConflict"]!["dangerMode"]!.DeepClone() }, "danger_declaration"), witness["dangerDeclarationFingerprint"]!.GetValue<string>());
            var companions = envelope["OwnerCompanionAfterImages"]!.AsObject();
            GuardianZeroClosed(companions, AfterlifeSpiritualConflictState.StatePath + " " + SpiritualWoundOpportunityReceiptState.StatePath);
            Assert.True(JsonNode.DeepEquals(actual, companions[AfterlifeSpiritualConflictState.StatePath]));
            Assert.True(JsonNode.DeepEquals(receipt, companions[SpiritualWoundOpportunityReceiptState.StatePath]));
            activeGuard = "signed-before-images";
            AssertGuardianZeroRetryBeforeImages(setup, origin, envelope);
            var signedManifest = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, "game_state/control/pending_turn_snapshot.json")!);
            var signedRequest = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, "input/turn_request.json")!);
            var turnEvidence = witness["turnEvidence"]!.AsObject();
            foreach (var name in new[] { "sessionId", "requestId" })
            { Assert.Equal(signedManifest[name]!.GetValue<string>(), turnEvidence[name]!.GetValue<string>()); Assert.Equal(signedRequest[name]!.GetValue<string>(), turnEvidence[name]!.GetValue<string>()); }
            var snapshotToken = signedManifest["manifestPayloadHash"]!.GetValue<string>();
            Assert.Equal(snapshotToken, turnEvidence["snapshotToken"]!.GetValue<string>());
            Assert.Equal("sha256:" + snapshotToken.ToLowerInvariant(), turnEvidence["originalSnapshotFingerprint"]!.GetValue<string>());
            Assert.Equal((46, 46, "chaos_sea"), (signedManifest["turnNumber"]!.GetValue<int>(), signedRequest["turnNumber"]!.GetValue<int>(), turnEvidence["realm"]!.GetValue<string>()));
            Assert.True(JsonNode.DeepEquals(signedManifest["preGeneratedDices1d20"], turnEvidence["acceptedD20Values"]));
            Assert.True(JsonNode.DeepEquals(signedRequest["preGeneratedDices1d20"], turnEvidence["acceptedD20Values"]));
            // The complete imagePathCount remains the genuine C1 owner's frozen inventory claim and an exact retry invariant.
            // It is never replaced by a DATA-only fixture count or inferred from this smaller common-plan before-image subset.
            activeGuard = "prepared-schema";
            var prepared = envelope["PreparedHashOperands"]!.AsObject();
            GuardianZeroClosed(prepared, "DefinitionAfterImage AuthorityFingerprints PendingAfterImages PendingGmPacket PendingPublicationAuthority OwnerTransitions ProjectionInput OwnerAuthorityFingerprint WoundStageBundle LiveWoundProofFingerprint WoundAnchorPlanFingerprint DirectWoundPublicationAuthority TreatmentResourcePublicationAuthority JointRepairCommonAuthority WoundIdentityAfterImage WoundHistoryAfterImage");
            foreach (var name in new[] { "PendingGmPacket", "PendingPublicationAuthority", "WoundStageBundle", "LiveWoundProofFingerprint", "WoundAnchorPlanFingerprint", "DirectWoundPublicationAuthority", "TreatmentResourcePublicationAuthority", "JointRepairCommonAuthority", "WoundIdentityAfterImage", "WoundHistoryAfterImage" }) GuardianZeroExplicitNull(prepared, name);
            Assert.Empty(prepared["PendingAfterImages"]!.AsObject()); Assert.Empty(prepared["OwnerTransitions"]!.AsArray());
            Assert.Empty(envelope["WoundCarrierAfterImages"]!.AsObject());
            foreach (var row in prepared["AuthorityFingerprints"]!.AsArray()) GuardianZeroClosed(row!.AsObject(), "Key Value");
            var projection = prepared["ProjectionInput"]!.AsObject(); GuardianZeroClosed(projection, "Definitions State History OwnerAuthorityFingerprint");
            Assert.True(JsonNode.DeepEquals(ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, ResourceMaterializationContract.DefinitionsPath)!), prepared["DefinitionAfterImage"]));
            Assert.True(JsonNode.DeepEquals(prepared["DefinitionAfterImage"], projection["Definitions"]));
            activeGuard = "journal-schema";
            var journal = ReadGuardianZeroStrictJson(envelope["AllocationJournalJson"]!.GetValue<string>()).AsArray();
            Assert.Equal((5, 5), (journal.Count, envelope["AllocationCursor"]!.GetValue<int>()));
            foreach (var row in journal)
            { GuardianZeroClosed(row!.AsObject(), "ordinal kind owner coordinate value"); foreach (var field in row.AsObject()) Assert.NotNull(field.Value); }
            activeGuard = "journal-uniqueness";
            Assert.Equal(5, journal.Select(row => row!["value"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
            activeGuard = "interval-schema";
            var allocations = new string[5]; var bindings = new Dictionary<GuardianZeroRetryLocation, string>();
            var interval = envelope["Interval"]!.AsObject();
            GuardianZeroClosed(interval, "ConflictId ExchangeId Ordinal AppliedRange ReplayRange EventRange AppliedTransitions ReplayTransitions Events EffectBefore EffectAfter");
            foreach (var row in interval) Assert.NotNull(row.Value);
            Assert.Equal(("conflict_resource_cost", "exchange_guardian_zero_46", 0), (interval["ConflictId"]!.GetValue<string>(), interval["ExchangeId"]!.GetValue<string>(), interval["Ordinal"]!.GetValue<int>()));
            foreach (var (name, end) in new[] { ("AppliedRange", 2), ("ReplayRange", 0), ("EventRange", 4) })
            { GuardianZeroClosed(interval[name]!.AsObject(), "Start End"); Assert.Equal((0, end), (interval[name]!["Start"]!.GetValue<int>(), interval[name]!["End"]!.GetValue<int>())); }
            activeGuard = "interval-transition-copies";
            Assert.True(JsonNode.DeepEquals(interval["AppliedTransitions"], envelope["AppliedTransitions"]));
            Assert.True(JsonNode.DeepEquals(interval["ReplayTransitions"], envelope["ReplayTransitions"]));
            activeGuard = "interval-event-copies";
            Assert.True(JsonNode.DeepEquals(interval["Events"], envelope["Events"])); Assert.Empty(interval["ReplayTransitions"]!.AsArray());
            var transitions = interval["AppliedTransitions"]!.AsArray().Select(ReadGuardianZeroExactData<ResourceTransition>).ToArray();
            var events = interval["Events"]!.AsArray().Select(ReadGuardianZeroExactData<ResourceAppliedEvent>).ToArray();
            var commonEvents = envelope["ResourceEvents"]!.AsArray().Select(ReadGuardianZeroExactData<ResourceAppliedEvent>).ToArray();
            Assert.Equal(2, transitions.Length); Assert.Equal(4, events.Length); Assert.Equal(4, commonEvents.Length);
            activeGuard = "common-event-order";
            Assert.Equal(events.OrderBy(row => row.Turn).ThenBy(row => row.ExecutionSequence).ThenBy(row => row.EventKind, StringComparer.Ordinal).ThenBy(row => row.OperationId, StringComparer.Ordinal), commonEvents);
            activeGuard = "resource-image-copies";
            var state = envelope["StateAfterImage"]!.AsObject(); var history = envelope["HistoryAfterImage"]!.AsObject();
            GuardianZeroClosed(state, "schemaVersion entries"); GuardianZeroClosed(history, "schemaVersion entries");
            Assert.Equal(1, state["schemaVersion"]!.GetValue<int>()); Assert.Equal(1, history["schemaVersion"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(state, ReadGuardianZeroStrictJson(envelope["ResourceStateAfterImage"]!.GetValue<string>())));
            Assert.True(JsonNode.DeepEquals(history, ReadGuardianZeroStrictJson(envelope["ResourceHistoryAfterImage"]!.GetValue<string>())));
            Assert.True(JsonNode.DeepEquals(state, projection["State"])); Assert.True(JsonNode.DeepEquals(history, projection["History"]));
            var beforeState = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, ResourceMaterializationContract.StatePath)!);
            var beforeHistory = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, ResourceMaterializationContract.HistoryPath)!);
            activeGuard = "retained-resource-history";
            var oldRows = beforeHistory["entries"]!.AsArray(); Assert.Equal(4, oldRows.Count); Assert.Equal(6, history["entries"]!.AsArray().Count);
            for (var index = 0; index < oldRows.Count; index++) Assert.True(JsonNode.DeepEquals(oldRows[index], history["entries"]![index]));
            Assert.Equal(2, state["entries"]!.AsArray().Count); var restoredState = state.DeepClone().AsObject();
            var oppositionOwner = setup.OriginalConflict["activeConflict"]!["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
            var coordinates = new[] { new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, oppositionOwner, "spiritual_action_points"),
                new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points") };
            for (var side = 0; side < 2; side++)
            {
                var eventRef = "turn_46:afterlife_conflict:exchange_guardian_zero_46:" + (side == 0 ? "opposition" : "player");
                var key = new ResourceOperationKey(eventRef, "afterlife_cost", "exchange_guardian_zero_46", coordinates[side], ResourceOperation.Spend);
                var coordinateHash = SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(key)!.AsObject(), "resource_mutation_key");
                activeGuard = "resource-allocation";
                allocations[2 * side] = Allocation(2 * side, "resource_operation", "resources", coordinateHash);
                allocations[2 * side + 1] = Allocation(2 * side + 1, "resource_transition", "resources", coordinateHash);
                activeGuard = "resource-transition";
                var transition = transitions[side];
                Assert.Equal((allocations[2 * side], allocations[2 * side + 1], eventRef, coordinates[side]), (transition.OperationId, transition.TransitionId, transition.EventRef, transition.Coordinate));
                Assert.Equal((ResourceTransitionOperation.Spend, "afterlife_cost", "exchange_guardian_zero_46", ResourceMutationPhase.DirectCost, 130, side), (transition.Operation, transition.OriginKind, transition.OriginId, transition.Phase, transition.Priority, transition.ExecutionSequence));
                Assert.Equal((3m, 3m, 3m, 0m, side == 0 ? 9m : 10m, 46), (transition.RequestedAmount, transition.AppliedAmount, transition.BeforeState!.Current, transition.AfterState!.Current, transition.AfterState.Maximum, transition.Turn));
                Assert.Equal(transition.BeforeState! with { Current = 0m }, transition.AfterState);
                activeGuard = "resource-state-coordinate";
                var stateIndex = GuardianZeroIndex(state["entries"]!.AsArray(), row => GuardianZeroCoordinate(row, coordinates[side]));
                var beforeIndex = GuardianZeroIndex(beforeState["entries"]!.AsArray(), row => GuardianZeroCoordinate(row, coordinates[side])); Assert.Equal(beforeIndex, stateIndex);
                var expectedEntry = beforeState["entries"]![beforeIndex]!.DeepClone(); expectedEntry["current"] = 0m;
                expectedEntry["chronology"]!["lastTransitionId"] = transition.TransitionId; expectedEntry["chronology"]!["lastEventRef"] = eventRef; expectedEntry["chronology"]!["lastTransitionTurn"] = 46;
                Assert.True(JsonNode.DeepEquals(expectedEntry, state["entries"]![stateIndex])); restoredState["entries"]![stateIndex] = beforeState["entries"]![beforeIndex]!.DeepClone();
                activeGuard = "resource-history-transition";
                var historyIndex = GuardianZeroIndex(history["entries"]!.AsArray(), row => row["eventRef"]!.GetValue<string>() == eventRef && GuardianZeroCoordinate(row["coordinate"]!.AsObject(), coordinates[side]));
                Assert.Equal(oldRows.Count + side, historyIndex);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ResourceHistoryState.SerializeCanonicalTransitions([transition]))!["entries"]![0], history["entries"]![historyIndex]));
                foreach (var prefix in new[] { "/Interval/AppliedTransitions/", "/AppliedTransitions/" })
                { Bind(prefix + side + "/OperationId", transition.OperationId); Bind(prefix + side + "/TransitionId", transition.TransitionId); }
                foreach (var prefix in new[] { "/Interval/EffectAfter/ResourceMutations/", "/FullEffectBoundary/ResourceMutations/" })
                { Bind(prefix + side + "/Transition/OperationId", transition.OperationId); Bind(prefix + side + "/Transition/TransitionId", transition.TransitionId); }
                Bind("/ResourceStateAfterImage", transition.TransitionId, "/entries/" + stateIndex + "/chronology/lastTransitionId");
                foreach (var prefix in new[] { "/StateAfterImage", "/PreparedHashOperands/ProjectionInput/State" }) Bind(prefix + "/entries/" + stateIndex + "/chronology/lastTransitionId", transition.TransitionId);
                foreach (var prefix in new[] { "/HistoryAfterImage", "/PreparedHashOperands/ProjectionInput/History" })
                { Bind(prefix + "/entries/" + historyIndex + "/operationId", transition.OperationId); Bind(prefix + "/entries/" + historyIndex + "/transitionId", transition.TransitionId); }
                Bind("/ResourceHistoryAfterImage", transition.OperationId, "/entries/" + historyIndex + "/operationId"); Bind("/ResourceHistoryAfterImage", transition.TransitionId, "/entries/" + historyIndex + "/transitionId");
                activeGuard = "resource-events";
                for (var kind = 0; kind < 2; kind++)
                {
                    var eventKind = kind == 0 ? "resource_spent" : "resource_depleted"; var ev = events[2 * side + kind];
                    Assert.Equal((eventKind, transition.OperationId, eventRef, coordinates[side], 3m, 0m, 3m, 46, side), (ev.EventKind, ev.OperationId, ev.EventRef, ev.Coordinate, ev.Before, ev.After, ev.AppliedAmount, ev.Turn, ev.ExecutionSequence));
                    Assert.Equal(transition.SourceEvidence.AuthorityFingerprint, ev.SourceFingerprint);
                    Bind("/Interval/Events/" + (2 * side + kind) + "/OperationId", transition.OperationId); Bind("/Events/" + (2 * side + kind) + "/OperationId", transition.OperationId);
                    var commonIndex = Assert.Single(commonEvents.Select((row, index) => (row, index)).Where(pair => pair.row == ev)).index;
                    Bind("/ResourceEvents/" + commonIndex + "/OperationId", transition.OperationId);
                }
            }
            Assert.True(JsonNode.DeepEquals(beforeState, restoredState));
            activeGuard = "effect-schema";
            var effects = envelope["EffectPlan"]!.AsObject();
            GuardianZeroClosed(effects, "InputFingerprint CarrierAuthorityFingerprint SourceAuthorityFingerprint TargetAuthorityFingerprint SkillScopeFingerprint AllocatedCombatantIds AllocatedEffectIds AllocatedTransitionIds WoundApplicationRootEffectBindings Sources Targets SourceBindings DeferredReactions ReactionExpansionCount ReactionExpansionUsage ActiveEffects ResourceTriggerCarriers AcceptedCarrierBaselines EventInput CarrierBeforeImages CarrierAfterImages IdentityIndexBeforeImage IdentityIndexAfterImage TouchedPaths DeletedPaths IsAcceptedBoundaryComplete AcceptedBoundaryBasePlanFingerprint AcceptedBoundaryFinalPlanFingerprint MortalRecoveryExecutionFingerprint SpiritualHealingExecutionFingerprint MortalDiagnosisExecutionFingerprint MortalTreatmentExecutionFingerprint RetainedSourceAuthorityFingerprint RetainedTargetAuthorityFingerprint");
            foreach (var name in new[] { "MortalRecoveryExecutionFingerprint", "SpiritualHealingExecutionFingerprint", "MortalDiagnosisExecutionFingerprint", "MortalTreatmentExecutionFingerprint" }) GuardianZeroExplicitNull(effects, name);
            Assert.True(effects["IsAcceptedBoundaryComplete"]!.GetValue<bool>());
            var rootId = setup.Data.Retained.Effects.Single()["effectId"]!.GetValue<string>();
            var activation = AssertGuardianZeroRetryEffectBoundaries(envelope, transitions, rootId, effects, name => activeGuard = name);
            var effectCoordinate = SpiritualWoundStateJson.Hash(JsonSerializer.SerializeToNode(new EffectIdentityAllocationKey(activation.Stamp.Identity.EventRef, "trigger_execution", rootId))!.AsObject(), "allocation_effect_transition");
            allocations[4] = Allocation(4, "effect_transition", "effects", effectCoordinate);
            AssertGuardianZeroRetryCarriers(setup, origin, envelope, rootId, allocations[4], activation, Bind, name => activeGuard = name);
            Assert.Equal(5, allocations.Distinct(StringComparer.Ordinal).Count()); Assert.Equal(58, bindings.Count);
            var resourceHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { "book_of_eternity.spiritual_closed_resource_result", "1", "conflict_resource_cost", "exchange_guardian_zero_46", "0", JsonSerializer.Serialize(transitions), JsonSerializer.Serialize(Array.Empty<ResourceTransition>()), JsonSerializer.Serialize(events) });
            var effectPrefixHash = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { "book_of_eternity.spiritual_closed_effect_prefix", "1", effects["AcceptedBoundaryBasePlanFingerprint"]!.GetValue<string>(), interval["EffectAfter"]!["Fingerprint"]!.GetValue<string>() });
            var sourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
            var effectHash = WoundAcceptedTurnFingerprintWriter.Compute(ReadGuardianZeroEffectPayloadHashFields(effects));
            var preparedHash = WoundAcceptedTurnFingerprintWriter.Compute(ReadGuardianZeroPreparedPlanHashFields(envelope, effectHash));
            Bind("/Interval/EffectAfter/Fingerprint", interval["EffectAfter"]!["Fingerprint"]!.GetValue<string>());
            Bind("/FullEffectBoundary/Fingerprint", envelope["FullEffectBoundary"]!["Fingerprint"]!.GetValue<string>());
            activeGuard = "full-effect-payload-hash"; Bind("/EffectPlan/AcceptedBoundaryFinalPlanFingerprint", effectHash);
            activeGuard = "common-prepared-hash"; Bind("/PreparedPlanFingerprint", preparedHash);
            foreach (var prefix in new[] { "/witness", "/receipt/strainComparisons/0/witness", "/OwnerCompanionAfterImages/" + GuardianZeroEscape(SpiritualWoundOpportunityReceiptState.StatePath) + "/strainComparisons/0/witness" })
            {
                activeGuard = "closed-resource-hash"; Bind(prefix + "/resourceResultFingerprint", resourceHash);
                activeGuard = "closed-effect-hash"; Bind(prefix + "/effectPlanFingerprint", effectPrefixHash);
                activeGuard = "source-hash"; Bind(prefix + "/sourceFingerprint", sourceHash); Bind(prefix + "/sourceId", "spiritual_source_" + sourceHash[7..]);
            }
            activeGuard = "complete-reference-inventory";
            Assert.Equal(74, bindings.Count);
            var values = bindings.Values.Distinct(StringComparer.Ordinal).ToArray(); Assert.Equal(13, values.Length);
            var occurrences = ReadGuardianZeroScalarOccurrences(envelope, values);
            Assert.Equal(bindings.Keys.OrderBy(GuardianZeroLocationSort, StringComparer.Ordinal), occurrences.Keys.OrderBy(GuardianZeroLocationSort, StringComparer.Ordinal));
            foreach (var pair in bindings) Assert.Equal(pair.Value, occurrences[pair.Key]);
            return new(json, allocations, bindings);

            string Allocation(int ordinal, string kind, string owner, string coordinate)
            {
                var row = journal[ordinal]!.AsObject(); GuardianZeroClosed(row, "ordinal kind owner coordinate value");
                Assert.Equal((ordinal, kind, owner, coordinate), (row["ordinal"]!.GetValue<int>(), row["kind"]!.GetValue<string>(), row["owner"]!.GetValue<string>(), row["coordinate"]!.GetValue<string>()));
                var value = row["value"]!.GetValue<string>(); Assert.Matches("^" + kind + "_[0-9a-f]{32}$", value);
                Bind("/AllocationJournalJson", value, "/" + ordinal + "/value"); return value;
            }
            void Bind(string outer, string value, string? inner = null)
            {
                var location = new GuardianZeroRetryLocation(outer, inner); Assert.Equal(value, ReadGuardianZeroScalar(envelope, location)); Assert.True(bindings.TryAdd(location, value), "Duplicate mapped location " + location);
            }
        }
        catch (Exception failure) when (failure is Xunit.Sdk.XunitException or FormatException or JsonException or InvalidOperationException)
        {
            throw new GuardianZeroRetryGuardFailure(activeGuard, failure);
        }
    }

    /// <summary>Reconstructs lifecycle0/1/2, then resource3/4 and terminal5 from complete detached typed evidence.</summary>
    /// <param name="envelope">Full before/after/terminal images.</param>
    /// <param name="transitions">Independently validated complete AP transitions.</param>
    /// <param name="rootId">Exact permanent guardian root.</param>
    /// <param name="effects">Complete detached effect plan whose declared stamp must agree.</param>
    /// <param name="mark">Marks the concrete detached boundary guard without changing authority.</param>
    /// <returns>The single independently reconstructed accepted activation.</returns>
    private static AcceptedEffectActivation AssertGuardianZeroRetryEffectBoundaries(JsonObject envelope,
        ResourceTransition[] transitions, string rootId, JsonObject effects, Action<string> mark)
    {
        mark("effect-prefix-before");
        var before = envelope["Interval"]!["EffectBefore"]!.AsObject(); var after = envelope["Interval"]!["EffectAfter"]!.AsObject(); var terminal = envelope["FullEffectBoundary"]!.AsObject();
        const string prefixKeys = "Fingerprint PlanAuthority Boundaries BoundaryCloses CausalClosures AcceptedActivations RejectedActivations AppliedComponentEvidence ResourceMutations ReleasedReactions TerminalAvailabilityReservations ExpansionUsage";
        GuardianZeroClosed(before, prefixKeys); GuardianZeroClosed(after, prefixKeys); GuardianZeroClosed(terminal, prefixKeys + " ExpansionCount UseProjectionOrdinal PendingFrontierBoundaryOrdinal IsComplete");
        var stamp = ReadGuardianZeroExactData<EffectAcceptedPlanAuthorityStamp>(before["PlanAuthority"]);
        Assert.Equal(new EffectAcceptedPlanAuthorityStamp(effects["InputFingerprint"]!.GetValue<string>(), effects["CarrierAuthorityFingerprint"]!.GetValue<string>(), effects["SourceAuthorityFingerprint"]!.GetValue<string>(), effects["TargetAuthorityFingerprint"]!.GetValue<string>(), effects["SkillScopeFingerprint"]?.GetValue<string>() ?? "none"), stamp);
        var boundary = ReadGuardianZeroExactData<EffectEventBoundaryStamp>(Assert.Single(before["Boundaries"]!.AsArray()));
        var accepted = Assert.Single(before["AcceptedActivations"]!.AsArray())!.AsObject(); GuardianZeroClosed(accepted, "Boundary Activation Candidate CandidateFingerprint MechanicsOrdinal");
        var candidateData = accepted["Candidate"]!.AsObject();
        GuardianZeroClosed(candidateData, "Activation UseSeed EffectAuthority Producer PlannedMutationKeys PlannedComponentIds PlannedComponentIdsByMutation PendingOutputs ReactionOutputs Origin CandidateFingerprint CausalMaterialFingerprint PendingWaveOrdinal ResolvedOutputs");
        foreach (var name in new[] { "UseSeed", "Producer", "CandidateFingerprint", "CausalMaterialFingerprint" }) GuardianZeroExplicitNull(candidateData, name);
        foreach (var name in new[] { "PlannedMutationKeys", "PlannedComponentIds", "PlannedComponentIdsByMutation", "PendingOutputs", "ReactionOutputs", "ResolvedOutputs" }) Assert.Empty(candidateData[name]!.AsArray());
        GuardianZeroClosed(candidateData["Origin"]!.AsObject(), "Mutations ComponentIds ComponentIdsByMutation SourceExports");
        foreach (var row in candidateData["Origin"]!.AsObject()) Assert.Empty(row.Value!.AsArray());
        Assert.Equal(0, candidateData["PendingWaveOrdinal"]!.GetValue<int>());
        var proposedActivation = ReadGuardianZeroExactData<EffectActivationCandidate>(candidateData["Activation"]);
        var authority = new ResourcePendingAuthorityBinding("permanent", rootId);
        Assert.Equal(authority, proposedActivation.EffectAuthority);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(authority), candidateData["EffectAuthority"]));
        var ownerEvent = "turn_46:lifecycle:owner_turn_end:chaos_sea:guardian:" + SpiritualGuardianAttributionTestData.GuardianId;
        var eventRef = EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(ownerEvent, rootId, authority, "on_owner_turn_end");
        Assert.Equal(new EffectActivationCandidate(new(rootId, "on_owner_turn_end", "owner_turn_end", eventRef, ownerEvent), 100, false, authority), proposedActivation);
        var candidate = new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(proposedActivation, null, null, [], [],
            new Dictionary<ResourceOperationKey, string>(), [], [], EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin.Empty);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(candidate)), candidateData));
        var activation = ReadGuardianZeroExactData<AcceptedEffectActivation>(accepted["Activation"]);
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
            var evidence = ReadGuardianZeroExactData<AcceptedResourceMutationEvidence>(after["ResourceMutations"]![side]);
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
        GuardianZeroExplicitNull(terminal, "PendingFrontierBoundaryOrdinal"); Assert.True(terminal["IsComplete"]!.GetValue<bool>());
        return activation;
    }

    /// <summary>Checks all guardian/root/identity/carrier copies and binds the exact eight opaque effect references.</summary>
    /// <param name="setup">Independent original parent and raw images.</param>
    /// <param name="origin">Physical pre-authoring origin.</param>
    /// <param name="envelope">Full untrimmed attempt DATA.</param>
    /// <param name="rootId">Permanent root, never a player array assumption.</param>
    /// <param name="transitionId">The independently journal-bound fresh root transition.</param>
    /// <param name="activation">Validated exact guardian lifecycle activation.</param>
    /// <param name="bind">Adds one already checked scalar location, rejecting duplicate locations.</param>
    /// <param name="mark">Marks the precise carrier/identity/event contract being checked.</param>
    private static void AssertGuardianZeroRetryCarriers(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin, JsonObject envelope,
        string rootId, string transitionId, AcceptedEffectActivation activation, Action<string, string, string?> bind, Action<string> mark)
    {
        mark("guardian-root");
        var effects = envelope["EffectPlan"]!.AsObject(); var path = AfterlifeEntityProfileState.StatePath;
        var originalProfiles = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, path)!);
        var beforeMap = effects["CarrierBeforeImages"]!.AsObject();
        var carrierPaths = new[] { EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath, path, AfterlifeSpiritualConflictState.StatePath };
        GuardianZeroClosed(beforeMap, string.Join(' ', carrierPaths));
        foreach (var name in carrierPaths)
        {
            var bytes = GuardianZeroOriginBytes(setup, origin, name);
            Assert.True(JsonNode.DeepEquals(bytes is null ? null : ReadGuardianAttributionJson(bytes), beforeMap[name]), name);
        }
        var afterMap = effects["CarrierAfterImages"]!.AsObject(); GuardianZeroClosed(afterMap, path);
        Assert.True(JsonNode.DeepEquals(afterMap, envelope["EffectCarrierAfterImages"]));
        var profiles = afterMap[path]!.AsObject(); var rows = profiles["profiles"]!.AsArray();
        var profileIndex = GuardianZeroIndex(rows, row => row["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId);
        var guardian = rows[profileIndex]!.AsObject(); var roots = guardian["activeEffects"]!.AsArray(); Assert.Single(roots);
        var rootIndex = GuardianZeroIndex(roots, row => row["effectId"]!.GetValue<string>() == rootId);
        mark("guardian-siblings");
        var expectedRoot = setup.Data.Retained.Effects.Single().DeepClone(); expectedRoot["chronology"]!["lastTransitionId"] = transitionId; expectedRoot["chronology"]!["lastTransitionTurn"] = 46;
        Assert.True(JsonNode.DeepEquals(expectedRoot, roots[rootIndex]));
        Assert.True(JsonNode.DeepEquals(setup.Data.Retained.Parents.Single(), Assert.Single(guardian["activeWounds"]!.AsArray())));
        var expectedProfiles = originalProfiles.DeepClone().AsObject();
        if (!expectedProfiles.ContainsKey("schemaVersion")) expectedProfiles["schemaVersion"] = 1;
        foreach (var row in expectedProfiles["profiles"]!.AsArray().OfType<JsonObject>())
            foreach (var name in new[] { "activeWounds", "activeEffects" }) if (!row.ContainsKey(name)) row[name] = new JsonArray();
        var restoredProfiles = profiles.DeepClone().AsObject(); ReadGuardianAttributionProfile(restoredProfiles)["activeEffects"]![rootIndex] = setup.Data.Retained.Effects.Single().DeepClone();
        Assert.True(JsonNode.DeepEquals(expectedProfiles, restoredProfiles));
        mark("guardian-siblings");
        Assert.True(JsonNode.DeepEquals(Assert.Single(effects["ActiveEffects"]!.AsArray()), expectedRoot));
        foreach (var name in new[] { "ResourceTriggerCarriers", "AcceptedCarrierBaselines" })
        {
            var catalog = effects[name]!.AsObject(); GuardianZeroClosed(catalog, "PlayerEffects NpcEffects EnemyCombatants AllyCombatants AfterlifeProfiles SpiritualConflict");
            Assert.True(JsonNode.DeepEquals(name == "ResourceTriggerCarriers" ? profiles : expectedProfiles, catalog["AfterlifeProfiles"]));
            Assert.True(JsonNode.DeepEquals(setup.OrdinaryRoot, catalog["SpiritualConflict"]));
            foreach (var (key, file) in new[] { ("PlayerEffects", EffectCarrierCatalog.PlayerPath), ("NpcEffects", EffectCarrierCatalog.NpcPath), ("EnemyCombatants", EffectCarrierCatalog.EnemiesPath), ("AllyCombatants", EffectCarrierCatalog.AlliesPath) })
            { var bytes = GuardianZeroOriginBytes(setup, origin, file); Assert.True(JsonNode.DeepEquals(bytes is null ? null : ReadGuardianAttributionJson(bytes), catalog[key])); }
        }
        mark("retained-effect-identity");
        var identityBefore = ReadGuardianAttributionJson(GuardianZeroOriginBytes(setup, origin, EffectIdentityState.StatePath)!);
        Assert.True(JsonNode.DeepEquals(identityBefore, effects["IdentityIndexBeforeImage"]));
        var identity = effects["IdentityIndexAfterImage"]!.AsObject(); Assert.True(JsonNode.DeepEquals(identity, envelope["EffectIdentityAfterImage"]));
        var parsed = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(identity), EffectIdentityState.StatePath); Assert.Empty(parsed.Issues); Assert.NotNull(parsed.State);
        var entryIndex = GuardianZeroIndex(identity["entries"]!.AsArray(), row => row["effectId"]!.GetValue<string>() == rootId);
        var oldIndex = GuardianZeroIndex(identityBefore["entries"]!.AsArray(), row => row["effectId"]!.GetValue<string>() == rootId); Assert.Equal(oldIndex, entryIndex);
        var old = identityBefore["entries"]![oldIndex]!["transitions"]!.AsArray(); var current = identity["entries"]![entryIndex]!["transitions"]!.AsArray(); Assert.Equal(old.Count + 1, current.Count);
        for (var index = 0; index < old.Count; index++) Assert.True(JsonNode.DeepEquals(old[index], current[index]));
        var append = Assert.Single(parsed.State!.Entries, row => row.EffectId == rootId).Transitions[^1];
        Assert.Equal((transitionId, "trigger", 46, activation.Stamp.Identity.EventRef), (append.TransitionId, append.Kind, append.Turn, append.EventRef));
        Assert.Equal(new[] { rootId }, append.SourceEffectIds); Assert.Equal(new[] { rootId }, append.ResultEffectIds); Assert.Null(append.ReceiptId);
        var restoredIdentity = identity.DeepClone().AsObject(); restoredIdentity["entries"]![entryIndex]!["transitions"] = old.DeepClone(); Assert.True(JsonNode.DeepEquals(identityBefore, restoredIdentity));
        var execution = Assert.Single(envelope["ResourceTriggerExecutions"]!.AsArray())!.AsObject();
        GuardianZeroClosed(execution, "EffectId TriggerId EventKind EventRef MutationKeys RemainingUseBudget ComponentIds TriggerEventRef ComponentIdsByMutation");
        Assert.Equal((rootId, "on_owner_turn_end", "owner_turn_end", activation.Stamp.Identity.EventRef, activation.Stamp.Identity.TriggerEventRef),
            (execution["EffectId"]!.GetValue<string>(), execution["TriggerId"]!.GetValue<string>(), execution["EventKind"]!.GetValue<string>(), execution["EventRef"]!.GetValue<string>(), execution["TriggerEventRef"]!.GetValue<string>()));
        GuardianZeroExplicitNull(execution, "RemainingUseBudget"); Assert.Empty(execution["MutationKeys"]!.AsArray()); Assert.Empty(execution["ComponentIds"]!.AsArray()); Assert.Empty(execution["ComponentIdsByMutation"]!.AsObject());
        mark("effect-event-input");
        var eventInput = effects["EventInput"]!.AsObject(); GuardianZeroClosed(eventInput, "turn events lifecycleEvents sceneId");
        Assert.Equal(46, eventInput["turn"]!.GetValue<int>()); Assert.Equal("conflict_resource_cost", eventInput["sceneId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(new JsonArray(new JsonObject { ["kind"] = "accepted_turn", ["authorityId"] = "turn_46", ["eventRef"] = "turn_46:accepted_effect" }), eventInput["events"]));
        Assert.Equal(3, eventInput["lifecycleEvents"]!.AsArray().Count);
        for (var index = 0; index < 3; index++)
        {
            var owner = index == 0; var side = index == 1 ? "opposition" : "player";
            var expected = new JsonObject { ["eventRef"] = owner ? activation.Stamp.Identity.TriggerEventRef : "afterlife_exchange:conflict_resource_cost:exchange_guardian_zero_46:lifecycle:" + side,
                ["turn"] = 46, ["phase"] = owner ? "owner_turn_end" : "afterlife_exchange_end", ["realm"] = "chaos_sea",
                ["target"] = new JsonObject { ["kind"] = owner ? "guardian" : "spiritual_conflict_side", ["targetId"] = owner ? SpiritualGuardianAttributionTestData.GuardianId : "conflict_resource_cost:" + side },
                ["triggerId"] = null, ["currentTime"] = null, ["currentSceneId"] = owner ? null : "conflict_resource_cost", ["sceneClosed"] = false,
                ["sourceSatisfied"] = null, ["conditionSatisfied"] = null, ["targetSatisfied"] = null, ["currentRealm"] = "chaos_sea" };
            if (!owner) expected["causalEventRef"] = "afterlife_exchange:conflict_resource_cost:exchange_guardian_zero_46";
            Assert.True(JsonNode.DeepEquals(expected, eventInput["lifecycleEvents"]![index]));
        }
        var triggerProfiles = effects["ResourceTriggerCarriers"]!["AfterlifeProfiles"]!["profiles"]!.AsArray();
        var triggerIndex = GuardianZeroIndex(triggerProfiles, row => row["actorType"]!.GetValue<string>() == "guardian" && row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId);
        var triggerRoot = GuardianZeroIndex(triggerProfiles[triggerIndex]!["activeEffects"]!.AsArray(), row => row["effectId"]!.GetValue<string>() == rootId);
        bind("/EffectPlan/AllocatedTransitionIds/0", transitionId, null); bind("/EffectPlan/ActiveEffects/0/chronology/lastTransitionId", transitionId, null);
        bind("/EffectPlan/ResourceTriggerCarriers/AfterlifeProfiles/profiles/" + triggerIndex + "/activeEffects/" + triggerRoot + "/chronology/lastTransitionId", transitionId, null);
        var carrierTail = "/" + GuardianZeroEscape(path) + "/profiles/" + profileIndex + "/activeEffects/" + rootIndex + "/chronology/lastTransitionId";
        bind("/EffectPlan/CarrierAfterImages" + carrierTail, transitionId, null); bind("/EffectCarrierAfterImages" + carrierTail, transitionId, null);
        var identityTail = "/entries/" + entryIndex + "/transitions/" + old.Count + "/transitionId";
        bind("/EffectPlan/IdentityIndexAfterImage" + identityTail, transitionId, null); bind("/EffectIdentityAfterImage" + identityTail, transitionId, null);
    }

    /// <summary>Checks all before-image paths, explicit absences and exact authored-or-signed bytes without reading current mutable state.</summary>
    /// <param name="setup">Original authored byte inventory.</param>
    /// <param name="origin">Full signed physical inventory.</param>
    /// <param name="envelope">Complete reported before images.</param>
    private static void AssertGuardianZeroRetryBeforeImages(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin, JsonObject envelope)
    {
        var rows = envelope["BeforeImages"]!.AsArray();
        var expected = ("game_state/combat/allies.json game_state/combat/enemies.json game_state/control/pending_effect_resolutions.json game_state/control/pending_spiritual_wound_decisions.json game_state/control/pending_turn_snapshot.authority.json game_state/control/pending_turn_snapshot.json game_state/control/spiritual_wound_capture_checkpoint.json game_state/effects/effect_commands.json game_state/effects/effect_identity_index.json game_state/meta/afterlife_entity_profiles.json game_state/meta/afterlife_spiritual_conflict_state.json game_state/npcs/npc_effects.json game_state/player/effects.json game_state/resources/resource_commands.json game_state/resources/resource_definitions.json game_state/resources/resource_history.json game_state/resources/resource_owner_authority.json game_state/resources/resource_state.json game_state/wounds/spiritual_wound_opportunity_receipts.json game_state/wounds/wound_commands.json input/turn_request.json").Split(' ');
        Assert.Equal(expected, rows.Select(row => row!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        foreach (var node in rows)
        {
            var row = node!.AsObject(); GuardianZeroClosed(row, "path Existed bytesBase64 Fingerprint");
            var bytes = GuardianZeroOriginBytes(setup, origin, row["path"]!.GetValue<string>());
            Assert.Equal(bytes is not null, row["Existed"]!.GetValue<bool>());
            Assert.Equal(bytes is null ? null : Convert.ToBase64String(bytes), row["bytesBase64"]?.GetValue<string>());
            if (bytes is null) GuardianZeroExplicitNull(row, "bytesBase64");
            Assert.Equal(new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint, row["Fingerprint"]!.GetValue<string>());
        }
        var expectedTouched = ("game_state/control/pending_spiritual_wound_decisions.json game_state/control/spiritual_wound_capture_checkpoint.json game_state/effects/effect_commands.json game_state/effects/effect_identity_index.json game_state/meta/afterlife_entity_profiles.json game_state/meta/afterlife_spiritual_conflict_state.json game_state/resources/resource_definitions.json game_state/resources/resource_history.json game_state/resources/resource_owner_authority.json game_state/resources/resource_state.json game_state/wounds/spiritual_wound_opportunity_receipts.json game_state/wounds/wound_commands.json").Split(' ');
        Assert.Equal(expectedTouched, envelope["TouchedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
        Assert.Equal(GuardianZeroPublicationPrivatePaths.Append(EffectAcceptedTurnPlan.CommandPath).Order(StringComparer.Ordinal), envelope["ConsumedPaths"]!.AsArray().Select(row => row!.GetValue<string>()));
    }

    /// <summary>Reads the complete original bytes for a path, applying only the fixture's independently authored input replacements.</summary>
    /// <param name="setup">Exact authored input bytes.</param>
    /// <param name="origin">Signed origin before those replacements.</param>
    /// <param name="path">Canonical relative path.</param>
    /// <returns>Exact bytes or the independently observed absence.</returns>
    private static byte[]? GuardianZeroOriginBytes(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin, string path) =>
        setup.Authored.TryGetValue(path, out var authored) ? authored : origin.Files.TryGetValue(path.Replace('/', Path.DirectorySeparatorChar), out var bytes) ? bytes : null;

    /// <summary>Round-trips a positional DATA record so unknown keys, missing null fields and type changes cannot be discarded.</summary>
    /// <typeparam name="T">An existing immutable positional record, never an owner or authority handle.</typeparam>
    /// <param name="node">Complete detached record node.</param>
    /// <returns>The exact existing typed DATA.</returns>
    private static T ReadGuardianZeroExactData<T>(JsonNode? node) where T : class
    {
        Assert.NotNull(node); var value = JsonSerializer.Deserialize<T>(node!.ToJsonString()); Assert.NotNull(value);
        Assert.True(JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(value))); return value!;
    }

    /// <summary>Parses complete JSON after rejecting duplicate keys recursively, before JsonNode can hide a duplicate.</summary>
    /// <param name="json">Exact outer or explicitly embedded JSON text.</param>
    /// <returns>The complete tree without removing or normalizing any input field.</returns>
    private static JsonNode ReadGuardianZeroStrictJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            Check(document.RootElement); return JsonNode.Parse(json)!;
        }
        catch (JsonException error) { Assert.True(false, error.ToString()); throw; }
        void Check(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject()) { Assert.True(keys.Add(property.Name), "Duplicate JSON key " + property.Name); Check(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Check(item);
        }
    }

    /// <summary>Requires the exact property set, making unknown fields and missing explicit nulls distinct failures.</summary>
    /// <param name="value">Complete object being checked.</param>
    /// <param name="keys">Space-separated exact schema keys.</param>
    private static void GuardianZeroClosed(JsonObject value, string keys) =>
        Assert.Equal(keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal), value.Select(row => row.Key).Order(StringComparer.Ordinal));

    /// <summary>Requires an explicitly present null rather than accepting a missing key.</summary>
    /// <param name="value">Complete object.</param>
    /// <param name="key">Required null-valued property.</param>
    private static void GuardianZeroExplicitNull(JsonObject value, string key) { Assert.True(value.ContainsKey(key), "Missing explicit null " + key); Assert.Null(value[key]); }

    /// <summary>Finds exactly one semantic join, never inferring identity from an array position.</summary>
    /// <param name="rows">Complete retained rows.</param>
    /// <param name="predicate">Independent permanent identity or full coordinate predicate.</param>
    /// <returns>The unique verified index.</returns>
    private static int GuardianZeroIndex(JsonArray rows, Func<JsonObject, bool> predicate) =>
        Assert.Single(rows.Select((row, index) => (row: row!.AsObject(), index)).Where(pair => predicate(pair.row))).index;

    /// <summary>Checks every canonical resource coordinate field.</summary>
    /// <param name="row">Canonical state row or History coordinate.</param>
    /// <param name="coordinate">Independent real owner coordinate.</param>
    /// <returns>Whether all four fields agree exactly.</returns>
    private static bool GuardianZeroCoordinate(JsonObject row, ResourceCoordinate coordinate) => row["realm"]!.GetValue<string>() == coordinate.Realm &&
        row["ownerKind"]!.GetValue<string>() == ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind) && row["resourceOwnerId"]!.GetValue<string>() == coordinate.ResourceOwnerId && row["resourceKey"]!.GetValue<string>() == coordinate.ResourceKey;

    /// <summary>Escapes one literal RFC6901 property without changing the represented path.</summary>
    /// <param name="value">Literal property.</param>
    /// <returns>The escaped segment.</returns>
    private static string GuardianZeroEscape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>Provides deterministic diagnostic ordering without conflating outer and embedded pointers.</summary>
    /// <param name="location">A fully typed scalar location.</param>
    /// <returns>Unambiguous sort text.</returns>
    private static string GuardianZeroLocationSort(GuardianZeroRetryLocation location) => location.Outer + "\n" + location.Inner;

    /// <summary>Reads exactly one declared scalar, parsing only its explicitly named embedded JSON boundary.</summary>
    /// <param name="envelope">Complete detached envelope.</param>
    /// <param name="location">The independently validated scalar path.</param>
    /// <returns>The exact scalar string.</returns>
    private static string ReadGuardianZeroScalar(JsonObject envelope, GuardianZeroRetryLocation location)
    {
        var node = ReadPressureZeroRetryPath(envelope, location.Outer);
        if (location.Inner is not null) node = ReadPressureZeroRetryPath(ReadGuardianZeroStrictJson(node!.GetValue<string>()), location.Inner);
        return node!.GetValue<string>();
    }

    /// <summary>Enumerates all occurrences and rejects hidden substrings, key references or encoded copies of the changing values.</summary>
    /// <param name="envelope">Full untrimmed DATA.</param>
    /// <param name="values">Only the independently derived changing values, not a caller's allowed reference set.</param>
    /// <returns>Every exact scalar location; none are inferred as authorized by their count.</returns>
    private static Dictionary<GuardianZeroRetryLocation, string> ReadGuardianZeroScalarOccurrences(JsonObject envelope, string[] values)
    {
        var result = new Dictionary<GuardianZeroRetryLocation, string>();
        var embedded = new HashSet<string>(["/AllocationJournalJson", "/ExchangeJson", "/StrainPreimageJson", "/ResourceStateAfterImage", "/ResourceHistoryAfterImage"], StringComparer.Ordinal);
        Walk(envelope, "", null, ""); return result;
        void Walk(JsonNode? node, string path, string? outer, string property)
        {
            if (node is JsonObject obj)
                foreach (var row in obj)
                { foreach (var value in values) Assert.False(row.Key.Contains(value, StringComparison.Ordinal), "Hidden mapped key"); Walk(row.Value, path + "/" + GuardianZeroEscape(row.Key), outer, row.Key); }
            else if (node is JsonArray array) for (var index = 0; index < array.Count; index++) Walk(array[index], path + "/" + index, outer, "");
            else if (node is JsonValue scalar && scalar.TryGetValue<string>(out var text))
            {
                if (outer is null && embedded.Contains(path)) { Walk(ReadGuardianZeroStrictJson(text), "", path, ""); return; }
                if (property.EndsWith("Base64", StringComparison.Ordinal))
                { var bytes = Convert.FromBase64String(text); foreach (var value in values) Assert.False(Encoding.UTF8.GetString(bytes).Contains(value, StringComparison.Ordinal), "Encoded mapped reference " + path); }
                foreach (var value in values)
                    if (text == value) Assert.True(result.TryAdd(outer is null ? new(path) : new(outer, path), text));
                    else Assert.False(text.Contains(value, StringComparison.Ordinal), "Hidden scalar substring " + path);
            }
        }
    }

    /// <summary>Revalidates a reported attempt instead of trusting its cached bindings or slot list.</summary>
    /// <param name="setup">Same independent original fixture.</param>
    /// <param name="origin">Complete signed origin.</param>
    /// <param name="reported">Immutable envelope plus untrusted reported map.</param>
    /// <returns>A newly validated map from the envelope itself.</returns>
    private static GuardianZeroRetryAttemptData ValidateGuardianZeroReportedAttempt(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin, GuardianZeroRetryAttemptData reported)
    {
        var current = ReadGuardianZeroRetryAttempt(setup, origin, reported.EnvelopeJson); Assert.Equal(current.Allocations, reported.Allocations);
        Assert.Equal(current.Bindings.Count, reported.Bindings.Count);
        foreach (var row in current.Bindings) { Assert.True(reported.Bindings.TryGetValue(row.Key, out var value)); Assert.Equal(row.Value, value); }
        return current;
    }

    /// <summary>Translates only individually validated scalar tokens, comparing every other field and every embedded byte.</summary>
    /// <param name="setup">One unchanged fixture/session/request.</param>
    /// <param name="origin">Exact original signed files.</param>
    /// <param name="first">Complete real first attempt.</param>
    /// <param name="retry">Complete real fresh retry.</param>
    private static void AssertGuardianZeroFreshRetryEquivalent(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin,
        GuardianZeroRetryAttemptData first, GuardianZeroRetryAttemptData retry)
    {
        first = ValidateGuardianZeroReportedAttempt(setup, origin, first); retry = ValidateGuardianZeroReportedAttempt(setup, origin, retry);
        Assert.Equal(10, first.Allocations.Concat(retry.Allocations).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Bindings.Keys.OrderBy(GuardianZeroLocationSort, StringComparer.Ordinal), retry.Bindings.Keys.OrderBy(GuardianZeroLocationSort, StringComparer.Ordinal));
        var translated = ReadGuardianZeroStrictJson(first.EnvelopeJson).AsObject(); var expected = ReadGuardianZeroStrictJson(retry.EnvelopeJson).AsObject();
        foreach (var row in first.Bindings)
        {
            Assert.Equal(row.Value, ReadGuardianZeroScalar(translated, row.Key)); var replacement = retry.Bindings[row.Key];
            if (row.Key.Inner is null) SetPressureZeroRetryPath(translated, row.Key.Outer, JsonValue.Create(replacement)!);
            else
            {
                var raw = ReadPressureZeroRetryPath(translated, row.Key.Outer)!.GetValue<string>();
                SetPressureZeroRetryPath(translated, row.Key.Outer, JsonValue.Create(PatchGuardianZeroEmbeddedScalar(raw, row.Key.Inner, row.Value, replacement))!);
            }
        }
        Assert.True(JsonNode.DeepEquals(translated, expected), "Difference outside independently verified five-ID/74-position full-envelope bijection.");
    }

    /// <summary>Changes only one exact quoted UTF8 scalar token; every other embedded byte remains untouched.</summary>
    /// <param name="json">Exact retained embedded JSON bytes represented as a string.</param>
    /// <param name="pointer">The verified structural inner pointer.</param>
    /// <param name="before">Expected current scalar.</param>
    /// <param name="after">Same-family replacement scalar.</param>
    /// <returns>Only that token changed, without whole-string JSON serialization or normalization.</returns>
    private static string PatchGuardianZeroEmbeddedScalar(string json, string pointer, string before, string after)
    {
        var bytes = Encoding.UTF8.GetBytes(json); var reader = new Utf8JsonReader(bytes); Assert.True(reader.Read());
        long start = -1, end = -1; var count = 0;
        FindGuardianZeroScalarToken(ref reader, "", pointer, before, ref start, ref end, ref count); Assert.Equal(1, count);
        var replacement = JsonSerializer.SerializeToUtf8Bytes(after); Assert.Equal(end - start, (long)replacement.Length);
        var changed = bytes.ToArray(); replacement.AsSpan().CopyTo(changed.AsSpan(checked((int)start), replacement.Length));
        Assert.True(bytes.AsSpan(0, checked((int)start)).SequenceEqual(changed.AsSpan(0, checked((int)start))));
        Assert.True(bytes.AsSpan(checked((int)end)).SequenceEqual(changed.AsSpan(checked((int)end))));
        return Encoding.UTF8.GetString(changed);
    }

    /// <summary>Locates a unique quoted token with a structural UTF8 walk, not text search or a regex.</summary>
    /// <param name="reader">The current exact input reader.</param>
    /// <param name="path">Current structural path.</param>
    /// <param name="target">The one independently checked pointer.</param>
    /// <param name="expected">Expected scalar text at that pointer.</param>
    /// <param name="start">Receives exact token start.</param>
    /// <param name="end">Receives exact token end.</param>
    /// <param name="count">Counts matches, so missing or duplicated paths cannot silently pass.</param>
    private static void FindGuardianZeroScalarToken(ref Utf8JsonReader reader, string path, string target, string expected,
        ref long start, ref long end, ref int count)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            { Assert.Equal(JsonTokenType.PropertyName, reader.TokenType); var key = reader.GetString()!; Assert.True(reader.Read()); FindGuardianZeroScalarToken(ref reader, path + "/" + GuardianZeroEscape(key), target, expected, ref start, ref end, ref count); }
        else if (reader.TokenType == JsonTokenType.StartArray)
        { var index = 0; while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) FindGuardianZeroScalarToken(ref reader, path + "/" + index++, target, expected, ref start, ref end, ref count); }
        else if (path == target)
        { Assert.Equal(JsonTokenType.String, reader.TokenType); Assert.Equal(expected, reader.GetString()); start = reader.TokenStartIndex; end = reader.BytesConsumed; count++; }
    }

    /// <summary>Runs exactly twenty-six independent detached corruptions before any late hook is armed.</summary>
    /// <param name="setup">Independent same-fixture source operands.</param>
    /// <param name="origin">Complete signed origin.</param>
    /// <param name="first">Already accepted genuine first-attempt DATA.</param>
    /// <param name="output">Retains each actual refusal and complete exception diagnostic.</param>
    /// <returns>The actual number of completed rejection probes, not additional live runtime cases.</returns>
    private static int AssertGuardianZeroRetryProbes(GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin,
        GuardianZeroRetryAttemptData first, Action<string> output)
    {
        _ = ValidateGuardianZeroReportedAttempt(setup, origin, first);
        var probes = new List<(string Name, string Guard, Action<JsonObject> Change)>
        {
            ("missing explicit null", "prepared-schema", e => e["PreparedHashOperands"]!.AsObject().Remove("PendingPublicationAuthority")),
            ("unknown top field", "envelope-schema", e => e["unexpectedGuardianRetryField"] = true),
            ("missing journal row with retained cursor", "journal-schema", e => EditJournal(e, rows => rows.RemoveAt(4))),
            ("journal causal coordinate", "resource-allocation", e => EditJournal(e, rows => rows[0]!["coordinate"] = "sha256:" + new string('0', 64))),
            ("duplicate operation allocation", "journal-uniqueness", e => EditJournal(e, rows => rows[2]!["value"] = rows[0]!["value"]!.DeepClone())),
            ("unknown extra allocation reference", "effect-event-input", e => e["EffectPlan"]!["EventInput"]!["sceneId"] = first.Allocations[0]),
            ("cross-side spent event operation in all event copies", "resource-events", e =>
            {
                e["Interval"]!["Events"]![0]!["OperationId"] = first.Allocations[2]; e["Events"]![0]!["OperationId"] = first.Allocations[2];
                var row = Assert.Single(e["ResourceEvents"]!.AsArray(), item => item!["EventKind"]!.GetValue<string>() == "resource_spent" && item["ExecutionSequence"]!.GetValue<int>() == 0)!;
                row["OperationId"] = first.Allocations[2];
            }),
            ("cross-side State chronology in all State copies", "resource-state-coordinate", e =>
            {
                EditState(e, state =>
                {
                    var rows = state["entries"]!.AsArray(); var index = GuardianZeroIndex(rows, row => row["ownerKind"]!.GetValue<string>() == "afterlife_conflict_side");
                    rows[index]!["chronology"]!["lastTransitionId"] = first.Allocations[3];
                });
            }),
            ("wrong guardian actor in all current carrier copies", "guardian-root", e =>
            {
                foreach (var profiles in CurrentProfiles(e)) ReadGuardianAttributionProfile(profiles)["actorId"] = "guardian_other";
            }),
            ("missing IV sibling in all root copies", "guardian-siblings", e =>
            {
                var roots = new List<JsonNode> { e["EffectPlan"]!["ActiveEffects"]![0]! };
                roots.AddRange(CurrentProfiles(e).Select(profiles => ReadGuardianAttributionProfile(profiles)["activeEffects"]![0]!));
                foreach (var root in roots) { var rows = root["components"]!.AsArray(); Assert.Equal(4, rows.Count); rows.RemoveAt(3); }
            }),
            ("retained resource History prefix in all History copies", "retained-resource-history", e => EditHistory(e, history => history["entries"]![0]!["eventRef"] = "changed_retained_history")),
            ("retained effect identity prefix", "retained-effect-identity", e => e["EffectPlan"]!["IdentityIndexBeforeImage"]!["entries"]![0]!["transitions"]![0]!["eventRef"] = "changed_retained_identity"),
            ("exact ordinary raw bytes", "original-raw", e => e["ordinaryBytesBase64"] = Convert.ToBase64String([.. setup.RawOrdinaryBytes, (byte)' '])),
            ("unrelated actual player strain", "actual-root", e => e["actualRoot"]!["activeConflict"]!["playerSideStrain"] = "strained"),
            ("receipt source copy mismatch", "receipt-source", e => e["receipt"]!["strainComparisons"]![0]!["witness"]!["sourceId"] = "spiritual_source_" + new string('0', 64)),
            ("terminal seal", "effect-terminal", e => e["FullEffectBoundary"]!["UseProjectionOrdinal"] = 6),
            ("EffectAfter fingerprint", "effect-prefix-after", e => e["Interval"]!["EffectAfter"]!["Fingerprint"] = "sha256:" + new string('0', 64)),
            ("terminal fingerprint", "effect-terminal", e => e["FullEffectBoundary"]!["Fingerprint"] = "sha256:" + new string('0', 64)),
            ("closed resource claim with dependent hashes resealed", "closed-resource-hash", e => { e["witness"]!["resourceResultFingerprint"] = "sha256:" + new string('0', 64); ResealWitnessCopies(e); }),
            ("closed effect claim with dependent hashes resealed", "closed-effect-hash", e => { e["witness"]!["effectPlanFingerprint"] = "sha256:" + new string('0', 64); ResealWitnessCopies(e); }),
            ("source fingerprint", "source-hash", e => e["witness"]!["sourceFingerprint"] = "sha256:" + new string('0', 64)),
            ("full effect payload fingerprint", "full-effect-payload-hash", e => e["EffectPlan"]!["AcceptedBoundaryFinalPlanFingerprint"] = "sha256:" + new string('0', 64)),
            ("common prepared fingerprint", "common-prepared-hash", e => e["PreparedPlanFingerprint"] = "sha256:" + new string('0', 64)),
            ("altered RequestedAmount in both transition copies with source/common hashes resealed", "resource-transition", e =>
            {
                e["Interval"]!["AppliedTransitions"]![0]!["RequestedAmount"] = 2; e["AppliedTransitions"]![0]!["RequestedAmount"] = 2;
                var interval = e["Interval"]!;
                e["witness"]!["resourceResultFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { "book_of_eternity.spiritual_closed_resource_result", "1", "conflict_resource_cost", "exchange_guardian_zero_46", "0",
                    interval["AppliedTransitions"]!.ToJsonString(), interval["ReplayTransitions"]!.ToJsonString(), interval["Events"]!.ToJsonString() });
                ResealWitnessCopies(e);
            })
        };
        var count = 0;
        foreach (var probe in probes)
        {
            var changed = ReadGuardianZeroStrictJson(first.EnvelopeJson).AsObject(); probe.Change(changed);
            Assert.False(JsonNode.DeepEquals(changed, ReadGuardianZeroStrictJson(first.EnvelopeJson)), probe.Name + " must change DATA");
            Reject(probe.Name, probe.Guard, () => ReadGuardianZeroRetryAttempt(setup, origin, changed.ToJsonString()));
        }
        var marker = "\"AllocationCursor\":5"; var offset = first.EnvelopeJson.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(offset >= 0); Assert.Equal(offset, first.EnvelopeJson.LastIndexOf(marker, StringComparison.Ordinal));
        var duplicate = first.EnvelopeJson.Insert(offset, marker + ",");
        Reject("duplicate raw JSON key", "envelope-json", () => ReadGuardianZeroRetryAttempt(setup, origin, duplicate));
        var forged = ReadGuardianZeroStrictJson(first.EnvelopeJson).AsObject(); forged["actualRoot"]!["activeConflict"]!["playerSideStrain"] = "strained";
        Reject("forged envelope with stale reported bindings", "actual-root", () => ValidateGuardianZeroReportedAttempt(setup, origin, first with { EnvelopeJson = forged.ToJsonString() }));
        Assert.Equal(26, count); return count;

        void Reject(string name, string expectedGuard, Action work)
        {
            var failure = Record.Exception(work);
            output(JsonSerializer.Serialize(new { phase = "guardian_zero_retry_detached_probe", name, expectedGuard, failure = failure?.ToString() }));
            var rejected = Assert.IsType<GuardianZeroRetryGuardFailure>(failure); Assert.Equal(expectedGuard, rejected.Guard); Assert.NotNull(rejected.InnerException); count++;
        }
        static void EditJournal(JsonObject envelope, Action<JsonArray> change)
        { var rows = ReadGuardianZeroStrictJson(envelope["AllocationJournalJson"]!.GetValue<string>()).AsArray(); change(rows); envelope["AllocationJournalJson"] = rows.ToJsonString(); }
        static void EditState(JsonObject envelope, Action<JsonObject> change)
        {
            var state = envelope["StateAfterImage"]!.DeepClone().AsObject(); change(state); envelope["StateAfterImage"] = state;
            envelope["ResourceStateAfterImage"] = state.ToJsonString(); envelope["PreparedHashOperands"]!["ProjectionInput"]!["State"] = state.DeepClone();
        }
        static void EditHistory(JsonObject envelope, Action<JsonObject> change)
        {
            var history = envelope["HistoryAfterImage"]!.DeepClone().AsObject(); change(history); envelope["HistoryAfterImage"] = history;
            envelope["ResourceHistoryAfterImage"] = history.ToJsonString(); envelope["PreparedHashOperands"]!["ProjectionInput"]!["History"] = history.DeepClone();
        }
        static JsonObject[] CurrentProfiles(JsonObject envelope) => [envelope["EffectPlan"]!["CarrierAfterImages"]![AfterlifeEntityProfileState.StatePath]!.AsObject(),
            envelope["EffectCarrierAfterImages"]![AfterlifeEntityProfileState.StatePath]!.AsObject(), envelope["EffectPlan"]!["ResourceTriggerCarriers"]!["AfterlifeProfiles"]!.AsObject()];
        static void ResealWitnessCopies(JsonObject envelope)
        {
            var witness = envelope["witness"]!.AsObject(); var sourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
            witness["sourceFingerprint"] = sourceHash; witness["sourceId"] = "spiritual_source_" + sourceHash[7..];
            envelope["receipt"]!["strainComparisons"]![0]!["witness"] = witness.DeepClone();
            envelope["OwnerCompanionAfterImages"]![SpiritualWoundOpportunityReceiptState.StatePath]!["strainComparisons"]![0]!["witness"] = witness.DeepClone();
            var effectHash = WoundAcceptedTurnFingerprintWriter.Compute(ReadGuardianZeroEffectPayloadHashFields(envelope["EffectPlan"]!.AsObject()));
            envelope["PreparedPlanFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(ReadGuardianZeroPreparedPlanHashFields(envelope, effectHash));
        }
    }
}
