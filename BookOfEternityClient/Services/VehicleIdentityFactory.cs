namespace BookOfEternityClient.Services;

internal class VehicleIdentityFactory
{
    internal virtual string CreateVehicleId() =>
        "vehicle_" + Guid.NewGuid().ToString("N");
}
