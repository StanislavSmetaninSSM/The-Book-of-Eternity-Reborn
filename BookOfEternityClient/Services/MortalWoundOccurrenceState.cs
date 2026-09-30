using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure, strict state for the signed pending Mortal-wound occurrences.  This type owns
/// neither a filesystem write nor a lease: T070 publishes its canonical after-image.
/// </summary>
internal sealed class MortalWoundOccurrenceState
{
    internal const string StatePath = "game_state/control/pending_mortal_wound_occurrences.json";
    private const int MaximumOccurrences = 32;
    private const int MaximumEvents = 160;
    private readonly ReadOnlyCollection<MortalWoundOccurrence> _occurrences;

    private MortalWoundOccurrenceState(IEnumerable<MortalWoundOccurrence> occurrences)
    {
        _occurrences = Array.AsReadOnly((occurrences ?? throw new ArgumentNullException(nameof(occurrences)))
            .Select(DetachOccurrence)
            .ToArray());
    }

    public IReadOnlyList<MortalWoundOccurrence> Occurrences => _occurrences;

    internal MortalWoundOccurrenceState DetachForReceiptPlan() =>
        new(_occurrences);

    internal MortalWoundOccurrenceState RemoveForReceiptPlan(string occurrenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        if (_occurrences.Count(value => string.Equals(
                value.OccurrenceId,
                occurrenceId,
                StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException(
                "Receipt planning requires exactly one pending Mortal-wound occurrence.");
        }

        return new MortalWoundOccurrenceState(_occurrences.Where(value =>
            !string.Equals(value.OccurrenceId, occurrenceId, StringComparison.Ordinal)));
    }

    public static MortalWoundOccurrenceParseResult Parse(string? json, string path)
    {
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(json))
        {
            Add(issues, path, "mortal_wound_occurrence_invalid_root");
            return new MortalWoundOccurrenceParseResult(false, null, FreezeIssues(issues));
        }

        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException)
        {
            Add(issues, path, "mortal_wound_occurrence_invalid_root");
            return new MortalWoundOccurrenceParseResult(false, null, FreezeIssues(issues));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Add(issues, path, "mortal_wound_occurrence_invalid_root");
                return new MortalWoundOccurrenceParseResult(false, null, FreezeIssues(issues));
            }

            ResourceMaterializationContract.FindDuplicateProperties(root, path, issues, "mortal_wound_occurrence_duplicate_property");
            Closed(root, path, RootFields, issues);
            var schemaVersion = Int(root, "schemaVersion", path, issues, required: true);
            if (schemaVersion is not 1) Add(issues, path + ".schemaVersion", "mortal_wound_occurrence_invalid_field");
            if (!root.TryGetProperty("occurrences", out var rows))
                Add(issues, path + ".occurrences", "mortal_wound_occurrence_missing_field");
            else if (rows.ValueKind != JsonValueKind.Array)
                Add(issues, path + ".occurrences", "mortal_wound_occurrence_invalid_field");
            else if (rows.GetArrayLength() > MaximumOccurrences)
                Add(issues, path + ".occurrences", "mortal_wound_occurrence_limit_exceeded");

            var parsed = new List<MortalWoundOccurrence>();
            if (rows.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var row in rows.EnumerateArray())
                {
                    var value = ParseOccurrence(row, $"{path}.occurrences[{index++}]", issues);
                    if (value is not null) parsed.Add(value);
                }
            }

