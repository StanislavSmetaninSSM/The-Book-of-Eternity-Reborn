using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

/// <summary>
/// A source subsystem may project wound semantics only after the common resource and
/// effect boundary has finalized. Implementations are explicitly registered by the
/// accepted-mechanics planner; an unregistered source kind cannot enter the reducer.
/// </summary>
internal interface IMortalWoundOccurrenceProducerDraft
{
    string AdapterKind { get; }

    MortalWoundOccurrenceProducerProjectionResult Project(
        AcceptedMechanicsResourcePlanningResult resourceResult,
        AcceptedEffectBoundaryTranscript effectBoundaryTranscript);
}

internal sealed class MortalWoundOccurrenceProducerProjectionResult
{
    private readonly ReadOnlyCollection<MortalWoundAcceptedHarmResult> _harms;
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal MortalWoundOccurrenceProducerProjectionResult(
        string producerOperationKey,
        WoundAcceptedResponseEventProjection? acceptedResponse,
        IReadOnlyList<MortalWoundAcceptedHarmResult> harms,
        IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(harms);
        ArgumentNullException.ThrowIfNull(issues);
        ProducerOperationKey = producerOperationKey;
        AcceptedResponse = acceptedResponse is null
            ? null
            : new WoundAcceptedResponseEventProjection(
                acceptedResponse.SessionId,
                acceptedResponse.RequestId,
                acceptedResponse.SnapshotToken,
                acceptedResponse.Turn,
                acceptedResponse.Events);
        _harms = Array.AsReadOnly(harms
            .Select(MortalWoundAcceptedHarmResult.DetachedCopy)
            .ToArray());
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    internal string ProducerOperationKey { get; }
    internal WoundAcceptedResponseEventProjection? AcceptedResponse { get; }
    internal IReadOnlyList<MortalWoundAcceptedHarmResult> Harms => _harms;
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal bool Success => AcceptedResponse != null && _issues.Count == 0;
}

/// <summary>
/// Reduces one finalized client-owned producer result to the common pending-occurrence
/// candidate language. It is pure: publication belongs to the accepted common plan.
/// </summary>
internal static class MortalWoundOccurrenceCandidateReducer
{
    private const int MaximumCandidates = 32;
    private static readonly IReadOnlySet<string> AdapterKinds = new HashSet<string>(
        new[] { "formal", "qte", "combat", "trap", "check", "hazard", "narrative" },
        StringComparer.Ordinal);

    internal static MortalWoundOccurrenceCandidateReductionResult ReduceRegistered(
        IMortalWoundOccurrenceProducerDraft draft,
        AcceptedMechanicsResourcePlanningResult resourceResult)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(resourceResult);
        if (!resourceResult.IsValid || resourceResult.EffectBoundaryTranscript is null)
        {
            return Invalid(new[]
            {
                ProducerIssue(
                    "mortalWoundProducer.finalizedBoundary",
                    "mortal_wound_producer_boundary_invalid",
                    "resource/effect result is not finalized")
            });
        }

        var projection = draft.Project(
            resourceResult,
            resourceResult.EffectBoundaryTranscript);
        if (projection is null)
        {
            return Invalid(new[]
            {
                ProducerIssue(
                    "mortalWoundProducer.projection",
                    "mortal_wound_producer_projection_invalid",
                    "null")
            });
        }
        if (!projection.Success)
        {
            var projectionIssues = projection.Issues.ToList();
            if (projectionIssues.Count == 0)
            {
                projectionIssues.Add(ProducerIssue(
                    "mortalWoundProducer.projection",
                    "mortal_wound_producer_projection_invalid",
                    "missing accepted response"));
            }
            return Invalid(projectionIssues);
        }

        return Reduce(MortalWoundAcceptedProducerResult.CreateRegistered(
            draft.AdapterKind,
            projection.ProducerOperationKey,
            projection.AcceptedResponse!,
            projection.Harms));
    }

