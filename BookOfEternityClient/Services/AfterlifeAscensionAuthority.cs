using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeAscensionAuthority
{
    internal static bool IsReady(JsonObject soulRoot)
    {
        ArgumentNullException.ThrowIfNull(soulRoot);
        return IsReady(soulRoot.ToJsonString());
    }

    internal static bool IsReady(string? soulJson)
    {
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(soulJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var root = document.RootElement;

            if (root.TryGetProperty("soulProgression", out var progression) &&
                progression.ValueKind == JsonValueKind.Object &&
                (HasPercentAtLeastOneHundred(progression) ||
                 HasIntegralAtLeast(
                     progression,
                     "totalExperience",
                     AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience) ||
                 HasIntegralAtLeast(progression, "tier", 4) ||
                 HasTranscendenceTier(progression, "tierName")))
            {
                return true;
            }

            if (!root.TryGetProperty("enlightenment", out var enlightenment))
                return false;
            if (enlightenment.ValueKind == JsonValueKind.Object)
            {
                return HasTranscendenceTier(enlightenment, "currentTier") ||
                       HasIntegralAtLeast(enlightenment, "level", 4) ||
                       HasPercentAtLeastOneHundred(enlightenment) ||
                       HasIntegralAtLeast(
                           enlightenment,
                           "experience",
                           AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience);
            }

            return enlightenment.ValueKind == JsonValueKind.Number &&
                   enlightenment.TryGetDouble(out var numericEnlightenment) &&
                   double.IsFinite(numericEnlightenment) &&
                   numericEnlightenment >=
                   AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasPercentAtLeastOneHundred(JsonElement owner) =>
        owner.TryGetProperty("progressPercent", out var progressPercent) &&
        progressPercent.ValueKind == JsonValueKind.Number &&
        progressPercent.TryGetDouble(out var parsedPercent) &&
        double.IsFinite(parsedPercent) &&
        parsedPercent >= 100;

    private static bool HasIntegralAtLeast(
        JsonElement owner,
        string propertyName,
        int minimum) =>
        owner.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed) &&
        parsed >= minimum;

    private static bool HasTranscendenceTier(
        JsonElement owner,
        string propertyName) =>
        owner.TryGetProperty(propertyName, out var tier) &&
        tier.ValueKind == JsonValueKind.String &&
        IsTranscendenceTierName(tier.GetString());

    private static bool IsTranscendenceTierName(string? tierName) =>
        string.Equals(tierName, "Transcendence", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tierName, "Трансценденция", StringComparison.OrdinalIgnoreCase);
}
