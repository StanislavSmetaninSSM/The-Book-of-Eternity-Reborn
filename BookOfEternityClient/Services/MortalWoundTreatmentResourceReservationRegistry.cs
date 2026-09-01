using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentResourceReservationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentResourceReservationAuthority? Authority,
    MortalWoundTreatmentResourceReservationOwnership? Ownership = null);

internal sealed record MortalWoundTreatmentResourceLifecycleResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    int ChangedCount);

internal enum MortalWoundTreatmentResourceReservationState
{
    ProvisionalHeld,
    ConfirmedHeld
}

internal sealed class MortalWoundTreatmentResourceReservationAgreement
{
    private readonly ReadOnlyCollection<string> _claimFingerprints;
    private readonly ReadOnlyCollection<string> _bindingFingerprints;
    private readonly ReadOnlyCollection<long> _availableQuantities;

    internal MortalWoundTreatmentResourceReservationAgreement(
        object registryCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority authority,
        MortalWoundTreatmentResourceReservationState state)
    {
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability))
        {
            throw new InvalidOperationException(
                "Treatment resource agreements require registry authority.");
        }

        OperationKey = coordinates.OperationKey;
        AttemptId = coordinates.AttemptId;
        Mode = mode;
        CoordinatesFingerprint = coordinates.CoordinatesFingerprint;
        AcceptedStateFingerprint = coordinates.AcceptedStateFingerprint;
        RouteFingerprint = authority.RouteFingerprint;
        CourseId = authority.CourseId;
        CourseMilestoneOrdinal = authority.CourseMilestoneOrdinal;
        CourseCoordinateFingerprint = authority.CourseCoordinateFingerprint;
        RequirementAuthorityFingerprint = authority.RequirementAuthorityFingerprint;
        PolicyFingerprint =
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(
                authority.Policy);
        _claimFingerprints = Array.AsReadOnly(authority.Claims
            .Select(static claim => claim.ClaimFingerprint)
            .ToArray());
        _bindingFingerprints = Array.AsReadOnly(authority.Claims
            .Select(static claim => claim.BindingFingerprint)
            .ToArray());
        _availableQuantities = Array.AsReadOnly(authority.Claims
            .Select(static claim => claim.AvailableQuantity)
            .ToArray());
        AuthorityFingerprint = authority.AuthorityFingerprint;
        Authority = authority;
        State = state;
        AgreementFingerprint = ComputeAgreementFingerprint(
            coordinates,
            mode,
            authority);
    }

    internal string OperationKey { get; }
    internal string AttemptId { get; }
    internal string Mode { get; }
    internal string CoordinatesFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }
    internal string RouteFingerprint { get; }
    internal string? CourseId { get; }
    internal int? CourseMilestoneOrdinal { get; }
    internal string? CourseCoordinateFingerprint { get; }
    internal string RequirementAuthorityFingerprint { get; }
    internal string PolicyFingerprint { get; }
    internal IReadOnlyList<string> ClaimFingerprints => _claimFingerprints;
    internal IReadOnlyList<string> BindingFingerprints => _bindingFingerprints;
    internal IReadOnlyList<long> AvailableQuantities => _availableQuantities;
    internal string AuthorityFingerprint { get; }
    internal string AgreementFingerprint { get; }
    internal MortalWoundTreatmentResourceReservationAuthority Authority { get; }
    internal MortalWoundTreatmentResourceReservationState State { get; set; }
    internal string? PersistedRequestFingerprint { get; set; }

    internal bool Agrees(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority authority) =>
        string.Equals(OperationKey, coordinates.OperationKey, StringComparison.Ordinal) &&
        string.Equals(AttemptId, coordinates.AttemptId, StringComparison.Ordinal) &&
        string.Equals(Mode, mode, StringComparison.Ordinal) &&
        string.Equals(
            CoordinatesFingerprint,
            coordinates.CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AcceptedStateFingerprint,
            coordinates.AcceptedStateFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(RouteFingerprint, authority.RouteFingerprint, StringComparison.Ordinal) &&
        string.Equals(CourseId, authority.CourseId, StringComparison.Ordinal) &&
        CourseMilestoneOrdinal == authority.CourseMilestoneOrdinal &&
        string.Equals(
            CourseCoordinateFingerprint,
            authority.CourseCoordinateFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            RequirementAuthorityFingerprint,
            authority.RequirementAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            PolicyFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(authority.Policy),
            StringComparison.Ordinal) &&
        ClaimFingerprints.SequenceEqual(
            authority.Claims.Select(static claim => claim.ClaimFingerprint),
            StringComparer.Ordinal) &&
        BindingFingerprints.SequenceEqual(
            authority.Claims.Select(static claim => claim.BindingFingerprint),
            StringComparer.Ordinal) &&
        AvailableQuantities.SequenceEqual(
            authority.Claims.Select(static claim => claim.AvailableQuantity)) &&
        string.Equals(
            AuthorityFingerprint,
            authority.AuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AgreementFingerprint,
            ComputeAgreementFingerprint(coordinates, mode, authority),
            StringComparison.Ordinal);

    private static string ComputeAgreementFingerprint(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority authority)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_reservation_agreement",
            "1",
            coordinates.OperationKey,
            coordinates.AttemptId,
            mode,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            authority.RouteFingerprint,
            authority.CourseId,
            authority.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            authority.CourseCoordinateFingerprint,
            authority.RequirementAuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(authority.Policy),
            authority.Claims.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var claim in authority.Claims)
        {
            fields.Add(claim.ClaimFingerprint);
            fields.Add(claim.BindingFingerprint);
            fields.Add(claim.AvailableQuantity.ToString(CultureInfo.InvariantCulture));
        }
        fields.Add(authority.AuthorityFingerprint);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}

