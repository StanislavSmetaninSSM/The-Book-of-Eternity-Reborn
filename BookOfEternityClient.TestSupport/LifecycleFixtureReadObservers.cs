using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Owns optional lifecycle read observers and passive canonical source counts.
/// </summary>
internal sealed class LifecycleFixtureReadObservers
{
    private readonly bool _forwardingEnabled;
    private readonly CanonicalReadActivityAccumulator _sources = new();
    private Func<string, Task>? _initial;
    private Func<string, Task>? _returned;
    private readonly bool _closingForwardingEnabled;
    private Func<Task>? _closing;

    /// <summary>
    /// Creates stable forwarding delegates for the fixture's lifetime.
    /// </summary>
    /// <param name="forwardingEnabled">
    /// Whether read callbacks may be attached, including after construction.
    /// </param>
    /// <param name="closingForwardingEnabled">Whether session-closing callbacks may be attached; independent of read forwarding.</param>
    internal LifecycleFixtureReadObservers(bool forwardingEnabled = true, bool closingForwardingEnabled = true)
    {
        _forwardingEnabled = forwardingEnabled;
        _closingForwardingEnabled = closingForwardingEnabled;
        AfterInitialValidationAsync = forwardingEnabled ? path => _initial?.Invoke(path) ?? Task.CompletedTask : null;
        AfterReadAttemptAsync = forwardingEnabled ? path => _returned?.Invoke(path) ?? Task.CompletedTask : null;
        AfterSessionClosingAsync = closingForwardingEnabled ? () => _closing?.Invoke() ?? Task.CompletedTask : null;
    }

    /// <summary>Shares the selected positive construction policy with the real-file mechanism control.</summary>
    /// <returns>The RED scaffold's unchanged original forwarding policy with passive DATA available alongside it.</returns>
    internal static LifecycleFixtureReadObservers CreateForSealedSourceObservation() => new();

    /// <summary>Gets the actual optional closing forwarder; existing read-only opt-ins keep it enabled by default.</summary>
    internal Func<Task>? AfterSessionClosingAsync { get; }

    /// <summary>Attaches an ordinary closing callback only when the immutable construction policy permits it.</summary>
    internal Func<Task>? ClosingObserver
    {
        get => _closing;
        set
        {
            if (!_closingForwardingEnabled && value is not null)
                throw new InvalidOperationException("Closing observation was disabled before fixture construction.");
            _closing = value;
        }
    }

    /// <summary>
    /// Gets the initial read hook, or <see langword="null"/> for an explicitly unobserved fixture.
    /// </summary>
    internal Func<string, Task>? AfterInitialValidationAsync { get; }

    /// <summary>
    /// Gets the returned read hook, or <see langword="null"/> for an explicitly unobserved fixture.
    /// </summary>
    internal Func<string, Task>? AfterReadAttemptAsync { get; }

    /// <summary>
    /// Gets whether arbitrary read observers can be attached to this fixture.
    /// </summary>
    internal bool ForwardingEnabled => _forwardingEnabled;

    /// <summary>
    /// Gets or attaches the initial observer; disabled fixtures reject non-<see langword="null"/> attachment.
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
    /// Gets or attaches the returned observer; disabled fixtures reject non-<see langword="null"/> attachment.
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
    /// Gets the sealed source counter installed without an arbitrary callback.
    /// </summary>
    internal CanonicalReadActivityAccumulator CanonicalSources => _sources;

    /// <summary>
    /// Counts all canonical source events for one exact path.
    /// </summary>
    /// <param name="path">
    /// The original relative source spelling.
    /// </param>
    /// <returns>
    /// The complete count, or zero when the path has not been observed.
    /// </returns>
    internal long ReadCount(string path) => _sources.ReadCount(path);

    /// <summary>
    /// Copies every source count without imposing a record or byte quota.
    /// </summary>
    /// <returns>
    /// Detached per-path canonical source counts; these are not all operating-system reads.
    /// </returns>
    internal Dictionary<string, long> CaptureSources() => _sources.Capture();
}
