using System.Collections.ObjectModel;
using System.Globalization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDeteriorationPolicyAuthorityResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundDeteriorationPolicyAuthority? Authority);

/// <summary>
/// Registry-owned proof that one exact canonical Mortal deterioration policy is
/// structurally valid, currently applicable, and strictly worsening. The authority
/// carries no caller-authored policy or mutation surface.
/// </summary>
internal sealed class MortalWoundDeteriorationPolicyAuthority
{
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound.deterioration_policy_authority";
    private readonly MortalWoundDeteriorationPolicyDefinition _policy;

    private MortalWoundDeteriorationPolicyAuthority(
        MortalWoundDeteriorationPolicyDefinition policy,
        string authorityFingerprint)
    {
        _policy = policy;
        PolicyRef = policy.PolicyRef;
        Classification = policy.Classification;
        AuthorityFingerprint = authorityFingerprint;
    }

    public string PolicyRef { get; }
    public MortalWoundDeteriorationPolicyClassification Classification { get; }
    public string AuthorityFingerprint { get; }

    internal MortalWoundDeteriorationPolicyDefinition Policy => _policy;

    internal static MortalWoundDeteriorationPolicyAuthorityResult Create(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding,
        string woundId,
        string policyRef)
    {
        if (fileSystem is null || writeLease is null)
        {
            return InvalidAuthority(
                "recovery.deteriorationPolicy",
                "missing canonical file-system authority or write lease");
        }

        return AcceptedTurnAuthorityRegistry
            .CreateMortalWoundDeteriorationPolicyAuthority(
                fileSystem,
                writeLease,
                binding,
                woundId,
                policyRef);
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult Create(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string policyRef)
    {
        if (acceptedState is null)
        {
            return InvalidAuthority(
                "recovery.deteriorationPolicy",
                "missing accepted-state authority");
        }

        return acceptedState.CreateDeteriorationPolicyAuthority(
            coordinates,
            policyRef);
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult CreateRegistered(
        object capability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        string? policyRef,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        var path = acceptedState.WoundSourcePath + ".recovery.deteriorationPolicy";
        if (!AcceptedTurnAuthorityRegistry
                .IsDeteriorationPolicyAuthorityCapability(capability) ||
            scope is not ("recovery" or "treatment_interruption"))
        {
            return InvalidAuthority(
                path,
                "detached registry capability or unsupported authority scope");
        }

        var wound = acceptedState.CurrentWound;
        if (!string.Equals(wound.Lifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(wound.Owner.Realm, "mortal_world", StringComparison.Ordinal) ||
            !string.Equals(wound.Classification.Domain, "physical", StringComparison.Ordinal) ||
            wound.Recovery.DeteriorationPolicy is not { } policyElement)
        {
            return InvalidAuthority(
                path,
                "current active Mortal physical wound with one policy");
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(policyRef))
        {
            return ReferenceMismatch(path + ".policyRef", policyRef ?? "missing");
        }

        var parsed = MortalWoundDeteriorationPolicyContract.Parse(
            policyElement,
            path,
            wound.Owner.Realm,
            WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind),
            wound.Severity.Rank);
        if (!parsed.IsValid || parsed.Policy is not { } policy)
        {
            return new MortalWoundDeteriorationPolicyAuthorityResult(
                false,
                new ReadOnlyCollection<ValidationIssue>(parsed.Issues.ToArray()),
                null);
        }

        if (!string.Equals(policy.PolicyRef, policyRef, StringComparison.Ordinal))
            return ReferenceMismatch(path + ".policyRef", policyRef!);

        if (!IsApplicableStrictWorseningPolicy(wound, policy))
            return NotStrictlyWorsening(path, policy.ResultKind.ToString());

        var authorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                AuthorityDomain,
                "1",
                scope,
                acceptedState.SessionGeneration,
                acceptedState.AcceptedStateFingerprint,
                acceptedState.BindingFingerprint,
                acceptedState.WoundFingerprint,
                acceptedState.WoundSourcePath,
                policy.CanonicalProjection,
                policy.PolicyRef,
                policy.Classification.ToString(),
                coordinates?.CoordinatesFingerprint
            });
        return new MortalWoundDeteriorationPolicyAuthorityResult(
            true,
            Array.Empty<ValidationIssue>(),
            new MortalWoundDeteriorationPolicyAuthority(
                policy,
                authorityFingerprint));
    }

