using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks bounded position arithmetic independently of wound ownership and canonical state mutation.
/// </summary>
public sealed class SpiritualWoundPositionTests
{
    /// <summary>
    /// Combines complete side sums before saturation, including equal extreme burdens and repeated evaluation.
    /// </summary>
    /// <param name="canonical">
    /// Unmodified starting rank.
    /// </param>
    /// <param name="player">
    /// Total player burden.
    /// </param>
    /// <param name="opposition">
    /// Total opposition burden.
    /// </param>
    /// <param name="expected">
    /// Required bounded rank.
    /// </param>
    [Theory]
    [InlineData(1, 1L, 0L, 0)]
    [InlineData(0, 1L, 2L, 1)]
    [InlineData(2, 4L, 3L, 1)]
    [InlineData(-2, 3L, 4L, -1)]
    [InlineData(0, 3L, 3L, 0)]
    [InlineData(2, 0L, 5L, 2)]
    [InlineData(-2, 5L, 0L, -2)]
    [InlineData(1, long.MaxValue, long.MaxValue, 1)]
    [InlineData(0, long.MaxValue, 0L, -2)]
    [InlineData(0, 0L, long.MaxValue, 2)]
    public void EffectivePosition_CombinesBeforeClampingWithoutCumulativeErosion(int canonical, long player, long opposition, int expected)
    {
        Assert.Equal(expected, ValidationService.ComputeSpiritualEffectivePosition(canonical, player, opposition));
        Assert.Equal(expected, ValidationService.ComputeSpiritualEffectivePosition(canonical, player, opposition));
    }

    /// <summary>
    /// Rejects unsupported ranks and negative burdens instead of converting them into a position advantage.
    /// </summary>
    /// <param name="canonical">
    /// Candidate canonical rank.
    /// </param>
    /// <param name="player">
    /// Candidate player burden.
    /// </param>
    /// <param name="opposition">
    /// Candidate opposition burden.
    /// </param>
    [Theory]
    [InlineData(-3, 0L, 0L)]
    [InlineData(3, 0L, 0L)]
    [InlineData(0, -1L, 0L)]
    [InlineData(0, 0L, -1L)]
    public void EffectivePosition_RejectsInvalidInputs(int canonical, long player, long opposition) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ValidationService.ComputeSpiritualEffectivePosition(canonical, player, opposition));
}
