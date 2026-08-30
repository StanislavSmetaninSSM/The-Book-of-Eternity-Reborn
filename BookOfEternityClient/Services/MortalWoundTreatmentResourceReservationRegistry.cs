using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentResourceReservationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentResourceReservationAuthority? Authority);

internal sealed class MortalWoundTreatmentResourceReservationAgreement
{
    private readonly ReadOnlyCollection<string> _claimFingerprints;
    private readonly ReadOnlyCollection<string> _bindingFingerprints;
    private readonly ReadOnlyCollection<long> _availableQuantities;

    internal MortalWoundTreatmentResourceReservationAgreement(
        object registryCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority authority)
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

internal sealed class MortalWoundTreatmentResourceReservationRegistry
{
    private const string IssuePath = "treatmentAttempt.resources";
    private readonly Dictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement> _byOperationKey =
        new(StringComparer.Ordinal);

    internal MortalWoundTreatmentResourceReservationResult Reserve(
        object registryCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentResourceReservationAuthority candidate)
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
                ? Valid(existing.Authority)
                : Invalid(
                    "mortal_wound_treatment_resource_reservation_conflict",
                    "the exact prior resource agreement for this operation key",
                    coordinates.OperationKey);
        }

        if (!TryValidateAggregate(candidate, out var aggregateFailure))
        {
            return Invalid(
                "mortal_wound_treatment_resource_reservation_overbooked",
                "aggregate held quantity no greater than the witnessed availability",
                aggregateFailure!);
        }

        var agreement = new MortalWoundTreatmentResourceReservationAgreement(
            registryCapability,
            coordinates,
            mode,
            candidate);
        _byOperationKey.Add(coordinates.OperationKey, agreement);
        return Valid(candidate);
    }

    internal void InvalidateAll() => _byOperationKey.Clear();

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
        MortalWoundTreatmentResourceReservationAuthority authority) => new(
        true,
        Array.Empty<ValidationIssue>(),
        authority);

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
}