    internal static MortalWoundDeteriorationPolicyAuthorityResult InvalidAuthority(
        string path,
        string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_authority_invalid",
        "one registry-current accepted-state authority for the exact wound and policy",
        actual));

    private static bool IsApplicableStrictWorseningPolicy(
        WoundMaterializationEnvelope wound,
        MortalWoundDeteriorationPolicyDefinition policy)
    {
        if (policy.Classification !=
            MortalWoundDeteriorationPolicyClassification.StrictlyWorsening)
        {
            return false;
        }

        return policy.ResultKind switch
        {
            MortalWoundDeteriorationResultKind.IncreaseSeverity =>
                wound.Severity.Rank is >= 1 and < 4,
            MortalWoundDeteriorationResultKind.DeathContour => true,
            MortalWoundDeteriorationResultKind.AddComplication =>
                CanAcceptComplication(wound, policy),
            _ => false
        };
    }

    private static bool CanAcceptComplication(
        WoundMaterializationEnvelope wound,
        MortalWoundDeteriorationPolicyDefinition policy)
    {
        try
        {
            var resultingComplications = checked(wound.Complications.Count + 1);
            var resultingSlots = checked(
                wound.Consequences.SlotsUsed + policy.AdditionalConsequenceSlots);
            var resultingDefinitions = checked(
                wound.Consequences.OwnedEffectSources.Definitions.Count +
                policy.AdditionalDefinitions);
            var resultingRoots = checked(
                wound.Consequences.OwnedEffectSources.RootBindings.Count +
                policy.AdditionalRoots);
            return resultingComplications <= WoundMaterializationContract.MaxComplications &&
                   resultingSlots <= wound.Consequences.SlotBudget &&
                   resultingSlots <= WoundMaterializationContract.MaxConsequences &&
                   resultingDefinitions <=
                        WoundMaterializationContract.MaxOwnedEffectDefinitions &&
                   resultingRoots <=
                       WoundMaterializationContract.MaxOwnedEffectRootBindings;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static MortalWoundDeteriorationPolicyAuthorityResult ReferenceMismatch(
        string path,
        string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_reference_mismatch",
        "the exact policyRef parsed from the current canonical wound",
        actual));

    private static MortalWoundDeteriorationPolicyAuthorityResult
        NotStrictlyWorsening(string path, string actual) => Failure(Issue(
        path,
        "mortal_wound_deterioration_policy_not_strictly_worsening",
        "one currently applicable strictly-worsening policy result",
        actual));

    private static MortalWoundDeteriorationPolicyAuthorityResult Failure(
        ValidationIssue issue) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(new[] { issue }),
        null);

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The Mortal wound deterioration policy cannot grant current authority.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Re-export the current wound state and use its exact closed, applicable, strictly-worsening policy.");
}

internal enum MortalWoundRecoveryPlanningDisposition
{
    Rejected,
    Resolved,
    ExactReplay,
    InvalidHistory
}

internal enum MortalWoundRecoveryDisposition
{
    NotDue,
    Progressed,
    BlockedNotStabilized,
    Deteriorated,
    DeathHandoffRequired,
    NoNaturalRecovery
}

internal interface IMortalWoundRecoveryTransitionIntent;

internal sealed record MortalWoundRecoveryProgressIntent(
    string AuthorityFingerprint,
    long ElapsedCadences,
    long? NextRecoveryAnchorMinute,
    long RecoveryAnchorMinute,
    string TickKey,
    string WoundId) : IMortalWoundRecoveryTransitionIntent;

internal sealed record MortalWoundRecoveryDeteriorationIntent(
    string AuthorityFingerprint,
    long ElapsedCadences,
    long? NextDeteriorationAnchorMinute,
    string PolicyRef,
    string TickKey,
    string WoundId) : IMortalWoundRecoveryTransitionIntent;

internal sealed record MortalWoundDeathHandoffIntent(
    string AuthorityFingerprint,
    string PolicyRef,
    string TickKey,
    string WoundId) : IMortalWoundRecoveryTransitionIntent;

