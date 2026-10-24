using System.Collections.Concurrent;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Owns optional lifecycle read observers and passive canonical source counts.
/// </summary>
internal sealed class LifecycleFixtureReadObservers
{
    private readonly bool _forwardingEnabled;
    private readonly ConcurrentDictionary<string, long> _sources = new(StringComparer.Ordinal);
    private Func<string, Task>? _initial;
    private Func<string, Task>? _returned;

    /// <summary>
    /// Creates stable forwarding delegates for the fixture's lifetime.
    /// </summary>
    /// <param name="forwardingEnabled">
    /// Whether read callbacks may be attached, including after construction.
    /// </param>
    internal LifecycleFixtureReadObservers(bool forwardingEnabled = true)
    {
        _forwardingEnabled = forwardingEnabled;
        AfterInitialValidationAsync = path => _initial?.Invoke(path) ?? Task.CompletedTask;
        AfterReadAttemptAsync = path => _returned?.Invoke(path) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Gets the initial read hook, or null for an explicitly unobserved fixture.
    /// </summary>
    internal Func<string, Task>? AfterInitialValidationAsync { get; }

    /// <summary>
    /// Gets the returned read hook, or null for an explicitly unobserved fixture.
    /// </summary>
    internal Func<string, Task>? AfterReadAttemptAsync { get; }

    /// <summary>
    /// Gets or attaches the initial observer; disabled fixtures reject non-null attachment.
    /// </summary>
    internal Func<string, Task>? InitialObserver
    {
        get => _initial;
        set
        {
            if (!_forwardingEnabled && value != null)
                throw new InvalidOperationException("Read observation was disabled before fixture construction.");
            _initial = value;
        }
    }

    /// <summary>
    /// Gets or attaches the returned observer; disabled fixtures reject non-null attachment.
    /// </summary>
    internal Func<string, Task>? ReturnedObserver
    {
        get => _returned;
        set
        {
            if (!_forwardingEnabled && value != null)
                throw new InvalidOperationException("Read observation was disabled before fixture construction.");
            _returned = value;
        }
    }

    /// <summary>
    /// Records one actual canonical source event without retaining file data.
    /// </summary>
    /// <param name="path">
    /// The original relative source spelling supplied by the manager.
    /// </param>
    internal void ObserveCanonicalSource(string path) => _sources.AddOrUpdate(path, 1, (_, count) => count + 1);

    /// <summary>
    /// Counts all canonical source events for one exact path.
    /// </summary>
    /// <param name="path">
    /// The original relative source spelling.
    /// </param>
    /// <returns>
    /// The complete count, or zero when the path has not been observed.
    /// </returns>
    internal long ReadCount(string path) => _sources.GetValueOrDefault(path);

    /// <summary>
    /// Copies every source count without imposing a record or byte quota.
    /// </summary>
    /// <returns>
    /// Detached per-path canonical source counts; these are not all operating-system reads.
    /// </returns>
    internal Dictionary<string, long> CaptureSources() => new(_sources, StringComparer.Ordinal);
}