            ValidatePersistedSet(parsed, path, issues);
            if (issues.Count != 0) return new MortalWoundOccurrenceParseResult(false, null, FreezeIssues(issues));
            return new MortalWoundOccurrenceParseResult(true, new MortalWoundOccurrenceState(parsed), FreezeIssues(issues));
        }
    }

    public static string SerializeCanonical(MortalWoundOccurrenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WritePropertyName("occurrences");
            writer.WriteStartArray();
            foreach (var value in state.Occurrences) WriteOccurrence(writer, value);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ComputeOccurrenceFingerprint(MortalWoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        return Hash("book_of_eternity.mortal_wound.occurrence", "1",
            occurrence.OccurrenceId, occurrence.OpportunityRef, occurrence.CandidateFingerprint);
    }

    public static MortalWoundOccurrenceAppendPlanResult PlanAppend(
        MortalWoundOccurrenceState before,
        MortalWoundOccurrenceCandidateBatch batch,
        MortalWoundOpportunityReceiptState consumedReceipts)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(consumedReceipts);
        var issues = new List<ValidationIssue>();
        var priorAgreementIssues = MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
            before,
            consumedReceipts);
        if (priorAgreementIssues.Count != 0)
        {
            issues.AddRange(priorAgreementIssues);
            return new MortalWoundOccurrenceAppendPlanResult("conflict", FreezeIssues(issues), null, null);
        }

        var candidates = batch.Candidates?.ToArray() ?? Array.Empty<MortalWoundOccurrenceCandidate>();
        if (!TryBuildBatch(candidates, StatePath, issues, out var built, out var batchFingerprint))
            return new MortalWoundOccurrenceAppendPlanResult("conflict", FreezeIssues(issues), null, null);

        var consumed = ConsumedOccurrences(consumedReceipts).ToArray();
        var allKnown = before.Occurrences.Concat(consumed).ToArray();

        var exact = built.All(candidate =>
            before.Occurrences.Any(existing => SameOccurrence(existing, candidate)) ||
            consumedReceipts.Receipts.Any(receipt => ReceiptMatchesOccurrence(receipt, candidate)));
        var sourceKeyCollision = allKnown.Any(existing => string.Equals(existing.ProducerOperationKey, built[0].ProducerOperationKey, StringComparison.Ordinal));
        if (exact)
            return new MortalWoundOccurrenceAppendPlanResult("exact_replay", FreezeIssues(issues),
                new MortalWoundOccurrenceState(before.Occurrences), batchFingerprint);
        if (sourceKeyCollision || before.Occurrences.Count + built.Count > MaximumOccurrences ||
            built.Any(candidate => allKnown.Any(existing => ConfusableProducerKey(existing, candidate))) ||
            built.Any(candidate => allKnown.Any(existing => SameTuple(existing, candidate) ||
                ConfusableTuple(existing, candidate))))
        {
            Add(issues, StatePath, "mortal_wound_occurrence_batch_agreement_mismatch");
            return new MortalWoundOccurrenceAppendPlanResult("conflict", FreezeIssues(issues), null, null);
        }

        // Persisted occurrence chronology is append-only.  TryBuildBatch has already
        // canonicalized the new group by its producer ordinal; ordering older groups
        // again would alter the accepted before-image on an otherwise new append.
        var after = before.Occurrences.Concat(built).ToArray();
        return new MortalWoundOccurrenceAppendPlanResult("appended", FreezeIssues(issues),
            new MortalWoundOccurrenceState(after), batchFingerprint);
    }

    private static MortalWoundOccurrence? ParseOccurrence(JsonElement row, string path, List<ValidationIssue> issues)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path, "mortal_wound_occurrence_invalid_field");
            return null;
        }
        ResourceMaterializationContract.FindDuplicateProperties(row, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(row, path, RowFields, issues);
        var occurrenceId = Text(row, "occurrenceId", path, issues);
        var opportunityRef = Text(row, "opportunityRef", path, issues);
        var session = Text(row, "sourceSessionId", path, issues);
        var request = Text(row, "sourceRequestId", path, issues);
        var snapshot = Text(row, "sourceSnapshotToken", path, issues);
        var turn = Int(row, "sourceTurn", path, issues, true);
        var key = Text(row, "producerOperationKey", path, issues);
        var ordinal = Int(row, "producerCandidateOrdinal", path, issues, true);
        var count = Int(row, "producerCandidateCount", path, issues, true);
        var adapter = Text(row, "adapterKind", path, issues);
        var eventOrdinal = Int(row, "acceptedEventOrdinal", path, issues, true);
        var events = ParseEvents(row, path, issues);
        var eventsFingerprint = Text(row, "acceptedEventsFingerprint", path, issues);
        var owner = ParseOwner(Property(row, "owner", path, issues), path + ".owner", issues);
        var domain = Text(row, "domain", path, issues);
        var profile = Text(row, "profileKey", path, issues);
        var source = ParseSource(Property(row, "source", path, issues), path + ".source", issues);
        var outcome = ParseOutcome(Property(row, "outcome", path, issues), path + ".outcome", issues);
        var hard = Int(row, "hardMaximumSeverityRank", path, issues, true);
        var minimum = NullableInt(row, "minimumSeverityRank", path, issues);
        var guarantee = ParseGuarantee(Property(row, "guaranteedTrigger", path, issues), path + ".guaranteedTrigger", issues);
        var safe = ParseSafe(Property(row, "safeContext", path, issues), path + ".safeContext", issues);
        var target = ParseTarget(row, path, issues);
        var sourceResult = Text(row, "sourceResultFingerprint", path, issues);
        var candidateFingerprint = Text(row, "candidateFingerprint", path, issues);
        var occurrenceFingerprint = Text(row, "occurrenceFingerprint", path, issues);

        if (new[] { occurrenceId, opportunityRef, session, request, snapshot, key, adapter, eventsFingerprint, domain, profile,
                sourceResult, candidateFingerprint, occurrenceFingerprint }.Any(value => value is null) ||
            turn is null || ordinal is null || count is null || eventOrdinal is null || hard is null || owner is null ||
            source is null || outcome is null || safe is null)
            return null;

        var occurrence = new MortalWoundOccurrence(occurrenceId!, opportunityRef!, session!, request!, snapshot!, turn.Value,
            key!, ordinal.Value, count.Value, adapter!, eventOrdinal.Value, events, eventsFingerprint!, owner, domain!, profile!,
            source, outcome, hard.Value, minimum, guarantee, safe, target, sourceResult!, candidateFingerprint!, occurrenceFingerprint!);
        ValidateOccurrence(occurrence, path, issues);
        return occurrence;
    }

    private static void ValidateOccurrence(MortalWoundOccurrence value, string path, List<ValidationIssue> issues)
    {
        if (!Adapters.Contains(value.AdapterKind) || value.Domain != "physical" || value.Outcome.Kind != "harmful" ||
            value.Outcome.MaximumSeverityRank is < 1 or > 4 || value.HardMaximumSeverityRank is < 1 or > 4 ||
            value.ProducerCandidateCount <= 0 || value.ProducerCandidateOrdinal < 0 ||
            value.ProducerCandidateOrdinal >= value.ProducerCandidateCount || value.SourceTurn <= 0)
            Add(issues, path, "mortal_wound_occurrence_invalid_field");
        ValidateExact(value.SourceSessionId, path + ".sourceSessionId", issues);
        ValidateExact(value.SourceRequestId, path + ".sourceRequestId", issues);
        ValidateExact(value.SourceSnapshotToken, path + ".sourceSnapshotToken", issues);
        ValidateExact(value.ProducerOperationKey, path + ".producerOperationKey", issues);
        ValidateExact(value.ProfileKey, path + ".profileKey", issues);
        ValidateExact(value.Source.Kind, path + ".source.kind", issues);
        ValidateExact(value.Source.SourceId, path + ".source.sourceId", issues);
        ValidateExact(value.Source.State, path + ".source.state", issues);
        ValidateFingerprint(value.SourceResultFingerprint, path + ".sourceResultFingerprint", issues);
        if (value.ProducerCandidateCount > MaximumOccurrences)
            Add(issues, path + ".producerCandidateCount", "mortal_wound_occurrence_invalid_field");
        if (!OwnerIsMortal(value.Owner)) Add(issues, path + ".owner.carrierPath", "mortal_wound_occurrence_invalid_owner_mapping");
        if (!Readable(value.Outcome.ReadableCause)) Add(issues, path + ".outcome.readableCause", "mortal_wound_occurrence_invalid_readable_text");
        if (value.AcceptedEvents.Count is < 1 or > MaximumEvents)
            Add(issues, path + ".acceptedEvents", value.AcceptedEvents.Count > MaximumEvents ? "mortal_wound_occurrence_limit_exceeded" : "mortal_wound_occurrence_invalid_event_set");
        for (var index = 0; index < value.AcceptedEvents.Count; index++)
        {
            var acceptedEvent = value.AcceptedEvents[index];
            var eventPath = $"{path}.acceptedEvents[{index}]";
            ValidateExact(acceptedEvent.EventRef, eventPath + ".eventRef", issues);
            ValidateExact(acceptedEvent.Kind, eventPath + ".kind", issues);
            ValidateExact(acceptedEvent.AuthorityId, eventPath + ".authorityId", issues);
            ValidateFingerprint(acceptedEvent.SemanticFingerprint, eventPath + ".semanticFingerprint", issues);
        }
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        var eventAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < value.AcceptedEvents.Count; index++)
        {
            var acceptedEvent = value.AcceptedEvents[index];
            if (!Exact(acceptedEvent.EventRef) || !Exact(acceptedEvent.AuthorityId))
                Add(issues, $"{path}.acceptedEvents[{index}]", "mortal_wound_occurrence_invalid_event_set");
            Unique(acceptedEvent.EventRef, $"{path}.acceptedEvents[{index}].eventRef", eventRefs, eventAliases, issues);
        }
        if (value.AcceptedEventOrdinal < 0 || value.AcceptedEventOrdinal >= value.AcceptedEvents.Count)
            Add(issues, path + ".acceptedEventOrdinal", "mortal_wound_occurrence_invalid_event_ordinal");
        if (!string.Equals(value.AcceptedEventsFingerprint, ComputeAcceptedEventsFingerprint(value.AcceptedEvents), StringComparison.Ordinal))
            Add(issues, path + ".acceptedEventsFingerprint", "mortal_wound_occurrence_accepted_events_fingerprint_mismatch");
        ValidateSafe(value.SafeContext, path + ".safeContext", issues);
        ValidateGuarantee(value, path, issues);
        if (value.WorseningTarget is not null &&
            (value.WorseningTarget.CauseKind is not ("deterioration" or "retrauma") || !Exact(value.WorseningTarget.WoundId)))
            Add(issues, path + ".worseningTarget", "mortal_wound_occurrence_invalid_worsening_target");
        var candidate = ComputeCandidateFingerprint(value);
        if (!string.Equals(value.CandidateFingerprint, candidate, StringComparison.Ordinal))
            Add(issues, path + ".candidateFingerprint", "mortal_wound_occurrence_candidate_fingerprint_mismatch");
        var hex = candidate.StartsWith("sha256:", StringComparison.Ordinal) ? candidate[7..] : string.Empty;
        if (!string.Equals(value.OccurrenceId, "mortal_wound_occurrence_" + hex, StringComparison.Ordinal))
            Add(issues, path + ".occurrenceId", "mortal_wound_occurrence_invalid_field");
        if (!string.Equals(value.OpportunityRef, "mortal_wound_" + hex, StringComparison.Ordinal))
            Add(issues, path + ".opportunityRef", "mortal_wound_occurrence_invalid_field");
        if (!string.Equals(value.OccurrenceFingerprint, ComputeOccurrenceFingerprint(value), StringComparison.Ordinal))
            Add(issues, path + ".occurrenceFingerprint", "mortal_wound_occurrence_fingerprint_mismatch");
    }

    private static void ValidateGuarantee(MortalWoundOccurrence value, string path, List<ValidationIssue> issues)
    {
        if (value.MinimumSeverityRank is null && value.GuaranteedTrigger is null) return;
        if (value.MinimumSeverityRank is null || value.GuaranteedTrigger is null)
        {
            Add(issues, value.MinimumSeverityRank is null ? path + ".minimumSeverityRank" : path + ".guaranteedTrigger",
                "mortal_wound_occurrence_invalid_field");
            return;
        }
        var guarantee = value.GuaranteedTrigger;
        var minimum = value.MinimumSeverityRank.Value;
        ValidateExact(guarantee.TriggerId, path + ".guaranteedTrigger.triggerId", issues);
        ValidateExact(guarantee.SourceKind, path + ".guaranteedTrigger.sourceKind", issues);
        ValidateExact(guarantee.SourceId, path + ".guaranteedTrigger.sourceId", issues);
        ValidateExact(guarantee.SourceState, path + ".guaranteedTrigger.sourceState", issues);
        ValidateExact(guarantee.Realm, path + ".guaranteedTrigger.realm", issues);
        ValidateExact(guarantee.Domain, path + ".guaranteedTrigger.domain", issues);
        ValidateFingerprint(
            guarantee.SourceContractFingerprint,
            path + ".guaranteedTrigger.sourceContractFingerprint",
            issues);
        if (minimum is < 1 or > 4)
            Add(issues, path + ".minimumSeverityRank", "mortal_wound_occurrence_invalid_field");
        if (guarantee.RequiredSeverityRank is < 1 or > 4)
            Add(issues, path + ".guaranteedTrigger.requiredSeverityRank", "mortal_wound_occurrence_invalid_field");
        if (guarantee.RequiredSeverityRank != minimum ||
            minimum > value.Outcome.MaximumSeverityRank || minimum > value.HardMaximumSeverityRank ||
            guarantee.SourceState != "active" || guarantee.SourceState != value.Source.State ||
            guarantee.MaterializedAtTurn <= 0 || guarantee.MaterializedAtTurn >= value.SourceTurn ||
            guarantee.SourceKind != value.Source.Kind || guarantee.SourceId != value.Source.SourceId || guarantee.Realm != value.Owner.Realm ||
            guarantee.Domain != value.Domain || guarantee.Owner != value.Owner)
            Add(issues, path + ".guaranteedTrigger", "mortal_wound_occurrence_invalid_field");
        if (!string.Equals(guarantee.AuthorityFingerprint, ComputeGuaranteeFingerprint(guarantee), StringComparison.Ordinal))
            Add(issues, path + ".guaranteedTrigger.authorityFingerprint", "mortal_wound_occurrence_invalid_field");
    }

    private static bool TryBuildBatch(IReadOnlyList<MortalWoundOccurrenceCandidate> candidates, string path,
        List<ValidationIssue> issues, out List<MortalWoundOccurrence> built, out string? fingerprint)
    {
        built = new List<MortalWoundOccurrence>();
        fingerprint = null;
        if (candidates.Count == 0) { Add(issues, path, "mortal_wound_occurrence_batch_agreement_mismatch"); return false; }
        if (candidates.Any(value => value is null || value.AcceptedEvents is null || value.Owner is null || value.Source is null ||
                                    value.Outcome is null || value.SafeContext is null || string.IsNullOrEmpty(value.SourceSessionId) ||
                                    string.IsNullOrEmpty(value.SourceRequestId) || string.IsNullOrEmpty(value.ProducerOperationKey) ||
                                    string.IsNullOrEmpty(value.AdapterKind) || string.IsNullOrEmpty(value.Domain) ||
                                    string.IsNullOrEmpty(value.ProfileKey) || string.IsNullOrEmpty(value.SourceResultFingerprint)))
        {
            Add(issues, path, "mortal_wound_occurrence_invalid_field"); return false;
        }
        var first = candidates[0];
        if (candidates.Any(value => value.ProducerOperationKey != first.ProducerOperationKey ||
                                    value.ProducerCandidateCount != first.ProducerCandidateCount) ||
            first.ProducerCandidateCount != candidates.Count ||
            candidates.Select(value => value.ProducerCandidateOrdinal).Order().SequenceEqual(Enumerable.Range(0, candidates.Count)) is false)
        {
            Add(issues, path, "mortal_wound_occurrence_batch_agreement_mismatch"); return false;
        }
        var ordered = candidates.OrderBy(value => value.ProducerCandidateOrdinal).ToArray();
        var eventFingerprint = ComputeAcceptedEventsFingerprint(ordered[0].AcceptedEvents);
        if (ordered.Any(value => !SameBatchAuthority(ordered[0], value) ||
                                 !value.AcceptedEvents.SequenceEqual(ordered[0].AcceptedEvents) ||
                                 ComputeAcceptedEventsFingerprint(value.AcceptedEvents) != eventFingerprint))
        {
            Add(issues, path, "mortal_wound_occurrence_batch_agreement_mismatch"); return false;
        }
        foreach (var candidate in ordered)
        {
            var guarantee = candidate.GuaranteedTrigger is null ? null : ToAuthority(candidate.GuaranteedTrigger);
            var occurrence = new MortalWoundOccurrence(string.Empty, string.Empty, candidate.SourceSessionId, candidate.SourceRequestId,
                candidate.SourceSnapshotToken, candidate.SourceTurn, candidate.ProducerOperationKey, candidate.ProducerCandidateOrdinal, candidate.ProducerCandidateCount,
                candidate.AdapterKind, candidate.AcceptedEventOrdinal, candidate.AcceptedEvents, eventFingerprint, candidate.Owner,
                candidate.Domain, candidate.ProfileKey, candidate.Source, candidate.Outcome, candidate.HardMaximumSeverityRank,
                candidate.MinimumSeverityRank, guarantee, candidate.SafeContext, candidate.WorseningTarget, candidate.SourceResultFingerprint,
                string.Empty, string.Empty);
            var candidateFingerprint = ComputeCandidateFingerprint(occurrence);
            var hex = candidateFingerprint[7..];
            occurrence = occurrence with
            {
                OccurrenceId = "mortal_wound_occurrence_" + hex,
                OpportunityRef = "mortal_wound_" + hex,
                CandidateFingerprint = candidateFingerprint
            };
            occurrence = occurrence with { OccurrenceFingerprint = ComputeOccurrenceFingerprint(occurrence) };
            var localIssues = new List<ValidationIssue>();
            ValidateOccurrence(occurrence, path, localIssues);
            if (localIssues.Count != 0) { issues.AddRange(localIssues); return false; }
            built.Add(occurrence);
        }
        fingerprint = ComputeBatchFingerprint(built);
        return true;
    }

    private static bool SameBatchAuthority(MortalWoundOccurrenceCandidate first, MortalWoundOccurrenceCandidate other) =>
        first.SourceSessionId == other.SourceSessionId && first.SourceRequestId == other.SourceRequestId &&
        first.SourceSnapshotToken == other.SourceSnapshotToken && first.SourceTurn == other.SourceTurn &&
        first.AdapterKind == other.AdapterKind &&
        first.SourceResultFingerprint == other.SourceResultFingerprint;

    private static bool SameOccurrence(MortalWoundOccurrence left, MortalWoundOccurrence right) =>
        left.CandidateFingerprint == right.CandidateFingerprint && left.OccurrenceFingerprint == right.OccurrenceFingerprint;

    private static bool ReceiptMatchesOccurrence(
        MortalWoundOpportunityReceipt receipt,
        MortalWoundOccurrence occurrence)
    {
        var selectedEvent = occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal];
        return receipt.OpportunityId == occurrence.OccurrenceId &&
            receipt.SourceSessionId == occurrence.SourceSessionId &&
            receipt.SourceRequestId == occurrence.SourceRequestId &&
            receipt.SourceSnapshotToken == occurrence.SourceSnapshotToken &&
            receipt.SourceTurn == occurrence.SourceTurn &&
            receipt.ProducerOperationKey == occurrence.ProducerOperationKey &&
            receipt.ProducerCandidateOrdinal == occurrence.ProducerCandidateOrdinal &&
            receipt.ProducerCandidateCount == occurrence.ProducerCandidateCount &&
            receipt.EventRef == selectedEvent.EventRef &&
            receipt.EventSemanticFingerprint == selectedEvent.SemanticFingerprint &&
            receipt.SourceResultFingerprint == occurrence.SourceResultFingerprint &&
            receipt.CandidateFingerprint == occurrence.CandidateFingerprint &&
            receipt.OccurrenceFingerprint == occurrence.OccurrenceFingerprint;
    }

    private static bool SameTuple(MortalWoundOccurrence left, MortalWoundOccurrence right) =>
        left.ProducerOperationKey == right.ProducerOperationKey && left.ProducerCandidateOrdinal == right.ProducerCandidateOrdinal;
    private static bool ConfusableTuple(MortalWoundOccurrence left, MortalWoundOccurrence right) =>
        left.ProducerCandidateOrdinal == right.ProducerCandidateOrdinal &&
        MortalLocationIdentityState.BuildConfusableKey(left.ProducerOperationKey) ==
        MortalLocationIdentityState.BuildConfusableKey(right.ProducerOperationKey);
    private static bool ConfusableProducerKey(MortalWoundOccurrence left, MortalWoundOccurrence right) =>
        MortalLocationIdentityState.BuildConfusableKey(left.ProducerOperationKey) ==
        MortalLocationIdentityState.BuildConfusableKey(right.ProducerOperationKey);
    // T064-B exposes parsed immutable receipts.  This state needs only their consumed
    // source tuple and seals; it never accepts caller-supplied flattened receipt data.
    private static IEnumerable<MortalWoundOccurrence> ConsumedOccurrences(MortalWoundOpportunityReceiptState receipts) =>
        receipts.Receipts.Select(receipt => new MortalWoundOccurrence(
            receipt.OpportunityId, string.Empty, receipt.SourceSessionId, receipt.SourceRequestId,
            receipt.SourceSnapshotToken, receipt.SourceTurn,
            receipt.ProducerOperationKey, receipt.ProducerCandidateOrdinal, receipt.ProducerCandidateCount, string.Empty, 0,
            Array.Empty<WoundAcceptedEventAuthority>(), string.Empty,
            new WoundOwnerCoordinate(string.Empty, string.Empty, string.Empty, string.Empty), string.Empty, string.Empty,
            new MortalWoundOccurrenceSource(string.Empty, string.Empty, string.Empty),
            new MortalWoundOccurrenceOutcome(string.Empty, 0, string.Empty), 0, null, null,
            new WoundOpportunitySafeContext(string.Empty, string.Empty, Array.Empty<string>()), null,
            receipt.SourceResultFingerprint, receipt.CandidateFingerprint, receipt.OccurrenceFingerprint));

    private static void ValidatePersistedSet(IReadOnlyList<MortalWoundOccurrence> values, string path, List<ValidationIssue> issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var idAliases = new HashSet<string>(StringComparer.Ordinal);
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var refAliases = new HashSet<string>(StringComparer.Ordinal);
        var tuples = new HashSet<string>(StringComparer.Ordinal);
        var tupleAliases = new HashSet<string>(StringComparer.Ordinal);
        var producerAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var completedBatchKeys = new HashSet<string>(StringComparer.Ordinal);
        string? activeBatchKey = null;
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index]; var rowPath = $"{path}.occurrences[{index}]";
            Unique(value.OccurrenceId, rowPath + ".occurrenceId", ids, idAliases, issues);
            Unique(value.OpportunityRef, rowPath + ".opportunityRef", refs, refAliases, issues);
            Unique(value.ProducerOperationKey + "\u001f" + value.ProducerCandidateOrdinal, rowPath + ".producerOperationKey", tuples, tupleAliases, issues);
            var producerAlias = MortalLocationIdentityState.BuildConfusableKey(value.ProducerOperationKey);
            if (producerAliases.TryGetValue(producerAlias, out var priorProducerKey) &&
                !string.Equals(priorProducerKey, value.ProducerOperationKey, StringComparison.Ordinal))
            {
                Add(issues, rowPath + ".producerOperationKey", "mortal_wound_occurrence_invalid_field");
            }
            else
            {
                producerAliases[producerAlias] = value.ProducerOperationKey;
            }
            if (!string.Equals(activeBatchKey, value.ProducerOperationKey, StringComparison.Ordinal))
            {
                if (activeBatchKey is not null) completedBatchKeys.Add(activeBatchKey);
                if (completedBatchKeys.Contains(value.ProducerOperationKey))
                    Add(issues, rowPath, "mortal_wound_occurrence_batch_agreement_mismatch");
                activeBatchKey = value.ProducerOperationKey;
            }
            if (index > 0 && string.Equals(values[index - 1].ProducerOperationKey, value.ProducerOperationKey, StringComparison.Ordinal) &&
                values[index - 1].ProducerCandidateOrdinal >= value.ProducerCandidateOrdinal)
                Add(issues, rowPath + ".producerCandidateOrdinal", "mortal_wound_occurrence_batch_agreement_mismatch");
        }
        foreach (var group in values.Select((value, index) => (value, index)).GroupBy(item => item.value.ProducerOperationKey, StringComparer.Ordinal))
        {
            var first = group.First();
            var ordered = group.OrderBy(item => item.index).ToArray();
            foreach (var item in ordered.Skip(1))
            {
                var rowPath = $"{path}.occurrences[{item.index}]";
                if (!SameStoredBatchAuthority(first.value, item.value))
                    Add(issues, rowPath, "mortal_wound_occurrence_batch_agreement_mismatch");
                if (!item.value.AcceptedEvents.SequenceEqual(first.value.AcceptedEvents))
                    Add(issues, rowPath + ".acceptedEvents", "mortal_wound_occurrence_batch_agreement_mismatch");
            }
            if (ordered.Length == first.value.ProducerCandidateCount)
            {
                for (var ordinal = 0; ordinal < ordered.Length; ordinal++)
                {
                    if (ordered[ordinal].value.ProducerCandidateOrdinal == ordinal) continue;
                    Add(issues, $"{path}.occurrences[{ordered[ordinal].index}].producerCandidateOrdinal",
                        "mortal_wound_occurrence_batch_agreement_mismatch");
                    Add(issues, $"{path}.occurrences[{ordered[ordinal].index}]",
                        "mortal_wound_occurrence_batch_agreement_mismatch");
                    break;
                }
            }
        }
    }

    private static MortalWoundOccurrence DetachOccurrence(MortalWoundOccurrence value)
    {
        var events = Array.AsReadOnly(value.AcceptedEvents
            .Select(item => new WoundAcceptedEventAuthority(item.EventRef, item.Kind, item.AuthorityId, item.SemanticFingerprint))
            .ToArray());
        var owner = new WoundOwnerCoordinate(value.Owner.Realm, value.Owner.OwnerKind, value.Owner.OwnerId, value.Owner.CarrierPath);
        var source = new MortalWoundOccurrenceSource(value.Source.Kind, value.Source.SourceId, value.Source.State);
        var outcome = new MortalWoundOccurrenceOutcome(value.Outcome.Kind, value.Outcome.MaximumSeverityRank, value.Outcome.ReadableCause);
        var safe = new WoundOpportunitySafeContext(value.SafeContext.Target, value.SafeContext.Cause,
            Array.AsReadOnly(value.SafeContext.AllowedLocationKinds.ToArray()));
        var target = value.WorseningTarget is null ? null : new MortalWoundOccurrenceWorseningTarget(
            value.WorseningTarget.WoundId, value.WorseningTarget.CauseKind);
        var guarantee = value.GuaranteedTrigger is null ? null : new WoundGuaranteedTriggerAuthority(
            value.GuaranteedTrigger.TriggerId, value.GuaranteedTrigger.SourceKind, value.GuaranteedTrigger.SourceId,
            value.GuaranteedTrigger.SourceState, value.GuaranteedTrigger.Realm, value.GuaranteedTrigger.Domain,
            new WoundOwnerCoordinate(value.GuaranteedTrigger.Owner.Realm, value.GuaranteedTrigger.Owner.OwnerKind,
                value.GuaranteedTrigger.Owner.OwnerId, value.GuaranteedTrigger.Owner.CarrierPath),
            value.GuaranteedTrigger.RequiredSeverityRank, value.GuaranteedTrigger.MaterializedAtTurn,
            value.GuaranteedTrigger.SourceContractFingerprint, value.GuaranteedTrigger.AuthorityFingerprint);
        return value with
        {
            AcceptedEvents = events,
            Owner = owner,
            Source = source,
            Outcome = outcome,
            SafeContext = safe,
            WorseningTarget = target,
            GuaranteedTrigger = guarantee
        };
    }

    private static bool SameStoredBatchAuthority(MortalWoundOccurrence first, MortalWoundOccurrence other) =>
        first.SourceSessionId == other.SourceSessionId && first.SourceRequestId == other.SourceRequestId &&
        first.SourceSnapshotToken == other.SourceSnapshotToken && first.SourceTurn == other.SourceTurn &&
        first.ProducerCandidateCount == other.ProducerCandidateCount && first.AdapterKind == other.AdapterKind &&
        first.SourceResultFingerprint == other.SourceResultFingerprint && first.AcceptedEventsFingerprint == other.AcceptedEventsFingerprint;

    private static void Unique(string value, string path, HashSet<string> exact, HashSet<string> aliases, List<ValidationIssue> issues)
    {
        if (!exact.Add(value) || !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            Add(issues, path, "mortal_wound_occurrence_invalid_field");
    }

    private static IReadOnlyList<WoundAcceptedEventAuthority> ParseEvents(JsonElement row, string path, List<ValidationIssue> issues)
    {
        if (!row.TryGetProperty("acceptedEvents", out var array))
        {
            Add(issues, path + ".acceptedEvents", "mortal_wound_occurrence_missing_field"); return Array.Empty<WoundAcceptedEventAuthority>();
        }
        if (array.ValueKind != JsonValueKind.Array)
        {
            Add(issues, path + ".acceptedEvents", "mortal_wound_occurrence_invalid_field"); return Array.Empty<WoundAcceptedEventAuthority>();
        }
        var result = new List<WoundAcceptedEventAuthority>(); var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.acceptedEvents[{index++}]";
            if (item.ValueKind != JsonValueKind.Object) { Add(issues, itemPath, "mortal_wound_occurrence_invalid_field"); continue; }
            ResourceMaterializationContract.FindDuplicateProperties(item, itemPath, issues, "mortal_wound_occurrence_duplicate_property");
            Closed(item, itemPath, EventFields, issues);
            var eventRef = Text(item, "eventRef", itemPath, issues);
            var kind = Text(item, "kind", itemPath, issues);
            var authority = Text(item, "authorityId", itemPath, issues);
            var semantic = Text(item, "semanticFingerprint", itemPath, issues);
            if (eventRef is not null && kind is not null && authority is not null && semantic is not null)
                result.Add(new WoundAcceptedEventAuthority(eventRef, kind, authority, semantic));
        }
        return result;
    }

    private static WoundOwnerCoordinate? ParseOwner(JsonElement? element, string path, List<ValidationIssue> issues)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) { Add(issues, path, "mortal_wound_occurrence_missing_field"); return null; }
        ResourceMaterializationContract.FindDuplicateProperties(objectValue, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(objectValue, path, OwnerFields, issues);
        if (!HasAny(objectValue, OwnerFields)) Add(issues, path, "mortal_wound_occurrence_missing_field");
        var realm = Text(objectValue, "realm", path, issues); var kind = Text(objectValue, "ownerKind", path, issues);
        var id = Text(objectValue, "ownerId", path, issues); var carrier = Text(objectValue, "carrierPath", path, issues);
        return realm is null || kind is null || id is null || carrier is null ? null : new WoundOwnerCoordinate(realm, kind, id, carrier);
    }

    private static MortalWoundOccurrenceSource? ParseSource(JsonElement? element, string path, List<ValidationIssue> issues)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) { Add(issues, path, "mortal_wound_occurrence_missing_field"); return null; }
        ResourceMaterializationContract.FindDuplicateProperties(objectValue, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(objectValue, path, SourceFields, issues);
        if (!HasAny(objectValue, SourceFields)) Add(issues, path, "mortal_wound_occurrence_missing_field");
        var kind = Text(objectValue, "kind", path, issues); var id = Text(objectValue, "sourceId", path, issues); var state = Text(objectValue, "state", path, issues);
        return kind is null || id is null || state is null ? null : new MortalWoundOccurrenceSource(kind, id, state);
    }

    private static MortalWoundOccurrenceOutcome? ParseOutcome(JsonElement? element, string path, List<ValidationIssue> issues)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) { Add(issues, path, "mortal_wound_occurrence_missing_field"); return null; }
        ResourceMaterializationContract.FindDuplicateProperties(objectValue, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(objectValue, path, OutcomeFields, issues);
        if (!HasAny(objectValue, OutcomeFields)) Add(issues, path, "mortal_wound_occurrence_missing_field");
        var kind = Text(objectValue, "kind", path, issues); var max = Int(objectValue, "maximumSeverityRank", path, issues, true);
        var cause = ReadableText(objectValue, "readableCause", path, issues);
        return kind is null || max is null || cause is null ? null : new MortalWoundOccurrenceOutcome(kind, max.Value, cause);
    }

    private static WoundOpportunitySafeContext? ParseSafe(JsonElement? element, string path, List<ValidationIssue> issues)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectValue) { Add(issues, path, "mortal_wound_occurrence_missing_field"); return null; }
        ResourceMaterializationContract.FindDuplicateProperties(objectValue, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(objectValue, path, SafeFields, issues);
        if (!HasAny(objectValue, SafeFields)) Add(issues, path, "mortal_wound_occurrence_missing_field");
        var target = ReadableText(objectValue, "target", path, issues);
        var cause = ReadableText(objectValue, "cause", path, issues);
        if (!objectValue.TryGetProperty("allowedLocationKinds", out var locations)) { Add(issues, path + ".allowedLocationKinds", "mortal_wound_occurrence_missing_field"); return null; }
        if (locations.ValueKind != JsonValueKind.Array) { Add(issues, path + ".allowedLocationKinds", "mortal_wound_occurrence_invalid_safe_context"); return null; }
        var values = new List<string>();
        foreach (var item in locations.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) { Add(issues, path + ".allowedLocationKinds", "mortal_wound_occurrence_invalid_safe_context"); continue; }
            values.Add(item.GetString()!);
        }
        return target is null || cause is null ? null : new WoundOpportunitySafeContext(target, cause, values);
    }

    private static WoundGuaranteedTriggerAuthority? ParseGuarantee(JsonElement? element, string path, List<ValidationIssue> issues)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null) return null;
        if (element.Value.ValueKind != JsonValueKind.Object) { Add(issues, path, "mortal_wound_occurrence_invalid_field"); return null; }
        var value = element.Value;
        ResourceMaterializationContract.FindDuplicateProperties(value, path, issues, "mortal_wound_occurrence_duplicate_property");
        Closed(value, path, GuaranteeFields, issues);
        if (!HasAny(value, GuaranteeFields)) Add(issues, path, "mortal_wound_occurrence_missing_field");
        var trigger = Text(value, "triggerId", path, issues); var sourceKind = Text(value, "sourceKind", path, issues);
        var sourceId = Text(value, "sourceId", path, issues); var sourceState = Text(value, "sourceState", path, issues);
        var realm = Text(value, "realm", path, issues); var domain = Text(value, "domain", path, issues);
        var owner = ParseOwner(Property(value, "owner", path, issues), path + ".owner", issues);
        var required = Int(value, "requiredSeverityRank", path, issues, true); var turn = Int(value, "materializedAtTurn", path, issues, true);
        var sourceFingerprint = Text(value, "sourceContractFingerprint", path, issues); var authority = Text(value, "authorityFingerprint", path, issues);
        return trigger is null || sourceKind is null || sourceId is null || sourceState is null || realm is null || domain is null || owner is null ||
               required is null || turn is null || sourceFingerprint is null || authority is null ? null :
            new WoundGuaranteedTriggerAuthority(trigger, sourceKind, sourceId, sourceState, realm, domain, owner, required.Value, turn.Value, sourceFingerprint, authority);
    }

    private static MortalWoundOccurrenceWorseningTarget? ParseTarget(JsonElement row, string path, List<ValidationIssue> issues)
    {
        if (!row.TryGetProperty("worseningTarget", out var element)) return null;
        if (element.ValueKind == JsonValueKind.Null) { Add(issues, path + ".worseningTarget", "mortal_wound_occurrence_create_target_mismatch"); return null; }
        if (element.ValueKind != JsonValueKind.Object) { Add(issues, path + ".worseningTarget", "mortal_wound_occurrence_invalid_worsening_target"); return null; }
        ResourceMaterializationContract.FindDuplicateProperties(element, path + ".worseningTarget", issues, "mortal_wound_occurrence_duplicate_property");
        Closed(element, path + ".worseningTarget", TargetFields, issues);
        var wound = Text(element, "woundId", path + ".worseningTarget", issues); var cause = Text(element, "causeKind", path + ".worseningTarget", issues);
        if (wound is null || cause is null)
        {
            Add(issues, path + ".worseningTarget", "mortal_wound_occurrence_invalid_worsening_target");
            return null;
        }
        return new MortalWoundOccurrenceWorseningTarget(wound, cause);
    }

    private static JsonElement? Property(JsonElement value, string name, string path, List<ValidationIssue> issues)
    {
        if (value.TryGetProperty(name, out var property)) return property;
        Add(issues, path + "." + name, "mortal_wound_occurrence_missing_field");
        return null;
    }

    private static string? Text(JsonElement value, string name, string path, List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property)) { Add(issues, path + "." + name, "mortal_wound_occurrence_missing_field"); return null; }
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(property.GetString())) { Add(issues, path + "." + name, "mortal_wound_occurrence_invalid_field"); return null; }
        return property.GetString();
    }

    private static string? ReadableText(JsonElement value, string name, string path, List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            Add(issues, path + "." + name, "mortal_wound_occurrence_missing_field");
            return null;
        }
        if (property.ValueKind != JsonValueKind.String)
        {
            Add(issues, path + "." + name, "mortal_wound_occurrence_invalid_field");
            return null;
        }
        return property.GetString()!;
    }

    private static int? Int(JsonElement value, string name, string path, List<ValidationIssue> issues, bool required)
    {
        if (!value.TryGetProperty(name, out var property)) { if (required) Add(issues, path + "." + name, "mortal_wound_occurrence_missing_field"); return null; }
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var result)) { Add(issues, path + "." + name, "mortal_wound_occurrence_invalid_field"); return null; }
        return result;
    }

    private static int? NullableInt(JsonElement value, string name, string path, List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property)) { Add(issues, path + "." + name, "mortal_wound_occurrence_missing_field"); return null; }
        if (property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var result)) { Add(issues, path + "." + name, "mortal_wound_occurrence_invalid_field"); return null; }
        return result;
    }

    private static void Closed(JsonElement value, string path, IReadOnlySet<string> fields, List<ValidationIssue> issues) =>
        ResourceMaterializationContract.ValidateClosedObject(value, path, fields, issues, "mortal_wound_occurrence_unknown_field");

    private static bool HasAny(JsonElement value, IReadOnlySet<string> fields) =>
        value.EnumerateObject().Any(property => fields.Contains(property.Name));

    private static void Add(List<ValidationIssue> issues, string path, string code) =>
        issues.Add(new ValidationIssue(path, IssueSeverity.Error, code, code));

    private static IReadOnlyList<ValidationIssue> FreezeIssues(IEnumerable<ValidationIssue> issues) =>
        Array.AsReadOnly(issues.ToArray());

    private static bool OwnerIsMortal(WoundOwnerCoordinate owner) => owner switch
    {
        { Realm: "mortal_world", OwnerKind: "player", OwnerId: "player_current",
            CarrierPath: WoundCarrierCatalog.PlayerPath } => true,
        { Realm: "mortal_world", OwnerKind: "npc",
            CarrierPath: WoundCarrierCatalog.NpcPath } => Exact(owner.OwnerId),
        { Realm: "mortal_world", OwnerKind: "combatant",
            CarrierPath: WoundCarrierCatalog.EnemiesPath or
                WoundCarrierCatalog.AlliesPath } => Exact(owner.OwnerId),
        { Realm: "mortal_world", OwnerKind: "combatant_member",
            CarrierPath: WoundCarrierCatalog.EnemiesPath or
                WoundCarrierCatalog.AlliesPath } => Exact(owner.OwnerId),
        _ => false
    };

    private static void ValidateSafe(WoundOpportunitySafeContext value, string path, List<ValidationIssue> issues)
    {
        if (!Readable(value.Target)) Add(issues, path + ".target", "mortal_wound_occurrence_invalid_readable_text");
        if (!Readable(value.Cause)) Add(issues, path + ".cause", "mortal_wound_occurrence_invalid_readable_text");
        var kinds = value.AllowedLocationKinds;
        if (kinds.Count is < 1 or > 5 || kinds.Distinct(StringComparer.Ordinal).Count() != kinds.Count ||
            kinds.Any(kind => kind is not ("anatomical" or "systemic" or "mental" or "spiritual_axis" or "other")))
            Add(issues, path + ".allowedLocationKinds", "mortal_wound_occurrence_invalid_safe_context");
    }

    private static bool Readable(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2000 && value == value.Trim() &&
        value.All(character => !char.IsControl(character));
    private static bool Exact(string? value) => !string.IsNullOrEmpty(value) && ResourceMaterializationContract.IsExactIdentifier(value);
    private static void ValidateExact(string? value, string path, List<ValidationIssue> issues)
    {
        if (!Exact(value)) Add(issues, path, "mortal_wound_occurrence_invalid_field");
    }

    private static void ValidateFingerprint(string? value, string path, List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(value))
            Add(issues, path, "mortal_wound_occurrence_invalid_field");
    }

    private static string ComputeAcceptedEventsFingerprint(IReadOnlyList<WoundAcceptedEventAuthority> events)
    {
        var fields = new List<string?> { "book_of_eternity.wound.accepted_event_set", "1", events.Count.ToString(CultureInfo.InvariantCulture) };
        for (var index = 0; index < events.Count; index++)
        {
            var value = events[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture)); fields.Add(value.EventRef); fields.Add(value.Kind);
            fields.Add(value.AuthorityId); fields.Add(value.SemanticFingerprint);
        }
        return Hash(fields);
    }

    private static string ComputeCandidateFingerprint(MortalWoundOccurrence value)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound.occurrence_candidate", "1", value.SourceSessionId, value.SourceRequestId,
            value.SourceSnapshotToken, value.SourceTurn.ToString(CultureInfo.InvariantCulture), value.ProducerOperationKey,
            value.ProducerCandidateOrdinal.ToString(CultureInfo.InvariantCulture), value.ProducerCandidateCount.ToString(CultureInfo.InvariantCulture),
            value.AdapterKind, value.AcceptedEventOrdinal.ToString(CultureInfo.InvariantCulture), value.AcceptedEventsFingerprint,
            value.Owner.Realm, value.Owner.OwnerKind, value.Owner.OwnerId, value.Owner.CarrierPath, value.Domain, value.ProfileKey,
            value.Source.Kind, value.Source.SourceId, value.Source.State, value.Outcome.Kind,
            value.Outcome.MaximumSeverityRank.ToString(CultureInfo.InvariantCulture), value.Outcome.ReadableCause,
            value.HardMaximumSeverityRank.ToString(CultureInfo.InvariantCulture), value.MinimumSeverityRank?.ToString(CultureInfo.InvariantCulture),
            value.GuaranteedTrigger is null ? null : ComputeGuaranteeFingerprint(value.GuaranteedTrigger),
            value.SafeContext.Target, value.SafeContext.Cause, value.SafeContext.AllowedLocationKinds.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < value.SafeContext.AllowedLocationKinds.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture)); fields.Add(value.SafeContext.AllowedLocationKinds[index]);
        }
        fields.Add(value.WorseningTarget is null ? "create" : "worsen");
        fields.Add(value.WorseningTarget?.WoundId); fields.Add(value.WorseningTarget?.CauseKind); fields.Add(value.SourceResultFingerprint);
        return Hash(fields);
    }

    private static string ComputeGuaranteeFingerprint(WoundGuaranteedTriggerAuthority value) => Hash(
        "book_of_eternity.wound.guaranteed_trigger_authority", "1", value.TriggerId, value.SourceKind, value.SourceId,
        value.SourceState, value.Realm, value.Domain, value.Owner.Realm, value.Owner.OwnerKind, value.Owner.OwnerId,
        value.Owner.CarrierPath, value.RequiredSeverityRank.ToString(CultureInfo.InvariantCulture),
        value.MaterializedAtTurn.ToString(CultureInfo.InvariantCulture), value.SourceContractFingerprint);

    private static WoundGuaranteedTriggerAuthority ToAuthority(WoundGuaranteedTriggerEvidence value) => new WoundGuaranteedTriggerAuthority(
        value.TriggerId, value.SourceKind, value.SourceId, value.SourceState, value.Realm, value.Domain, value.Owner,
        value.RequiredSeverityRank, value.MaterializedAtTurn, value.SourceContractFingerprint, string.Empty) with
    {
        AuthorityFingerprint = Hash("book_of_eternity.wound.guaranteed_trigger_authority", "1", value.TriggerId, value.SourceKind,
            value.SourceId, value.SourceState, value.Realm, value.Domain, value.Owner.Realm, value.Owner.OwnerKind, value.Owner.OwnerId,
            value.Owner.CarrierPath, value.RequiredSeverityRank.ToString(CultureInfo.InvariantCulture),
            value.MaterializedAtTurn.ToString(CultureInfo.InvariantCulture), value.SourceContractFingerprint)
    };

    private static string ComputeBatchFingerprint(IEnumerable<MortalWoundOccurrence> values)
    {
        var ordered = values.OrderBy(value => value.ProducerCandidateOrdinal).ToArray();
        var fields = new List<string?> { "book_of_eternity.mortal_wound.occurrence_candidate_batch", "1", ordered[0].ProducerOperationKey,
            ordered.Length.ToString(CultureInfo.InvariantCulture) };
        foreach (var value in ordered) { fields.Add(value.ProducerCandidateOrdinal.ToString(CultureInfo.InvariantCulture)); fields.Add(value.CandidateFingerprint); }
        return Hash(fields);
    }

    private static string Hash(params string?[] fields) => Hash((IEnumerable<string?>)fields);
    private static string Hash(IEnumerable<string?> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null) { builder.Append("-1:"); continue; }
            builder.Append(Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
        }
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void WriteOccurrence(Utf8JsonWriter writer, MortalWoundOccurrence value)
    {
        writer.WriteStartObject();
        writer.WriteString("occurrenceId", value.OccurrenceId); writer.WriteString("opportunityRef", value.OpportunityRef);
        writer.WriteString("sourceSessionId", value.SourceSessionId); writer.WriteString("sourceRequestId", value.SourceRequestId);
        writer.WriteString("sourceSnapshotToken", value.SourceSnapshotToken); writer.WriteNumber("sourceTurn", value.SourceTurn);
        writer.WriteString("producerOperationKey", value.ProducerOperationKey); writer.WriteNumber("producerCandidateOrdinal", value.ProducerCandidateOrdinal); writer.WriteNumber("producerCandidateCount", value.ProducerCandidateCount);
        writer.WriteString("adapterKind", value.AdapterKind); writer.WriteNumber("acceptedEventOrdinal", value.AcceptedEventOrdinal);
        writer.WritePropertyName("acceptedEvents"); writer.WriteStartArray();
        foreach (var item in value.AcceptedEvents) { writer.WriteStartObject(); writer.WriteString("eventRef", item.EventRef); writer.WriteString("kind", item.Kind); writer.WriteString("authorityId", item.AuthorityId); writer.WriteString("semanticFingerprint", item.SemanticFingerprint); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WriteString("acceptedEventsFingerprint", value.AcceptedEventsFingerprint);
        WriteOwner(writer, "owner", value.Owner); writer.WriteString("domain", value.Domain); writer.WriteString("profileKey", value.ProfileKey);
        writer.WritePropertyName("source"); writer.WriteStartObject(); writer.WriteString("kind", value.Source.Kind); writer.WriteString("sourceId", value.Source.SourceId); writer.WriteString("state", value.Source.State); writer.WriteEndObject();
        writer.WritePropertyName("outcome"); writer.WriteStartObject(); writer.WriteString("kind", value.Outcome.Kind); writer.WriteNumber("maximumSeverityRank", value.Outcome.MaximumSeverityRank); writer.WriteString("readableCause", value.Outcome.ReadableCause); writer.WriteEndObject();
        writer.WriteNumber("hardMaximumSeverityRank", value.HardMaximumSeverityRank);
        if (value.MinimumSeverityRank is null) writer.WriteNull("minimumSeverityRank"); else writer.WriteNumber("minimumSeverityRank", value.MinimumSeverityRank.Value);
        if (value.GuaranteedTrigger is null) writer.WriteNull("guaranteedTrigger"); else WriteGuarantee(writer, value.GuaranteedTrigger);
        writer.WritePropertyName("safeContext"); writer.WriteStartObject(); writer.WriteString("target", value.SafeContext.Target); writer.WriteString("cause", value.SafeContext.Cause);
        writer.WritePropertyName("allowedLocationKinds"); writer.WriteStartArray(); foreach (var kind in value.SafeContext.AllowedLocationKinds) writer.WriteStringValue(kind); writer.WriteEndArray(); writer.WriteEndObject();
        if (value.WorseningTarget is not null) { writer.WritePropertyName("worseningTarget"); writer.WriteStartObject(); writer.WriteString("woundId", value.WorseningTarget.WoundId); writer.WriteString("causeKind", value.WorseningTarget.CauseKind); writer.WriteEndObject(); }
        writer.WriteString("sourceResultFingerprint", value.SourceResultFingerprint); writer.WriteString("candidateFingerprint", value.CandidateFingerprint); writer.WriteString("occurrenceFingerprint", value.OccurrenceFingerprint);
        writer.WriteEndObject();
    }

    private static void WriteOwner(Utf8JsonWriter writer, string name, WoundOwnerCoordinate owner)
    {
        writer.WritePropertyName(name); writer.WriteStartObject(); writer.WriteString("realm", owner.Realm); writer.WriteString("ownerKind", owner.OwnerKind);
        writer.WriteString("ownerId", owner.OwnerId); writer.WriteString("carrierPath", owner.CarrierPath); writer.WriteEndObject();
    }

    private static void WriteGuarantee(Utf8JsonWriter writer, WoundGuaranteedTriggerAuthority value)
    {
        writer.WritePropertyName("guaranteedTrigger"); writer.WriteStartObject(); writer.WriteString("triggerId", value.TriggerId); writer.WriteString("sourceKind", value.SourceKind);
        writer.WriteString("sourceId", value.SourceId); writer.WriteString("sourceState", value.SourceState); writer.WriteString("realm", value.Realm); writer.WriteString("domain", value.Domain);
        WriteOwner(writer, "owner", value.Owner); writer.WriteNumber("requiredSeverityRank", value.RequiredSeverityRank); writer.WriteNumber("materializedAtTurn", value.MaterializedAtTurn);
        writer.WriteString("sourceContractFingerprint", value.SourceContractFingerprint); writer.WriteString("authorityFingerprint", value.AuthorityFingerprint); writer.WriteEndObject();
    }

    private static readonly IReadOnlySet<string> RootFields = Set("schemaVersion", "occurrences");
    private static readonly IReadOnlySet<string> RowFields = Set("occurrenceId", "opportunityRef", "sourceSessionId", "sourceRequestId", "sourceSnapshotToken", "sourceTurn", "producerOperationKey", "producerCandidateOrdinal", "producerCandidateCount", "adapterKind", "acceptedEventOrdinal", "acceptedEvents", "acceptedEventsFingerprint", "owner", "domain", "profileKey", "source", "outcome", "hardMaximumSeverityRank", "minimumSeverityRank", "guaranteedTrigger", "safeContext", "worseningTarget", "sourceResultFingerprint", "candidateFingerprint", "occurrenceFingerprint");
    private static readonly IReadOnlySet<string> EventFields = Set("eventRef", "kind", "authorityId", "semanticFingerprint");
    private static readonly IReadOnlySet<string> OwnerFields = Set("realm", "ownerKind", "ownerId", "carrierPath");
    private static readonly IReadOnlySet<string> SourceFields = Set("kind", "sourceId", "state");
    private static readonly IReadOnlySet<string> OutcomeFields = Set("kind", "maximumSeverityRank", "readableCause");
    private static readonly IReadOnlySet<string> SafeFields = Set("target", "cause", "allowedLocationKinds");
    private static readonly IReadOnlySet<string> TargetFields = Set("woundId", "causeKind");
    private static readonly IReadOnlySet<string> GuaranteeFields = Set("triggerId", "sourceKind", "sourceId", "sourceState", "realm", "domain", "owner", "requiredSeverityRank", "materializedAtTurn", "sourceContractFingerprint", "authorityFingerprint");
    private static readonly IReadOnlySet<string> Adapters = Set("formal", "qte", "combat", "trap", "check", "hazard", "narrative");
    private static IReadOnlySet<string> Set(params string[] values) => new HashSet<string>(values, StringComparer.Ordinal);
}

