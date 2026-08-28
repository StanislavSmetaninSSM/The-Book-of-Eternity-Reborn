using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record WoundOpportunityEventEvidence(
    string AdapterKind,
    string AuthorityKind,
    string AuthorityId,
    string OutcomeKind,
    int MaximumSeverityRank,
    string ReadableCause);

internal static class WoundOpportunityEventEvidenceFingerprint
{
    private const string Domain =
        "book_of_eternity.wound.opportunity_event_evidence";

    internal static string Compute(WoundOpportunityEventEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            Domain,
            "1",
            evidence.AdapterKind,
            evidence.AuthorityKind,
            evidence.AuthorityId,
            evidence.OutcomeKind,
            evidence.MaximumSeverityRank.ToString(CultureInfo.InvariantCulture),
            evidence.ReadableCause
        });
    }
}

internal sealed class WoundOpportunitySafeContext :
    IEquatable<WoundOpportunitySafeContext>
{
    private readonly string[] _allowedLocationKinds;

    internal WoundOpportunitySafeContext(
        string target,
        string cause,
        IReadOnlyList<string> allowedLocationKinds)
    {
        ArgumentNullException.ThrowIfNull(allowedLocationKinds);
        Target = target;
        Cause = cause;
        _allowedLocationKinds = allowedLocationKinds.ToArray();
    }

    internal string Target { get; }
    internal string Cause { get; }
    internal IReadOnlyList<string> AllowedLocationKinds =>
        Array.AsReadOnly(_allowedLocationKinds.ToArray());

    public bool Equals(WoundOpportunitySafeContext? other) =>
        other is not null &&
        string.Equals(Target, other.Target, StringComparison.Ordinal) &&
        string.Equals(Cause, other.Cause, StringComparison.Ordinal) &&
        _allowedLocationKinds.SequenceEqual(
            other._allowedLocationKinds,
            StringComparer.Ordinal);

    public override bool Equals(object? obj) =>
        obj is WoundOpportunitySafeContext other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Target, StringComparer.Ordinal);
        hash.Add(Cause, StringComparer.Ordinal);
        foreach (var value in _allowedLocationKinds)
            hash.Add(value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}

internal sealed record WoundGuaranteedTriggerEvidence(
    string TriggerId,
    string SourceKind,
    string SourceId,
    string SourceState,
    string Realm,
    string Domain,
    WoundOwnerCoordinate Owner,
    int RequiredSeverityRank,
    int MaterializedAtTurn,
    string SourceContractFingerprint);

internal sealed record WoundGuaranteedTriggerAuthority(
    string TriggerId,
    string SourceKind,
    string SourceId,
    string SourceState,
    string Realm,
    string Domain,
    WoundOwnerCoordinate Owner,
    int RequiredSeverityRank,
    int MaterializedAtTurn,
    string SourceContractFingerprint,
    string AuthorityFingerprint);

internal sealed record WoundOpportunityBuildRequest(
    WoundAcceptedTurnBinding Binding,
    string OpportunityId,
    string PublicRef,
    string EventRef,
    WoundOwnerCoordinate Owner,
    string Domain,
    string ProfileKey,
    string SourceKind,
    string SourceId,
    string SourceState,
    WoundOpportunityEventEvidence EventEvidence,
    int HardMaximumSeverityRank,
    WoundGuaranteedTriggerEvidence? GuaranteedTrigger,
    WoundOpportunitySafeContext SafeContext);

internal sealed record WoundOpportunityCompositionResult(
    WoundOpportunityAuthority? Opportunity,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Opportunity is not null && Issues.Count == 0;
}

internal sealed partial record WoundOpportunityAuthority
{
    private const string AuthorityDomain =
        "book_of_eternity.wound.opportunity_authority";
    private const string GuaranteeDomain =
        "book_of_eternity.wound.guaranteed_trigger_authority";
    private const int MaximumReadableLength = 2_000;

