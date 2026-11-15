namespace BookOfEternityClient.UI;

/// <summary>Formats canonical vehicle DATA without interpreting commands or granting access.</summary>
internal static class MortalVehicleDisplay
{
    /// <summary>Matches the client-owned selection to one exact current vehicle identity.</summary>
    /// <param name="activeVehicleId">Canonical selected identity, or null.</param>
    /// <param name="vehicleId">Canonical identity of the displayed vehicle.</param>
    /// <returns>Whether this vehicle is explicitly selected.</returns>
    internal static bool IsSelected(string? activeVehicleId, string? vehicleId) =>
        !string.IsNullOrEmpty(vehicleId) && string.Equals(activeVehicleId, vehicleId, StringComparison.Ordinal);

    /// <summary>Formats availability without substituting legacy status or selection flags.</summary>
    /// <param name="availability">The vehicle's canonical availability field.</param>
    /// <returns>A player-facing label, including an explicit unknown value when absent.</returns>
    internal static string DescribeAvailability(string? availability) =>
        availability?.Trim().ToLowerInvariant() switch
        {
            null or "" => "не указано",
            "active" => "Активен (оседлан/управляется)",
            "parked" => "Припаркован",
            "pocket" => "В кармане (призываемый)",
            _ => availability!.Trim()
        };
}
