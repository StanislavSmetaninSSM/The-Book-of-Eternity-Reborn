using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundProcedureDiceReservationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundProcedureDiceReservation? Reservation,
    bool WasCreated = false);

internal sealed class MortalWoundProcedureDiceReservation
{
    private readonly ReadOnlyCollection<int> _sourceIndices;
    private readonly ReadOnlyCollection<int> _sourceRolls;

    internal MortalWoundProcedureDiceReservation(
        string operationKey,
        string attemptId,
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string poolFingerprint,
        string rollMode,
        IReadOnlyList<int> sourceIndices,
        IReadOnlyList<int> sourceRolls,
        string claimFingerprint)
    {
        OperationKey = operationKey;
        AttemptId = attemptId;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        PoolFingerprint = poolFingerprint;
        RollMode = rollMode;
        _sourceIndices = Array.AsReadOnly(sourceIndices.ToArray());
        _sourceRolls = Array.AsReadOnly(sourceRolls.ToArray());
        ClaimFingerprint = claimFingerprint;
    }

    internal string OperationKey { get; }
    internal string AttemptId { get; }
    internal string CoordinatesFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }
    internal string PoolFingerprint { get; }
    internal string RollMode { get; }
    internal IReadOnlyList<int> SourceIndices => _sourceIndices;
    internal IReadOnlyList<int> SourceRolls => _sourceRolls;
    internal string ClaimFingerprint { get; }
}

internal sealed class MortalWoundProcedureDiceReservationRegistry
{
    private readonly Dictionary<string, string> _coordinatesByOperationKey =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, MortalWoundProcedureDiceReservation>
        _byCoordinatesFingerprint = new(StringComparer.Ordinal);
    private readonly HashSet<int> _occupiedSourceIndices = new();

    internal MortalWoundProcedureDiceReservationResult Reserve(
        object acceptedPoolReadCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string rollMode,
        ReadOnlySpan<int> acceptedD20EventValues,
        string poolFingerprint)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (!AcceptedTurnAuthorityRegistry.IsProcedureDicePoolReadCapability(
                acceptedPoolReadCapability))
        {
            return Invalid(
                "mortal_wound_treatment_procedure_dice_authority_invalid",
                "registry-authorized access to the accepted d20 pool",
                "missing accepted-pool read authority");
        }

        var requiredCount = rollMode switch
        {
            "normal" => 1,
            "advantage" or "disadvantage" => 2,
            _ => 0
        };
        if (requiredCount == 0)
            return Invalid(
                "mortal_wound_treatment_procedure_dice_mode_invalid",
                "normal, advantage, or disadvantage",
                rollMode);
        if (acceptedD20EventValues.IsEmpty)
            return Invalid(
                "mortal_wound_treatment_procedure_dice_pool_exhausted",
                $"one contiguous free span of {requiredCount} accepted d20 value(s)",
                "empty accepted d20 pool");
        for (var index = 0; index < acceptedD20EventValues.Length; index++)
        {
            if (acceptedD20EventValues[index] is >= 1 and <= 20)
                continue;
            return Invalid(
                "mortal_wound_treatment_procedure_dice_pool_invalid",
                "accepted d20 values in range 1..20",
                $"index {index} has {acceptedD20EventValues[index]}");
        }

        if (_coordinatesByOperationKey.TryGetValue(
                coordinates.OperationKey,
                out var existingCoordinatesFingerprint) &&
            !string.Equals(
                existingCoordinatesFingerprint,
                coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal))
        {
            return Invalid(
                "mortal_wound_treatment_procedure_dice_reservation_conflict",
                "the exact prior coordinates and roll mode for this operation key",
                coordinates.OperationKey);
        }

