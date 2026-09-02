using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    private readonly object _continuationAuthority;
    private readonly string _identitySeed;
    private readonly MortalItemAcceptedTurnNormalizationSnapshot _snapshot;
    private readonly MortalItemPublicationBaselineResult _baseline;
    private readonly Dictionary<string, JsonObject> _skillAfterImages;
    private readonly MortalItemConsumptionPlanningInput _consumptionInput;
    private readonly MortalItemConsumptionPlanningResult _consumption;
    private readonly Dictionary<string, JsonNode?> _finalRoots;
    private readonly Dictionary<string, JsonObject> _publicationAfterImages;
    private readonly ResourceCapacityIntent[] _capacityTransitions;
    private readonly ResourceOwnerKey[] _terminalOwners;
    private readonly ResourceOwnerAuthority _baselineOwnerAuthority;
    private readonly Dictionary<string, JsonNode?> _effectSourceBeforeRoots;
    private readonly WoundPreparedAcceptedTurnPlan _preparedWoundPlan;
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
        IReadOnlyDictionary<string, JsonNode?> effectSourceBeforeRoots,
        WoundPreparedAcceptedTurnPlan preparedWoundPlan,
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
        ArgumentNullException.ThrowIfNull(effectSourceBeforeRoots);
        ArgumentNullException.ThrowIfNull(preparedWoundPlan);
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
        _continuationAuthority = continuationAuthority;
        _identitySeed = ComputeIdentitySeed(continuation.SemanticFingerprint);
        _snapshot = snapshot.Clone();
        var sealedEnvelope = _snapshot.CloneItemCommandEnvelope();
        var sealedItemPhase = _snapshot.CloneItemPhase();
        var sealedBaseline = _snapshot.CloneFinalBaseline();
        if (sealedEnvelope is null ||
            sealedItemPhase is null ||
            sealedBaseline is null ||
            !ItemEnvelopesEqual(itemEnvelope, sealedEnvelope) ||
            !ItemPhasesEqual(itemPhase, sealedItemPhase) ||
            !BaselinesEqual(baseline, sealedBaseline))
        {
            throw new ArgumentException(
                "Treatment item publication artifacts must be the exact sealed snapshot artifacts.",
                nameof(snapshot));
        }
        _baseline = Clone(sealedBaseline);
        if (_baseline.FinalCarrierRoots.GetValueOrDefault(
                NpcCoreChangesContract.NpcCorePath) is not JsonObject npcBaseline)
        {
            throw new ArgumentException(
                "Treatment item publication baseline requires one exact NPC root.",
                nameof(baseline));
        }
        var skillIssues = WoundAcceptedTurnPlanner
            .ComposeTreatmentSkillProjectionOnFinalItemBaseline(
                _continuationAuthority,
                PublicationReservationAuthority,
                npcBaseline,
                out var skillAfterImages,
                out var recomposedSkillAuthority,
                out var recomposedSkillFingerprint);
        if (skillIssues.Count != 0 ||
            !ReferenceEquals(recomposedSkillAuthority, SkillProjectionAuthority) ||
            !string.Equals(
                recomposedSkillFingerprint,
                SkillProjectionFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Treatment item publication skill projection did not recompose.",
                nameof(skillProjectionAuthority));
        }
        _skillAfterImages = CloneObjects(skillAfterImages);
        _consumptionInput = Clone(consumptionInput);
        _consumption = Clone(consumption);
        _finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(finalRoots);
        _publicationAfterImages = publicationAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        _capacityTransitions = AdaptCapacityTransitions(
            consumption.CapacityTransitions);
        _terminalOwners = consumption.TerminalOwners.Distinct().ToArray();
        _baselineOwnerAuthority = baselineOwnerAuthority;
        _effectSourceBeforeRoots =
            MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
                effectSourceBeforeRoots);
        _preparedWoundPlan = preparedWoundPlan.ClonePreservingCacheAuthority();
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
        IReadOnlyDictionary<string, JsonNode?> effectSourceBeforeRoots,
        WoundPreparedAcceptedTurnPlan preparedWoundPlan,
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
        effectSourceBeforeRoots,
        preparedWoundPlan,
        effectSourceBeforeFingerprint,
        normalizedEffectSourceFingerprint,
        finalOwnerAuthority);

    internal bool HasValidSeal()
    {
        return TryRecompose(
                   out _,
                   out _,
                   out _) &&
               string.Equals(_seal, ComputeFingerprint(), StringComparison.Ordinal);
    }

}
