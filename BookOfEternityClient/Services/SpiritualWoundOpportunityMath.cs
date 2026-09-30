namespace BookOfEternityClient.Services;

internal readonly record struct SpiritualWoundCalculationInput(
    long HarmfulMargin,
    int AppliedArtTier,
    int TargetResilienceTier,
    string? PreviousStrain,
    string? NewStrain,
    string? DangerMode,
    int SourceSeverityCap);

// A recomputable value result, never evidence authorizing a wound transition.
internal sealed record SpiritualWoundCalculation(
    SpiritualWoundCalculationInput Input,
    int PreviousStrainRank,
    int NewStrainRank,
    int ExtraJumpSteps,
    long TraumaPressure,
    int FormulaSeverityRank,
    int DestinationSeverityCap,
    int DangerModeSeverityCap,
    bool HasIncreasingStrain,
    int MaximumSeverityRank);

internal static class SpiritualWoundOpportunityMath
{
    internal static SpiritualWoundCalculation? Calculate(SpiritualWoundCalculationInput input)
    {
        var previous = StrainRank(input.PreviousStrain);
        var next = StrainRank(input.NewStrain);
        var modeCap = SpiritualConflictDangerPolicy.SeverityCap(input.DangerMode);
        if (previous < 0 || next < 0 || modeCap < 0
            || input.AppliedArtTier is < 0 or > 5
            || input.TargetResilienceTier is < 0 or > 5
            || input.SourceSeverityCap is < 0 or > 4)
        {
            return null;
        }
        var extra = Math.Max(0, next - previous - 1);
        var adjustment = 2L * (input.AppliedArtTier - (long)input.TargetResilienceTier)
            + 2L * (next - 1)
            + 3L * extra;
        long pressure;
        try
        {
            pressure = checked(input.HarmfulMargin + adjustment);
        }
        catch (OverflowException)
        {
            return null;
        }
        var formula = pressure switch
        {
            < 8 => 0,
            < 13 => 1,
            < 18 => 2,
            < 23 => 3,
            _ => 4
        };
        var increasing = next > previous;
        var maximum = increasing
            ? Math.Min(Math.Min(formula, next), Math.Min(modeCap, input.SourceSeverityCap))
            : 0;
        return new(input, previous, next, extra, pressure, formula, next, modeCap, increasing, maximum);
    }

    internal static int StrainRank(string? strain) => strain switch
    {
        "clear" => 0,
        "strained" => 1,
        "fractured" => 2,
        "overwhelmed" => 3,
        "broken" => 4,
        _ => -1
    };

}
