using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundProcedureClaimRecoveryBatchResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest> _requests;

    internal MortalWoundProcedureClaimRecoveryBatchResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        IEnumerable<MortalWoundTreatmentAttemptRequest> requests,
        MortalWoundProcedureDiceReservationRegistry? diceRegistry,
        MortalWoundCriticalReactionReservationRegistry? reactionRegistry)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        _requests = MortalWoundTreatmentShellDetachment.Freeze(requests);
        DiceRegistry = diceRegistry;
        ReactionRegistry = reactionRegistry;
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal IReadOnlyList<MortalWoundTreatmentAttemptRequest> Requests => _requests;
    internal MortalWoundProcedureDiceReservationRegistry? DiceRegistry { get; }
    internal MortalWoundCriticalReactionReservationRegistry? ReactionRegistry { get; }
}

/// <summary>
/// Reconstructs one detached procedure-claim batch into isolated registries. The
/// caller remains responsible for current-state admission and for publishing the
/// reconstructed registries atomically only after fresh-authority validation.
/// </summary>
internal sealed class MortalWoundProcedureClaimRecoveryCoordinator
{
    private static readonly object DicePoolReadCapability = new();

    private sealed record VirtualHistoricalFateClaim(
        MortalWoundProcedurePotentialNaturalOneClaimSpan SourceSpan,
        string EffectId);

    internal static bool IsDicePoolReadCapability(object capability) =>
        ReferenceEquals(capability, DicePoolReadCapability);

