using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualHealingResolverTests
{
    private static SpiritualHealingCalculationInput Input(
        int tier = 3, int rank = 3, int roll = 10,
        long modifiers = 0, long complications = 0)
        => new(tier, rank, roll, modifiers, complications);

    private static SpiritualHealingCalculation Calculate(SpiritualHealingCalculationInput input)
        => Assert.IsType<SpiritualHealingCalculation>(SpiritualHealingOutcomeMath.Calculate(input));

    [Theory]
    [InlineData(-6, "NoImprovement", 0, 0, 3)]
    [InlineData(-5, "NoImprovement", 0, 0, 3)]
    [InlineData(-4, "RecoveryPoint", 0, 1, 3)]
    [InlineData(-1, "RecoveryPoint", 0, 1, 3)]
    [InlineData(0, "ReduceOne", 1, 0, 2)]
    [InlineData(7, "ReduceOne", 1, 0, 2)]
    [InlineData(8, "ReduceTwo", 2, 0, 1)]
    [InlineData(9, "ReduceTwo", 2, 0, 1)]
    public void ExactMarginBands_PreserveEveryBoundary(int margin, string band, int steps, int points, int resultingRank)
    {
        var input = Input(modifiers: margin);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input);
        Assert.True(actual.HasSufficientTier);
        Assert.Equal((long?)(16 + margin), actual.Total);
        Assert.Equal((long?)16, actual.Difficulty);
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(points, actual.RecoveryPointsAdded);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.False(actual.HealsWound);
    }

    [Theory]
    [InlineData(3, 2, 14, 0, 1, 20, 15, 5)]
    [InlineData(5, 4, 10, -3, 2, 17, 20, -3)]
    [InlineData(2, 1, 5, 4, 0, 13, 12, 1)]
    [InlineData(5, 1, 10, 0, 0, 20, 12, 8)]
    [InlineData(1, 1, 10, -8, 0, 4, 12, -8)]
    public void ExactAudit_UsesTierSignedModifiersAndComplicationAggregate(int tier, int rank, int roll, int modifiers, int complications, int total, int difficulty, int margin)
    {
        var input = Input(tier, rank, roll, modifiers, complications);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input);
        Assert.True(actual.HasSufficientTier);
        Assert.Equal((long?)total, actual.Total);
        Assert.Equal((long?)difficulty, actual.Difficulty);
        Assert.Equal((long?)margin, actual.Margin);
    }

    [Theory]
    [InlineData(5, 4, 1, 100, 0, 93, "NoImprovement", 0, 4, false)]
    [InlineData(1, 1, 1, 100, 0, 91, "NoImprovement", 0, 1, false)]
    [InlineData(4, 4, 20, -100, 5, -95, "ReduceTwo", 2, 2, false)]
    [InlineData(1, 1, 20, -100, 0, -90, "ReduceTwo", 1, 1, true)]
    public void NaturalResults_OverrideOppositeNumericBandOnlyAfterTierGate(int tier, int rank, int roll, int modifiers, int complications, int margin, string band, int steps, int resultingRank, bool heals)
    {
        var actual = Calculate(Input(tier, rank, roll, modifiers, complications));
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(0, actual.RecoveryPointsAdded);
    }

    [Theory]
    [InlineData(1, 0, "ReduceOne", 1, 1, true)]
    [InlineData(2, 0, "ReduceOne", 1, 1, false)]
    [InlineData(3, 0, "ReduceOne", 1, 2, false)]
    [InlineData(4, 0, "ReduceOne", 1, 3, false)]
    [InlineData(1, 8, "ReduceTwo", 1, 1, true)]
    [InlineData(2, 8, "ReduceTwo", 2, 1, true)]
    [InlineData(3, 8, "ReduceTwo", 2, 1, false)]
    [InlineData(4, 8, "ReduceTwo", 2, 2, false)]
    public void BoundedImprovement_DistinguishesActiveOneFromHealedOne(int rank, int margin, string band, int steps, int resultingRank, bool heals)
    {
        var actual = Calculate(Input(tier: 5, rank: rank, modifiers: 10 + 2 * rank - 20 + margin));
        Assert.Equal((long?)margin, actual.Margin);
        Assert.Equal(band, actual.ResultBand.ToString());
        Assert.Equal(steps, actual.ImprovementSteps);
        Assert.Equal(resultingRank, actual.ResultingSeverityRank);
        Assert.InRange(actual.ResultingSeverityRank, 1, 4);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(0, actual.RecoveryPointsAdded);
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(0, 2)] [InlineData(0, 3)] [InlineData(0, 4)]
    [InlineData(1, 2)] [InlineData(1, 3)] [InlineData(1, 4)] [InlineData(2, 3)]
    [InlineData(2, 4)] [InlineData(3, 4)]
    public void InsufficientTier_IsExplicitAndNeverBypassedByNaturalTwenty(int tier, int rank)
    {
        var input = Input(tier, rank, roll: 20, modifiers: 100);
        var actual = Calculate(input);
        Assert.Equal(input, actual.Input); Assert.False(actual.HasSufficientTier);
        Assert.Equal(SpiritualHealingResultBand.InsufficientTier, actual.ResultBand);
        Assert.Null(actual.Total); Assert.Null(actual.Difficulty); Assert.Null(actual.Margin);
        Assert.Equal(0, actual.ImprovementSteps); Assert.Equal(0, actual.RecoveryPointsAdded);
        Assert.Equal(rank, actual.ResultingSeverityRank); Assert.False(actual.HealsWound);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 3)] [InlineData(4, 4)]
    [InlineData(5, 1)] [InlineData(5, 2)] [InlineData(5, 3)] [InlineData(5, 4)]
    public void MatchingTierAndTierFive_AreSufficient(int tier, int rank)
    {
        var actual = Calculate(Input(tier, rank, roll: 20));
        Assert.True(actual.HasSufficientTier); Assert.Equal(SpiritualHealingResultBand.ReduceTwo, actual.ResultBand);
        Assert.Equal(Math.Min(2, rank), actual.ImprovementSteps);
        Assert.Equal(Math.Max(1, rank - 2), actual.ResultingSeverityRank);
        Assert.Equal(rank <= 2, actual.HealsWound);
    }

    [Fact]
    public void LongAuditBoundaries_RemainExactWithoutClamping()
    {
        var low = Calculate(Input(tier: 1, rank: 1, roll: 2, modifiers: long.MinValue + 8));
        Assert.Equal((long?)(long.MinValue + 12), low.Total); Assert.Equal((long?)12, low.Difficulty);
        Assert.Equal((long?)long.MinValue, low.Margin); Assert.Equal(SpiritualHealingResultBand.NoImprovement, low.ResultBand);
        var high = Calculate(Input(tier: 5, rank: 4, roll: 19, modifiers: long.MaxValue - 29));
        Assert.Equal((long?)long.MaxValue, high.Total); Assert.Equal((long?)(long.MaxValue - 18), high.Margin);
        Assert.Equal(SpiritualHealingResultBand.ReduceTwo, high.ResultBand);
        var difficulty = Calculate(Input(tier: 5, rank: 4, roll: 19, complications: long.MaxValue - 18));
        Assert.Equal((long?)long.MaxValue, difficulty.Difficulty); Assert.Equal((long?)(29 - long.MaxValue), difficulty.Margin);
        Assert.Equal(SpiritualHealingResultBand.NoImprovement, difficulty.ResultBand);
    }

    [Theory]
    [InlineData(-1, 3, 10, 0)] [InlineData(6, 3, 10, 0)] [InlineData(3, 0, 10, 0)]
    [InlineData(5, 5, 10, 0)] [InlineData(3, 3, 0, 0)] [InlineData(3, 3, 21, 0)]
    [InlineData(3, 3, 10, -1)] [InlineData(3, 3, 10, long.MinValue)]
    [InlineData(0, 1, 21, 0)] [InlineData(0, 1, 20, -1)]
    public void InvalidDomain_RejectsBeforeInsufficientTier(int tier, int rank, int roll, long complications)
    {
        Assert.Null(SpiritualHealingOutcomeMath.Calculate(Input(tier, rank, roll, complications: complications)));
    }

    [Theory]
    [InlineData(2, long.MaxValue, 0)] [InlineData(2, 0, long.MaxValue)]
    [InlineData(2, long.MinValue, 0)] [InlineData(20, long.MaxValue, 0)]
    public void InvalidArithmetic_RejectsOverflowInEveryAuditFieldEvenOnNaturalTwenty(int roll, long modifiers, long complications)
    {
        Assert.Null(SpiritualHealingOutcomeMath.Calculate(
            Input(tier: 4, rank: 4, roll: roll, modifiers: modifiers, complications: complications)));
    }
}
