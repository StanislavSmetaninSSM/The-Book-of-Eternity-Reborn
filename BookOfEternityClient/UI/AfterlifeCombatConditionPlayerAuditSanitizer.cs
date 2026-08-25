using System.Text.Json.Nodes;

namespace BookOfEternityClient.UI;

internal static class AfterlifeCombatConditionPlayerAuditSanitizer
{
    private static readonly string[] HiddenConditionTokenFields =
    [
        "conditionId",
        "displayName",
        "name",
        "summary",
        "auditRequirement"
    ];

    private static readonly string[] RollModeConditionReferenceFields =
    [
        "conditionId",
        "sourceId",
        "id",
        "source",
        "summary"
    ];

    public static JsonNode? Sanitize(JsonNode? root)
    {
        if (root == null)
            return null;

        var clone = root.DeepClone();
        SanitizeNode(clone, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return clone;
    }

    public static JsonNode? Sanitize(
        JsonNode? root,
        EffectPlayerProjectionResult projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var clone = Sanitize(root);
        if (clone == null)
            return null;

        ReplaceCombatConditionsWithAcceptedProjection(clone, projection);
        ScrubTechnicalConditionReferences(clone);
        return clone;
    }

    public static bool IsVisibleToPlayer(JsonObject condition)
    {
        if (condition["visibleToPlayer"] is JsonValue visibleValue &&
            visibleValue.TryGetValue<bool>(out var visibleToPlayer) &&
            !visibleToPlayer)
        {
            return false;
        }

        var visibility = NormalizeKey(ReadString(condition["visibility"]));
        var audience = NormalizeKey(ReadString(condition["audience"]));
        return !IsHiddenVisibility(visibility) &&
               !IsHiddenVisibility(audience);
    }

    private static void SanitizeNode(JsonNode? node, HashSet<string> inheritedHiddenConditionTokens)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var hiddenConditionTokens = inheritedHiddenConditionTokens;
                if (obj["combatConditions"] is JsonArray combatConditions)
                {
                    hiddenConditionTokens = MergeHiddenConditionTokens(inheritedHiddenConditionTokens, combatConditions);
                    obj["combatConditions"] = FilterVisibleCombatConditions(combatConditions);
                }

                SanitizeRollModeSources(obj, hiddenConditionTokens);

                foreach (var child in obj.Select(static property => property.Value).ToArray())
                    SanitizeNode(child, hiddenConditionTokens);
                break;
            }
            case JsonArray array:
            {
                foreach (var child in array.ToArray())
                    SanitizeNode(child, inheritedHiddenConditionTokens);
                break;
            }
        }
    }

    private static HashSet<string> MergeHiddenConditionTokens(
        HashSet<string> inheritedHiddenConditionTokens,
        JsonArray combatConditions)
    {
        var hiddenConditionTokens = new HashSet<string>(
            inheritedHiddenConditionTokens,
            StringComparer.OrdinalIgnoreCase);

        foreach (var condition in combatConditions.OfType<JsonObject>())
        {
            if (IsVisibleToPlayer(condition))
                continue;

            foreach (var field in HiddenConditionTokenFields)
                AddHiddenConditionToken(hiddenConditionTokens, condition[field]);
        }

        return hiddenConditionTokens;
    }

    private static void AddHiddenConditionToken(HashSet<string> hiddenConditionTokens, JsonNode? node)
    {
        var token = ReadString(node)?.Trim();
        if (!string.IsNullOrWhiteSpace(token))
            hiddenConditionTokens.Add(token);
    }

    private static JsonArray FilterVisibleCombatConditions(JsonArray combatConditions)
    {
        var visible = new JsonArray();
        foreach (var condition in combatConditions.OfType<JsonObject>().Where(IsVisibleToPlayer))
            visible.Add(condition.DeepClone());
        return visible;
    }

    private static void ReplaceCombatConditionsWithAcceptedProjection(
        JsonNode? node,
        EffectPlayerProjectionResult projection)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("combatConditions"))
                {
                    var visible = new JsonArray();
                    var conflictId = ReadString(obj["conflictId"])?.Trim();
                    var targetPrefix = string.IsNullOrWhiteSpace(conflictId)
                        ? null
                        : conflictId + ":";
                    if (projection.IsAvailable && targetPrefix != null)
                    {
                        foreach (var entry in projection.Entries.Where(entry =>
                                     string.Equals(entry.TargetKind, "spiritual_conflict_side", StringComparison.Ordinal) &&
                                     entry.TargetId.StartsWith(targetPrefix, StringComparison.Ordinal)))
                        {
                            var facts = new JsonArray();
                            foreach (var fact in entry.Facts)
                            {
                                facts.Add(new JsonObject
                                {
                                    ["label"] = fact.Label,
                                    ["value"] = fact.Value
                                });
                            }

                            var actions = new JsonArray();
                            foreach (var action in entry.Actions)
                            {
                                actions.Add(new JsonObject
                                {
                                    ["label"] = action.Label,
                                    ["description"] = action.Description
                                });
                            }

                            visible.Add(new JsonObject
                            {
                                ["name"] = entry.Name,
                                ["summary"] = entry.Summary,
                                ["state"] = entry.State,
                                ["facts"] = facts,
                                ["actions"] = actions
                            });
                        }
                    }

                    obj["combatConditions"] = visible;
                    obj["combatConditionsStatus"] = projection.IsAvailable
                        ? "accepted_player_projection"
                        : "unavailable";
                }

                foreach (var child in obj.Select(static property => property.Value).ToArray())
                    ReplaceCombatConditionsWithAcceptedProjection(child, projection);
                break;
            case JsonArray array:
                foreach (var child in array.ToArray())
                    ReplaceCombatConditionsWithAcceptedProjection(child, projection);
                break;
        }
    }

    private static void ScrubTechnicalConditionReferences(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var sourceType = NormalizeKey(
                    ReadString(obj["sourceType"]) ?? ReadString(obj["type"]));
                if (sourceType == "combat_condition" || obj.ContainsKey("conditionId"))
                {
                    foreach (var field in RollModeConditionReferenceFields)
                        obj.Remove(field);
                }

                foreach (var child in obj.Select(static property => property.Value).ToArray())
                    ScrubTechnicalConditionReferences(child);
                break;
            case JsonArray array:
                foreach (var child in array.ToArray())
                    ScrubTechnicalConditionReferences(child);
                break;
        }
    }

    private static void SanitizeRollModeSources(
        JsonObject obj,
        IReadOnlySet<string> hiddenConditionTokens)
    {
        if (hiddenConditionTokens.Count == 0 ||
            obj["rollMode"] is not JsonObject rollMode)
        {
            return;
        }

        foreach (var sideProperty in rollMode.ToArray())
        {
            if (sideProperty.Value is not JsonObject sideMode)
                continue;

            if (sideMode["advantageSources"] is JsonArray advantageSources)
                sideMode["advantageSources"] = FilterRollModeSources(advantageSources, hiddenConditionTokens);
            if (sideMode["disadvantageSources"] is JsonArray disadvantageSources)
                sideMode["disadvantageSources"] = FilterRollModeSources(disadvantageSources, hiddenConditionTokens);
        }
    }

    private static JsonArray FilterRollModeSources(
        JsonArray sources,
        IReadOnlySet<string> hiddenConditionTokens)
    {
        var visibleSources = new JsonArray();
        foreach (var source in sources)
        {
            if (IsHiddenCombatConditionRollModeSource(source, hiddenConditionTokens))
            {
                continue;
            }

            visibleSources.Add(source?.DeepClone());
        }

        return visibleSources;
    }

    private static bool IsHiddenCombatConditionRollModeSource(
        JsonNode? source,
        IReadOnlySet<string> hiddenConditionTokens)
    {
        if (source is not JsonObject sourceObject)
        {
            var legacySource = ReadString(source)?.Trim();
            return !string.IsNullOrWhiteSpace(legacySource) &&
                   hiddenConditionTokens.Contains(legacySource);
        }

        var sourceType = NormalizeKey(ReadString(sourceObject["sourceType"]) ?? ReadString(sourceObject["type"]));
        var conditionId = ReadString(sourceObject["conditionId"]);
        var isConditionBacked =
            sourceType == "combat_condition" ||
            !string.IsNullOrWhiteSpace(conditionId);
        if (!isConditionBacked)
            return false;

        return RollModeConditionReferenceFields
            .Select(field => ReadString(sourceObject[field])?.Trim())
            .Any(value => !string.IsNullOrWhiteSpace(value) && hiddenConditionTokens.Contains(value));
    }

    private static bool IsHiddenVisibility(string? visibility) =>
        visibility is "hidden" or "gm_only" or "private" or "secret" or "concealed" or "spoiler";

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? ReadString(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return string.IsNullOrWhiteSpace(text) ? null : text;
            if (value.TryGetValue<int>(out var intValue))
                return intValue.ToString();
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue ? "true" : "false";
        }

        return null;
    }
}
