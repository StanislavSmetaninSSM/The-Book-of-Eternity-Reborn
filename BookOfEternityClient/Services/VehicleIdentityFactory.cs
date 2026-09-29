namespace BookOfEternityClient.Services;

/// <summary>
/// Supplies ordinary opaque vehicle identities while allowing owner-scoped allocation policies.
/// </summary>
internal class VehicleIdentityFactory
{
    /// <summary>
    /// Allocates an ordinary random permanent vehicle identity.
    /// </summary>
    /// <returns>
    /// A vehicle-prefixed GUID identity.
    /// </returns>
    internal virtual string CreateVehicleId() =>
        "vehicle_" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Allocates for an admitted same-turn reference using the existing ordinary policy by default.
    /// </summary>
    /// <param name="vehicleRef">
    /// Nonempty admitted reference checked by the owning composer.
    /// </param>
    /// <returns>
    /// The permanent identity returned by the selected allocation policy.
    /// </returns>
    internal virtual string CreateVehicleId(string vehicleRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleRef);
        return CreateVehicleId();
    }
}
