using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

// Formatting of already parsed DATA only. The canonical cumulative counter has
// no calendar epoch; it must not be rendered as minutes since midnight.
internal static class WorldTimeDisplay
{
    internal static bool TryReadMinutes(JsonElement source, out long minutes)
    {
        minutes = 0;
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty("currentTimeInMinutes", out var value)) return false;
        return (value.ValueKind == JsonValueKind.Number ? value.TryGetInt64(out minutes)
            : value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out minutes)) && minutes >= 0;
    }

    internal static string FormatMinutes(long minutes) => minutes.ToString(CultureInfo.InvariantCulture) + " мин.";
    internal static string Format(long minutes) => "Игровое время: " + FormatMinutes(minutes);
}