internal sealed record MortalWoundRecoveryReceipt(
    string AuthorityFingerprint,
    string ReceiptFingerprint,
    string TickKey,
    string WoundId);

internal sealed record MortalWoundRecoveryResolution(
    string AuthorityFingerprint,
    long? CadenceDueMinute,
    string ClockKind,
    string ClockSourcePath,
    long CurrentTimeInMinutes,
    MortalWoundDeathHandoffIntent? DeathHandoff,
    long? DeteriorationGraceDeadlineMinute,
    long? DeteriorationAnchorMinute,
    string? DeteriorationPolicyRef,
    long ElapsedCadences,
    long ElapsedDeteriorationCadences,
    string Mode,
    long? NextRecoveryAnchorMinute,
    long? NextDeteriorationAnchorMinute,
    long RecoveryAnchorMinute,
    MortalWoundRecoveryDisposition RecoveryDisposition,
    string TickKey,
    IReadOnlyList<IMortalWoundRecoveryTransitionIntent> TransitionIntents);

internal sealed record MortalWoundRecoveryPlanningResult(
    MortalWoundRecoveryPlanningDisposition Disposition,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundRecoveryReceipt? ReplayReceipt,
    MortalWoundRecoveryResolution? Resolution);

internal static class MortalWoundRecoveryPlanner
{
    private const string AuthorityDomain =
        "book_of_eternity.mortal_wound.recovery_resolution";
    private const string TickDomain =
        "book_of_eternity.mortal_wound.recovery_tick";

    internal static MortalWoundRecoveryPlanningResult Plan(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding,
        string woundId)
    {
        if (fileSystem is null || writeLease is null || binding is null ||
            !ResourceMaterializationContract.IsExactIdentifier(woundId))
        {
            return RegistryFailure("missing or malformed recovery authority coordinates");
        }

        return AcceptedTurnAuthorityRegistry.PlanMortalWoundRecovery(
            fileSystem,
            writeLease,
            binding,
            woundId);
    }

    internal static MortalWoundRecoveryPlanningResult PlanRegistered(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundAcceptedTurnBinding binding,
        string woundId,
        object capability)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(woundId);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        if (!AcceptedTurnAuthorityRegistry
                .IsMortalWoundRecoveryPlannerCapability(capability) ||
            !acceptedState.IsLeaseBoundTo(fileSystem, writeLease) ||
            !acceptedState.AgreesWithBindingAndWound(binding, woundId))
        {
            return RegistryFailure(
                "detached planner capability or mismatched accepted-state authority");
        }

        var wound = acceptedState.CurrentWound;
        var sourcePath = acceptedState.WoundSourcePath;
        if (!string.Equals(wound.WoundId, woundId, StringComparison.Ordinal) ||
            !string.Equals(wound.Lifecycle, "active", StringComparison.Ordinal) ||
            !string.Equals(wound.Owner.Realm, "mortal_world", StringComparison.Ordinal) ||
            !string.Equals(
                wound.Classification.Domain,
                "physical",
                StringComparison.Ordinal) ||
            !string.Equals(
                wound.Recovery.ClockKind,
                "world_time.currentTimeInMinutes",
                StringComparison.Ordinal) ||
            wound.Recovery.RecoveryAnchor is not { } recoveryAnchor)
        {
            return Failure(
                sourcePath + ".recovery",
                "mortal_wound_recovery_authority_invalid",
                "one current active Mortal physical wound with a canonical recovery anchor",
                "missing, inactive, foreign-realm, or unanchored wound");
        }