internal sealed record MortalWoundOccurrenceSource(string Kind, string SourceId, string State);
internal sealed record MortalWoundOccurrenceOutcome(string Kind, int MaximumSeverityRank, string ReadableCause);
internal sealed record MortalWoundOccurrenceWorseningTarget(string WoundId, string CauseKind);

internal sealed record MortalWoundOccurrenceCandidate
{
    private IReadOnlyList<WoundAcceptedEventAuthority> _acceptedEvents = Array.Empty<WoundAcceptedEventAuthority>();
    internal MortalWoundOccurrenceCandidate(
        string SourceSessionId, string SourceRequestId, string SourceSnapshotToken, int SourceTurn, string ProducerOperationKey,
        int ProducerCandidateOrdinal, int ProducerCandidateCount, string AdapterKind, int AcceptedEventOrdinal,
        IReadOnlyList<WoundAcceptedEventAuthority> AcceptedEvents, WoundOwnerCoordinate Owner, string Domain,
        string ProfileKey, MortalWoundOccurrenceSource Source, MortalWoundOccurrenceOutcome Outcome,
        int HardMaximumSeverityRank, int? MinimumSeverityRank, WoundGuaranteedTriggerEvidence? GuaranteedTrigger,
        WoundOpportunitySafeContext SafeContext, MortalWoundOccurrenceWorseningTarget? WorseningTarget,
        string SourceResultFingerprint)
    {
        this.SourceSessionId = SourceSessionId;
        this.SourceRequestId = SourceRequestId;
        this.SourceSnapshotToken = SourceSnapshotToken;
        this.SourceTurn = SourceTurn;
        this.ProducerOperationKey = ProducerOperationKey;
        this.ProducerCandidateOrdinal = ProducerCandidateOrdinal;
        this.ProducerCandidateCount = ProducerCandidateCount;
        this.AdapterKind = AdapterKind;
        this.AcceptedEventOrdinal = AcceptedEventOrdinal;
        this.AcceptedEvents = AcceptedEvents;
        this.Owner = Owner;
        this.Domain = Domain;
        this.ProfileKey = ProfileKey;
        this.Source = Source;
        this.Outcome = Outcome;
        this.HardMaximumSeverityRank = HardMaximumSeverityRank;
        this.MinimumSeverityRank = MinimumSeverityRank;
        this.GuaranteedTrigger = GuaranteedTrigger;
        this.SafeContext = SafeContext;
        this.WorseningTarget = WorseningTarget;
        this.SourceResultFingerprint = SourceResultFingerprint;
    }

