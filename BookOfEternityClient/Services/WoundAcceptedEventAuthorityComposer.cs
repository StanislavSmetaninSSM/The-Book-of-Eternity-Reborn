using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Seals one complete typed accepted-response event projection. Selected wound semantics
/// are derived from typed evidence at an exact ordinal; callers never supply a semantic
/// fingerprint for the composer to trust.
/// </summary>
internal static class WoundAcceptedEventAuthorityComposer
{
    private const int MaximumEvents = 160;
    private const int MaximumSelections = 32;
    private const int MaximumReadableLength = 2_000;
    private static readonly IReadOnlySet<string> AdapterKinds = new HashSet<string>(
        new[] { "formal", "qte", "combat", "trap", "check", "hazard", "narrative" },
        StringComparer.Ordinal);

    internal static WoundAcceptedEventAuthorityCompositionResult Compose(
        WoundAcceptedResponseEventProjection projection,
        IReadOnlyList<WoundSelectedEventEvidence> selectedEvidence)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(selectedEvidence);
        var issues = new List<ValidationIssue>();

        if (!ExactCoordinate(projection.SessionId) ||
            !ExactCoordinate(projection.RequestId) ||
            !ExactCoordinate(projection.SnapshotToken) ||
            projection.Turn <= 0)
        {
            Add(
                issues,
                "acceptedWoundEvents.binding",
                "wound_accepted_event_binding_invalid",
                "session/request/snapshot/turn coordinates are malformed");
        }

        var rows = projection.Events;
        if (rows.Count is < 1 or > MaximumEvents)
        {
            Add(
                issues,
                "acceptedWoundEvents.events",
                rows.Count > MaximumEvents
                    ? "wound_accepted_event_limit_exceeded"
                    : "wound_accepted_event_set_invalid",
                $"count={rows.Count}");
            return Failure(issues);
        }

