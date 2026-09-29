using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

/// <summary>
/// Describes one scalar recovery boundary whose effects and transition must still
/// be accepted by the owning unpublished execution.
/// </summary>
/// <param name="SeverityRank">
/// The resulting active tier before separate full healing.
/// </param>
/// <param name="Progress">
/// The checked remaining points after this boundary.
/// </param>
/// <param name="FullHeal">
/// Whether this is the separate terminal healing boundary.
/// </param>
internal sealed record MortalWoundRecoveryProgressStage(
    int SeverityRank,
    long Progress,
    bool FullHeal);

/// <summary>
/// Computes the bounded natural-progress sequence without allocating identities,
/// changing anchors, or publishing wound or effect state.
/// </summary>
internal static class MortalWoundRecoveryProgressProgram
{
    /// <summary>
    /// Applies newly consumed recovery intervals to the declared threshold policy.
    /// The result contains at most one boundary per source severity tier and a
    /// separate full-heal boundary when severity I reaches its threshold.
    /// </summary>
    /// <param name="severityRank">
    /// The active source rank, from I through IV.
    /// </param>
    /// <param name="currentProgress">
    /// The non-negative progress already accepted for the current tier.
    /// </param>
    /// <param name="threshold">
    /// The strictly positive declared threshold used at every resulting tier.
    /// </param>
    /// <param name="elapsedCadences">
    /// Newly elapsed, unconsumed intervals. Zero produces no progress boundary.
    /// </param>
    /// <param name="carryOverflow">
    /// Whether progress beyond a reached threshold continues into the next tier.
    /// </param>
    /// <returns>
    /// An immutable ordered sequence of scalar recovery and optional healing boundaries.
    /// </returns>
    internal static ImmutableArray<MortalWoundRecoveryProgressStage> Create(
        int severityRank,
        long currentProgress,
        long threshold,
        long elapsedCadences,
        bool carryOverflow)
    {
        if (severityRank is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(severityRank));
        if (currentProgress < 0)
            throw new ArgumentOutOfRangeException(nameof(currentProgress));
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (elapsedCadences < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedCadences));
        if (elapsedCadences == 0)
            return ImmutableArray<MortalWoundRecoveryProgressStage>.Empty;

        var progress = checked(currentProgress + elapsedCadences);
        var stages = ImmutableArray.CreateBuilder<MortalWoundRecoveryProgressStage>();
        var rank = severityRank;
        while (progress >= threshold)
        {
            progress = carryOverflow ? checked(progress - threshold) : 0;
            if (rank == 1)
            {
                // A recover boundary stages the explicit I-threshold result;
                // the existing heal reducer subsequently owns terminalization.
                stages.Add(new MortalWoundRecoveryProgressStage(1, progress, false));
                stages.Add(new MortalWoundRecoveryProgressStage(1, progress, true));
                return stages.ToImmutable();
            }
            rank--;
            stages.Add(new MortalWoundRecoveryProgressStage(rank, progress, false));
            if (!carryOverflow)
                return stages.ToImmutable();
        }
        if (stages.Count == 0)
            stages.Add(new MortalWoundRecoveryProgressStage(rank, progress, false));
        return stages.ToImmutable();
    }
}
