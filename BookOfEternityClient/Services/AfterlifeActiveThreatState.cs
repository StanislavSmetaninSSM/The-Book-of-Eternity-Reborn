using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeActiveThreatState
{
    public const string StatePath = "game_state/meta/afterlife_active_threats.json";
    public const string ThreatsProperty = "threats";
    public const string AddsProperty = "afterlifeThreatsToAdd";
    public const string UpdatesProperty = "afterlifeThreatsToUpdate";
    public const string CompleteActivitiesProperty = "completeAfterlifeThreatActivities";
    public const string RemovalsProperty = "afterlifeThreatsToRemove";
    public const string LastInvalidCommandProperty = "lastInvalidThreatCommand";
    public const string LastInvalidCommandReasonProperty = "lastInvalidThreatCommandReason";
    public const int SchemaVersion = 1;

    public static readonly HashSet<string> Realms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Chaos Sea",
        "Море Хаоса",
        "Shining Abode",
        "Сияющая Обитель"
    };

    public static readonly HashSet<string> ImpactTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Faction",
        "Location",
        "Resource",
        "Guardian",
        "Resident",
        "Actor",
        "Realm",
        "Scope"
    };

    public static readonly HashSet<string> ImpactTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Military",
        "Economic",
        "Social",
        "Covert",
        "Stability",
        "Environment",
        "Combat",
        "Politics",
        "Relationship",
        "Progression"
    };

    public static readonly HashSet<string> TerminalActivityStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "abandoned",
        "failed",
        "cancelled"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [ThreatsProperty] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();
        UpsertThreats(result, previousRoot?[ThreatsProperty]);
        UpsertThreats(result, currentRoot?[ThreatsProperty]);
        ApplyThreatAdds(result, currentRoot?[AddsProperty]);
        ApplyThreatUpdates(result, currentRoot?[UpdatesProperty]);
        ApplyCompleteThreatActivities(result, currentRoot?[CompleteActivitiesProperty]);
        ApplyThreatRemovals(result, currentRoot?[RemovalsProperty]);

        result.Remove(AddsProperty);
        result.Remove(UpdatesProperty);
        result.Remove(CompleteActivitiesProperty);
        result.Remove(RemovalsProperty);
        return result;
    }

    private static void ApplyThreatAdds(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var command in EnumerateCommandObjects(result, commandsNode, AddsProperty))
            UpsertThreat(EnsureThreats(result), command);
    }

    private static void ApplyThreatUpdates(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var update in EnumerateCommandObjects(result, commandsNode, UpdatesProperty))
        {
            var threatId = GetNodeString(update["threatId"]);
            if (string.IsNullOrWhiteSpace(threatId))
            {
                MarkInvalid(result, update, "afterlifeThreatsToUpdate requires threatId");
                continue;
            }

            var threat = FindThreat(EnsureThreats(result), threatId);
            if (threat == null)
            {
                MarkInvalid(result, update, $"afterlifeThreatsToUpdate target not found: {threatId}");
                continue;
            }

            CopyIfPresent(update, threat, "realm");
            CopyIfPresent(update, threat, "scopeId");
            CopyIfPresent(update, threat, "displayName");
            CopyIfPresent(update, threat, "threatArchetype");
            CopyIfPresent(update, threat, "intensity");
            CopyIfPresent(update, threat, "impactProfile");
            CopyIfPresent(update, threat, "visibleToPlayer");
            CopyIfPresent(update, threat, "linkedFactionId");
            CopyIfPresent(update, threat, "linkedGuardianId");
            CopyIfPresent(update, threat, "sarefLink");

            if (update.TryGetPropertyValue("currentActivity", out var activityNode))
                ApplyCurrentActivityPatch(result, threat, update, activityNode);

            if (update["ledgerEntry"] is JsonObject ledgerEntry)
                EnsureLedger(threat).Add(CloneObject(ledgerEntry));
        }
    }

    private static void ApplyCurrentActivityPatch(
        JsonObject result,
        JsonObject threat,
        JsonObject update,
        JsonNode? activityNode)
    {
        if (activityNode == null)
        {
            MarkInvalid(result, update, "afterlifeThreatsToUpdate must not clear currentActivity; use completeAfterlifeThreatActivities");
            return;
        }

        if (activityNode is not JsonObject patch)
        {
            MarkInvalid(result, update, "afterlifeThreatsToUpdate.currentActivity must be object");
            return;
        }

        var activeState = GetNodeString(patch["activeState"]);
        if (!string.IsNullOrWhiteSpace(activeState) && TerminalActivityStates.Contains(activeState))
        {
            MarkInvalid(result, update, "afterlifeThreatsToUpdate must not complete currentActivity; use completeAfterlifeThreatActivities");
            return;
        }

        var activity = threat["currentActivity"] as JsonObject ?? new JsonObject();
        foreach (var property in patch)
            activity[property.Key] = property.Value?.DeepClone();
        threat["currentActivity"] = activity;
    }

    private static void ApplyCompleteThreatActivities(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var completion in EnumerateCommandObjects(result, commandsNode, CompleteActivitiesProperty))
        {
            var threatId = GetNodeString(completion["threatId"]);
            if (string.IsNullOrWhiteSpace(threatId))
            {
                MarkInvalid(result, completion, "completeAfterlifeThreatActivities requires threatId");
                continue;
            }

            var threat = FindThreat(EnsureThreats(result), threatId);
            if (threat == null)
            {
                MarkInvalid(result, completion, $"completeAfterlifeThreatActivities target not found: {threatId}");
                continue;
            }

            if (threat["currentActivity"] is not JsonObject currentActivity)
            {
                MarkInvalid(result, completion, $"completeAfterlifeThreatActivities requires active currentActivity for {threatId}");
                continue;
            }

            var expectedActivityId = GetNodeString(completion["activityId"]);
            var currentActivityId = GetNodeString(currentActivity["activityId"]);
            if (!string.IsNullOrWhiteSpace(expectedActivityId) &&
                !string.IsNullOrWhiteSpace(currentActivityId) &&
                !string.Equals(expectedActivityId, currentActivityId, StringComparison.OrdinalIgnoreCase))
            {
                MarkInvalid(result, completion, $"completeAfterlifeThreatActivities.activityId does not match currentActivity for {threatId}");
                continue;
            }

            var ledgerEntry = CloneObject(currentActivity);
            CopyIfPresent(completion, ledgerEntry, "activityId");
            CopyIfPresent(completion, ledgerEntry, "finalState");
            CopyIfPresent(completion, ledgerEntry, "completionSummary");
            CopyIfPresent(completion, ledgerEntry, "completedAtTurn");
            CopyIfPresent(completion, ledgerEntry, "consequences");
            EnsureLedger(threat).Add(ledgerEntry);
            threat["currentActivity"] = null;
        }
    }

    private static void ApplyThreatRemovals(JsonObject result, JsonNode? commandsNode)
    {
        var threats = EnsureThreats(result);
        foreach (var removal in EnumerateCommandObjects(result, commandsNode, RemovalsProperty))
        {
            var threatId = GetNodeString(removal["threatId"]);
            if (string.IsNullOrWhiteSpace(threatId))
            {
                MarkInvalid(result, removal, "afterlifeThreatsToRemove requires threatId");
                continue;
            }

            for (var index = threats.Count - 1; index >= 0; index--)
            {
                if (threats[index] is JsonObject threat &&
                    string.Equals(GetNodeString(threat["threatId"]), threatId, StringComparison.OrdinalIgnoreCase))
                {
                    threats.RemoveAt(index);
                    break;
                }
            }
        }
    }

    private static IEnumerable<JsonObject> EnumerateCommandObjects(JsonObject result, JsonNode? commandsNode, string propertyName)
    {
        if (commandsNode == null)
            yield break;

        if (commandsNode is JsonObject singleCommand)
        {
            yield return singleCommand;
            yield break;
        }

        if (commandsNode is not JsonArray commands)
        {
            result[LastInvalidCommandProperty] = commandsNode.DeepClone();
            result[LastInvalidCommandReasonProperty] = $"{propertyName} must be object or array";
            yield break;
        }

        foreach (var command in commands)
        {
            if (command is JsonObject commandObject)
            {
                yield return commandObject;
                continue;
            }

            result[LastInvalidCommandProperty] = command?.DeepClone();
            result[LastInvalidCommandReasonProperty] = $"{propertyName} entries must be objects";
        }
    }

    private static void UpsertThreats(JsonObject result, JsonNode? threatsNode)
    {
        if (threatsNode is not JsonArray threats)
            return;

        var resultThreats = EnsureThreats(result);
        foreach (var threat in threats.OfType<JsonObject>())
            UpsertThreat(resultThreats, threat);
    }

    private static void UpsertThreat(JsonArray threats, JsonObject threat)
    {
        var threatId = GetNodeString(threat["threatId"]);
        if (string.IsNullOrWhiteSpace(threatId))
        {
            threats.Add(CloneObject(threat));
            return;
        }

        for (var index = 0; index < threats.Count; index++)
        {
            if (threats[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing["threatId"]), threatId, StringComparison.OrdinalIgnoreCase))
            {
                threats[index] = CloneObject(threat);
                return;
            }
        }

        threats.Add(CloneObject(threat));
    }

    private static JsonObject? FindThreat(JsonArray threats, string threatId)
    {
        foreach (var threat in threats.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(threat["threatId"]), threatId, StringComparison.OrdinalIgnoreCase))
                return threat;
        }

        return null;
    }

    private static JsonArray EnsureThreats(JsonObject root) =>
        EnsureArray(root, ThreatsProperty);

    private static JsonArray EnsureLedger(JsonObject threat) =>
        EnsureArray(threat, "ledger");

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray existing)
            return existing;

        var array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static void CopyIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out var value))
            return;

        target[propertyName] = value?.DeepClone();
    }

    private static void MarkInvalid(JsonObject root, JsonObject command, string reason)
    {
        root[LastInvalidCommandProperty] = command.DeepClone();
        root[LastInvalidCommandReasonProperty] = reason;
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone().AsObject();

    public static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        if (node is JsonValue value && value.TryGetValue<string>(out var result))
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();

        return null;
    }
}
