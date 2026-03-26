using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeQuestHistoryAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/quests/quest_history.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var questHistory = new List<JsonObject>();
        foreach (var quest in CollectQuestHistoryEntries(previous).OfType<JsonObject>())
            UpsertByIdentity(questHistory, quest, "questId", "questName", "title", "name");
        foreach (var quest in CollectQuestHistoryEntries(currentNode).OfType<JsonObject>())
            UpsertByIdentity(questHistory, quest, "questId", "questName", "title", "name");

        var questRewards = new List<JsonObject>();
        CollectNamedObjectEntries(previous, "questRewards", questRewards);
        CollectNamedObjectEntries(currentNode, "questRewards", questRewards);

        var questChains = new List<JsonObject>();
        CollectNamedObjectEntries(previous, "questChains", questChains);
        CollectNamedObjectEntries(currentNode, "questChains", questChains);

        result["questHistory"] = ToArray(questHistory);
        if (questRewards.Count > 0)
            result["questRewards"] = ToArray(questRewards);
        else
            result.Remove("questRewards");
        if (questChains.Count > 0)
            result["questChains"] = ToArray(questChains);
        else
            result.Remove("questChains");
        result.Remove("questLog");
        result.Remove("quests");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeQuestStateAsync(string path, string updateProp, IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var quests = EnsureArray(result, "quests");

        foreach (var quest in CollectQuestStateEntries(previous, updateProp))
            UpsertQuestByIdentity(quests, quest);
        foreach (var quest in CollectQuestStateEntries(currentNode, updateProp))
            UpsertQuestByIdentity(quests, quest);

        result.Remove(updateProp);
        await WriteIfChangedAsync(path, currentNode, result);
    }


    private async Task NormalizeRivalSoulArcsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = RivalSoulArcService.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var arcs = EnsureArray(result, "arcs");
        var trackerRoot = await ReadObjectAsync(GuardianProjectState.TrackerPath);
        var currentTurn = await TryReadCurrentTurnNumberAsync();
        var soulStateRoot = await ReadObjectAsync("game_state/meta/soul_state.json");
        var currentIncarnation = GetNodeInt(soulStateRoot?["currentIncarnation"], 0);
        var projectJournalEntries = new List<JsonObject>();
        const string worldEventsPath = "game_state/world/world_events.json";
        var previousWorldEvents = await ReadBackupNodeAsync(worldEventsPath, backups);
        var currentWorldEvents = await ReadNodeAsync(worldEventsPath);

        foreach (var arc in CollectRivalSoulArcEntries(previous))
            UpsertByIdentity(arcs, arc, "arcId");
        foreach (var arc in CollectRivalSoulArcEntries(currentNode))
            UpsertByIdentity(arcs, arc, "arcId");

        result.Remove("UpdateRivalSoulArcs");
        await WriteIfChangedAsync(path, currentNode, result);

        var worldEventsChanged = false;
        if (trackerRoot != null &&
            currentIncarnation > 0 &&
            ConsumeLoreResearchVisibleRivalClues(previous, result, previousWorldEvents, currentWorldEvents, trackerRoot, currentIncarnation, currentTurn, projectJournalEntries, out worldEventsChanged))
        {
            await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerRoot.ToJsonString(JsonOpts));
            await _fs.WriteFileAtomicAsync(path, result.ToJsonString(JsonOpts));
            if (worldEventsChanged && currentWorldEvents != null)
                await _fs.WriteFileAtomicAsync(worldEventsPath, currentWorldEvents.ToJsonString(JsonOpts));
        }

        if (projectJournalEntries.Count > 0)
            await AppendGuardianProjectJournalEntriesAsync(projectJournalEntries);
    }

    private static bool ConsumeLoreResearchVisibleRivalClues(
        JsonObject? previousArcsRoot,
        JsonObject currentArcsRoot,
        JsonNode? previousWorldEventsRoot,
        JsonNode? currentWorldEventsRoot,
        JsonObject trackerRoot,
        int currentIncarnation,
        int currentTurn,
        List<JsonObject> journalEntries,
        out bool worldEventsChanged)
    {
        worldEventsChanged = false;
        var previousRevealKeys = CollectVisibleBonusClueRevealKeys(previousArcsRoot, previousWorldEventsRoot);
        if (currentArcsRoot["arcs"] is not JsonArray arcs)
            return false;

        var changed = false;
        var consumedRevealKeys = new HashSet<string>(previousRevealKeys, StringComparer.OrdinalIgnoreCase);
        var sponsorGuardianByArcId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var arc in arcs.OfType<JsonObject>())
        {
            var arcId = GetNodeString(arc["arcId"]);
            var sponsorGuardianId = GetNodeString((arc["sponsorGuardianRef"] as JsonObject)?["guardianId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(sponsorGuardianId) ||
                arc["publicSignals"] is not JsonArray publicSignals)
            {
                continue;
            }

            sponsorGuardianByArcId[arcId!] = sponsorGuardianId!;

            foreach (var signal in publicSignals.OfType<JsonObject>())
            {
                if (!GetJsonBool(signal["visibleToPlayer"]))
                    continue;

                var sourceProjectId = GetNodeString(signal["bonusClueSourceProjectId"]);
                if (string.IsNullOrWhiteSpace(sourceProjectId))
                    continue;

                if (GetJsonBool(signal["bonusClueConsumed"]))
                {
                    consumedRevealKeys.Add(BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false));
                    continue;
                }

                var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
                {
                    changed |= MarkBonusClueConsumed(signal, currentTurn, sourceProjectId!);
                    continue;
                }

                var clueCost = Math.Max(1, GetNodeInt(signal["bonusClueCost"], 1));
                if (!GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, sponsorGuardianId!, sourceProjectId!, currentIncarnation, clueCost))
                    continue;

                changed |= MarkBonusClueConsumed(signal, currentTurn, sourceProjectId!);
                if (!string.IsNullOrWhiteSpace(revealKey))
                    consumedRevealKeys.Add(revealKey);
                changed = true;
                journalEntries.Add(new JsonObject
                {
                    ["entryId"] = $"gpj_{sponsorGuardianId}_{sourceProjectId}_{arcId}_clue_{Guid.NewGuid():N}",
                    ["turn"] = currentTurn,
                    ["guardianId"] = sponsorGuardianId,
                    ["projectId"] = sourceProjectId,
                    ["eventType"] = "assisted",
                    ["visibility"] = "player_known",
                    ["title"] = "Израсходован bonus clue для rival-нити",
                    ["summary"] = $"Проект lore_research раскрыл новый видимый сигнал чужой нити судьбы и потратил {clueCost} clue budget.",
                    ["details"] = new JsonArray
                    {
                        $"ArcId: {arcId}",
                        $"SignalId: {GetNodeString(signal["signalId"])}",
                        $"Clue cost: {clueCost}"
                    }
                });
            }
        }

        foreach (var worldEvent in EnumerateWorldEventObjects(currentWorldEventsRoot))
        {
            var relatedArcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            var sourceProjectId = GetNodeString(worldEvent["bonusClueSourceProjectId"]);
            if (string.IsNullOrWhiteSpace(relatedArcId) ||
                string.IsNullOrWhiteSpace(sourceProjectId) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent) ||
                !sponsorGuardianByArcId.TryGetValue(relatedArcId!, out var sponsorGuardianId))
            {
                continue;
            }

            if (GetJsonBool(worldEvent["bonusClueConsumed"]))
            {
                consumedRevealKeys.Add(BuildVisibleBonusClueRevealKey(relatedArcId!, worldEvent, isWorldEvent: true));
                continue;
            }

            var revealKey = BuildVisibleBonusClueRevealKey(relatedArcId!, worldEvent, isWorldEvent: true);
            if (!string.IsNullOrWhiteSpace(revealKey) && consumedRevealKeys.Contains(revealKey))
            {
                worldEventsChanged |= MarkBonusClueConsumed(worldEvent, currentTurn, sourceProjectId!);
                continue;
            }

            var clueCost = Math.Max(1, GetNodeInt(worldEvent["bonusClueCost"], 1));
            if (!GuardianProjectState.TryConsumeVisibleRivalClue(trackerRoot, sponsorGuardianId, sourceProjectId!, currentIncarnation, clueCost))
                continue;

            worldEventsChanged |= MarkBonusClueConsumed(worldEvent, currentTurn, sourceProjectId!);
            if (!string.IsNullOrWhiteSpace(revealKey))
                consumedRevealKeys.Add(revealKey);
            changed = true;
            journalEntries.Add(new JsonObject
            {
                ["entryId"] = $"gpj_{sponsorGuardianId}_{sourceProjectId}_{relatedArcId}_world_event_clue_{Guid.NewGuid():N}",
                ["turn"] = currentTurn,
                ["guardianId"] = sponsorGuardianId,
                ["projectId"] = sourceProjectId,
                ["eventType"] = "assisted",
                ["visibility"] = "player_known",
                ["title"] = "Израсходован bonus clue для rival-нити через новость мира",
                ["summary"] = $"Проект lore_research раскрыл linked world event чужой нити судьбы и потратил {clueCost} clue budget.",
                ["details"] = new JsonArray
                {
                    $"ArcId: {relatedArcId}",
                    $"WorldEventId: {GetNodeString(worldEvent["eventId"])}",
                    $"Clue cost: {clueCost}"
                }
            });
        }

        return changed;
    }

    private static bool IsPlayerVisibleRivalWorldEvent(JsonObject worldEvent)
    {
        var visibility = GetNodeString(worldEvent["visibility"]);
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> CollectVisibleBonusClueRevealKeys(JsonObject? arcsRoot, JsonNode? worldEventsRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (arcsRoot?["arcs"] is JsonArray arcs)
        {
            foreach (var arc in arcs.OfType<JsonObject>())
            {
                var arcId = GetNodeString(arc["arcId"]);
                if (string.IsNullOrWhiteSpace(arcId) || arc["publicSignals"] is not JsonArray publicSignals)
                    continue;

                foreach (var signal in publicSignals.OfType<JsonObject>())
                {
                    if (!GetJsonBool(signal["visibleToPlayer"]) ||
                        string.IsNullOrWhiteSpace(GetNodeString(signal["bonusClueSourceProjectId"])))
                    {
                        continue;
                    }

                    result.Add(BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false));
                }
            }
        }

        foreach (var worldEvent in EnumerateWorldEventObjects(worldEventsRoot))
        {
            var arcId = GetNodeString(worldEvent["relatedRivalArcId"]);
            if (string.IsNullOrWhiteSpace(arcId) ||
                string.IsNullOrWhiteSpace(GetNodeString(worldEvent["bonusClueSourceProjectId"])) ||
                !IsPlayerVisibleRivalWorldEvent(worldEvent))
            {
                continue;
            }

            result.Add(BuildVisibleBonusClueRevealKey(arcId!, worldEvent, isWorldEvent: true));
        }

        return result;
    }

    private static string BuildVisibleBonusClueRevealKey(string arcId, JsonObject source, bool isWorldEvent)
    {
        var revealId = GetNodeString(source["bonusClueRevealId"]);
        if (!string.IsNullOrWhiteSpace(revealId))
            return $"{arcId}::reveal::{revealId}";

        if (!isWorldEvent)
        {
            var signalId = GetNodeString(source["signalId"]);
            if (!string.IsNullOrWhiteSpace(signalId))
                return $"{arcId}::signal::{signalId}";

            return $"{arcId}::signal::{GetNodeInt(source["stage"])}::{GetNodeString(source["source"])}::{GetNodeString(source["description"])}";
        }

        var worldEventId = GetNodeString(source["eventId"]);
        if (!string.IsNullOrWhiteSpace(worldEventId))
            return $"{arcId}::world_event::{worldEventId}";

        return $"{arcId}::world_event::{GetNodeString(source["eventTitle"])}::{GetNodeString(source["title"])}::{GetNodeString(source["summary"])}::{GetNodeString(source["description"])}";
    }

    private static bool MarkBonusClueConsumed(JsonObject node, int currentTurn, string sourceProjectId)
    {
        var changed = false;
        if (!GetJsonBool(node["bonusClueConsumed"]))
        {
            node["bonusClueConsumed"] = true;
            changed = true;
        }

        if (GetNodeInt(node["bonusClueConsumedAtTurn"]) != currentTurn)
        {
            node["bonusClueConsumedAtTurn"] = currentTurn;
            changed = true;
        }

        if (!string.Equals(GetNodeString(node["bonusClueConsumedProjectId"]), sourceProjectId, StringComparison.OrdinalIgnoreCase))
        {
            node["bonusClueConsumedProjectId"] = sourceProjectId;
            changed = true;
        }

        return changed;
    }

    private static IEnumerable<JsonObject> EnumerateWorldEventObjects(JsonNode? root)
    {
        if (root is JsonObject obj)
        {
            if (obj["worldEventsLog"] is JsonArray worldEventsLog)
            {
                foreach (var worldEvent in worldEventsLog.OfType<JsonObject>())
                    yield return worldEvent;
                yield break;
            }

            if (obj["events"] is JsonArray events)
            {
                foreach (var worldEvent in events.OfType<JsonObject>())
                    yield return worldEvent;
                yield break;
            }
        }
        else if (root is JsonArray arr)
        {
            foreach (var worldEvent in arr.OfType<JsonObject>())
                yield return worldEvent;
        }
    }

}

