using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class StructuredBonusCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Canonicalize(JsonElement bonuses)
    {
        if (bonuses.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var normalized = new List<JsonObject>();
        foreach (var item in bonuses.EnumerateArray())
            normalized.Add(NormalizeBonus(item));

        return SerializeSorted(normalized);
    }

    public static string Canonicalize(JsonArray? bonuses)
    {
        if (bonuses == null)
            return string.Empty;

        var normalized = new List<JsonObject>();
        foreach (var item in bonuses)
        {
            if (item is JsonObject obj)
                normalized.Add(NormalizeBonus(obj));
            else
                normalized.Add(new JsonObject
                {
                    ["raw"] = item?.ToJsonString(JsonOpts) ?? string.Empty
                });
        }

        return SerializeSorted(normalized);
    }

    private static string SerializeSorted(List<JsonObject> normalized)
    {
        var ordered = normalized
            .OrderBy(o => o.ToJsonString(JsonOpts), StringComparer.Ordinal)
            .ToList();

        var arr = new JsonArray();
        foreach (var item in ordered)
            arr.Add(item);

        return arr.ToJsonString(JsonOpts);
    }

    private static JsonObject NormalizeBonus(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return new JsonObject
            {
                ["raw"] = item.GetRawText()
            };
        }

        return new JsonObject
        {
            ["description"] = ReadAsStableString(item, "description"),
            ["bonusType"] = ReadAsStableString(item, "bonusType"),
            ["target"] = ReadAsStableString(item, "target"),
            ["valueType"] = ReadAsStableString(item, "valueType"),
            ["value"] = ReadScalarAsStableString(item, "value"),
            ["application"] = ReadAsStableString(item, "application"),
            ["condition"] = ReadScalarAsStableString(item, "condition")
        };
    }

    private static JsonObject NormalizeBonus(JsonObject item)
    {
        return new JsonObject
        {
            ["description"] = ReadAsStableString(item, "description"),
            ["bonusType"] = ReadAsStableString(item, "bonusType"),
            ["target"] = ReadAsStableString(item, "target"),
            ["valueType"] = ReadAsStableString(item, "valueType"),
            ["value"] = ReadScalarAsStableString(item, "value"),
            ["application"] = ReadAsStableString(item, "application"),
            ["condition"] = ReadScalarAsStableString(item, "condition")
        };
    }

    private static string ReadAsStableString(JsonElement item, string propName)
    {
        if (!item.TryGetProperty(propName, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static string ReadScalarAsStableString(JsonElement item, string propName)
    {
        if (!item.TryGetProperty(propName, out var value))
            return string.Empty;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static string ReadAsStableString(JsonObject item, string propName)
    {
        var value = item[propName];
        if (value == null)
            return string.Empty;

        return value switch
        {
            JsonValue jv => jv.TryGetValue<string>(out var s) ? s ?? string.Empty : jv.ToJsonString(JsonOpts),
            _ => value.ToJsonString(JsonOpts)
        };
    }

    private static string ReadScalarAsStableString(JsonObject item, string propName)
    {
        var value = item[propName];
        if (value == null)
            return string.Empty;

        if (value is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s))
                return s ?? string.Empty;
            if (jv.TryGetValue<int>(out var i))
                return i.ToString();
            if (jv.TryGetValue<long>(out var l))
                return l.ToString();
            if (jv.TryGetValue<double>(out var d))
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (jv.TryGetValue<bool>(out var b))
                return b ? "true" : "false";
        }

        return value.ToJsonString(JsonOpts);
    }
}
