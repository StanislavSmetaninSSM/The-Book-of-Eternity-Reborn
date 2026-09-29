using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Records or replays vehicle allocation against the admitted same-turn reference.
/// </summary>
internal sealed class SpiritualWoundVehicleIdentityFactory : VehicleIdentityFactory
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly VehicleIdentityFactory _underlying;

    /// <summary>
    /// Binds one attempt's journal to its ordinary vehicle allocation policy.
    /// </summary>
    /// <param name="journal">
    /// Non-null ordered allocation stream owned by the capture attempt.
    /// </param>
    /// <param name="underlying">
    /// Non-null ordinary policy invoked only for new allocations.
    /// </param>
    internal SpiritualWoundVehicleIdentityFactory(SpiritualWoundReplayJournal journal,
        VehicleIdentityFactory underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <inheritdoc/>
    internal override string CreateVehicleId(string vehicleRef)
    {
        try
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(vehicleRef))
                throw new ArgumentException("An exact vehicle reference is required.", nameof(vehicleRef));
            var coordinate = SpiritualWoundStateJson.Hash(
                new JsonObject { ["vehicleRef"] = vehicleRef }, "allocation_vehicle");
            return _journal.Request("vehicle", "vehicles", coordinate,
                () => _underlying.CreateVehicleId(vehicleRef));
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Revokes an attempt that requests an allocation without an admitted reference.
    /// </summary>
    /// <returns>
    /// No value; an unbound allocation always throws.
    /// </returns>
    internal override string CreateVehicleId()
    {
        _journal.Invalidate();
        throw new InvalidOperationException("Vehicle replay requires an admitted reference.");
    }
}