internal sealed class MortalWoundTreatmentResourceReservationOwnership
{
    private readonly MortalWoundTreatmentResourceReservationAgreement _agreement;
    private readonly MortalWoundTreatmentResourceReservationAuthority _authority;

    private MortalWoundTreatmentResourceReservationOwnership(
        MortalWoundTreatmentResourceReservationAgreement agreement,
        MortalWoundTreatmentResourceReservationAuthority authority,
        bool wasCreated)
    {
        _agreement = agreement;
        _authority = authority;
        WasCreated = wasCreated;
    }

    internal string OperationKey => _agreement.OperationKey;
    internal bool WasCreated { get; }

    internal static MortalWoundTreatmentResourceReservationOwnership Create(
        object registryCapability,
        MortalWoundTreatmentResourceReservationAgreement agreement,
        bool wasCreated)
    {
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability))
        {
            throw new InvalidOperationException(
                "Treatment resource ownership requires registry authority.");
        }
        ArgumentNullException.ThrowIfNull(agreement);
        return new MortalWoundTreatmentResourceReservationOwnership(
            agreement,
            agreement.Authority,
            wasCreated);
    }

    internal bool Matches(
        MortalWoundTreatmentResourceReservationAgreement agreement,
        MortalWoundTreatmentResourceReservationAuthority authority) =>
        ReferenceEquals(_agreement, agreement) &&
        ReferenceEquals(_authority, authority);
}

internal sealed class MortalWoundTreatmentResourceReservationRegistry
{
    private const string IssuePath = "treatmentAttempt.resources";
    private readonly Dictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement> _byOperationKey =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FinalizedTombstone> _finalizedByOperationKey =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReleasedTombstone> _releasedByOperationKey =
        new(StringComparer.Ordinal);

    internal MortalWoundTreatmentResourceReservationResult Reserve(
        object registryCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority candidate) => Restore(
        registryCapability,
        coordinates,
        mode,
        candidate,
        MortalWoundTreatmentResourceReservationState.ProvisionalHeld);

    internal MortalWoundTreatmentResourceReservationResult RestoreConfirmed(
        object registryCapability,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceReservationAuthority candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        var restored = Restore(
            registryCapability,
            request.Coordinates,
            request.Mode,
            candidate,
            MortalWoundTreatmentResourceReservationState.ConfirmedHeld);
        if (restored.IsValid &&
            _byOperationKey.TryGetValue(
                request.Coordinates.OperationKey,
                out var agreement))
        {
            agreement.State =
                MortalWoundTreatmentResourceReservationState.ConfirmedHeld;
            agreement.PersistedRequestFingerprint = request.RequestFingerprint;
        }
        return restored;
    }

