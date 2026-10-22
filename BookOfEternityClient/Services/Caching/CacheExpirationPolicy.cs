namespace BookOfEternityClient.Services.Caching;

/// <summary>
/// Defines positive retention lifetimes and a periodic idle-eviction interval.
/// </summary>
public sealed class CacheExpirationPolicy
{
    /// <summary>
    /// Creates a policy whose configured lifetimes bound retention together.
    /// </summary>
    /// <param name="absoluteLifetime">
    /// The positive lifetime from each replacement, or null to omit this limit.
    /// </param>
    /// <param name="slidingLifetime">
    /// The positive lifetime from the last successful read, or null to omit this limit.
    /// </param>
    /// <param name="sweepInterval">
    /// The positive interval between idle expiration sweeps.
    /// </param>
    public CacheExpirationPolicy(TimeSpan? absoluteLifetime, TimeSpan? slidingLifetime, TimeSpan sweepInterval)
    {
        AbsoluteLifetime = absoluteLifetime;
        SlidingLifetime = slidingLifetime;
        SweepInterval = sweepInterval;
    }

    /// <summary>
    /// Gets the fixed lifetime from replacement, or null when it is unlimited.
    /// </summary>
    public TimeSpan? AbsoluteLifetime { get; }

    /// <summary>
    /// Gets the lifetime since a successful lookup, or null when it is unlimited.
    /// </summary>
    public TimeSpan? SlidingLifetime { get; }

    /// <summary>
    /// Gets the interval at which the backend removes idle expired entries.
    /// </summary>
    public TimeSpan SweepInterval { get; }
}
