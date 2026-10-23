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
    private Func<OperationStructuralSnapshot?>? _capture;
    private OperationStructuralSnapshot? _snapshot;
    private object _version = new();
    private object _captureGeneration = new();

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

    /// <summary>
    /// Checks the exact capture delegate bound to this provider without invoking it.
    /// </summary>
    /// <param name="capture">
    /// The proposed complete structural capture policy.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the provider is live and bound to this exact delegate; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool HasCapturePolicy(Func<OperationStructuralSnapshot?> capture)
    {
        lock (_gate) return ReferenceEquals(_capture, capture);
    }

    /// <summary>
    /// Releases retained comparison DATA and invalidates captures already running outside the monitor.
    /// </summary>
    internal void ClearCapturedSnapshot()
    {
        lock (_gate)
        {
            _snapshot = null;
            _version = new();
            _captureGeneration = new();
        }
    }

    /// <summary>
    /// Permanently releases comparison DATA and the capture callback when the owning operation closes.
    /// </summary>
    internal void Retire()
    {
        lock (_gate)
        {
            _capture = null;
            _snapshot = null;
            _version = new();
            _captureGeneration = new();
        }
    }

    /// <inheritdoc />
    public bool TryCaptureVersion(out object? version)
    {
        Func<OperationStructuralSnapshot?> capture;
        object generation;
        lock (_gate)
        {
            if (_capture is null)
            {
                version = null;
                return false;
            }
            capture = _capture;
            generation = _captureGeneration;
        }
        OperationStructuralSnapshot? current;
        try { current = capture(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            JsonException or OverflowException or NullReferenceException or NotSupportedException)
        {
            current = null;
        }
        lock (_gate)
        {
            if (_capture is null || !ReferenceEquals(generation, _captureGeneration))
            {
                version = null;
                return false;
            }
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
