using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundCriticalReactionReservationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundCriticalReactionReservation? Reservation,
    MortalWoundCriticalReactionReservationAgreement? Agreement = null,
    bool WasCreated = false);

internal sealed record MortalWoundProcedureReservationSetResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundProcedureDiceReservation? DiceReservation,
    MortalWoundCriticalReactionReservation? CriticalReactionReservation,
    MortalWoundCriticalReactionReservationAgreement? CriticalReactionAgreement = null,
    MortalWoundProcedureReservationOwnership? Ownership = null);

internal sealed class MortalWoundProcedureReservationOwnership
{
    private MortalWoundProcedureReservationOwnership(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
        bool diceWasCreated,
        bool criticalReactionWasCreated)
    {
        DiceReservation = diceReservation;
        CriticalReactionReservation = criticalReactionReservation;
        CriticalReactionAgreement = criticalReactionAgreement;
        DiceWasCreated = diceWasCreated;
        CriticalReactionWasCreated = criticalReactionWasCreated;
    }

    private MortalWoundProcedureDiceReservation DiceReservation { get; }
    private MortalWoundCriticalReactionReservation? CriticalReactionReservation { get; }
    private MortalWoundCriticalReactionReservationAgreement?
        CriticalReactionAgreement { get; }
    internal bool DiceWasCreated { get; }
    internal bool CriticalReactionWasCreated { get; }

    internal static MortalWoundProcedureReservationOwnership Create(
        object capability,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
        bool diceWasCreated,
        bool criticalReactionWasCreated)
    {
        if (!AcceptedTurnAuthorityRegistry.IsProcedureReservationOwnershipCapability(
                capability))
        {
            throw new InvalidOperationException(
                "Procedure reservation ownership requires registry authority.");
        }
        return new MortalWoundProcedureReservationOwnership(
            diceReservation,
            criticalReactionReservation,
            criticalReactionAgreement,
            diceWasCreated,
            criticalReactionWasCreated);
    }

    internal bool Matches(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement) =>
        ReferenceEquals(DiceReservation, diceReservation) &&
        ReferenceEquals(CriticalReactionReservation, criticalReactionReservation) &&
        ReferenceEquals(CriticalReactionAgreement, criticalReactionAgreement);
}

internal sealed class MortalWoundCriticalReactionReservationAgreement
{
    internal MortalWoundCriticalReactionReservationAgreement(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundCriticalReactionReservation? reservation)
    {
        OperationKey = coordinates.OperationKey;
        AttemptId = coordinates.AttemptId;
        CoordinatesFingerprint = coordinates.CoordinatesFingerprint;
        AcceptedStateFingerprint = coordinates.AcceptedStateFingerprint;
        Reservation = reservation;
    }

    internal string OperationKey { get; }
    internal string AttemptId { get; }
    internal string CoordinatesFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }
    internal MortalWoundCriticalReactionReservation? Reservation { get; }

    internal bool Agrees(MortalWoundTreatmentAttemptCoordinates coordinates) =>
        string.Equals(OperationKey, coordinates.OperationKey, StringComparison.Ordinal) &&
        string.Equals(AttemptId, coordinates.AttemptId, StringComparison.Ordinal) &&
        string.Equals(
            CoordinatesFingerprint,
            coordinates.CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AcceptedStateFingerprint,
            coordinates.AcceptedStateFingerprint,
            StringComparison.Ordinal);

    internal bool Agrees(MortalWoundProcedureDiceReservation reservation) =>
        string.Equals(OperationKey, reservation.OperationKey, StringComparison.Ordinal) &&
        string.Equals(AttemptId, reservation.AttemptId, StringComparison.Ordinal) &&
        string.Equals(
            CoordinatesFingerprint,
            reservation.CoordinatesFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(
            AcceptedStateFingerprint,
            reservation.AcceptedStateFingerprint,
            StringComparison.Ordinal);
}

internal sealed class MortalWoundCriticalReactionReservation
{
    internal MortalWoundCriticalReactionReservation(
        string operationKey,
        string attemptId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string effectId,
        string triggerId,
        string acceptedEffectFingerprint,
        string preparedReactionFingerprint,
        string claimFingerprint)
    {
        OperationKey = operationKey;
        AttemptId = attemptId;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        EffectId = effectId;
        TriggerId = triggerId;
        AcceptedEffectFingerprint = acceptedEffectFingerprint;
        PreparedReactionFingerprint = preparedReactionFingerprint;
        ClaimFingerprint = claimFingerprint;
    }

    internal string OperationKey { get; }
    internal string AttemptId { get; }
    internal string CoordinatesFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }
    internal string EffectId { get; }
    internal string TriggerId { get; }
    internal string AcceptedEffectFingerprint { get; }
    internal string PreparedReactionFingerprint { get; }
    internal string ClaimFingerprint { get; }
}

