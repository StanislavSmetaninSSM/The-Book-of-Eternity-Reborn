using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Binds one item allocation to its admitted original-turn cause; it grants no authority.
/// </summary>
/// <param name="SessionId">
/// Signed session owning the original draft.
/// </param>
/// <param name="SnapshotToken">
/// Exact original snapshot token.
/// </param>
/// <param name="RequestId">
/// Original request identity.
/// </param>
/// <param name="Turn">
/// Original accepted turn number.
/// </param>
/// <param name="CreationRef">
/// Admitted same-turn item reference.
/// </param>
/// <param name="SourceTurn">
/// Admitted materialization source turn.
/// </param>
/// <param name="FilePath">
/// Exact carrier input path.
/// </param>
/// <param name="JsonPath">
/// Exact candidate path within that carrier.
/// </param>
/// <param name="Carrier">
/// Admitted destination carrier coordinate.
/// </param>
/// <param name="Route">
/// Validated route, source authority and source item identities.
/// </param>
internal sealed record MortalItemAllocationKey(
    string SessionId,
    string SnapshotToken,
    string RequestId,
    int Turn,
    string CreationRef,
    int SourceTurn,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate Carrier,
    MortalItemRouteAuthority Route);

/// <summary>
/// Supplies ordinary item identities while retaining retries for collisions with known identities.
/// </summary>
internal class MortalItemIdentityFactory
{
    /// <summary>
    /// Gets whether this factory's allocation attempt remains usable.
    /// </summary>
    internal virtual bool IsHealthy => true;

    /// <summary>
    /// Gets whether this factory restricts handoffs to callers presenting its exact instance.
    /// </summary>
    internal virtual bool IsAttemptScoped => false;

    /// <summary>
    /// Creates an ordinary opaque item identity.
    /// </summary>
    /// <returns>
    /// An identity with the existing item prefix.
    /// </returns>
    internal virtual string CreateItemId() => "itm_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Creates one identity that does not collide with the accepted item inventory.
    /// </summary>
    /// <param name="key">
    /// Admitted causal key, required by scoped factories; ordinary callers may omit it.
    /// </param>
    /// <param name="knownIds">
    /// Exact identities already reserved by the owner; this method does not mutate the set.
    /// </param>
    /// <returns>
    /// The first ordinary allocation absent from <paramref name="knownIds"/>.
    /// </returns>
    internal virtual string CreateItemId(MortalItemAllocationKey? key, IReadOnlySet<string> knownIds)
    {
        ArgumentNullException.ThrowIfNull(knownIds);
        string value;
        do { value = CreateItemId(); } while (knownIds.Contains(value));
        return value;
    }
}

/// <summary>
/// Retains item identities in one attempt's ordered stream without exposing normalization authority.
/// </summary>
internal sealed class SpiritualWoundItemIdentityFactory : MortalItemIdentityFactory
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly MortalItemIdentityFactory _underlying;

    /// <summary>
    /// Binds an attempt's journal to the ordinary collision-aware allocation policy.
    /// </summary>
    /// <param name="journal">
    /// Non-null journal owned by the capture attempt.
    /// </param>
    /// <param name="underlying">
    /// Ordinary allocator invoked only when the retained stream permits a new value.
    /// </param>
    internal SpiritualWoundItemIdentityFactory(SpiritualWoundReplayJournal journal,
        MortalItemIdentityFactory underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <inheritdoc/>
    internal override bool IsHealthy => _journal.IsHealthy;

    /// <inheritdoc/>
    internal override bool IsAttemptScoped => true;

    /// <inheritdoc/>
    internal override string CreateItemId(MortalItemAllocationKey? key, IReadOnlySet<string> knownIds)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(knownIds);
            key = key with
            {
                Carrier = FreezeCarrier(key.Carrier),
                Route = key.Route with
                {
                    Destination = FreezeCarrier(key.Route.Destination),
                    SourceItemIds = Array.AsReadOnly(key.Route.SourceItemIds.ToArray())
                }
            };
            var callbackKnownIds = knownIds.ToFrozenSet(StringComparer.Ordinal);
            var coordinate = SpiritualWoundStateJson.Hash(
                JsonSerializer.SerializeToNode(key)!.AsObject(), "allocation_item");
            var value = _journal.Request("item", "items", coordinate,
                () => _underlying.CreateItemId(key, callbackKnownIds));
            if (knownIds.Contains(value))
                throw new InvalidOperationException("Replayed item identity collides with accepted authority.");
            return value;
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Copies a carrier and freezes the list visible to overridable allocation code.
    /// </summary>
    /// <param name="carrier">
    /// Owner-provided carrier coordinate.
    /// </param>
    /// <returns>
    /// Detached carrier with a read-only container path.
    /// </returns>
    private static MortalItemCarrierCoordinate FreezeCarrier(MortalItemCarrierCoordinate carrier) =>
        carrier with { ContainerPath = Array.AsReadOnly(carrier.ContainerPath.ToArray()) };

    /// <inheritdoc/>
    internal override string CreateItemId()
    {
        _journal.Invalidate();
        throw new InvalidOperationException("Item replay requires an admitted causal key.");
    }
}