    public string SourceSessionId { get; init; }
    public string SourceRequestId { get; init; }
    public string SourceSnapshotToken { get; init; }
    public int SourceTurn { get; init; }
    public string ProducerOperationKey { get; init; }
    public int ProducerCandidateOrdinal { get; init; }
    public int ProducerCandidateCount { get; init; }
    public string AdapterKind { get; init; }
    public int AcceptedEventOrdinal { get; init; }
    public IReadOnlyList<WoundAcceptedEventAuthority> AcceptedEvents
    {
        get => _acceptedEvents;
        init => _acceptedEvents = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value)))
            .Select(item => new WoundAcceptedEventAuthority(item.EventRef, item.Kind, item.AuthorityId, item.SemanticFingerprint))
            .ToArray());
    }
    public WoundOwnerCoordinate Owner { get; init; }
    public string Domain { get; init; }
    public string ProfileKey { get; init; }
    public MortalWoundOccurrenceSource Source { get; init; }
    public MortalWoundOccurrenceOutcome Outcome { get; init; }
    public int HardMaximumSeverityRank { get; init; }
    public int? MinimumSeverityRank { get; init; }
    public WoundGuaranteedTriggerEvidence? GuaranteedTrigger { get; init; }
    public WoundOpportunitySafeContext SafeContext { get; init; }
    public MortalWoundOccurrenceWorseningTarget? WorseningTarget { get; init; }
    public string SourceResultFingerprint { get; init; }
}

