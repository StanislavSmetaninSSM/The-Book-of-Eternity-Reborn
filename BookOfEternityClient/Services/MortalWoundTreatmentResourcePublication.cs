using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentResourcePublicationBuildResult(
    MortalWoundTreatmentResourcePublicationAuthority? Authority,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Authority is not null && Issues.Count == 0;
}

internal sealed class MortalWoundTreatmentResourcePublicationAuthority
{
    private readonly MortalWoundTreatmentResourcePublicationDraft _draft;
    private readonly string _finalizationSeal;
    private readonly string _requestResourceSeal;
    private readonly MortalWoundTreatmentItemPublicationAuthority?
        _itemPublicationAuthority;

    private MortalWoundTreatmentResourcePublicationAuthority(
        object mintCapability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentResolution resolutionAuthority,
        MortalWoundTreatmentResourceFinalization finalization,
        object continuationAuthority,
        object publicationReservationAuthority,
        string semanticFingerprint,
        string identitySeed,
        MortalWoundTreatmentResourcePublicationDraft draft,
        MortalWoundTreatmentItemPublicationAuthority? itemPublicationAuthority)
    {
        if (!WoundAcceptedTurnPlanner
                .IsTreatmentResourcePublicationMintCapability(mintCapability))
        {
            throw new InvalidOperationException(
                "Treatment resource publication authority requires the private planner mint.");
        }
        AcceptedStateAuthority = acceptedStateAuthority;
        RequestAuthority = requestAuthority;
        ResolutionAuthority = resolutionAuthority;
        Finalization = finalization;
        ContinuationAuthority = continuationAuthority;
        PublicationReservationAuthority = publicationReservationAuthority;
        SemanticFingerprint = semanticFingerprint;
        IdentitySeed = identitySeed;
        _draft = draft;
        _itemPublicationAuthority = itemPublicationAuthority;
        _finalizationSeal = ComputeFinalizationSeal(finalization);
        _requestResourceSeal = ComputeRequestResourceSeal(
            requestAuthority.ResourceAuthority);
        AuthorityFingerprint = ComputeAuthorityFingerprint(
            acceptedStateAuthority,
            requestAuthority,
            resolutionAuthority,
            finalization,
            continuationAuthority,
            semanticFingerprint,
            identitySeed,
            _finalizationSeal,
            _requestResourceSeal,
            draft.Fingerprint,
            itemPublicationAuthority?.Fingerprint);
    }

    internal MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority { get; }
    internal MortalWoundTreatmentAttemptRequest RequestAuthority { get; }
    internal MortalWoundTreatmentResolution ResolutionAuthority { get; }
    internal MortalWoundTreatmentResourceFinalization Finalization { get; }
    internal object ContinuationAuthority { get; }
    internal object PublicationReservationAuthority { get; }
    internal string SemanticFingerprint { get; }
    internal string IdentitySeed { get; }
    internal string RequestFingerprint => RequestAuthority.RequestFingerprint;
    internal string ResultFingerprint => ResolutionAuthority.ResultFingerprint;
    internal string FinalizationFingerprint => Finalization.FinalizationFingerprint;
    internal string ContinuationFingerprint =>
        WoundAcceptedTurnPlanner.GetTreatmentContinuationFingerprint(
            ContinuationAuthority);
    internal string DraftFingerprint => _draft.Fingerprint;
    internal string AuthorityFingerprint { get; }
    internal string Fingerprint => AuthorityFingerprint;
    internal bool RequiresConfirmedHold =>
        !string.Equals(Finalization.Disposition, "not_required", StringComparison.Ordinal);
    internal IResourceRegisteredSystemOutcomeDraft RegisteredOutcome => _draft;
    internal MortalWoundTreatmentItemPublicationAuthority?
        ItemPublicationAuthority => _itemPublicationAuthority;
    internal ResourceOwnerAuthority? FinalItemOwnerAuthority =>
        _itemPublicationAuthority?.FinalOwnerAuthority;
    internal IReadOnlyList<ResourceOwnerKey> ItemTerminalOwners =>
        _itemPublicationAuthority?.TerminalOwners ?? Array.Empty<ResourceOwnerKey>();

    internal AcceptedMechanicsIdentityFactory CreateIdentityFactory() =>
        new TreatmentPublicationIdentityFactory(IdentitySeed);

    internal static (string OperationId, string TransitionId)
        CreateMutationIdentities(string identitySeed, ResourceMutationIntent intent)
    {
        var factory = new TreatmentPublicationIdentityFactory(identitySeed);
        return (factory.CreateOperationId(intent), factory.CreateTransitionId(intent));
    }