internal sealed class MortalWoundCriticalReactionReservationRegistry
{
    private readonly Dictionary<string, MortalWoundCriticalReactionReservationAgreement>
        _byOperationKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedEffectIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MortalWoundCriticalReactionReservationAgreement>
        _finalizedByOperationKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _spentEffectIds = new(StringComparer.Ordinal);

    internal bool IsEmpty =>
        _byOperationKey.Count == 0 &&
        _claimedEffectIds.Count == 0 &&
        _finalizedByOperationKey.Count == 0 &&
        _spentEffectIds.Count == 0;

    internal MortalWoundCriticalReactionReservationResult Reserve(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        IReadOnlyList<FateShieldReactionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(candidates);

        if (_finalizedByOperationKey.ContainsKey(coordinates.OperationKey))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_reservation_finalized",
                "a fresh operation that does not reuse a finalized Fate agreement",
                coordinates.OperationKey);
        }
        if (_byOperationKey.TryGetValue(coordinates.OperationKey, out var existing))
        {
            if (!existing.Agrees(coordinates))
            {
                return Invalid(
                    "mortal_wound_treatment_critical_reaction_reservation_conflict",
                    "the exact prior attempt, coordinates, accepted state, and Fate candidate",
                    coordinates.OperationKey);
            }
            if (existing.Reservation is null)
            {
                return new MortalWoundCriticalReactionReservationResult(
                    true,
                    Array.Empty<ValidationIssue>(),
                    null,
                    existing);
            }
            return candidates.Any(candidate =>
                    CandidateAgrees(existing.Reservation, candidate))
                ? Valid(existing.Reservation, existing)
                : Invalid(
                    "mortal_wound_treatment_critical_reaction_reservation_conflict",
                    "the exact prior attempt, coordinates, accepted state, and Fate candidate",
                    coordinates.OperationKey);
        }

        var eligibleCandidates = candidates
            .Where(candidate => !_spentEffectIds.Contains(candidate.EffectId))
            .ToArray();
        var candidate = FateShieldReactionArbiter.SelectOldest(
            eligibleCandidates,
            _claimedEffectIds);
        if (candidate is null)
        {
            var agreement = new MortalWoundCriticalReactionReservationAgreement(
                coordinates,
                reservation: null);
            _byOperationKey.Add(
                coordinates.OperationKey,
                agreement);
            return new MortalWoundCriticalReactionReservationResult(
                true,
                Array.Empty<ValidationIssue>(),
                null,
                agreement,
                WasCreated: true);
        }

        var preparedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
            "1",
            candidate.EffectId,
            candidate.TriggerId,
            candidate.AcceptedEffectFingerprint,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint
        });
        var claimFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.critical_reaction_claim",
            "1",
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            candidate.EffectId,
            candidate.TriggerId,
            candidate.AcceptedEffectFingerprint,
            preparedFingerprint
        });
        var reservation = new MortalWoundCriticalReactionReservation(
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            candidate.EffectId,
            candidate.TriggerId,
            candidate.AcceptedEffectFingerprint,
            preparedFingerprint,
            claimFingerprint);
        var reservationAgreement =
            new MortalWoundCriticalReactionReservationAgreement(
                coordinates,
                reservation);
        _byOperationKey.Add(coordinates.OperationKey, reservationAgreement);
        _claimedEffectIds.Add(candidate.EffectId);
        return Valid(
            reservation,
            reservationAgreement,
            wasCreated: true);
    }

    internal MortalWoundCriticalReactionReservationResult RestoreExact(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        IReadOnlyList<FateShieldReactionCandidate> candidates,
        MortalWoundPreparedCriticalReaction? preparedReaction,
        int maximumHistoricallyClaimedOlderCandidates,
        HashSet<string> historicallyClaimedEffectIds)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(historicallyClaimedEffectIds);
        if (maximumHistoricallyClaimedOlderCandidates < 0)
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                "a non-negative producer-reachability allowance",
                maximumHistoricallyClaimedOlderCandidates.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        if (_byOperationKey.ContainsKey(coordinates.OperationKey) ||
            _finalizedByOperationKey.ContainsKey(coordinates.OperationKey))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_reservation_conflict",
                "one exact restored Fate agreement per operation key",
                coordinates.OperationKey);
        }

        var candidateEffectIds = new HashSet<string>(StringComparer.Ordinal);
        if (candidates.Any(candidate =>
                candidate is null ||
                !candidateEffectIds.Add(candidate.EffectId)) ||
            historicallyClaimedEffectIds.Any(effectId =>
                !candidateEffectIds.Contains(effectId) ||
                _claimedEffectIds.Contains(effectId) ||
                _spentEffectIds.Contains(effectId)) ||
            historicallyClaimedEffectIds.Count >
                maximumHistoricallyClaimedOlderCandidates)
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                "one unique current Fate candidate topology within the available historical gap evidence",
                coordinates.OperationKey);
        }

        var remainingHistoricalClaims =
            maximumHistoricallyClaimedOlderCandidates -
            historicallyClaimedEffectIds.Count;
        var availableCandidates = candidates
            .Where(candidate =>
                !_claimedEffectIds.Contains(candidate.EffectId) &&
                !_spentEffectIds.Contains(candidate.EffectId) &&
                !historicallyClaimedEffectIds.Contains(candidate.EffectId))
            .OrderBy(static candidate => candidate.CreatedAtTurn)
            .ThenBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
            .ToArray();
        if (preparedReaction is null)
        {
            if (availableCandidates.Length > remainingHistoricalClaims)
            {
                return Invalid(
                    "mortal_wound_treatment_critical_reaction_restore_mismatch",
                    "the producer-selected oldest reachable Fate candidate",
                    "no prepared reaction");
            }
            var emptyAgreement = new MortalWoundCriticalReactionReservationAgreement(
                coordinates,
                reservation: null);
            _byOperationKey.Add(coordinates.OperationKey, emptyAgreement);
            foreach (var candidate in availableCandidates)
                historicallyClaimedEffectIds.Add(candidate.EffectId);
            return new MortalWoundCriticalReactionReservationResult(
                true,
                Array.Empty<ValidationIssue>(),
                null,
                emptyAgreement,
                WasCreated: true);
        }

        var matches = availableCandidates.Where(candidate =>
                string.Equals(candidate.EffectId, preparedReaction.EffectId,
                    StringComparison.Ordinal) &&
                string.Equals(candidate.TriggerId, preparedReaction.TriggerId,
                    StringComparison.Ordinal) &&
                string.Equals(candidate.AcceptedEffectFingerprint,
                    preparedReaction.AcceptedEffectFingerprint,
                    StringComparison.Ordinal))
            .ToArray();
        var preparedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
            "1",
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint
        });
        var selectedOrdinal = Array.FindIndex(
            availableCandidates,
            candidate => string.Equals(
                             candidate.EffectId,
                             preparedReaction.EffectId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             candidate.TriggerId,
                             preparedReaction.TriggerId,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             candidate.AcceptedEffectFingerprint,
                             preparedReaction.AcceptedEffectFingerprint,
                             StringComparison.Ordinal));
        if (matches.Length != 1 ||
            selectedOrdinal < 0 ||
            selectedOrdinal > remainingHistoricalClaims ||
            !string.Equals(
                preparedReaction.PreparedReactionFingerprint,
                preparedFingerprint,
                StringComparison.Ordinal) ||
            _claimedEffectIds.Contains(preparedReaction.EffectId) ||
            _spentEffectIds.Contains(preparedReaction.EffectId))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                "one exact current unclaimed Fate candidate and prepared reaction seal",
                preparedReaction.EffectId);
        }

        var claimFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.critical_reaction_claim",
            "1",
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            preparedFingerprint
        });
        var reservation = new MortalWoundCriticalReactionReservation(
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            preparedFingerprint,
            claimFingerprint);
        var agreement = new MortalWoundCriticalReactionReservationAgreement(
            coordinates,
            reservation);
        _byOperationKey.Add(coordinates.OperationKey, agreement);
        for (var index = 0; index < selectedOrdinal; index++)
            historicallyClaimedEffectIds.Add(availableCandidates[index].EffectId);
        _claimedEffectIds.Add(preparedReaction.EffectId);
        return Valid(reservation, agreement, wasCreated: true);
    }

    internal MortalWoundCriticalReactionReservationResult RestoreFinalized(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundPreparedCriticalReaction? preparedReaction)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        if (_byOperationKey.ContainsKey(coordinates.OperationKey))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_reservation_conflict",
                "one finalized Fate agreement per operation key",
                coordinates.OperationKey);
        }

        if (_finalizedByOperationKey.TryGetValue(
                coordinates.OperationKey,
                out var existing))
        {
            if (!existing.Agrees(coordinates) ||
                !FinalizedReservationAgrees(existing.Reservation, preparedReaction))
            {
                return Invalid(
                    "mortal_wound_treatment_critical_reaction_reservation_conflict",
                    "the exact persisted finalized Fate agreement",
                    coordinates.OperationKey);
            }
            if (existing.Reservation is null)
            {
                return new MortalWoundCriticalReactionReservationResult(
                    true,
                    Array.Empty<ValidationIssue>(),
                    null,
                    existing);
            }
            return Valid(existing.Reservation, existing);
        }

        if (preparedReaction is null)
        {
            var emptyAgreement = new MortalWoundCriticalReactionReservationAgreement(
                coordinates,
                reservation: null);
            _finalizedByOperationKey.Add(coordinates.OperationKey, emptyAgreement);
            return new MortalWoundCriticalReactionReservationResult(
                true,
                Array.Empty<ValidationIssue>(),
                null,
                emptyAgreement,
                WasCreated: true);
        }

        var preparedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
            "1",
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint
        });
        if (!string.Equals(
                preparedReaction.PreparedReactionFingerprint,
                preparedFingerprint,
                StringComparison.Ordinal) ||
            _claimedEffectIds.Contains(preparedReaction.EffectId) ||
            _spentEffectIds.Contains(preparedReaction.EffectId))
        {
            return Invalid(
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                "one exact unclaimed persisted finalized Fate reaction",
                preparedReaction.EffectId);
        }

        var claimFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.critical_reaction_claim",
            "1",
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            preparedFingerprint
        });
        var reservation = new MortalWoundCriticalReactionReservation(
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            preparedReaction.EffectId,
            preparedReaction.TriggerId,
            preparedReaction.AcceptedEffectFingerprint,
            preparedFingerprint,
            claimFingerprint);
        var agreement = new MortalWoundCriticalReactionReservationAgreement(
            coordinates,
            reservation);
        _finalizedByOperationKey.Add(coordinates.OperationKey, agreement);
        _spentEffectIds.Add(preparedReaction.EffectId);
        return Valid(reservation, agreement, wasCreated: true);
    }

    internal bool CanRelease(MortalWoundCriticalReactionReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return _byOperationKey.TryGetValue(reservation.OperationKey, out var current) &&
               ReferenceEquals(current.Reservation, reservation) &&
               _claimedEffectIds.Contains(reservation.EffectId);
    }

    internal bool MatchesReleaseAgreement(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? reservation,
        MortalWoundCriticalReactionReservationAgreement? agreement)
    {
        ArgumentNullException.ThrowIfNull(diceReservation);
        if (_finalizedByOperationKey.ContainsKey(diceReservation.OperationKey))
            return false;
        if (!_byOperationKey.TryGetValue(
                diceReservation.OperationKey,
                out var current))
        {
            return reservation is null && agreement is null;
        }
        if (!ReferenceEquals(current, agreement) ||
            !current.Agrees(diceReservation) ||
            !ReferenceEquals(current.Reservation, reservation))
        {
            return false;
        }
        return reservation is null || CanRelease(reservation);
    }

    internal void ReleaseUnchecked(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservationAgreement? agreement)
    {
        if (_finalizedByOperationKey.ContainsKey(diceReservation.OperationKey))
            return;
        _byOperationKey.Remove(diceReservation.OperationKey);
        if (agreement?.Reservation is not null)
            _claimedEffectIds.Remove(agreement.Reservation.EffectId);
    }

    internal void InvalidateAll()
    {
        _byOperationKey.Clear();
        _claimedEffectIds.Clear();
        _finalizedByOperationKey.Clear();
        _spentEffectIds.Clear();
    }

    private static bool FinalizedReservationAgrees(
        MortalWoundCriticalReactionReservation? reservation,
        MortalWoundPreparedCriticalReaction? preparedReaction)
    {
        if (reservation is null || preparedReaction is null)
            return reservation is null && preparedReaction is null;
        return string.Equals(
                   reservation.EffectId,
                   preparedReaction.EffectId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   reservation.TriggerId,
                   preparedReaction.TriggerId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   reservation.AcceptedEffectFingerprint,
                   preparedReaction.AcceptedEffectFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   reservation.PreparedReactionFingerprint,
                   preparedReaction.PreparedReactionFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool CandidateAgrees(
        MortalWoundCriticalReactionReservation reservation,
        FateShieldReactionCandidate candidate) =>
        string.Equals(reservation.EffectId, candidate.EffectId, StringComparison.Ordinal) &&
        string.Equals(reservation.TriggerId, candidate.TriggerId, StringComparison.Ordinal) &&
        string.Equals(
            reservation.AcceptedEffectFingerprint,
            candidate.AcceptedEffectFingerprint,
            StringComparison.Ordinal);

    private static MortalWoundCriticalReactionReservationResult Valid(
        MortalWoundCriticalReactionReservation reservation,
        MortalWoundCriticalReactionReservationAgreement agreement,
        bool wasCreated = false) => new(
        true,
        Array.Empty<ValidationIssue>(),
        reservation,
        agreement,
        wasCreated);

    private static MortalWoundCriticalReactionReservationResult Invalid(
        string code,
        string expected,
        string actual) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(new[]
        {
            new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "The Fate Shield reservation for the Mortal wound procedure cannot be trusted.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual)
        }),
        null);

}
