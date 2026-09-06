namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualRecoveryCalculationInput(
    int HealingTier, int SeverityRank, long CurrentStepProgress);

internal sealed record SpiritualRecoveryCalculation(
    SpiritualRecoveryCalculationInput Input,
    int PointsAdded,
    long AvailablePoints,
    int StepsCompleted,
    int ResultingSeverityRank,
    long RemainingProgress,
    int ResultingThreshold,
    bool HealsWound,
    long UnusedPoints);

internal static class SpiritualWoundRecoveryMath
{
    internal static SpiritualRecoveryCalculation? Calculate(SpiritualRecoveryCalculationInput input)
    {
        if (input.HealingTier is < 0 or > 5
            || input.SeverityRank is < 1 or > 4
            || input.CurrentStepProgress < 0)
        {
            return null;
        }

        var added = 1 + input.HealingTier;
        long available;
        try
        {
            available = checked(input.CurrentStepProgress + added);
        }
        catch (OverflowException)
        {
            return null;
        }
        var remaining = available;
        var rank = input.SeverityRank;
        var steps = 0;
        while (rank > 0 && remaining >= 2L * rank)
        {
            remaining -= 2L * rank;
            steps++;
            if (rank == 1)
                return new(input, added, available, steps, 1, 0, 2, true, remaining);
            rank--;
        }
        return new(input, added, available, steps, rank, remaining, 2 * rank, false, 0);
    }
}