    private static MortalWoundOccurrenceCandidateReductionResult Reduce(
        MortalWoundAcceptedProducerResult producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        var issues = new List<ValidationIssue>();
        if (!AdapterKinds.Contains(producer.AdapterKind) ||
            !Exact(producer.ProducerOperationKey) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                producer.SourceResultFingerprint))
        {
            Add(
                issues,
                "mortalWoundProducer",
                "mortal_wound_producer_coordinate_invalid",
                "malformed adapter, operation key, or derived source-result seal");
        }

        var harms = producer.Harms
            .OrderBy(value => value?.ProducerCandidateOrdinal ?? int.MaxValue)
            .ToArray();
        if (harms.Length > MaximumCandidates)
        {
            Add(
                issues,
                "mortalWoundProducer.harms",
                "mortal_wound_producer_limit_exceeded",
                $"count={harms.Length}");
        }

        if (harms.Length == 0)
        {
            var harmlessEventResult = WoundAcceptedEventAuthorityComposer.Compose(
                producer.AcceptedResponse,
                Array.Empty<WoundSelectedEventEvidence>());
            issues.AddRange(harmlessEventResult.Issues);
            return issues.Count == 0
                ? new MortalWoundOccurrenceCandidateReductionResult(
                    "harmless",
                    null,
                    Array.Empty<ValidationIssue>(),
                    producer.SourceResultFingerprint)
                : Invalid(issues, producer.SourceResultFingerprint);
        }

        for (var index = 0; index < harms.Length; index++)
        {
            var harm = harms[index];
            if (harm is null || harm.ProducerCandidateOrdinal != index)
            {
                Add(
                    issues,
                    $"mortalWoundProducer.harms[{index}]",
                    "mortal_wound_producer_candidate_ordinal_invalid",
                    harm is null ? "null" : $"ordinal={harm.ProducerCandidateOrdinal}");
            }
        }

