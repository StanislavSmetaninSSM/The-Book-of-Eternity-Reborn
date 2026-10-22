namespace BookOfEternityClient.Core;

/// <summary>
/// Tracks supported canonical mutations and an opaque version shared by one root.
/// </summary>
internal sealed class CanonicalValidationEpoch
{
    private readonly object _state = new();
    private readonly HashSet<object> _mutations = new(ReferenceEqualityComparer.Instance);
    private object _version = new();
    private long _begins;
    private long _ends;

    /// <summary>
    /// Registers an exact mutation identity and changes the version before its effects begin.
    /// </summary>
    /// <param name="mutation">
    /// Non-null identity retained through the mutation and all of its cleanup; duplicate registration is rejected.
    /// </param>
    internal void Begin(object mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        lock (_state)
        {
            var nextVersion = new object();
            if (!_mutations.Add(mutation))
                throw new InvalidOperationException("The canonical mutation is already registered.");
            _version = nextVersion;
            unchecked { _begins++; }
        }
    }

    /// <summary>
    /// Removes only the matching mutation identity and changes the version after its cleanup ends.
    /// </summary>
    /// <param name="mutation">
    /// Non-null original identity; an unknown or already retired identity has no effect.
    /// </param>
    internal void End(object mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        lock (_state)
        {
            if (!_mutations.Contains(mutation)) return;
            var nextVersion = new object();
            _mutations.Remove(mutation);
            _version = nextVersion;
            unchecked { _ends++; }
        }
    }

    /// <summary>
    /// Captures the current opaque version only while no supported mutation remains active.
    /// </summary>
    /// <param name="version">
    /// The current identity on success, or <see langword="null"/> while any mutation is active.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the root is free of active mutation identities; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool TryCapture(out object? version)
    {
        lock (_state)
        {
            version = _mutations.Count == 0 ? _version : null;
            return version != null;
        }
    }

    /// <summary>
    /// Captures diagnostic transition totals without exposing the authoritative version identity.
    /// </summary>
    /// <returns>
    /// Begin and end totals plus the number of currently active identities, captured under the epoch gate.
    /// These counters never establish authority or cache eligibility.
    /// </returns>
    internal (long Begins, long Ends, int Active) CaptureActivity()
    {
        lock (_state) return (_begins, _ends, _mutations.Count);
    }

}
