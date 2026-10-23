namespace BookOfEternityClient.Services;

/// <summary>
/// Defines the initial comparison seam for complete owner-defined structural DATA.
/// </summary>
internal sealed class OperationStructuralSnapshot
{
    /// <summary>
    /// Accepts the owner-defined ordered values and actual companion references.
    /// </summary>
    /// <param name="values">
    /// Ordered nullable structural values; their comparison protocol is implemented after causal controls.
    /// </param>
    /// <param name="identities">
    /// Actual nullable companion identities whose equality must use reference identity.
    /// </param>
    internal OperationStructuralSnapshot(IEnumerable<string?> values, IEnumerable<object?> identities)
    {
    }

    /// <summary>
    /// Exposes the initial nonmatching comparison seam without granting reuse.
    /// </summary>
    /// <param name="other">
    /// The independently captured current DATA snapshot.
    /// </param>
    /// <returns>
    /// Always false until exact value and identity comparison is implemented.
    /// </returns>
    internal bool Matches(OperationStructuralSnapshot other) => false;
}

/// <summary>
/// Supplies the initial constant-version seam before structural invalidation is implemented.
/// </summary>
internal sealed class OperationStructuralDependency : IOperationDependency
{
    private readonly object _version = new();

    /// <summary>
    /// Accepts the current DATA capture callback for causal invalidation controls.
    /// </summary>
    /// <param name="capture">
    /// Captures complete owner-defined DATA, or null when reuse is not eligible.
    /// </param>
    internal OperationStructuralDependency(Func<OperationStructuralSnapshot?> capture)
    {
    }

    /// <inheritdoc />
    public bool TryCaptureVersion(out object? version)
    {
        version = _version;
        return true;
    }
}
