using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Core;

public static class GuardianManifestation
{
    public const string FixedFlexibility = "fixed";
    public const string SelectiveFlexibility = "selective";
    public const string AdaptiveFlexibility = "adaptive";

    public static bool IsValidFormFlexibility(string? value) =>
        value is FixedFlexibility or SelectiveFlexibility or AdaptiveFlexibility;

    public static string GetCanonicalName(JsonElement guardian) =>
        GetString(guardian, "canonicalName");

    public static string GetCanonicalName(JsonObject guardian) =>
        GetNodeString(guardian["canonicalName"]);

    public static string GetDisplayName(JsonElement guardian)
    {
        if (guardian.TryGetProperty("manifestation", out var manifestation) &&
            manifestation.ValueKind == JsonValueKind.Object)
        {
            var currentDisplayName = GetString(manifestation, "currentDisplayName");
            if (!string.IsNullOrWhiteSpace(currentDisplayName))
                return currentDisplayName;
        }

        var canonicalName = GetCanonicalName(guardian);
        if (!string.IsNullOrWhiteSpace(canonicalName))
            return canonicalName;

        var legacyName = GetString(guardian, "name");
        if (!string.IsNullOrWhiteSpace(legacyName))
            return legacyName;

        return GetString(guardian, "guardianName");
    }

    public static string GetDisplayName(JsonObject guardian)
    {
        if (guardian["manifestation"] is JsonObject manifestation)
        {
            var currentDisplayName = GetNodeString(manifestation["currentDisplayName"]);
            if (!string.IsNullOrWhiteSpace(currentDisplayName))
                return currentDisplayName;
        }

        var canonicalName = GetCanonicalName(guardian);
        if (!string.IsNullOrWhiteSpace(canonicalName))
            return canonicalName;

        var legacyName = GetNodeString(guardian["name"]);
        if (!string.IsNullOrWhiteSpace(legacyName))
            return legacyName;

        return GetNodeString(guardian["guardianName"]);
    }

    public static string GetAppearanceDescription(JsonElement guardian)
    {
        if (guardian.TryGetProperty("manifestation", out var manifestation) &&
            manifestation.ValueKind == JsonValueKind.Object)
        {
            return GetString(manifestation, "appearanceDescription");
        }

        return "";
    }

    public static string GetAppearanceDescription(JsonObject guardian)
    {
        if (guardian["manifestation"] is JsonObject manifestation)
            return GetNodeString(manifestation["appearanceDescription"]);

        return "";
    }

    public static string GetPresentationStyle(JsonElement guardian)
    {
        if (guardian.TryGetProperty("manifestation", out var manifestation) &&
            manifestation.ValueKind == JsonValueKind.Object)
        {
            return GetString(manifestation, "currentPresentationStyle");
        }

        return "";
    }

    public static string GetPresentationStyle(JsonObject guardian)
    {
        if (guardian["manifestation"] is JsonObject manifestation)
            return GetNodeString(manifestation["currentPresentationStyle"]);

        return "";
    }

    public static string GetPronouns(JsonElement guardian)
    {
        if (guardian.TryGetProperty("manifestation", out var manifestation) &&
            manifestation.ValueKind == JsonValueKind.Object)
        {
            return GetString(manifestation, "currentPronouns");
        }

        return "";
    }

    public static string GetPronouns(JsonObject guardian)
    {
        if (guardian["manifestation"] is JsonObject manifestation)
            return GetNodeString(manifestation["currentPronouns"]);

        return "";
    }

    public static string GetFormFlexibility(JsonElement guardian)
    {
        if (guardian.TryGetProperty("manifestation", out var manifestation) &&
            manifestation.ValueKind == JsonValueKind.Object)
        {
            return GetString(manifestation, "formFlexibility");
        }

        return "";
    }

    public static string GetFormFlexibility(JsonObject guardian)
    {
        if (guardian["manifestation"] is JsonObject manifestation)
            return GetNodeString(manifestation["formFlexibility"]);

        return "";
    }

    public static bool HasDistinctCanonicalName(JsonElement guardian)
    {
        var canonicalName = GetCanonicalName(guardian);
        var displayName = GetDisplayName(guardian);
        return !string.IsNullOrWhiteSpace(canonicalName) &&
               !string.IsNullOrWhiteSpace(displayName) &&
               !string.Equals(canonicalName, displayName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasDistinctCanonicalName(JsonObject guardian)
    {
        var canonicalName = GetCanonicalName(guardian);
        var displayName = GetDisplayName(guardian);
        return !string.IsNullOrWhiteSpace(canonicalName) &&
               !string.IsNullOrWhiteSpace(displayName) &&
               !string.Equals(canonicalName, displayName, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFormFlexibilityLabel(string? flexibility) =>
        (flexibility ?? "").Trim().ToLowerInvariant() switch
        {
            FixedFlexibility => "Постоянная форма",
            SelectiveFlexibility => "Избирательная смена формы",
            AdaptiveFlexibility => "Адаптивная смена формы",
            _ => string.IsNullOrWhiteSpace(flexibility) ? "" : flexibility!
        };

    public static string GetPresentationStyleLabel(string? style) =>
        (style ?? "").Trim().ToLowerInvariant() switch
        {
            "feminine" => "Женская подача",
            "masculine" => "Мужская подача",
            "neutral" => "Нейтральная подача",
            "androgynous" => "Андрогинная подача",
            "shifting" => "Текучая подача",
            _ => string.IsNullOrWhiteSpace(style) ? "" : style!
        };

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return property.GetString() ?? "";
    }

    private static string GetNodeString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var stringValue)
            ? stringValue ?? ""
            : "";
}
