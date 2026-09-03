using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentResourcePreparationResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentResourcePreparationResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResourceReservationAuthority? authority,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentResourceReservationOwnership? ownership)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Authority = authority;
        AcceptedState = acceptedState;
        Ownership = ownership;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentResourceReservationAuthority? Authority { get; }

    internal MortalWoundTreatmentAcceptedStateAuthority? AcceptedState { get; }
    internal MortalWoundTreatmentResourceReservationOwnership? Ownership { get; }

    internal static MortalWoundTreatmentResourcePreparationResult Valid(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentResourceReservationAuthority authority,
        MortalWoundTreatmentResourceReservationOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(ownership);
        return new MortalWoundTreatmentResourcePreparationResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority,
            acceptedState,
            ownership);
    }

    internal static MortalWoundTreatmentResourcePreparationResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        issues,
        null,
        null,
        null);
}

internal sealed class MortalWoundTreatmentResourceRehydrationResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentResourceRehydrationResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResourceReservationAuthority? authority)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Authority = authority;
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal MortalWoundTreatmentResourceReservationAuthority? Authority { get; }

    internal static MortalWoundTreatmentResourceRehydrationResult Valid(
        MortalWoundTreatmentResourceReservationAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return new MortalWoundTreatmentResourceRehydrationResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority);
    }

    internal static MortalWoundTreatmentResourceRehydrationResult Invalid(
        ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new MortalWoundTreatmentResourceRehydrationResult(
            false,
            new[] { issue },
            null);
    }

    internal static MortalWoundTreatmentResourceRehydrationResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(false, issues, null);
}

internal sealed class MortalWoundTreatmentResourceFinalizationResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentResourceFinalizationResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResourceFinalization? finalization)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        Finalization = finalization;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentResourceFinalization? Finalization { get; }

    internal static MortalWoundTreatmentResourceFinalizationResult Valid(
        MortalWoundTreatmentResourceFinalization finalization)
    {
        ArgumentNullException.ThrowIfNull(finalization);
        return new MortalWoundTreatmentResourceFinalizationResult(
            true,
            Array.Empty<ValidationIssue>(),
            finalization);
    }

    internal static MortalWoundTreatmentResourceFinalizationResult Invalid(
        ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new MortalWoundTreatmentResourceFinalizationResult(
            false,
            new[] { issue },
            null);
    }
}

internal sealed class MortalWoundTreatmentResourceFinalization
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentResourceConsumptionIntent>
        _consumptions;
    private readonly ReadOnlyCollection<string> _releasedClaimFingerprints;

    private MortalWoundTreatmentResourceFinalization(
        string disposition,
        string? reservationId,
        string requestFingerprint,
        string resultFingerprint,
        string resourceAuthorityFingerprint,
        string consumptionTrigger,
        IEnumerable<MortalWoundTreatmentResourceConsumptionIntent> consumptions,
        IEnumerable<string> releasedClaimFingerprints,
        string finalizationFingerprint)
    {
        Disposition = disposition;
        ReservationId = reservationId;
        RequestFingerprint = requestFingerprint;
        ResultFingerprint = resultFingerprint;
        ResourceAuthorityFingerprint = resourceAuthorityFingerprint;
        ConsumptionTrigger = consumptionTrigger;
        _consumptions = MortalWoundTreatmentShellDetachment.Freeze(consumptions);
        _releasedClaimFingerprints = MortalWoundTreatmentShellDetachment.Freeze(
            releasedClaimFingerprints);
        FinalizationFingerprint = finalizationFingerprint;
    }

    public string Disposition { get; }
    public string? ReservationId { get; }
    public string RequestFingerprint { get; }
    public string ResultFingerprint { get; }
    public string ResourceAuthorityFingerprint { get; }
    public string ConsumptionTrigger { get; }
    public IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> Consumptions =>
        _consumptions;
    public IReadOnlyList<string> ReleasedClaimFingerprints =>
        _releasedClaimFingerprints;
    public string FinalizationFingerprint { get; }

    internal static MortalWoundTreatmentResourceFinalization Create(
        string disposition,
        MortalWoundTreatmentResolution resolution,
        IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> consumptions,
        IReadOnlyList<string> releasedClaimFingerprints)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(consumptions);
        ArgumentNullException.ThrowIfNull(releasedClaimFingerprints);
        var resource = resolution.ResourceAuthority;
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_finalization",
            "1",
            disposition,
            resource.ReservationId,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resource.AuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(resource.Policy),
            resolution.ConsumptionTrigger,
            consumptions.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(consumptions.Select(static intent => intent.IntentFingerprint));
        fields.Add(releasedClaimFingerprints.Count.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(releasedClaimFingerprints);
        return new MortalWoundTreatmentResourceFinalization(
            disposition,
            resource.ReservationId,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resource.AuthorityFingerprint,
            resolution.ConsumptionTrigger,
            consumptions,
            releasedClaimFingerprints,
            WoundAcceptedTurnFingerprintWriter.Compute(fields));
    }
}

internal sealed class MortalWoundTreatmentResourceConsumptionIntent
{
    private MortalWoundTreatmentResourceConsumptionIntent(
        string scope,
        int? courseMilestoneOrdinal,
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        string ownerKind,
        string ownerId,
        int quantity,
        string claimFingerprint,
        string intentFingerprint)
    {
        Scope = scope;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        RequirementIndex = requirementIndex;
        Kind = kind;
        AuthorityRef = authorityRef;
        Realm = realm;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        Quantity = quantity;
        ClaimFingerprint = claimFingerprint;
        IntentFingerprint = intentFingerprint;
    }

    public string Scope { get; }
    public int? CourseMilestoneOrdinal { get; }
    public int RequirementIndex { get; }
    public string Kind { get; }
    public string AuthorityRef { get; }
    public string Realm { get; }
    public string OwnerKind { get; }
    public string OwnerId { get; }
    public int Quantity { get; }
    public string ClaimFingerprint { get; }
    public string IntentFingerprint { get; }

    internal static MortalWoundTreatmentResourceConsumptionIntent Create(
        MortalWoundTreatmentResourceClaim claim,
        int? courseMilestoneOrdinal)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var intentFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.resource_consumption_intent",
                "1",
                claim.Scope,
                courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
                claim.RequirementIndex.ToString(CultureInfo.InvariantCulture),
                claim.Kind,
                claim.AuthorityRef,
                claim.Realm,
                claim.OwnerKind,
                claim.OwnerId,
                claim.Quantity.ToString(CultureInfo.InvariantCulture),
                claim.ClaimFingerprint
            });
        return new MortalWoundTreatmentResourceConsumptionIntent(
            claim.Scope,
            courseMilestoneOrdinal,
            claim.RequirementIndex,
            claim.Kind,
            claim.AuthorityRef,
            claim.Realm,
            claim.OwnerKind,
            claim.OwnerId,
            claim.Quantity,
            claim.ClaimFingerprint,
            intentFingerprint);
    }
}

