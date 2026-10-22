namespace BookOfEternityClient.Core;

/// <summary>
/// Tracks supported canonical mutations and an opaque version shared by one root.
/// </summary>
internal sealed class CanonicalValidationEpoch
{
    private readonly object _state = new();
    private readonly HashSet<object> _mutations = new(ReferenceEqualityComparer.Instance);
    private object _version = new();

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
}
