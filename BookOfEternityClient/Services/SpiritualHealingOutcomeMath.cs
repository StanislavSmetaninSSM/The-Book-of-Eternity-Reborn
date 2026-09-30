namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualHealingCalculationInput(
    int HealingTier, int WoundSeverityRank, int NaturalRoll,
    long ValidatedModifiers, long ComplicationModifier);

internal enum SpiritualHealingResultBand
{
    InsufficientTier,
    NoImprovement,
    RecoveryPoint,
    ReduceOne,
    ReduceTwo
}

// Recomputable values only: never an accepted roll or wound-transition authority.
internal sealed record SpiritualHealingCalculation(
    SpiritualHealingCalculationInput Input,
    bool HasSufficientTier,
    long? Total,
    long? Difficulty,
    long? Margin,
    SpiritualHealingResultBand ResultBand,
    int ImprovementSteps,
    int ResultingSeverityRank,
    bool HealsWound,
    int RecoveryPointsAdded);

internal static class SpiritualHealingOutcomeMath
{
    internal static SpiritualHealingCalculation? Calculate(SpiritualHealingCalculationInput input)
    {
        if (input.HealingTier is < 0 or > 5
            || input.WoundSeverityRank is < 1 or > 4
            || input.NaturalRoll is < 1 or > 20
            || input.ComplicationModifier < 0)
        {
            return null;
        }

        if (input.HealingTier < input.WoundSeverityRank)
        {
            return new(input, false, null, null, null,
                SpiritualHealingResultBand.InsufficientTier, 0, input.WoundSeverityRank, false, 0);
        }

        long total;
        long difficulty;
        long margin;
        try
        {
            total = checked(input.ValidatedModifiers + (input.NaturalRoll + 2L * input.HealingTier));
            difficulty = checked(input.ComplicationModifier + (10L + 2L * input.WoundSeverityRank));
            margin = checked(total - difficulty);
        }
        catch (OverflowException)
        {
            return null;
        }
        var band = input.NaturalRoll == 1 ? SpiritualHealingResultBand.NoImprovement
            : input.NaturalRoll == 20 || margin >= 8 ? SpiritualHealingResultBand.ReduceTwo
            : margin >= 0 ? SpiritualHealingResultBand.ReduceOne
            : margin >= -4 ? SpiritualHealingResultBand.RecoveryPoint
            : SpiritualHealingResultBand.NoImprovement;
        var requestedSteps = band switch
        {
            SpiritualHealingResultBand.ReduceTwo => 2,
            SpiritualHealingResultBand.ReduceOne => 1,
            _ => 0
        };
        var steps = Math.Min(requestedSteps, input.WoundSeverityRank);
        return new(input, true, total, difficulty, margin, band, steps,
            Math.Max(1, input.WoundSeverityRank - steps),
            steps >= input.WoundSeverityRank,
            band == SpiritualHealingResultBand.RecoveryPoint ? 1 : 0);
    }
}