    private static readonly IReadOnlySet<string> AdapterKinds = Set(
        "formal",
        "narrative");
    private static readonly IReadOnlySet<string> OutcomeKinds = Set(
        "harmful",
        "harmless");
    private static readonly IReadOnlySet<string> Domains = Set(
        "physical",
        "spiritual");
    private static readonly IReadOnlySet<string> LocationKinds = Set(
        "anatomical",
        "systemic",
        "mental",
        "spiritual_axis",
        "other");

    internal static WoundOpportunityCompositionResult Compose(
        WoundOpportunityBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<ValidationIssue>();
        var binding = request.Binding;
        if (!ValidBinding(binding))
        {
            Add(
                issues,
                "woundOpportunity.binding",
                "wound_opportunity_binding_invalid",
                "one exact accepted-turn binding and event-set seal",
                "malformed or resealed binding");
            return new WoundOpportunityCompositionResult(null, issues);
        }

        var matchingEvents = binding.AcceptedEvents
            .Where(value => string.Equals(
                value.EventRef,
                request.EventRef!,
                StringComparison.Ordinal))
            .ToArray();
        if (matchingEvents.Length != 1)
        {
            Add(
                issues,
                "woundOpportunity.eventRef",
                "wound_opportunity_event_unresolved",
                "one exact accepted event",
                request.EventRef ?? "null");
        }

        var eventEvidence = request.EventEvidence;
        ValidateEventEvidence(eventEvidence, issues);
        var inputEvidenceFingerprint = eventEvidence is null
            ? string.Empty
            : WoundOpportunityEventEvidenceFingerprint.Compute(eventEvidence);
        if (matchingEvents.Length == 1 &&
            eventEvidence is not null &&
            (!string.Equals(
                 matchingEvents[0].Kind,
                 eventEvidence.AuthorityKind,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 matchingEvents[0].AuthorityId,
                 eventEvidence.AuthorityId,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 matchingEvents[0].SemanticFingerprint,
                 inputEvidenceFingerprint,
                 StringComparison.Ordinal)))
        {
            Add(
                issues,
                "woundOpportunity.eventEvidence",
                "wound_opportunity_event_fingerprint_mismatch",
                matchingEvents[0].SemanticFingerprint,
                inputEvidenceFingerprint);
        }

        if (eventEvidence is not null &&
            string.Equals(
                eventEvidence.OutcomeKind,
                "harmless",
                StringComparison.Ordinal) &&
            eventEvidence.MaximumSeverityRank > 0)
        {
            Add(
                issues,
                "woundOpportunity.eventEvidence.maximumSeverityRank",
                "wound_opportunity_harmless_conflict",
                "0 for a harmless accepted result",
                eventEvidence.MaximumSeverityRank.ToString(
                    CultureInfo.InvariantCulture));
        }

        ValidateCoordinates(request, issues);
        ValidateSafeContext(request.SafeContext, issues);
        WoundGuaranteedTriggerAuthority? guarantee = null;
        if (request.GuaranteedTrigger is not null && eventEvidence is not null)
        {
            guarantee = SealGuarantee(request, issues);
        }

        if (issues.Count != 0)
            return new WoundOpportunityCompositionResult(null, issues);

        var maximum = Math.Min(
            eventEvidence!.MaximumSeverityRank,
            request.HardMaximumSeverityRank);
        var minimum = guarantee?.RequiredSeverityRank;
        var safeContext = CloneSafeContext(request.SafeContext);
        var authorityFingerprint = ComputeAuthorityFingerprint(
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            request.OpportunityId,
            request.PublicRef,
            request.EventRef!,
            request.Owner,
            request.Domain,
            request.ProfileKey,
            request.SourceKind,
            request.SourceId,
            request.SourceState,
            minimum,
            maximum,
            guarantee,
            safeContext,
            inputEvidenceFingerprint);
        return new WoundOpportunityCompositionResult(
            new WoundOpportunityAuthority(
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken,
                request.OpportunityId,
                request.PublicRef,
                request.EventRef!,
                request.Owner with { },
                request.Domain,
                request.ProfileKey,
                request.SourceKind,
                request.SourceId,
                request.SourceState,
                minimum,
                maximum,
                CloneGuarantee(guarantee),
                safeContext,
                inputEvidenceFingerprint,
                authorityFingerprint),
            Array.Empty<ValidationIssue>());
    }