    internal bool HasValidSeal()
    {
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                ContinuationAuthority,
                out var continuation) ||
            !ReferenceEquals(continuation.AcceptedStateAuthority, AcceptedStateAuthority) ||
            !ReferenceEquals(continuation.RequestAuthority, RequestAuthority) ||
            !ReferenceEquals(continuation.Resolution, ResolutionAuthority) ||
            !ReferenceEquals(continuation.ResourceFinalization, Finalization) ||
            !ReferenceEquals(
                continuation.ReservationAuthority,
                PublicationReservationAuthority) ||
            !string.Equals(
                continuation.SemanticFingerprint,
                SemanticFingerprint,
                StringComparison.Ordinal) ||
            !_draft.HasValidSeal())
        {
            return false;
        }

        var expected = ComputeAuthorityFingerprint(
            AcceptedStateAuthority,
            RequestAuthority,
            ResolutionAuthority,
            Finalization,
            ContinuationAuthority,
            SemanticFingerprint,
            IdentitySeed,
            _finalizationSeal,
            _requestResourceSeal,
            _draft.Fingerprint,
            _itemPublicationAuthority?.Fingerprint);
        return string.Equals(
                   _finalizationSeal,
                   ComputeFinalizationSeal(Finalization),
                   StringComparison.Ordinal) &&
               string.Equals(
                   _requestResourceSeal,
                   ComputeRequestResourceSeal(RequestAuthority.ResourceAuthority),
                   StringComparison.Ordinal) &&
               (_itemPublicationAuthority?.HasValidSeal() ?? true) &&
               string.Equals(expected, AuthorityFingerprint, StringComparison.Ordinal);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCandidate(
        AcceptedMechanicsPlan candidate,
        MortalWoundTreatmentResourceFinalization recomposedFinalization)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(recomposedFinalization);
        if (!HasValidSeal())
        {
            return new[]
            {
                Issue(
                    "treatmentPublication.resources.authority",
                    "mortal_wound_treatment_publication_resource_authority_mismatch",
                    "one exact private-minted sealed resource publication authority",
                    "changed authority")
            };
        }
        if (!string.Equals(
                recomposedFinalization.FinalizationFingerprint,
                FinalizationFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                recomposedFinalization.RequestFingerprint,
                RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                recomposedFinalization.ResultFingerprint,
                ResultFingerprint,
                StringComparison.Ordinal) ||
            !FinalizationsEqual(recomposedFinalization, Finalization))
        {
            return new[]
            {
                Issue(
                    "treatmentPublication.resources.finalization",
                    "mortal_wound_treatment_publication_resource_finalization_mismatch",
                    FinalizationFingerprint,
                    recomposedFinalization.FinalizationFingerprint)
            };
        }
        if (!ReferenceEquals(candidate.TreatmentResourcePublicationAuthority, this))
        {
            return new[]
            {
                Issue(
                    "treatmentPublication.resources.authority",
                    "mortal_wound_treatment_publication_resource_authority_mismatch",
                    AuthorityFingerprint,
                    candidate.TreatmentResourcePublicationAuthority?.AuthorityFingerprint ??
                    "missing")
            };
        }

        foreach (var pair in _draft.ExpectedBeforeImages)
        {
            if (!candidate.BeforeImages.TryGetValue(pair.Key, out var actual) ||
                !BeforeImagesEqual(pair.Value, actual))
            {
                return new[]
                {
                    Issue(
                        pair.Key,
                        "mortal_wound_treatment_publication_resource_before_image_mismatch",
                        "the exact sealed canonical resource before-image",
                        "missing or changed before-image")
                };
            }
        }

        return _draft.ValidateCandidate(candidate);
    }

    internal static MortalWoundTreatmentResourcePublicationAuthority Create(
        object mintCapability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentResolution resolutionAuthority,
        MortalWoundTreatmentResourceFinalization finalization,
        object continuationAuthority,
        object publicationReservationAuthority,
        string semanticFingerprint,
        string identitySeed,
        MortalWoundTreatmentResourcePublicationDraft draft,
        MortalWoundTreatmentItemPublicationAuthority? itemPublicationAuthority) => new(
        mintCapability,
        acceptedStateAuthority,
        requestAuthority,
        resolutionAuthority,
        finalization,
        continuationAuthority,
        publicationReservationAuthority,
        semanticFingerprint,
        identitySeed,
        draft,
        itemPublicationAuthority);

    private static string ComputeAuthorityFingerprint(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentResourceFinalization finalization,
        object continuationAuthority,
        string semanticFingerprint,
        string identitySeed,
        string finalizationSeal,
        string requestResourceSeal,
        string draftFingerprint,
        string? itemPublicationFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_authority",
            "1",
            acceptedState.AcceptedStateFingerprint,
            acceptedState.BindingFingerprint,
            request.RequestFingerprint,
            request.ResourceAuthority.AuthorityFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            finalization.FinalizationFingerprint,
            finalizationSeal,
            requestResourceSeal,
            WoundAcceptedTurnPlanner.GetTreatmentContinuationFingerprint(
                continuationAuthority),
            semanticFingerprint,
            identitySeed,
            draftFingerprint,
            itemPublicationFingerprint
        });

    private sealed class TreatmentPublicationIdentityFactory(string identitySeed)
        : AcceptedMechanicsIdentityFactory
    {
        internal override string CreateOperationId(ResourceMutationIntent intent) =>
            Create("resource_operation_", "mutation_operation", Describe(intent.Key));

        internal override string CreateTransitionId(ResourceMutationIntent intent) =>
            Create("resource_transition_", "mutation_transition", Describe(intent.Key));

        internal override string CreateOperationId(ResourceCapacityIntent intent) =>
            Create("resource_operation_", "capacity_operation", Describe(intent.Key));

        internal override string CreateTransitionId(ResourceCapacityIntent intent) =>
            Create("resource_transition_", "capacity_transition", Describe(intent.Key));

        private string Create(string prefix, string domain, string key)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                identitySeed + "\0" + domain + "\0" + key));
            return prefix + new Guid(bytes.AsSpan(0, 16)).ToString("N");
        }

        private static string Describe(ResourceOperationKey key) =>
            $"{key.EventRef}\0{key.OriginKind}\0{key.OriginId}\0" +
            $"{DescribeCoordinate(key.Coordinate)}\0" +
            key.Operation;

        private static string Describe(ResourceCapacityOperationKey key) =>
            $"{key.EventRef}\0{key.OriginKind}\0{key.OriginId}\0" +
            $"{DescribeCoordinate(key.Coordinate)}\0" +
            key.Operation;

        private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
            $"{coordinate.Realm}/" +
            $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
            $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";
    }

    private static string ComputeRequestResourceSeal(
        MortalWoundTreatmentResourceReservationAuthority value)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_request_resource_seal",
            "1",
            value.ReservationDisposition,
            value.ReservationId,
            value.CoordinatesFingerprint,
            value.AcceptedStateFingerprint,
            value.RouteFingerprint,
            value.CourseId,
            value.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            value.CourseCoordinateFingerprint,
            value.RequirementAuthorityFingerprint,
            MortalWoundTreatmentResourceComposer.ComputePolicyFingerprint(value.Policy),
            value.AuthorityFingerprint,
            value.Claims.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var claim in value.Claims)
        {
            fields.Add(claim.Scope);
            fields.Add(claim.RequirementIndex.ToString(CultureInfo.InvariantCulture));
            fields.Add(claim.Kind);
            fields.Add(claim.AuthorityRef);
            fields.Add(claim.Realm);
            fields.Add(claim.OwnerKind);
            fields.Add(claim.OwnerId);
            fields.Add(claim.Quantity.ToString(CultureInfo.InvariantCulture));
            fields.Add(claim.SuccessWitnessFingerprint);
            fields.Add(claim.ClaimFingerprint);
            fields.Add(claim.AvailableQuantity.ToString(CultureInfo.InvariantCulture));
            fields.Add(claim.BindingFingerprint);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string ComputeFinalizationSeal(
        MortalWoundTreatmentResourceFinalization value)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_finalization_seal",
            "1",
            value.Disposition,
            value.ReservationId,
            value.RequestFingerprint,
            value.ResultFingerprint,
            value.ResourceAuthorityFingerprint,
            value.ConsumptionTrigger,
            value.FinalizationFingerprint,
            value.Consumptions.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var consumption in value.Consumptions)
        {
            fields.Add(consumption.Scope);
            fields.Add(consumption.CourseMilestoneOrdinal?.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(consumption.RequirementIndex.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(consumption.Kind);
            fields.Add(consumption.AuthorityRef);
            fields.Add(consumption.Realm);
            fields.Add(consumption.OwnerKind);
            fields.Add(consumption.OwnerId);
            fields.Add(consumption.Quantity.ToString(CultureInfo.InvariantCulture));
            fields.Add(consumption.ClaimFingerprint);
            fields.Add(consumption.IntentFingerprint);
        }
        fields.Add(value.ReleasedClaimFingerprints.Count.ToString(
            CultureInfo.InvariantCulture));
        fields.AddRange(value.ReleasedClaimFingerprints);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool FinalizationsEqual(
        MortalWoundTreatmentResourceFinalization left,
        MortalWoundTreatmentResourceFinalization right) =>
        string.Equals(left.Disposition, right.Disposition, StringComparison.Ordinal) &&
        string.Equals(left.ReservationId, right.ReservationId, StringComparison.Ordinal) &&
        string.Equals(left.RequestFingerprint, right.RequestFingerprint, StringComparison.Ordinal) &&
        string.Equals(left.ResultFingerprint, right.ResultFingerprint, StringComparison.Ordinal) &&
        string.Equals(
            left.ResourceAuthorityFingerprint,
            right.ResourceAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(left.ConsumptionTrigger, right.ConsumptionTrigger, StringComparison.Ordinal) &&
        string.Equals(
            left.FinalizationFingerprint,
            right.FinalizationFingerprint,
            StringComparison.Ordinal) &&
        left.Consumptions.Count == right.Consumptions.Count &&
        left.Consumptions.Zip(right.Consumptions).All(pair =>
            string.Equals(
                ComputeConsumptionSeal(pair.First),
                ComputeConsumptionSeal(pair.Second),
                StringComparison.Ordinal)) &&
        left.ReleasedClaimFingerprints.SequenceEqual(
            right.ReleasedClaimFingerprints,
            StringComparer.Ordinal);

    private static string ComputeConsumptionSeal(
        MortalWoundTreatmentResourceConsumptionIntent value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            value.Scope,
            value.CourseMilestoneOrdinal?.ToString(CultureInfo.InvariantCulture),
            value.RequirementIndex.ToString(CultureInfo.InvariantCulture),
            value.Kind,
            value.AuthorityRef,
            value.Realm,
            value.OwnerKind,
            value.OwnerId,
            value.Quantity.ToString(CultureInfo.InvariantCulture),
            value.ClaimFingerprint,
            value.IntentFingerprint
        });

    private static Guid CreateDeterministicGuid(string seed, int ordinal)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            seed + "\0" + ordinal.ToString(CultureInfo.InvariantCulture)));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static bool BeforeImagesEqual(
        CanonicalBeforeImage left,
        CanonicalBeforeImage right) =>
        left.Existed == right.Existed &&
        (left.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(
            right.Bytes ?? Array.Empty<byte>());

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The guaranteed Mortal wound resource publication authority did not agree.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual);
}

