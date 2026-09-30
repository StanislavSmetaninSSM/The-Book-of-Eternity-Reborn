using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundOpportunityTests
{
    private static SpiritualWoundCalculationInput Input(
        long margin = 0, int art = 0, int resilience = 0,
        string? before = "clear", string? after = "strained",
        string? mode = "hostile", int sourceCap = 4)
        => new(margin, art, resilience, before, after, mode, sourceCap);

    [Theory]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(12, 1)]
    [InlineData(13, 2)]
    [InlineData(17, 2)]
    [InlineData(18, 3)]
    [InlineData(22, 3)]
    [InlineData(23, 4)]
    public void PressureThresholds_UseEveryExactBoundary(int pressure, int expected)
    {
        var input = Input(margin: pressure - 6, before: "overwhelmed", after: "broken");
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(input));
        Assert.Equal(input, actual.Input);
        Assert.Equal((long)pressure, actual.TraumaPressure);
        Assert.Equal(expected, actual.FormulaSeverityRank);
        Assert.Equal(expected, actual.MaximumSeverityRank);
        Assert.Equal(3, actual.PreviousStrainRank);
        Assert.Equal(4, actual.NewStrainRank);
        Assert.Equal(0, actual.ExtraJumpSteps);
        Assert.True(actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData("clear", "clear", 0, 0, 0)]
    [InlineData("clear", "strained", 1, 0, 1)]
    [InlineData("strained", "fractured", 2, 0, 2)]
    [InlineData("fractured", "overwhelmed", 3, 0, 3)]
    [InlineData("overwhelmed", "broken", 4, 0, 4)]
    [InlineData("clear", "fractured", 2, 1, 2)]
    [InlineData("clear", "overwhelmed", 3, 2, 3)]
    [InlineData("clear", "broken", 4, 3, 4)]
    [InlineData("strained", "broken", 4, 2, 4)]
    [InlineData("broken", "broken", 4, 0, 0)]
    [InlineData("broken", "strained", 1, 0, 0)]
    public void ExactDestinationAndExtraJumps_RequireIncreasingStrain(
        string before, string after, int rank, int jumps, int maximum)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: before, after: after)));
        Assert.Equal(rank, actual.NewStrainRank);
        Assert.Equal(rank, actual.DestinationSeverityCap);
        Assert.Equal(jumps, actual.ExtraJumpSteps);
        Assert.Equal(30L + 2 * (rank - 1) + 3 * jumps, actual.TraumaPressure);
        Assert.Equal(maximum, actual.MaximumSeverityRank);
        Assert.Equal(maximum > 0, actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData("training", 0)]
    [InlineData("controlled", 2)]
    [InlineData("hostile", 4)]
    [InlineData("annihilation", 4)]
    public void DangerModes_OnlyCapAndNeverForceWounds(string mode, int cap)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: "overwhelmed", after: "broken", mode: mode)));
        Assert.Equal(cap, actual.DangerModeSeverityCap);
        Assert.Equal(cap, actual.MaximumSeverityRank);
        Assert.True(actual.HasIncreasingStrain);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SourceCap_IsNeverBypassed(int cap)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, before: "overwhelmed", after: "broken", sourceCap: cap)));
        Assert.Equal(cap, actual.MaximumSeverityRank);
        Assert.Equal(cap, actual.Input.SourceSeverityCap);
    }

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(1, 0, 12)]
    [InlineData(2, 0, 14)]
    [InlineData(3, 0, 16)]
    [InlineData(4, 0, 18)]
    [InlineData(0, 1, 8)]
    [InlineData(0, 2, 6)]
    [InlineData(0, 3, 4)]
    [InlineData(0, 4, 2)]
    [InlineData(5, 0, 20)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 5, 10)]
    public void ArtAndPassiveResilience_UseExactSignedTierDifference(int art, int resilience, int pressure)
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 10, art: art, resilience: resilience)));
        Assert.Equal((long)pressure, actual.TraumaPressure);
        Assert.Equal(art, actual.Input.AppliedArtTier);
        Assert.Equal(resilience, actual.Input.TargetResilienceTier);
    }

    [Fact]
    public void RawNegativeMargin_IsNotNormalizedOrClamped()
    {
        var actual = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: -7, art: 5, before: "clear", after: "broken")));
        Assert.Equal(-7L, actual.Input.HarmfulMargin);
        Assert.Equal(18L, actual.TraumaPressure);
        Assert.Equal(3, actual.MaximumSeverityRank);
    }

    [Fact]
    public void ExtremeIntMargins_DoNotWrapPressure()
    {
        var high = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: int.MaxValue, art: 5, after: "broken")));
        var low = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: int.MinValue, resilience: 5)));
        var opposite = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: -(long)int.MinValue, art: 5, after: "broken")));
        Assert.Equal((long)int.MaxValue + 25, high.TraumaPressure);
        Assert.Equal(4, high.MaximumSeverityRank);
        Assert.Equal((long)int.MinValue - 10, low.TraumaPressure);
        Assert.Equal(0, low.MaximumSeverityRank);
        Assert.Equal(2147483648L, opposite.Input.HarmfulMargin);
        Assert.Equal(2147483673L, opposite.TraumaPressure);
        Assert.Equal(4, opposite.MaximumSeverityRank);
        Assert.Equal(long.MaxValue, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MaxValue - 25,
                art: 5, after: "broken"))).TraumaPressure);
        Assert.Equal(long.MinValue, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MinValue + 10,
                resilience: 5))).TraumaPressure);
        Assert.Equal(long.MaxValue - 1, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MaxValue - 9,
                art: 5, after: "clear"))).TraumaPressure);
        Assert.Equal(long.MinValue + 4, Assert.IsType<SpiritualWoundCalculation>(
            SpiritualWoundOpportunityMath.Calculate(Input(margin: long.MinValue + 1,
                resilience: 1, after: "fractured"))).TraumaPressure);
    }

    [Theory]
    [InlineData(-1, 0, 4)]
    [InlineData(6, 0, 4)]
    [InlineData(0, -1, 4)]
    [InlineData(0, 6, 4)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 5)]
    [InlineData(int.MaxValue, int.MinValue, int.MaxValue)]
    public void InvalidNumericDomain_RejectsWithoutClamping(int art, int resilience, int cap)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: 30, art: art, resilience: resilience, after: "broken", sourceCap: cap)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("unknown", false)]
    [InlineData("unknown", true)]
    [InlineData("BROKEN", false)]
    [InlineData("BROKEN", true)]
    [InlineData(" broken ", false)]
    [InlineData(" broken ", true)]
    public void InvalidStrainToken_RejectsBothCoordinates(string? strain, bool destination)
    {
        var input = destination ? Input(after: strain) : Input(before: strain);
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("HOSTILE")]
    [InlineData(" hostile ")]
    public void InvalidDangerToken_RejectsWithoutHostileFallback(string? mode)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(Input(mode: mode)));
    }

    [Theory]
    [InlineData(long.MaxValue, 5, 0, "broken")]
    [InlineData(long.MinValue, 0, 5, "strained")]
    public void InvalidArithmetic_RejectsUnrepresentablePressureWithoutWrapping(
        long margin, int art, int resilience, string after)
    {
        Assert.Null(SpiritualWoundOpportunityMath.Calculate(
            Input(margin: margin, art: art, resilience: resilience, after: after)));
    }
}