internal sealed class MortalWoundTreatmentResourceClaim
{
    private MortalWoundTreatmentResourceClaim(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        string ownerKind,
        string ownerId,
        int quantity,
        string successWitnessFingerprint,
        string claimFingerprint,
        long availableQuantity,
        string bindingFingerprint)
    {
        Scope = scope;
        RequirementIndex = requirementIndex;
        Kind = kind;
        AuthorityRef = authorityRef;
        Realm = realm;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        Quantity = quantity;
        SuccessWitnessFingerprint = successWitnessFingerprint;
        ClaimFingerprint = claimFingerprint;
        AvailableQuantity = availableQuantity;
        BindingFingerprint = bindingFingerprint;
    }

    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundTreatmentResourceClaim(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        string ownerKind,
        string ownerId,
        int quantity,
        string successWitnessFingerprint,
        string claimFingerprint)
        : this(
            scope,
            requirementIndex,
            kind,
            authorityRef,
            realm,
            ownerKind,
            ownerId,
            quantity,
            successWitnessFingerprint,
            claimFingerprint,
            availableQuantity: quantity,
            bindingFingerprint: string.Empty)
    {
    }

    public string Scope { get; }
    public int RequirementIndex { get; }
    public string Kind { get; }
    public string AuthorityRef { get; }
    public string Realm { get; }
    public string OwnerKind { get; }
    public string OwnerId { get; }
    public int Quantity { get; }
    public string SuccessWitnessFingerprint { get; }
    public string ClaimFingerprint { get; }

    internal long AvailableQuantity { get; }
    internal string BindingFingerprint { get; }

    internal static MortalWoundTreatmentResourceClaim Create(
        object mintCapability,
        MortalWoundTreatmentRequirementBinding binding,
        long availableQuantity)
    {
        MortalWoundTreatmentResourceComposer.RequireClaimMintCapability(mintCapability);
        ArgumentNullException.ThrowIfNull(binding);
        var row = binding.ResolvedRequirement;
        var witness = binding.SuccessWitness;
        if (row.RequestedQuantity is not { } quantity ||
            quantity <= 0 ||
            row.OwnerKind is null ||
            row.OwnerId is null)
        {
            throw new InvalidOperationException(
                "A resource claim requires one complete positive quantity binding.");
        }
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_claim",
            "1",
            witness.Scope,
            row.RequirementIndex.ToString(CultureInfo.InvariantCulture),
            row.Kind,
            row.AuthorityRef,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            quantity.ToString(CultureInfo.InvariantCulture),
            witness.WitnessFingerprint
        });
        return new MortalWoundTreatmentResourceClaim(
            witness.Scope,
            row.RequirementIndex,
            row.Kind,
            row.AuthorityRef,
            row.Realm,
            row.OwnerKind,
            row.OwnerId,
            quantity,
            witness.WitnessFingerprint,
            fingerprint,
            availableQuantity,
            binding.BindingFingerprint);
    }
}

internal sealed partial class MortalWoundTreatmentResourceReservationAuthority
{
    private readonly ReadOnlyCollection<MortalWoundTreatmentResourceClaim> _claims;

    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundTreatmentResourceReservationAuthority(
        string reservationDisposition,
        string? reservationId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string requirementAuthorityFingerprint,
        MortalWoundTreatmentResourcePolicy policy,
        IReadOnlyList<MortalWoundTreatmentResourceClaim> claims,
        string authorityFingerprint)
    {
        ReservationDisposition = reservationDisposition;
        ReservationId = reservationId;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        RouteFingerprint = routeFingerprint;
        CourseId = courseId;
        CourseMilestoneOrdinal = courseMilestoneOrdinal;
        CourseCoordinateFingerprint = courseCoordinateFingerprint;
        RequirementAuthorityFingerprint = requirementAuthorityFingerprint;
        Policy = ClonePolicy(policy);
        _claims = MortalWoundTreatmentShellDetachment.Freeze(claims);
        AuthorityFingerprint = authorityFingerprint;
    }

    public string ReservationDisposition { get; }
    public string? ReservationId { get; }
    public string CoordinatesFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string RouteFingerprint { get; }
    public string? CourseId { get; }
    public int? CourseMilestoneOrdinal { get; }
    public string? CourseCoordinateFingerprint { get; }
    public string RequirementAuthorityFingerprint { get; }
    public MortalWoundTreatmentResourcePolicy Policy { get; }
    public IReadOnlyList<MortalWoundTreatmentResourceClaim> Claims => _claims;
    public string AuthorityFingerprint { get; }

    internal static MortalWoundTreatmentResourceReservationAuthority CreateNotRequired(
        object mintCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string routeFingerprint,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourcePolicy policy)
    {
        MortalWoundTreatmentResourceComposer.RequireAuthorityMintCapability(mintCapability);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(requirementAuthority);
        ArgumentNullException.ThrowIfNull(policy);
        var detachedPolicy = ClonePolicy(policy);
        var authorityFingerprint = ComputeFingerprint(
            "not_required",
            reservationId: null,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            Array.Empty<MortalWoundTreatmentResourceClaim>());
        return new MortalWoundTreatmentResourceReservationAuthority(
            "not_required",
            null,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            Array.Empty<MortalWoundTreatmentResourceClaim>(),
            authorityFingerprint);
    }

    internal static MortalWoundTreatmentResourceReservationAuthority CreateHeld(
        object mintCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string routeFingerprint,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentResourcePolicy policy,
        IReadOnlyList<MortalWoundTreatmentResourceClaim> claims)
    {
        MortalWoundTreatmentResourceComposer.RequireAuthorityMintCapability(mintCapability);
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(requirementAuthority);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.Count == 0)
            throw new InvalidOperationException("A held authority requires at least one claim.");
        var detachedPolicy = ClonePolicy(policy);
        var reservationSemanticFingerprint = ComputeFingerprint(
            "held",
            reservationId: null,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            claims);
        var reservationId = "wound_treatment_resource_reservation_" +
            reservationSemanticFingerprint["sha256:".Length..];
        var authorityFingerprint = ComputeFingerprint(
            "held",
            reservationId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            claims);
        return new MortalWoundTreatmentResourceReservationAuthority(
            "held",
            reservationId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            routeFingerprint,
            requirementAuthority.CourseId,
            requirementAuthority.CourseMilestoneOrdinal,
            requirementAuthority.CourseCoordinateFingerprint,
            requirementAuthority.AuthorityFingerprint,
            detachedPolicy,
            claims,
            authorityFingerprint);
    }

    private static string ComputeFingerprint(
        string disposition,
        string? reservationId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string routeFingerprint,
        string? courseId,
        int? courseMilestoneOrdinal,
        string? courseCoordinateFingerprint,
        string requirementAuthorityFingerprint,
        MortalWoundTreatmentResourcePolicy policy,
        IReadOnlyList<MortalWoundTreatmentResourceClaim> claims)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_reservation_authority",
            "1",
            disposition,
            reservationId,
            coordinatesFingerprint,
            acceptedStateFingerprint,
            routeFingerprint,
            courseId,
            courseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            courseCoordinateFingerprint,
            requirementAuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(policy),
            claims.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(claims.Select(static claim => claim.ClaimFingerprint));
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalWoundTreatmentResourcePolicy ClonePolicy(
        MortalWoundTreatmentResourcePolicy policy) => new(
        policy.ReserveBeforeResolution,
        policy.ConsumeOn.ToImmutableArray(),
        policy.RefundOn.ToImmutableArray(),
        policy.Mutations.Select(static mutation => mutation with { }).ToImmutableArray());
}