        try
        {
            return ComposeResolution(
                fileSystem,
                writeLease,
                acceptedState,
                binding,
                wound,
                sourcePath,
                recoveryAnchor);
        }
        catch (OverflowException)
        {
            return Failure(
                EffectAcceptedTurnInputComposer.WorldTimePath,
                "mortal_wound_recovery_checked_time_overflow",
                "canonical cadence, deadline, elapsed, and next-anchor arithmetic inside Int64",
                "checked time overflow");
        }
    }

    internal static MortalWoundRecoveryPlanningResult RegistryFailure(string actual) =>
        Failure(
            "recovery",
            "mortal_wound_recovery_authority_invalid",
            "one registry-current accepted-state authority for the exact binding and wound",
            actual);

    private static MortalWoundRecoveryPlanningResult ComposeResolution(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope wound,
        string sourcePath,
        WoundRecoveryAnchor recoveryAnchor)
    {
        var recovery = wound.Recovery;
        var currentMinute = acceptedState.CurrentGameMinute;
        var noNaturalRecovery = string.Equals(
            recovery.Mode,
            "no_natural_recovery",
            StringComparison.Ordinal);
        var recoveryBlocked = string.Equals(
                                  recovery.Mode,
                                  "requires_stabilization",
                                  StringComparison.Ordinal) &&
                              recovery.Blockers.Count != 0;

        long? cadenceDueMinute = null;
        long? nextRecoveryAnchorMinute = null;
        long elapsedCadences = 0;
        var recoveryDue = false;
        if (!noNaturalRecovery)
        {
            cadenceDueMinute = checked(recoveryAnchor.AnchorMinute + recovery.Cadence);
            if (!recoveryBlocked && currentMinute >= cadenceDueMinute.Value)
            {
                elapsedCadences = checked(
                    (currentMinute - recoveryAnchor.AnchorMinute) / recovery.Cadence);
                var nextOrdinal = checked(elapsedCadences + 1);
                nextRecoveryAnchorMinute = checked(
                    recoveryAnchor.AnchorMinute +
                    checked(nextOrdinal * recovery.Cadence));
                recoveryDue = elapsedCadences > 0;
            }
            else
            {
                nextRecoveryAnchorMinute = cadenceDueMinute;
            }
        }

        MortalWoundDeteriorationPolicyAuthority? deteriorationAuthority = null;
        MortalWoundDeteriorationPolicyDefinition? deteriorationPolicy = null;
        long? deteriorationAnchorMinute = null;
        long? deteriorationGraceDeadlineMinute = null;
        long? nextDeteriorationAnchorMinute = null;
        long elapsedDeteriorationCadences = 0;
        var deteriorationDue = false;

        if (recovery.DeteriorationPolicy is { } policyElement)
        {
            var policyPath = sourcePath + ".recovery.deteriorationPolicy";
            var parsed = MortalWoundDeteriorationPolicyContract.Parse(
                policyElement,
                policyPath,
                wound.Owner.Realm,
                WoundMaterializationContract.ResolveEffectTargetKind(
                    wound.Owner.OwnerKind),
                wound.Severity.Rank);
            if (!parsed.IsValid || parsed.Policy is not { } parsedPolicy)
            {
                return new MortalWoundRecoveryPlanningResult(
                    MortalWoundRecoveryPlanningDisposition.Rejected,
                    Array.AsReadOnly(parsed.Issues.ToArray()),
                    null,
                    null);
            }

            var policyAuthority =
                MortalWoundDeteriorationPolicyAuthority.Create(
                    fileSystem,
                    writeLease,
                    binding,
                    wound.WoundId,
                    parsedPolicy.PolicyRef);
            if (!policyAuthority.IsValid || policyAuthority.Authority is null)
            {
                return new MortalWoundRecoveryPlanningResult(
                    MortalWoundRecoveryPlanningDisposition.Rejected,
                    Array.AsReadOnly(policyAuthority.Issues.ToArray()),
                    null,
                    null);
            }

            deteriorationAuthority = policyAuthority.Authority;
            deteriorationPolicy = deteriorationAuthority.Policy;
            var conditionActive = recovery.Blockers.Contains(
                deteriorationPolicy.ConditionKey,
                StringComparer.Ordinal);
            if (conditionActive)
            {
                if (recovery.DeteriorationAnchor is not { } conditionAnchor ||
                    !string.Equals(
                        conditionAnchor.ConditionKey,
                        deteriorationPolicy.ConditionKey,
                        StringComparison.Ordinal))
                {
                    return Failure(
                        sourcePath + ".recovery.deteriorationAnchor",
                        "mortal_wound_recovery_authority_invalid",
                        "one canonical condition anchor matching the active deterioration policy",
                        "missing or mismatched condition anchor");
                }

                deteriorationAnchorMinute = conditionAnchor.AnchorMinute;
                deteriorationGraceDeadlineMinute = checked(
                    conditionAnchor.AnchorMinute + deteriorationPolicy.GraceMinutes);
                if (currentMinute >= deteriorationGraceDeadlineMinute.Value)
                {
                    elapsedDeteriorationCadences = checked(
                        (currentMinute - deteriorationGraceDeadlineMinute.Value) /
                        deteriorationPolicy.CadenceMinutes + 1);
                    nextDeteriorationAnchorMinute = checked(
                        deteriorationGraceDeadlineMinute.Value +
                        checked(
                            elapsedDeteriorationCadences *
                            deteriorationPolicy.CadenceMinutes));
                    deteriorationDue = true;
                }
                else
                {
                    nextDeteriorationAnchorMinute =
                        deteriorationGraceDeadlineMinute;
                }
            }
            else if (recovery.DeteriorationAnchor is not null)
            {
                return Failure(
                    sourcePath + ".recovery.deteriorationAnchor",
                    "mortal_wound_recovery_authority_invalid",
                    "no condition anchor after its policy condition is satisfied",
                    "stale condition anchor");
            }
        }
        else if (recovery.DeteriorationAnchor is not null)
        {
            return Failure(
                sourcePath + ".recovery.deteriorationAnchor",
                "mortal_wound_recovery_authority_invalid",
                "no condition anchor without a deterioration policy",
                "orphan condition anchor");
        }

        var recoveryDisposition = deteriorationDue
            ? deteriorationPolicy!.ResultKind ==
              MortalWoundDeteriorationResultKind.DeathContour
                ? MortalWoundRecoveryDisposition.DeathHandoffRequired
                : MortalWoundRecoveryDisposition.Deteriorated
            : recoveryDue
                ? MortalWoundRecoveryDisposition.Progressed
                : noNaturalRecovery
                    ? MortalWoundRecoveryDisposition.NoNaturalRecovery
                    : recoveryBlocked
                        ? MortalWoundRecoveryDisposition.BlockedNotStabilized
                        : MortalWoundRecoveryDisposition.NotDue;

        var tickKey = CreateTickKey(
            acceptedState,
            wound,
            currentMinute,
            recoveryAnchor,
            deteriorationAnchorMinute,
            recoveryDisposition);
        var authorityFingerprint = CreateAuthorityFingerprint(
            acceptedState,
            wound,
            currentMinute,
            recoveryAnchor,
            cadenceDueMinute,
            deteriorationAuthority,
            deteriorationAnchorMinute,
            deteriorationGraceDeadlineMinute,
            elapsedCadences,
            elapsedDeteriorationCadences,
            nextRecoveryAnchorMinute,
            nextDeteriorationAnchorMinute,
            recoveryDisposition,
            tickKey);

        var intents = new List<IMortalWoundRecoveryTransitionIntent>();
        MortalWoundDeathHandoffIntent? deathHandoff = null;
        if (recoveryDisposition ==
            MortalWoundRecoveryDisposition.DeathHandoffRequired)
        {
            deathHandoff = new MortalWoundDeathHandoffIntent(
                authorityFingerprint,
                deteriorationPolicy!.PolicyRef,
                tickKey,
                wound.WoundId);
            intents.Add(deathHandoff);
        }
        else
        {
            if (recoveryDue)
            {
                intents.Add(new MortalWoundRecoveryProgressIntent(
                    authorityFingerprint,
                    elapsedCadences,
                    nextRecoveryAnchorMinute,
                    recoveryAnchor.AnchorMinute,
                    tickKey,
                    wound.WoundId));
            }
            if (deteriorationDue)
            {
                intents.Add(new MortalWoundRecoveryDeteriorationIntent(
                    authorityFingerprint,
                    elapsedDeteriorationCadences,
                    nextDeteriorationAnchorMinute,
                    deteriorationPolicy!.PolicyRef,
                    tickKey,
                    wound.WoundId));
            }
        }

        return new MortalWoundRecoveryPlanningResult(
            MortalWoundRecoveryPlanningDisposition.Resolved,
            Array.Empty<ValidationIssue>(),
            null,
            new MortalWoundRecoveryResolution(
                authorityFingerprint,
                cadenceDueMinute,
                recovery.ClockKind,
                EffectAcceptedTurnInputComposer.WorldTimePath,
                currentMinute,
                deathHandoff,
                deteriorationGraceDeadlineMinute,
                deteriorationAnchorMinute,
                deteriorationPolicy?.PolicyRef,
                elapsedCadences,
                elapsedDeteriorationCadences,
                recovery.Mode,
                nextRecoveryAnchorMinute,
                nextDeteriorationAnchorMinute,
                recoveryAnchor.AnchorMinute,
                recoveryDisposition,
                tickKey,
                Array.AsReadOnly(intents.ToArray())));
    }

    private static string CreateTickKey(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundMaterializationEnvelope wound,
        long currentMinute,
        WoundRecoveryAnchor recoveryAnchor,
        long? deteriorationAnchorMinute,
        MortalWoundRecoveryDisposition disposition)
    {
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            TickDomain,
            "1",
            acceptedState.SessionGeneration,
            acceptedState.BindingFingerprint,
            acceptedState.WoundFingerprint,
            wound.WoundId,
            currentMinute.ToString(CultureInfo.InvariantCulture),
            recoveryAnchor.AnchorMinute.ToString(CultureInfo.InvariantCulture),
            recoveryAnchor.AnchorTransitionId,
            deteriorationAnchorMinute?.ToString(CultureInfo.InvariantCulture),
            disposition.ToString()
        });
        var hash = fingerprint["sha256:".Length..];
        return "wound_recovery_tick_" + hash[..32];
    }

    private static string CreateAuthorityFingerprint(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundMaterializationEnvelope wound,
        long currentMinute,
        WoundRecoveryAnchor recoveryAnchor,
        long? cadenceDueMinute,
        MortalWoundDeteriorationPolicyAuthority? deteriorationAuthority,
        long? deteriorationAnchorMinute,
        long? deteriorationGraceDeadlineMinute,
        long elapsedCadences,
        long elapsedDeteriorationCadences,
        long? nextRecoveryAnchorMinute,
        long? nextDeteriorationAnchorMinute,
        MortalWoundRecoveryDisposition disposition,
        string tickKey) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            AuthorityDomain,
            "1",
            acceptedState.SessionGeneration,
            acceptedState.AcceptedStateFingerprint,
            acceptedState.BindingFingerprint,
            acceptedState.WoundFingerprint,
            acceptedState.HistoryFingerprint,
            acceptedState.ClockFingerprint,
            acceptedState.WoundSourcePath,
            wound.WoundId,
            wound.Recovery.Mode,
            currentMinute.ToString(CultureInfo.InvariantCulture),
            recoveryAnchor.AnchorKind,
            recoveryAnchor.AnchorMinute.ToString(CultureInfo.InvariantCulture),
            recoveryAnchor.AnchorTransitionId,
            cadenceDueMinute?.ToString(CultureInfo.InvariantCulture),
            deteriorationAuthority?.AuthorityFingerprint,
            deteriorationAnchorMinute?.ToString(CultureInfo.InvariantCulture),
            deteriorationGraceDeadlineMinute?.ToString(CultureInfo.InvariantCulture),
            elapsedCadences.ToString(CultureInfo.InvariantCulture),
            elapsedDeteriorationCadences.ToString(CultureInfo.InvariantCulture),
            nextRecoveryAnchorMinute?.ToString(CultureInfo.InvariantCulture),
            nextDeteriorationAnchorMinute?.ToString(CultureInfo.InvariantCulture),
            disposition.ToString(),
            tickKey
        });

    private static MortalWoundRecoveryPlanningResult Failure(
        string path,
        string code,
        string expected,
        string actual) => new(
        MortalWoundRecoveryPlanningDisposition.Rejected,
        new ReadOnlyCollection<ValidationIssue>(new[]
        {
            new ValidationIssue(
                path,
                IssueSeverity.Error,
                "The canonical Mortal wound recovery tick cannot be planned.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual,
                repairHint:
                    "Re-export the current accepted wound state and canonical world clock; never inject a tick, policy, or anchor.")
        }),
        null,
        null);
}
