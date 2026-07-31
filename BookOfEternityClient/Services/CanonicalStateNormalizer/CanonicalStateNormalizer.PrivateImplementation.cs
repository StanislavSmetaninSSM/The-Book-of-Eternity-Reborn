using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class CanonicalStateNormalizer
{
    private static bool FactionIdentityMatches(JsonObject existing, string? factionId)
    {
        if (!string.IsNullOrWhiteSpace(factionId) &&
            string.Equals(GetNodeString(existing["factionId"]), factionId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string? ResolveFactionIdentity(string? factionId, string? initialFactionId)
    {
        return !string.IsNullOrWhiteSpace(factionId) ? factionId : initialFactionId;
    }

    private static void NormalizeStoredFactionReference(JsonObject entry)
    {
        var resolvedFactionId = ResolveFactionIdentity(GetNodeString(entry["factionId"]), GetNodeString(entry["initialFactionId"]));
        if (!string.IsNullOrWhiteSpace(resolvedFactionId))
            entry["factionId"] = resolvedFactionId;
        entry.Remove("initialFactionId");
    }

    private static IEnumerable<JsonNode> CollectFactionChronicleEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray)
                if (item != null)
                    yield return NormalizeFactionChronicleEntry(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj["entries"] is JsonArray entries)
        {
            foreach (var item in entries)
                if (item != null)
                    yield return NormalizeFactionChronicleEntry(item);
        }

        if (obj["factionChronicleUpdates"] is JsonArray updates)
        {
            foreach (var item in updates)
                if (item != null)
                    yield return NormalizeFactionChronicleEntry(item);
        }
    }

    private static JsonNode NormalizeFactionChronicleEntry(JsonNode entry)
    {
        if (entry is not JsonObject obj)
        {
            return new JsonObject
            {
                ["entry"] = entry.ToString()
            };
        }

        if (obj.ContainsKey("entry") || obj.ContainsKey("chronicle") || obj.ContainsKey("text"))
            return obj.DeepClone();

        return new JsonObject
        {
            ["factionId"] = obj["factionId"]?.DeepClone(),
            ["factionName"] = obj["factionName"]?.DeepClone(),
            ["entry"] = GetNodeString(obj["entryToAppend"]) ?? obj.ToJsonString(),
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };
    }

    private static void UpsertByIdentity(List<JsonObject> items, JsonObject candidate, params string[] keys)
    {
        var keyValue = keys
            .Select(k => GetNodeString(candidate[k]))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (!string.IsNullOrWhiteSpace(keyValue))
        {
            var existing = items.FirstOrDefault(item =>
                keys.Select(k => GetNodeString(item[k]))
                    .Any(v => !string.IsNullOrWhiteSpace(v) && string.Equals(v, keyValue, StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                MergeObject(existing, candidate);
                return;
            }
        }

        items.Add(candidate.DeepClone()!.AsObject());
    }

    private static void UpsertByIdentity(JsonArray items, JsonObject candidate, params string[] keys)
    {
        var existing = items
            .OfType<JsonObject>()
            .FirstOrDefault(item =>
            {
                foreach (var key in keys)
                {
                    var left = GetNodeString(item[key]);
                    var right = GetNodeString(candidate[key]);
                    if (!string.IsNullOrWhiteSpace(left) &&
                        !string.IsNullOrWhiteSpace(right) &&
                        string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            });

        if (existing != null)
        {
            MergeObject(existing, candidate);
            return;
        }

        items.Add(candidate.DeepClone());
    }

    private static void AddUniqueNode(JsonArray array, JsonNode node)
    {
        var raw = node.ToJsonString();
        foreach (var existing in array)
        {
            if (existing?.ToJsonString() == raw)
                return;
        }

        array.Add(node.DeepClone());
    }

    private static JsonArray ToArray(IEnumerable<JsonObject> objects)
    {
        var arr = new JsonArray();
        foreach (var obj in objects)
            arr.Add(obj.DeepClone());
        return arr;
    }

    private async Task WriteIfChangedAsync(string path, JsonNode? currentNode, JsonObject result)
    {
        var currentJson = currentNode?.ToJsonString(JsonOpts) ?? string.Empty;
        var resultJson = result.ToJsonString(JsonOpts);
        if (string.Equals(currentJson, resultJson, StringComparison.Ordinal))
            return;

        await WriteCanonicalFileAtomicAsync(path, resultJson);
    }

    private Task<string?> ReadCanonicalFileAsync(string path) =>
        _writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(_writeLease, path);

    private Task WriteCanonicalFileAtomicAsync(string path, string content) =>
        _writeLease == null
            ? _fs.WriteFileAtomicAsync(path, content)
            : _fs.WriteFileAtomicAsync(_writeLease, path, content);

    private bool CanonicalFileExists(string path) =>
        _writeLease == null
            ? _fs.FileExists(path)
            : _fs.FileExists(_writeLease, path);

    internal async Task<string?> ReadBackupTextAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var candidate = path.Trim();
        if (!Path.IsPathFullyQualified(candidate))
            return await ReadCanonicalFileAsync(candidate.Replace('\\', '/'));

        var absolutePath = Path.GetFullPath(candidate);
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(_fs.BasePath),
            absolutePath);
        if (!Path.IsPathRooted(relativePath) &&
            !string.Equals(relativePath, "..", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return await ReadCanonicalFileAsync(relativePath.Replace('\\', '/'));
        }

        if (!File.Exists(absolutePath))
            return null;

        return await File.ReadAllTextAsync(absolutePath);
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str)) return str;
            if (value.TryGetValue<int>(out var intValue)) return intValue.ToString();
            if (value.TryGetValue<long>(out var longValue)) return longValue.ToString();
            if (value.TryGetValue<bool>(out var boolValue)) return boolValue ? "true" : "false";
        }
        return node?.ToString();
    }

    private static int GetNodeInt(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue)) return intValue;
            if (value.TryGetValue<long>(out var longValue) && longValue <= int.MaxValue && longValue >= int.MinValue)
                return (int)longValue;
            if (value.TryGetValue<string>(out var str) && int.TryParse(str, out var parsed))
                return parsed;
        }
        return defaultValue;
    }

    private static bool GetJsonBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue;
            if (value.TryGetValue<string>(out var str) && bool.TryParse(str, out var parsed))
                return parsed;
        }

        return false;
    }

    private static bool NormalizeInventoryItemJournalEntries(JsonNode node)
    {
        var changed = false;

        if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                changed |= NormalizeInventoryItemJournalEntries(item);
            return changed;
        }

        if (node is not JsonObject obj)
            return false;

        changed |= NormalizeInventoryItemJournalEntriesObject(obj);

        foreach (var collectionName in new[] { "items", "entries", "itemJournals", "UpdateInventory" })
        {
            if (obj[collectionName] is not JsonArray collection)
                continue;

            foreach (var item in collection.OfType<JsonObject>())
                changed |= NormalizeInventoryItemJournalEntriesObject(item);
        }

        return changed;
    }

    private static bool NormalizeInventoryItemJournalEntriesObject(JsonObject obj)
    {
        var changed = false;

        if (obj["journalEntries"] is JsonArray journalEntries)
            changed |= NormalizePlayerFacingTurnAnchorsInArray(journalEntries);

        foreach (var fieldName in PlayerFacingItemJournalObjectFields)
        {
            if (obj[fieldName] is JsonValue value &&
                value.TryGetValue<string>(out var text))
            {
                var normalized = StripPlayerFacingTurnAnchor(text);
                if (!string.Equals(text, normalized, StringComparison.Ordinal))
                {
                    obj[fieldName] = normalized;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool NormalizePlayerFacingTurnAnchorsInArray(JsonArray array)
    {
        var changed = false;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is JsonValue value &&
                value.TryGetValue<string>(out var text))
            {
                var normalized = StripPlayerFacingTurnAnchor(text);
                if (!string.Equals(text, normalized, StringComparison.Ordinal))
                {
                    array[index] = normalized;
                    changed = true;
                }
            }
            else if (array[index] is JsonObject obj)
            {
                changed |= NormalizeInventoryItemJournalEntriesObject(obj);
            }
        }

        return changed;
    }

    private static readonly string[] PlayerFacingItemJournalObjectFields =
    {
        "event",
        "description",
        "text",
        "content",
        "entry",
        "spiritVoice",
        "magicalResonance",
        "entryToAppend"
    };

    private static string StripPlayerFacingTurnAnchor(string text)
    {
        var trimmedStart = text.TrimStart();
        if (!trimmedStart.StartsWith("#[", StringComparison.Ordinal))
            return text;

        var closeIndex = trimmedStart.IndexOf(']');
        if (closeIndex <= 2)
            return text;

        var anchor = trimmedStart[2..closeIndex].Trim();
        if (anchor.Length == 0)
            return text;

        var looksLikeTurnAnchor =
            anchor.All(char.IsDigit) ||
            anchor.Contains("turn", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeTurnAnchor)
            return text;

        var rest = trimmedStart[(closeIndex + 1)..].TrimStart();
        if (rest.StartsWith(".", StringComparison.Ordinal) ||
            rest.StartsWith("-", StringComparison.Ordinal) ||
            rest.StartsWith(":", StringComparison.Ordinal))
        {
            return rest[1..].TrimStart();
        }

        return text;
    }
}

