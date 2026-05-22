using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class ChaosSeaGuardianPoliticsState
{
    public const string StatePath = "game_state/meta/chaos_sea_guardian_politics.json";
    public const string RelationsProperty = "relations";
    public const string ProjectsProperty = "projects";
    public const string InfluenceZonesProperty = "influenceZones";
    public const string ChronicleProperty = "chronicle";
    public const string RelationUpdatesProperty = "guardianPoliticalRelationUpdates";
    public const string ProjectUpdatesProperty = "guardianPoliticalProjectUpdates";
    public const string InfluenceUpdatesProperty = "guardianPoliticalInfluenceUpdates";
    public const string ChronicleUpdatesProperty = "guardianPoliticalChronicleUpdates";
    public const string CompleteProjectsProperty = "completeGuardianPoliticalProjects";
    public const string LastInvalidCommandProperty = "lastInvalidGuardianPoliticalCommand";
    public const string LastInvalidCommandReasonProperty = "lastInvalidGuardianPoliticalCommandReason";
    public const int SchemaVersion = 1;

    public static readonly HashSet<string> RelationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "alliance",
        "rivalry",
        "debt",
        "fear",
        "patronage",
        "memory_oath",
        "trade",
        "hostility",
        "hidden_dependency"
    };

    public static readonly HashSet<string> Visibilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "known",
        "rumored",
        "hidden",
        "gm_only"
    };

    public static readonly HashSet<string> ProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "blocked",
        "completed",
        "failed",
        "abandoned"
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            [RelationsProperty] = new JsonArray(),
            [ProjectsProperty] = new JsonArray(),
            [InfluenceZonesProperty] = new JsonArray(),
            [ChronicleProperty] = new JsonArray(),
            ["playerRole"] = null,
            ["sarefLinks"] = new JsonArray(),
            ["openConflicts"] = new JsonArray()
        };

    public static JsonObject ProjectCanonicalRoot(JsonObject? currentRoot, JsonObject? previousRoot)
    {
        var result = CreateDefaultRoot();
        UpsertArray(result, RelationsProperty, "relationId", previousRoot?[RelationsProperty]);
        UpsertArray(result, ProjectsProperty, "projectId", previousRoot?[ProjectsProperty]);
        UpsertArray(result, InfluenceZonesProperty, "zoneId", previousRoot?[InfluenceZonesProperty]);
        UpsertArray(result, ChronicleProperty, "entryId", previousRoot?[ChronicleProperty]);

        UpsertArray(result, RelationsProperty, "relationId", currentRoot?[RelationsProperty]);
        UpsertArray(result, ProjectsProperty, "projectId", currentRoot?[ProjectsProperty]);
        UpsertArray(result, InfluenceZonesProperty, "zoneId", currentRoot?[InfluenceZonesProperty]);
        UpsertArray(result, ChronicleProperty, "entryId", currentRoot?[ChronicleProperty]);
        CopyIfPresent(currentRoot, result, "playerRole");
        CopyIfPresent(currentRoot, result, "sarefLinks");
        CopyIfPresent(currentRoot, result, "openConflicts");

        ApplyRelationUpdates(result, currentRoot?[RelationUpdatesProperty]);
        ApplyProjectUpdates(result, currentRoot?[ProjectUpdatesProperty]);
        ApplyInfluenceUpdates(result, currentRoot?[InfluenceUpdatesProperty]);
        ApplyChronicleUpdates(result, currentRoot?[ChronicleUpdatesProperty]);
        ApplyProjectCompletions(result, currentRoot?[CompleteProjectsProperty]);

        result.Remove(RelationUpdatesProperty);
        result.Remove(ProjectUpdatesProperty);
        result.Remove(InfluenceUpdatesProperty);
        result.Remove(ChronicleUpdatesProperty);
        result.Remove(CompleteProjectsProperty);
        return result;
    }

    private static void ApplyRelationUpdates(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var command in EnumerateCommandObjects(result, commandsNode, RelationUpdatesProperty))
            UpsertById(EnsureArray(result, RelationsProperty), command, "relationId");
    }

    private static void ApplyProjectUpdates(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var command in EnumerateCommandObjects(result, commandsNode, ProjectUpdatesProperty))
            UpsertById(EnsureArray(result, ProjectsProperty), command, "projectId");
    }

    private static void ApplyInfluenceUpdates(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var command in EnumerateCommandObjects(result, commandsNode, InfluenceUpdatesProperty))
            UpsertById(EnsureArray(result, InfluenceZonesProperty), command, "zoneId");
    }

    private static void ApplyChronicleUpdates(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var command in EnumerateCommandObjects(result, commandsNode, ChronicleUpdatesProperty))
            UpsertById(EnsureArray(result, ChronicleProperty), command, "entryId");
    }

    private static void ApplyProjectCompletions(JsonObject result, JsonNode? commandsNode)
    {
        foreach (var completion in EnumerateCommandObjects(result, commandsNode, CompleteProjectsProperty))
        {
            var projectId = GetNodeString(completion["projectId"]);
            if (string.IsNullOrWhiteSpace(projectId))
            {
                MarkInvalid(result, completion, "completeGuardianPoliticalProjects requires projectId");
                continue;
            }

            var project = FindById(EnsureArray(result, ProjectsProperty), "projectId", projectId);
            if (project == null)
            {
                MarkInvalid(result, completion, $"completeGuardianPoliticalProjects target not found: {projectId}");
                continue;
            }

            project["status"] = GetNodeString(completion["finalState"]) ?? GetNodeString(completion["status"]) ?? "completed";
            CopyIfPresent(completion, project, "completedAtTurn");
            CopyIfPresent(completion, project, "completionSummary");
            CopyIfPresent(completion, project, "outcome");
            CopyIfPresent(completion, project, "consequences");
            CopyIfPresent(completion, project, "visibility");
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

    private static void UpsertArray(JsonObject result, string propertyName, string idProperty, JsonNode? sourceNode)
    {
        if (sourceNode is not JsonArray source)
            return;

        var target = EnsureArray(result, propertyName);
        foreach (var item in source.OfType<JsonObject>())
            UpsertById(target, item, idProperty);
    }

    private static void UpsertById(JsonArray target, JsonObject item, string idProperty)
    {
        var id = GetNodeString(item[idProperty]);
        if (string.IsNullOrWhiteSpace(id))
        {
            target.Add(CloneObject(item));
            return;
        }

        for (var index = 0; index < target.Count; index++)
        {
            if (target[index] is JsonObject existing &&
                string.Equals(GetNodeString(existing[idProperty]), id, StringComparison.OrdinalIgnoreCase))
            {
                target[index] = CloneObject(item);
                return;
            }
        }

        target.Add(CloneObject(item));
    }

    private static JsonObject? FindById(JsonArray items, string idProperty, string id)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(item[idProperty]), id, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static void CopyIfPresent(JsonObject? source, JsonObject target, string propertyName)
    {
        if (source == null || !source.TryGetPropertyValue(propertyName, out var value))
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