        if (_byCoordinatesFingerprint.TryGetValue(
                coordinates.CoordinatesFingerprint,
                out var existing))
        {
            var sourceAgreement = existing.SourceIndices.Count == requiredCount;
            for (var offset = 0; sourceAgreement && offset < requiredCount; offset++)
            {
                sourceAgreement = existing.SourceIndices[offset] >= 0 &&
                    existing.SourceIndices[offset] < acceptedD20EventValues.Length &&
                    acceptedD20EventValues[existing.SourceIndices[offset]] ==
                    existing.SourceRolls[offset];
            }
            var expectedClaimFingerprint = ComputeClaimFingerprint(
                coordinates,
                poolFingerprint,
                rollMode,
                existing.SourceIndices,
                existing.SourceRolls);
            return string.Equals(
                       existing.OperationKey,
                       coordinates.OperationKey,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.AttemptId,
                       coordinates.AttemptId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.AcceptedStateFingerprint,
                       coordinates.AcceptedStateFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.PoolFingerprint,
                       poolFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(existing.RollMode, rollMode, StringComparison.Ordinal) &&
                   string.Equals(
                       existing.ClaimFingerprint,
                       expectedClaimFingerprint,
                       StringComparison.Ordinal) &&
                   sourceAgreement
                ? Valid(existing)
                : Invalid(
                    "mortal_wound_treatment_procedure_dice_reservation_conflict",
                    "the exact prior coordinates and roll mode for this operation key",
                    coordinates.OperationKey);
        }

        var start = FindFirstFreeSpan(acceptedD20EventValues.Length, requiredCount);
        if (start < 0)
        {
            return Invalid(
                "mortal_wound_treatment_procedure_dice_pool_exhausted",
                $"one contiguous free span of {requiredCount} accepted d20 value(s)",
                $"{_occupiedSourceIndices.Count} source index(es) already held");
        }

        var indices = new int[requiredCount];
        var rolls = new int[requiredCount];
        for (var offset = 0; offset < requiredCount; offset++)
        {
            indices[offset] = start + offset;
            rolls[offset] = acceptedD20EventValues[start + offset];
        }
        var claimFingerprint = ComputeClaimFingerprint(
            coordinates,
            poolFingerprint,
            rollMode,
            indices,
            rolls);
        var reservation = new MortalWoundProcedureDiceReservation(
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            poolFingerprint,
            rollMode,
            indices,
            rolls,
            claimFingerprint);

        _coordinatesByOperationKey.Add(
            coordinates.OperationKey,
            coordinates.CoordinatesFingerprint);
        _byCoordinatesFingerprint.Add(
            coordinates.CoordinatesFingerprint,
            reservation);
        foreach (var sourceIndex in indices)
            _occupiedSourceIndices.Add(sourceIndex);
        return Valid(reservation, wasCreated: true);
    }

    internal bool Release(MortalWoundProcedureDiceReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (!CanRelease(reservation))
            return false;
        ReleaseUnchecked(reservation);
        return true;
    }

    internal bool CanRelease(MortalWoundProcedureDiceReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return _coordinatesByOperationKey.TryGetValue(
                   reservation.OperationKey,
                   out var coordinatesFingerprint) &&
               string.Equals(
                   coordinatesFingerprint,
                   reservation.CoordinatesFingerprint,
                   StringComparison.Ordinal) &&
               _byCoordinatesFingerprint.TryGetValue(
                   reservation.CoordinatesFingerprint,
                   out var current) &&
               ReferenceEquals(current, reservation) &&
               reservation.SourceIndices.All(sourceIndex =>
                   _occupiedSourceIndices.Contains(sourceIndex));
    }

    internal void ReleaseUnchecked(MortalWoundProcedureDiceReservation reservation)
    {
        _coordinatesByOperationKey.Remove(reservation.OperationKey);
        _byCoordinatesFingerprint.Remove(reservation.CoordinatesFingerprint);
        foreach (var sourceIndex in reservation.SourceIndices)
            _occupiedSourceIndices.Remove(sourceIndex);
    }

    internal void InvalidateAll()
    {
        _coordinatesByOperationKey.Clear();
        _byCoordinatesFingerprint.Clear();
        _occupiedSourceIndices.Clear();
    }

    private int FindFirstFreeSpan(int poolCount, int requiredCount)
    {
        for (var start = 0; start <= poolCount - requiredCount; start++)
        {
            var available = true;
            for (var offset = 0; offset < requiredCount; offset++)
            {
                if (!_occupiedSourceIndices.Contains(start + offset))
                    continue;
                available = false;
                break;
            }
            if (available)
                return start;
        }
        return -1;
    }

    private static string ComputeClaimFingerprint(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string poolFingerprint,
        string rollMode,
        IReadOnlyList<int> sourceIndices,
        IReadOnlyList<int> sourceRolls)
    {
        var claimFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_dice_claim",
            "1",
            poolFingerprint,
            coordinates.AcceptedStateFingerprint,
            coordinates.OperationKey,
            coordinates.AttemptId,
            coordinates.CoordinatesFingerprint,
            rollMode,
            sourceIndices.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };
        for (var offset = 0; offset < sourceIndices.Count; offset++)
        {
            claimFields.Add(sourceIndices[offset].ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            claimFields.Add(sourceRolls[offset].ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(claimFields);
    }

    private static MortalWoundProcedureDiceReservationResult Valid(
        MortalWoundProcedureDiceReservation reservation,
        bool wasCreated = false) => new(
        true,
        Array.Empty<ValidationIssue>(),
        reservation,
        wasCreated);

    private static MortalWoundProcedureDiceReservationResult Invalid(
        string code,
        string expected,
        string actual) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(new[]
        {
            new ValidationIssue(
                LiveTurnPreparationService.TurnRequestPath,
                IssueSeverity.Error,
                "The accepted d20 reservation for the Mortal wound procedure cannot be trusted.",
                code: code,
                actor: "Client",
                section: "wound_materialization",
                expected: expected,
                actual: actual)
        }),
        null);
}
