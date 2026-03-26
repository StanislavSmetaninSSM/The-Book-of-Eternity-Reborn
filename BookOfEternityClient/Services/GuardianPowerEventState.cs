using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianPowerEventState
{
    public const string JournalPath = "game_state/meta/abode_power_journal.json";

    public static readonly string[] AllowedReasonTypes =
    {
        "guardian_quest",
        "project_assist",
        "project_completion",
        "project_failure",
        "offering",
        "resonance",
        "correction_spend",
        "rival_strike",
        "rival_defense"
    };

    public static readonly string[] AllowedVisibility =
    {
        "player_known",
        "hidden"
    };

    public static bool IsValidReasonType(string? value) =>
        AllowedReasonTypes.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidVisibility(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        AllowedVisibility.Contains((value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    public static string GetEventId(JsonObject evt) => GetNodeString(evt["eventId"]);

    public static string GetGuardianId(JsonObject evt) => GetNodeString(evt["guardianId"]);

    public static string GetEventId(JsonElement evt) => GetString(evt, "eventId");

    public static string GetGuardianId(JsonElement evt) => GetString(evt, "guardianId");

    public static bool ApplyEvents(
        JsonObject guardiansRoot,
        IEnumerable<JsonObject> events,
        int currentTurn,
        List<JsonObject> journalEntries)
    {
        var changed = false;
        foreach (var evt in events)
        {
            if (evt == null)
                continue;

            changed = ApplySingleEvent(guardiansRoot, evt, currentTurn, journalEntries) || changed;
        }

        return changed;
    }

    public static async Task AppendJournalEntriesAsync(FileSystemManager fs, IEnumerable<JsonObject> entries)
    {
        var buffered = entries.Where(item => item != null).ToList();
        if (buffered.Count == 0)
            return;

        JsonObject root;
        var existing = await fs.ReadFileAsync(JournalPath);
        if (string.IsNullOrWhiteSpace(existing))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }

        var entriesArray = root["entries"] as JsonArray ?? new JsonArray();
        root["entries"] = entriesArray;

        foreach (var entry in buffered)
        {
            var eventId = GetNodeString(entry["eventId"]);
            var duplicate = entriesArray
                .OfType<JsonObject>()
                .Any(existingEntry =>
                    string.Equals(GetNodeString(existingEntry["eventId"]), eventId, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
                continue;

            entriesArray.Add(entry.DeepClone());
        }

        await fs.WriteFileAtomicAsync(JournalPath, root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    public static JsonObject BuildEvent(
        string eventId,
        string guardianId,
        int delta,
        string reasonType,
        string sourceSurface,
        string sourceId,
        string title,
        string summary,
        JsonObject audit,
        string? relatedGuardianId = null,
        string visibility = "player_known",
        string? appliedAt = null)
    {
        return new JsonObject
        {
            ["eventId"] = eventId,
            ["guardianId"] = guardianId,
            ["delta"] = delta,
            ["reasonType"] = reasonType,
            ["sourceSurface"] = sourceSurface,
            ["sourceId"] = sourceId,
            ["title"] = title,
            ["summary"] = summary,
            ["visibility"] = visibility,
            ["appliedAt"] = appliedAt ?? DateTime.UtcNow.ToString("o"),
            ["relatedGuardianId"] = relatedGuardianId,
            ["audit"] = audit.DeepClone()
        };
    }

    private static bool ApplySingleEvent(
        JsonObject guardiansRoot,
        JsonObject evt,
        int currentTurn,
        List<JsonObject> journalEntries)
    {
        var guardianId = GetGuardianId(evt);
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        var appliedAt = GetNodeString(evt["appliedAt"]);
        if (string.IsNullOrWhiteSpace(appliedAt))
            appliedAt = DateTime.UtcNow.ToString("o");
        evt["appliedAt"] = appliedAt;

        var requestedDelta = GetNodeInt(evt["delta"]);
        if (requestedDelta == 0)
            return false;

        if (guardiansRoot["guardians"] is not JsonArray guardians)
            return false;

        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
        if (guardian == null)
            return false;

        var guardianName = GuardianManifestation.GetDisplayName(ToJsonElement(guardian));
        var abodePower = AbodePowerRules.EnsureCanonicalState(guardian);
        var currentPower = AbodePowerRules.GetCurrentPower(guardian);
        var nextPower = AbodePowerRules.ClampCurrentPower(currentPower + requestedDelta);
        var appliedDelta = nextPower - currentPower;
        if (appliedDelta == 0)
            return false;

        abodePower["currentPower"] = nextPower;
        abodePower["tier"] = AbodePowerRules.GetTierLabel(nextPower);
        abodePower["lastUpdatedAt"] = appliedAt;
        var history = abodePower["history"] as JsonArray ?? new JsonArray();
        history.Add(new JsonObject
        {
            ["eventId"] = GetNodeString(evt["eventId"]),
            ["timestamp"] = appliedAt,
            ["change"] = appliedDelta,
            ["reason"] = GetNodeString(evt["title"]),
            ["summary"] = GetNodeString(evt["summary"]),
            ["reasonType"] = GetNodeString(evt["reasonType"]),
            ["source"] = GetNodeString(evt["sourceSurface"]),
            ["sourceId"] = GetNodeString(evt["sourceId"]),
            ["relatedGuardianId"] = evt["relatedGuardianId"]?.DeepClone(),
            ["audit"] = evt["audit"]?.DeepClone()
        });
        abodePower["history"] = history;
        guardian["abodePower"] = abodePower;
        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            activeGuardian["abodePower"] = abodePower.DeepClone();
            GuardianGachaChargeRules.NormalizeGuardianGachaState(activeGuardian);
        }

        journalEntries.Add(new JsonObject
        {
            ["entryId"] = $"abode_power_{guardianId}_{GetNodeString(evt["eventId"])}",
            ["eventId"] = GetNodeString(evt["eventId"]),
            ["turn"] = currentTurn,
            ["guardianId"] = guardianId,
            ["guardianName"] = guardianName,
            ["delta"] = appliedDelta,
            ["reasonType"] = GetNodeString(evt["reasonType"]),
            ["sourceSurface"] = GetNodeString(evt["sourceSurface"]),
            ["sourceId"] = GetNodeString(evt["sourceId"]),
            ["title"] = GetNodeString(evt["title"]),
            ["summary"] = GetNodeString(evt["summary"]),
            ["visibility"] = string.IsNullOrWhiteSpace(GetNodeString(evt["visibility"])) ? "player_known" : GetNodeString(evt["visibility"]),
            ["relatedGuardianId"] = evt["relatedGuardianId"]?.DeepClone(),
            ["appliedAt"] = appliedAt,
            ["audit"] = evt["audit"]?.DeepClone()
        });

        return true;
    }

    private static string GetNodeString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;

        try
        {
            return value.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return node.ToJsonString();
        }
    }

    private static int GetNodeInt(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsed))
                return parsed;
            if (value.TryGetValue<long>(out var parsedLong) &&
                parsedLong <= int.MaxValue &&
                parsedLong >= int.MinValue)
            {
                return (int)parsedLong;
            }
            if (value.TryGetValue<string>(out var parsedString) && int.TryParse(parsedString, out var parsedFromString))
                return parsedFromString;
        }

        return defaultValue;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static JsonElement ToJsonElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
}
