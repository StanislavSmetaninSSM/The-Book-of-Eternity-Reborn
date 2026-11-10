namespace BookOfEternityClient.Services;

/// <summary>
/// Retains one contract-parsed immutable parent and its exact intrinsic comparison facts.
/// These facts do not authenticate recorded History, current availability or publication authority.
/// </summary>
internal sealed class SpiritualStrainParentData
{
    /// <summary>Retains the owning codec's fully parsed immutable envelope and computes its fingerprint once.</summary>
    /// <param name="parentSourceId">The earlier same-request source for version 1, otherwise null.</param>
    /// <param name="parentWound">Complete immutable envelope returned by the wound contract parser.</param>
    /// <param name="effectId">Exact selected owned root identity.</param>
    /// <param name="componentId">Exact selected definition-local component identity.</param>
    internal SpiritualStrainParentData(string? parentSourceId, WoundMaterializationEnvelope parentWound,
        string effectId, string componentId)
    {
        ParentSourceId = parentSourceId;
        ParentWound = parentWound;
        ParentSemanticFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parentWound);
        EffectId = effectId;
        ComponentId = componentId;
    }

    /// <summary>Gets the optional version-1 earlier source coordinate, not a source capability.</summary>
    internal string? ParentSourceId { get; }
    /// <summary>Gets the already parsed immutable complete parent envelope.</summary>
    internal WoundMaterializationEnvelope ParentWound { get; }
    /// <summary>Gets its once-computed full semantic fingerprint for an independent History join.</summary>
    internal string ParentSemanticFingerprint { get; }
    /// <summary>Gets the exact selected owned root identity.</summary>
    internal string EffectId { get; }
    /// <summary>Gets the selected component identity within that root's definition.</summary>
    internal string ComponentId { get; }
}