internal sealed record MortalWoundOccurrenceCandidateBatch
{
    private IReadOnlyList<MortalWoundOccurrenceCandidate> _candidates = Array.Empty<MortalWoundOccurrenceCandidate>();

    internal MortalWoundOccurrenceCandidateBatch(IReadOnlyList<MortalWoundOccurrenceCandidate> Candidates) =>
        this.Candidates = Candidates;

    public IReadOnlyList<MortalWoundOccurrenceCandidate> Candidates
    {
        get => _candidates;
        init => _candidates = Array.AsReadOnly((value ?? throw new ArgumentNullException(nameof(value))).ToArray());
    }
}

internal sealed record MortalWoundOccurrence(
    string OccurrenceId, string OpportunityRef, string SourceSessionId, string SourceRequestId, string SourceSnapshotToken, int SourceTurn,
    string ProducerOperationKey, int ProducerCandidateOrdinal, int ProducerCandidateCount, string AdapterKind, int AcceptedEventOrdinal,
    IReadOnlyList<WoundAcceptedEventAuthority> AcceptedEvents, string AcceptedEventsFingerprint, WoundOwnerCoordinate Owner,
    string Domain, string ProfileKey, MortalWoundOccurrenceSource Source, MortalWoundOccurrenceOutcome Outcome,
    int HardMaximumSeverityRank, int? MinimumSeverityRank, WoundGuaranteedTriggerAuthority? GuaranteedTrigger,
    WoundOpportunitySafeContext SafeContext, MortalWoundOccurrenceWorseningTarget? WorseningTarget,
    string SourceResultFingerprint, string CandidateFingerprint, string OccurrenceFingerprint);

internal sealed record MortalWoundOccurrenceParseResult(bool IsValid, MortalWoundOccurrenceState? State, IReadOnlyList<ValidationIssue> Issues);
internal sealed record MortalWoundOccurrenceAppendPlanResult(string Disposition, IReadOnlyList<ValidationIssue> Issues,
    MortalWoundOccurrenceState? State, string? BatchFingerprint);
