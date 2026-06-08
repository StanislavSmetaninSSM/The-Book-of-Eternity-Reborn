using System.Text.Json.Nodes;

namespace BookOfEternityClient.UI;

internal static class AfterlifeCombatConditionPlayerAuditSanitizer
{
    private static readonly string[] HiddenConditionIdentityFields =
    [
        "conditionId",
        "displayName",
        "name"
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

            foreach (var field in HiddenConditionIdentityFields)
            {
                var identityValue = ReadString(condition[field]);
                if (!string.IsNullOrWhiteSpace(identityValue))
                    hiddenConditionTokens.Add(identityValue);
            }
        }

        return hiddenConditionTokens;
    }

    private static JsonArray FilterVisibleCombatConditions(JsonArray combatConditions)
    {
        var visible = new JsonArray();
        foreach (var condition in combatConditions.OfType<JsonObject>().Where(IsVisibleToPlayer))
            visible.Add(condition.DeepClone());
        return visible;
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
            if (source is JsonObject sourceObject &&
                IsHiddenCombatConditionRollModeSource(sourceObject, hiddenConditionTokens))
            {
                continue;
            }

            visibleSources.Add(source?.DeepClone());
        }

        return visibleSources;
    }

    private static bool IsHiddenCombatConditionRollModeSource(
        JsonObject source,
        IReadOnlySet<string> hiddenConditionTokens)
    {
        var sourceType = NormalizeKey(ReadString(source["sourceType"]) ?? ReadString(source["type"]));
        var conditionId = ReadString(source["conditionId"]);
        var isConditionBacked =
            sourceType == "combat_condition" ||
            !string.IsNullOrWhiteSpace(conditionId);
        if (!isConditionBacked)
            return false;

        return RollModeConditionReferenceFields
            .Select(field => ReadString(source[field]))
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
