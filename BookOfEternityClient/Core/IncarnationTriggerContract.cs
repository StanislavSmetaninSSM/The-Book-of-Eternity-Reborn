using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed class IncarnationTriggerPayload
{
    public string WorldDescription { get; init; } = "";
    public string CharacterDescription { get; init; } = "";
    public string Circumstances { get; init; } = "";
    public string Source { get; init; } = "";
    public string GuardianId { get; init; } = "";
    public string SeverityBand { get; init; } = "";
    public string Reason { get; init; } = "";
    public string ProvocationSummary { get; init; } = "";

    public bool IsGuardianForced =>
        string.Equals(Source, IncarnationTriggerContract.GuardianForcedSource, StringComparison.OrdinalIgnoreCase);
}

internal static class IncarnationTriggerContract
{
    public const string GuardianForcedSource = "guardian_forced";
    public const string HarshSeverityBand = "harsh";
    public const string SevereSeverityBand = "severe";

    public static bool TryParse(string json, out IncarnationTriggerPayload payload)
    {
        payload = new IncarnationTriggerPayload();

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryParse(doc.RootElement, out payload);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParse(JsonElement root, out IncarnationTriggerPayload payload)
    {
        payload = new IncarnationTriggerPayload();

        if (root.ValueKind != JsonValueKind.Object)
            return false;

        var contractRoot = root;
        if (root.TryGetProperty("TriggerIncarnation", out var nested) && nested.ValueKind == JsonValueKind.Object)
            contractRoot = nested;

        if (!TryGetRequiredString(contractRoot, "worldDescription", out var worldDescription) ||
            !TryGetRequiredString(contractRoot, "characterDescription", out var characterDescription) ||
            !TryGetRequiredString(contractRoot, "circumstances", out var circumstances))
        {
            return false;
        }

        payload = new IncarnationTriggerPayload
        {
            WorldDescription = worldDescription,
            CharacterDescription = characterDescription,
            Circumstances = circumstances,
            Source = GetOptionalString(contractRoot, "source"),
            GuardianId = GetOptionalString(contractRoot, "guardianId"),
            SeverityBand = GetOptionalString(contractRoot, "severityBand"),
            Reason = GetOptionalString(contractRoot, "reason"),
            ProvocationSummary = GetOptionalString(contractRoot, "provocationSummary")
        };

        return true;
    }

    public static bool IsValidSeverityBand(string? severityBand) =>
        string.Equals(severityBand, HarshSeverityBand, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(severityBand, SevereSeverityBand, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetRequiredString(JsonElement root, string propertyName, out string value)
    {
        value = "";

        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return "";

        return property.GetString() ?? "";
    }
}
