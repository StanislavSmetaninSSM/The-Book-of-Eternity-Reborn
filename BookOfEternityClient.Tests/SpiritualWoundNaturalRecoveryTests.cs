using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundNaturalRecoveryTests
{
    private static SpiritualRecoveryCalculationInput Input(int tier = 0, int rank = 4, long progress = 0)
        => new(tier, rank, progress);

    private static SpiritualRecoveryCalculation Calculate(SpiritualRecoveryCalculationInput input)
    {
        var actual = Assert.IsType<SpiritualRecoveryCalculation>(SpiritualWoundRecoveryMath.Calculate(input));
        Assert.Equal(input, actual.Input);
        Assert.Equal(1 + input.HealingTier, actual.PointsAdded);
        Assert.Equal(checked(input.CurrentStepProgress + actual.PointsAdded), actual.AvailablePoints);
        Assert.InRange(actual.StepsCompleted, 0, input.SeverityRank);
        Assert.InRange(actual.ResultingSeverityRank, 1, 4);
        Assert.InRange(actual.RemainingProgress, 0L, actual.ResultingThreshold - 1L);
        var spent = Enumerable.Range(0, actual.StepsCompleted)
            .Sum(step => 2L * (input.SeverityRank - step));
        Assert.Equal(actual.AvailablePoints, spent + actual.RemainingProgress + actual.UnusedPoints);
        return actual;
    }

    private static void AssertResult(SpiritualRecoveryCalculation actual,
        int steps, int rank, long progress, bool heals, long unused)
    {
        Assert.Equal(steps, actual.StepsCompleted);
        Assert.Equal(rank, actual.ResultingSeverityRank);
        Assert.Equal(progress, actual.RemainingProgress);
        Assert.Equal(2 * rank, actual.ResultingThreshold);
        Assert.Equal(heals, actual.HealsWound);
        Assert.Equal(unused, actual.UnusedPoints);
    }

    [Theory]
    [InlineData(0, 1, 1, 0, 1, 1, false, 0)]
    [InlineData(1, 1, 2, 1, 1, 0, true, 0)]
    [InlineData(2, 1, 3, 1, 1, 0, true, 1)]
    [InlineData(3, 1, 4, 1, 1, 0, true, 2)]
    [InlineData(4, 1, 5, 1, 1, 0, true, 3)]
    [InlineData(5, 1, 6, 1, 1, 0, true, 4)]
    [InlineData(0, 2, 1, 0, 2, 1, false, 0)]
    [InlineData(1, 2, 2, 0, 2, 2, false, 0)]
    [InlineData(2, 2, 3, 0, 2, 3, false, 0)]
    [InlineData(3, 2, 4, 1, 1, 0, false, 0)]
    [InlineData(4, 2, 5, 1, 1, 1, false, 0)]
    [InlineData(5, 2, 6, 2, 1, 0, true, 0)]
    [InlineData(0, 3, 1, 0, 3, 1, false, 0)]
    [InlineData(1, 3, 2, 0, 3, 2, false, 0)]
    [InlineData(2, 3, 3, 0, 3, 3, false, 0)]
    [InlineData(3, 3, 4, 0, 3, 4, false, 0)]
    [InlineData(4, 3, 5, 0, 3, 5, false, 0)]
    [InlineData(5, 3, 6, 1, 2, 0, false, 0)]
    [InlineData(0, 4, 1, 0, 4, 1, false, 0)]
    [InlineData(1, 4, 2, 0, 4, 2, false, 0)]
    [InlineData(2, 4, 3, 0, 4, 3, false, 0)]
    [InlineData(3, 4, 4, 0, 4, 4, false, 0)]
    [InlineData(4, 4, 5, 0, 4, 5, false, 0)]
    [InlineData(5, 4, 6, 0, 4, 6, false, 0)]
    public void EveryTierAddsOnePlusTier_WithoutAnActiveHealingTierGate(
        int tier, int rank, int added, int steps, int nextRank, long progress, bool heals, long unused)
    {
        var actual = Calculate(Input(tier, rank));
        Assert.Equal(added, actual.PointsAdded);
        AssertResult(actual, steps, nextRank, progress, heals, unused);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 1, false)]
    [InlineData(1, 1, 1, 1, 0, true)]
    [InlineData(2, 2, 0, 2, 3, false)]
    [InlineData(2, 3, 1, 1, 0, false)]
    [InlineData(3, 4, 0, 3, 5, false)]
    [InlineData(3, 5, 1, 2, 0, false)]
    [InlineData(4, 6, 0, 4, 7, false)]
    [InlineData(4, 7, 1, 3, 0, false)]
    public void ExactThresholdBoundary_UsesTheCurrentSeverity(
        int rank, long progress, int steps, int nextRank, long nextProgress, bool heals)
    {
        AssertResult(Calculate(Input(rank: rank, progress: progress)),
            steps, nextRank, nextProgress, heals, 0);
    }

    [Theory]
    [InlineData(4, 7, 5, 1, 3, 5, false, 0)]
    [InlineData(3, 5, 5, 2, 1, 1, false, 0)]
    [InlineData(2, 3, 5, 2, 1, 0, true, 3)]
    [InlineData(3, 6, 5, 3, 1, 0, true, 0)]
    [InlineData(4, 14, 5, 4, 1, 0, true, 0)]
    [InlineData(4, 20, 0, 4, 1, 0, true, 1)]
    [InlineData(3, 8, 0, 1, 2, 3, false, 0)]
    [InlineData(1, 2, 0, 1, 1, 0, true, 1)]
    public void CarriedProgress_CrossesEveryFollowingStepWithoutLoss(
        int rank, long progress, int tier, int steps, int nextRank, long nextProgress, bool heals, long unused)
    {
        AssertResult(Calculate(Input(tier, rank, progress)),
            steps, nextRank, nextProgress, heals, unused);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(5, 4, 4)]
    public void RepeatedNumericCycles_FromFourHealInTheApprovedCount(
        int tier, int expectedCycles, long unused)
    {
        var rank = 4;
        long progress = 0;
        var totalSteps = 0;
        SpiritualRecoveryCalculation? last = null;
        for (var cycle = 1; cycle <= expectedCycles; cycle++)
        {
            last = Calculate(Input(tier, rank, progress));
            Assert.Equal(cycle == expectedCycles, last.HealsWound);
            totalSteps += last.StepsCompleted;
            rank = last.ResultingSeverityRank;
            progress = last.RemainingProgress;
        }
        Assert.NotNull(last);
        Assert.Equal(4, totalSteps);
        Assert.Equal(1, rank);
        Assert.Equal(0L, progress);
        Assert.Equal(2, last.ResultingThreshold);
        Assert.Equal(unused, last.UnusedPoints);
    }

    [Fact]
    public void LargestRepresentableAggregate_CompletesInFourBoundedSteps()
    {
        var actual = Calculate(Input(tier: 5, progress: long.MaxValue - 6));
        Assert.Equal(long.MaxValue, actual.AvailablePoints);
        AssertResult(actual, 4, 1, 0, true, long.MaxValue - 20);
    }

    [Theory]
    [InlineData(-1, 4, 0)]
    [InlineData(6, 4, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 4, -1)]
    [InlineData(0, 4, long.MinValue)]
    public void InvalidDomain_RejectsWithoutClamping(int tier, int rank, long progress)
    {
        Assert.Null(SpiritualWoundRecoveryMath.Calculate(Input(tier, rank, progress)));
    }

    [Theory]
    [InlineData(0, long.MaxValue)]
    [InlineData(5, long.MaxValue - 5)]
    [InlineData(5, long.MaxValue)]
    public void InvalidArithmetic_RejectsOverflowRatherThanLosingProgress(int tier, long progress)
    {
        Assert.Null(SpiritualWoundRecoveryMath.Calculate(Input(tier: tier, progress: progress)));
    }
}
