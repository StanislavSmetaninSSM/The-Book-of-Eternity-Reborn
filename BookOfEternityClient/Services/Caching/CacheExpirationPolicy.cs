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
    /// The positive lifetime from each replacement, or <see langword="null"/> to omit this limit.
    /// </param>
    /// <param name="slidingLifetime">
    /// The positive lifetime from the last successful read, or <see langword="null"/> to omit this limit.
    /// </param>
    /// <param name="sweepInterval">
    /// The positive interval between idle expiration sweeps.
    /// </param>
    public CacheExpirationPolicy(TimeSpan? absoluteLifetime, TimeSpan? slidingLifetime, TimeSpan sweepInterval)
    {
        if (absoluteLifetime is null && slidingLifetime is null)
            throw new ArgumentException("At least one expiration lifetime is required.");
        if (absoluteLifetime is { } absolute && absolute <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(absoluteLifetime), "The absolute lifetime must be positive.");
        if (slidingLifetime is { } sliding && sliding <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(slidingLifetime), "The sliding lifetime must be positive.");
        if (sweepInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sweepInterval), "The sweep interval must be positive.");
        AbsoluteLifetime = absoluteLifetime;
        SlidingLifetime = slidingLifetime;
        SweepInterval = sweepInterval;
    }

    /// <summary>
    /// Gets the fixed lifetime from replacement, or <see langword="null"/> when it is unlimited.
    /// </summary>
    public TimeSpan? AbsoluteLifetime { get; }

    /// <summary>
    /// Gets the lifetime since a successful lookup, or <see langword="null"/> when it is unlimited.
    /// </summary>
    public TimeSpan? SlidingLifetime { get; }

    /// <summary>
    /// Gets the requested interval for removing idle expired entries.
    /// Timer scheduling rounds to whole milliseconds and may use shorter intervals for long requests.
    /// </summary>
    public TimeSpan SweepInterval { get; }
}