    internal MortalWoundProcedureClaimRecoveryBatchResult Restore(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(requests);
        if (!acceptedState.TryReadProcedureDicePool(
                DicePoolReadCapability,
                out var acceptedD20EventValues,
                out var poolFingerprint))
        {
            return Failure(
                "mortal_wound_treatment_procedure_claim_recovery_stale",
                "the exact typed current accepted d20 evidence",
                "missing or foreign accepted d20 evidence");
        }

        var restoredDice = new MortalWoundProcedureDiceReservationRegistry();
        var restoredReactions = new MortalWoundCriticalReactionReservationRegistry();
        var historicallyClaimedReactionEffectIds =
            new HashSet<string>(StringComparer.Ordinal);
        var virtualHistoricalFateClaims =
            new List<VirtualHistoricalFateClaim>();
        var restoredRequests = requests.ToArray();
        var recoveryOrder = Enumerable.Range(0, requests.Count)
            .Where(index =>
                requests[index].ModeAuthority is MortalWoundProcedureCheckAuthority &&
                string.Equals(requests[index].Mode, "procedure", StringComparison.Ordinal) &&
                string.Equals(
                    requests[index].Coordinates.AcceptedStateFingerprint,
                    acceptedState.AcceptedStateFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        foreach (var requestIndex in recoveryOrder)
        {
            var request = requests[requestIndex];
            var procedure = (MortalWoundProcedureCheckAuthority)request.ModeAuthority;
            if (!request.Coordinates.AgreesWithAcceptedStateSemantics(acceptedState))
            {
                return Failure(
                    "mortal_wound_treatment_procedure_claim_recovery_stale",
                    "current persisted procedure coordinates",
                    request.Coordinates.OperationKey);
            }

            var dice = restoredDice.RestoreExact(
                DicePoolReadCapability,
                request.Coordinates,
                procedure.RollMode,
                procedure.SourceIndices,
                procedure.SourceRolls,
                acceptedD20EventValues,
                poolFingerprint);
            if (!dice.IsValid || dice.Reservation is null)
                return Failure(dice.Issues);

            if (!ReleaseOverlappedVirtualClaims(
                    dice.Reservation.SourceIndices,
                    virtualHistoricalFateClaims,
                    historicallyClaimedReactionEffectIds) ||
                !HasExactVirtualClaimAgreement(
                    virtualHistoricalFateClaims,
                    historicallyClaimedReactionEffectIds))
            {
                return Failure(
                    "mortal_wound_treatment_procedure_claim_recovery_invalid",
                    "one release-aware virtual Fate claim per historical d20 span",
                    request.Coordinates.OperationKey);
            }

            MortalWoundCriticalReactionReservation? reactionReservation = null;
            MortalWoundCriticalReactionReservationAgreement? reactionAgreement = null;
            if (RequiresPlayerFateReservation(
                    dice.Reservation,
                    procedure.RollMode,
                    procedure.RollActorKind,
                    procedure.RollActorId))
            {
                if (!restoredDice.TryGetPotentialPlayerNaturalOneClaimSpansBefore(
                        DicePoolReadCapability,
                        dice.Reservation,
                        acceptedD20EventValues,
                        out var potentialClaimSpans))
                {
                    return Failure(
                        "mortal_wound_treatment_procedure_claim_recovery_invalid",
                        "producer-reachable accepted d20 gap evidence",
                        request.Coordinates.OperationKey);
                }
                var newPotentialClaimSpans = potentialClaimSpans
                    .Where(candidate => virtualHistoricalFateClaims.All(existing =>
                        !candidate.Overlaps(existing.SourceSpan)))
                    .ToArray();
                var historicalBefore = new HashSet<string>(
                    historicallyClaimedReactionEffectIds,
                    StringComparer.Ordinal);
                var maximumHistoricallyClaimedOlderCandidates = checked(
                    virtualHistoricalFateClaims.Count +
                    newPotentialClaimSpans.Length);
                var reaction = restoredReactions.RestoreExact(
                    request.Coordinates,
                    acceptedState.EffectMechanics.FateShieldReactionCandidates,
                    procedure.PreparedCriticalReaction,
                    maximumHistoricallyClaimedOlderCandidates,
                    historicallyClaimedReactionEffectIds);
                if (!reaction.IsValid || reaction.Agreement is null)
                    return Failure(reaction.Issues);

                var newlyVirtualEffectIds = acceptedState.EffectMechanics
                    .FateShieldReactionCandidates
                    .OrderBy(static candidate => candidate.CreatedAtTurn)
                    .ThenBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
                    .Where(candidate =>
                        historicallyClaimedReactionEffectIds.Contains(
                            candidate.EffectId) &&
                        !historicalBefore.Contains(candidate.EffectId))
                    .Select(static candidate => candidate.EffectId)
                    .ToArray();
                if (newlyVirtualEffectIds.Length > newPotentialClaimSpans.Length)
                {
                    return Failure(
                        "mortal_wound_treatment_procedure_claim_recovery_invalid",
                        "one newly skipped Fate candidate per new historical d20 span",
                        request.Coordinates.OperationKey);
                }
                for (var index = 0; index < newlyVirtualEffectIds.Length; index++)
                {
                    virtualHistoricalFateClaims.Add(new VirtualHistoricalFateClaim(
                        newPotentialClaimSpans[index],
                        newlyVirtualEffectIds[index]));
                }
                if (!HasExactVirtualClaimAgreement(
                        virtualHistoricalFateClaims,
                        historicallyClaimedReactionEffectIds))
                {
                    return Failure(
                        "mortal_wound_treatment_procedure_claim_recovery_invalid",
                        "one exact virtual Fate candidate bound to each active historical d20 span",
                        request.Coordinates.OperationKey);
                }
                reactionReservation = reaction.Reservation;
                reactionAgreement = reaction.Agreement;
            }
            else if (procedure.PreparedCriticalReaction is not null)
            {
                return Failure(
                    "mortal_wound_treatment_critical_reaction_restore_mismatch",
                    "no prepared Fate reaction outside a player natural one",
                    request.Coordinates.OperationKey);
            }

            var attachedProcedure = procedure.AttachRestoredReservations(
                dice.Reservation,
                reactionReservation,
                reactionAgreement);
            var attachedRequest = request.AttachRestoredProcedureAuthority(
                attachedProcedure);
            if (attachedRequest is null)
            {
                return Failure(
                    "mortal_wound_treatment_procedure_claim_recovery_invalid",
                    "one request preserving its complete public seal",
                    request.Coordinates.OperationKey);
            }
            restoredRequests[requestIndex] = attachedRequest;
        }

        return new MortalWoundProcedureClaimRecoveryBatchResult(
            true,
            Array.Empty<ValidationIssue>(),
            restoredRequests,
            restoredDice,
            restoredReactions);
    }

    private static bool ReleaseOverlappedVirtualClaims(
        IReadOnlyList<int> currentSourceIndices,
        IList<VirtualHistoricalFateClaim> virtualClaims,
        ISet<string> historicallyClaimedEffectIds)
    {
        for (var index = virtualClaims.Count - 1; index >= 0; index--)
        {
            var claim = virtualClaims[index];
            if (!claim.SourceSpan.Overlaps(currentSourceIndices))
                continue;
            if (!historicallyClaimedEffectIds.Remove(claim.EffectId))
                return false;
            virtualClaims.RemoveAt(index);
        }
        return true;
    }

    private static bool HasExactVirtualClaimAgreement(
        IReadOnlyList<VirtualHistoricalFateClaim> virtualClaims,
        ISet<string> historicallyClaimedEffectIds)
    {
        if (virtualClaims.Count != historicallyClaimedEffectIds.Count ||
            virtualClaims.Select(static claim => claim.EffectId)
                .Distinct(StringComparer.Ordinal).Count() != virtualClaims.Count)
        {
            return false;
        }
        for (var left = 0; left < virtualClaims.Count; left++)
        {
            if (!historicallyClaimedEffectIds.Contains(
                    virtualClaims[left].EffectId))
                return false;
            for (var right = left + 1; right < virtualClaims.Count; right++)
            {
                if (virtualClaims[left].SourceSpan.Overlaps(
                        virtualClaims[right].SourceSpan))
                    return false;
            }
        }
        return true;
    }

    private static bool RequiresPlayerFateReservation(
        MortalWoundProcedureDiceReservation reservation,
        string rollMode,
        string rollActorKind,
        string rollActorId)
    {
        var selectedOrdinal = reservation.SourceRolls.Count == 1
            ? 0
            : rollMode switch
            {
                "advantage" => reservation.SourceRolls[1] > reservation.SourceRolls[0]
                    ? 1
                    : 0,
                "disadvantage" => reservation.SourceRolls[1] < reservation.SourceRolls[0]
                    ? 1
                    : 0,
                _ => -1
            };
        return selectedOrdinal >= 0 &&
               reservation.SourceRolls[selectedOrdinal] == 1 &&
               string.Equals(rollActorKind, "player", StringComparison.Ordinal) &&
               string.Equals(rollActorId, "player_current", StringComparison.Ordinal);
    }

    private static MortalWoundProcedureClaimRecoveryBatchResult Failure(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        issues,
        Array.Empty<MortalWoundTreatmentAttemptRequest>(),
        null,
        null);

    private static MortalWoundProcedureClaimRecoveryBatchResult Failure(
        string code,
        string expected,
        string actual) => Failure(new[]
        {
            new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "The persisted die and Fate claims for Mortal wound treatment cannot be restored.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual)
        });
}