        var responseEvents = producer.AcceptedResponse.Events;
        var selectedByOrdinal = new Dictionary<int, WoundOpportunityEventEvidence>();
        foreach (var harm in harms)
        {
            if (harm is null)
                continue;
            if (harm.AcceptedEventOrdinal < 0 ||
                harm.AcceptedEventOrdinal >= responseEvents.Count ||
                responseEvents[harm.AcceptedEventOrdinal] is null)
            {
                Add(
                    issues,
                    $"mortalWoundProducer.harms[{harm.ProducerCandidateOrdinal}].acceptedEventOrdinal",
                    "mortal_wound_producer_event_ordinal_invalid",
                    harm.AcceptedEventOrdinal.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            var coordinates = responseEvents[harm.AcceptedEventOrdinal];
            var evidence = new WoundOpportunityEventEvidence(
                producer.AdapterKind,
                coordinates.Kind,
                coordinates.AuthorityId,
                harm.Outcome?.Kind ?? string.Empty,
                harm.Outcome?.MaximumSeverityRank ?? 0,
                harm.Outcome?.ReadableCause ?? string.Empty);
            if (selectedByOrdinal.TryGetValue(harm.AcceptedEventOrdinal, out var existing) &&
                existing != evidence)
            {
                Add(
                    issues,
                    $"mortalWoundProducer.harms[{harm.ProducerCandidateOrdinal}].acceptedEventOrdinal",
                    "mortal_wound_producer_shared_event_evidence_conflict",
                    harm.AcceptedEventOrdinal.ToString(CultureInfo.InvariantCulture));
                continue;
            }
            selectedByOrdinal[harm.AcceptedEventOrdinal] = evidence;
        }

        var eventResult = WoundAcceptedEventAuthorityComposer.Compose(
            producer.AcceptedResponse,
            selectedByOrdinal
                .OrderBy(pair => pair.Key)
                .Select(pair => new WoundSelectedEventEvidence(pair.Key, pair.Value))
                .ToArray());
        issues.AddRange(eventResult.Issues);
        if (issues.Count != 0 || !eventResult.Success)
            return Invalid(issues, producer.SourceResultFingerprint);

        var candidates = harms.Select(harm => new MortalWoundOccurrenceCandidate(
            producer.AcceptedResponse.SessionId,
            producer.AcceptedResponse.RequestId,
            producer.AcceptedResponse.SnapshotToken,
            producer.AcceptedResponse.Turn,
            producer.ProducerOperationKey,
            harm.ProducerCandidateOrdinal,
            harms.Length,
            producer.AdapterKind,
            harm.AcceptedEventOrdinal,
            eventResult.Events,
            harm.Owner,
            harm.Domain,
            harm.ProfileKey,
            harm.Source,
            harm.Outcome,
            harm.HardMaximumSeverityRank,
            harm.MinimumSeverityRank,
            harm.GuaranteedTrigger,
            harm.SafeContext,
            harm.WorseningTarget,
            producer.SourceResultFingerprint)).ToArray();
        var batch = new MortalWoundOccurrenceCandidateBatch(candidates);

        var emptyPending = MortalWoundOccurrenceState.Parse(
            "{\"schemaVersion\":1,\"occurrences\":[]}",
            MortalWoundOccurrenceState.StatePath);
        var emptyReceipts = MortalWoundOpportunityReceiptState.Parse(
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}",
            MortalWoundOpportunityReceiptState.StatePath);
        if (!emptyPending.IsValid || emptyPending.State is null ||
            !emptyReceipts.IsValid || emptyReceipts.State is null)
        {
            throw new InvalidOperationException(
                "The built-in empty Mortal wound occurrence states are invalid.");
        }

        var validation = MortalWoundOccurrenceState.PlanAppend(
            emptyPending.State,
            batch,
            emptyReceipts.State);
        if (!string.Equals(validation.Disposition, "appended", StringComparison.Ordinal) ||
            validation.Issues.Count != 0)
        {
            issues.AddRange(validation.Issues);
            if (issues.Count == 0)
            {
                Add(
                    issues,
                    "mortalWoundProducer.harms",
                    "mortal_wound_producer_candidate_invalid",
                    validation.Disposition);
            }
            return Invalid(issues, producer.SourceResultFingerprint);
        }

        return new MortalWoundOccurrenceCandidateReductionResult(
            "harmful",
            batch,
            Array.Empty<ValidationIssue>(),
            producer.SourceResultFingerprint);
    }

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static MortalWoundOccurrenceCandidateReductionResult Invalid(
        IEnumerable<ValidationIssue> issues,
        string sourceResultFingerprint = "") => new(
        "invalid",
        null,
        Array.AsReadOnly(issues.ToArray()),
        sourceResultFingerprint);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The finalized Mortal wound producer result cannot be reduced.",
        code: code,
        actor: "Client",
        section: "mortal_wound_occurrence_producer",
        expected:
        "one closed harmless result or one complete contiguous typed harmful candidate batch",
        actual: actual,
        repairHint:
        "Repair the registered typed producer; never infer missing injury semantics from response prose or source JSON."));

    private static ValidationIssue ProducerIssue(
        string path,
        string code,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The finalized Mortal wound producer result cannot be reduced.",
        code: code,
        actor: "Client",
        section: "mortal_wound_occurrence_producer",
        expected:
        "one registered typed producer projected from the finalized common resource/effect boundary",
        actual: actual,
        repairHint:
        "Repair or register the typed producer; correlation input cannot substitute for a finalized source result.");
}

internal sealed class MortalWoundAcceptedProducerResult
{
    private readonly ReadOnlyCollection<MortalWoundAcceptedHarmResult> _harms;

    private MortalWoundAcceptedProducerResult(
        string adapterKind,
        string producerOperationKey,
        WoundAcceptedResponseEventProjection acceptedResponse,
        IReadOnlyList<MortalWoundAcceptedHarmResult> harms)
    {
        ArgumentNullException.ThrowIfNull(acceptedResponse);
        ArgumentNullException.ThrowIfNull(harms);
        AdapterKind = adapterKind;
        ProducerOperationKey = producerOperationKey;
        AcceptedResponse = new WoundAcceptedResponseEventProjection(
            acceptedResponse.SessionId,
            acceptedResponse.RequestId,
            acceptedResponse.SnapshotToken,
            acceptedResponse.Turn,
            acceptedResponse.Events);
        _harms = Array.AsReadOnly(harms
            .Select(CloneHarm)
            .ToArray());
        SourceResultFingerprint = ComputeSourceResultFingerprint();
    }

