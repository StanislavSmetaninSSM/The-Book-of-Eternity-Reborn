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

    /// <summary>
    /// Checks the existing strict adverse policy against a complete canonical source wound.
    /// </summary>
    /// <param name="wound">
    /// The source whose severity and owned graph must admit the declared adverse boundary.
    /// </param>
    /// <param name="policy">
    /// The closed parsed policy; neutral, beneficial and inapplicable adverse results fail.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a strictly adverse applicable boundary; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsApplicableStrictWorseningPolicy(
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

    /// <summary>
    /// Plans recovery only after registry admission and complete signed-history agreement, resolving durable replay before fresh math.
    /// </summary>
    /// <param name="fileSystem">
    /// The owning canonical filesystem.
    /// </param>
    /// <param name="writeLease">
    /// Its active canonical write lease.
    /// </param>
    /// <param name="acceptedState">
    /// The exact registry-owned accepted source authority.
    /// </param>
    /// <param name="binding">
    /// The current accepted binding for the selected source.
    /// </param>
    /// <param name="woundId">
    /// The exact selected wound identity.
    /// </param>
    /// <param name="capability">
    /// The private registry planner capability; detached caller objects fail.
    /// </param>
    /// <returns>
    /// The original receipt on exact replay, a fresh deterministic resolution, or rejected/invalid-history issues without intents or writes.
    /// </returns>
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

        var historyFailure = ValidateCurrentHistory(fileSystem, writeLease, acceptedState, out var history);
        if (historyFailure is not null)
            return historyFailure;

        var wound = acceptedState.CurrentWound;
        if (history!.TryResolveRecoveryReplay(wound, acceptedState.CurrentGameMinute,
                out var replayReceipt, out var replayIssues))
        {
            return new(MortalWoundRecoveryPlanningDisposition.ExactReplay,
                Array.Empty<ValidationIssue>(), replayReceipt, null);
        }
        if (replayIssues.Count != 0)
            return InvalidHistory(replayIssues);
        var consumed = history.GetRecoveryConsumption(wound);
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
                recoveryAnchor, consumed.Recovery, consumed.Deterioration);
        }
        catch (OverflowException)
        {
            return Failure(
                EffectAcceptedTurnInputComposer.WorldTimePath,
                "mortal_wound_recovery_checked_time_overflow",
                "canonical cadence, deadline, elapsed, and next-anchor arithmetic inside Int64",
                "checked time overflow");
        }
        catch (InvalidDataException exception)
        {
            return HistoryFailure("wound_history_recovery_consumption_invalid",
                "elapsed consumption belonging to the exact signed epochs", exception.Message);
        }
    }

    internal static MortalWoundRecoveryPlanningResult RegistryFailure(string actual) =>
        Failure(
            "recovery",
            "mortal_wound_recovery_authority_invalid",
            "one registry-current accepted-state authority for the exact binding and wound",
            actual);

    /// <summary>
    /// Parses the complete current history and checks its semantic agreement with the signed accepted source.
    /// </summary>
    /// <param name="fileSystem">
    /// The owning canonical filesystem.
    /// </param>
    /// <param name="writeLease">
    /// The active lease authorizing the complete safe history read.
    /// </param>
    /// <param name="acceptedState">
    /// The signed accepted source whose complete history seal must agree.
    /// </param>
    /// <param name="state">
    /// Receives the strictly parsed complete history only on success; otherwise null.
    /// </param>
    /// <returns>
    /// Null on complete agreement, otherwise a closed invalid-history planning result.
    /// </returns>
    private static MortalWoundRecoveryPlanningResult? ValidateCurrentHistory(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        out WoundHistoryState? state)
    {
        state = null;
        WoundHistoryParseResult history;
        try
        {
            history = WoundHistoryState.Parse(
                fileSystem.ReadFileSync(writeLease, WoundHistoryState.HistoryPath),
                WoundHistoryState.HistoryPath);
        }
        catch (Exception exception) when (exception is IOException or
                                             UnauthorizedAccessException or
                                             InvalidDataException)
        {
            return HistoryFailure(
                "mortal_wound_recovery_history_read_failed",
                "one readable safe canonical wound-history file under the active lease",
                exception.GetType().Name);
        }

        if (!history.IsValid)
            return InvalidHistory(history.Issues);
        if (!acceptedState.MatchesCompleteHistory(history))
        {
            return HistoryFailure(
                "mortal_wound_recovery_history_mismatch",
                "complete current history matching the accepted snapshot's semantic history seal",
                "current history differs from the accepted snapshot");
        }
        state = history.State;
        return null;
    }

    private static MortalWoundRecoveryPlanningResult HistoryFailure(
        string code,
        string expected,
        string actual) => InvalidHistory(new[]
    {
        new ValidationIssue(
            WoundHistoryState.HistoryPath,
            IssueSeverity.Error,
            "The current Mortal wound history cannot authorize recovery.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore client-accepted history and re-export the signed accepted state; never hand-write history, a tick, an anchor or a receipt.")
    });

    private static MortalWoundRecoveryPlanningResult InvalidHistory(
        IReadOnlyList<ValidationIssue> issues) => new(
        MortalWoundRecoveryPlanningDisposition.InvalidHistory,
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()), null, null);

    /// <summary>
    /// Validates live adverse policy authority before reconstructing fresh recovery math from signed evidence.
    /// </summary>
    /// <param name="fileSystem">
    /// The owning canonical filesystem.
    /// </param>
    /// <param name="writeLease">
    /// Its active canonical write lease.
    /// </param>
    /// <param name="acceptedState">
    /// The admitted original accepted source authority.
    /// </param>
    /// <param name="binding">
    /// The exact original accepted binding.
    /// </param>
    /// <param name="wound">
    /// The complete current selected wound.
    /// </param>
    /// <param name="sourcePath">
    /// The original canonical wound diagnostic path.
    /// </param>
    /// <param name="recoveryAnchor">
    /// The canonical recovery anchor already validated by admission.
    /// </param>
    /// <param name="consumedRecovery">
    /// The previously consumed ordinal in the exact recovery epoch.
    /// </param>
    /// <param name="consumedDeterioration">
    /// The previously consumed ordinal in the exact condition epoch.
    /// </param>
    /// <returns>
    /// A fresh deterministic resolution, or policy admission issues without publication authority or writes.
    /// </returns>
    private static MortalWoundRecoveryPlanningResult ComposeResolution(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope wound,
        string sourcePath,
        WoundRecoveryAnchor recoveryAnchor,
        long consumedRecovery,
        long consumedDeterioration)
    {
        if (wound.Recovery.DeteriorationPolicy is { } policyElement)
        {
            var parsed = MortalWoundDeteriorationPolicyContract.Parse(policyElement,
                sourcePath + ".recovery.deteriorationPolicy", wound.Owner.Realm,
                WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind),
                wound.Severity.Rank);
            if (!parsed.IsValid || parsed.Policy is null)
                return new(MortalWoundRecoveryPlanningDisposition.Rejected, parsed.Issues, null, null);
            var authority = MortalWoundDeteriorationPolicyAuthority.Create(
                fileSystem, writeLease, binding, wound.WoundId, parsed.Policy.PolicyRef);
            if (!authority.IsValid)
                return new(MortalWoundRecoveryPlanningDisposition.Rejected, authority.Issues, null, null);
        }
        return ReconstructResolution(MortalWoundRecoverySourceEvidence.FromAcceptedState(acceptedState),
            binding, wound, consumedRecovery, consumedDeterioration);
    }

    /// <summary>
    /// Reconstructs comparison evidence using the original signed coordinates and exact epoch consumption.
    /// Blocked natural recovery retains its first unconsumed due minute.
    /// </summary>
    /// <param name="acceptedState">
    /// Detached original source coordinates; they confer no live publication authority.
    /// </param>
    /// <param name="binding">
    /// The original accepted binding retained by the persisted evaluation.
    /// </param>
    /// <param name="wound">
    /// The complete canonical wound before the evaluation.
    /// </param>
    /// <param name="consumedRecovery">
    /// The previously consumed ordinal in the wound's exact recovery epoch.
    /// </param>
    /// <param name="consumedDeterioration">
    /// The previously consumed ordinal in its exact condition epoch, or zero without one.
    /// </param>
    /// <returns>
    /// The deterministic original resolution or a structural failure, without a registry capability or writes.
    /// </returns>
    internal static MortalWoundRecoveryPlanningResult ReconstructResolution(
        MortalWoundRecoverySourceEvidence acceptedState,
        WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope wound,
        long consumedRecovery,
        long consumedDeterioration)
    {
        var sourcePath = acceptedState.WoundSourcePath;
        var recoveryAnchor = wound.Recovery.RecoveryAnchor
            ?? throw new InvalidDataException("Recovery evidence has no canonical epoch.");
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
            var schedule = MortalWoundRecoveryCadence.Evaluate(
                recoveryAnchor.AnchorMinute, recovery.Cadence, currentMinute, consumedRecovery, false,
                advancePastElapsed: !recoveryBlocked);
            nextRecoveryAnchorMinute = schedule.NextDueMinute;
            if (!recoveryBlocked)
            {
                elapsedCadences = schedule.NewIntervals;
                recoveryDue = elapsedCadences > 0;
            }
        }

        string? deteriorationAuthorityFingerprint = null;
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

            if (!MortalWoundDeteriorationPolicyAuthority.IsApplicableStrictWorseningPolicy(wound, parsedPolicy))
                return Failure(policyPath, "mortal_wound_deterioration_policy_authority_invalid",
                    "one applicable strictly adverse source policy", parsedPolicy.ResultKind.ToString());

            deteriorationPolicy = parsedPolicy;
            deteriorationAuthorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound.deterioration_policy_authority", "1", "recovery",
                acceptedState.SessionGeneration, acceptedState.AcceptedStateFingerprint,
                acceptedState.BindingFingerprint, acceptedState.WoundFingerprint,
                acceptedState.WoundSourcePath, parsedPolicy.CanonicalProjection,
                parsedPolicy.PolicyRef, parsedPolicy.Classification.ToString(), null
            });
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
                var schedule = MortalWoundRecoveryCadence.Evaluate(
                    deteriorationGraceDeadlineMinute.Value, deteriorationPolicy.CadenceMinutes,
                    currentMinute, consumedDeterioration, true);
                elapsedDeteriorationCadences = schedule.NewIntervals;
                nextDeteriorationAnchorMinute = schedule.NextDueMinute;
                deteriorationDue = elapsedDeteriorationCadences > 0;
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
            deteriorationAuthorityFingerprint,
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

    /// <summary>
    /// Derives the original logical tick from signed source and epoch coordinates.
    /// </summary>
    /// <param name="acceptedState">
    /// The detached original accepted scalar evidence.
    /// </param>
    /// <param name="wound">
    /// The complete original selected wound.
    /// </param>
    /// <param name="currentMinute">
    /// The signed original evaluation minute.
    /// </param>
    /// <param name="recoveryAnchor">
    /// The original canonical recovery epoch.
    /// </param>
    /// <param name="deteriorationAnchorMinute">
    /// The original active condition minute, or null when no condition is active.
    /// </param>
    /// <param name="disposition">
    /// The reconstructed evaluation outcome.
    /// </param>
    /// <returns>
    /// The original deterministic logical recovery tick identifier.
    /// </returns>
    private static string CreateTickKey(
        MortalWoundRecoverySourceEvidence acceptedState,
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

    /// <summary>
    /// Seals every original signed source, cadence and outcome coordinate of a reconstructed resolution.
    /// </summary>
    /// <param name="acceptedState">
    /// The detached original accepted scalar evidence.
    /// </param>
    /// <param name="wound">
    /// The complete original selected wound.
    /// </param>
    /// <param name="currentMinute">
    /// The original signed evaluation minute.
    /// </param>
    /// <param name="recoveryAnchor">
    /// The canonical recovery epoch retained by the source.
    /// </param>
    /// <param name="cadenceDueMinute">
    /// The first recovery due minute, or null without natural recovery.
    /// </param>
    /// <param name="deteriorationAuthorityFingerprint">
    /// The reconstructed strict policy authority seal, or null without a policy.
    /// </param>
    /// <param name="deteriorationAnchorMinute">
    /// The original active condition minute, or null when inactive.
    /// </param>
    /// <param name="deteriorationGraceDeadlineMinute">
    /// The first condition due minute, or null when inactive.
    /// </param>
    /// <param name="elapsedCadences">
    /// Newly elapsed unconsumed natural intervals.
    /// </param>
    /// <param name="elapsedDeteriorationCadences">
    /// Newly elapsed unconsumed condition intervals.
    /// </param>
    /// <param name="nextRecoveryAnchorMinute">
    /// The next due minute on the unchanged recovery schedule, or null without natural recovery.
    /// </param>
    /// <param name="nextDeteriorationAnchorMinute">
    /// The next due minute on the unchanged condition schedule, or null when inactive.
    /// </param>
    /// <param name="disposition">
    /// The reconstructed evaluation outcome.
    /// </param>
    /// <param name="tickKey">
    /// The original deterministic logical evaluation tick.
    /// </param>
    /// <returns>
    /// The complete deterministic original recovery resolution authority fingerprint.
    /// </returns>
    private static string CreateAuthorityFingerprint(
        MortalWoundRecoverySourceEvidence acceptedState,
        WoundMaterializationEnvelope wound,
        long currentMinute,
        WoundRecoveryAnchor recoveryAnchor,
        long? cadenceDueMinute,
        string? deteriorationAuthorityFingerprint,
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
            deteriorationAuthorityFingerprint,
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