    internal MortalWoundTreatmentResourceReservationResult RestoreFinalized(
        object registryCapability,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceReservationAuthority candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability) ||
            !CandidateAgrees(request.Coordinates, candidate))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_authority_invalid",
                "one registry-authorized finalized treatment request",
                request.RequestFingerprint);
        }

        if (_byOperationKey.ContainsKey(request.Coordinates.OperationKey))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_conflict",
                "one finalized history tombstone without an active hold",
                request.Coordinates.OperationKey);
        }

        var historicalFinalizationFingerprint =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.resource_finalized_history",
                "1",
                request.RequestFingerprint,
                candidate.AuthorityFingerprint
            });
        var tombstone = new FinalizedTombstone(
            request.Coordinates.OperationKey,
            request.RequestFingerprint,
            candidate.ReservationId,
            candidate.AuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(
                candidate.Policy),
            historicalFinalizationFingerprint);
        if (_finalizedByOperationKey.TryGetValue(
                request.Coordinates.OperationKey,
                out var existing))
        {
            return existing == tombstone
                ? Valid(candidate, MortalWoundTreatmentResourceReservationOwnership.Create(
                    registryCapability,
                    new MortalWoundTreatmentResourceReservationAgreement(
                        registryCapability,
                        request.Coordinates,
                        request.Mode,
                        candidate,
                        MortalWoundTreatmentResourceReservationState.ConfirmedHeld),
                    wasCreated: false))
                : Invalid(
                    "mortal_wound_treatment_resource_reservation_conflict",
                    "the exact prior finalized history tombstone",
                    request.Coordinates.OperationKey);
        }
        _finalizedByOperationKey.Add(request.Coordinates.OperationKey, tombstone);
        return Valid(candidate, MortalWoundTreatmentResourceReservationOwnership.Create(
            registryCapability,
            new MortalWoundTreatmentResourceReservationAgreement(
                registryCapability,
                request.Coordinates,
                request.Mode,
                candidate,
                MortalWoundTreatmentResourceReservationState.ConfirmedHeld),
            wasCreated: true));
    }

    internal bool IsEmpty => _byOperationKey.Count == 0 &&
                             _finalizedByOperationKey.Count == 0;

    private MortalWoundTreatmentResourceReservationResult Restore(
        object registryCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority candidate,
        MortalWoundTreatmentResourceReservationState state)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_authority_invalid",
                "registry-authorized resource reservation access",
                "missing registry authority");
        }

        if (!CandidateAgrees(coordinates, candidate))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_authority_invalid",
                "one exact composer-minted authority for the current coordinates",
                candidate.AuthorityFingerprint);
        }

        if (_byOperationKey.TryGetValue(
                coordinates.OperationKey,
                out var existing))
        {
            return existing.Agrees(coordinates, mode, candidate)
                ? Valid(
                    existing.Authority,
                    MortalWoundTreatmentResourceReservationOwnership.Create(
                        registryCapability,
                        existing,
                        wasCreated: false))
                : Invalid(
                    "mortal_wound_treatment_resource_reservation_conflict",
                    "the exact prior resource agreement for this operation key",
                    coordinates.OperationKey);
        }

        if (_finalizedByOperationKey.ContainsKey(coordinates.OperationKey))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_finalized",
                "an operation key not already finalized",
                coordinates.OperationKey);
        }

        var agreement = new MortalWoundTreatmentResourceReservationAgreement(
            registryCapability,
            coordinates,
            mode,
            candidate,
            state);
        if (_releasedByOperationKey.TryGetValue(
                coordinates.OperationKey,
                out var released) &&
            !string.Equals(
                released.AgreementFingerprint,
                agreement.AgreementFingerprint,
                StringComparison.Ordinal))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_conflict",
                "the exact released agreement semantics for retry",
                coordinates.OperationKey);
        }

        if (!TryValidateAggregate(candidate, out var aggregateFailure))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_overbooked",
                "aggregate held quantity no greater than the witnessed availability",
                aggregateFailure!);
        }

        _releasedByOperationKey.Remove(coordinates.OperationKey);
        _byOperationKey.Add(coordinates.OperationKey, agreement);
        return Valid(
            candidate,
            MortalWoundTreatmentResourceReservationOwnership.Create(
                registryCapability,
                agreement,
                wasCreated: true));
    }

    internal bool RollbackNew(
        object registryCapability,
        MortalWoundTreatmentResourceReservationOwnership ownership,
        MortalWoundTreatmentResourceReservationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(authority);
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability) ||
            !_byOperationKey.TryGetValue(
                ownership.OperationKey,
                out var current) ||
            !ownership.Matches(current, authority))
        {
            return false;
        }

        if (current.State !=
            MortalWoundTreatmentResourceReservationState.ProvisionalHeld)
        {
            return false;
        }

        if (!ownership.WasCreated)
            return true;
        _byOperationKey.Remove(ownership.OperationKey);
        return true;
    }

    internal MortalWoundTreatmentResourceLifecycleResult ConfirmPersisted(
        object registryCapability,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
    {
        if (!TryValidateRequestBatch(
                registryCapability,
                requests,
                out var agreements,
                out var failure))
        {
            return failure!;
        }

        foreach (var (request, agreement) in agreements!)
        {
            if (agreement!.PersistedRequestFingerprint is not null &&
                !string.Equals(
                    agreement.PersistedRequestFingerprint,
                    request.RequestFingerprint,
                    StringComparison.Ordinal))
            {
                return LifecycleInvalid(
                    "mortal_wound_treatment_resource_confirmation_conflict",
                    "the exact prior persisted request fingerprint",
                    request.RequestFingerprint);
            }
        }

        var changed = 0;
        foreach (var (request, agreement) in agreements)
        {
            if (agreement!.State ==
                MortalWoundTreatmentResourceReservationState.ProvisionalHeld)
            {
                agreement.State =
                    MortalWoundTreatmentResourceReservationState.ConfirmedHeld;
                changed++;
            }
            agreement.PersistedRequestFingerprint ??= request.RequestFingerprint;
        }
        return LifecycleValid(changed);
    }

    internal MortalWoundTreatmentResourceLifecycleResult Release(
        object registryCapability,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
        string reason)
    {
        if (reason is not ("cancelled" or "validation_failed" or "rolled_back"))
        {
            return LifecycleInvalid(
                "mortal_wound_treatment_resource_release_reason_invalid",
                "cancelled | validation_failed | rolled_back",
                reason);
        }
        if (!TryValidateRequestBatch(
                registryCapability,
                requests,
                out var agreements,
                out var failure,
                allowMissing: true))
        {
            return failure!;
        }

        foreach (var (request, agreement) in agreements!)
        {
            if (agreement is null)
            {
                if (!_releasedByOperationKey.TryGetValue(
                        request.Coordinates.OperationKey,
                        out var released))
                {
                    return LifecycleInvalid(
                        "mortal_wound_treatment_resource_release_conflict",
                        "one exact active agreement or prior release tombstone",
                        request.Coordinates.OperationKey);
                }
                var rebuilt = MortalWoundTreatmentResourceComposer
                    .RehydratePersistedAuthority(request);
                if (!rebuilt.IsValid || rebuilt.Authority is null)
                    return new MortalWoundTreatmentResourceLifecycleResult(
                        false,
                        rebuilt.Issues,
                        0);
                var rebuiltAgreement =
                    new MortalWoundTreatmentResourceReservationAgreement(
                        registryCapability,
                        request.Coordinates,
                        request.Mode,
                        rebuilt.Authority,
                        MortalWoundTreatmentResourceReservationState.ProvisionalHeld);
                if (!string.Equals(
                        released.RequestFingerprint,
                        request.RequestFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        released.AgreementFingerprint,
                        rebuiltAgreement.AgreementFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        released.Reason,
                        reason,
                        StringComparison.Ordinal))
                {
                    return LifecycleInvalid(
                        "mortal_wound_treatment_resource_release_conflict",
                        "the exact already released request and agreement",
                        request.RequestFingerprint);
                }
                continue;
            }
            if (agreement.PersistedRequestFingerprint is not null &&
                !string.Equals(
                    agreement.PersistedRequestFingerprint,
                    request.RequestFingerprint,
                    StringComparison.Ordinal))
            {
                return LifecycleInvalid(
                    "mortal_wound_treatment_resource_release_conflict",
                    "the exact confirmed persisted request",
                    request.RequestFingerprint);
            }
            if (string.Equals(reason, "rolled_back", StringComparison.Ordinal) &&
                agreement.State !=
                    MortalWoundTreatmentResourceReservationState.ProvisionalHeld)
            {
                return LifecycleInvalid(
                    "mortal_wound_treatment_resource_release_conflict",
                    "a provisional held agreement eligible for rollback release",
                    request.RequestFingerprint);
            }
        }

        var changed = 0;
        foreach (var (request, agreement) in agreements)
        {
            if (agreement is null)
                continue;
            _byOperationKey.Remove(request.Coordinates.OperationKey);
            _releasedByOperationKey[request.Coordinates.OperationKey] =
                new ReleasedTombstone(
                    request.RequestFingerprint,
                    agreement.AgreementFingerprint,
                    reason);
            changed++;
        }
        return LifecycleValid(changed);
    }

    internal MortalWoundTreatmentResourceLifecycleResult Commit(
        object registryCapability,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        ArgumentNullException.ThrowIfNull(finalization);
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability))
        {
            return LifecycleInvalid(
                "mortal_wound_treatment_resource_commit_authority_invalid",
                "one registry-authorized exact resource finalization",
                finalization.FinalizationFingerprint);
        }

        var finalized = _finalizedByOperationKey.Values.Where(candidate =>
                string.Equals(
                    candidate.RequestFingerprint,
                    finalization.RequestFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        if (finalized.Length != 0)
        {
            return finalized.Length == 1 &&
                   string.Equals(
                       finalized[0].AuthorityFingerprint,
                       finalization.ResourceAuthorityFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       finalized[0].ReservationId,
                       finalization.ReservationId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       finalized[0].FinalizationFingerprint,
                       finalization.FinalizationFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       ComputeFinalizationFingerprint(
                           finalized[0].PolicyFingerprint,
                           finalization),
                       finalization.FinalizationFingerprint,
                       StringComparison.Ordinal)
                ? LifecycleValid(0)
                : LifecycleInvalid(
                    "mortal_wound_treatment_resource_commit_conflict",
                    "the exact prior finalization tombstone",
                    finalization.FinalizationFingerprint);
        }

        var active = _byOperationKey.Values.Where(candidate =>
                string.Equals(
                    candidate.PersistedRequestFingerprint,
                    finalization.RequestFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        if (active.Length != 1 ||
            active[0].State !=
                MortalWoundTreatmentResourceReservationState.ConfirmedHeld ||
            !string.Equals(
                active[0].AuthorityFingerprint,
                finalization.ResourceAuthorityFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                active[0].Authority.ReservationId,
                finalization.ReservationId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ComputeFinalizationFingerprint(
                    active[0].PolicyFingerprint,
                    finalization),
                finalization.FinalizationFingerprint,
                StringComparison.Ordinal))
        {
            return LifecycleInvalid(
                "mortal_wound_treatment_resource_commit_conflict",
                "one exact confirmed persisted reservation",
                finalization.RequestFingerprint);
        }

        var agreement = active[0];
        _byOperationKey.Remove(agreement.OperationKey);
        _finalizedByOperationKey.Add(
            agreement.OperationKey,
            new FinalizedTombstone(
                agreement.OperationKey,
                finalization.RequestFingerprint,
                finalization.ReservationId,
                finalization.ResourceAuthorityFingerprint,
                agreement.PolicyFingerprint,
                finalization.FinalizationFingerprint));
        return LifecycleValid(1);
    }

    private static string ComputeFinalizationFingerprint(
        string policyFingerprint,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_finalization",
            "1",
            finalization.Disposition,
            finalization.ReservationId,
            finalization.RequestFingerprint,
            finalization.ResultFingerprint,
            finalization.ResourceAuthorityFingerprint,
            policyFingerprint,
            finalization.ConsumptionTrigger,
            finalization.Consumptions.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(finalization.Consumptions.Select(static intent =>
            intent.IntentFingerprint));
        fields.Add(finalization.ReleasedClaimFingerprints.Count.ToString(
            CultureInfo.InvariantCulture));
        fields.AddRange(finalization.ReleasedClaimFingerprints);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal void InvalidateAll()
    {
        _byOperationKey.Clear();
        _finalizedByOperationKey.Clear();
        _releasedByOperationKey.Clear();
    }

    private bool TryValidateRequestBatch(
        object registryCapability,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest>? requests,
        out IReadOnlyList<(MortalWoundTreatmentAttemptRequest Request,
            MortalWoundTreatmentResourceReservationAgreement? Agreement)>? agreements,
        out MortalWoundTreatmentResourceLifecycleResult? failure,
        bool allowMissing = false)
    {
        agreements = null;
        failure = null;
        if (!AcceptedTurnAuthorityRegistry.IsTreatmentResourceRegistryCapability(
                registryCapability) ||
            requests is null ||
            requests.Count > WoundResponseInputComposer.MaximumAcceptedCommandCount)
        {
            failure = LifecycleInvalid(
                "mortal_wound_treatment_resource_lifecycle_authority_invalid",
                "one registry-authorized bounded complete request batch",
                requests is null ? "missing requests" : requests.Count.ToString(
                    CultureInfo.InvariantCulture));
            return false;
        }

        var validated = new List<(MortalWoundTreatmentAttemptRequest,
            MortalWoundTreatmentResourceReservationAgreement?)>();
        var operationKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (request is null ||
                !operationKeys.Add(request.Coordinates.OperationKey) ||
                !MortalWoundTreatmentDetachedSealValidator.IsValid(request))
            {
                failure = LifecycleInvalid(
                    "mortal_wound_treatment_resource_lifecycle_request_invalid",
                    "one complete sealed request per unique operation key",
                    request?.RequestFingerprint ?? "missing request");
                return false;
            }

            var rebuilt = MortalWoundTreatmentResourceComposer
                .RehydratePersistedAuthority(request);
            if (!rebuilt.IsValid || rebuilt.Authority is null)
            {
                failure = new MortalWoundTreatmentResourceLifecycleResult(
                    false,
                    rebuilt.Issues,
                    0);
                return false;
            }
            if (_finalizedByOperationKey.ContainsKey(
                    request.Coordinates.OperationKey))
            {
                failure = LifecycleInvalid(
                    "mortal_wound_treatment_resource_lifecycle_finalized",
                    "an operation key without a finalized tombstone",
                    request.Coordinates.OperationKey);
                return false;
            }
            if (!_byOperationKey.TryGetValue(
                    request.Coordinates.OperationKey,
                    out var agreement))
            {
                if (allowMissing)
                {
                    validated.Add((request, null));
                    continue;
                }
                failure = LifecycleInvalid(
                    "mortal_wound_treatment_resource_lifecycle_missing",
                    "one exact active reservation agreement",
                    request.Coordinates.OperationKey);
                return false;
            }
            if (!agreement.Agrees(
                    request.Coordinates,
                    request.Mode,
                    rebuilt.Authority))
            {
                failure = LifecycleInvalid(
                    "mortal_wound_treatment_resource_lifecycle_conflict",
                    "the exact active reservation agreement",
                    request.RequestFingerprint);
                return false;
            }
            validated.Add((request, agreement));
        }
        agreements = validated;
        return true;
    }

    private bool TryValidateAggregate(
        MortalWoundTreatmentResourceReservationAuthority candidate,
        out string? failure)
    {
        var aggregate = new Dictionary<ResourceAggregateKey, ResourceAggregate>();
        try
        {
            foreach (var agreement in _byOperationKey.Values)
            {
                foreach (var claim in agreement.Authority.Claims)
                {
                    if (!TryAddClaim(aggregate, claim, out failure))
                        return false;
                }
            }
            foreach (var claim in candidate.Claims)
            {
                if (!TryAddClaim(aggregate, claim, out failure))
                    return false;
            }
        }
        catch (OverflowException)
        {
            failure = "checked signed-64-bit aggregate overflow";
            return false;
        }

        failure = null;
        return true;
    }

    private static bool TryAddClaim(
        IDictionary<ResourceAggregateKey, ResourceAggregate> aggregate,
        MortalWoundTreatmentResourceClaim claim,
        out string? failure)
    {
        var key = new ResourceAggregateKey(
            claim.Kind,
            claim.AuthorityRef,
            claim.Realm,
            claim.OwnerKind,
            claim.OwnerId);
        if (!aggregate.TryGetValue(key, out var current))
        {
            current = new ResourceAggregate(0, claim.AvailableQuantity);
        }
        else if (current.AvailableQuantity != claim.AvailableQuantity)
        {
            failure = $"{key}: witnessed availability changed from " +
                $"{current.AvailableQuantity} to {claim.AvailableQuantity}";
            return false;
        }

        var held = checked(current.HeldQuantity + claim.Quantity);
        if (claim.Quantity <= 0 ||
            claim.AvailableQuantity < 0 ||
            held > claim.AvailableQuantity)
        {
            failure = $"{key}: held={held}, available={claim.AvailableQuantity}";
            return false;
        }
        aggregate[key] = new ResourceAggregate(held, claim.AvailableQuantity);
        failure = null;
        return true;
    }

    private static bool CandidateAgrees(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentResourceReservationAuthority candidate) =>
        string.Equals(
            coordinates.CoordinatesFingerprint,
            candidate.CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            coordinates.AcceptedStateFingerprint,
            candidate.AcceptedStateFingerprint,
            StringComparison.Ordinal) &&
        ((candidate.Claims.Count == 0 &&
          string.Equals(
              candidate.ReservationDisposition,
              "not_required",
              StringComparison.Ordinal) &&
          candidate.ReservationId is null) ||
         (candidate.Claims.Count != 0 &&
          string.Equals(
              candidate.ReservationDisposition,
              "held",
              StringComparison.Ordinal) &&
          !string.IsNullOrWhiteSpace(candidate.ReservationId)));

    private static MortalWoundTreatmentResourceReservationResult Valid(
        MortalWoundTreatmentResourceReservationAuthority authority,
        MortalWoundTreatmentResourceReservationOwnership ownership) => new(
        true,
        Array.Empty<ValidationIssue>(),
        authority,
        ownership);

    private static MortalWoundTreatmentResourceLifecycleResult LifecycleValid(
        int changedCount) => new(
        true,
        Array.Empty<ValidationIssue>(),
        changedCount);

    private static MortalWoundTreatmentResourceLifecycleResult LifecycleInvalid(
        string code,
        string expected,
        string actual) => new(
        false,
        Invalid(code, expected, actual).Issues,
        0);

    internal static MortalWoundTreatmentResourceLifecycleResult LifecycleFailure(
        string code,
        string expected,
        string actual) => LifecycleInvalid(code, expected, actual);

    internal static MortalWoundTreatmentResourceReservationResult Invalid(
        string code,
        string expected,
        string actual) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(new[]
        {
            new ValidationIssue(
                IssuePath,
                IssueSeverity.Error,
                "The provisional Mortal wound-treatment resource agreement cannot be trusted.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                "Recreate resource preparation from the exact current accepted state and operation semantics.")
        }),
        null);

    private readonly record struct ResourceAggregateKey(
        string Kind,
        string AuthorityRef,
        string Realm,
        string OwnerKind,
        string OwnerId);

    private readonly record struct ResourceAggregate(
        long HeldQuantity,
        long AvailableQuantity);

    private sealed record FinalizedTombstone(
        string OperationKey,
        string RequestFingerprint,
        string? ReservationId,
        string AuthorityFingerprint,
        string PolicyFingerprint,
        string FinalizationFingerprint);

    private sealed record ReleasedTombstone(
        string RequestFingerprint,
        string AgreementFingerprint,
        string Reason);
}
