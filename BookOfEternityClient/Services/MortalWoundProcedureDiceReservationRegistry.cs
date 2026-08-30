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
        string coordinatesFingerprint,
        string acceptedStateFingerprint,
        string rollMode,
        IReadOnlyList<int> sourceIndices,
        IReadOnlyList<int> sourceRolls)
    {
        OperationKey = operationKey;
        CoordinatesFingerprint = coordinatesFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        RollMode = rollMode;
        _sourceIndices = Array.AsReadOnly(sourceIndices.ToArray());
        _sourceRolls = Array.AsReadOnly(sourceRolls.ToArray());
    }

    internal string OperationKey { get; }
    internal string CoordinatesFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }
    internal string RollMode { get; }
    internal IReadOnlyList<int> SourceIndices => _sourceIndices;
    internal IReadOnlyList<int> SourceRolls => _sourceRolls;
}

internal sealed class MortalWoundProcedureDiceReservationRegistry
{
    private readonly Dictionary<string, MortalWoundProcedureDiceReservation> _byOperationKey =
        new(StringComparer.Ordinal);
    private readonly HashSet<int> _occupiedSourceIndices = new();

    internal MortalWoundProcedureDiceReservationResult Reserve(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string rollMode,
        ReadOnlySpan<int> acceptedD20EventValues)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

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

        if (_byOperationKey.TryGetValue(coordinates.OperationKey, out var existing))
        {
            return string.Equals(
                       existing.CoordinatesFingerprint,
                       coordinates.CoordinatesFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       existing.AcceptedStateFingerprint,
                       coordinates.AcceptedStateFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(existing.RollMode, rollMode, StringComparison.Ordinal) &&
                   existing.SourceIndices.Count == requiredCount
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
        var reservation = new MortalWoundProcedureDiceReservation(
            coordinates.OperationKey,
            coordinates.CoordinatesFingerprint,
            coordinates.AcceptedStateFingerprint,
            rollMode,
            indices,
            rolls);

        _byOperationKey.Add(coordinates.OperationKey, reservation);
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
        return _byOperationKey.TryGetValue(reservation.OperationKey, out var current) &&
               ReferenceEquals(current, reservation) &&
               reservation.SourceIndices.All(sourceIndex =>
                   _occupiedSourceIndices.Contains(sourceIndex));
    }

    internal void ReleaseUnchecked(MortalWoundProcedureDiceReservation reservation)
    {
        _byOperationKey.Remove(reservation.OperationKey);
        foreach (var sourceIndex in reservation.SourceIndices)
            _occupiedSourceIndices.Remove(sourceIndex);
    }

    internal void InvalidateAll()
    {
        _byOperationKey.Clear();
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
