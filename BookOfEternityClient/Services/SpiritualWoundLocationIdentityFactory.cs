using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Holds the journal and ordinary generator selected for one location intake attempt.
/// </summary>
internal sealed class SpiritualWoundLocationIdentityFactory : MortalLocationIdentityFactory
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly MortalLocationIdentityFactory _underlying;

    /// <summary>
    /// Binds an allocation journal and ordinary location identity generator.
    /// </summary>
    /// <param name="journal">
    /// Journal for this capture attempt.
    /// </param>
    /// <param name="underlying">
    /// Ordinary factory used for new allocations.
    /// </param>
    internal SpiritualWoundLocationIdentityFactory(
        SpiritualWoundReplayJournal journal,
        MortalLocationIdentityFactory underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <summary>
    /// Gets whether the retained allocation attempt remains usable.
    /// </summary>
    internal override bool IsHealthy => _journal.IsHealthy && _underlying.IsHealthy;

    /// <inheritdoc />
    internal override string CreateLocationId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateLocationReceiptId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateLinkId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateLinkReceiptId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateTransitionId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateThreatId() => RejectUnbound();

    /// <inheritdoc />
    internal override string CreateLocationId(MortalLocationCreationAllocation allocation) =>
        Allocate("location", "location_creation_key", allocation,
            () => _underlying.CreateLocationId(allocation));

    /// <inheritdoc />
    internal override string CreateLocationReceiptId(MortalLocationCreationAllocation allocation) =>
        Allocate("location_receipt", "location_creation_key", allocation,
            () => _underlying.CreateLocationReceiptId(allocation));

    /// <inheritdoc />
    internal override string CreateLinkId(MortalLocationCreationAllocation allocation) =>
        Allocate("location_link", "location_link_creation_key", allocation,
            () => _underlying.CreateLinkId(allocation));

    /// <inheritdoc />
    internal override string CreateLinkReceiptId(MortalLocationCreationAllocation allocation) =>
        Allocate("location_link_receipt", "location_link_creation_key", allocation,
            () => _underlying.CreateLinkReceiptId(allocation));

    /// <inheritdoc />
    internal override string CreateTransitionId(MortalLocationTransitionAllocation allocation) =>
        Allocate("location_transition", "location_transition_key", allocation,
            () => _underlying.CreateTransitionId(allocation));

    /// <inheritdoc />
    internal override string CreateThreatId(MortalLocationThreatAllocation allocation) =>
        Allocate("location_threat", "location_threat_key", allocation,
            () => _underlying.CreateThreatId(allocation));

    /// <summary>
    /// Hashes the typed causal coordinate and retains its ordinary identity.
    /// </summary>
    /// <typeparam name="TAllocation">
    /// Allocation coordinate type serialized into the journal comparison key.
    /// </typeparam>
    /// <param name="kind">
    /// Semantic identity family recorded by the journal.
    /// </param>
    /// <param name="domain">
    /// Separate hash domain for the coordinate family.
    /// </param>
    /// <param name="allocation">
    /// Non-null admitted allocation coordinate.
    /// </param>
    /// <param name="generate">
    /// Ordinary callback used only when a new identity is needed.
    /// </param>
    /// <returns>
    /// Recorded or newly generated permanent identity.
    /// </returns>
    private string Allocate<TAllocation>(
        string kind,
        string domain,
        TAllocation allocation,
        Func<string> generate)
    {
        try
        {
            if (!IsHealthy)
                throw new InvalidOperationException("The location allocation attempt is revoked.");
            ArgumentNullException.ThrowIfNull(allocation);
            var key = JsonSerializer.SerializeToNode(allocation)!.AsObject();
            var coordinate = SpiritualWoundStateJson.Hash(key, domain);
            return _journal.Request(kind, "locations", coordinate, generate);
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Revokes the attempt when a caller omits the admitted allocation coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an identity.
    /// </returns>
    private string RejectUnbound()
    {
        _journal.Invalidate();
        throw new InvalidOperationException("A typed location operation is required for journaled allocation.");
    }
}
