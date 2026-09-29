using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks threshold policy, bounded tier traversal and checked interval arithmetic.
/// </summary>
public sealed class MortalWoundRecoveryProgressProgramTests
{
    /// <summary>
    /// Retains every severity boundary and the separate terminal healing stage.
    /// </summary>
    [Fact]
    public void NewlyElapsedIntervals_CrossAllTiersAndRetainSeparateFullHeal()
    {
        var stages = MortalWoundRecoveryProgressProgram.Create(4, 0, 2, 9, true);

        Assert.Equal(new[] { 3, 2, 1, 1, 1 }, stages.Select(stage => stage.SeverityRank));
        Assert.Equal(new long[] { 7, 5, 3, 1, 1 }, stages.Select(stage => stage.Progress));
        Assert.All(stages.Take(4), stage => Assert.False(stage.FullHeal));
        Assert.True(stages[^1].FullHeal);
    }

    /// <summary>
    /// Discards excess points after the first threshold when overflow carry is disabled.
    /// </summary>
    [Fact]
    public void DisabledOverflow_DiscardsRemainderAfterFirstThreshold()
    {
        var stage = Assert.Single(MortalWoundRecoveryProgressProgram.Create(4, 1, 2, 9, false));

        Assert.Equal(3, stage.SeverityRank);
        Assert.Equal(0, stage.Progress);
        Assert.False(stage.FullHeal);
    }

    /// <summary>
    /// Requires a newly consumed interval before applying existing threshold progress.
    /// </summary>
    [Fact]
    public void UnconsumedIntervalAbsence_DoesNotReapplyExistingThresholdProgress()
    {
        Assert.Empty(MortalWoundRecoveryProgressProgram.Create(2, 3, 2, 0, true));
        var stage = Assert.Single(MortalWoundRecoveryProgressProgram.Create(2, 0, 3, 1, true));
        Assert.Equal(2, stage.SeverityRank);
        Assert.Equal(1, stage.Progress);
    }

    /// <summary>
    /// Rejects addition overflow and keeps arbitrarily large elapsed counts bounded by tier count.
    /// </summary>
    [Fact]
    public void CheckedProgressOverflow_RejectsBeforeAnyStageCanPublish()
    {
        Assert.Throws<OverflowException>(() =>
            MortalWoundRecoveryProgressProgram.Create(4, long.MaxValue, 2, 1, true));
        var bounded = MortalWoundRecoveryProgressProgram.Create(4, 0, 1, long.MaxValue, true);
        Assert.Equal(5, bounded.Length);
        Assert.True(bounded[^1].FullHeal);
    }
}
