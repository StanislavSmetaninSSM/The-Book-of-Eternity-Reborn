using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure event/effect checks on caller-authenticated detached DATA; never publication authority.</summary>
internal static class MortalLivingWorldEvents
{
    private const string ReportPath = "game_state/control/mortal_living_world_report.json";
    private sealed record Reference(string Kind, string Id, string? Owner)
    {
        internal string Key => new JsonArray(Kind, Id, Owner).ToJsonString();
    }

    /// <summary>Validates declared events against original and settled canonical documents, without reads or mutation.</summary>
    internal static IReadOnlyList<ValidationIssue> Validate(JsonObject report,
        IReadOnlyDictionary<string, JsonObject> original, IReadOnlyDictionary<string, JsonObject> current)
    {
        var issues = new List<ValidationIssue>();
        void Fail(string location, string detail, string code = "mortal_living_world_event_invalid") =>
            issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error, detail, code: code, section: location,
                repairHint: $"Repair {location} using exact canonical identities and actual original/settled values; do not invent state changes."));
        try
        {
            if (report["events"] is not JsonArray events || !Integer(report["fromMinute"], out var start) ||
                !Integer(report["toMinute"], out var end) || start < 0 || end < start)
            { Fail("events", "Expected events array and a valid assessment interval."); return issues; }
            if (Text(report["resolution"]) == "noSignificantChange" && events.Count == 0) return issues;
            if (Text(report["resolution"]) != "changes" || events.Count == 0)
            { Fail("events", "Changes require event evidence; no-change assessments cannot carry events."); return issues; }
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var usedEffects = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                var path = $"events[{i}]";
                if (events[i] is not JsonObject ev || !Shape(ev, "eventId", "fromMinute", "toMinute", "actors", "targets", "cause", "effects", "consequences", "knowledge"))
                { Fail(path, "Expected a complete closed event object."); continue; }
                if (Text(ev["eventId"]) is not { } id || !eventIds.Add(id)) Fail(path, "Event identity must be exact and unique.");
                if (!Integer(ev["fromMinute"], out var from) || !Integer(ev["toMinute"], out var to) || from < start || to > end || to < from)
                    Fail(path, "Event interval must stay inside the assessment interval.");
                References(ev["actors"], true, path + ".actors");
                var targets = References(ev["targets"], false, path + ".targets");
                if (ev["cause"] is not JsonObject cause || !Shape(cause, "summary", "references") || Text(cause["summary"]) is null)
                    Fail(path + ".cause", "Expected a cause summary and exact references.");
                else References(cause["references"], true, path + ".cause.references");
                var effectIds = new HashSet<string>(StringComparer.Ordinal);
                if (ev["effects"] is not JsonArray effects || effects.Count == 0) Fail(path + ".effects", "An event needs actual semantic effects.");
                else for (var j = 0; j < effects.Count; j++)
                {
                    var ep = $"{path}.effects[{j}]";
                    if (effects[j] is not JsonObject effect || !Shape(effect, "effectId", "target", "operation", "field", "before", "after"))
                    { Fail(ep, "Expected a complete closed effect object."); continue; }
                    if (Text(effect["effectId"]) is not { } effectId || !effectIds.Add(effectId)) Fail(ep, "Effect identity must be exact and unique inside its event.");
                    var target = ParseReference(effect["target"], false);
                    if (target is null || !targets.Contains(target)) { Fail(ep, "Effect target must be declared in the event targets."); continue; }
                    var field = Text(effect["field"]);
                    if (Text(effect["operation"]) != "update" || field is null || !Fields(target.Kind).Contains(field, StringComparer.Ordinal))
                    { Fail(ep, "Only update of a supported semantic field is admitted."); continue; }
                    if (!usedEffects.Add(target.Key + ":" + field)) Fail(ep, "A canonical target/field cannot prove multiple effects.", "mortal_living_world_effect_reused");
                    var before = Resolve(original, target);
                    var after = Resolve(current, target);
                    if (before is null || after is null) { Fail(ep, "Update requires an unambiguous existing original and current canonical target."); continue; }
                    var beforePresent = before.TryGetPropertyValue(field, out var beforeValue);
                    var afterPresent = after.TryGetPropertyValue(field, out var afterValue);
                    if (!Matches(effect["before"], beforePresent, beforeValue) || !Matches(effect["after"], afterPresent, afterValue))
                        Fail(ep, "Declared before/after evidence does not match canonical state.", "mortal_living_world_effect_mismatch");
                    if (beforePresent == afterPresent && SemanticField(target.Kind, field, beforeValue) == SemanticField(target.Kind, field, afterValue))
                        Fail(ep, "Cosmetic edits, array order or unchanged values are not semantic effects.", "mortal_living_world_effect_not_meaningful");
                }
                var covered = new HashSet<string>(StringComparer.Ordinal);
                if (ev["consequences"] is not JsonArray consequences || consequences.Count == 0) Fail(path, "Consequences must identify their effect evidence.");
                else foreach (var node in consequences)
                {
                    if (node is not JsonObject consequence || !Shape(consequence, "summary", "effectIds") || Text(consequence["summary"]) is null ||
                        consequence["effectIds"] is not JsonArray refs || refs.Count == 0)
                    { Fail(path + ".consequences", "Expected a summary and nonempty effect references."); continue; }
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var reference in refs)
                        if (Text(reference) is not { } key || !effectIds.Contains(key) || !seen.Add(key)) Fail(path + ".consequences", "Unknown or duplicate effect reference.");
                        else covered.Add(key);
                }
                if (!covered.SetEquals(effectIds)) Fail(path + ".consequences", "Every effect must support an explicit consequence.");
                ValidateKnowledge(ev["knowledge"], Text(ev["eventId"]), path);
            }
            return issues;

            HashSet<Reference> References(JsonNode? node, bool systems, string path)
            {
                var result = new HashSet<Reference>();
                if (node is not JsonArray array || array.Count == 0) { Fail(path, "Expected nonempty exact references."); return result; }
                foreach (var item in array)
                {
                    var reference = ParseReference(item, systems);
                    if (reference is null || !result.Add(reference)) { Fail(path, "Invalid or duplicate typed reference."); continue; }
                    if (reference.Kind != "system" && Resolve(original, reference) is null && Resolve(current, reference) is null)
                        Fail(path, "Reference does not resolve to an unambiguous canonical identity.");
                }
                return result;
            }
            void ValidateKnowledge(JsonNode? node, string? eventId, string path)
            {
                if (node is not JsonObject knowledge || !Shape(knowledge, "player", "newsReferences") || knowledge["newsReferences"] is not JsonArray refs)
                { Fail(path + ".knowledge", "Expected explicit knowledge and news references."); return; }
                var player = Text(knowledge["player"]);
                if (player == "hidden" && refs.Count == 0)
                {
                    if (eventId is not null && current.TryGetValue("game_state/world/world_events.json", out var publicWorld) &&
                        publicWorld["worldEventsLog"] is JsonArray publicEvents &&
                        publicEvents.OfType<JsonObject>().Any(entry => Text(entry["eventId"]) == eventId))
                        Fail(path + ".knowledge", "A hidden event cannot also appear in world news under the same identity, regardless of its visibility label.");
                    return;
                }
                if (player != "known" || refs.Count == 0) { Fail(path + ".knowledge", "Hidden events cannot reference news; known events require news."); return; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                current.TryGetValue("game_state/world/world_events.json", out var news);
                foreach (var item in refs)
                {
                    var id = Text(item);
                    var matches = news?["worldEventsLog"] is JsonArray entries
                        ? entries.OfType<JsonObject>().Where(entry => Text(entry["eventId"]) == id).ToArray() : [];
                    if (id is null || !seen.Add(id) || matches.Length != 1)
                    { Fail(path + ".knowledge", "Public news reference must identify exactly one canonical event."); continue; }
                    var entry = matches[0];
                    if (!(Shape(entry, "eventId", "title", "description", "visibility") ||
                          Shape(entry, "eventId", "title", "description", "visibility", "locationId")) ||
                        Text(entry["title"]) is null || Text(entry["description"]) is null || Text(entry["visibility"]) != "public")
                    { Fail(path + ".knowledge", "Referenced news must be a closed public projection, without private causes, provenance or payloads."); continue; }
                    if (entry.ContainsKey("locationId"))
                    {
                        var locationId = Text(entry["locationId"]);
                        var location = locationId is null ? null : Resolve(current, new Reference("location", locationId, null));
                        if (location?["discovery"] is not JsonObject discovery ||
                            Text(discovery["tier"]) is not ("discovered" or "visited") || Text(discovery["audience"]) != "player_known")
                            Fail(path + ".knowledge", "News location must resolve uniquely to a discovered or visited player-known location.");
                    }
                }
            }
        }
        catch (ArgumentException) { Fail("events", "Duplicate JSON properties or invalid JSON DATA."); }
        catch (InvalidOperationException) { Fail("events", "Malformed JSON DATA."); }
        return issues;
    }

    /// <summary>Explicit absent paths are detached source DATA supplied by the owner, not authentication.</summary>
    internal static IReadOnlyList<ValidationIssue> Validate(JsonObject report,
        IReadOnlyDictionary<string, JsonObject> original, IReadOnlyDictionary<string, JsonObject> current,
        IReadOnlySet<string> originalAbsentPaths) =>
        throw new NotImplementedException("#1516 lifecycle evidence RED scaffold");

    private static bool Shape(JsonObject obj, params string[] keys) => obj.Count == keys.Length && keys.All(obj.ContainsKey);
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text == text.Trim() ? text : null;
    private static bool Integer(JsonNode? node, out long value)
    {
        value = 0;
        return node is JsonValue json && (json.TryGetValue<long>(out value) || json.TryGetValue<int>(out var small) && (value = small) == small);
    }
    private static Reference? ParseReference(JsonNode? node, bool systems)
    {
        if (node is not JsonObject obj || Text(obj["kind"]) is not { } kind || Text(obj["id"]) is not { } id) return null;
        if (kind is "factionProject" or "threat")
            return Shape(obj, "kind", "id", "ownerId") && Text(obj["ownerId"]) is { } owner ? new(kind, id, owner) : null;
        if (!Shape(obj, "kind", "id")) return null;
        if (kind == "system") return systems && id is "worldCycle" or "factionCycle" ? new(kind, id, null) : null;
        return kind is "npc" or "npcActivity" or "faction" or "location" or "locationLink" or "worldFlag" ? new(kind, id, null) : null;
    }
    private static string[] Fields(string kind) => kind switch
    {
        "npc" => ["currentLocationId", "level", "experience", "characteristics", "factionAffiliations"],
        "npcActivity" => ["currentActivity", "completedActivities"],
        "faction" => ["currentAgenda", "principles", "level", "experience", "powerProfile", "governance", "leadership", "relations"],
        "factionProject" => ["activeState", "finalState", "resourcesSpent", "timeSpentMinutes", "currentStep", "completionTurn"],
        "location" => ["factionControl", "actorBindings", "locationStorages", "activeThreats", "customStates"],
        "locationLink" => ["access"],
        "threat" => ["intensity", "longTermGoal", "currentActivity"],
        "worldFlag" => ["value"],
        _ => []
    };
    private static JsonObject? Resolve(IReadOnlyDictionary<string, JsonObject> documents, Reference reference)
    {
        var candidates = new List<JsonObject>();
        void Collect(string path, string collection, string key, string id)
        {
            if (documents.TryGetValue(path, out var root) && root[collection] is JsonArray array)
                candidates.AddRange(array.OfType<JsonObject>().Where(obj => Text(obj[key]) == id));
        }
        switch (reference.Kind)
        {
            case "npc": case "npcActivity":
                Collect("game_state/npcs/npc_core.json", "UpdateNPCs", "NPCId", reference.Id);
                if (candidates.Count > 1) return null;
                var originalCount = candidates.Count;
                Collect("game_state/npcs/npc_core.json", "NPCsInScene", "NPCId", reference.Id);
                if (candidates.Count - originalCount > 1) return null;
                // The same NPC may legitimately be projected in both canonical arrays, but must agree exactly.
                return candidates.Count > 0 && candidates.All(item => JsonNode.DeepEquals(item, candidates[0])) && candidates.Count <= 2 ? candidates[0] : null;
            case "faction": Collect("game_state/factions/faction_core.json", "factions", "factionId", reference.Id); break;
            case "location": Collect("game_state/world/world_map.json", "locations", "locationId", reference.Id); break;
            case "locationLink": Collect("game_state/world/world_map.json", "links", "linkId", reference.Id); break;
            case "worldFlag": Collect("game_state/world/world_flags.json", "worldStateFlags", "flagId", reference.Id); break;
            case "factionProject":
                Collect("game_state/factions/faction_projects.json", "activeProjects", "projectId", reference.Id);
                Collect("game_state/factions/faction_projects.json", "completedProjects", "projectId", reference.Id);
                candidates.RemoveAll(item => Text(item["factionId"]) != reference.Owner); break;
            case "threat":
                var owner = Resolve(documents, new Reference("location", reference.Owner!, null));
                if (owner?["activeThreats"] is JsonArray threats)
                    candidates.AddRange(threats.OfType<JsonObject>().Where(item => Text(item["threatId"]) == reference.Id));
                break;
        }
        return candidates.Count == 1 ? candidates[0] : null;
    }
    private static bool Matches(JsonNode? node, bool present, JsonNode? actual)
    {
        if (node is not JsonObject evidence || evidence["present"] is not JsonValue value || !value.TryGetValue<bool>(out var declared) || declared != present) return false;
        return declared ? Shape(evidence, "present", "value") && JsonNode.DeepEquals(evidence["value"], actual) : Shape(evidence, "present");
    }
    // Domain-specific projections distinguish activity outcome/progress from its narrative label.
    // Evidence still compares the entire original/settled value; this projection only decides meaningfulness.
    private static string SemanticField(string kind, string field, JsonNode? node)
    {
        if (kind is "npcActivity" or "threat" && field == "currentActivity")
            return Activity(node);
        if (kind == "npcActivity" && field == "completedActivities")
            return ProjectEach(node, "finalOutcome");
        if (kind == "npc" && field == "factionAffiliations")
            return ProjectEach(node, "factionId", "rank", "branch", "membershipStatus");
        if (kind == "location" && field == "factionControl")
            return ProjectEach(node, "factionId", "controlType", "controlLevel");
        if (kind == "location" && field == "activeThreats")
            return node is JsonArray threats
                ? "[" + string.Join(",", threats.Select(Threat).OrderBy(value => value, StringComparer.Ordinal)) + "]"
                : Threat(node);
        if (kind == "location" && field == "locationStorages")
            return ProjectEach(node, "storageId", "capacity", "volume", "owner", "authorizedUsers", "hasFullAccess", "contents");
        if (kind == "locationLink" && field == "access")
            return Project(node, "state", "requirements");
        return Semantic(node);
    }
    private static string Activity(JsonNode? node) => Project(node,
        "totalTimeCostMinutes", "timeSpentMinutes", "currentStepNumber", "totalStepsInActivity", "linkedQuestId", "linkedPlotOutlineNode");
    private static string Threat(JsonNode? node)
    {
        if (node is not JsonObject threat) return Semantic(node);
        // Same canonical activity contract as NPCs; do not count labels inside the aggregate route.
        return "[" + Project(threat, "threatId", "intensity", "longTermGoal", "threatArchetype") + "," +
            (threat.ContainsKey("currentActivity") ? Activity(threat["currentActivity"]) : "absent") + "]";
    }
    private static string ProjectEach(JsonNode? node, params string[] fields) => node is JsonArray array
        ? "[" + string.Join(",", array.Select(item => Project(item, fields)).OrderBy(value => value, StringComparer.Ordinal)) + "]"
        : Project(node, fields);
    private static string Project(JsonNode? node, params string[] fields)
    {
        if (node is not JsonObject obj) return Semantic(node);
        return "{" + string.Join(",", fields.Where(obj.ContainsKey).OrderBy(field => field, StringComparer.Ordinal)
            .Select(field => System.Text.Json.JsonSerializer.Serialize(field) + ":" + Semantic(obj[field]))) + "}";
    }
    // Sort array representations as multisets; reordering alone is not evidence. Preserve multiplicity.
    private static string Semantic(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject obj => "{" + string.Join(",", obj.Where(pair => pair.Key is not ("name" or "description" or "image_prompt" or "displayName"))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => System.Text.Json.JsonSerializer.Serialize(pair.Key) + ":" + Semantic(pair.Value))) + "}",
        JsonArray array => "[" + string.Join(",", array.Select(Semantic).OrderBy(value => value, StringComparer.Ordinal)) + "]",
        _ => node.ToJsonString()
    };
}
