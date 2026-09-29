using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundCanonicalAnchorAllocation(
    string WoundId,
    string TransitionId,
    WoundRecoveryAnchor RecoveryAnchor,
    WoundDeteriorationAnchor? DeteriorationAnchor);

internal sealed record MortalWoundCanonicalAnchorPlanResult(
    MortalWoundCanonicalAnchorPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Plan is not null && Issues.Count == 0;
}

internal sealed class MortalWoundCanonicalAnchorPlan
{
    private readonly MortalWoundCanonicalAnchorAllocation[] _allocations;

    /// <summary>
    /// Seals a canonical anchor allocation phase for one exact wound stage bundle.
    /// </summary>
    /// <param name="woundStageBundleFingerprint">
    /// The fingerprint of the validated wound stage bundle.
    /// </param>
    /// <param name="currentTimeInMinutes">
    /// The non-negative current canonical minute used for newly allocated anchors.
    /// </param>
    /// <param name="allocations">
    /// The non-empty exact wound and transition allocations.
    /// </param>
    /// <param name="preservesRecoveryAnchor">
    /// Whether this is a condition-reentry phase retaining the existing recovery anchors.
    /// </param>
    private MortalWoundCanonicalAnchorPlan(
        string woundStageBundleFingerprint,
        long currentTimeInMinutes,
        IReadOnlyList<MortalWoundCanonicalAnchorAllocation> allocations,
        bool preservesRecoveryAnchor = false)
    {
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                woundStageBundleFingerprint))
        {
            throw new ArgumentException(
                "Expected the exact wound stage-bundle fingerprint.",
                nameof(woundStageBundleFingerprint));
        }
        if (currentTimeInMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTimeInMinutes));
        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Count == 0)
        {
            throw new ArgumentException(
                "A canonical anchor plan requires at least one accepted Mortal anchor allocation.",
                nameof(allocations));
        }

        var woundIds = new HashSet<string>(StringComparer.Ordinal);
        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        _allocations = allocations.Select(value =>
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!ResourceMaterializationContract.IsExactIdentifier(value.WoundId) ||
                !ResourceMaterializationContract.IsExactIdentifier(value.TransitionId) ||
                !woundIds.Add(value.WoundId) ||
                !transitionIds.Add(value.TransitionId) ||
                (preservesRecoveryAnchor
                    ? value.RecoveryAnchor.AnchorMinute > currentTimeInMinutes ||
                      value.DeteriorationAnchor is null
                    : value.RecoveryAnchor.AnchorMinute != currentTimeInMinutes ||
                      !string.Equals(value.RecoveryAnchor.AnchorTransitionId,
                          value.TransitionId, StringComparison.Ordinal)) ||
                value.DeteriorationAnchor is { } deterioration &&
                (deterioration.AnchorMinute != currentTimeInMinutes ||
                 !string.Equals(
                     deterioration.AnchorTransitionId,
                     value.TransitionId,
                     StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Canonical anchor allocations require unique exact coordinates bound to one minute.",
                    nameof(allocations));
            }
            return value with
            {
                RecoveryAnchor = value.RecoveryAnchor with { },
                DeteriorationAnchor = value.DeteriorationAnchor is { } anchor
                    ? anchor with { }
                    : null
            };
        }).ToArray();
        WoundStageBundleFingerprint = woundStageBundleFingerprint;
        CurrentTimeInMinutes = currentTimeInMinutes;
        PreservesRecoveryAnchor = preservesRecoveryAnchor;
        Fingerprint = ComputeFingerprint(
            woundStageBundleFingerprint,
            currentTimeInMinutes,
            _allocations,
            preservesRecoveryAnchor);
    }

    internal string WoundStageBundleFingerprint { get; }

    internal long CurrentTimeInMinutes { get; }

    internal string Fingerprint { get; }

    /// <summary>
    /// Indicates that the sealed phase retains recovery anchors while allocating reentered condition anchors.
    /// </summary>
    internal bool PreservesRecoveryAnchor { get; }

    internal IReadOnlyList<MortalWoundCanonicalAnchorAllocation> Allocations =>
        Array.AsReadOnly(_allocations.Select(Clone).ToArray());

    internal static bool RequiresInitialCreateAnchors(
        AcceptedMechanicsWoundStageBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.FinalPlan.CarrierContributions
            .SelectMany(static contribution => contribution.Mutations)
            .Any(static mutation =>
                string.Equals(mutation.Operation, "add", StringComparison.Ordinal) &&
                mutation.BeforeWound is null &&
                mutation.AfterWound is { } after &&
                string.Equals(
                    after.Classification.Domain,
                    "physical",
                    StringComparison.Ordinal));
    }

    /// <summary>
    /// Detects a physical worsening that reenters an inactive deterioration condition.
    /// </summary>
    /// <param name="bundle">
    /// The sealed wound stage bundle containing validated carrier mutations.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a worsening needs a new condition anchor;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RequiresConditionReentryAnchors(AcceptedMechanicsWoundStageBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.FinalPlan.CarrierContributions
            .SelectMany(static contribution => contribution.Mutations)
            .Any(IsConditionReentry);
    }

    /// <summary>
    /// Derives a pure create or condition-reentry anchor phase from the canonical clock and sealed mutations.
    /// </summary>
    /// <param name="fileSystem">
    /// The file system supplying the canonical world-time root.
    /// </param>
    /// <param name="writeLease">
    /// The active canonical lease protecting the clock and accepted state.
    /// </param>
    /// <param name="bundle">
    /// The exact validated wound stage bundle whose allocated transition identities are retained.
    /// </param>
    /// <returns>
    /// The sealed anchor plan, or issues when the clock or mutation phase is invalid.
    /// </returns>
    internal static MortalWoundCanonicalAnchorPlanResult Create(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsWoundStageBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(bundle);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var worldTimeJson = fileSystem.ReadFileAsync(
                writeLease,
                EffectAcceptedTurnInputComposer.WorldTimePath)
            .GetAwaiter()
            .GetResult();
        var currentTime = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
            worldTimeJson);
        if (currentTime is null)
        {
            return Failed(
                EffectAcceptedTurnInputComposer.WorldTimePath,
                "mortal_wound_recovery_anchor_clock_invalid",
                "one non-negative canonical currentTimeInMinutes",
                worldTimeJson is null ? "missing" : "malformed");
        }

        try
        {
            if (!RequiresInitialCreateAnchors(bundle) && RequiresConditionReentryAnchors(bundle))
                return CreateConditionReentry(bundle, currentTime.Value);
            var final = bundle.FinalPlan;
            var allocations = new List<MortalWoundCanonicalAnchorAllocation>();
            foreach (var mutation in final.CarrierContributions
                         .SelectMany(static contribution => contribution.Mutations)
                         .OrderBy(static mutation => mutation.WoundId, StringComparer.Ordinal))
            {
                if (!string.Equals(mutation.Operation, "add", StringComparison.Ordinal) ||
                    mutation.BeforeWound is not null ||
                    mutation.AfterWound is not { } after ||
                    !string.Equals(
                        after.Classification.Domain,
                        "physical",
                        StringComparison.Ordinal))
                {
                    return Failed(
                        mutation.AfterWound?.Owner.CarrierPath ??
                        mutation.BeforeWound?.Owner.CarrierPath ??
                        WoundIdentityState.StatePath,
                        "accepted_mechanics_wound_anchor_phase_mixed",
                        "only unanchored accepted physical create mutations in the initial-anchor phase",
                        $"{mutation.Operation}:{mutation.WoundId}");
                }

                if (!string.Equals(
                        after.LastTransition.Kind,
                        "create",
                        StringComparison.Ordinal) ||
                    !final.AllocatedWoundIds.Contains(
                        after.WoundId,
                        StringComparer.Ordinal) ||
                    !final.AllocatedTransitionIds.Contains(
                        after.LastTransition.TransitionId,
                        StringComparer.Ordinal) ||
                    after.Recovery.RecoveryAnchor is not null ||
                    after.Recovery.DeteriorationAnchor is not null)
                {
                    return Failed(
                        after.Owner.CarrierPath,
                        "accepted_mechanics_wound_anchor_create_invalid",
                        "one unanchored accepted physical create with exact allocated identities",
                        after.WoundId);
                }

                var recoveryAnchor = new WoundRecoveryAnchor(
                    "creation",
                    currentTime.Value,
                    after.LastTransition.TransitionId);
                var conditionKey = ReadActiveDeteriorationCondition(after.Recovery);
                var deteriorationAnchor = conditionKey is null
                    ? null
                    : new WoundDeteriorationAnchor(
                        conditionKey,
                        currentTime.Value,
                        after.LastTransition.TransitionId);
                allocations.Add(new MortalWoundCanonicalAnchorAllocation(
                    after.WoundId,
                    after.LastTransition.TransitionId,
                    recoveryAnchor,
                    deteriorationAnchor));
            }

            if (allocations.Count == 0)
            {
                return Failed(
                    WoundIdentityState.StatePath,
                    "accepted_mechanics_wound_anchor_create_missing",
                    "at least one accepted unanchored physical create",
                    "none");
            }

            return new MortalWoundCanonicalAnchorPlanResult(
                new MortalWoundCanonicalAnchorPlan(
                    bundle.BundleFingerprint,
                    currentTime.Value,
                    allocations),
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                JsonException or NullReferenceException)
        {
            return Failed(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_anchor_create_invalid",
                "one complete sealed physical-create anchor plan",
                exception.GetType().Name);
        }
    }

    /// <summary>
    /// Checks the anchor seal against its exact wound stage bundle.
    /// </summary>
    /// <param name="bundle">
    /// The wound stage bundle to compare. A <see langword="null"/> value does not agree.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the bundle fingerprint and recomputed anchor seal agree;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal bool AgreesWith(AcceptedMechanicsWoundStageBundle bundle) =>
        bundle is not null &&
        string.Equals(
            WoundStageBundleFingerprint,
            bundle.BundleFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            Fingerprint,
            ComputeFingerprint(
                WoundStageBundleFingerprint,
                CurrentTimeInMinutes,
                _allocations,
                PreservesRecoveryAnchor),
            StringComparison.Ordinal);

    internal bool TryGetAllocation(
        string woundId,
        out MortalWoundCanonicalAnchorAllocation allocation)
    {
        var matches = _allocations.Where(value => string.Equals(
            value.WoundId,
            woundId,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1)
        {
            allocation = Clone(matches[0]);
            return true;
        }
        allocation = null!;
        return false;
    }

    /// <summary>
    /// Copies the sealed allocations while retaining the anchor phase and fingerprint inputs.
    /// </summary>
    /// <returns>
    /// A detached plan with the same allocation authority.
    /// </returns>
    internal MortalWoundCanonicalAnchorPlan DetachedCopy() =>
        new(WoundStageBundleFingerprint, CurrentTimeInMinutes, _allocations, PreservesRecoveryAnchor);

    /// <summary>
    /// Derives condition anchors for a pure physical worsening phase.
    /// </summary>
    /// <param name="bundle">
    /// The sealed bundle supplying original wound images and allocated worsening identities.
    /// </param>
    /// <param name="currentTimeInMinutes">
    /// The canonical minute for each reentered deterioration condition.
    /// </param>
    /// <returns>
    /// The sealed reentry plan, or a phase conflict without an allocation.
    /// </returns>
    private static MortalWoundCanonicalAnchorPlanResult CreateConditionReentry(
        AcceptedMechanicsWoundStageBundle bundle,
        long currentTimeInMinutes)
    {
        var final = bundle.FinalPlan;
        var allocations = new List<MortalWoundCanonicalAnchorAllocation>();
        foreach (var mutation in final.CarrierContributions
                     .SelectMany(static contribution => contribution.Mutations)
                     .OrderBy(static mutation => mutation.WoundId, StringComparer.Ordinal))
        {
            if (mutation.Operation != "update" || mutation.BeforeWound is not { } before ||
                mutation.AfterWound is not { } after || after.Classification.Domain != "physical" ||
                after.LastTransition.Kind != "worsen" || before.WoundId != after.WoundId ||
                !final.AllocatedTransitionIds.Contains(after.LastTransition.TransitionId, StringComparer.Ordinal))
            {
                return Failed(WoundIdentityState.StatePath, "accepted_mechanics_wound_anchor_phase_mixed",
                    "only accepted physical worsening updates in the condition-reentry phase", mutation.Operation);
            }
            if (!IsConditionReentry(mutation))
                continue;
            if (before.Recovery.RecoveryAnchor is not { } recoveryAnchor ||
                before.Recovery.DeteriorationAnchor is not null ||
                after.Recovery.RecoveryAnchor != recoveryAnchor ||
                after.Recovery.DeteriorationAnchor is not null ||
                after.Care != WoundTransitionReducer.DeriveWorseningCare(before) ||
                !after.Recovery.Blockers.SequenceEqual(
                    WoundTransitionReducer.DeriveWorseningBlockers(before), StringComparer.Ordinal))
            {
                return Failed(after.Owner.CarrierPath, "accepted_mechanics_wound_anchor_reentry_invalid",
                    "exact worsening care reset with retained recovery anchor and absent condition anchor", after.WoundId);
            }
            allocations.Add(new MortalWoundCanonicalAnchorAllocation(after.WoundId,
                after.LastTransition.TransitionId, recoveryAnchor with { },
                new WoundDeteriorationAnchor("not_stabilized", currentTimeInMinutes,
                    after.LastTransition.TransitionId)));
        }
        return new MortalWoundCanonicalAnchorPlanResult(
            new MortalWoundCanonicalAnchorPlan(bundle.BundleFingerprint, currentTimeInMinutes,
                allocations, preservesRecoveryAnchor: true), Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Identifies the exact transition from stabilized care into an active stabilization deterioration condition.
    /// </summary>
    /// <param name="mutation">
    /// The validated carrier mutation to inspect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a physical worsening that reenters the condition;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsConditionReentry(WoundCarrierMutation mutation) =>
        mutation.Operation == "update" && mutation.BeforeWound is { } before &&
        mutation.AfterWound is { } after && after.Classification.Domain == "physical" &&
        after.LastTransition.Kind == "worsen" && before.Care.State == "stabilized" &&
        after.Care.State == "untreated" &&
        ReadActiveDeteriorationCondition(before.Recovery) is null &&
        ReadActiveDeteriorationCondition(after.Recovery) == "not_stabilized";

    private static string? ReadActiveDeteriorationCondition(WoundRecovery recovery)
    {
        if (recovery.DeteriorationPolicy is not { } policy ||
            policy.ValueKind != JsonValueKind.Object ||
            !policy.TryGetProperty("unmetConditions", out var conditions) ||
            conditions.ValueKind != JsonValueKind.Array ||
            conditions.GetArrayLength() != 1)
        {
            return null;
        }
        var condition = conditions[0].ValueKind == JsonValueKind.String
            ? conditions[0].GetString()
            : null;
        return ResourceMaterializationContract.IsExactIdentifier(condition) &&
               recovery.Blockers.Contains(condition!, StringComparer.Ordinal)
            ? condition
            : null;
    }

    private static MortalWoundCanonicalAnchorAllocation Clone(
        MortalWoundCanonicalAnchorAllocation value) =>
        value with
        {
            RecoveryAnchor = value.RecoveryAnchor with { },
            DeteriorationAnchor = value.DeteriorationAnchor is { } anchor
                ? anchor with { }
                : null
        };

    /// <summary>
    /// Computes the private anchor allocation seal, retaining the existing create-phase formula.
    /// </summary>
    /// <param name="bundleFingerprint">
    /// The exact validated wound stage-bundle fingerprint.
    /// </param>
    /// <param name="currentTimeInMinutes">
    /// The canonical minute used for new anchor allocations.
    /// </param>
    /// <param name="allocations">
    /// The wound and transition allocations included in ordinal wound order.
    /// </param>
    /// <param name="preservesRecoveryAnchor">
    /// Whether to bind the separate condition-reentry phase marker into the seal.
    /// </param>
    /// <returns>
    /// The semantic allocation fingerprint.
    /// </returns>
    private static string ComputeFingerprint(
        string bundleFingerprint,
        long currentTimeInMinutes,
        IReadOnlyList<MortalWoundCanonicalAnchorAllocation> allocations,
        bool preservesRecoveryAnchor)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound.canonical_anchor_plan",
            "1",
            bundleFingerprint,
            currentTimeInMinutes.ToString(CultureInfo.InvariantCulture),
            allocations.Count.ToString(CultureInfo.InvariantCulture)
        };
        if (preservesRecoveryAnchor)
            fields.Add("condition_reentry");
        foreach (var allocation in allocations.OrderBy(
                     static value => value.WoundId,
                     StringComparer.Ordinal))
        {
            fields.Add(allocation.WoundId);
            fields.Add(allocation.TransitionId);
            fields.Add(allocation.RecoveryAnchor.AnchorKind);
            fields.Add(allocation.RecoveryAnchor.AnchorMinute.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(allocation.RecoveryAnchor.AnchorTransitionId);
            fields.Add(allocation.DeteriorationAnchor?.ConditionKey);
            fields.Add(allocation.DeteriorationAnchor?.AnchorMinute.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(allocation.DeteriorationAnchor?.AnchorTransitionId);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalWoundCanonicalAnchorPlanResult Failed(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            null,
            new[]
            {
                new ValidationIssue(
                    path,
                    IssueSeverity.Error,
                    "Canonical Mortal wound recovery anchors could not be allocated.",
                    code: code,
                    section: "wound_materialization",
                    expected: expected,
                    actual: actual,
                    repairHint:
                        "Rebuild the accepted initial wound plan from the current canonical turn and world clock.")
            });
}
