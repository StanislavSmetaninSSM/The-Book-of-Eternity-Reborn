namespace BookOfEternityClient.Services;

internal sealed class AcceptedMechanicsDirectWoundPublicationAuthority
{
    private const string FingerprintDomain =
        "book_of_eternity.accepted_mechanics.direct_wound_publication";

    private AcceptedMechanicsDirectWoundPublicationAuthority(
        string sessionId,
        string requestId,
        string snapshotToken,
        string realm,
        int turn,
        string woundStageBundleFingerprint,
        string woundAnchorPlanFingerprint,
        string turnRequestBeforeImageFingerprint,
        string worldTimeBeforeImageFingerprint)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        Realm = realm;
        Turn = turn;
        WoundStageBundleFingerprint = woundStageBundleFingerprint;
        WoundAnchorPlanFingerprint = woundAnchorPlanFingerprint;
        TurnRequestBeforeImageFingerprint = turnRequestBeforeImageFingerprint;
        WorldTimeBeforeImageFingerprint = worldTimeBeforeImageFingerprint;
        Fingerprint = ComputeFingerprint(this);
    }

    internal string SessionId { get; }

    internal string RequestId { get; }

    internal string SnapshotToken { get; }

    internal string Realm { get; }

    internal int Turn { get; }

    internal string WoundStageBundleFingerprint { get; }

    internal string WoundAnchorPlanFingerprint { get; }

    internal string TurnRequestBeforeImageFingerprint { get; }

    internal string WorldTimeBeforeImageFingerprint { get; }

    internal string Fingerprint { get; }

    internal static AcceptedMechanicsDirectWoundPublicationAuthority Create(
        AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundCanonicalAnchorPlan anchorPlan,
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        AcceptedMechanicsWoundCommonInputComposer.PublicationAuthorityProof proof)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(anchorPlan);
        ArgumentNullException.ThrowIfNull(beforeImages);
        if (!AcceptedMechanicsWoundCommonInputComposer.IsPublicationAuthorityProof(proof) ||
            !anchorPlan.AgreesWith(bundle))
        {
            throw new ArgumentException(
                "Only the canonical wound common-input composer may mint direct publication authority.",
                nameof(proof));
        }

        var turnRequest = RequirePresent(
            beforeImages,
            LiveTurnPreparationService.TurnRequestPath);
        var worldTime = RequirePresent(
            beforeImages,
            EffectAcceptedTurnInputComposer.WorldTimePath);
        var binding = bundle.Input.Binding;
        return new AcceptedMechanicsDirectWoundPublicationAuthority(
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            binding.Turn,
            bundle.BundleFingerprint,
            anchorPlan.Fingerprint,
            turnRequest.Fingerprint,
            worldTime.Fingerprint);
    }

    internal bool AgreesWith(
        AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundCanonicalAnchorPlan anchorPlan)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(anchorPlan);
        var binding = bundle.Input.Binding;
        return string.Equals(SessionId, binding.SessionId, StringComparison.Ordinal) &&
               string.Equals(RequestId, binding.RequestId, StringComparison.Ordinal) &&
               string.Equals(
                   SnapshotToken,
                   binding.SnapshotToken,
                   StringComparison.Ordinal) &&
               string.Equals(Realm, binding.Realm, StringComparison.Ordinal) &&
               Turn == binding.Turn &&
               string.Equals(
                   WoundStageBundleFingerprint,
                   bundle.BundleFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundAnchorPlanFingerprint,
                   anchorPlan.Fingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(Fingerprint, ComputeFingerprint(this), StringComparison.Ordinal);
    }

    internal bool AgreesWith(
        AcceptedMechanicsPlanBinding binding,
        AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(plan);
        var beforeImages = plan.BeforeImages;
        return string.Equals(SessionId, binding.SessionId, StringComparison.Ordinal) &&
               string.Equals(RequestId, binding.RequestId, StringComparison.Ordinal) &&
               string.Equals(
                   SnapshotToken,
                   binding.SnapshotToken,
                   StringComparison.Ordinal) &&
               string.Equals(Realm, binding.Realm, StringComparison.Ordinal) &&
               Turn == binding.Turn &&
               string.Equals(
                   WoundStageBundleFingerprint,
                   plan.WoundStageBundle?.BundleFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundAnchorPlanFingerprint,
                   plan.WoundAnchorPlanFingerprint,
                   StringComparison.Ordinal) &&
               beforeImages.TryGetValue(
                   LiveTurnPreparationService.TurnRequestPath,
                   out var turnRequest) &&
               turnRequest.Existed &&
               turnRequest.Bytes is not null &&
               string.Equals(
                   TurnRequestBeforeImageFingerprint,
                   turnRequest.Fingerprint,
                   StringComparison.Ordinal) &&
               beforeImages.TryGetValue(
                   EffectAcceptedTurnInputComposer.WorldTimePath,
                   out var worldTime) &&
               worldTime.Existed &&
               worldTime.Bytes is not null &&
               string.Equals(
                   WorldTimeBeforeImageFingerprint,
                   worldTime.Fingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(Fingerprint, ComputeFingerprint(this), StringComparison.Ordinal);
    }

    internal AcceptedMechanicsDirectWoundPublicationAuthority DetachedCopy() =>
        new(
            SessionId,
            RequestId,
            SnapshotToken,
            Realm,
            Turn,
            WoundStageBundleFingerprint,
            WoundAnchorPlanFingerprint,
            TurnRequestBeforeImageFingerprint,
            WorldTimeBeforeImageFingerprint);

    private static CanonicalBeforeImage RequirePresent(
        IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages,
        string path) =>
        beforeImages.TryGetValue(path, out var beforeImage) &&
        beforeImage.Existed &&
        beforeImage.Bytes is not null
            ? beforeImage
            : throw new InvalidOperationException(
                $"Direct wound publication requires exact retained authority bytes at '{path}'.");

    private static string ComputeFingerprint(
        AcceptedMechanicsDirectWoundPublicationAuthority value) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            FingerprintDomain,
            "1",
            value.SessionId,
            value.RequestId,
            value.SnapshotToken,
            value.Realm,
            value.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.WoundStageBundleFingerprint,
            value.WoundAnchorPlanFingerprint,
            value.TurnRequestBeforeImageFingerprint,
            value.WorldTimeBeforeImageFingerprint
        });
}