    internal static string RecomputeAuthorityFingerprint(
        WoundOpportunityAuthority value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ComputeAuthorityFingerprint(
            value.SessionId,
            value.RequestId,
            value.SnapshotToken,
            value.OpportunityId,
            value.PublicRef,
            value.EventRef,
            value.Owner,
            value.Domain,
            value.ProfileKey,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.MinimumSeverityRank,
            value.MaximumSeverityRank,
            value.GuaranteedTrigger,
            value.SafeContext,
            value.InputEvidenceFingerprint);
    }

    internal static bool HasCompleteShape(WoundOpportunityAuthority? value)
    {
        if (value is null ||
            !Exact(value.SessionId) ||
            !Exact(value.RequestId) ||
            !Exact(value.SnapshotToken) ||
            !Exact(value.OpportunityId) ||
            !PublicRefIsValid(value.PublicRef) ||
            !Exact(value.EventRef) ||
            !OwnerIsValid(value.Owner) ||
            !Domains.Contains(value.Domain) ||
            !Exact(value.ProfileKey) ||
            !Exact(value.SourceKind) ||
            !Exact(value.SourceId) ||
            !Exact(value.SourceState) ||
            value.MaximumSeverityRank is < 1 or > 4 ||
            value.MinimumSeverityRank is < 1 or > 4 ||
            value.MinimumSeverityRank > value.MaximumSeverityRank ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.InputEvidenceFingerprint) ||
            !SafeContextIsValid(value.SafeContext) ||
            !string.Equals(
                value.AuthorityFingerprint,
                RecomputeAuthorityFingerprint(value),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (value.GuaranteedTrigger is null)
            return value.MinimumSeverityRank is null;
        var guarantee = value.GuaranteedTrigger;
        return value.MinimumSeverityRank == guarantee.RequiredSeverityRank &&
               guarantee.RequiredSeverityRank <= value.MaximumSeverityRank &&
               GuaranteeShapeIsValid(guarantee) &&
               string.Equals(
                   guarantee.SourceKind,
                   value.SourceKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   guarantee.SourceId,
                   value.SourceId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   guarantee.SourceState,
                   value.SourceState,
                   StringComparison.Ordinal) &&
               string.Equals(
                   guarantee.Realm,
                   value.Owner.Realm,
                   StringComparison.Ordinal) &&
               string.Equals(
                   guarantee.Domain,
                   value.Domain,
                   StringComparison.Ordinal) &&
               guarantee.Owner == value.Owner &&
               string.Equals(
                   guarantee.AuthorityFingerprint,
                   ComputeGuaranteeFingerprint(guarantee),
                   StringComparison.Ordinal);
    }

    internal static WoundOpportunitySafeContext CloneSafeContext(
        WoundOpportunitySafeContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WoundOpportunitySafeContext(
            value.Target,
            value.Cause,
            value.AllowedLocationKinds);
    }

    internal static WoundGuaranteedTriggerAuthority? CloneGuarantee(
        WoundGuaranteedTriggerAuthority? value) => value is null
        ? null
        : value with { Owner = value.Owner with { } };

    private static WoundGuaranteedTriggerAuthority? SealGuarantee(
        WoundOpportunityBuildRequest request,
        ICollection<ValidationIssue> issues)
    {
        var value = request.GuaranteedTrigger!;
        if (!Exact(value.TriggerId) ||
            !Exact(value.SourceKind) ||
            !Exact(value.SourceId) ||
            !Exact(value.SourceState) ||
            !Domains.Contains(value.Domain) ||
            !OwnerIsValid(value.Owner) ||
            value.RequiredSeverityRank is < 1 or > 4 ||
            value.MaterializedAtTurn <= 0 ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                value.SourceContractFingerprint))
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger",
                "wound_guarantee_invalid",
                "one complete pre-materialized guaranteed trigger",
                "malformed guarantee evidence");
            return null;
        }

        if (!string.Equals(value.SourceState, "active", StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger.sourceState",
                "wound_guarantee_source_inactive",
                "active",
                value.SourceState);
        }
        if (value.MaterializedAtTurn >= request.Binding.Turn)
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger.materializedAtTurn",
                "wound_guarantee_not_pre_materialized",
                $"turn before {request.Binding.Turn}",
                value.MaterializedAtTurn.ToString(CultureInfo.InvariantCulture));
        }
        if (!string.Equals(value.SourceKind, request.SourceKind, StringComparison.Ordinal) ||
            !string.Equals(value.SourceId, request.SourceId, StringComparison.Ordinal) ||
            !string.Equals(value.SourceState, request.SourceState, StringComparison.Ordinal) ||
            !string.Equals(value.Realm, request.Binding.Realm, StringComparison.Ordinal) ||
            !string.Equals(value.Domain, request.Domain, StringComparison.Ordinal) ||
            value.Owner != request.Owner)
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger",
                "wound_guarantee_binding_mismatch",
                "exact opportunity source, owner, realm, and domain",
                "guarantee coordinates differ");
        }
        if (value.RequiredSeverityRank > request.HardMaximumSeverityRank)
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger.requiredSeverityRank",
                "wound_guarantee_hard_cap_conflict",
                $"I-{Roman(request.HardMaximumSeverityRank)}",
                Roman(value.RequiredSeverityRank));
        }
        else if (value.RequiredSeverityRank >
                 request.EventEvidence.MaximumSeverityRank)
        {
            Add(
                issues,
                "woundOpportunity.guaranteedTrigger.requiredSeverityRank",
                "wound_guarantee_event_cap_conflict",
                $"I-{Roman(request.EventEvidence.MaximumSeverityRank)}",
                Roman(value.RequiredSeverityRank));
        }
        if (issues.Count != 0)
            return null;

        var provisional = new WoundGuaranteedTriggerAuthority(
            value.TriggerId,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.Realm,
            value.Domain,
            value.Owner with { },
            value.RequiredSeverityRank,
            value.MaterializedAtTurn,
            value.SourceContractFingerprint,
            string.Empty);
        return provisional with
        {
            AuthorityFingerprint = ComputeGuaranteeFingerprint(provisional)
        };
    }

    private static void ValidateEventEvidence(
        WoundOpportunityEventEvidence value,
        ICollection<ValidationIssue> issues)
    {
        if (value is null ||
            !AdapterKinds.Contains(value.AdapterKind) ||
            !Exact(value.AuthorityKind) ||
            !Exact(value.AuthorityId) ||
            !OutcomeKinds.Contains(value.OutcomeKind) ||
            value.MaximumSeverityRank is < 0 or > 4 ||
            !Readable(value.ReadableCause))
        {
            Add(
                issues,
                "woundOpportunity.eventEvidence",
                "wound_opportunity_event_evidence_invalid",
                "closed formal/narrative evidence, outcome, severity cap, and cause",
                "malformed evidence");
            return;
        }
        if (string.Equals(value.OutcomeKind, "harmful", StringComparison.Ordinal) &&
            value.MaximumSeverityRank == 0)
        {
            Add(
                issues,
                "woundOpportunity.eventEvidence.maximumSeverityRank",
                "wound_opportunity_harmful_cap_invalid",
                "I-IV for a harmful accepted result",
                "0");
        }
    }

    private static void ValidateCoordinates(
        WoundOpportunityBuildRequest request,
        ICollection<ValidationIssue> issues)
    {
        if (!Exact(request.OpportunityId) ||
            !PublicRefIsValid(request.PublicRef) ||
            !Exact(request.EventRef) ||
            !OwnerIsValid(request.Owner) ||
            !Domains.Contains(request.Domain) ||
            !Exact(request.ProfileKey) ||
            !Exact(request.SourceKind) ||
            !Exact(request.SourceId) ||
            !Exact(request.SourceState) ||
            request.HardMaximumSeverityRank is < 1 or > 4)
        {
            Add(
                issues,
                "woundOpportunity",
                "wound_opportunity_coordinate_invalid",
                "exact opportunity, owner, source, domain, profile, and hard cap",
                "malformed coordinate");
        }
        if (request.Owner is not null &&
            !string.Equals(
                request.Owner.Realm,
                request.Binding.Realm,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundOpportunity.owner.realm",
                "wound_opportunity_realm_mismatch",
                request.Binding.Realm,
                request.Owner.Realm);
        }
        var expectedDomain = string.Equals(
            request.Binding.Realm,
            "mortal_world",
            StringComparison.Ordinal)
            ? "physical"
            : "spiritual";
        if (!string.Equals(request.Domain, expectedDomain, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundOpportunity.domain",
                "wound_opportunity_domain_mismatch",
                expectedDomain,
                request.Domain ?? "null");
        }
    }

    private static void ValidateSafeContext(
        WoundOpportunitySafeContext value,
        ICollection<ValidationIssue> issues)
    {
        if (!SafeContextIsValid(value))
        {
            Add(
                issues,
                "woundOpportunity.safeContext",
                "wound_opportunity_safe_context_invalid",
                "bounded target, cause, and unique closed location kinds",
                "malformed safe context");
        }
    }

    private static bool ValidBinding(WoundAcceptedTurnBinding? value) =>
        value is not null &&
        Exact(value.SessionId) &&
        Exact(value.RequestId) &&
        Exact(value.SnapshotToken) &&
        value.Turn > 0 &&
        value.Realm is "mortal_world" or "chaos_sea" or "shining_abode" &&
        value.AcceptedEvents.Count > 0 &&
        value.AcceptedEvents.All(eventValue =>
            eventValue is not null &&
            Exact(eventValue.EventRef) &&
            Exact(eventValue.Kind) &&
            Exact(eventValue.AuthorityId) &&
            ResourceMaterializationContract.IsAuthorityFingerprint(
                eventValue.SemanticFingerprint)) &&
        string.Equals(
            value.AcceptedEventsFingerprint,
            WoundAcceptedEventSetFingerprint.Compute(value.AcceptedEvents),
            StringComparison.Ordinal);

    private static bool SafeContextIsValid(WoundOpportunitySafeContext? value)
    {
        if (value is null ||
            !Readable(value.Target) ||
            !Readable(value.Cause) ||
            value.AllowedLocationKinds.Count == 0 ||
            value.AllowedLocationKinds.Count > LocationKinds.Count ||
            value.AllowedLocationKinds.Any(kind => !LocationKinds.Contains(kind)))
        {
            return false;
        }
        return value.AllowedLocationKinds.Distinct(StringComparer.Ordinal).Count() ==
               value.AllowedLocationKinds.Count;
    }

    private static bool GuaranteeShapeIsValid(
        WoundGuaranteedTriggerAuthority value) =>
        Exact(value.TriggerId) &&
        Exact(value.SourceKind) &&
        Exact(value.SourceId) &&
        string.Equals(value.SourceState, "active", StringComparison.Ordinal) &&
        value.Realm is "mortal_world" or "chaos_sea" or "shining_abode" &&
        Domains.Contains(value.Domain) &&
        OwnerIsValid(value.Owner) &&
        value.RequiredSeverityRank is >= 1 and <= 4 &&
        value.MaterializedAtTurn > 0 &&
        ResourceMaterializationContract.IsAuthorityFingerprint(
            value.SourceContractFingerprint);

    private static bool OwnerIsValid(WoundOwnerCoordinate? value) => value switch
    {
        { Realm: "mortal_world", OwnerKind: "player", OwnerId: "player_current",
            CarrierPath: WoundCarrierCatalog.PlayerPath } => true,
        { Realm: "mortal_world", OwnerKind: "npc",
            CarrierPath: WoundCarrierCatalog.NpcPath } => Exact(value.OwnerId),
        { Realm: "mortal_world", OwnerKind: "combatant" or "combatant_member",
            CarrierPath: WoundCarrierCatalog.EnemiesPath or
                WoundCarrierCatalog.AlliesPath } => Exact(value.OwnerId),
        { Realm: "chaos_sea" or "shining_abode",
            OwnerKind: "player_soul" or "guardian" or "resident" or
                "radiant_actor" or "afterlife_actor",
            CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath } =>
            Exact(value.OwnerId),
        _ => false
    };

    private static string ComputeGuaranteeFingerprint(
        WoundGuaranteedTriggerAuthority value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            GuaranteeDomain,
            "1",
            value.TriggerId,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.Realm,
            value.Domain,
            value.Owner.Realm,
            value.Owner.OwnerKind,
            value.Owner.OwnerId,
            value.Owner.CarrierPath,
            value.RequiredSeverityRank.ToString(CultureInfo.InvariantCulture),
            value.MaterializedAtTurn.ToString(CultureInfo.InvariantCulture),
            value.SourceContractFingerprint
        });

    private static string ComputeAuthorityFingerprint(
        string sessionId,
        string requestId,
        string snapshotToken,
        string opportunityId,
        string publicRef,
        string eventRef,
        WoundOwnerCoordinate owner,
        string domain,
        string profileKey,
        string sourceKind,
        string sourceId,
        string sourceState,
        int? minimumSeverityRank,
        int maximumSeverityRank,
        WoundGuaranteedTriggerAuthority? guarantee,
        WoundOpportunitySafeContext safeContext,
        string inputEvidenceFingerprint)
    {
        var fields = new List<string?>
        {
            AuthorityDomain,
            "1",
            sessionId,
            requestId,
            snapshotToken,
            opportunityId,
            publicRef,
            eventRef,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            domain,
            profileKey,
            sourceKind,
            sourceId,
            sourceState,
            minimumSeverityRank?.ToString(CultureInfo.InvariantCulture),
            maximumSeverityRank.ToString(CultureInfo.InvariantCulture),
            guarantee?.AuthorityFingerprint,
            safeContext.Target,
            safeContext.Cause,
            safeContext.AllowedLocationKinds.Count.ToString(
                CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < safeContext.AllowedLocationKinds.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(safeContext.AllowedLocationKinds[index]);
        }
        fields.Add(inputEvidenceFingerprint);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool Readable(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.Length <= MaximumReadableLength &&
        !value.Any(char.IsControl);

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool PublicRefIsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 256 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static string Roman(int rank) => rank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => rank.ToString(CultureInfo.InvariantCulture)
    };

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "Wound opportunity violates sealed accepted-turn authority.",
        code: code,
        section: "wound_opportunity",
        expected: expected,
        actual: actual,
        repairHint:
            "Use the unchanged accepted event and select only a legal wound decision."));
}