internal static class MortalWoundTreatmentResourceComposer
{
    private const string IssuePath = "treatmentAttempt.resources";
    private static readonly object ClaimMintCapability = new();
    private static readonly object AuthorityMintCapability = new();
    private static readonly object ResourceReservationCapability = new();

    internal static bool IsResourceReservationCapability(object capability) =>
        ReferenceEquals(capability, ResourceReservationCapability);

    internal static MortalWoundTreatmentResourceLifecycleResult
        ConfirmPersistedTreatmentResources(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(requests);
        return AcceptedTurnAuthorityRegistry
            .ConfirmPersistedMortalWoundTreatmentResources(
                fileSystem,
                writeLease,
                ResourceReservationCapability,
                requests);
    }

    internal static MortalWoundTreatmentResourceLifecycleResult ReleaseTreatmentResources(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(requests);
        return AcceptedTurnAuthorityRegistry.ReleaseMortalWoundTreatmentResources(
            fileSystem,
            writeLease,
            ResourceReservationCapability,
            requests,
            reason);
    }

    internal static void RequireClaimMintCapability(object? capability)
    {
        if (!ReferenceEquals(capability, ClaimMintCapability))
            throw new InvalidOperationException("Only the resource composer may mint claims.");
    }

    internal static void RequireAuthorityMintCapability(object? capability)
    {
        if (!ReferenceEquals(capability, AuthorityMintCapability))
            throw new InvalidOperationException(
                "Only the resource composer may mint reservation authority.");
    }

