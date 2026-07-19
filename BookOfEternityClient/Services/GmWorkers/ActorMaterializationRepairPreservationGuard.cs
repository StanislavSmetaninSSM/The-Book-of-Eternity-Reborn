using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services.GmWorkers;

internal static class ActorMaterializationRepairPreservationGuard
{
    private static readonly HashSet<string> RepairCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "actor_materialization_missing",
        "actor_materialization_invalid_envelope",
        "actor_materialization_actor_binding_mismatch",
        "actor_materialization_duplicate_id",
        "actor_materialization_duplicate_property",
        "actor_materialization_invalid_actor_type",
        "actor_materialization_inventory_reference_mismatch",
        "actor_materialization_section_missing",
        "actor_materialization_section_content_mismatch",
        "actor_materialization_capability_mismatch",
        "actor_materialization_existing_resend_forbidden",
        "actor_materialization_historical_envelope_changed",
        "afterlife_actor_materialization_profile_missing",
        "afterlife_actor_materialization_profile_ambiguous",
        "afterlife_actor_materialization_memory_missing"
    };

    internal static IReadOnlyList<string> Validate(
        string path,
        string? baselineJson,
        string? proposedJson,
        IReadOnlyList<WorkerValidationIssue> issues)
    {
        var relevantIssues = issues
            .Where(issue => RepairCodes.Contains(issue.Code) && IssueTargetsPath(issue, path))
            .ToArray();
        if (relevantIssues.Length == 0)
            return [];

        if (string.IsNullOrWhiteSpace(baselineJson) || string.IsNullOrWhiteSpace(proposedJson))
        {
            return
            [
                $"Actor materialization repair must replace an existing JSON file while preserving protected actor data: {path}."
            ];
        }

        JsonNode? baselineRoot;
        JsonNode? proposedRoot;
        try
        {
            baselineRoot = JsonNode.Parse(baselineJson);
            proposedRoot = JsonNode.Parse(proposedJson);
        }
        catch (JsonException)
        {
            return [$"Actor materialization preservation check requires valid JSON for {path}."];
        }

        if (baselineRoot == null || proposedRoot == null)
            return [$"Actor materialization preservation check requires JSON roots for {path}."];

        var baselineCopy = baselineRoot.DeepClone();
        var proposedCopy = proposedRoot.DeepClone();
        var errors = new List<string>();
        foreach (var actorGroup in relevantIssues.GroupBy(issue => issue.Actor, StringComparer.Ordinal))
        {
            if (!TryParseActorIdentity(actorGroup.Key, out var actorType, out var actorId))
            {
                errors.Add(
                    $"Actor materialization repair issue must include exact actorType:actorId coordinates for {path}.");
                continue;
            }

            var baselineActors = FindActors(baselineCopy, path, actorType, actorId);
            var proposedActors = FindActors(proposedCopy, path, actorType, actorId);
            if (actorGroup.Any(issue => string.Equals(
                    issue.Code,
                    "afterlife_actor_materialization_profile_missing",
                    StringComparison.OrdinalIgnoreCase)) &&
                baselineActors.Count == 0)
            {
                if (proposedActors.Count != 1)
                {
                    errors.Add(
                        $"Actor materialization repair may add exactly one missing profile for {actorType}:{actorId}; found {proposedActors.Count}.");
                    continue;
                }

                proposedActors[0].Parent.Remove(proposedActors[0].Actor);
                continue;
            }

            if (actorGroup.Any(issue => string.Equals(
                    issue.Code,
                    "afterlife_actor_materialization_profile_ambiguous",
                    StringComparison.OrdinalIgnoreCase)))
            {
                if (!TryNormalizeAmbiguousProfiles(baselineActors, proposedActors))
                {
                    errors.Add(
                        $"Actor materialization ambiguity repair for {actorType}:{actorId} must keep one unchanged canonical profile and remove only duplicates.");
                }
                continue;
            }

            if (baselineActors.Count == 0 || baselineActors.Count != proposedActors.Count)
            {
                errors.Add(
                    $"Actor materialization repair cannot prove protected actor data for {actorType}:{actorId}: baseline={baselineActors.Count}, proposal={proposedActors.Count}.");
                continue;
            }

            var mutablePaths = ResolveMutablePaths(actorGroup);
            for (var index = 0; index < baselineActors.Count; index++)
            {
                foreach (var mutablePath in mutablePaths)
                {
                    RemovePath(baselineActors[index].Actor, mutablePath);
                    RemovePath(proposedActors[index].Actor, mutablePath);
                }
            }
        }

        if (errors.Count == 0 && !JsonNode.DeepEquals(baselineCopy, proposedCopy))
        {
            errors.Add(
                $"Actor materialization repair changed protected actor data or unrelated canonical data in {path}; only exact issue targets may change.");
        }

        return errors;
    }

    private static bool TryNormalizeAmbiguousProfiles(
        IReadOnlyList<ActorNode> baselineActors,
        IReadOnlyList<ActorNode> proposedActors)
    {
        if (baselineActors.Count < 2 || proposedActors.Count != 1 ||
            !baselineActors.Any(candidate => JsonNode.DeepEquals(candidate.Actor, proposedActors[0].Actor)))
        {
            return false;
        }

        foreach (var actor in baselineActors.Reverse())
            actor.Parent.Remove(actor.Actor);
        proposedActors[0].Parent.Remove(proposedActors[0].Actor);
        return true;
    }

    private static IReadOnlyList<string[]> ResolveMutablePaths(IEnumerable<WorkerValidationIssue> issues)
    {
        var paths = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var issue in issues)
        {
            var code = issue.Code.ToLowerInvariant();
            var section = issue.Section?.Trim();
            switch (code)
            {
                case "actor_materialization_section_missing":
                case "actor_materialization_section_content_mismatch":
                    if (!string.IsNullOrWhiteSpace(section))
                        Add(["materialization", "sections", section]);
                    break;
                case "actor_materialization_capability_mismatch":
                    if (!string.IsNullOrWhiteSpace(section))
                        Add(["materialization", "capabilities", section]);
                    break;
                case "actor_materialization_inventory_reference_mismatch":
                    Add(["equippedItems"]);
                    break;
                case "afterlife_actor_materialization_memory_missing":
                    Add(["gmThoughtsSummary"]);
                    Add(["ledger"]);
                    Add(["progressionLedger"]);
                    break;
                default:
                    Add(["materialization"]);
                    break;
            }
        }

        return paths.Values.ToArray();

        void Add(string[] path) => paths[string.Join('\u001f', path)] = path;
    }

    private static void RemovePath(JsonObject actor, IReadOnlyList<string> path)
    {
        JsonObject current = actor;
        for (var index = 0; index < path.Count - 1; index++)
        {
            if (current[path[index]] is not JsonObject child)
                return;
            current = child;
        }

        current.Remove(path[^1]);
    }

    private static List<ActorNode> FindActors(
        JsonNode root,
        string path,
        string actorType,
        string actorId)
    {
        var result = new List<ActorNode>();
        if (root is not JsonObject rootObject)
            return result;

        if (path.Equals("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
        {
            AddMatchingActors(rootObject, "UpdateNPCs", actorId, mortal: true, actorType, result);
            AddMatchingActors(rootObject, "NPCsInScene", actorId, mortal: true, actorType, result);
            return result;
        }

        if (path.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase))
            AddMatchingActors(rootObject, AfterlifeEntityProfileState.ProfilesProperty, actorId, mortal: false, actorType, result);

        return result;
    }

    private static void AddMatchingActors(
        JsonObject root,
        string collectionName,
        string actorId,
        bool mortal,
        string actorType,
        ICollection<ActorNode> result)
    {
        if (root[collectionName] is not JsonArray collection)
            return;

        foreach (var node in collection)
        {
            if (node is not JsonObject actor || !MatchesActor(actor, actorId, mortal, actorType))
                continue;
            result.Add(new ActorNode(actor, collection));
        }
    }

    private static bool MatchesActor(JsonObject actor, string actorId, bool mortal, string actorType)
    {
        if (mortal)
        {
            return string.Equals(ReadString(actor, "NPCId"), actorId, StringComparison.Ordinal) ||
                   string.Equals(ReadString(actor, "npcId"), actorId, StringComparison.Ordinal) ||
                   string.Equals(ReadString(actor, "initialId"), actorId, StringComparison.Ordinal) ||
                   string.Equals(ReadString(actor, "id"), actorId, StringComparison.Ordinal);
        }

        return string.Equals(ReadString(actor, "actorType"), actorType, StringComparison.Ordinal) &&
               string.Equals(ReadString(actor, "actorId"), actorId, StringComparison.Ordinal);
    }

    private static string? ReadString(JsonObject root, string propertyName) =>
        root[propertyName] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool IssueTargetsPath(WorkerValidationIssue issue, string path)
    {
        var actor = issue.Actor ?? string.Empty;
        if (actor.StartsWith("mortal_npc:", StringComparison.Ordinal))
            return path.Equals("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase);
        if (actor.Contains(':', StringComparison.Ordinal))
            return path.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase);
        return issue.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseActorIdentity(
        string? identity,
        out string actorType,
        out string actorId)
    {
        actorType = string.Empty;
        actorId = string.Empty;
        if (string.IsNullOrWhiteSpace(identity))
            return false;

        var separator = identity.IndexOf(':');
        if (separator <= 0 || separator == identity.Length - 1)
            return false;
        actorType = identity[..separator];
        actorId = identity[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(actorType) && !string.IsNullOrWhiteSpace(actorId);
    }

    private sealed record ActorNode(JsonObject Actor, JsonArray Parent);
}
