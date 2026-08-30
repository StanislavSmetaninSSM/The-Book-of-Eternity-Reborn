using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalWoundTreatmentRouteFingerprint
{
    private const string FingerprintDomain =
        "book_of_eternity.mortal_wound_treatment.route";

    internal static string Compute(
        WoundMaterializationEnvelope wound,
        string routeId)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);

        using var document = JsonDocument.Parse(
            WoundMaterializationContract.SerializeCanonical(wound));
        var routes = document.RootElement
            .GetProperty("treatment")
            .GetProperty("routes")
            .EnumerateArray()
            .Where(route => string.Equals(
                route.GetProperty("routeId").GetString(),
                routeId,
                StringComparison.Ordinal))
            .ToArray();
        if (routes.Length != 1)
            throw new InvalidOperationException("The selected route is not unique.");

        var route = routes[0];
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            FingerprintDomain,
            "1",
            route.GetProperty("routeId").GetString(),
            route.GetProperty("visibility").GetString(),
            route.GetProperty("mode").GetString(),
            route.GetProperty("requirements").GetRawText(),
            route.GetProperty("resourcePolicy").GetRawText(),
            route.GetProperty("resolution").GetRawText(),
            route.GetProperty("outcomes").GetRawText(),
            route.GetProperty("interruption").GetRawText()
        });
    }
}
