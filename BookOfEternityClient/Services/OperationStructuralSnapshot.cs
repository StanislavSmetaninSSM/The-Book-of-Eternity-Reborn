using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Owns exact ordered structural values and actual companion identities without granting authority.
/// </summary>
internal sealed class OperationStructuralSnapshot
{
    private readonly string?[] _values;
    private readonly object?[] _identities;

    /// <summary>
    /// Detaches the owner-defined ordered values and companion reference sequence.
    /// </summary>
    /// <param name="values">
    /// Complete ordered immutable structural values; null and empty remain distinct.
    /// </param>
    /// <param name="identities">
    /// Actual nullable companion identities whose equality must use reference identity.
    /// </param>
    internal OperationStructuralSnapshot(IEnumerable<string?> values, IEnumerable<object?> identities)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(identities);
        _values = values.ToArray();
        _identities = identities.ToArray();
    }

    /// <summary>
    /// Compares the full ordered values and exact references with another capture.
    /// </summary>
    /// <param name="other">
    /// The independently captured current DATA snapshot.
    /// </param>
    /// <returns>
    /// True only when all ordinal values and actual reference identities agree.
    /// </returns>
    internal bool Matches(OperationStructuralSnapshot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!_values.SequenceEqual(other._values, StringComparer.Ordinal) ||
            _identities.Length != other._identities.Length) return false;
        for (var i = 0; i < _identities.Length; i++)
            if (!ReferenceEquals(_identities[i], other._identities[i])) return false;
        return true;
    }
}

/// <summary>
/// Captures current structural DATA outside its state monitor and versions exact changes without resurrection.
/// </summary>
internal sealed class OperationStructuralDependency : IOperationDependency
{
    private readonly object _gate = new();
    private readonly Func<OperationStructuralSnapshot?> _capture;
    private OperationStructuralSnapshot? _snapshot;
    private object _version = new();

    /// <summary>
    /// Selects the complete current DATA capture without domain validation or I/O.
    /// </summary>
    /// <param name="capture">
    /// Captures complete owner-defined DATA, or null when reuse is not eligible.
    /// </param>
    internal OperationStructuralDependency(Func<OperationStructuralSnapshot?> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        _capture = capture;
    }

    /// <summary>
    /// Gets whether this provider currently retains comparison DATA.
    /// </summary>
    internal bool HasCapturedSnapshot
    {
        get { lock (_gate) return _snapshot is not null; }
    }

    /// <inheritdoc />
    public bool TryCaptureVersion(out object? version)
    {
        OperationStructuralSnapshot? current;
        try { current = _capture(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            JsonException or OverflowException or NullReferenceException or NotSupportedException)
        {
            current = null;
        }
        lock (_gate)
        {
            if (current is null)
            {
                _snapshot = null;
                _version = new();
                version = null;
                return false;
            }
            if (_snapshot is null || !_snapshot.Matches(current))
            {
                _snapshot = current;
                _version = new();
            }
            version = _version;
            return true;
        }
    }
}