internal sealed class MortalWoundTreatmentResourcePublicationDraft
    : IResourceRegisteredSystemOutcomeDraft,
        IResourceRegisteredSystemCapacityDraft
{
    internal sealed record ExpectedTransition(
        int FinalizationOrdinal,
        int Turn,
        string EventRef,
        string SourceId,
        string OperationId,
        string TransitionId,
        ResourceCoordinate Coordinate,
        decimal Quantity,
        decimal Before,
        decimal After,
        int Priority,
        string SourceFingerprint,
        string PolicyFingerprint,
        bool EmitsDepleted);

    private readonly ResourceMutationSourceExport[] _sources;
    private readonly ResourceMutationIntent[] _mutations;
    private readonly ExpectedTransition[] _expectedTransitions;
    private readonly Dictionary<string, CanonicalBeforeImage> _beforeImages;
    private readonly MortalWoundTreatmentItemPublicationAuthority?
        _itemPublicationAuthority;
    private readonly string _sealFingerprint;

    internal MortalWoundTreatmentResourcePublicationDraft(
        string acceptedStateFingerprint,
        string requestFingerprint,
        string resultFingerprint,
        string finalizationFingerprint,
        IReadOnlyList<ResourceMutationSourceExport> sources,
        IReadOnlyList<ResourceMutationIntent> mutations,
        IReadOnlyList<ExpectedTransition> expectedTransitions,
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        MortalWoundTreatmentItemPublicationAuthority? itemPublicationAuthority = null)
    {
        AcceptedStateFingerprint = acceptedStateFingerprint;
        RequestFingerprint = requestFingerprint;
        ResultFingerprint = resultFingerprint;
        FinalizationFingerprint = finalizationFingerprint;
        _sources = sources.Select(CloneSource).ToArray();
        _mutations = mutations.Select(CloneMutation).ToArray();
        _expectedTransitions = expectedTransitions.Select(CloneExpected).ToArray();
        _beforeImages = beforeImages.ToDictionary(
            static pair => pair.Key,
            static pair => new CanonicalBeforeImage(
                pair.Value.Existed,
                pair.Value.Bytes),
            StringComparer.Ordinal);
        _itemPublicationAuthority = itemPublicationAuthority;
        _sealFingerprint = ComputeFingerprint(
            AcceptedStateFingerprint,
            RequestFingerprint,
            ResultFingerprint,
            FinalizationFingerprint,
            _sources,
            _mutations,
            _expectedTransitions,
            _beforeImages,
            itemPublicationAuthority?.Fingerprint);
        Fingerprint = _sealFingerprint;
    }

    internal string AcceptedStateFingerprint { get; }
    internal string RequestFingerprint { get; }
    internal string ResultFingerprint { get; }
    internal string FinalizationFingerprint { get; }
    public string Fingerprint { get; }

    public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
        Array.AsReadOnly(_sources.Select(CloneSource).ToArray());

    public IReadOnlyList<ResourceMutationIntent> Mutations =>
        Array.AsReadOnly(_mutations.Select(CloneMutation).ToArray());

    public IReadOnlyList<ResourceCapacityIntent> CapacityTransitions =>
        _itemPublicationAuthority?.CapacityTransitions ??
        Array.Empty<ResourceCapacityIntent>();

    public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
        new ReadOnlyDictionary<string, CanonicalBeforeImage>(
            _beforeImages.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(
                    pair.Value.Existed,
                    pair.Value.Bytes),
                StringComparer.Ordinal));

    internal bool HasValidSeal() => string.Equals(
        _sealFingerprint,
        ComputeFingerprint(
            AcceptedStateFingerprint,
            RequestFingerprint,
            ResultFingerprint,
            FinalizationFingerprint,
            _sources,
            _mutations,
            _expectedTransitions,
            _beforeImages,
            _itemPublicationAuthority?.Fingerprint),
        StringComparison.Ordinal);

    public ResourceRegisteredSystemOutcomeProjectionResult Project(
        AcceptedMechanicsResourcePlanningResult resourceResult)
    {
        ArgumentNullException.ThrowIfNull(resourceResult);
        var issues = ValidateTransitions(resourceResult.AppliedTransitions
                .Concat(resourceResult.ReplayTransitions)
                .ToArray())
            .Concat(_itemPublicationAuthority?.ValidateResourceProjection(resourceResult) ??
                    Array.Empty<ValidationIssue>())
            .ToArray();
        return new ResourceRegisteredSystemOutcomeProjectionResult(
            _itemPublicationAuthority?.PublicationAfterImages ??
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            Array.Empty<AcceptedMechanicsOwnerTransition>(),
            issues);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCandidate(
        AcceptedMechanicsPlan candidate)
    {
        var issues = new List<ValidationIssue>();
        var ownedFingerprints = _sources
            .Select(static value => value.AuthorityFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var events = candidate.ResourceEvents
            .Where(value => ownedFingerprints.Contains(value.SourceFingerprint))
            .ToArray();
        var expectedEventCount = _expectedTransitions.Sum(static value =>
            value.EmitsDepleted ? 2 : 1);
        if (events.Length != expectedEventCount)
        {
            issues.Add(Issue(
                "mortal_wound_treatment_publication_resource_projection_cardinality_mismatch",
                expectedEventCount.ToString(CultureInfo.InvariantCulture),
                events.Length.ToString(CultureInfo.InvariantCulture)));
            return issues;
        }

        ResourceAppliedEvent? previous = null;
        foreach (var expected in _expectedTransitions)
        {
            var matches = events.Where(actual =>
                string.Equals(
                    actual.OperationId,
                    expected.OperationId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    actual.EventRef,
                    expected.EventRef,
                    StringComparison.Ordinal) &&
                ResourceCoordinateComparer.Instance.Equals(
                    actual.Coordinate,
                    expected.Coordinate) &&
                actual.Before == expected.Before &&
                actual.After == expected.After &&
                actual.AppliedAmount == expected.Quantity &&
                actual.Turn == expected.Turn &&
                string.Equals(
                    actual.SourceFingerprint,
                    expected.SourceFingerprint,
                    StringComparison.Ordinal)).ToArray();
            var expectedKinds = expected.EmitsDepleted
                ? new[] { "resource_depleted", "resource_spent" }
                : new[] { "resource_spent" };
            var primary = matches.SingleOrDefault(static actual => string.Equals(
                actual.EventKind,
                "resource_spent",
                StringComparison.Ordinal));
            if (matches.Length != expectedKinds.Length ||
                !matches.Select(static actual => actual.EventKind)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedKinds, StringComparer.Ordinal) ||
                primary is null ||
                matches.Any(actual =>
                    actual.ExecutionSequence != primary.ExecutionSequence) ||
                (previous is not null && primary.ExecutionSequence !=
                    previous.ExecutionSequence + 1))
            {
                issues.Add(Issue(
                    "mortal_wound_treatment_publication_resource_projection_mismatch",
                    Describe(expected),
                    $"matches={matches.Length};" +
                    $"eventKinds={string.Join(',', matches.Select(static value => value.EventKind))};" +
                    $"expected={Describe(expected)}"));
            }
            previous = primary ?? previous;
        }
        if (issues.Count == 0 && _itemPublicationAuthority is not null)
            issues.AddRange(_itemPublicationAuthority.ValidateCandidate(candidate));
        return issues;
    }

    private IReadOnlyList<ValidationIssue> ValidateTransitions(
        IReadOnlyList<ResourceTransition> transitions)
    {
        var issues = new List<ValidationIssue>();
        var ownedFingerprints = _sources.Select(static value =>
                value.AuthorityFingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var owned = transitions.Where(transition =>
                string.Equals(
                    transition.OriginKind,
                    "registered_system_outcome",
                    StringComparison.Ordinal) &&
                ownedFingerprints.Contains(
                    transition.SourceEvidence.AuthorityFingerprint))
            .ToArray();
        if (owned.Length != _expectedTransitions.Length)
        {
            issues.Add(Issue(
                "mortal_wound_treatment_publication_resource_projection_cardinality_mismatch",
                _expectedTransitions.Length.ToString(CultureInfo.InvariantCulture),
                owned.Length.ToString(CultureInfo.InvariantCulture)));
            return issues;
        }

        ResourceTransition? previous = null;
        foreach (var expected in _expectedTransitions)
        {
            var matches = owned.Where(transition =>
                string.Equals(transition.EventRef, expected.EventRef, StringComparison.Ordinal) &&
                string.Equals(transition.OriginId, expected.SourceId, StringComparison.Ordinal) &&
                string.Equals(transition.OperationId, expected.OperationId, StringComparison.Ordinal) &&
                string.Equals(transition.TransitionId, expected.TransitionId, StringComparison.Ordinal) &&
                ResourceCoordinateComparer.Instance.Equals(
                    transition.Coordinate,
                    expected.Coordinate) &&
                transition.Operation == ResourceTransitionOperation.Spend).ToArray();
            if (matches.Length != 1)
            {
                issues.Add(Issue(
                    "mortal_wound_treatment_publication_resource_projection_mismatch",
                    Describe(expected),
                    $"matches={matches.Length}"));
                continue;
            }
            var actual = matches[0];
            if (actual.Turn != expected.Turn ||
                actual.Phase != ResourceMutationPhase.RegisteredSystemOutcome ||
                actual.Priority != expected.Priority ||
                actual.RequestedAmount != expected.Quantity ||
                actual.AppliedAmount != expected.Quantity ||
                actual.Outcome != ResourceTransitionOutcome.Applied ||
                actual.BeforeState?.Current != expected.Before ||
                actual.AfterState?.Current != expected.After ||
                actual.ReceiptId is not null ||
                !string.Equals(
                    actual.SourceEvidence.SourceKind,
                    "registered_system_outcome",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.SourceEvidence.SourceId,
                    expected.SourceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.SourceEvidence.AuthorityFingerprint,
                    expected.SourceFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.PolicyFingerprint,
                    expected.PolicyFingerprint,
                    StringComparison.Ordinal) ||
                (previous is not null &&
                 actual.ExecutionSequence != previous.ExecutionSequence + 1))
            {
                issues.Add(Issue(
                    "mortal_wound_treatment_publication_resource_projection_mismatch",
                    Describe(expected),
                    $"eventRef={actual.EventRef};before={actual.BeforeState?.Current};after={actual.AfterState?.Current}"));
            }
            previous = actual;
        }
        return issues;
    }

    private static string ComputeFingerprint(
        string acceptedStateFingerprint,
        string requestFingerprint,
        string resultFingerprint,
        string finalizationFingerprint,
        IReadOnlyList<ResourceMutationSourceExport> sources,
        IReadOnlyList<ResourceMutationIntent> mutations,
        IReadOnlyList<ExpectedTransition> expectedTransitions,
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        string? itemPublicationFingerprint)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_draft",
            "1",
            acceptedStateFingerprint,
            requestFingerprint,
            resultFingerprint,
            finalizationFingerprint,
            itemPublicationFingerprint,
            sources.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var source in sources)
        {
            fields.Add(source.SourceKind);
            fields.Add(source.SourceId);
            fields.Add(source.AuthorityFingerprint);
            fields.Add(source.State.ToString());
            fields.Add(source.SameTurn.ToString(CultureInfo.InvariantCulture));
            fields.Add(source.BoundOwner?.Realm);
            fields.Add(source.BoundOwner is null ? null :
                ResourceDefinitionCatalog.GetOwnerKindToken(source.BoundOwner.OwnerKind));
            fields.Add(source.BoundOwner?.ResourceOwnerId);
        }
        fields.Add(mutations.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var mutation in mutations)
        {
            AppendCoordinate(fields, mutation.Coordinate);
            fields.Add(mutation.EventRef);
            fields.Add(mutation.Amount.ToString(CultureInfo.InvariantCulture));
            fields.Add(mutation.Source.SourceKind);
            fields.Add(mutation.Source.SourceId);
            fields.Add(mutation.Source.Operation.ToString());
            fields.Add(mutation.Dependencies.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var dependency in mutation.Dependencies)
            {
                fields.Add(dependency.EventRef);
                fields.Add(dependency.OriginKind);
                fields.Add(dependency.OriginId);
                AppendCoordinate(fields, dependency.Coordinate);
                fields.Add(dependency.Operation.ToString());
            }
            fields.Add(mutation.ReceiptId);
        }
        fields.Add(expectedTransitions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var expected in expectedTransitions)
        {
            fields.Add(expected.FinalizationOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.Turn.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.EventRef);
            fields.Add(expected.SourceId);
            fields.Add(expected.OperationId);
            fields.Add(expected.TransitionId);
            AppendCoordinate(fields, expected.Coordinate);
            fields.Add(expected.Quantity.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.Before.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.After.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.Priority.ToString(CultureInfo.InvariantCulture));
            fields.Add(expected.SourceFingerprint);
            fields.Add(expected.PolicyFingerprint);
            fields.Add(expected.EmitsDepleted.ToString(CultureInfo.InvariantCulture));
        }
        fields.Add(beforeImages.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in beforeImages.OrderBy(
                     static value => value.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value.Existed.ToString(CultureInfo.InvariantCulture));
            fields.Add(pair.Value.Bytes is null
                ? null
                : "sha256:" + Convert.ToHexString(
                    SHA256.HashData(pair.Value.Bytes)).ToLowerInvariant());
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendCoordinate(
        ICollection<string?> fields,
        ResourceCoordinate coordinate)
    {
        fields.Add(coordinate.Realm);
        fields.Add(ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind));
        fields.Add(coordinate.ResourceOwnerId);
        fields.Add(coordinate.ResourceKey);
    }

    private static ResourceMutationSourceExport CloneSource(
        ResourceMutationSourceExport value) => value with
    {
        BoundOwner = value.BoundOwner is null ? null : value.BoundOwner with { }
    };

    private static ResourceMutationIntent CloneMutation(
        ResourceMutationIntent value) => value with
    {
        Coordinate = value.Coordinate with { },
        Source = value.Source with { },
        Dependencies = Array.AsReadOnly(value.Dependencies.Select(static dependency =>
            dependency with { Coordinate = dependency.Coordinate with { } }).ToArray()),
        EventRequirements = Array.AsReadOnly(value.EventRequirements.ToArray())
    };

    private static ExpectedTransition CloneExpected(ExpectedTransition value) =>
        value with { Coordinate = value.Coordinate with { } };

    private static string Describe(ExpectedTransition value) =>
        $"ordinal={value.FinalizationOrdinal};eventRef={value.EventRef};" +
        $"operationId={value.OperationId};before={value.Before};after={value.After};" +
        $"emitsDepleted={value.EmitsDepleted}";

    private static ValidationIssue Issue(
        string code,
        string expected,
        string actual) => new(
        ResourceMaterializationContract.HistoryPath,
        IssueSeverity.Error,
        "The guaranteed Mortal wound resource projection did not agree.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual);
}

internal static partial class WoundAcceptedTurnPlanner
{
    private static readonly object TreatmentResourcePublicationMintCapability = new();

    private sealed record TreatmentItemPublicationBuild(
        MortalWoundTreatmentItemPublicationAuthority? Authority,
        IReadOnlyList<ValidationIssue> Issues);

    internal static bool IsTreatmentResourcePublicationMintCapability(
        object? capability) => ReferenceEquals(
        capability,
        TreatmentResourcePublicationMintCapability);

    internal static MortalWoundTreatmentResourcePublicationBuildResult
        BuildTreatmentResourcePublication(
            object continuationAuthority,
            object publicationReservationAuthority,
            ResourceDefinitionCatalog definitions,
            ResourceStateLedger state,
            ResourceHistoryState history,
            ResourceOwnerAuthority owners,
            IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
            MortalItemAcceptedTurnNormalizationSnapshot? itemSnapshot,
            WoundPreparedAcceptedTurnPlan preparedWoundPlan,
            string effectSourceBeforeFingerprint,
            Func<string, string?> readDocument)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(beforeImages);
        ArgumentNullException.ThrowIfNull(preparedWoundPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectSourceBeforeFingerprint);
        ArgumentNullException.ThrowIfNull(readDocument);
        if (!TryReadTreatmentContinuation(
                continuationAuthority,
                out var continuation) ||
            !ReferenceEquals(
                continuation.ReservationAuthority,
                publicationReservationAuthority))
        {
            return Invalid(
                "mortal_wound_treatment_publication_resource_authority_mismatch",
                "the exact private treatment continuation and publication reservation",
                "foreign or changed continuation");
        }

        var finalization = continuation.ResourceFinalization;
        var sources = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expected = new List<
            MortalWoundTreatmentResourcePublicationDraft.ExpectedTransition>();
        var issues = new List<ValidationIssue>();
        var identitySeed = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_identity",
            "1",
            continuation.AcceptedStateFingerprint,
            continuation.RequestAuthority.RequestFingerprint,
            continuation.Resolution.ResultFingerprint,
            finalization.FinalizationFingerprint,
            continuation.SemanticFingerprint
        });
        var claims = continuation.RequestAuthority.ResourceAuthority.Claims
            .ToDictionary(static value => value.ClaimFingerprint, StringComparer.Ordinal);
        MortalWoundTreatmentItemPublicationAuthority? itemPublicationAuthority = null;
        if (finalization.Consumptions.Any(static consumption => string.Equals(
                consumption.Kind,
                "item_quantity",
                StringComparison.Ordinal)))
        {
            if (itemSnapshot is null ||
                !itemSnapshot.HasFinalPublicationBaseline ||
                !itemSnapshot.RecomputesFinalPublicationBaseline())
            {
                return Invalid(
                    "mortal_wound_treatment_publication_item_consumption_unsupported",
                    "one genuine complete private item publication capability",
                    "missing, foreign, or incomplete item authority");
            }
            var itemPublication = BuildTreatmentItemPublication(
                continuationAuthority,
                continuation,
                finalization,
                itemSnapshot,
                claims,
                definitions,
                state,
                history,
                owners,
                identitySeed,
                preparedWoundPlan,
                effectSourceBeforeFingerprint,
                readDocument);
            issues.AddRange(itemPublication.Issues);
            itemPublicationAuthority = itemPublication.Authority;
            if (itemPublicationAuthority is null || issues.Count != 0)
            {
                return new MortalWoundTreatmentResourcePublicationBuildResult(
                    null,
                    issues.Count == 0
                        ? new[]
                        {
                            PublicationIssue(
                                "mortal_wound_treatment_publication_item_consumption_unsupported",
                                "one genuine complete private item publication capability",
                                "missing, foreign, or incomplete item authority")
                        }
                        : issues);
            }
            owners = itemPublicationAuthority.FinalOwnerAuthority;
        }
        var running = new Dictionary<ResourceCoordinate, decimal>(
            ResourceCoordinateComparer.Instance);
        var priorByCoordinate = new Dictionary<ResourceCoordinate, ResourceOperationKey>(
            ResourceCoordinateComparer.Instance);
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal];
            if (!string.Equals(
                    consumption.Kind,
                    "resource_quantity",
                    StringComparison.Ordinal))
                continue;
            if (!claims.TryGetValue(consumption.ClaimFingerprint, out var claim) ||
                !ConsumptionAgrees(consumption, claim) ||
                !ResourceDefinitionCatalog.TryParseOwnerKind(
                    consumption.OwnerKind,
                    out var ownerKind))
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_finalization_mismatch",
                    "one exact selected resource claim and typed owner coordinate",
                    consumption.ClaimFingerprint));
                continue;
            }
            var owner = owners.Resolve(new ResourceOwnerRequest(
                consumption.Realm,
                ownerKind,
                consumption.AuthorityRef,
                consumption.OwnerId,
                OwnerRef: null));
            var coordinate = owner.Entry is null
                ? new ResourceCoordinate(
                    consumption.Realm,
                    ownerKind,
                    consumption.OwnerId,
                    consumption.AuthorityRef)
                : new ResourceCoordinate(
                    owner.Entry.Key.Realm,
                    owner.Entry.Key.OwnerKind,
                    owner.Entry.Key.ResourceOwnerId,
                    consumption.AuthorityRef);
            var definitionResolved = definitions.TryResolveExact(
                consumption.AuthorityRef,
                out var definition);
            var liveResolved = state.TryResolveExact(coordinate, out var live);
            if (!owner.Success || owner.Entry is null ||
                !definitionResolved ||
                definition is null ||
                definition.NumericKind != ResourceNumericKind.Integer ||
                definition.Quantum != 1m ||
                !definition.AllowedOwnerKinds.Contains(ownerKind) ||
                !definition.AllowedOperations.Contains(ResourceOperation.Spend) ||
                !liveResolved ||
                live is null ||
                live.State != ResourceLifecycleState.Active ||
                live.Current != claim.AvailableQuantity ||
                decimal.Truncate(live.Current) != live.Current)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "one exact active integer resource coordinate equal to the sealed held quantity witness",
                    DescribeResourceStateMismatch(
                        coordinate,
                        owner,
                        definition,
                        live,
                        claim.AvailableQuantity)));
                continue;
            }
            if (!running.TryGetValue(coordinate, out var before))
                before = live.Current;
            decimal after;
            try
            {
                after = checked(before - consumption.Quantity);
            }
            catch (OverflowException)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "checked full-quantity resource spend",
                    Describe(coordinate)));
                continue;
            }
            if (after < definition.MinimumPolicy.Value)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "sufficient current quantity for the full selected spend",
                    Describe(coordinate)));
                continue;
            }

            var eventRef = CreateEventRef(
                continuation.SemanticFingerprint,
                finalization.FinalizationFingerprint,
                ordinal);
            var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                new string?[]
                {
                    "book_of_eternity.mortal_wound_treatment.resource_publication_source",
                    "1",
                    continuation.AcceptedStateFingerprint,
                    continuation.RequestAuthority.RequestFingerprint,
                    continuation.Resolution.ResultFingerprint,
                    finalization.FinalizationFingerprint,
                    consumption.IntentFingerprint,
                    eventRef,
                    Describe(coordinate),
                    before.ToString(CultureInfo.InvariantCulture),
                    after.ToString(CultureInfo.InvariantCulture)
                });
            var source = new ResourceMutationSourceExport(
                "registered_system_outcome",
                eventRef,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true);
            sources.Add(source);
            var dependencies = priorByCoordinate.TryGetValue(
                    coordinate,
                    out var prior)
                ? new[] { prior }
                : Array.Empty<ResourceOperationKey>();
            var mutation = new ResourceMutationIntent(
                eventRef,
                coordinate,
                consumption.Quantity,
                new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    eventRef,
                    ResourceOperation.Spend),
                dependencies,
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            priorByCoordinate[coordinate] = mutation.Key;
            running[coordinate] = after;

            var sourceCatalog = ResourceMutationSourceCatalog.Create(new[] { source });
            var route = sourceCatalog.Catalog?.Resolve(
                mutation.Source,
                definition,
                coordinate);
            if (route is not { IsValid: true, Route: not null })
            {
                issues.AddRange(sourceCatalog.Issues);
                if (route is not null)
                    issues.AddRange(route.Issues);
                continue;
            }
            var identities = MortalWoundTreatmentResourcePublicationAuthority
                .CreateMutationIdentities(identitySeed, mutation);
            expected.Add(new MortalWoundTreatmentResourcePublicationDraft.ExpectedTransition(
                ordinal,
                continuation.Resolution.Coordinates.Turn,
                eventRef,
                eventRef,
                identities.OperationId,
                identities.TransitionId,
                coordinate,
                consumption.Quantity,
                before,
                after,
                route.Route.Priority,
                sourceFingerprint,
                route.Route.PolicyBinding.AuthorityFingerprint,
                EmitsDepleted: before > definition.MinimumPolicy.Value &&
                               after == definition.MinimumPolicy.Value));
        }
        if (issues.Count != 0)
        {
            return new MortalWoundTreatmentResourcePublicationBuildResult(
                null,
                issues);
        }

        var requiredPaths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        };
        var draftBeforeImages = new Dictionary<string, CanonicalBeforeImage>(
            StringComparer.Ordinal);
        foreach (var path in requiredPaths)
        {
            if (!beforeImages.TryGetValue(path, out var image))
            {
                return Invalid(
                    "mortal_wound_treatment_publication_resource_before_image_missing",
                    "all exact canonical resource before-images",
                    path);
            }
            draftBeforeImages.Add(
                path,
                new CanonicalBeforeImage(image.Existed, image.Bytes));
        }

        var catalog = ResourceMutationSourceCatalog.Create(sources);
        if (!catalog.IsValid || catalog.Catalog is null)
        {
            return new MortalWoundTreatmentResourcePublicationBuildResult(
                null,
                catalog.Issues);
        }
        var draft = new MortalWoundTreatmentResourcePublicationDraft(
            continuation.AcceptedStateFingerprint,
            continuation.RequestAuthority.RequestFingerprint,
            continuation.Resolution.ResultFingerprint,
            finalization.FinalizationFingerprint,
            sources,
            mutations,
            expected,
            draftBeforeImages,
            itemPublicationAuthority);
        var authority = MortalWoundTreatmentResourcePublicationAuthority.Create(
            TreatmentResourcePublicationMintCapability,
            continuation.AcceptedStateAuthority,
            continuation.RequestAuthority,
            continuation.Resolution,
            finalization,
            continuationAuthority,
            publicationReservationAuthority,
            continuation.SemanticFingerprint,
            identitySeed,
            draft,
            itemPublicationAuthority);
        return new MortalWoundTreatmentResourcePublicationBuildResult(
            authority,
            Array.Empty<ValidationIssue>());
    }

    private static TreatmentItemPublicationBuild BuildTreatmentItemPublication(
        object continuationAuthority,
        TreatmentContinuationView continuation,
        MortalWoundTreatmentResourceFinalization finalization,
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        IReadOnlyDictionary<string, MortalWoundTreatmentResourceClaim> claims,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        ResourceOwnerAuthority owners,
        string identitySeed,
        WoundPreparedAcceptedTurnPlan preparedWoundPlan,
        string effectSourceBeforeFingerprint,
        Func<string, string?> readDocument)
    {
        var itemPhase = snapshot.CloneItemPhase();
        var baseline = snapshot.CloneFinalBaseline();
        var envelope = snapshot.CloneItemCommandEnvelope();
        if (itemPhase is null || baseline is null || envelope is null ||
            !itemPhase.IsValid || baseline.Issues.Count != 0 ||
            !string.Equals(
                envelope.Fingerprint,
                continuation.ItemCommandEnvelope.Fingerprint,
                StringComparison.Ordinal) ||
            baseline.FinalCarrierRoots.GetValueOrDefault(
                NpcCoreChangesContract.NpcCorePath) is not JsonObject npcBaseline)
        {
            return ItemUnsupported("incomplete sealed item baseline");
        }

        var skillIssues = ComposeTreatmentSkillProjectionOnFinalItemBaseline(
            continuationAuthority,
            continuation.ReservationAuthority,
            npcBaseline,
            out var skillAfterImages,
            out var skillProjectionAuthority,
            out var skillProjectionFingerprint);
        if (skillIssues.Count != 0)
            return new TreatmentItemPublicationBuild(null, skillIssues);

        var skillComposedRoots =
            MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
                baseline.FinalCarrierRoots);
        if (skillAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var npcSkillAfterImage))
        {
            skillComposedRoots[NpcCoreChangesContract.NpcCorePath] =
                npcSkillAfterImage.DeepClone();
        }

        var itemRows = finalization.Consumptions
            .Select((consumption, index) => (Consumption: consumption, Index: index))
            .Where(static row => string.Equals(
                row.Consumption.Kind,
                "item_quantity",
                StringComparison.Ordinal))
            .ToArray();
        var commands = new List<MortalItemConsumptionCommand>(itemRows.Length);
        for (var index = 0; index < itemRows.Length; index++)
        {
            var row = itemRows[index];
            if (!claims.TryGetValue(
                    row.Consumption.ClaimFingerprint,
                    out var claim) ||
                !ConsumptionAgrees(row.Consumption, claim) ||
                row.Consumption.Quantity > int.MaxValue)
            {
                return ItemUnsupported(
                    "item finalization does not match one exact held claim");
            }
            var commandFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                new string?[]
                {
                    "book_of_eternity.mortal_wound_treatment.item_consumption_command",
                    "1",
                    continuation.AcceptedStateFingerprint,
                    continuation.RequestAuthority.RequestFingerprint,
                    continuation.Resolution.ResultFingerprint,
                    finalization.FinalizationFingerprint,
                    row.Index.ToString("D4", CultureInfo.InvariantCulture),
                    row.Consumption.AuthorityRef,
                    row.Consumption.Quantity.ToString(CultureInfo.InvariantCulture),
                    row.Consumption.ClaimFingerprint,
                    row.Consumption.IntentFingerprint
                });
            commands.Add(new MortalItemConsumptionCommand(
                row.Index + 1,
                row.Consumption.AuthorityRef,
                checked((int)row.Consumption.Quantity),
                row.Consumption.ClaimFingerprint,
                "mitrn_" + commandFingerprint["sha256:".Length..],
                "mortal_wound_treatment",
                "treatment_item_" +
                commandFingerprint["sha256:".Length..("sha256:".Length + 24)]));
        }

        var identity = MortalItemIdentityState.Parse(
            baseline.IdentityIndexAfterImage.DeepClone());
        if (identity.Issues.Count != 0)
            return new TreatmentItemPublicationBuild(null, identity.Issues);
        var capacitySourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_source",
                "1",
                identitySeed,
                baseline.Fingerprint
            });
        var capacityPolicyFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_policy",
                "1",
                identitySeed,
                baseline.Fingerprint
            });
        var consumptionInput = new MortalItemConsumptionPlanningInput(
            continuation.Resolution.Coordinates.Turn,
            baseline.Fingerprint,
            CreateConsumptionCarrierRoots(skillComposedRoots),
            identity,
            commands,
            definitions,
            state,
            new ResourceSourceEvidence(
                "mortal_wound_treatment",
                "treatment_item_capacity_" +
                capacitySourceFingerprint[
                    "sha256:".Length..("sha256:".Length + 20)],
                capacitySourceFingerprint),
            capacityPolicyFingerprint);
        var consumption = MortalItemConsumptionPlanner.Plan(consumptionInput);
        if (!consumption.IsValid || consumption.IdentityIndexAfterImage is null)
            return new TreatmentItemPublicationBuild(null, consumption.Issues);

        var finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            skillComposedRoots);
        foreach (var pair in consumption.CarrierAfterImages)
            finalRoots[pair.Key] = pair.Value.DeepClone();
        finalRoots[MortalItemIdentityState.StatePath] =
            consumption.IdentityIndexAfterImage.DeepClone();

        var publicationAfterImages = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        if (skillAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var composedNpc))
        {
            publicationAfterImages[NpcCoreChangesContract.NpcCorePath] =
                composedNpc.DeepClone().AsObject();
        }
        foreach (var pair in consumption.CarrierAfterImages)
        {
            if (!JsonNode.DeepEquals(
                    skillComposedRoots.GetValueOrDefault(pair.Key),
                    pair.Value))
            {
                publicationAfterImages[pair.Key] =
                    pair.Value.DeepClone().AsObject();
            }
        }
        if (!JsonNode.DeepEquals(
                baseline.IdentityIndexAfterImage,
                consumption.IdentityIndexAfterImage))
        {
            publicationAfterImages[MortalItemIdentityState.StatePath] =
                consumption.IdentityIndexAfterImage.DeepClone().AsObject();
        }

        var existingHistoricalOwners = owners.ExportInput().HistoricalOwners;
        var ownerResult = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult(finalRoots.TryGetValue(path, out var root)
                    ? root?.ToJsonString()
                    : readDocument(path)),
                state: null,
                history: null,
                CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage,
                existingHistoricalOwners.Concat(consumption.TerminalOwners)
                    .Distinct()
                    .ToArray())
            .GetAwaiter()
            .GetResult();
        if (!ownerResult.IsValid || ownerResult.Authority is null)
            return new TreatmentItemPublicationBuild(null, ownerResult.Issues);

        var normalizedEffectSourceRoots = new Dictionary<string, JsonNode?>(
            StringComparer.Ordinal);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        {
            if (baseline.FinalCarrierRoots.TryGetValue(path, out var baselineRoot))
            {
                normalizedEffectSourceRoots[path] = baselineRoot?.DeepClone();
                continue;
            }
            var json = readDocument(path);
            normalizedEffectSourceRoots[path] = json is null
                ? null
                : JsonNode.Parse(json);
        }
        var normalizedEffectSourceAuthority =
            EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
                normalizedEffectSourceRoots,
                preparedWoundPlan);
        if (normalizedEffectSourceAuthority.Issues.Count != 0)
        {
            return new TreatmentItemPublicationBuild(
                null,
                normalizedEffectSourceAuthority.Issues);
        }

        var authority = MortalWoundTreatmentItemPublicationAuthority.Create(
            TreatmentResourcePublicationMintCapability,
            continuation.AcceptedStateAuthority,
            continuation.RequestAuthority,
            continuation.Resolution,
            finalization,
            continuationAuthority,
            continuation.ReservationAuthority,
            snapshot,
            envelope,
            itemPhase,
            baseline,
            skillProjectionAuthority,
            skillProjectionFingerprint,
            consumptionInput,
            consumption,
            finalRoots,
            publicationAfterImages,
            owners,
            effectSourceBeforeFingerprint,
            normalizedEffectSourceAuthority.CanonicalFingerprint,
            ownerResult.Authority);
        return authority.HasValidSeal()
            ? new TreatmentItemPublicationBuild(
                authority,
                Array.Empty<ValidationIssue>())
            : ItemUnsupported("item authority did not reproduce its complete seal");
    }

    private static MortalItemCarrierCatalogInput CreateConsumptionCarrierRoots(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var companions = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths
            .Skip(8)
            .Where(path => roots.GetValueOrDefault(path) is JsonObject)
            .ToDictionary(
                static path => path,
                path => roots[path]!.DeepClone().AsObject(),
                StringComparer.Ordinal);
        return new MortalItemCarrierCatalogInput(
            roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
            roots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject,
            roots.GetValueOrDefault(
                MortalItemAcceptedTransferCatalog.NpcCommandsPath) as JsonObject,
            roots.GetValueOrDefault(
                StorageTransportMoveService.CurrentLocationPath) as JsonObject,
            MortalItemProjectionRootParser.ToCarrierCatalogObject(
                roots.GetValueOrDefault(StorageTransportMoveService.VehiclesPath),
                StorageTransportMoveService.VehiclesPath),
            companions,
            roots.GetValueOrDefault(
                MortalLocationStorageContentsState.StatePath) as JsonObject);
    }

    private static TreatmentItemPublicationBuild ItemUnsupported(string actual) =>
        new(
            null,
            new[]
            {
                PublicationIssue(
                    "mortal_wound_treatment_publication_item_consumption_unsupported",
                    "one genuine complete private item publication capability",
                    actual)
            });

    private static bool ConsumptionAgrees(
        MortalWoundTreatmentResourceConsumptionIntent consumption,
        MortalWoundTreatmentResourceClaim claim) =>
        string.Equals(consumption.Scope, claim.Scope, StringComparison.Ordinal) &&
        consumption.RequirementIndex == claim.RequirementIndex &&
        string.Equals(consumption.Kind, claim.Kind, StringComparison.Ordinal) &&
        string.Equals(consumption.AuthorityRef, claim.AuthorityRef, StringComparison.Ordinal) &&
        string.Equals(consumption.Realm, claim.Realm, StringComparison.Ordinal) &&
        string.Equals(consumption.OwnerKind, claim.OwnerKind, StringComparison.Ordinal) &&
        string.Equals(consumption.OwnerId, claim.OwnerId, StringComparison.Ordinal) &&
        consumption.Quantity == claim.Quantity &&
        string.Equals(
            consumption.ClaimFingerprint,
            claim.ClaimFingerprint,
            StringComparison.Ordinal);

    private static string CreateEventRef(
        string semanticFingerprint,
        string finalizationFingerprint,
        int ordinal)
    {
        var identity = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_event",
            "1",
            semanticFingerprint,
            finalizationFingerprint
        });
        return "treatment_resource_" +
               identity["sha256:".Length..("sha256:".Length + 24)] +
               "_" + ordinal.ToString("D4", CultureInfo.InvariantCulture);
    }

    private static string Describe(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static string DescribeResourceStateMismatch(
        ResourceCoordinate coordinate,
        ResourceOwnerAuthorityResolution owner,
        ResourceDefinition? definition,
        ResourceStateEntry? live,
        long expectedAvailable) =>
        $"{Describe(coordinate)}; owner={owner.Success}; " +
        $"definition={(definition is null ? "missing" : definition.NumericKind)}; " +
        $"state={(live is null ? "missing" : live.State)}; " +
        $"current={(live is null ? "missing" : live.Current.ToString(CultureInfo.InvariantCulture))}; " +
        $"expectedAvailable={expectedAvailable.ToString(CultureInfo.InvariantCulture)}";

    private static MortalWoundTreatmentResourcePublicationBuildResult Invalid(
        string code,
        string expected,
        string actual) => new(
        null,
        new[] { PublicationIssue(code, expected, actual) });

    private static ValidationIssue PublicationIssue(
        string code,
        string expected,
        string actual) => new(
        "treatmentPublication.resources",
        IssueSeverity.Error,
        "The guaranteed Mortal wound resource publication could not be composed.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual);
}
