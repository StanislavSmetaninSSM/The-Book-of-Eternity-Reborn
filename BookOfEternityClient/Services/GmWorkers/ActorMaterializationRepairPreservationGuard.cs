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
        "npc_initial_id_collides_with_existing_permanent_id",
        "npc_existing_inventory_resend_forbidden",
        "npc_characteristics_empty",
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
        var unscopedIssues = issues
            .Where(issue => IsActorMaterializationIssue(issue.Code) &&
                            !RepairCodes.Contains(issue.Code) &&
                            IssueTargetsPath(issue, path))
            .ToArray();
        if (unscopedIssues.Length > 0)
        {
            return
            [
                $"Actor materialization repair cannot safely scope authority issue(s) for {path}: " +
                string.Join(", ", unscopedIssues.Select(issue => issue.Code).Distinct(StringComparer.OrdinalIgnoreCase)) +
                ". Use the main GM rollback/repair path instead."
            ];
        }

        var relevantIssues = issues
            .Where(issue => RepairCodes.Contains(issue.Code) && IssueTargetsPath(issue, path))
            .ToArray();
        if (relevantIssues.Length == 0)
            return [];

        if (relevantIssues.Any(issue => string.Equals(
                issue.Code,
                "npc_initial_id_collides_with_existing_permanent_id",
                StringComparison.OrdinalIgnoreCase)))
        {
            return ["NPC identity collisions require the main GM rollback/repair path."];
        }

        if (string.IsNullOrWhiteSpace(baselineJson) &&
            IsSafeMissingGuardianThoughtJournalCreation(path, relevantIssues))
        {
            baselineJson = """{ "entries": [] }""";
        }

        if (string.IsNullOrWhiteSpace(baselineJson) || string.IsNullOrWhiteSpace(proposedJson))
        {
            return
            [
                $"Actor materialization repair must replace an existing JSON file while preserving protected actor data: {path}."
            ];
        }

        try
        {
            var baselineRoot = JsonNode.Parse(baselineJson);
            var proposedRoot = JsonNode.Parse(proposedJson);
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

                var scopedIssues = actorGroup.ToList();
                if (!TryNormalizeMortalContinuityRepairs(
                        path,
                        actorType,
                        actorId,
                        baselineCopy,
                        proposedCopy,
                        scopedIssues,
                        out var mortalRepairError))
                {
                    errors.Add(mortalRepairError);
                    continue;
                }
                if (scopedIssues.Count == 0)
                    continue;

                var baselineActors = FindActors(baselineCopy, path, actorType, actorId);
                var proposedActors = FindActors(proposedCopy, path, actorType, actorId);
                var isDedicatedGuardianJournalRepair =
                    path.Equals(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(actorType, "guardian", StringComparison.Ordinal) &&
                    actorGroup.Any(issue => string.Equals(
                        issue.Code,
                        "afterlife_actor_materialization_memory_missing",
                        StringComparison.OrdinalIgnoreCase));
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

                if (!isDedicatedGuardianJournalRepair &&
                    (baselineActors.Count == 0 || baselineActors.Count != proposedActors.Count))
                {
                    errors.Add(
                        $"Actor materialization repair cannot prove protected actor data for {actorType}:{actorId}: baseline={baselineActors.Count}, proposal={proposedActors.Count}.");
                    continue;
                }

                if (!TryNormalizeAppendOnlyMemoryRepair(
                    path,
                    actorType,
                    actorId,
                    baselineCopy,
                    proposedCopy,
                    baselineActors,
                    proposedActors,
                    scopedIssues,
                    out var memoryRepairError))
                {
                    errors.Add(memoryRepairError);
                    continue;
                }
                if (!TryResolveMutablePaths(path, scopedIssues, out var mutablePaths, out var resolutionError))
                {
                    errors.Add(resolutionError);
                    continue;
                }

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
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return [$"Actor materialization preservation check requires valid JSON for {path}."];
        }
    }

    private static bool IsSafeMissingGuardianThoughtJournalCreation(
        string path,
        IReadOnlyList<WorkerValidationIssue> relevantIssues)
    {
        if (!path.Equals(GuardianThoughtJournalState.StatePath, StringComparison.Ordinal) ||
            relevantIssues.Count == 0)
        {
            return false;
        }

        string? routedActorId = null;
        foreach (var issue in relevantIssues)
        {
            if (!string.Equals(
                    issue.Code,
                    "afterlife_actor_materialization_memory_missing",
                    StringComparison.OrdinalIgnoreCase) ||
                !TryParseActorIdentity(issue.Actor, out var actorType, out var actorId) ||
                !string.Equals(actorType, "guardian", StringComparison.Ordinal) ||
                !string.Equals(issue.Actor, $"guardian:{actorId}", StringComparison.Ordinal) ||
                routedActorId != null &&
                !string.Equals(routedActorId, actorId, StringComparison.Ordinal))
            {
                return false;
            }

            routedActorId = actorId;
        }

        return routedActorId != null;
    }

    private static bool IsActorMaterializationIssue(string code) =>
        code.StartsWith("actor_materialization_", StringComparison.OrdinalIgnoreCase) ||
        code.StartsWith("afterlife_actor_materialization_", StringComparison.OrdinalIgnoreCase) ||
        code.StartsWith("afterlife_actor_binding_", StringComparison.OrdinalIgnoreCase) ||
        IsMortalContinuityIssue(code);

    private static bool IsMortalContinuityIssue(string code) =>
        string.Equals(code, "npc_initial_id_collides_with_existing_permanent_id", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "npc_characteristics_empty", StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizeMortalContinuityRepairs(
        string path,
        string actorType,
        string actorId,
        JsonNode baselineRoot,
        JsonNode proposedRoot,
        ICollection<WorkerValidationIssue> issues,
        out string error)
    {
        error = string.Empty;
        var mortalIssues = issues.Where(issue =>
                string.Equals(issue.Code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_characteristics_empty", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (mortalIssues.Length == 0)
            return true;

        if (!path.Equals("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actorType, "mortal_npc", StringComparison.Ordinal))
        {
            error = $"Mortal continuity repair scope is not safely derivable for {actorType}:{actorId} in {path}.";
            return false;
        }

        foreach (var issue in mortalIssues)
        {
            if (!TryReadMortalIssueCarrier(issue.Path, out var carrier))
            {
                error = $"Mortal continuity repair requires an exact UpdateNPCs or NPCsInScene carrier for {actorType}:{actorId}.";
                return false;
            }

            var baselineActors = FindMortalActorsInCarrier(baselineRoot, carrier, actorId);
            var proposedActors = FindMortalActorsInCarrier(proposedRoot, carrier, actorId);
            if (baselineActors.Count != 1 || proposedActors.Count != 1)
            {
                error = $"Mortal continuity repair cannot prove one exact {carrier} actor for {actorType}:{actorId}.";
                return false;
            }

            var baselineActor = baselineActors[0].Actor;
            var proposedActor = proposedActors[0].Actor;
            if (string.Equals(issue.Code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(issue.Section, "NPCInventory", StringComparison.Ordinal) ||
                    !issue.Path.EndsWith(".inventory", StringComparison.OrdinalIgnoreCase) ||
                    !TryParseExpectedInventory(issue.Expected, out var expectedInventory) ||
                    baselineActor["inventory"] is not JsonArray ||
                    proposedActor["inventory"] is not JsonArray proposedInventory ||
                    !JsonNode.DeepEquals(proposedInventory, expectedInventory))
                {
                    error = $"Mortal inventory repair for {actorType}:{actorId} must restore the exact validated pre-turn snapshot.";
                    return false;
                }

                baselineActor.Remove("inventory");
                proposedActor.Remove("inventory");
            }
            else
            {
                if (!string.Equals(issue.Section, "NPCCharacteristics", StringComparison.Ordinal) ||
                    !issue.Path.EndsWith(".characteristics", StringComparison.OrdinalIgnoreCase) ||
                    baselineActor["characteristics"] is not JsonObject baselineCharacteristics ||
                    baselineCharacteristics.Count != 0 ||
                    proposedActor["characteristics"] is not JsonObject proposedCharacteristics ||
                    proposedCharacteristics.Count == 0 ||
                    proposedCharacteristics.Any(property => !IsNumericJsonValue(property.Value)))
                {
                    error = $"Mortal characteristics repair for {actorType}:{actorId} must add only setting-defined numeric characteristics to the empty target object.";
                    return false;
                }

                baselineActor.Remove("characteristics");
                proposedActor.Remove("characteristics");
            }

            issues.Remove(issue);
        }

        return true;
    }

    private static bool TryReadMortalIssueCarrier(string issuePath, out string carrier)
    {
        foreach (var candidate in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (issuePath.Contains($".{candidate}[", StringComparison.OrdinalIgnoreCase) ||
                issuePath.StartsWith($"{candidate}[", StringComparison.OrdinalIgnoreCase))
            {
                carrier = candidate;
                return true;
            }
        }

        carrier = string.Empty;
        return false;
    }

    private static List<ActorNode> FindMortalActorsInCarrier(
        JsonNode root,
        string carrier,
        string actorId)
    {
        var result = new List<ActorNode>();
        if (root is JsonObject rootObject)
            AddMatchingActors(rootObject, carrier, actorId, mortal: true, "mortal_npc", result);
        return result;
    }

    private static bool TryParseExpectedInventory(string? expected, out JsonArray inventory)
    {
        inventory = null!;
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        try
        {
            if (JsonNode.Parse(expected) is not JsonArray parsed)
                return false;
            inventory = parsed;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsNumericJsonValue(JsonNode? node)
    {
        if (node == null)
            return false;

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.ValueKind == JsonValueKind.Number;
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

    private static bool TryResolveMutablePaths(
        string targetPath,
        IEnumerable<WorkerValidationIssue> issues,
        out IReadOnlyList<string[]> mutablePaths,
        out string error)
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
                    if (!TryExtractIssueSubpath(issue.Path, ".equippedItems", out var equipmentPath))
                        return FailMutablePathResolution(issue, out mutablePaths, out error);
                    Add(equipmentPath);
                    break;
                case "afterlife_actor_materialization_memory_missing":
                    if (targetPath.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
                        Add(["musings"]);
                    else if (targetPath.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase))
                        Add(["gmThoughtsSummary"]);
                    else
                        return FailMutablePathResolution(issue, out mutablePaths, out error);
                    break;
                case "actor_materialization_missing":
                    Add(["materialization"]);
                    break;
                case "actor_materialization_invalid_envelope":
                case "actor_materialization_duplicate_id":
                    if (!TryExtractIssueSubpath(issue.Path, ".materialization", out var envelopePath))
                        return FailMutablePathResolution(issue, out mutablePaths, out error);
                    Add(envelopePath);
                    break;
                case "actor_materialization_actor_binding_mismatch":
                    if (!EndsAtMarker(issue.Path, ".materialization"))
                        return FailMutablePathResolution(issue, out mutablePaths, out error);
                    Add(["materialization", "actorType"]);
                    Add(["materialization", "actorId"]);
                    break;
                default:
                    return FailMutablePathResolution(issue, out mutablePaths, out error);
            }
        }

        mutablePaths = paths.Values.ToArray();
        error = string.Empty;
        return true;

        void Add(string[] path) => paths[string.Join('\u001f', path)] = path;
    }

    private static bool FailMutablePathResolution(
        WorkerValidationIssue issue,
        out IReadOnlyList<string[]> mutablePaths,
        out string error)
    {
        mutablePaths = [];
        error = $"Actor materialization repair scope is not safely derivable for {issue.Code} at {issue.Path}.";
        return false;
    }

    private static bool TryExtractIssueSubpath(string issuePath, string marker, out string[] path)
    {
        path = [];
        var markerIndex = issuePath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return false;

        var suffix = issuePath[(markerIndex + marker.Length)..].TrimStart('.');
        path = marker.TrimStart('.').Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Concat(suffix.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        return path.Length > 0;
    }

    private static bool EndsAtMarker(string issuePath, string marker) =>
        issuePath.EndsWith(marker, StringComparison.OrdinalIgnoreCase);

    private static bool TryNormalizeAppendOnlyMemoryRepair(
        string path,
        string actorType,
        string actorId,
        JsonNode baselineRoot,
        JsonNode proposedRoot,
        IReadOnlyList<ActorNode> baselineActors,
        IReadOnlyList<ActorNode> proposedActors,
        ICollection<WorkerValidationIssue> issues,
        out string error)
    {
        error = string.Empty;
        var memoryIssues = issues.Where(issue => string.Equals(
                issue.Code,
                "afterlife_actor_materialization_memory_missing",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (memoryIssues.Length == 0)
            return true;

        if (path.Equals(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actorType, "resident", StringComparison.Ordinal))
        {
            if (!TryRemoveOneAppendedJournalEntry(
                    baselineRoot,
                    proposedRoot,
                    GuardianAbodeResidentState.ThoughtJournalProperty,
                    "residentId",
                    actorId,
                    allowOtherAppendedEntries: true,
                    out error))
            {
                return false;
            }
        }
        else if (path.Equals(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(actorType, "guardian", StringComparison.Ordinal))
        {
            if (!TryRemoveOneAppendedJournalEntry(
                    baselineRoot,
                    proposedRoot,
                    "entries",
                    GuardianThoughtJournalState.ActorIdProperty,
                    actorId,
                    allowOtherAppendedEntries: false,
                    out error))
            {
                return false;
            }
        }
        else if (path.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(actorType, "guardian", StringComparison.Ordinal))
        {
            if (baselineActors.Count != 1 || proposedActors.Count != 1 ||
                !TryRemoveOneAppendedJournalEntry(
                    baselineActors[0].Actor,
                    proposedActors[0].Actor,
                    "musings",
                    identityProperty: null,
                    actorId,
                    allowOtherAppendedEntries: false,
                    out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? $"Guardian memory repair for {actorType}:{actorId} must append exactly one musing without rewriting history."
                    : error;
                return false;
            }
        }
        else if (path.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else
        {
            error = $"Actor memory repair scope is not safely derivable for {actorType}:{actorId} in {path}.";
            return false;
        }

        foreach (var issue in memoryIssues)
            issues.Remove(issue);
        return true;
    }

    private static bool TryRemoveOneAppendedJournalEntry(
        JsonNode baselineOwner,
        JsonNode proposedOwner,
        string journalProperty,
        string? identityProperty,
        string actorId,
        bool allowOtherAppendedEntries,
        out string error)
    {
        error = string.Empty;
        if (baselineOwner is not JsonObject baselineObject || proposedOwner is not JsonObject proposedObject)
        {
            error = $"Actor memory repair for {actorId} requires object journal owners.";
            return false;
        }

        var baselineHadJournal = baselineObject.TryGetPropertyValue(journalProperty, out var baselineJournalNode);
        if (baselineHadJournal && baselineJournalNode is not JsonArray)
        {
            error = $"Actor memory repair for {actorId} cannot replace malformed {journalProperty} authority.";
            return false;
        }
        if (proposedObject[journalProperty] is not JsonArray proposedJournal)
        {
            error = $"Actor memory repair for {actorId} must append to {journalProperty}.";
            return false;
        }

        var baselineJournal = baselineJournalNode as JsonArray;
        var baselineCount = baselineJournal?.Count ?? 0;
        if (proposedJournal.Count <= baselineCount)
        {
            error = $"Actor memory repair for {actorId} must append a new {journalProperty} entry.";
            return false;
        }

        for (var index = 0; index < baselineCount; index++)
        {
            if (!JsonNode.DeepEquals(baselineJournal![index], proposedJournal[index]))
            {
                error = $"Actor memory repair for {actorId} must preserve existing {journalProperty} entries exactly and append only.";
                return false;
            }
        }

        var matchingAppendedIndexes = new List<int>();
        for (var index = baselineCount; index < proposedJournal.Count; index++)
        {
            if (proposedJournal[index] is not JsonObject entry ||
                identityProperty != null &&
                !string.Equals(ReadString(entry, identityProperty), actorId, StringComparison.Ordinal) ||
                !HasMeaningfulMemoryText(entry))
            {
                continue;
            }

            matchingAppendedIndexes.Add(index);
        }

        if (matchingAppendedIndexes.Count != 1 ||
            !allowOtherAppendedEntries && proposedJournal.Count != baselineCount + 1)
        {
            error = $"Actor memory repair for {actorId} must append exactly one meaningful {journalProperty} entry without rewriting history.";
            return false;
        }

        proposedJournal.RemoveAt(matchingAppendedIndexes[0]);
        if (!baselineHadJournal && proposedJournal.Count == 0)
            proposedObject.Remove(journalProperty);
        return true;
    }

    private static bool HasMeaningfulMemoryText(JsonObject entry) =>
        HasNonEmptyString(entry, "thought") || HasNonEmptyString(entry, "summary");

    private static bool HasNonEmptyString(JsonObject entry, string propertyName) =>
        entry[propertyName] is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text);

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
        else if (path.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(actorType, "guardian", StringComparison.Ordinal))
            AddMatchingSourceActors(rootObject, "guardians", "guardianId", actorId, result);
        else if (path.Equals(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(actorType, "resident", StringComparison.Ordinal))
            AddMatchingSourceActors(rootObject, GuardianAbodeResidentState.EntriesProperty, "residentId", actorId, result);

        return result;
    }

    private static void AddMatchingSourceActors(
        JsonObject root,
        string collectionName,
        string identityProperty,
        string actorId,
        ICollection<ActorNode> result)
    {
        if (root[collectionName] is not JsonArray collection)
            return;

        foreach (var node in collection)
        {
            if (node is JsonObject actor &&
                string.Equals(ReadString(actor, identityProperty), actorId, StringComparison.Ordinal))
            {
                result.Add(new ActorNode(actor, collection));
            }
        }
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
        if (IsMortalContinuityIssue(issue.Code))
        {
            return path.Equals("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase);
        }

        if (issue.Code is "afterlife_actor_materialization_profile_missing" or
            "afterlife_actor_materialization_profile_ambiguous")
            return path.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(
                issue.Code,
                "afterlife_actor_materialization_memory_missing",
                StringComparison.OrdinalIgnoreCase) &&
            TryParseActorIdentity(issue.Actor, out var actorType, out _))
        {
            if (string.Equals(actorType, "guardian", StringComparison.Ordinal))
            {
                return path.Equals(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase) ||
                       path.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(actorType, "resident", StringComparison.Ordinal))
                return path.Equals(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase);

            return path.Equals(AfterlifeEntityProfileState.StatePath, StringComparison.OrdinalIgnoreCase);
        }
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