internal sealed record WoundOpportunityDecisionRequest(
    string OpportunityRef,
    string Decision,
    int? SeverityRank,
    string? LocalWoundRef);

internal sealed record WoundOpportunityDecisionReceipt(
    string OpportunityId,
    string DecisionFingerprint,
    string OperationKey);

internal sealed record WoundOpportunityDecisionResult(
    WoundOpportunityDecisionAuthority? Decision,
    bool AlreadyConsumed,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Decision is not null && Issues.Count == 0;
}

internal sealed partial record WoundOpportunityDecisionAuthority(
    string OpportunityId,
    string Decision,
    int? SelectedSeverityRank,
    string? LocalWoundRef,
    string OperationKey,
    string DecisionFingerprint)
{
    private const string DecisionDomain =
        "book_of_eternity.wound.opportunity_decision";

    internal static WoundOpportunityDecisionResult Evaluate(
        WoundOpportunityAuthority opportunity,
        WoundOpportunityDecisionRequest request,
        IReadOnlyList<WoundOpportunityDecisionReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(receipts);
        var issues = new List<ValidationIssue>();
        if (!WoundOpportunityAuthority.HasCompleteShape(opportunity))
        {
            Add(
                issues,
                "woundDecision.opportunityRef",
                "wound_opportunity_authority_invalid",
                "one complete sealed opportunity",
                "malformed or resealed opportunity");
            return new WoundOpportunityDecisionResult(null, false, issues);
        }
        if (!string.Equals(
                request.OpportunityRef,
                opportunity.PublicRef,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundDecision.opportunityRef",
                "wound_opportunity_reference_mismatch",
                opportunity.PublicRef,
                request.OpportunityRef ?? "null");
        }

        int? selectedSeverity = null;
        string? localWoundRef = null;
        switch (request.Decision)
        {
            case "none":
                if (request.SeverityRank is not null || request.LocalWoundRef is not null)
                {
                    Add(
                        issues,
                        "woundDecision",
                        "wound_none_decision_has_proposal",
                        "none with no severity or wound reference",
                        "unexpected materialization fields");
                }
                if (opportunity.GuaranteedTrigger is not null)
                {
                    Add(
                        issues,
                        "woundDecision.decision",
                        "wound_guaranteed_result_required",
                        "materialize",
                        "none");
                }
                break;
            case "materialize":
                selectedSeverity = request.SeverityRank;
                localWoundRef = request.LocalWoundRef;
                if (selectedSeverity is not (>= 1 and <= 4) ||
                    !ResourceMaterializationContract.IsExactIdentifier(localWoundRef))
                {
                    Add(
                        issues,
                        "woundDecision",
                        "wound_materialize_decision_invalid",
                        "severity I-IV and one exact response-local woundRef",
                        "missing or malformed proposal coordinate");
                    break;
                }
                if (selectedSeverity > opportunity.MaximumSeverityRank)
                {
                    Add(
                        issues,
                        "woundDecision.severityRank",
                        "wound_severity_above_opportunity",
                        $"I-{Roman(opportunity.MaximumSeverityRank)}",
                        Roman(selectedSeverity.Value));
                }
                if (opportunity.GuaranteedTrigger is not null &&
                    selectedSeverity !=
                    opportunity.GuaranteedTrigger.RequiredSeverityRank)
                {
                    Add(
                        issues,
                        "woundDecision.severityRank",
                        "wound_guaranteed_severity_mismatch",
                        Roman(opportunity.GuaranteedTrigger.RequiredSeverityRank),
                        Roman(selectedSeverity.Value));
                }
                break;
            default:
                Add(
                    issues,
                    "woundDecision.decision",
                    "wound_decision_kind_invalid",
                    "none or materialize",
                    request.Decision ?? "null");
                break;
        }
        if (issues.Count != 0)
            return new WoundOpportunityDecisionResult(null, false, issues);

        var decisionFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                DecisionDomain,
                "1",
                opportunity.AuthorityFingerprint,
                opportunity.OpportunityId,
                opportunity.EventRef,
                opportunity.Owner.Realm,
                opportunity.Owner.OwnerKind,
                opportunity.Owner.OwnerId,
                opportunity.Owner.CarrierPath,
                request.Decision,
                selectedSeverity?.ToString(CultureInfo.InvariantCulture),
                localWoundRef
            });
        var operationKey = "wound_operation_" +
            decisionFingerprint["sha256:".Length..];
        var accepted = new WoundOpportunityDecisionAuthority(
            opportunity.OpportunityId,
            request.Decision!,
            selectedSeverity,
            localWoundRef,
            operationKey,
            decisionFingerprint);
        var consumed = receipts
            .Where(value => value is not null && string.Equals(
                value.OpportunityId,
                opportunity.OpportunityId,
                StringComparison.Ordinal))
            .ToArray();
        if (consumed.Length == 0)
        {
            return new WoundOpportunityDecisionResult(
                accepted,
                false,
                Array.Empty<ValidationIssue>());
        }
        if (consumed.Length == 1 &&
            string.Equals(
                consumed[0].DecisionFingerprint,
                decisionFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                consumed[0].OperationKey,
                operationKey,
                StringComparison.Ordinal))
        {
            return new WoundOpportunityDecisionResult(
                accepted,
                true,
                Array.Empty<ValidationIssue>());
        }

        Add(
            issues,
            "woundDecision.opportunityRef",
            "wound_opportunity_already_consumed",
            "the exact previously accepted decision or a new opportunity",
            "changed or ambiguous replay");
        return new WoundOpportunityDecisionResult(null, false, issues);
    }

    private static string Roman(int rank) => rank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => rank.ToString(CultureInfo.InvariantCulture)
    };

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "Wound decision violates its sealed opportunity.",
        code: code,
        section: "wound_opportunity_decision",
        expected: expected,
        actual: actual,
        repairHint:
            "Keep the same opportunity and choose none or one legal bounded wound."));
}