    internal static MortalWoundAcceptedProducerResult CreateRegistered(
        string adapterKind,
        string producerOperationKey,
        WoundAcceptedResponseEventProjection acceptedResponse,
        IReadOnlyList<MortalWoundAcceptedHarmResult> harms) => new(
        adapterKind,
        producerOperationKey,
        acceptedResponse,
        harms);

    public string AdapterKind { get; }
    public string ProducerOperationKey { get; }
    public WoundAcceptedResponseEventProjection AcceptedResponse { get; }
    public IReadOnlyList<MortalWoundAcceptedHarmResult> Harms => _harms;
    public string SourceResultFingerprint { get; }

    private string ComputeSourceResultFingerprint()
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound.accepted_producer_result",
            "1",
            AdapterKind,
            ProducerOperationKey,
            AcceptedResponse.SessionId,
            AcceptedResponse.RequestId,
            AcceptedResponse.SnapshotToken,
            AcceptedResponse.Turn.ToString(CultureInfo.InvariantCulture),
            AcceptedResponse.Events.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var ordinal = 0; ordinal < AcceptedResponse.Events.Count; ordinal++)
        {
            var value = AcceptedResponse.Events[ordinal];
            fields.Add(ordinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(value?.EventRef);
            fields.Add(value?.Kind);
            fields.Add(value?.AuthorityId);
        }

        var orderedHarms = _harms
            .OrderBy(value => value?.ProducerCandidateOrdinal ?? int.MaxValue)
            .ToArray();
        fields.Add(orderedHarms.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var value in orderedHarms)
            AppendHarm(fields, value);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendHarm(
        ICollection<string?> fields,
        MortalWoundAcceptedHarmResult? value)
    {
        if (value is null)
        {
            fields.Add(null);
            return;
        }

        fields.Add(value.ProducerCandidateOrdinal.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.AcceptedEventOrdinal.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.Owner?.Realm);
        fields.Add(value.Owner?.OwnerKind);
        fields.Add(value.Owner?.OwnerId);
        fields.Add(value.Owner?.CarrierPath);
        fields.Add(value.Domain);
        fields.Add(value.ProfileKey);
        fields.Add(value.Source?.Kind);
        fields.Add(value.Source?.SourceId);
        fields.Add(value.Source?.State);
        fields.Add(value.Outcome?.Kind);
        fields.Add(value.Outcome?.MaximumSeverityRank.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.Outcome?.ReadableCause);
        fields.Add(value.HardMaximumSeverityRank.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.MinimumSeverityRank?.ToString(CultureInfo.InvariantCulture));
        AppendGuarantee(fields, value.GuaranteedTrigger);
        fields.Add(value.SafeContext?.Target);
        fields.Add(value.SafeContext?.Cause);
        var locations = value.SafeContext?.AllowedLocationKinds ?? Array.Empty<string>();
        fields.Add(locations.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < locations.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(locations[index]);
        }
        fields.Add(value.WorseningTarget is null ? "create" : "worsen");
        fields.Add(value.WorseningTarget?.WoundId);
        fields.Add(value.WorseningTarget?.CauseKind);
    }

    private static void AppendGuarantee(
        ICollection<string?> fields,
        WoundGuaranteedTriggerEvidence? value)
    {
        fields.Add(value?.TriggerId);
        fields.Add(value?.SourceKind);
        fields.Add(value?.SourceId);
        fields.Add(value?.SourceState);
        fields.Add(value?.Realm);
        fields.Add(value?.Domain);
        fields.Add(value?.Owner?.Realm);
        fields.Add(value?.Owner?.OwnerKind);
        fields.Add(value?.Owner?.OwnerId);
        fields.Add(value?.Owner?.CarrierPath);
        fields.Add(value?.RequiredSeverityRank.ToString(CultureInfo.InvariantCulture));
        fields.Add(value?.MaterializedAtTurn.ToString(CultureInfo.InvariantCulture));
        fields.Add(value?.SourceContractFingerprint);
    }

    private static MortalWoundAcceptedHarmResult CloneHarm(
        MortalWoundAcceptedHarmResult value) =>
        MortalWoundAcceptedHarmResult.DetachedCopy(value);
}