        var selections = ValidateSelections(selectedEvidence, rows.Count, issues);
        var result = new List<WoundAcceptedEventAuthority>(rows.Count);
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        var eventAliases = new HashSet<string>(StringComparer.Ordinal);
        var authorityKeys = new HashSet<string>(StringComparer.Ordinal);
        var authorityAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < rows.Count; ordinal++)
        {
            var row = rows[ordinal];
            var path = $"acceptedWoundEvents.events[{ordinal}]";
            if (row is null ||
                !ExactCoordinate(row.EventRef) ||
                !ExactCoordinate(row.Kind) ||
                !ExactCoordinate(row.AuthorityId))
            {
                Add(
                    issues,
                    path,
                    "wound_accepted_event_row_invalid",
                    "malformed typed event coordinate");
                continue;
            }

            var eventAlias = MortalLocationIdentityState.BuildConfusableKey(row.EventRef);
            if (!eventRefs.Add(row.EventRef) || !eventAliases.Add(eventAlias))
            {
                Add(
                    issues,
                    path + ".eventRef",
                    "wound_accepted_event_duplicate_ref",
                    row.EventRef);
                continue;
            }

            var authorityKey = row.Kind + "\u001f" + row.AuthorityId;
            var authorityAlias =
                MortalLocationIdentityState.BuildConfusableKey(row.Kind) + "\u001f" +
                MortalLocationIdentityState.BuildConfusableKey(row.AuthorityId);
            if (!authorityKeys.Add(authorityKey) ||
                !authorityAliases.Add(authorityAlias))
            {
                Add(
                    issues,
                    path,
                    "wound_accepted_event_duplicate_authority",
                    row.Kind + ":" + row.AuthorityId);
                continue;
            }

            string semanticFingerprint;
            if (!selections.TryGetValue(ordinal, out var evidence))
            {
                semanticFingerprint = ComputeGenericSemanticFingerprint(
                    projection,
                    row);
            }
            else if (!string.Equals(
                         evidence.AuthorityKind,
                         row.Kind,
                         StringComparison.Ordinal) ||
                     !string.Equals(
                         evidence.AuthorityId,
                         row.AuthorityId,
                         StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path,
                    "wound_accepted_event_selected_evidence_mismatch",
                    $"ordinal={ordinal}");
                continue;
            }
            else
            {
                semanticFingerprint =
                    WoundOpportunityEventEvidenceFingerprint.Compute(evidence);
            }

            result.Add(new WoundAcceptedEventAuthority(
                row.EventRef,
                row.Kind,
                row.AuthorityId,
                semanticFingerprint));
        }

        if (issues.Count != 0 || result.Count != rows.Count)
            return Failure(issues);

        return new WoundAcceptedEventAuthorityCompositionResult(
            result,
            WoundAcceptedEventSetFingerprint.Compute(result),
            Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Verifies the complete historical event vector sealed by a Mortal occurrence,
    /// then derives the equivalent vector for the active decision snapshot. Generic
    /// sibling seals are deliberately rebound; selected wound semantics remain derived
    /// from the signed occurrence rather than copied from persisted fingerprints.
    /// </summary>
    internal static WoundAcceptedEventAuthorityCompositionResult
        RebindMortalOccurrence(
            MortalWoundOccurrence occurrence,
            IReadOnlyList<MortalWoundOccurrence> producerBatch,
            string sessionId,
            string requestId,
            string snapshotToken,
            int turn)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(producerBatch);
        var storedEvents = occurrence.AcceptedEvents ??
            Array.Empty<WoundAcceptedEventAuthority>();
        if (occurrence.AcceptedEventOrdinal < 0 ||
            occurrence.AcceptedEventOrdinal >= storedEvents.Count)
        {
            return MortalRebindFailure(
                "mortal_wound_occurrence_event_authority_mismatch",
                "selected occurrence event ordinal is outside the stored vector");
        }

        var coordinates = storedEvents
            .Select(value => value is null
                ? null!
                : new WoundAcceptedResponseEventCoordinate(
                    value.EventRef,
                    value.Kind,
                    value.AuthorityId))
            .ToArray();
        var orderedBatch = producerBatch
            .OrderBy(value => value?.ProducerCandidateOrdinal ?? int.MaxValue)
            .ToArray();
        if (orderedBatch.Length != occurrence.ProducerCandidateCount ||
            orderedBatch.Length == 0 ||
            !orderedBatch.Select(value => value?.ProducerCandidateOrdinal ?? -1)
                .SequenceEqual(Enumerable.Range(0, orderedBatch.Length)) ||
            orderedBatch.Count(value => value is not null && string.Equals(
                value.OccurrenceId,
                occurrence.OccurrenceId,
                StringComparison.Ordinal)) != 1 ||
            orderedBatch.Any(value => value is null ||
                !string.Equals(
                    value.ProducerOperationKey,
                    occurrence.ProducerOperationKey,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.SourceSessionId,
                    occurrence.SourceSessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.SourceRequestId,
                    occurrence.SourceRequestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.SourceSnapshotToken,
                    occurrence.SourceSnapshotToken,
                    StringComparison.Ordinal) ||
                value.SourceTurn != occurrence.SourceTurn ||
                value.ProducerCandidateCount != occurrence.ProducerCandidateCount ||
                !string.Equals(
                    value.AdapterKind,
                    occurrence.AdapterKind,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.SourceResultFingerprint,
                    occurrence.SourceResultFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    value.AcceptedEventsFingerprint,
                    occurrence.AcceptedEventsFingerprint,
                    StringComparison.Ordinal) ||
                !value.AcceptedEvents.SequenceEqual(storedEvents)))
        {
            return MortalRebindFailure(
                "mortal_wound_occurrence_event_authority_mismatch",
                "producer batch is incomplete or disagrees on source authority");
        }

        var selectedEvidence = new Dictionary<int, WoundOpportunityEventEvidence>();
        foreach (var row in orderedBatch)
        {
            if (row.AcceptedEventOrdinal < 0 ||
                row.AcceptedEventOrdinal >= storedEvents.Count ||
                row.Outcome is null ||
                storedEvents[row.AcceptedEventOrdinal] is not { } selected)
            {
                return MortalRebindFailure(
                    "mortal_wound_occurrence_event_authority_mismatch",
                    "producer batch selects an invalid event or outcome");
            }
            var evidence = new WoundOpportunityEventEvidence(
                row.AdapterKind,
                selected.Kind,
                selected.AuthorityId,
                row.Outcome.Kind,
                row.Outcome.MaximumSeverityRank,
                row.Outcome.ReadableCause);
            if (selectedEvidence.TryGetValue(row.AcceptedEventOrdinal, out var prior) &&
                prior != evidence)
            {
                return MortalRebindFailure(
                    "mortal_wound_occurrence_event_authority_mismatch",
                    "shared selected event has contradictory wound semantics");
            }
            selectedEvidence[row.AcceptedEventOrdinal] = evidence;
        }
        var selections = selectedEvidence
            .OrderBy(pair => pair.Key)
            .Select(pair => new WoundSelectedEventEvidence(pair.Key, pair.Value))
            .ToArray();
        var historical = Compose(
            new WoundAcceptedResponseEventProjection(
                occurrence.SourceSessionId,
                occurrence.SourceRequestId,
                occurrence.SourceSnapshotToken,
                occurrence.SourceTurn,
                coordinates),
            selections);
        if (!historical.Success)
            return historical;
        if (!historical.Events.SequenceEqual(storedEvents) ||
            !string.Equals(
                historical.EventsFingerprint,
                occurrence.AcceptedEventsFingerprint,
                StringComparison.Ordinal))
        {
            return MortalRebindFailure(
                "mortal_wound_occurrence_event_authority_mismatch",
                "stored event vector does not match its source snapshot authority");
        }

        return Compose(
            new WoundAcceptedResponseEventProjection(
                sessionId,
                requestId,
                snapshotToken,
                turn,
                coordinates),
            selections);
    }

    private static IReadOnlyDictionary<int, WoundOpportunityEventEvidence>
        ValidateSelections(
            IReadOnlyList<WoundSelectedEventEvidence> selections,
            int eventCount,
            ICollection<ValidationIssue> issues)
    {
        if (selections.Count > MaximumSelections)
        {
            Add(
                issues,
                "acceptedWoundEvents.selectedEvidence",
                "wound_accepted_event_limit_exceeded",
                $"count={selections.Count}");
        }

        var result = new Dictionary<int, WoundOpportunityEventEvidence>();
        for (var index = 0; index < selections.Count; index++)
        {
            var selection = selections[index];
            var path = $"acceptedWoundEvents.selectedEvidence[{index}]";
            if (selection is null ||
                selection.Ordinal < 0 ||
                selection.Ordinal >= eventCount ||
                !EvidenceIsValid(selection.Evidence))
            {
                Add(
                    issues,
                    path,
                    "wound_accepted_event_selected_evidence_invalid",
                    selection is null ? "null" : $"ordinal={selection.Ordinal}");
                continue;
            }

            if (!result.TryAdd(selection.Ordinal, selection.Evidence with { }))
            {
                Add(
                    issues,
                    path + ".ordinal",
                    "wound_accepted_event_selected_ordinal_duplicate",
                    selection.Ordinal.ToString());
            }
        }
        return new ReadOnlyDictionary<int, WoundOpportunityEventEvidence>(result);
    }

    private static bool EvidenceIsValid(WoundOpportunityEventEvidence? evidence) =>
        evidence is not null &&
        AdapterKinds.Contains(evidence.AdapterKind) &&
        ExactCoordinate(evidence.AuthorityKind) &&
        ExactCoordinate(evidence.AuthorityId) &&
        string.Equals(evidence.OutcomeKind, "harmful", StringComparison.Ordinal) &&
        evidence.MaximumSeverityRank is >= 1 and <= 4 &&
        Readable(evidence.ReadableCause);

    private static string ComputeGenericSemanticFingerprint(
        WoundAcceptedResponseEventProjection projection,
        WoundAcceptedResponseEventCoordinate row)
    {
        // Preserve the already-published production row order and hash formula. This is
        // deliberate compatibility, not reliance on caller-provided JSON ordering.
        var productionRow = new JsonObject
        {
            ["kind"] = row.Kind,
            ["authorityId"] = row.AuthorityId,
            ["eventRef"] = row.EventRef
        };
        var payload = projection.SessionId + "\n" +
            projection.RequestId + "\n" +
            projection.SnapshotToken + "\n" +
            productionRow.ToJsonString();
        return "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "accepted-wound-event-authority-v1\0" + payload)))
            .ToLowerInvariant();
    }

    private static bool ExactCoordinate(string? value) =>
        value is { Length: > 0 and <= MaximumReadableLength } &&
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool Readable(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= MaximumReadableLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    private static WoundAcceptedEventAuthorityCompositionResult Failure(
        IEnumerable<ValidationIssue> issues) => new(
        Array.Empty<WoundAcceptedEventAuthority>(),
        string.Empty,
        Array.AsReadOnly(issues.ToArray()));

    private static WoundAcceptedEventAuthorityCompositionResult MortalRebindFailure(
        string code,
        string actual) => Failure(new[]
        {
            new ValidationIssue(
                MortalWoundOccurrenceState.StatePath,
                IssueSeverity.Error,
                "The signed Mortal wound occurrence event authority is invalid.",
                code: code,
                actor: "Client",
                section: "wound_accepted_event_authority",
                expected:
                "the exact historical event vector and one complete active-snapshot rebind",
                actual: actual,
                repairHint:
                "Restore the signed occurrence snapshot; never copy, replace, remove, add, or reorder accepted event authority.")
        });

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The complete accepted wound event authority cannot be reconstructed.",
        code: code,
        actor: "Client",
        section: "wound_accepted_event_authority",
        expected:
        "one bounded ordered typed response event set with ordinal-derived wound semantics",
        actual: actual,
        repairHint:
        "Rebuild the typed event projection from the accepted mechanics result and derive selected evidence from sealed wound authority."));
}

