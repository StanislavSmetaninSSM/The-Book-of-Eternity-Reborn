using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentItemPublicationAuthority
{
    private readonly MortalItemAcceptedTurnNormalizationSnapshot _snapshot;
    private readonly MortalItemPublicationBaselineResult _baseline;
    private readonly MortalItemConsumptionPlanningInput _consumptionInput;
    private readonly MortalItemConsumptionPlanningResult _consumption;
    private readonly Dictionary<string, JsonNode?> _finalRoots;
    private readonly Dictionary<string, JsonObject> _publicationAfterImages;
    private readonly ResourceCapacityIntent[] _capacityTransitions;
    private readonly ResourceOwnerKey[] _terminalOwners;
    private readonly ResourceOwnerAuthority _baselineOwnerAuthority;
    private readonly string _effectSourceBeforeFingerprint;
    private readonly string _normalizedEffectSourceFingerprint;
    private readonly string _seal;

    private MortalWoundTreatmentItemPublicationAuthority(
        object mintCapability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentResolution resolutionAuthority,
        MortalWoundTreatmentResourceFinalization finalization,
        object continuationAuthority,
        object publicationReservationAuthority,
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        MortalTreatmentItemCommandEnvelope itemEnvelope,
        MortalItemCanonicalProjectionResult itemPhase,
        MortalItemPublicationBaselineResult baseline,
        object skillProjectionAuthority,
        string skillProjectionFingerprint,
        MortalItemConsumptionPlanningInput consumptionInput,
        MortalItemConsumptionPlanningResult consumption,
        IReadOnlyDictionary<string, JsonNode?> finalRoots,
        IReadOnlyDictionary<string, JsonObject> publicationAfterImages,
        ResourceOwnerAuthority baselineOwnerAuthority,
        string effectSourceBeforeFingerprint,
        string normalizedEffectSourceFingerprint,
        ResourceOwnerAuthority finalOwnerAuthority)
    {
        if (!WoundAcceptedTurnPlanner.IsTreatmentResourcePublicationMintCapability(
                mintCapability))
        {
            throw new InvalidOperationException(
                "Treatment item publication authority requires the private treatment mint.");
        }
        ArgumentNullException.ThrowIfNull(acceptedStateAuthority);
        ArgumentNullException.ThrowIfNull(requestAuthority);
        ArgumentNullException.ThrowIfNull(resolutionAuthority);
        ArgumentNullException.ThrowIfNull(finalization);
        ArgumentNullException.ThrowIfNull(continuationAuthority);
        ArgumentNullException.ThrowIfNull(publicationReservationAuthority);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(itemEnvelope);
        ArgumentNullException.ThrowIfNull(itemPhase);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(skillProjectionAuthority);
        ArgumentNullException.ThrowIfNull(consumptionInput);
        ArgumentNullException.ThrowIfNull(consumption);
        ArgumentNullException.ThrowIfNull(finalRoots);
        ArgumentNullException.ThrowIfNull(publicationAfterImages);
        ArgumentNullException.ThrowIfNull(baselineOwnerAuthority);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectSourceBeforeFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEffectSourceFingerprint);
        ArgumentNullException.ThrowIfNull(finalOwnerAuthority);
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                continuationAuthority,
                out var continuation) ||
            !ReferenceEquals(continuation.AcceptedStateAuthority, acceptedStateAuthority) ||
            !ReferenceEquals(continuation.RequestAuthority, requestAuthority) ||
            !ReferenceEquals(continuation.Resolution, resolutionAuthority) ||
            !ReferenceEquals(continuation.ResourceFinalization, finalization) ||
            !ReferenceEquals(
                continuation.ReservationAuthority,
                publicationReservationAuthority) ||
            !ReferenceEquals(
                continuation.SkillProjectionAuthority,
                skillProjectionAuthority))
        {
            throw new ArgumentException(
                "Treatment item publication inputs require exact continuation references.",
                nameof(continuationAuthority));
        }

        AcceptedStateAuthority = acceptedStateAuthority;
        RequestAuthority = requestAuthority;
        ResolutionAuthority = resolutionAuthority;
        Finalization = finalization;
        ContinuationFingerprint =
            WoundAcceptedTurnPlanner.GetTreatmentContinuationFingerprint(
                continuationAuthority);
        PublicationReservationAuthority = publicationReservationAuthority;
        SkillProjectionAuthority = skillProjectionAuthority;
        SkillProjectionFingerprint = skillProjectionFingerprint;
        _snapshot = snapshot.Clone();
        if (!string.Equals(
                itemEnvelope.Fingerprint,
                _snapshot.CloneItemCommandEnvelope()?.Fingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                itemPhase.Fingerprint,
                _snapshot.CloneItemPhase()?.Fingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                baseline.Fingerprint,
                _snapshot.CloneFinalBaseline()?.Fingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Treatment item publication artifacts must be the exact sealed snapshot artifacts.",
                nameof(snapshot));
        }
        _baseline = Clone(baseline);
        _consumptionInput = Clone(consumptionInput);
        _consumption = Clone(consumption);
        _finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(finalRoots);
        _publicationAfterImages = publicationAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        _capacityTransitions = consumption.CapacityTransitions
            .Select(static transition => transition.ResolvedCapacity is null
                ? transition
                : transition with
                {
                    PolicyFingerprint = transition.ResolvedCapacity.Binding
                        .AuthorityFingerprint
                })
            .ToArray();
        _terminalOwners = consumption.TerminalOwners.Distinct().ToArray();
        _baselineOwnerAuthority = baselineOwnerAuthority;
        _effectSourceBeforeFingerprint = effectSourceBeforeFingerprint;
        _normalizedEffectSourceFingerprint = normalizedEffectSourceFingerprint;
        FinalOwnerAuthority = finalOwnerAuthority;
        _seal = ComputeFingerprint();
        Fingerprint = _seal;
    }

    internal MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority { get; }
    internal MortalWoundTreatmentAttemptRequest RequestAuthority { get; }
    internal MortalWoundTreatmentResolution ResolutionAuthority { get; }
    internal MortalWoundTreatmentResourceFinalization Finalization { get; }
    internal string ContinuationFingerprint { get; }
    internal object PublicationReservationAuthority { get; }
    internal object SkillProjectionAuthority { get; }
    internal string SkillProjectionFingerprint { get; }
    internal MortalTreatmentItemCommandEnvelope ItemEnvelope =>
        _snapshot.CloneItemCommandEnvelope() ?? throw new InvalidOperationException(
            "The sealed treatment item envelope is missing.");
    internal MortalItemCanonicalProjectionResult ItemPhase =>
        _snapshot.CloneItemPhase() ?? throw new InvalidOperationException(
            "The sealed treatment item phase is missing.");
    internal MortalItemPublicationBaselineResult Baseline =>
        Clone(_baseline);
    internal MortalItemConsumptionPlanningResult Consumption => Clone(_consumption);
    internal IReadOnlyList<ResourceCapacityIntent> CapacityTransitions =>
        Array.AsReadOnly(_capacityTransitions.ToArray());
    internal IReadOnlyList<ResourceOwnerKey> TerminalOwners =>
        Array.AsReadOnly(_terminalOwners.ToArray());
    internal IReadOnlyDictionary<string, JsonObject> PublicationAfterImages =>
        new ReadOnlyDictionary<string, JsonObject>(_publicationAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal));
    internal ResourceOwnerAuthority FinalOwnerAuthority { get; }
    internal string Fingerprint { get; }

    internal static MortalWoundTreatmentItemPublicationAuthority Create(
        object mintCapability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest requestAuthority,
        MortalWoundTreatmentResolution resolutionAuthority,
        MortalWoundTreatmentResourceFinalization finalization,
        object continuationAuthority,
        object publicationReservationAuthority,
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        MortalTreatmentItemCommandEnvelope itemEnvelope,
        MortalItemCanonicalProjectionResult itemPhase,
        MortalItemPublicationBaselineResult baseline,
        object skillProjectionAuthority,
        string skillProjectionFingerprint,
        MortalItemConsumptionPlanningInput consumptionInput,
        MortalItemConsumptionPlanningResult consumption,
        IReadOnlyDictionary<string, JsonNode?> finalRoots,
        IReadOnlyDictionary<string, JsonObject> publicationAfterImages,
        ResourceOwnerAuthority baselineOwnerAuthority,
        string effectSourceBeforeFingerprint,
        string normalizedEffectSourceFingerprint,
        ResourceOwnerAuthority finalOwnerAuthority) => new(
        mintCapability,
        acceptedStateAuthority,
        requestAuthority,
        resolutionAuthority,
        finalization,
        continuationAuthority,
        publicationReservationAuthority,
        snapshot,
        itemEnvelope,
        itemPhase,
        baseline,
        skillProjectionAuthority,
        skillProjectionFingerprint,
        consumptionInput,
        consumption,
        finalRoots,
        publicationAfterImages,
        baselineOwnerAuthority,
        effectSourceBeforeFingerprint,
        normalizedEffectSourceFingerprint,
        finalOwnerAuthority);

    internal bool HasValidSeal()
    {
        var itemEnvelope = _snapshot.CloneItemCommandEnvelope();
        var itemPhase = _snapshot.CloneItemPhase();
        var baseline = _snapshot.CloneFinalBaseline();
        if (!_snapshot.HasFinalPublicationBaseline ||
            !_snapshot.RecomputesFinalPublicationBaseline() ||
            !_snapshot.MatchesFinalPublicationBaseline(_snapshot) ||
            itemEnvelope is null ||
            itemPhase is null ||
            baseline is null ||
            !string.Equals(
                baseline.Fingerprint,
                _baseline.Fingerprint,
                StringComparison.Ordinal) ||
            !itemPhase.IsValid ||
            baseline.Issues.Count != 0 ||
            !_consumption.IsValid)
        {
            return false;
        }

        var recomposed = MortalItemConsumptionPlanner.Plan(Clone(_consumptionInput));
        return ResultsEqual(recomposed, _consumption) &&
               string.Equals(_seal, ComputeFingerprint(), StringComparison.Ordinal);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCandidate(
        AcceptedMechanicsPlan candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!HasValidSeal())
            return new[] { Issue("authority", "sealed genuine item authority", "changed") };
        if (!string.Equals(
                candidate.OwnerAuthority.Fingerprint,
                FinalOwnerAuthority.Fingerprint,
                StringComparison.Ordinal))
        {
            return new[] { Issue("owners", FinalOwnerAuthority.Fingerprint,
                candidate.OwnerAuthority.Fingerprint) };
        }
        var candidateAfterImages = candidate.OwnerCompanionAfterImages;
        foreach (var pair in _publicationAfterImages)
        {
            if (!candidateAfterImages.TryGetValue(pair.Key, out var actual) ||
                !JsonNode.DeepEquals(pair.Value, actual) ||
                !candidate.TouchedPaths.Contains(pair.Key, StringComparer.Ordinal))
            {
                return new[] { Issue(pair.Key, "exact baseline-to-skill-to-item root",
                    "missing or changed") };
            }
        }
        return Array.Empty<ValidationIssue>();
    }

    internal bool MatchesNormalizationSnapshot(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        ResourceOwnerAuthority ownerAuthority)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        var finalOwnerInput = FinalOwnerAuthority.ExportInput();
        return HasValidSeal() &&
               snapshot.MatchesFinalPublicationBaseline(_snapshot) &&
               snapshot.MatchesAcceptedOwnerAuthority(_baselineOwnerAuthority) &&
               _terminalOwners.All(owner =>
                   _baselineOwnerAuthority.Entries.ContainsKey(owner) &&
                   !FinalOwnerAuthority.Entries.ContainsKey(owner) &&
                   finalOwnerInput.HistoricalOwners.Contains(owner)) &&
               string.Equals(
                   ownerAuthority.Fingerprint,
                   FinalOwnerAuthority.Fingerprint,
                   StringComparison.Ordinal);
    }

    internal IReadOnlyList<ValidationIssue> ValidateResourceProjection(
        AcceptedMechanicsResourcePlanningResult resourceResult)
    {
        ArgumentNullException.ThrowIfNull(resourceResult);
        var stateAfterImage = resourceResult.StateAfterImage;
        if (stateAfterImage is null)
            return new[] { Issue("resources", "one final resource state", "missing") };
        var transitions = resourceResult.AppliedTransitions
            .Concat(resourceResult.ReplayTransitions)
            .ToArray();
        foreach (var capacity in _capacityTransitions)
        {
            var matches = transitions.Where(transition =>
                    CapacityTransitionMatches(capacity, transition))
                .ToArray();
            if (matches.Length != 1)
                return new[] { Issue("capacity", capacity.EventRef,
                    $"matches={matches.Length}") };
            var after = matches[0].AfterState!;
            var live = stateAfterImage.Entries.Where(entry =>
                    ResourceCoordinateComparer.Instance.Equals(
                        entry.Coordinate,
                        capacity.Coordinate))
                .ToArray();
            if (live.Length != 1 ||
                live[0].Maximum != after.Maximum ||
                live[0].CapacityBinding != after.CapacityBinding ||
                live[0].State != after.State)
            {
                return new[] { Issue("resources", capacity.EventRef,
                    $"liveMatches={live.Length}") };
            }
        }
        foreach (var owner in _terminalOwners)
        {
            if (stateAfterImage.Entries.Any(entry =>
                    string.Equals(entry.Coordinate.Realm, owner.Realm,
                        StringComparison.Ordinal) &&
                    entry.Coordinate.OwnerKind == owner.OwnerKind &&
                    string.Equals(entry.Coordinate.ResourceOwnerId,
                        owner.ResourceOwnerId,
                        StringComparison.Ordinal)))
            {
                return new[] { Issue("terminalOwners", owner.ResourceOwnerId,
                    "live resource remained") };
            }
        }
        return Array.Empty<ValidationIssue>();
    }

    private static bool CapacityTransitionMatches(
        ResourceCapacityIntent expected,
        ResourceTransition actual) =>
        expected.Operation == ResourceCapacityOperation.Reconfigure &&
        expected.ResolvedCapacity is { } resolved &&
        expected.CurrentDisposition == ResourceCurrentDisposition.ScaleRatioExact &&
        actual.BeforeState is not null &&
        actual.AfterState is { } after &&
        string.Equals(actual.EventRef, expected.EventRef,
            StringComparison.Ordinal) &&
        string.Equals(actual.OriginKind, expected.OriginKind,
            StringComparison.Ordinal) &&
        string.Equals(actual.OriginId, expected.OriginId,
            StringComparison.Ordinal) &&
        ResourceCoordinateComparer.Instance.Equals(
            actual.Coordinate,
            expected.Coordinate) &&
        actual.Operation == ResourceTransitionOperation.Reconfigure &&
        actual.RequestedAmount == 0m &&
        actual.AppliedAmount == 0m &&
        actual.CapacityDisposition == ResourceCapacityDisposition.ScaleRatioExact &&
        actual.Phase == expected.Phase &&
        actual.Priority == expected.Priority &&
        actual.SourceEvidence == expected.SourceEvidence &&
        string.Equals(actual.PolicyFingerprint,
            expected.PolicyFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(actual.ReceiptId, expected.ReceiptId,
            StringComparison.Ordinal) &&
        after.Maximum == resolved.Maximum &&
        after.CapacityBinding == resolved.Binding;

    internal bool ProvesNpcRootTransition(JsonObject skillAfterImage, JsonObject finalRoot) =>
        HasValidSeal() &&
        _finalRoots.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var expected) &&
        JsonNode.DeepEquals(expected, finalRoot) &&
        (_consumption.CarrierAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var consumed)
            ? JsonNode.DeepEquals(consumed, finalRoot)
            : JsonNode.DeepEquals(skillAfterImage, finalRoot));

    internal bool ProvesNormalizedEffectSourceTransition(
        string beforeFingerprint,
        string normalizedFingerprint) =>
        HasValidSeal() &&
        string.Equals(
            beforeFingerprint,
            _effectSourceBeforeFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            normalizedFingerprint,
            _normalizedEffectSourceFingerprint,
            StringComparison.Ordinal);

    private string ComputeFingerprint()
    {
        var itemEnvelope = _snapshot.CloneItemCommandEnvelope();
        var itemPhase = _snapshot.CloneItemPhase();
        var baseline = _snapshot.CloneFinalBaseline();
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.item_publication_authority",
            "1",
            AcceptedStateAuthority.AcceptedStateFingerprint,
            RequestAuthority.RequestFingerprint,
            ResolutionAuthority.ResultFingerprint,
            Finalization.FinalizationFingerprint,
            ContinuationFingerprint,
            _snapshot.ProofFingerprint,
            itemEnvelope?.Fingerprint,
            itemPhase?.Fingerprint,
            baseline?.Fingerprint,
            SkillProjectionFingerprint,
            _consumption.Fingerprint,
            _baselineOwnerAuthority.Fingerprint,
            _effectSourceBeforeFingerprint,
            _normalizedEffectSourceFingerprint,
            FinalOwnerAuthority.Fingerprint
        };
        foreach (var pair in _finalRoots.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value is null ? "missing" :
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var pair in _publicationAfterImages.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("publication");
            fields.Add(pair.Key);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var capacity in _capacityTransitions)
        {
            fields.Add(capacity.EventRef);
            fields.Add(capacity.OriginKind);
            fields.Add(capacity.OriginId);
            fields.Add(Describe(capacity.Coordinate));
            fields.Add(capacity.Operation.ToString());
            fields.Add(capacity.ResolvedCapacity?.Maximum.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(capacity.PolicyFingerprint);
        }
        foreach (var owner in _terminalOwners)
        {
            fields.Add(owner.Realm);
            fields.Add(owner.OwnerKind.ToString());
            fields.Add(owner.ResourceOwnerId);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalItemConsumptionPlanningInput Clone(
        MortalItemConsumptionPlanningInput input) => new(
        input.Turn,
        input.BaselineFingerprint,
        Clone(input.CarrierRoots),
        MortalItemIdentityState.Parse(input.IdentityState.Root.DeepClone()),
        input.Commands.Select(static command => command with { }).ToArray(),
        input.Definitions,
        new ResourceStateLedger(input.ResourceState.Entries),
        input.CapacitySourceEvidence with { },
        input.CapacityPolicyFingerprint);

    private static MortalItemCarrierCatalogInput Clone(MortalItemCarrierCatalogInput roots) =>
        new(
            roots.PlayerInventory?.DeepClone().AsObject(),
            roots.NpcCore?.DeepClone().AsObject(),
            roots.NpcInventoryCommands?.DeepClone().AsObject(),
            roots.CurrentLocation?.DeepClone().AsObject(),
            roots.Vehicles?.DeepClone().AsObject(),
            roots.CompanionRoots.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal),
            roots.OffscreenLocationStorageContents?.DeepClone().AsObject());

    private static MortalItemCanonicalProjectionResult Clone(
        MortalItemCanonicalProjectionResult value) => new(
        MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            value.ItemPhaseAfterImages),
        value.IdentityIndexAfterImage.DeepClone().AsObject(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static MortalItemPublicationBaselineResult Clone(
        MortalItemPublicationBaselineResult value) => new(
        MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            value.FinalCarrierRoots),
        value.IdentityIndexAfterImage.DeepClone().AsObject(),
        value.AppliedTransformIds.ToArray(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static MortalItemConsumptionPlanningResult Clone(
        MortalItemConsumptionPlanningResult value) => new(
        value.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal),
        value.IdentityIndexAfterImage?.DeepClone().AsObject(),
        value.IdentityTransitions.Select(static value =>
            value.DeepClone().AsObject()).ToArray(),
        value.CapacityTransitions.ToArray(),
        value.TerminalOwners.ToArray(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static bool ResultsEqual(
        MortalItemConsumptionPlanningResult left,
        MortalItemConsumptionPlanningResult right) =>
        left.IsValid == right.IsValid &&
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal) &&
        JsonNode.DeepEquals(left.IdentityIndexAfterImage,
            right.IdentityIndexAfterImage) &&
        left.CarrierAfterImages.Count == right.CarrierAfterImages.Count &&
        left.CarrierAfterImages.All(pair =>
            right.CarrierAfterImages.TryGetValue(pair.Key, out var root) &&
            JsonNode.DeepEquals(pair.Value, root)) &&
        CapacityTransitionsEqual(
            left.CapacityTransitions,
            right.CapacityTransitions) &&
        left.TerminalOwners.SequenceEqual(right.TerminalOwners);

    private static bool CapacityTransitionsEqual(
        IReadOnlyList<ResourceCapacityIntent> left,
        IReadOnlyList<ResourceCapacityIntent> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(static pair => CapacityTransitionEqual(
            pair.First,
            pair.Second));

    private static bool CapacityTransitionEqual(
        ResourceCapacityIntent left,
        ResourceCapacityIntent right) =>
        string.Equals(left.EventRef, right.EventRef, StringComparison.Ordinal) &&
        string.Equals(left.OriginKind, right.OriginKind, StringComparison.Ordinal) &&
        string.Equals(left.OriginId, right.OriginId, StringComparison.Ordinal) &&
        ResourceCoordinateComparer.Instance.Equals(
            left.Coordinate,
            right.Coordinate) &&
        left.Operation == right.Operation &&
        ResolvedCapacityEqual(left.ResolvedCapacity, right.ResolvedCapacity) &&
        left.CurrentDisposition == right.CurrentDisposition &&
        left.Phase == right.Phase &&
        left.Priority == right.Priority &&
        left.SourceEvidence == right.SourceEvidence &&
        string.Equals(
            left.PolicyFingerprint,
            right.PolicyFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(left.ReceiptId, right.ReceiptId, StringComparison.Ordinal);

    private static bool ResolvedCapacityEqual(
        ResolvedResourceCapacity? left,
        ResolvedResourceCapacity? right) =>
        left is null && right is null ||
        left is not null &&
        right is not null &&
        left.Maximum == right.Maximum &&
        left.Binding == right.Binding &&
        left.Initialization == right.Initialization;

    private static string Describe(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static ValidationIssue Issue(string path, string expected, string actual) =>
        new(
            "treatmentPublication.items." + path,
            IssueSeverity.Error,
            "The guaranteed Mortal wound item publication authority did not agree.",
            code: "mortal_wound_treatment_publication_item_authority_mismatch",
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual);
}