    internal static MortalWoundTreatmentResourcePreparationResult PrepareProcedure(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundProcedureCheckAuthority modeAuthority) => Prepare(
        "procedure",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    internal static MortalWoundTreatmentResourcePreparationResult PrepareCourse(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundCourseModeAuthority modeAuthority) => Prepare(
        "course",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    internal static MortalWoundTreatmentResourcePreparationResult PrepareGuaranteed(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentCapabilityProof modeAuthority) => Prepare(
        "guaranteed",
        acceptedState,
        coordinates,
        before,
        requirementAuthority,
        modeAuthority);

    internal static bool RollbackNew(
        MortalWoundTreatmentResourcePreparationResult? preparation)
    {
        if (preparation is not
            {
                IsValid: true,
                Authority: not null,
                AcceptedState: not null,
                Ownership: not null
            } ||
            preparation.Issues.Count != 0)
        {
            return false;
        }

        return preparation.AcceptedState.RollbackNewTreatmentResources(
            ResourceReservationCapability,
            preparation.Ownership,
            preparation.Authority);
    }

    internal static bool RollbackNewProcedureAndResources(
        MortalWoundProcedureCheckAuthority? procedure,
        MortalWoundTreatmentResourcePreparationResult? preparation)
    {
        if (procedure is null ||
            preparation is not
            {
                IsValid: true,
                Authority: not null,
                AcceptedState: not null,
                Ownership: not null
            } ||
            preparation.Issues.Count != 0)
        {
            return false;
        }

        return procedure.RollbackNewProvisionalReservationsWithResources(
            preparation.AcceptedState,
            ResourceReservationCapability,
            preparation.Ownership,
            preparation.Authority);
    }

    internal static MortalWoundTreatmentResourceRehydrationResult
        RehydratePersistedAuthority(MortalWoundTreatmentAttemptRequest? request)
    {
        if (request is null ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request))
        {
            return InvalidRehydration(
                "mortal_wound_treatment_resource_recovery_request_invalid",
                "one complete independently valid detached request",
                request is null ? "missing request" : "request seal mismatch");
        }

        try
        {
            var issues = new List<ValidationIssue>();
            IReadOnlyList<MortalWoundTreatmentResourceClaim>? claims;
            if (request.RequirementAuthority.InterruptionReason is not null)
            {
                claims = Array.Empty<MortalWoundTreatmentResourceClaim>();
            }
            else if (!TryBuildClaims(request.RequirementAuthority, issues, out claims))
            {
                return MortalWoundTreatmentResourceRehydrationResult.Invalid(issues);
            }

            var persisted = request.ResourceAuthority;
            var rebuilt = claims!.Count == 0
                ? MortalWoundTreatmentResourceReservationAuthority.CreateNotRequired(
                    AuthorityMintCapability,
                    request.Coordinates,
                    request.RequirementAuthority.RouteFingerprint,
                    request.RequirementAuthority,
                    persisted.Policy)
                : MortalWoundTreatmentResourceReservationAuthority.CreateHeld(
                    AuthorityMintCapability,
                    request.Coordinates,
                    request.RequirementAuthority.RouteFingerprint,
                    request.RequirementAuthority,
                    persisted.Policy,
                    claims);
            if (!HasExactResourceAuthorityAgreement(persisted, rebuilt))
            {
                return InvalidRehydration(
                    "mortal_wound_treatment_resource_recovery_authority_mismatch",
                    "the exact persisted disposition, identifier, public claims, policy, and authority seal",
                    persisted.AuthorityFingerprint);
            }

            return MortalWoundTreatmentResourceRehydrationResult.Valid(rebuilt);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return InvalidRehydration(
                "mortal_wound_treatment_resource_recovery_invalid",
                "one freshly rebuilt resource authority from complete requirement evidence",
                exception.GetType().Name);
        }
    }

    internal static MortalWoundTreatmentResourceFinalizationResult Finalize(
        MortalWoundTreatmentResolution resolution)
    {
        try
        {
            if (resolution is null)
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_resolution_missing",
                    "one complete sealed terminal treatment resolution",
                    "missing resolution");
            }
            if (!string.Equals(
                    resolution.AttemptDisposition,
                    "AcceptedTerminal",
                    StringComparison.Ordinal))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_disposition_invalid",
                    "AcceptedTerminal",
                    resolution.AttemptDisposition);
            }

            var request = resolution.RequestAuthority;
            if (request is null ||
                !MortalWoundTreatmentDetachedSealValidator.IsValid(request))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_request_invalid",
                    "one independently valid complete detached request",
                    request is null ? "missing request" : "request seal mismatch");
            }
            if (!HasExactResolutionRequestAgreement(resolution, request))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_request_agreement_invalid",
                    "exact outer/request mode, coordinates, route, course, and authority seals",
                    resolution.RequestFingerprint);
            }
            if (!MortalWoundTreatmentResolution.TryRecomputeModeEvidenceFingerprint(
                    resolution,
                    out var modeEvidenceFingerprint) ||
                modeEvidenceFingerprint is null)
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_mode_evidence_invalid",
                    $"one independently recomputable {resolution.Mode} mode-evidence graph",
                    resolution.ModeEvidence?.GetType().Name ?? "missing evidence");
            }
            var expectedResolutionFingerprint =
                MortalWoundTreatmentResolution.ComputeResolutionAuthorityFingerprint(
                    resolution.RequestFingerprint,
                    resolution.Mode,
                    resolution.AttemptDisposition,
                    resolution.ResultCategory,
                    resolution.SelectedOutcomeIndex,
                    resolution.Interruption,
                    resolution.ConsumptionTrigger,
                    resolution.CourseId,
                    resolution.CourseMilestoneOrdinal,
                    resolution.CourseDisposition,
                    resolution.RouteFingerprint,
                    resolution.RouteCompletion,
                    resolution.CriticalReactionIntent?.IntentFingerprint,
                    modeEvidenceFingerprint);
            if (!string.Equals(
                    resolution.ResolutionAuthorityFingerprint,
                    expectedResolutionFingerprint,
                    StringComparison.Ordinal))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_resolution_authority_invalid",
                    expectedResolutionFingerprint,
                    resolution.ResolutionAuthorityFingerprint);
            }
            var expectedResultFingerprint =
                MortalWoundTreatmentResolution.ComputeResultFingerprint(
                    expectedResolutionFingerprint,
                    resolution.DeclaredResult);
            if (!string.Equals(
                    resolution.ResultFingerprint,
                    expectedResultFingerprint,
                    StringComparison.Ordinal))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_result_invalid",
                    expectedResultFingerprint,
                    resolution.ResultFingerprint);
            }

            var policy = resolution.ResourceAuthority.Policy;
            var expectedTrigger = !resolution.Interruption &&
                                  policy.ConsumeOn.Contains(
                                      resolution.ResultCategory,
                                      StringComparer.Ordinal)
                ? resolution.ResultCategory
                : "none";
            if (!string.Equals(
                    resolution.ConsumptionTrigger,
                    expectedTrigger,
                    StringComparison.Ordinal))
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_trigger_invalid",
                    expectedTrigger,
                    resolution.ConsumptionTrigger);
            }

            var resource = resolution.ResourceAuthority;
            if (!TrySelectCurrentClaims(
                    resolution,
                    expectedTrigger,
                    out var selectedClaimFingerprints,
                    out var selectorIssue))
            {
                return MortalWoundTreatmentResourceFinalizationResult.Invalid(
                    selectorIssue!);
            }

            var claimsByFingerprint = resource.Claims.ToDictionary(
                static claim => claim.ClaimFingerprint,
                StringComparer.Ordinal);
            var consumptions = new List<MortalWoundTreatmentResourceConsumptionIntent>();
            foreach (var claimFingerprint in selectedClaimFingerprints!)
            {
                var claim = claimsByFingerprint[claimFingerprint];
                consumptions.Add(MortalWoundTreatmentResourceConsumptionIntent.Create(
                    claim,
                    claim.Scope == "course_milestone"
                        ? resolution.CourseMilestoneOrdinal
                        : null));
            }
            var selectedClaimSet = selectedClaimFingerprints.ToHashSet(
                StringComparer.Ordinal);
            var released = resource.Claims
                .Where(claim => !selectedClaimSet.Contains(claim.ClaimFingerprint))
                .Select(static claim => claim.ClaimFingerprint)
                .ToList();

            if (resource.ReservationDisposition == "not_required")
            {
                if (resource.ReservationId is not null ||
                    resource.Claims.Count != 0 ||
                    consumptions.Count != 0 ||
                    released.Count != 0)
                {
                    return InvalidFinalization(
                        "mortal_wound_treatment_resource_finalization_reservation_invalid",
                        "not_required with a null reservation and empty partition",
                        resource.AuthorityFingerprint);
                }
                return MortalWoundTreatmentResourceFinalizationResult.Valid(
                    MortalWoundTreatmentResourceFinalization.Create(
                        "not_required",
                        resolution,
                        consumptions,
                        released));
            }

            if (resource.ReservationDisposition != "held" ||
                string.IsNullOrWhiteSpace(resource.ReservationId) ||
                resource.Claims.Count == 0 ||
                consumptions.Count + released.Count != resource.Claims.Count ||
                selectedClaimFingerprints!.Count != consumptions.Count)
            {
                return InvalidFinalization(
                    "mortal_wound_treatment_resource_finalization_partition_invalid",
                    "one disjoint complete ordered partition of every held claim",
                    resource.AuthorityFingerprint);
            }

            return MortalWoundTreatmentResourceFinalizationResult.Valid(
                MortalWoundTreatmentResourceFinalization.Create(
                    consumptions.Count == 0 ? "release_only" : "consume",
                    resolution,
                    consumptions,
                    released));
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException or
                                           NotSupportedException or
                                           NullReferenceException or
                                           OverflowException or
                                           IndexOutOfRangeException)
        {
            return InvalidFinalization(
                "mortal_wound_treatment_resource_finalization_graph_invalid",
                "one complete independently recomputable sealed resolution graph",
                exception.GetType().Name);
        }
    }

    private static bool HasExactResolutionRequestAgreement(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentAttemptRequest request)
    {
        var requirement = request.RequirementAuthority;
        var resource = request.ResourceAuthority;
        var isCourse = string.Equals(request.Mode, "course", StringComparison.Ordinal);
        return string.Equals(resolution.Mode, request.Mode, StringComparison.Ordinal) &&
               string.Equals(
                   resolution.Coordinates.CoordinatesFingerprint,
                   request.Coordinates.CoordinatesFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   resolution.RequestFingerprint,
                   request.RequestFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   resolution.RequirementAuthority.AuthorityFingerprint,
                   requirement.AuthorityFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   resolution.ResourceAuthority.AuthorityFingerprint,
                   resource.AuthorityFingerprint,
                   StringComparison.Ordinal) &&
               HasExactResourceAuthorityAgreement(
                   resolution.ResourceAuthority,
                   resource) &&
               string.Equals(
                   resolution.RouteFingerprint,
                   requirement.RouteFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   resolution.RouteFingerprint,
                   resource.RouteFingerprint,
                   StringComparison.Ordinal) &&
               resolution.CourseMilestoneOrdinal == request.MilestoneOrdinal &&
               resolution.CourseMilestoneOrdinal == requirement.CourseMilestoneOrdinal &&
               resolution.CourseMilestoneOrdinal == resource.CourseMilestoneOrdinal &&
               string.Equals(resolution.CourseId, requirement.CourseId,
                   StringComparison.Ordinal) &&
               string.Equals(resolution.CourseId, resource.CourseId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   requirement.CourseCoordinateFingerprint,
                   resource.CourseCoordinateFingerprint,
                   StringComparison.Ordinal) &&
               (isCourse
                   ? resolution.CourseMilestoneOrdinal is > 0 &&
                     !string.IsNullOrWhiteSpace(resolution.CourseId) &&
                     !string.IsNullOrWhiteSpace(requirement.CourseCoordinateFingerprint)
                   : resolution.CourseMilestoneOrdinal is null &&
                     resolution.CourseId is null &&
                     resolution.CourseDisposition is null &&
                     requirement.CourseCoordinateFingerprint is null);
    }

    private static bool HasExactResourceAuthorityAgreement(
        MortalWoundTreatmentResourceReservationAuthority actual,
        MortalWoundTreatmentResourceReservationAuthority expected)
    {
        if (!string.Equals(actual.ReservationDisposition,
                expected.ReservationDisposition, StringComparison.Ordinal) ||
            !string.Equals(actual.ReservationId, expected.ReservationId,
                StringComparison.Ordinal) ||
            !string.Equals(actual.CoordinatesFingerprint,
                expected.CoordinatesFingerprint, StringComparison.Ordinal) ||
            !string.Equals(actual.AcceptedStateFingerprint,
                expected.AcceptedStateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(actual.RouteFingerprint, expected.RouteFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(actual.CourseId, expected.CourseId,
                StringComparison.Ordinal) ||
            actual.CourseMilestoneOrdinal != expected.CourseMilestoneOrdinal ||
            !string.Equals(actual.CourseCoordinateFingerprint,
                expected.CourseCoordinateFingerprint, StringComparison.Ordinal) ||
            !string.Equals(actual.RequirementAuthorityFingerprint,
                expected.RequirementAuthorityFingerprint, StringComparison.Ordinal) ||
            !string.Equals(actual.AuthorityFingerprint, expected.AuthorityFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(ComputePolicyFingerprint(actual.Policy),
                ComputePolicyFingerprint(expected.Policy), StringComparison.Ordinal) ||
            actual.Claims.Count != expected.Claims.Count)
        {
            return false;
        }

        for (var index = 0; index < actual.Claims.Count; index++)
        {
            var left = actual.Claims[index];
            var right = expected.Claims[index];
            if (!string.Equals(left.Scope, right.Scope, StringComparison.Ordinal) ||
                left.RequirementIndex != right.RequirementIndex ||
                !string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) ||
                !string.Equals(left.AuthorityRef, right.AuthorityRef,
                    StringComparison.Ordinal) ||
                !string.Equals(left.Realm, right.Realm, StringComparison.Ordinal) ||
                !string.Equals(left.OwnerKind, right.OwnerKind,
                    StringComparison.Ordinal) ||
                !string.Equals(left.OwnerId, right.OwnerId, StringComparison.Ordinal) ||
                left.Quantity != right.Quantity ||
                !string.Equals(left.SuccessWitnessFingerprint,
                    right.SuccessWitnessFingerprint, StringComparison.Ordinal) ||
                !string.Equals(left.ClaimFingerprint, right.ClaimFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TrySelectCurrentClaims(
        MortalWoundTreatmentResolution resolution,
        string trigger,
        out IReadOnlyList<string>? selectedClaimFingerprints,
        out ValidationIssue? issue)
    {
        var selected = new List<string>();
        var selectedSet = new HashSet<string>(StringComparer.Ordinal);
        selectedClaimFingerprints = selected;
        issue = null;
        if (trigger == "none")
            return true;

        var claims = resolution.ResourceAuthority.Claims;
        foreach (var selector in resolution.ResourceAuthority.Policy.Mutations)
        {
            var isCurrent = selector.Scope switch
            {
                "common" when selector.MilestoneOrdinal is null => true,
                "course_milestone" when resolution.Mode == "course" &&
                    selector.MilestoneOrdinal == resolution.CourseMilestoneOrdinal => true,
                "course_milestone" when selector.MilestoneOrdinal is > 0 => false,
                _ => false
            };
            if (!isCurrent)
                continue;

            var matches = claims.Where(claim =>
                    string.Equals(claim.Scope, selector.Scope, StringComparison.Ordinal) &&
                    claim.RequirementIndex == selector.RequirementIndex)
                .ToArray();
            if (matches.Length != 1 ||
                matches[0].Kind is not ("item_quantity" or "resource_quantity") ||
                matches[0].Quantity <= 0 ||
                !selectedSet.Add(matches[0].ClaimFingerprint))
            {
                issue = CreateFinalizationIssue(
                    "mortal_wound_treatment_resource_finalization_selector_invalid",
                    "one unique current positive quantity claim for every active selector",
                    $"{selector.Scope}:{selector.MilestoneOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "null"}:{selector.RequirementIndex.ToString(CultureInfo.InvariantCulture)}");
                selectedClaimFingerprints = null;
                return false;
            }
            selected.Add(matches[0].ClaimFingerprint);
        }
        return true;
    }

    private static MortalWoundTreatmentResourceFinalizationResult InvalidFinalization(
        string code,
        string expected,
        string actual) => MortalWoundTreatmentResourceFinalizationResult.Invalid(
        CreateFinalizationIssue(code, expected, actual));

    private static ValidationIssue CreateFinalizationIssue(
        string code,
        string expected,
        string actual) => new(
        IssuePath + ".finalization",
        IssueSeverity.Error,
        "The Mortal wound-treatment resource finalization cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Discard the untrusted result and resolve the complete sealed treatment request again.");

    private static MortalWoundTreatmentResourceRehydrationResult InvalidRehydration(
        string code,
        string expected,
        string actual) => MortalWoundTreatmentResourceRehydrationResult.Invalid(
        new ValidationIssue(
            IssuePath + ".recovery",
            IssueSeverity.Error,
            "The persisted Mortal wound-treatment resource authority cannot be restored.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
            "Discard the untrusted persisted request and prepare it again from current accepted state."));

    private static MortalWoundTreatmentResourcePreparationResult Prepare(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        MortalWoundTreatmentRequirementAuthorityBundle? requirementAuthority,
        MortalWoundTreatmentModeAuthority? modeAuthority)
    {
        var issues = new List<ValidationIssue>();
        var routeIsCurrent = TryResolveCurrentRoute(
                mode,
                acceptedState,
                coordinates,
                before,
                issues,
                out var route,
                out var routeFingerprint);
        if (requirementAuthority is null)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_authority_invalid",
                "one complete matching current requirement bundle",
                "missing authority");
        }
        if (modeAuthority is null)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_mode_authority_invalid",
                $"one exact current {mode} mode authority",
                "missing authority");
        }
        if (!routeIsCurrent ||
            acceptedState is null ||
            coordinates is null ||
            before is null ||
            requirementAuthority is null ||
            modeAuthority is null)
        {
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        if (!TryValidateRequirementAuthority(
                route!,
                coordinates,
                requirementAuthority,
                routeFingerprint!,
                issues) ||
            !TryValidateModeAuthority(
                mode,
                acceptedState,
                coordinates,
                before,
                route!,
                routeFingerprint!,
                requirementAuthority,
                modeAuthority,
                issues) ||
            !TryValidatePolicy(route!, issues))
        {
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        if (mode == "course" &&
            string.Equals(
                requirementAuthority.CourseRequirementStatus,
                "Unsatisfied",
                StringComparison.Ordinal) &&
            requirementAuthority.InterruptionReason is null)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirements_unsatisfied",
                "one satisfied current first-course milestone",
                "trusted unsatisfied requirement bundle");
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        IReadOnlyList<MortalWoundTreatmentResourceClaim>? claims;
        if (requirementAuthority.InterruptionReason is not null)
        {
            claims = Array.Empty<MortalWoundTreatmentResourceClaim>();
        }
        else if (!TryBuildClaims(requirementAuthority, issues, out claims))
        {
            return MortalWoundTreatmentResourcePreparationResult.Invalid(issues);
        }

        var candidate = claims!.Count == 0
            ? MortalWoundTreatmentResourceReservationAuthority.CreateNotRequired(
                AuthorityMintCapability,
                coordinates,
                routeFingerprint!,
                requirementAuthority,
                route!.ResourcePolicy)
            : MortalWoundTreatmentResourceReservationAuthority.CreateHeld(
                AuthorityMintCapability,
                coordinates,
                routeFingerprint!,
                requirementAuthority,
                route!.ResourcePolicy,
                claims);
        var reservation = acceptedState.ReserveTreatmentResources(
            ResourceReservationCapability,
            coordinates,
            mode,
            modeAuthority,
            candidate);
        return reservation.IsValid &&
               reservation.Authority is not null &&
               reservation.Ownership is not null
            ? MortalWoundTreatmentResourcePreparationResult.Valid(
                acceptedState,
                reservation.Authority,
                reservation.Ownership)
            : MortalWoundTreatmentResourcePreparationResult.Invalid(
                reservation.Issues);
    }

    private static bool TryBuildClaims(
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        ICollection<ValidationIssue> issues,
        out IReadOnlyList<MortalWoundTreatmentResourceClaim>? claims)
    {
        var result = new List<MortalWoundTreatmentResourceClaim>();
        try
        {
            foreach (var scope in requirementAuthority.Scopes)
            {
                if (!string.Equals(scope.Status, "Satisfied", StringComparison.Ordinal) ||
                    scope.FailureWitnesses.Count != 0)
                {
                    AddIssue(
                        issues,
                        "mortal_wound_treatment_resource_requirements_unsatisfied",
                        "only satisfied current scopes may produce claims",
                        scope.Status);
                    claims = null;
                    return false;
                }
                foreach (var binding in scope.Bindings)
                {
                    var row = binding.ResolvedRequirement;
                    var witness = binding.SuccessWitness;
                    if (row.Kind is not ("item_quantity" or "resource_quantity"))
                        continue;
                    if (!TryValidateQuantityBinding(
                            scope,
                            binding,
                            out var availableQuantity))
                    {
                        AddIssue(
                            issues,
                            "mortal_wound_treatment_resource_quantity_witness_invalid",
                            "one exact current positive quantity success witness",
                            binding.BindingFingerprint);
                        claims = null;
                        return false;
                    }
                    result.Add(MortalWoundTreatmentResourceClaim.Create(
                        ClaimMintCapability,
                        binding,
                        availableQuantity));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_claim_invalid",
                "one complete ordered immutable claim set",
                exception.GetType().Name);
            claims = null;
            return false;
        }

        claims = new ReadOnlyCollection<MortalWoundTreatmentResourceClaim>(
            result.ToArray());
        return true;
    }

    private static bool TryValidateQuantityBinding(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        MortalWoundTreatmentRequirementBinding binding,
        out long availableQuantity)
    {
        availableQuantity = 0;
        var row = binding.ResolvedRequirement;
        var witness = binding.SuccessWitness;
        if (row.RequestedQuantity is not { } requested ||
            requested <= 0 ||
            row.OwnerKind is null ||
            row.OwnerId is null ||
            !string.Equals(witness.Scope, scope.Scope, StringComparison.Ordinal) ||
            witness.RequirementIndex != binding.RequirementIndex ||
            !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
            !string.Equals(witness.AuthorityRef, row.AuthorityRef, StringComparison.Ordinal) ||
            !string.Equals(witness.Realm, row.Realm, StringComparison.Ordinal) ||
            !string.Equals(witness.OwnerKind, row.OwnerKind, StringComparison.Ordinal) ||
            !string.Equals(witness.OwnerId, row.OwnerId, StringComparison.Ordinal))
        {
            return false;
        }

        switch (witness.Evidence)
        {
            case MortalWoundItemQuantityRequirementEvidence item
                when row.Kind == "item_quantity" &&
                     item.RequestedQuantity == requested &&
                     item.Count >= 0 &&
                     item.AvailableCount >= requested &&
                     item.AvailableCount <= item.Count &&
                     item.CumulativeRequestedQuantity >= requested &&
                     item.CumulativeRequestedQuantity <= item.AvailableCount &&
                     item.ReservationState == "available" &&
                     item.Lifecycle == "active" &&
                     item.Active:
                availableQuantity = item.AvailableCount;
                return true;
            case MortalWoundResourceQuantityRequirementEvidence resource
                when row.Kind == "resource_quantity" &&
                     resource.RequestedQuantity == requested &&
                     resource.CurrentValue >= 0 &&
                     resource.AvailableValue >= requested &&
                     resource.AvailableValue <= resource.CurrentValue &&
                     resource.CumulativeRequestedQuantity >= requested &&
                     resource.CumulativeRequestedQuantity <= resource.AvailableValue &&
                     resource.ReservationState == "available" &&
                     resource.Lifecycle == "active" &&
                     resource.Active:
                availableQuantity = resource.AvailableValue;
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveCurrentRoute(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        WoundMaterializationEnvelope? before,
        ICollection<ValidationIssue> issues,
        out MortalWoundTreatmentRouteDefinition? route,
        out string? routeFingerprint)
    {
        route = null;
        routeFingerprint = null;
        if (acceptedState is null ||
            coordinates is null ||
            before is null ||
            !acceptedState.HasCurrentAdmissionAuthority() ||
            !coordinates.MatchesAcceptedState(acceptedState) ||
            !acceptedState.MatchesCurrentWound(before))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_authority_invalid",
                "one current accepted state with exact coordinates and wound",
                "missing, stale, foreign, or mismatched authority");
            return false;
        }

        var matches = acceptedState.TreatmentDefinition.Routes.Where(candidate =>
                string.Equals(candidate.RouteId, coordinates.RouteId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            !string.Equals(matches[0].Mode, mode, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_route_invalid",
                $"one exact current {mode} route",
                coordinates.RouteId);
            return false;
        }

        try
        {
            routeFingerprint = MortalWoundTreatmentRouteFingerprint.Compute(
                before,
                coordinates.RouteId);
            route = matches[0];
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_route_invalid",
                "one canonical route with a recomputable fingerprint",
                exception.GetType().Name);
            return false;
        }
    }

    private static bool TryValidateRequirementAuthority(
        MortalWoundTreatmentRouteDefinition route,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentRequirementAuthorityBundle authority,
        string routeFingerprint,
        ICollection<ValidationIssue> issues)
    {
        var isCourse = string.Equals(route.Mode, "course", StringComparison.Ordinal);
        if (!string.Equals(authority.Mode, route.Mode, StringComparison.Ordinal) ||
            !string.Equals(
                authority.ContextFingerprint,
                coordinates.ContextFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.AcceptedStateFingerprint,
                coordinates.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(authority.RouteFingerprint, routeFingerprint, StringComparison.Ordinal) ||
            (!isCourse &&
             (authority.CourseId is not null ||
              authority.CourseMilestoneOrdinal is not null ||
              authority.CourseCoordinateFingerprint is not null ||
              authority.CourseRequirementStatus is not null ||
              authority.InterruptionReason is not null)))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_authority_invalid",
                "the complete matching current requirement bundle",
                authority.AuthorityFingerprint);
            return false;
        }

        var expectedScopes = isCourse ? 2 : 1;
        if (authority.Scopes.Count != expectedScopes ||
            !TryValidateScope(
                authority.Scopes[0],
                "common",
                null,
                route.Requirements,
                issues))
        {
            return false;
        }
        if (isCourse)
        {
            if (route is not MortalWoundCourseRouteDefinition courseRoute ||
                authority.CourseMilestoneOrdinal is not { } ordinal)
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_authority_invalid",
                    "one complete course coordinate and current milestone scope",
                    authority.AuthorityFingerprint);
                return false;
            }
            var milestones = courseRoute.Milestones.Where(milestone =>
                milestone.Ordinal == ordinal).ToArray();
            if (milestones.Length != 1 ||
                !TryValidateScope(
                    authority.Scopes[1],
                    "course_milestone",
                    ordinal,
                    milestones[0].Requirements,
                    issues))
            {
                return false;
            }
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_bundle",
            "1",
            authority.Mode,
            authority.ContextFingerprint,
            authority.AcceptedStateFingerprint,
            authority.RouteFingerprint,
            authority.CourseId,
            authority.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            authority.CourseCoordinateFingerprint,
            authority.CourseRequirementStatus,
            authority.InterruptionReason,
            authority.Scopes.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(authority.Scopes.Select(static scope => scope.AuthorityFingerprint));
        if (!string.Equals(
                authority.AuthorityFingerprint,
                WoundAcceptedTurnFingerprintWriter.Compute(fields),
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_authority_invalid",
                "one recomputable complete requirement bundle seal",
                authority.AuthorityFingerprint);
            return false;
        }
        return true;
    }

    private static bool TryValidateScope(
        MortalWoundTreatmentRequirementScopeAuthority scope,
        string expectedScope,
        int? expectedMilestoneOrdinal,
        IReadOnlyList<MortalWoundTreatmentRequirement> requirements,
        ICollection<ValidationIssue> issues)
    {
        var expectedIndices = Enumerable.Range(0, requirements.Count).ToArray();
        var actualIndices = scope.Bindings.Select(static binding => binding.RequirementIndex)
            .Concat(scope.FailureWitnesses.Select(static failure => failure.RequirementIndex))
            .OrderBy(static index => index)
            .ToArray();
        var expectedStatus = scope.FailureWitnesses.Count == 0
            ? "Satisfied"
            : "Unsatisfied";
        if (!string.Equals(scope.Scope, expectedScope, StringComparison.Ordinal) ||
            scope.CourseMilestoneOrdinal != expectedMilestoneOrdinal ||
            !string.Equals(scope.Status, expectedStatus, StringComparison.Ordinal) ||
            !actualIndices.SequenceEqual(expectedIndices))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_scope_invalid",
                $"one complete ordered {expectedScope} scope",
                scope.AuthorityFingerprint);
            return false;
        }

        foreach (var binding in scope.Bindings)
        {
            var row = binding.ResolvedRequirement;
            var witness = binding.SuccessWitness;
            var requirement = requirements[binding.RequirementIndex];
            var bindingFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                new string?[]
                {
                    "book_of_eternity.mortal_wound_treatment.requirement_binding",
                    "1",
                    witness.Scope,
                    row.RequirementIndex.ToString(CultureInfo.InvariantCulture),
                    row.AuthorityFingerprint,
                    witness.WitnessFingerprint
                });
            if (row.RequirementIndex != binding.RequirementIndex ||
                witness.RequirementIndex != binding.RequirementIndex ||
                !string.Equals(row.Kind, requirement.Kind, StringComparison.Ordinal) ||
                !string.Equals(witness.Scope, expectedScope, StringComparison.Ordinal) ||
                !string.Equals(witness.Kind, row.Kind, StringComparison.Ordinal) ||
                !string.Equals(witness.AuthorityRef, row.AuthorityRef, StringComparison.Ordinal) ||
                !string.Equals(binding.BindingFingerprint, bindingFingerprint, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_binding_invalid",
                    "one exact sealed current success binding",
                    binding.BindingFingerprint);
                return false;
            }
        }
        foreach (var failure in scope.FailureWitnesses)
        {
            if (!string.Equals(
                    failure.Kind,
                    requirements[failure.RequirementIndex].Kind,
                    StringComparison.Ordinal) ||
                !string.Equals(failure.Scope, expectedScope, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    "mortal_wound_treatment_resource_requirement_failure_invalid",
                    "one exact typed current failure witness",
                    failure.WitnessFingerprint);
                return false;
            }
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_scope",
            "1",
            scope.Scope,
            scope.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            scope.Status,
            scope.Bindings.Count.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(scope.Bindings.Select(static binding => binding.BindingFingerprint));
        fields.Add(scope.FailureWitnesses.Count.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(scope.FailureWitnesses.Select(static failure => failure.WitnessFingerprint));
        if (!string.Equals(
                scope.AuthorityFingerprint,
                WoundAcceptedTurnFingerprintWriter.Compute(fields),
                StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_requirement_scope_invalid",
                "one recomputable current requirement scope seal",
                scope.AuthorityFingerprint);
            return false;
        }
        return true;
    }

    private static bool TryValidateModeAuthority(
        string mode,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentRouteDefinition route,
        string routeFingerprint,
        MortalWoundTreatmentRequirementAuthorityBundle requirementAuthority,
        MortalWoundTreatmentModeAuthority modeAuthority,
        ICollection<ValidationIssue> issues)
    {
        var valid = modeAuthority switch
        {
            MortalWoundProcedureCheckAuthority procedure
                when mode == "procedure" && route is MortalWoundProcedureRouteDefinition =>
                string.Equals(
                    procedure.CoordinatesFingerprint,
                    coordinates.CoordinatesFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    procedure.AcceptedStateFingerprint,
                    coordinates.AcceptedStateFingerprint,
                    StringComparison.Ordinal) &&
                string.Equals(
                    procedure.RequirementAuthorityFingerprint,
                    requirementAuthority.AuthorityFingerprint,
                    StringComparison.Ordinal),
            MortalWoundCourseModeAuthority course
                when mode == "course" && route is MortalWoundCourseRouteDefinition =>
                course.GameTimeAuthority.Matches(acceptedState, coordinates) &&
                course.MilestoneOrdinal > 0 &&
                course.WindowDisposition is "ready" or "deadline_exceeded" &&
                string.Equals(course.CoordinatesFingerprint, coordinates.CoordinatesFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.AcceptedStateFingerprint, coordinates.AcceptedStateFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.CourseId, requirementAuthority.CourseId, StringComparison.Ordinal) &&
                course.MilestoneOrdinal == requirementAuthority.CourseMilestoneOrdinal &&
                string.Equals(course.CourseCoordinateFingerprint, requirementAuthority.CourseCoordinateFingerprint, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.CourseId, course.CourseId, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.RouteId, coordinates.RouteId, StringComparison.Ordinal) &&
                string.Equals(course.CourseStartAuthority.RouteFingerprint, routeFingerprint, StringComparison.Ordinal) &&
                (course.MilestoneOrdinal == 1
                    ? before.Care.ActiveCourseId is null &&
                      string.Equals(
                          course.CourseStartAuthority.StartingWoundFingerprint,
                          acceptedState.WoundFingerprint,
                          StringComparison.Ordinal) &&
                      acceptedState.MatchesCurrentWound(
                          course.CourseStartAuthority.StartingWound)
                    : string.Equals(
                        before.Care.ActiveCourseId,
                        course.CourseId,
                        StringComparison.Ordinal)),
            MortalWoundTreatmentCapabilityProof proof
                when mode == "guaranteed" && route is MortalWoundGuaranteedRouteDefinition guaranteed =>
                string.Equals(proof.CoordinatesFingerprint, coordinates.CoordinatesFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.AcceptedStateFingerprint, coordinates.AcceptedStateFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.ContextFingerprint, coordinates.ContextFingerprint, StringComparison.Ordinal) &&
                string.Equals(proof.CapabilityRef, guaranteed.Resolution.CapabilityRef, StringComparison.Ordinal) &&
                string.Equals(proof.WoundDomain, before.Classification.Domain, StringComparison.Ordinal) &&
                before.Severity.Rank >= proof.MinimumSeverityRank &&
                before.Severity.Rank <= proof.MaximumSeverityRank &&
                MatchesProofActor(proof, coordinates, guaranteed.Resolution.ActorRole),
            _ => false
        };
        if (!valid)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_mode_authority_invalid",
                $"one exact current {mode} mode authority",
                modeAuthority.GetType().Name);
        }
        return valid;
    }

    private static bool MatchesProofActor(
        MortalWoundTreatmentCapabilityProof proof,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string actorRole) => actorRole switch
    {
        "provider" =>
            string.Equals(proof.OwnerKind, coordinates.ProviderKind, StringComparison.Ordinal) &&
            string.Equals(proof.OwnerId, coordinates.ProviderId, StringComparison.Ordinal),
        "target" =>
            string.Equals(proof.OwnerKind, coordinates.TargetKind, StringComparison.Ordinal) &&
            string.Equals(proof.OwnerId, coordinates.TargetId, StringComparison.Ordinal),
        _ => false
    };

    private static bool TryValidatePolicy(
        MortalWoundTreatmentRouteDefinition route,
        ICollection<ValidationIssue> issues)
    {
        var policy = route.ResourcePolicy;
        var allowedConsumeOn = route.Mode == "procedure"
            ? new[] { "success", "partial_success", "failed_attempt" }
            : new[] { "success" };
        var selectorKeys = new HashSet<string>(StringComparer.Ordinal);
        var valid = policy.ReserveBeforeResolution &&
                    policy.ConsumeOn.Length <= allowedConsumeOn.Length &&
                    policy.ConsumeOn.Distinct(StringComparer.Ordinal).Count() ==
                        policy.ConsumeOn.Length &&
                    policy.ConsumeOn.All(value => allowedConsumeOn.Contains(
                        value,
                        StringComparer.Ordinal)) &&
                    policy.RefundOn.SequenceEqual(new[]
                    {
                        "cancelled", "validation_failed", "rolled_back"
                    }) &&
                    policy.Mutations.Length <= 64 &&
                    policy.Mutations.All(mutation =>
                        TryValidatePolicySelector(route, mutation, selectorKeys));
        if (!valid)
        {
            AddIssue(
                issues,
                "mortal_wound_treatment_resource_policy_invalid",
                "one closed mode-legal reserve-before-resolution policy",
                ComputePolicyFingerprint(policy));
        }
        return valid;
    }

    private static bool TryValidatePolicySelector(
        MortalWoundTreatmentRouteDefinition route,
        MortalWoundTreatmentResourceMutation mutation,
        ISet<string> selectorKeys)
    {
        if (!string.Equals(mutation.Kind, "consume_requirement", StringComparison.Ordinal) ||
            mutation.RequirementIndex < 0)
        {
            return false;
        }

        IReadOnlyList<MortalWoundTreatmentRequirement>? requirements = null;
        if (string.Equals(mutation.Scope, "common", StringComparison.Ordinal) &&
            mutation.MilestoneOrdinal is null)
        {
            requirements = route.Requirements;
        }
        else if (string.Equals(
                     mutation.Scope,
                     "course_milestone",
                     StringComparison.Ordinal) &&
                 mutation.MilestoneOrdinal is > 0 &&
                 route is MortalWoundCourseRouteDefinition course)
        {
            var milestones = course.Milestones.Where(candidate =>
                candidate.Ordinal == mutation.MilestoneOrdinal.Value).ToArray();
            if (milestones.Length != 1)
                return false;
            requirements = milestones[0].Requirements;
        }

        if (requirements is null ||
            mutation.RequirementIndex >= requirements.Count ||
            requirements[mutation.RequirementIndex].Kind is not
                ("item_quantity" or "resource_quantity"))
        {
            return false;
        }
        var key = string.Join(
            "\u001f",
            mutation.Kind,
            mutation.Scope,
            mutation.MilestoneOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "null",
            mutation.RequirementIndex.ToString(CultureInfo.InvariantCulture));
        return selectorKeys.Add(key);
    }

    internal static string ComputePolicyFingerprint(
        MortalWoundTreatmentResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_policy",
            "1",
            policy.ReserveBeforeResolution ? "true" : "false",
            policy.ConsumeOn.Length.ToString(CultureInfo.InvariantCulture)
        };
        fields.AddRange(policy.ConsumeOn);
        fields.Add(policy.RefundOn.Length.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(policy.RefundOn);
        fields.Add(policy.Mutations.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var mutation in policy.Mutations)
        {
            fields.Add(mutation.Kind);
            fields.Add(mutation.Scope);
            fields.Add(mutation.MilestoneOrdinal?.ToString(CultureInfo.InvariantCulture));
            fields.Add(mutation.RequirementIndex.ToString(CultureInfo.InvariantCulture));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        IssuePath,
        IssueSeverity.Error,
        "The Mortal wound-treatment resource authority cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Recreate resource preparation from the current accepted state, exact route, complete requirement bundle, and matching mode authority."));
}