internal sealed record WoundAcceptedResponseEventCoordinate(
    string EventRef,
    string Kind,
    string AuthorityId);

internal sealed class WoundAcceptedResponseEventProjection
{
    private readonly WoundAcceptedResponseEventCoordinate[] _events;

    internal WoundAcceptedResponseEventProjection(
        string sessionId,
        string requestId,
        string snapshotToken,
        int turn,
        IReadOnlyList<WoundAcceptedResponseEventCoordinate> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        Turn = turn;
        _events = events
            .Select(value => value is null ? null! : value with { })
            .ToArray();
    }

    public string SessionId { get; }
    public string RequestId { get; }
    public string SnapshotToken { get; }
    public int Turn { get; }
    public IReadOnlyList<WoundAcceptedResponseEventCoordinate> Events =>
        Array.AsReadOnly(_events
            .Select(value => value is null ? null! : value with { })
            .ToArray());
}

internal sealed record WoundSelectedEventEvidence(
    int Ordinal,
    WoundOpportunityEventEvidence Evidence);

internal sealed class WoundAcceptedEventAuthorityCompositionResult
{
    private readonly ReadOnlyCollection<WoundAcceptedEventAuthority> _events;
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal WoundAcceptedEventAuthorityCompositionResult(
        IEnumerable<WoundAcceptedEventAuthority> events,
        string eventsFingerprint,
        IEnumerable<ValidationIssue> issues)
    {
        _events = Array.AsReadOnly(events.Select(value => value with { }).ToArray());
        EventsFingerprint = eventsFingerprint;
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    public bool Success =>
        _issues.Count == 0 &&
        _events.Count != 0 &&
        ResourceMaterializationContract.IsAuthorityFingerprint(EventsFingerprint);
    public IReadOnlyList<WoundAcceptedEventAuthority> Events => _events;
    public string EventsFingerprint { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
}
