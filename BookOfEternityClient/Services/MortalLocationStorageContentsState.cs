using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal readonly record struct MortalLocationStorageKey(
    string LocationId,
    string StorageId);

internal sealed record MortalLocationStorageContentsParseResult(
    JsonObject Root,
    IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> Entries,
    IReadOnlyList<ValidationIssue> Issues);

internal static class MortalLocationStorageContentsState
{
    internal const string StatePath =
        "game_state/world/location_storage_contents.json";

    private static readonly HashSet<string> RootFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "entries"
    };

    private static readonly HashSet<string> EntryFields = new(StringComparer.Ordinal)
    {
        "locationId",
        "storageId",
        "contents"
    };

    internal static JsonObject CreateEmptyRoot() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    internal static MortalLocationStorageContentsParseResult Parse(JsonObject? root)
    {
        if (root == null)
        {
            return new MortalLocationStorageContentsParseResult(
                CreateEmptyRoot(),
                new Dictionary<MortalLocationStorageKey, JsonArray>(),
                Array.Empty<ValidationIssue>());
        }

        var issues = new List<ValidationIssue>();
        if (!RootFields.SetEquals(root.Select(static property => property.Key)) ||
            !TryReadInt(root["schemaVersion"], out var schemaVersion) ||
            schemaVersion != 1 ||
            root["entries"] is not JsonArray rawEntries)
        {
            issues.Add(Issue(
                StatePath,
                "mortal_location_storage_contents_invalid_root",
                "The offscreen location-storage item authority must use the exact closed root.",
                "schemaVersion=1 and entries[] only",
                root.ToJsonString()));
            return new MortalLocationStorageContentsParseResult(
                CreateEmptyRoot(),
                new Dictionary<MortalLocationStorageKey, JsonArray>(),
                issues);
        }

        var entries = new Dictionary<MortalLocationStorageKey, JsonArray>();
        var confusableCoordinates = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rawEntries.Count; index++)
        {
            var path = $"{StatePath}.entries[{index}]";
            if (rawEntries[index] is not JsonObject entry ||
                !EntryFields.SetEquals(entry.Select(static property => property.Key)) ||
                !TryReadExactString(entry["locationId"], out var locationId) ||
                !TryReadExactString(entry["storageId"], out var storageId) ||
                entry["contents"] is not JsonArray contents)
            {
                issues.Add(Issue(
                    path,
                    "mortal_location_storage_contents_entry_invalid",
                    "An offscreen storage entry must use one exact closed coordinate and an item array.",
                    "locationId, storageId, and contents[] only",
                    rawEntries[index]?.ToJsonString() ?? "null"));
                continue;
            }

            if (contents.Count == 0)
            {
                issues.Add(Issue(
                    path + ".contents",
                    "mortal_location_storage_contents_empty_entry",
                    "Empty offscreen storage coordinates are not persisted.",
                    "one or more item objects",
                    "empty array"));
                continue;
            }

            var key = new MortalLocationStorageKey(locationId, storageId);
            if (entries.ContainsKey(key))
            {
                issues.Add(Issue(
                    path,
                    "mortal_location_storage_contents_coordinate_duplicate",
                    "An offscreen storage coordinate may occur only once.",
                    "one exact locationId/storageId coordinate",
                    $"{locationId}/{storageId}"));
                continue;
            }

            var confusableKey = BuildConfusableKey(key);
            if (!confusableCoordinates.Add(confusableKey))
            {
                issues.Add(Issue(
                    path,
                    "mortal_location_storage_contents_coordinate_confusable",
                    "Offscreen storage coordinates cannot use case, whitespace, or Unicode-confusable aliases.",
                    "one exact and non-confusable locationId/storageId coordinate",
                    $"{locationId}/{storageId}"));
                continue;
            }

            entries.Add(key, contents.DeepClone().AsArray());
        }

        var canonicalRoot = BuildCanonicalRoot(entries);
        var canonicalEntries = ReadCanonicalEntries(canonicalRoot);
        return new MortalLocationStorageContentsParseResult(
            canonicalRoot,
            canonicalEntries,
            issues);
    }

    internal static JsonObject BuildCanonicalRoot(
        IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var values = new JsonArray();
        foreach (var pair in entries
                     .OrderBy(static pair => pair.Key.LocationId, StringComparer.Ordinal)
                     .ThenBy(static pair => pair.Key.StorageId, StringComparer.Ordinal))
        {
            if (!MortalItemIdentityRules.IsExactIdentity(pair.Key.LocationId) ||
                !MortalItemIdentityRules.IsExactIdentity(pair.Key.StorageId))
            {
                throw new ArgumentException(
                    "Offscreen storage coordinates must use exact identities.",
                    nameof(entries));
            }
            if (pair.Value.Count == 0)
            {
                throw new ArgumentException(
                    "Offscreen storage entries must not be empty.",
                    nameof(entries));
            }

            values.Add(new JsonObject
            {
                ["locationId"] = pair.Key.LocationId,
                ["storageId"] = pair.Key.StorageId,
                ["contents"] = pair.Value.DeepClone()
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = values
        };
    }

    private static IReadOnlyDictionary<MortalLocationStorageKey, JsonArray>
        ReadCanonicalEntries(JsonObject root)
    {
        var result = new Dictionary<MortalLocationStorageKey, JsonArray>();
        foreach (var entry in root["entries"]!.AsArray().OfType<JsonObject>())
        {
            var key = new MortalLocationStorageKey(
                entry["locationId"]!.GetValue<string>(),
                entry["storageId"]!.GetValue<string>());
            result.Add(key, entry["contents"]!.AsArray());
        }
        return result;
    }

    private static string BuildConfusableKey(MortalLocationStorageKey key) =>
        MortalLocationIdentityState.BuildConfusableKey(key.LocationId) +
        "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(key.StorageId);

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue(out value);
    }

    private static bool TryReadExactString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var text) ||
            text == null ||
            !MortalItemIdentityRules.IsExactIdentity(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: "mortal_location_storage_contents",
            section: "MortalLocationMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore the exact client-owned offscreen storage authority from the validated pre-turn snapshot.",
            repairTargetFiles: new[] { StatePath });
}