internal sealed record MortalWoundAcceptedHarmResult(
    int ProducerCandidateOrdinal,
    int AcceptedEventOrdinal,
    WoundOwnerCoordinate Owner,
    string Domain,
    string ProfileKey,
    MortalWoundOccurrenceSource Source,
    MortalWoundOccurrenceOutcome Outcome,
    int HardMaximumSeverityRank,
    int? MinimumSeverityRank,
    WoundGuaranteedTriggerEvidence? GuaranteedTrigger,
    WoundOpportunitySafeContext SafeContext,
    MortalWoundOccurrenceWorseningTarget? WorseningTarget)
{
    internal static MortalWoundAcceptedHarmResult DetachedCopy(
        MortalWoundAcceptedHarmResult value)
    {
        if (value is null)
            return null!;
        return value with
        {
            Owner = value.Owner is null ? null! : value.Owner with { },
            Source = value.Source is null ? null! : value.Source with { },
            Outcome = value.Outcome is null ? null! : value.Outcome with { },
            GuaranteedTrigger = value.GuaranteedTrigger is null
                ? null
                : value.GuaranteedTrigger with
                {
                    Owner = value.GuaranteedTrigger.Owner is null
                        ? null!
                        : value.GuaranteedTrigger.Owner with { }
                },
            SafeContext = value.SafeContext is null
                ? null!
                : new WoundOpportunitySafeContext(
                    value.SafeContext.Target,
                    value.SafeContext.Cause,
                    value.SafeContext.AllowedLocationKinds),
            WorseningTarget = value.WorseningTarget is null
                ? null
                : value.WorseningTarget with { }
        };
    }
}

internal sealed record MortalWoundOccurrenceCandidateReductionResult(
    string Disposition,
    MortalWoundOccurrenceCandidateBatch? Batch,
    IReadOnlyList<ValidationIssue> Issues,
    string SourceResultFingerprint)
{
    public bool Success =>
        Issues.Count == 0 &&
        Disposition is "harmful" or "harmless" &&
        (Disposition == "harmful") == (Batch is not null);
}

internal sealed class MortalWoundOccurrenceProducerPlanningResult
{
    private readonly ReadOnlyCollection<MortalWoundOccurrenceCandidateReductionResult>
        _producerResults;
    private readonly ReadOnlyCollection<MortalWoundOccurrenceCandidateBatch>
        _harmfulBatches;
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal MortalWoundOccurrenceProducerPlanningResult(
        IReadOnlyList<MortalWoundOccurrenceCandidateReductionResult> producerResults,
        IReadOnlyList<MortalWoundOccurrenceCandidateBatch> harmfulBatches,
        IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(producerResults);
        ArgumentNullException.ThrowIfNull(harmfulBatches);
        ArgumentNullException.ThrowIfNull(issues);
        _producerResults = Array.AsReadOnly(producerResults.ToArray());
        _harmfulBatches = Array.AsReadOnly(harmfulBatches.ToArray());
        _issues = Array.AsReadOnly(issues.ToArray());
    }

    internal bool Success => _issues.Count == 0;
    internal IReadOnlyList<MortalWoundOccurrenceCandidateReductionResult>
        ProducerResults => _producerResults;
    internal IReadOnlyList<MortalWoundOccurrenceCandidateBatch> HarmfulBatches =>
        _harmfulBatches;
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
}
