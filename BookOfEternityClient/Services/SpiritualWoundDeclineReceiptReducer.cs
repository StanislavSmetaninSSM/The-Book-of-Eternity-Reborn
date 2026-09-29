using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Derives unpublished decisions from one completed owner-reconstructed spiritual packet.
/// </summary>
internal static partial class SpiritualWoundDeclineReceiptReducer
{
    private static readonly object ReceiptIssuanceKey = new();
    private const string EmptyReceipt =
        """
        {"schemaVersion":1,"nextInstanceOrdinal":1,"nextClosureOrdinal":1,
         "nextSourceOrdinal":1,"nextDecisionOrdinal":1,"instances":[],
         "closures":[],"sources":[],"decisions":[]}
        """;

    /// <summary>
    /// Appends every source with a positive maximum or guarantee and its owner-proven decision.
    /// </summary>
    /// <param name="signed">
    /// Reauthenticated receipt and conflict images from the original pending snapshot.
    /// </param>
    /// <param name="packet">
    /// Detached completed C2 packet reconstructed by the retained source owner.
    /// </param>
    /// <param name="liveWounds">
    /// Exact completed owner proof for materialized decisions, or <see langword="null"/> when no wound was inserted.
    /// </param>
    /// <param name="authenticatedPair">
    /// Capture-issued proof of the entire completed C2 packet, client-owned satisfaction and terminal closure.
    /// </param>
    /// <returns>
    /// Validated append-only receipt after-image, or issues without a partial ledger.
    /// </returns>
    internal static Result Reduce(ValidationService.SpiritualSignedC1Origin signed,
        JsonObject packet, SpiritualLiveWoundCompletion? liveWounds = null,
        ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair? authenticatedPair = null)
    {
        ArgumentNullException.ThrowIfNull(signed);
        ArgumentNullException.ThrowIfNull(packet);
        try
        {
            var sources = packet["sources"]!.AsArray();
            var staged = packet["stagedDecisions"]!.AsArray();
            var cursor = packet["cursor"]!.AsObject();
            if (SpiritualWoundStateJson.Integer(cursor["nextSourceOrdinal"]) != sources.Count ||
                sources.Count == 0 ||
                staged.Count != sources.Count(source =>
                    SpiritualWoundStateJson.Integer(source!["maximumSeverityRank"]) > 0 ||
                    source["guaranteedSeverityRank"] is not null))
                return Fail("spiritual_c3_decline_packet_incomplete");
            var materializedCount = staged.Count(row => (string?)row?["decision"] == "materialize");
            var satisfiedCount = staged.Count(row => (string?)row?["decision"] == "guarantee_satisfied");
            if (materializedCount != (liveWounds?.Insertions.Count ?? 0))
                return Fail("spiritual_c3_decline_materialization_deferred");
            if ((materializedCount != 0 || satisfiedCount != 0) &&
                authenticatedPair?.Matches(signed, packet) != true)
                return Fail("spiritual_c3_materialization_owner_pair_missing");
            if (authenticatedPair is not null && satisfiedCount != authenticatedPair.SatisfactionCount)
                return Fail("spiritual_c3_satisfaction_owner_count_mismatch");
            var insertions = liveWounds?.Insertions;
            if (SpiritualWoundStateJson.Fingerprint(packet["originalSnapshotFingerprint"]) !=
                signed.SnapshotFingerprint)
                return Fail("spiritual_c3_decline_signed_snapshot_changed");
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(
                signed.Receipt.Existed
                    ? new UTF8Encoding(false, true).GetString(signed.Receipt.Bytes!)
                    : EmptyReceipt,
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!parsed.IsValid || parsed.State is null)
                return new(null, parsed.Issues);
            var before = parsed.State;
            var root = SpiritualWoundStateJson.Parse(
                SpiritualWoundOpportunityReceiptState.SerializeCanonical(before));
            var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
                signed.Receipt, signed.Conflict,
                SpiritualWoundStateJson.Text(packet["realm"]),
                SpiritualWoundStateJson.Text(packet["sessionId"]),
                SpiritualWoundStateJson.Text(packet["requestId"]),
                SpiritualWoundStateJson.Text(packet["snapshotToken"]),
                (int)SpiritualWoundStateJson.Integer(packet["turn"]));
            if (resolved.Issues.Count != 0 || resolved.InstanceId is null)
                return new(null, resolved.Issues);
            if (resolved.InstanceId != SpiritualWoundStateJson.Text(packet["conflictInstanceRef"]))
                return Fail("spiritual_c3_decline_instance_changed");
            if (resolved.ProposedRow is not null)
            {
                root["instances"]!.AsArray().Add(resolved.ProposedRow.DeepClone());
                root["nextInstanceOrdinal"] = checked(
                    (int)SpiritualWoundStateJson.Integer(root["nextInstanceOrdinal"]) + 1);
            }
            var instanceCount = root["sources"]!.AsArray().Count(row =>
                (string?)row?["instanceId"] == resolved.InstanceId);
            var generationByExchange = new Dictionary<long, int>();
            var newWoundByConflictSide = new Dictionary<(string ConflictId, string Side), string>();
            var stagedIndex = 0;
            var insertionIndex = 0;
            foreach (var source in sources)
            {
                var witness = source!.AsObject();
                if (SpiritualWoundStateJson.Integer(witness["maximumSeverityRank"]) == 0 &&
                    witness["guaranteedSeverityRank"] is null)
                    continue;
                var stagedDecision = staged[stagedIndex++]!.AsObject();
                if (!JsonNode.DeepEquals(stagedDecision["sourceId"], witness["sourceId"]) ||
                    !JsonNode.DeepEquals(stagedDecision["sourceOrdinal"], witness["sourceOrdinal"]))
                    return Fail("spiritual_c3_decline_source_changed");
                var kind = (string?)stagedDecision["decision"];
                SpiritualLiveWoundCompletion.Insertion? insertion = null;
                if (kind == "materialize")
                {
                    if (insertions is null || insertionIndex >= insertions.Count)
                        return Fail("spiritual_c3_materialization_proof_missing");
                    insertion = insertions[insertionIndex++];
                    if (!AuthenticatesMaterialization(witness, stagedDecision, insertion))
                        return Fail("spiritual_c3_materialization_proof_mismatch");
                    var conflictSide = (
                        SpiritualWoundStateJson.Text(witness["conflictId"]),
                        SpiritualWoundStateJson.Text(witness["affectedSide"]));
                    if (insertion.Wound.LastTransition.Kind == "create" &&
                        !newWoundByConflictSide.TryAdd(conflictSide, insertion.Wound.WoundId))
                        return Fail("spiritual_c3_duplicate_conflict_side_wound");
                    if (insertion.Wound.LastTransition.Kind == "worsen" &&
                        newWoundByConflictSide.TryGetValue(conflictSide, out var conflictWoundId) &&
                        witness["retraumaWoundId"] is null &&
                        conflictWoundId != insertion.Wound.WoundId)
                        return Fail("spiritual_c3_conflict_side_wound_mismatch");
                }
                else if (kind == "guarantee_satisfied")
                {
                    if (authenticatedPair?.MatchesSatisfaction(signed, packet,
                            witness, stagedDecision) != true)
                        return Fail("spiritual_c3_satisfaction_owner_proof_missing");
                    var conflictSide = (
                        SpiritualWoundStateJson.Text(witness["conflictId"]),
                        SpiritualWoundStateJson.Text(witness["affectedSide"]));
                    if (newWoundByConflictSide.TryGetValue(conflictSide, out var conflictWoundId) &&
                        conflictWoundId != SpiritualWoundStateJson.Text(stagedDecision["satisfiedWoundId"]))
                        return Fail("spiritual_c3_satisfaction_conflict_wound_mismatch");
                }
                else if (kind != "none")
                    return Fail("spiritual_c3_decision_kind_invalid");
                var exchange = SpiritualWoundStateJson.Integer(witness["exchangeOrdinal"]);
                if (!generationByExchange.TryGetValue(exchange, out var generation))
                {
                    generation = generationByExchange.Count + 1;
                    generationByExchange.Add(exchange, generation);
                }
                var ordinal = (int)SpiritualWoundStateJson.Integer(root["nextSourceOrdinal"]);
                var sourceRow = new JsonObject
                {
                    ["ordinal"] = ordinal,
                    ["instanceId"] = resolved.InstanceId,
                    ["instanceSourceOrdinal"] = ++instanceCount,
                    ["witness"] = witness.DeepClone()
                };
                root["sources"]!.AsArray().Add(sourceRow);
                root["nextSourceOrdinal"] = checked(ordinal + 1);
                var evidence = witness["turnEvidence"]!.AsObject();
                var decisionOrdinal = (int)SpiritualWoundStateJson.Integer(root["nextDecisionOrdinal"]);
                var decision = new JsonObject
                {
                    ["ordinal"] = decisionOrdinal,
                    ["decisionId"] = "spiritual_decision_" +
                        SpiritualWoundStateJson.Fingerprint(stagedDecision["decisionFingerprint"])[7..],
                    ["instanceId"] = resolved.InstanceId,
                    ["sourceId"] = witness["sourceId"]!.DeepClone(),
                    ["opportunityRef"] = stagedDecision["opportunityRef"]!.DeepClone(),
                    ["sourceWitness"] = witness.DeepClone(),
                    ["binding"] = new JsonObject
                    {
                        ["sessionId"] = evidence["sessionId"]!.DeepClone(),
                        ["requestId"] = evidence["requestId"]!.DeepClone(),
                        ["snapshotToken"] = evidence["snapshotToken"]!.DeepClone(),
                        ["turn"] = evidence["turn"]!.DeepClone(),
                        ["continuationGeneration"] = generation,
                        ["waveOrdinal"] = stagedDecision["waveOrdinal"]!.DeepClone(),
                        ["sourceOrdinal"] = witness["sourceOrdinal"]!.DeepClone()
                    },
                    ["decision"] = kind,
                    ["selectedSeverityRank"] = insertion?.Wound.Severity.Rank,
                    ["woundId"] = kind == "guarantee_satisfied"
                        ? stagedDecision["satisfiedWoundId"]!.DeepClone()
                        : insertion?.Wound.WoundId,
                    ["transitionId"] = insertion?.Wound.LastTransition.TransitionId,
                    ["sourceFingerprint"] = witness["sourceFingerprint"]!.DeepClone(),
                    ["decisionFingerprint"] = ""
                };
                if (kind == "guarantee_satisfied")
                    decision["satisfiedSeverityRank"] =
                        stagedDecision["satisfiedSeverityRank"]!.DeepClone();
                decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(
                    decision, "decision", "decisionFingerprint");
                root["decisions"]!.AsArray().Add(decision);
                root["nextDecisionOrdinal"] = checked(decisionOrdinal + 1);
            }
            if (insertions is not null && insertionIndex != insertions.Count)
                return Fail("spiritual_c3_materialization_proof_unconsumed");
            var terminal = authenticatedPair?.Terminal;
            if (terminal != null)
            {
                if (!authenticatedPair!.Matches(signed, packet))
                    return Fail("spiritual_c3_terminal_owner_pair_missing");
                var ordinal = (int)SpiritualWoundStateJson.Integer(root["nextClosureOrdinal"]);
                root["closures"]!.AsArray().Add(terminal.CreateClosure(resolved.InstanceId, ordinal));
                root["nextClosureOrdinal"] = checked(ordinal + 1);
            }
            var candidate = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(),
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!candidate.IsValid || candidate.State is null)
                return new(null, candidate.Issues);
            var append = SpiritualWoundOpportunityReceiptState.PlanAppend(before, candidate.State);
            return append.Disposition == "appended" && append.After is not null
                ? Complete(signed, liveWounds,
                    satisfiedCount != 0 || terminal != null ? authenticatedPair!.SatisfactionReceiptIdentity : null,
                    append.After)
                : Fail("spiritual_c3_decline_receipt_conflict");
        }
        catch (Exception error) when (error is JsonException or FormatException or
            InvalidOperationException or OverflowException or ArgumentException or DecoderFallbackException)
        {
            return Fail("spiritual_c3_decline_reduction_invalid");
        }
    }

    /// <summary>
    /// Seals the exact signed receipt image and owner-proven live chain after a successful append.
    /// </summary>
    /// <param name="signed">
    /// Reauthenticated original receipt image.
    /// </param>
    /// <param name="liveWounds">
    /// Completed live wound proof, or <see langword="null"/> when no wound was inserted.
    /// </param>
    /// <param name="satisfactionReceiptIdentity">
    /// Capture-owned identity for an authenticated guarantee result or terminal closure without a wound transition.
    /// </param>
    /// <param name="after">
    /// Strictly validated append-only receipt state.
    /// </param>
    /// <returns>
    /// Detached receipt and an owner-bound join proof when materializations, satisfactions or terminal closure were present.
    /// </returns>
    private static Result Complete(ValidationService.SpiritualSignedC1Origin signed,
        SpiritualLiveWoundCompletion? liveWounds, object? satisfactionReceiptIdentity,
        SpiritualWoundOpportunityReceiptState after)
    {
        var image = SpiritualWoundStateJson.Parse(
            SpiritualWoundOpportunityReceiptState.SerializeCanonical(after));
        return new(image, [], liveWounds is null && satisfactionReceiptIdentity is null ? null :
            new ValidatedReceiptProof(ReceiptIssuanceKey, liveWounds,
                satisfactionReceiptIdentity, signed.Receipt, image));
    }

    /// <summary>
    /// Joins one staged materialization to its actual source-bound wound reducer and history output.
    /// </summary>
    /// <param name="witness">
    /// Owner-reconstructed signed source witness in causal order.
    /// </param>
    /// <param name="staged">
    /// Matching saved C2 decision without authoritative wound identities.
    /// </param>
    /// <param name="insertion">
    /// Next owner-sealed live insertion in wound draft version order.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when the source, choice and history transition agree.
    /// </returns>
    private static bool AuthenticatesMaterialization(JsonObject witness,
        JsonObject staged, SpiritualLiveWoundCompletion.Insertion insertion)
    {
        var input = insertion.Input;
        var opportunity = input.Opportunities.SingleOrDefault();
        var transition = input.Transitions.SingleOrDefault();
        var wound = insertion.Wound;
        var parsedHistory = WoundHistoryState.Parse(
            insertion.ReducedState.History.ToJsonString(), WoundHistoryState.HistoryPath);
        if (opportunity is null || transition is null || !parsedHistory.IsValid ||
            !parsedHistory.State!.TryResolveExactTransition(
                wound.LastTransition.TransitionId, out var history) || history is null)
            return false;
        return insertion.SourceCoordinate == SpiritualWoundStateJson.Text(witness["coordinate"]) &&
            insertion.ExchangeOrdinal == SpiritualWoundStateJson.Integer(witness["exchangeOrdinal"]) &&
            opportunity.PublicRef == SpiritualWoundStateJson.Text(staged["opportunityRef"]) &&
            insertion.ProposalRawFingerprint ==
                SpiritualWoundStateJson.Fingerprint(staged["woundDraftFingerprint"]) &&
            wound.Severity.Rank == SpiritualWoundStateJson.Integer(staged["selectedSeverityRank"]) &&
            transition.Kind is "create" or "worsen" &&
            wound.LastTransition.Kind == transition.Kind &&
            history.Kind == transition.Kind && history.WoundId == wound.WoundId &&
            history.TransitionId == wound.LastTransition.TransitionId &&
            (witness["retraumaWoundId"] is null ||
             wound.WoundId == SpiritualWoundStateJson.Text(witness["retraumaWoundId"]));
    }

    /// <summary>
    /// Carries only a detached validated receipt or rejection diagnostics.
    /// </summary>
    /// <param name="AfterImage">
    /// Complete unpublished receipt root, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Issues">
    /// Fail-closed diagnostics; empty when <paramref name="AfterImage"/> is available.
    /// </param>
    /// <param name="Proof">
    /// Owner-bound proof for materialization, satisfied guarantees or terminal closure; otherwise <see langword="null"/>.
    /// </param>
    internal sealed record Result(JsonObject? AfterImage, IReadOnlyList<ValidationIssue> Issues,
        ValidatedReceiptProof? Proof = null);

    /// <summary>
    /// Proves that a complete decision receipt was joined to an exact owner result.
    /// Only the reducer can mint this witness after strict append validation.
    /// </summary>
    internal sealed class ValidatedReceiptProof
    {
        private readonly SpiritualLiveWoundCompletion? _liveWounds;
        private readonly object? _satisfactionReceiptIdentity;
        private readonly string _beforeFingerprint;
        private readonly string _afterFingerprint;

        /// <summary>
        /// Seals a receipt append that this reducer validated against the owner result.
        /// </summary>
        /// <param name="issuanceKey">
        /// Private reducer key required to issue the proof.
        /// </param>
        /// <param name="liveWounds">
        /// Exact live owner result, or <see langword="null"/> when no wound was inserted.
        /// </param>
        /// <param name="satisfactionReceiptIdentity">
        /// Capture-owned identity for a satisfied guarantee or terminal closure, if any.
        /// </param>
        /// <param name="before">
        /// Signed receipt before-image used by the append.
        /// </param>
        /// <param name="after">
        /// Validated unpublished receipt after-image.
        /// </param>
        internal ValidatedReceiptProof(object issuanceKey,
            SpiritualLiveWoundCompletion? liveWounds,
            object? satisfactionReceiptIdentity,
            CanonicalBeforeImage before, JsonObject after)
        {
            if (!ReferenceEquals(issuanceKey, ReceiptIssuanceKey))
                throw new InvalidOperationException("Only a validated receipt append can issue this proof.");
            _liveWounds = liveWounds;
            _satisfactionReceiptIdentity = satisfactionReceiptIdentity;
            _beforeFingerprint = before.Fingerprint;
            _afterFingerprint = SpiritualWoundStateJson.Hash(after, "accepted_receipt_join");
        }

        /// <summary>
        /// Checks the exact owner proof and receipt images before admitting common assembly.
        /// </summary>
        /// <param name="liveWounds">
        /// Live wound result attached to the completed ordinary reduction, if any.
        /// </param>
        /// <param name="satisfactionReceiptIdentity">
        /// Capture-owned satisfaction or terminal closure identity attached to the completed reduction, if any.
        /// </param>
        /// <param name="before">
        /// Signed original receipt before-image.
        /// </param>
        /// <param name="after">
        /// Detached receipt after-image attached to that reduction.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the reducer's exact completed join.
        /// </returns>
        internal bool Matches(SpiritualLiveWoundCompletion? liveWounds,
            object? satisfactionReceiptIdentity,
            CanonicalBeforeImage before, JsonObject after) =>
            ReferenceEquals(_liveWounds, liveWounds) &&
            ReferenceEquals(_satisfactionReceiptIdentity, satisfactionReceiptIdentity) &&
            _beforeFingerprint == before.Fingerprint &&
            _afterFingerprint == SpiritualWoundStateJson.Hash(after, "accepted_receipt_join");
    }

    private static Result Fail(string code) => new(null,
    [
        new ValidationIssue(SpiritualWoundOpportunityReceiptState.StatePath,
            IssueSeverity.Error, "The completed spiritual wound decisions cannot be reduced.",
            code: code, section: "AcceptedTurnWoundMaterialization")
    ]);
}
