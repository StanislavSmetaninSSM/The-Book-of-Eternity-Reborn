using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Reduces command-shaped meta/history payload files into canonical accumulated state files.
/// This keeps viewers and validation aligned on a single storage model.
/// </summary>
public class CanonicalStateNormalizer
{
    private static readonly string[] CanonicalAchievementCategories =
    {
        "combat",
        "exploration",
        "story",
        "social",
        "crafting",
        "meta",
        "death",
        "secret"
    };

    private static readonly string[] CanonicalAchievementRarities =
    {
        "common",
        "uncommon",
        "rare",
        "epic",
        "legendary"
    };

    private static readonly string[] CanonicalCodexCategories =
    {
        "cosmology",
        "geography",
        "history",
        "cultures",
        "creatures",
        "characters",
        "artifacts",
        "factions",
        "magic",
        "other"
    };

	    public static readonly string[] CanonicalAccumulatedFiles =
	    {
	        "game_state/meta/soul_state.json",
	        "game_state/meta/guardians.json",
	        "game_state/meta/character_chronicle.json",
	        "game_state/meta/achievements.json",
	        "lore/codex_entries.json",
	        "game_state/quests/regular_quests.json",
	        "game_state/quests/soul_quests.json",
	        "game_state/quests/quest_history.json",
	        "game_state/world/rival_soul_arcs.json",
	        "game_state/factions/faction_core.json",
	        "game_state/inventory/item_resources.json",
	        "game_state/inventory/item_bonds.json",
	        "game_state/inventory/item_text_updates.json",
	        "game_state/npcs/item_journals.json",
	        "game_state/factions/faction_structure.json",
	        "game_state/factions/faction_resources.json",
	        "game_state/factions/faction_projects.json",
        "game_state/factions/faction_custom.json",
        "game_state/factions/faction_chronicles.json"
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<CanonicalStateNormalizer> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public CanonicalStateNormalizer(FileSystemManager fs, ILogger<CanonicalStateNormalizer> logger)
    {
        _fs = fs;
        _logger = logger;
    }

	    public async Task NormalizeAccumulatedStateAsync(IReadOnlyDictionary<string, string>? backups = null)
	    {
	        await NormalizeSoulStateAsync(backups);
	        await NormalizeGuardiansAsync(backups);
	        await NormalizeCharacterChronicleAsync(backups);
	        await NormalizeAchievementsAsync(backups);
	        await NormalizeCodexAsync(backups);
	        await NormalizeQuestStateAsync("game_state/quests/regular_quests.json", "UpdateQuests", backups);
	        await NormalizeQuestStateAsync("game_state/quests/soul_quests.json", "UpdateSoulQuests", backups);
	        await NormalizeQuestHistoryAsync(backups);
	        await NormalizeRivalSoulArcsAsync(backups);
	        await NormalizeFactionCoreAsync(backups);
	        await NormalizeInventoryItemResourcesAsync(backups);
	        await NormalizeInventoryItemBondsAsync(backups);
	        await NormalizeInventoryItemTextsAsync(backups);
	        await NormalizeItemJournalsAsync(backups);
	        await NormalizeFactionStructureAsync(backups);
        await NormalizeFactionResourcesAsync(backups);
        await NormalizeFactionProjectsAsync(backups);
        await NormalizeFactionCustomAsync(backups);
        await NormalizeFactionChroniclesAsync(backups);
    }

    private async Task NormalizeSoulStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/soul_state.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        NormalizeInkFeathersShape(result);
        NormalizeSoulRelicsShape(result);

        if (current["metaStateUpdates"] is JsonObject updates)
            ApplyMetaStateUpdates(result, updates);

        result.Remove("metaStateUpdates");
        await WriteIfChangedAsync(path, current, result);
    }

    private async Task NormalizeGuardiansAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/guardians.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());

        if (currentNode is JsonObject currentObj)
        {
            MergeObject(result, currentObj);
            if (currentObj["guardians"] is JsonArray guardians)
                result["guardians"] = guardians.DeepClone();
            if (currentObj["activeGuardian"] is JsonObject activeGuardian)
                result["activeGuardian"] = activeGuardian.DeepClone();
            if (currentObj["chaosSeaNavigation"] is JsonObject nav)
                result["chaosSeaNavigation"] = nav.DeepClone();
            if (currentObj["pendingGuardianCreation"] is JsonObject pending)
                result["pendingGuardianCreation"] = pending.DeepClone();
            if (currentObj["UpdateGuardians"] is JsonArray updates)
                ApplyGuardianCommands(result, updates);
        }
        else if (currentNode is JsonArray currentArray)
        {
            result["guardians"] = currentArray.DeepClone();
        }

        result.Remove("UpdateGuardians");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeCharacterChronicleAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/character_chronicle.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = EnsureArray(result, "entries");

        foreach (var entry in CollectChronicleEntries(previous))
            AddUniqueNode(entries, entry);
        foreach (var entry in CollectChronicleEntries(currentNode))
            AddUniqueNode(entries, entry);

        result.Remove("characterChronicleUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeAchievementsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/meta/achievements.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var unlocked = new List<JsonObject>();
        var tracked = new List<JsonObject>();

        CollectAchievementObjects(previous, "unlockedAchievements", unlocked);
        CollectAchievementObjects(current, "unlockedAchievements", unlocked);
        CollectAchievementObjects(previous, "trackedProgress", tracked);
        CollectAchievementObjects(current, "trackedProgress", tracked);

        if (current["achievementUnlocks"] is JsonArray unlockCommands)
        {
            foreach (var unlock in unlockCommands.OfType<JsonObject>())
                UpsertByIdentity(unlocked, unlock, "achievementId", "name");
        }

        var unlockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var achievement in unlocked)
        {
            var key = GetNodeString(achievement["achievementId"]) ?? GetNodeString(achievement["name"]);
            if (!string.IsNullOrWhiteSpace(key))
                unlockedIds.Add(key);
        }

        tracked = tracked
            .Where(item =>
            {
                var key = GetNodeString(item["achievementId"]) ?? GetNodeString(item["name"]);
                return string.IsNullOrWhiteSpace(key) || !unlockedIds.Contains(key);
            })
            .ToList();

        result["unlockedAchievements"] = ToArray(unlocked);
        result["trackedProgress"] = ToArray(tracked);
        result["stats"] = BuildAchievementStats(unlocked);
        result.Remove("achievementUnlocks");

        await WriteIfChangedAsync(path, current, result);
    }

    private async Task NormalizeCodexAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "lore/codex_entries.json";
        var current = await ReadObjectAsync(path);
        if (current == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        MergeObject(result, current);

        var entries = new List<JsonObject>();
        CollectCodexEntries(previous, entries);
        CollectCodexEntries(current, entries);

        if (current["loreCodexUpdates"] is JsonArray updates)
            ApplyCodexUpdates(entries, updates);

        result["entries"] = ToArray(entries);
        result["totalEntries"] = entries.Count;
        result["categories"] = BuildCodexCategoryStats(entries);
        result.Remove("loreCodexUpdates");

        await WriteIfChangedAsync(path, current, result);
    }

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

    private async Task NormalizeFactionCoreAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_core.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var factions = new List<JsonObject>();

        foreach (var faction in CollectFactionCoreEntries(previous))
            UpsertByIdentity(factions, NormalizeFactionCoreEntry(faction), "factionId");
        foreach (var faction in CollectFactionCoreEntries(currentNode))
            UpsertByIdentity(factions, NormalizeFactionCoreEntry(faction), "factionId");

        result["factions"] = ToArray(factions);
        result.Remove("factionDataChanges");
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

        foreach (var arc in CollectRivalSoulArcEntries(previous))
            UpsertByIdentity(arcs, arc, "arcId");
        foreach (var arc in CollectRivalSoulArcEntries(currentNode))
            UpsertByIdentity(arcs, arc, "arcId");

        result.Remove("UpdateRivalSoulArcs");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemResourcesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_resources.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries"))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["inventoryItemsResources"] is JsonArray resourceChanges)
                ApplyInventoryResourceCommands(entries, resourceChanges);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("inventoryItemsResources");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemBondsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_bonds.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries"))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["itemBondLevelChanges"] is JsonArray bondChanges)
                ApplyInventoryBondCommands(entries, bondChanges);
            if (currentObj["itemFateCardUnlocks"] is JsonArray fateCardUnlocks)
                ApplyInventoryFateCardUnlockCommands(entries, fateCardUnlocks);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("itemBondLevelChanges");
        result.Remove("itemFateCardUnlocks");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeInventoryItemTextsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/inventory/item_text_updates.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventoryTextEntries(previous))
            UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventoryTextEntries(currentObj))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");

            if (currentObj["updateItemTextContents"] is JsonArray textUpdates)
                ApplyInventoryTextCommands(entries, textUpdates);
        }
        else
        {
            foreach (var entry in CollectInventoryTextEntries(currentNode))
                UpsertByIdentity(entries, entry, "existedId", "itemId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("updateItemTextContents");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeItemJournalsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/item_journals.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectInventorySidecarEntries(previous, "entries", "itemJournals"))
            UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectInventorySidecarEntries(currentObj, "entries", "itemJournals"))
                UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");

            if (currentObj["itemJournalUpdates"] is JsonArray journalUpdates)
                ApplyItemJournalCommands(entries, journalUpdates);
        }
        else
        {
            foreach (var entry in CollectInventorySidecarEntries(currentNode, "entries", "itemJournals"))
                UpsertByIdentity(entries, entry, "itemId", "existedId", "id", "itemName", "name");
        }

        result["entries"] = entries;
        result.Remove("itemJournals");
        result.Remove("itemJournalUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeFactionStructureAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_structure.json";
        var currentNode = await ReadNodeAsync(path);
        var factionCoreCurrent = await ReadNodeAsync("game_state/factions/faction_core.json");
        var factionCorePrevious = await ReadBackupObjectAsync("game_state/factions/faction_core.json", backups);

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectFactionEntryObjects(previous, "entries"))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionStructureEntriesFromCore(factionCorePrevious))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionStructureEntriesFromCore(factionCoreCurrent))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

            if (currentObj["factionRankChanges"] is JsonArray factionRankChanges)
                ApplyFactionRankChangeCommands(entries, factionRankChanges);
            if (currentObj["factionBonusChanges"] is JsonArray factionBonusChanges)
                ApplyFactionBonusChangeCommands(entries, factionBonusChanges);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        }

        if (currentNode == null && previous == null && entries.Count == 0)
            return;

        result["entries"] = entries;
        result.Remove("factionRankChanges");
        result.Remove("factionBonusChanges");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeFactionResourcesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_resources.json";
        var currentNode = await ReadNodeAsync(path);
        var factionCoreCurrent = await ReadNodeAsync("game_state/factions/faction_core.json");
        var factionCorePrevious = await ReadBackupObjectAsync("game_state/factions/faction_core.json", backups);

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectFactionEntryObjects(previous, "entries"))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionResourceEntriesFromCore(factionCorePrevious))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionResourceEntriesFromCore(factionCoreCurrent))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

            if (currentObj["factionResourceChanges"] is JsonArray factionResourceChanges)
                ApplyFactionResourceChangeCommands(entries, factionResourceChanges);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        }

        if (currentNode == null && previous == null && entries.Count == 0)
            return;

        result["entries"] = entries;
        result.Remove("factionResourceChanges");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeFactionProjectsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_projects.json";
        var currentNode = await ReadNodeAsync(path);
        var factionCoreCurrent = await ReadNodeAsync("game_state/factions/faction_core.json");
        var factionCorePrevious = await ReadBackupObjectAsync("game_state/factions/faction_core.json", backups);

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var activeProjects = new List<JsonObject>();
        var completedProjects = new List<JsonObject>();

        CollectFactionProjectObjects(previous, "activeProjects", activeProjects);
        CollectFactionProjectObjects(previous, "completedProjects", completedProjects);
        CollectFactionProjectsFromCore(factionCorePrevious, activeProjects, completedProjects);
        CollectFactionProjectsFromCore(factionCoreCurrent, activeProjects, completedProjects);

        if (currentNode is JsonObject currentObj)
        {
            CollectFactionProjectObjects(currentObj, "activeProjects", activeProjects);
            CollectFactionProjectObjects(currentObj, "completedProjects", completedProjects);

            if (currentObj["factionProjectUpdates"] is JsonArray factionProjectUpdates)
                ApplyFactionProjectUpdateCommands(activeProjects, factionProjectUpdates);
            if (currentObj["completeFactionProjects"] is JsonArray completeFactionProjects)
                ApplyFactionProjectCompletionCommands(activeProjects, completedProjects, completeFactionProjects);
        }
        else
        {
            CollectFactionProjectObjects(currentNode, "activeProjects", activeProjects);
            CollectFactionProjectObjects(currentNode, "completedProjects", completedProjects);
        }

        if (currentNode == null && previous == null && activeProjects.Count == 0 && completedProjects.Count == 0)
            return;

        result["activeProjects"] = ToArray(activeProjects);
        result["completedProjects"] = ToArray(completedProjects);
        result.Remove("factionProjectUpdates");
        result.Remove("completeFactionProjects");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeFactionCustomAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_custom.json";
        var currentNode = await ReadNodeAsync(path);
        var factionCoreCurrent = await ReadNodeAsync("game_state/factions/faction_core.json");
        var factionCorePrevious = await ReadBackupObjectAsync("game_state/factions/faction_core.json", backups);

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = new JsonArray();

        foreach (var entry in CollectFactionEntryObjects(previous, "entries"))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionCustomEntriesFromCore(factionCorePrevious))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        foreach (var entry in CollectFactionCustomEntriesFromCore(factionCoreCurrent))
            UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");

            if (currentObj["factionCustomStateChanges"] is JsonArray factionCustomStateChanges)
                ApplyFactionCustomStateCommands(entries, factionCustomStateChanges);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertByIdentity(entries, entry, "factionId", "factionName", "name");
        }

        if (currentNode == null && previous == null && entries.Count == 0)
            return;

        result["entries"] = entries;
        result.Remove("factionCustomStateChanges");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private async Task NormalizeFactionChroniclesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/factions/faction_chronicles.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null) return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? new JsonObject());
        var entries = EnsureArray(result, "entries");

        foreach (var entry in CollectFactionChronicleEntries(previous))
            AddUniqueNode(entries, entry);
        foreach (var entry in CollectFactionChronicleEntries(currentNode))
            AddUniqueNode(entries, entry);

        result.Remove("factionChronicleUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private void ApplyMetaStateUpdates(JsonObject root, JsonObject updates)
    {
        if (updates["inkFeatherChanges"] is JsonObject feathers)
        {
            NormalizeInkFeathersShape(root);
            var current = root["inkFeathers"]!.AsObject();
            var currentValue = GetNodeInt(current["current"]);
            var totalValue = GetNodeInt(current["total"], currentValue);
            currentValue += GetNodeInt(feathers["add"]);
            currentValue -= GetNodeInt(feathers["spend"]);
            totalValue += Math.Max(0, GetNodeInt(feathers["add"]));
            current["current"] = Math.Max(0, currentValue);
            current["total"] = Math.Max(totalValue, currentValue);
        }

        if (updates["enlightenmentProgression"] is JsonObject progression)
        {
            var newTier = GetNodeInt(progression["newTier"], -1);
            var experience = GetNodeInt(progression["experience"]);

            var enlightenment = root["enlightenment"] as JsonObject ?? new JsonObject();
            if (newTier >= 0)
            {
                enlightenment["level"] = newTier;
                if (string.IsNullOrWhiteSpace(GetNodeString(enlightenment["currentTier"])))
                    enlightenment["currentTier"] = $"Ур. {newTier}";
            }
            enlightenment["experience"] = experience;
            root["enlightenment"] = enlightenment;

            var soulProgression = root["soulProgression"] as JsonObject ?? new JsonObject();
            if (newTier >= 0)
            {
                soulProgression["tier"] = newTier;
                if (string.IsNullOrWhiteSpace(GetNodeString(soulProgression["tierName"])))
                    soulProgression["tierName"] = $"Ур. {newTier}";
            }
            soulProgression["totalExperience"] = experience;
            if (!soulProgression.ContainsKey("experienceInCurrentTier"))
                soulProgression["experienceInCurrentTier"] = experience;
            root["soulProgression"] = soulProgression;
        }

        if (updates["soulRelicOperations"] is JsonObject relicOps)
        {
            NormalizeSoulRelicsShape(root);
            var soulRelics = root["soulRelics"]!.AsObject();
            var equipped = EnsureArray(soulRelics, "equipped");
            var stored = EnsureArray(soulRelics, "stored");

            if (relicOps["addRelic"] is JsonObject relicToAdd)
                UpsertRelic(stored, relicToAdd);

            if (relicOps["removeRelic"] is JsonObject removeRelic)
            {
                var relicId = GetNodeString(removeRelic["relicId"]);
                RemoveRelic(equipped, relicId);
                RemoveRelic(stored, relicId);
            }

            if (relicOps["equipRelic"] is JsonObject equipRelic)
            {
                var relicId = GetNodeString(equipRelic["relicId"]);
                var slot = GetNodeString(equipRelic["slot"]) ?? "";
                var relic = TakeRelic(stored, relicId) ?? TakeRelic(equipped, relicId);
                if (relic != null)
                {
                    SetRelicEquipped(relic, true, slot);
                    UpsertRelic(equipped, relic);
                }
            }

            if (relicOps["unequipRelic"] is JsonObject unequipRelic)
            {
                var relicId = GetNodeString(unequipRelic["relicId"]);
                var relic = TakeRelic(equipped, relicId) ?? TakeRelic(stored, relicId);
                if (relic != null)
                {
                    SetRelicEquipped(relic, false, "");
                    UpsertRelic(stored, relic);
                }
            }

            if (relicOps["updateRelicField"] is JsonObject updateRelicField)
            {
                var relicId = GetNodeString(updateRelicField["relicId"]);
                var field = GetNodeString(updateRelicField["field"]);
                if (!string.IsNullOrWhiteSpace(relicId) && !string.IsNullOrWhiteSpace(field))
                {
                    var relic = FindRelic(equipped, relicId) ?? FindRelic(stored, relicId);
                    if (relic != null)
                        relic[field!] = updateRelicField["newValue"]?.DeepClone();
                }
            }
        }

        if (updates["lifeTransitions"] is JsonObject lifeTransitions &&
            lifeTransitions["recordLifeCompletion"] is JsonObject lifeRecord)
        {
            var livesHistory = EnsureArray(root, "livesHistory");
            AddUniqueNode(livesHistory, lifeRecord);
        }

        if (updates["memoryLegacyGrant"] is JsonObject memoryLegacyGrant)
        {
            var legacyType = GetNodeString(memoryLegacyGrant["legacyType"]);
            if (!string.IsNullOrWhiteSpace(legacyType))
            {
                var pendingLegacy = new JsonObject
                {
                    ["legacyId"] = GetNodeString(memoryLegacyGrant["legacyId"]) ?? $"memory_legacy_{Guid.NewGuid():N}",
                    ["sourceLifeHint"] = GetNodeString(memoryLegacyGrant["sourceLifeHint"]) ?? "",
                    ["legacyType"] = legacyType,
                    ["grantSource"] = "memoryLegacyGrant",
                    ["grantSnapshot"] = memoryLegacyGrant.DeepClone(),
                    ["applicationState"] = "pending",
                    ["grantedAtUtc"] = DateTime.UtcNow.ToString("o")
                };

                if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
                {
                    pendingLegacy["characteristic"] = GetNodeString(memoryLegacyGrant["characteristic"]) ?? "";
                    pendingLegacy["bonus"] = GetNodeInt(memoryLegacyGrant["bonus"]);
                }
                else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
                {
                    pendingLegacy["skillName"] = GetNodeString(memoryLegacyGrant["skillName"]) ?? "";
                    pendingLegacy["skillDescription"] = GetNodeString(memoryLegacyGrant["skillDescription"]) ?? "";
                    pendingLegacy["rarity"] = GetNodeString(memoryLegacyGrant["rarity"]) ?? "Uncommon";
                    pendingLegacy["type"] = GetNodeString(memoryLegacyGrant["type"]) ?? "MemoryLegacy";
                    pendingLegacy["group"] = GetNodeString(memoryLegacyGrant["group"]) ?? "Knowledge";
                    pendingLegacy["playerStatBonus"] = GetNodeString(memoryLegacyGrant["playerStatBonus"]) ?? "";
                    pendingLegacy["masteryLevel"] = GetNodeInt(memoryLegacyGrant["masteryLevel"], 1);
                    pendingLegacy["maxMasteryLevel"] = GetNodeInt(memoryLegacyGrant["maxMasteryLevel"], 1);
                    pendingLegacy["structuredBonuses"] = memoryLegacyGrant["structuredBonuses"]?.DeepClone() ?? new JsonArray();
                }

                root["pendingMemoryLegacy"] = pendingLegacy;
            }
        }
    }

    private void ApplyGuardianCommands(JsonObject root, JsonArray updates)
    {
        var guardians = EnsureArray(root, "guardians");

        foreach (var commandNode in updates.OfType<JsonObject>())
        {
            var command = GetNodeString(commandNode["command"]);
            if (string.IsNullOrWhiteSpace(command))
                continue;

            switch (command)
            {
                case "create":
                    if (commandNode["data"] is JsonObject data)
                    {
                        UpsertGuardian(guardians, data);
                        if (root["activeGuardian"] == null)
                            root["activeGuardian"] = data.DeepClone();
                    }
                    break;

                case "updateReputation":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        var delta = GetNodeInt(commandNode["reputationChange"]);
                        var relationshipData = guardian["relationshipData"] as JsonObject ?? new JsonObject();
                        var currentRep = GetNodeInt(relationshipData["currentReputation"], GetNodeInt(guardian["reputation"]));
                        var nextRep = currentRep + delta;
                        relationshipData["currentReputation"] = nextRep;
                        guardian["relationshipData"] = relationshipData;
                        guardian["reputation"] = nextRep;

                        var history = EnsureArray(relationshipData, "reputationHistory");
                        history.Add(new JsonObject
                        {
                            ["change"] = delta,
                            ["reason"] = GetNodeString(commandNode["reason"]) ?? "",
                            ["timestamp"] = DateTime.UtcNow.ToString("o")
                        });
                        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "completeQuest":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        var questManagement = guardian["questManagement"] as JsonObject ?? new JsonObject();
                        var completed = EnsureArray(questManagement, "completedQuests");
                        completed.Add(new JsonObject
                        {
                            ["questId"] = GetNodeString(commandNode["questId"]) ?? "",
                            ["result"] = GetNodeString(commandNode["outcome"]) ?? "",
                            ["completionDate"] = DateTime.UtcNow.ToString("o")
                        });

                        RemoveQuestFromArray(questManagement["activeQuests"] as JsonArray, GetNodeString(commandNode["questId"]));
                        RemoveQuestFromArray(questManagement["availableQuests"] as JsonArray, GetNodeString(commandNode["questId"]));
                        guardian["questManagement"] = questManagement;
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "processGacha":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        var (chargesPerReturn, normalizedUsedCharges) = GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
                        var gacha = guardian["gachaSystem"] as JsonObject ?? new JsonObject();
                        var history = EnsureArray(gacha, "gachaHistory");
                        var resultNode = commandNode["result"] as JsonObject;
                        history.Add(new JsonObject
                        {
                            ["relicId"] = resultNode != null ? GetNodeString(resultNode["relicId"]) ?? GetNodeString(resultNode["name"]) ?? "" : "",
                            ["costInFeathers"] = GetNodeInt(commandNode["inkFeathersSpent"]),
                            ["finalRarity"] = resultNode != null ? GetNodeString(resultNode["rarity"]) ?? "" : "",
                            ["timestamp"] = DateTime.UtcNow.ToString("o")
                        });
                        gacha["chargesPerReturn"] = chargesPerReturn;
                        gacha["chargesUsedThisReturn"] = normalizedUsedCharges + 1;
                        guardian["gachaSystem"] = gacha;
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "addMusings":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        var musings = EnsureArray(guardian, "musings");
                        if (commandNode["musings"] is JsonArray newMusings)
                        {
                            foreach (var musing in newMusings)
                            {
                                if (musing is JsonObject musingObject)
                                {
                                    var normalizedMusing = CloneObject(musingObject);
                                    if (string.IsNullOrWhiteSpace(GetNodeString(normalizedMusing["thought"])) &&
                                        !string.IsNullOrWhiteSpace(GetNodeString(normalizedMusing["text"])))
                                    {
                                        normalizedMusing["thought"] = GetNodeString(normalizedMusing["text"]);
                                    }
                                    AddUniqueNode(musings, normalizedMusing);
                                }
                                else if (musing != null)
                                {
                                    AddUniqueNode(musings, musing);
                                }
                            }
                        }
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "updateProject":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        if (commandNode["currentProject"] is JsonObject currentProject)
                        {
                            var normalizedProject = CloneObject(currentProject);
                            if (string.IsNullOrWhiteSpace(GetNodeString(normalizedProject["projectName"])) &&
                                !string.IsNullOrWhiteSpace(GetNodeString(normalizedProject["name"])))
                            {
                                normalizedProject["projectName"] = GetNodeString(normalizedProject["name"]);
                            }
                            guardian["currentProject"] = normalizedProject;
                        }
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "unlockLore":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        var loreFragments = EnsureArray(guardian, "loreFragments");
                        if (commandNode["loreFragment"] is JsonObject loreFragment)
                        {
                            var clone = CloneObject(loreFragment);
                            clone["isUnlocked"] = true;
                            UpsertByIdentity(loreFragments, clone, "fragmentId", "title");
                        }
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;

                case "setMood":
                    {
                        var guardianId = GetNodeString(commandNode["guardianId"]);
                        if (string.IsNullOrWhiteSpace(guardianId))
                            break;
                        var guardian = FindGuardian(guardians, guardianId!);
                        if (guardian == null)
                            break;
                        if (commandNode["mood"] is JsonObject mood)
                            guardian["mood"] = mood.DeepClone();
                        SyncActiveGuardian(root, guardianId!, guardian);
                    }
                    break;
            }
        }
    }

    private async Task<JsonNode?> ReadNodeAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON node: {Path}", relativePath);
            return null;
        }
    }

    private async Task<JsonObject?> ReadObjectAsync(string relativePath)
    {
        return await ReadNodeAsync(relativePath) as JsonObject;
    }

    private async Task<JsonObject?> ReadBackupObjectAsync(string originalRelativePath, IReadOnlyDictionary<string, string>? backups)
    {
        if (backups == null || !backups.TryGetValue(originalRelativePath, out var backupPath))
            return null;

        return await ReadNodeAsync(backupPath) as JsonObject;
    }

    private static JsonObject CloneObject(JsonObject obj)
    {
        return JsonNode.Parse(obj.ToJsonString())!.AsObject();
    }

    private static void MergeObject(JsonObject target, JsonObject source)
    {
        foreach (var prop in source)
            target[prop.Key] = prop.Value?.DeepClone();
    }

    private static JsonArray EnsureArray(JsonObject obj, string propName)
    {
        if (obj[propName] is JsonArray arr)
            return arr;

        var created = new JsonArray();
        obj[propName] = created;
        return created;
    }

    private static void NormalizeInkFeathersShape(JsonObject root)
    {
        if (root["inkFeathers"] is JsonValue currentValue)
        {
            var current = GetNodeInt(currentValue);
            root["inkFeathers"] = new JsonObject
            {
                ["current"] = current,
                ["total"] = Math.Max(current, 0)
            };
        }
        else if (root["inkFeathers"] is not JsonObject)
        {
            root["inkFeathers"] = new JsonObject
            {
                ["current"] = 0,
                ["total"] = 0
            };
        }
    }

    private static void NormalizeSoulRelicsShape(JsonObject root)
    {
        if (root["soulRelics"] is JsonArray flatRelics)
        {
            var equipped = new JsonArray();
            var stored = new JsonArray();
            foreach (var relic in flatRelics.OfType<JsonObject>())
            {
                var clone = CloneObject(relic);
                var isEquipped = clone["gameplayStatus"] is JsonObject gameplayStatus &&
                                 gameplayStatus["equipped"] is JsonValue eqValue &&
                                 eqValue.TryGetValue<bool>(out var eq) && eq;
                if (isEquipped) equipped.Add(clone);
                else stored.Add(clone);
            }

            root["soulRelics"] = new JsonObject
            {
                ["equipped"] = equipped,
                ["stored"] = stored
            };
        }
        else if (root["soulRelics"] is JsonObject soulRelics)
        {
            EnsureArray(soulRelics, "equipped");
            EnsureArray(soulRelics, "stored");
        }
        else
        {
            root["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray()
            };
        }
    }

    private static void UpsertRelic(JsonArray array, JsonObject relic)
    {
        var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
        var existing = FindRelic(array, relicId);
        if (existing != null)
        {
            MergeObject(existing, relic);
            return;
        }
        array.Add(relic.DeepClone());
    }

    private static JsonObject? FindRelic(JsonArray array, string? relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return null;
        return array
            .OfType<JsonObject>()
            .FirstOrDefault(item =>
                string.Equals(GetNodeString(item["relicId"]) ?? GetNodeString(item["id"]), relicId, StringComparison.OrdinalIgnoreCase));
    }

    private static void RemoveRelic(JsonArray array, string? relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return;
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject obj) continue;
            var itemId = GetNodeString(obj["relicId"]) ?? GetNodeString(obj["id"]);
            if (string.Equals(itemId, relicId, StringComparison.OrdinalIgnoreCase))
            {
                array.RemoveAt(i);
                return;
            }
        }
    }

    private static JsonObject? TakeRelic(JsonArray array, string? relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId)) return null;
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject obj) continue;
            var itemId = GetNodeString(obj["relicId"]) ?? GetNodeString(obj["id"]);
            if (string.Equals(itemId, relicId, StringComparison.OrdinalIgnoreCase))
            {
                array.RemoveAt(i);
                return obj;
            }
        }

        return null;
    }

    private static void SetRelicEquipped(JsonObject relic, bool equipped, string slot)
    {
        var gameplayStatus = relic["gameplayStatus"] as JsonObject ?? new JsonObject();
        gameplayStatus["equipped"] = equipped;
        gameplayStatus["currentSlot"] = equipped && !string.IsNullOrWhiteSpace(slot) ? slot : null;
        relic["gameplayStatus"] = gameplayStatus;

        if (equipped && !string.IsNullOrWhiteSpace(slot))
            relic["slot"] = slot;
    }

    private static JsonObject? FindGuardian(JsonArray guardians, string guardianId)
    {
        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(g => string.Equals(GetNodeString(g["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));

        if (guardian != null)
        {
            GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
            return guardian;
        }

        return null;
    }

    private static void UpsertGuardian(JsonArray guardians, JsonObject guardian)
    {
        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);
        var guardianId = GetNodeString(guardian["guardianId"]);
        var existing = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(g =>
                !string.IsNullOrWhiteSpace(guardianId) &&
                string.Equals(GetNodeString(g["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            MergeObject(existing, guardian);
        else
            guardians.Add(guardian.DeepClone());
    }

    private static void SyncActiveGuardian(JsonObject root, string guardianId, JsonObject guardian)
    {
        if (root["activeGuardian"] is not JsonObject activeGuardian)
            return;

        if (string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
            root["activeGuardian"] = guardian.DeepClone();
    }

    private static void RemoveQuestFromArray(JsonArray? array, string? questId)
    {
        if (array == null || string.IsNullOrWhiteSpace(questId)) return;
        for (int i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is not JsonObject obj) continue;
            if (string.Equals(GetNodeString(obj["questId"]), questId, StringComparison.OrdinalIgnoreCase))
                array.RemoveAt(i);
        }
    }

    private static IEnumerable<JsonNode> CollectChronicleEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var entry in rootArray)
                if (entry != null)
                    yield return entry.DeepClone();
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj["entries"] is JsonArray entries)
        {
            foreach (var entry in entries)
                if (entry != null)
                    yield return entry.DeepClone();
        }

        if (obj["characterChronicleUpdates"] is JsonArray updates)
        {
            foreach (var update in updates)
                if (update != null)
                    yield return update.DeepClone();
        }
    }

    private static void CollectAchievementObjects(JsonObject? root, string fieldName, List<JsonObject> destination)
    {
        if (root?[fieldName] is not JsonArray arr)
            return;

        foreach (var item in arr.OfType<JsonObject>())
            UpsertByIdentity(destination, item, "achievementId", "name");
    }

    private static JsonObject BuildAchievementStats(List<JsonObject> unlocked)
    {
        var byCategory = new JsonObject();
        var byRarity = new JsonObject();

        foreach (var category in CanonicalAchievementCategories)
            byCategory[category] = 0;
        foreach (var rarity in CanonicalAchievementRarities)
            byRarity[rarity] = 0;

        foreach (var category in unlocked.Select(a => GetNodeString(a["category"]) ?? "other").GroupBy(c => c))
            byCategory[category.Key] = category.Count();
        foreach (var rarity in unlocked.Select(a => GetNodeString(a["rarity"]) ?? "common").GroupBy(r => r))
            byRarity[rarity.Key] = rarity.Count();

        return new JsonObject
        {
            ["totalUnlocked"] = unlocked.Count,
            ["byCategory"] = byCategory,
            ["byRarity"] = byRarity
        };
    }

    private static void CollectCodexEntries(JsonObject? root, List<JsonObject> destination)
    {
        if (root?["entries"] is not JsonArray entries)
            return;

        foreach (var entry in entries.OfType<JsonObject>())
            UpsertByIdentity(destination, entry, "entryId", "title");
    }

    private static void ApplyCodexUpdates(List<JsonObject> entries, JsonArray updates)
    {
        foreach (var update in updates.OfType<JsonObject>())
        {
            var command = GetNodeString(update["command"]);
            if (string.Equals(command, "add", StringComparison.OrdinalIgnoreCase) &&
                update["entry"] is JsonObject entry)
            {
                UpsertByIdentity(entries, entry, "entryId", "title");
            }
            else if (string.Equals(command, "update", StringComparison.OrdinalIgnoreCase))
            {
                var entryId = GetNodeString(update["entryId"]);
                if (string.IsNullOrWhiteSpace(entryId) || update["updates"] is not JsonObject patch)
                    continue;

                var existing = entries.FirstOrDefault(e =>
                    string.Equals(GetNodeString(e["entryId"]), entryId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    MergeObject(existing, patch);
            }
        }
    }

    private static JsonObject BuildCodexCategoryStats(List<JsonObject> entries)
    {
        var categories = new JsonObject();
        foreach (var categoryName in CanonicalCodexCategories)
            categories[categoryName] = 0;
        foreach (var category in entries.Select(e => GetNodeString(e["category"]) ?? "other").GroupBy(c => c))
            categories[category.Key] = category.Count();
        return categories;
    }

    private static IEnumerable<JsonNode> CollectQuestHistoryEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray)
            {
                if (item == null) continue;
                yield return NormalizeQuestHistoryEntry(item);
            }
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj["quests"] is JsonArray quests)
        {
            foreach (var item in quests)
            {
                if (item == null) continue;
                yield return NormalizeQuestHistoryEntry(item);
            }
        }

        if (obj["questHistory"] is JsonArray questHistory)
        {
            foreach (var item in questHistory)
            {
                if (item == null) continue;
                yield return NormalizeQuestHistoryEntry(item);
            }
        }

        if (obj["questLog"] is JsonArray questLogArray)
        {
            foreach (var item in questLogArray)
            {
                if (item == null) continue;
                yield return NormalizeQuestHistoryEntry(item);
            }
        }
        else if (obj["questLog"] != null)
        {
            yield return NormalizeQuestHistoryEntry(obj["questLog"]!);
        }
    }

    private static JsonNode NormalizeQuestHistoryEntry(JsonNode entry)
    {
        if (entry is JsonObject obj)
            return obj.DeepClone();

        return new JsonObject
        {
            ["name"] = entry.ToString(),
            ["status"] = "history"
        };
    }

    private static IEnumerable<JsonObject> CollectQuestStateEntries(JsonNode? root, string updateProp)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return NormalizeQuestStateEntry(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { "quests", updateProp })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return NormalizeQuestStateEntry(item);
        }

        if (obj.ContainsKey("questId") || obj.ContainsKey("questName") || obj.ContainsKey("title") || obj.ContainsKey("name"))
            yield return NormalizeQuestStateEntry(obj);
    }

    private static IEnumerable<JsonObject> CollectRivalSoulArcEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return CloneObject(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { "arcs", "UpdateRivalSoulArcs" })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("arcId"))
            yield return CloneObject(obj);
    }

    private static JsonObject NormalizeQuestStateEntry(JsonObject source)
    {
        var result = CloneObject(source);
        var appendedEntry = GetNodeString(result["newDetailsLogEntry"]);
        if (!string.IsNullOrWhiteSpace(appendedEntry))
            result["__appendDetailsLogEntry"] = appendedEntry;
        result.Remove("newDetailsLogEntry");
        return result;
    }

    private static void UpsertQuestByIdentity(JsonArray quests, JsonObject candidate)
    {
        var existing = quests
            .OfType<JsonObject>()
            .FirstOrDefault(item => MatchesByAnyIdentity(item, candidate, "questId", "initialId", "questName", "title", "name"));

        var appendEntry = GetNodeString(candidate["__appendDetailsLogEntry"]);
        candidate.Remove("__appendDetailsLogEntry");

        if (existing != null)
        {
            MergeObject(existing, candidate);
            if (!string.IsNullOrWhiteSpace(appendEntry))
            {
                var detailsLog = EnsureArray(existing, "detailsLog");
                detailsLog.Add(appendEntry);
            }
            return;
        }

        var clone = CloneObject(candidate);
        if (!string.IsNullOrWhiteSpace(appendEntry))
        {
            var detailsLog = EnsureArray(clone, "detailsLog");
            detailsLog.Add(appendEntry);
        }
        quests.Add(clone);
    }

    private static bool MatchesByAnyIdentity(JsonObject left, JsonObject right, params string[] keys)
    {
        foreach (var key in keys)
        {
            var leftValue = GetNodeString(left[key]);
            var rightValue = GetNodeString(right[key]);
            if (!string.IsNullOrWhiteSpace(leftValue) &&
                !string.IsNullOrWhiteSpace(rightValue) &&
                string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectNamedObjectEntries(JsonNode? root, string propName, List<JsonObject> target)
    {
        if (root is not JsonObject obj || obj[propName] is not JsonArray arr)
            return;

        foreach (var item in arr.OfType<JsonObject>())
        {
            var clone = CloneObject(item);
            var identityKeys = propName.Equals("questChains", StringComparison.OrdinalIgnoreCase)
                ? new[] { "chainId", "currentQuest" }
                : new[] { "questId", "name" };
            UpsertByIdentity(target, clone, identityKeys);
        }
    }

    private static IEnumerable<JsonObject> CollectInventorySidecarEntries(JsonNode? root, params string[] propNames)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return CloneObject(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in propNames)
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }
    }

    private static IEnumerable<JsonObject> CollectInventoryTextEntries(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        if (obj["entries"] is JsonArray entries)
        {
            foreach (var item in entries.OfType<JsonObject>())
                yield return CloneObject(item);
        }
    }

    private static IEnumerable<JsonObject> CollectFactionCoreEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return CloneObject(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }
    }

    private static JsonObject NormalizeFactionCoreEntry(JsonObject source)
    {
        var entry = CloneObject(source);
        var factionId = GetNodeString(entry["factionId"]);
        var initialId = GetNodeString(entry["initialId"]);
        var hasExplicitNullFactionId = entry.ContainsKey("factionId") && entry["factionId"] == null;

        if (string.IsNullOrWhiteSpace(factionId) &&
            !string.IsNullOrWhiteSpace(initialId) &&
            hasExplicitNullFactionId &&
            entry["isNewFaction"] is JsonValue isNewFactionValue &&
            isNewFactionValue.TryGetValue<bool>(out var isNewFaction) &&
            isNewFaction &&
            LooksLikeCanonicalNewFactionEntry(entry))
        {
            entry["factionId"] = initialId;
            entry.Remove("initialId");
            entry.Remove("isNewFaction");
        }

        return entry;
    }

    private static bool LooksLikeCanonicalNewFactionEntry(JsonObject entry)
    {
        return !string.IsNullOrWhiteSpace(GetNodeString(entry["name"])) &&
               !string.IsNullOrWhiteSpace(GetNodeString(entry["description"])) &&
               !string.IsNullOrWhiteSpace(GetNodeString(entry["developmentArchetype"])) &&
               entry["powerProfile"] is JsonObject &&
               entry["resources"] is JsonObject &&
               entry["ranks"] is JsonObject &&
               entry.ContainsKey("isPlayerFaction") &&
               entry.ContainsKey("isPlayerMember") &&
               entry["reputation"] != null &&
               entry["level"] != null &&
               entry["experience"] != null &&
               entry["experienceForNextLevel"] != null;
    }

    private static void ApplyInventoryResourceCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            if (command["resource"] != null)
                entry["resource"] = command["resource"]?.DeepClone();
            if (command["maximumResource"] != null)
                entry["maximumResource"] = command["maximumResource"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["resourceType"])))
                entry["resourceType"] = GetNodeString(command["resourceType"]);
            if (command["contentsPath"] != null)
                entry["contentsPath"] = command["contentsPath"]?.DeepClone();
            if (command["isEmpty"] != null)
                entry["isEmpty"] = command["isEmpty"]?.DeepClone();
        }
    }

    private static void ApplyInventoryBondCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            if (command["newBondLevel"] != null)
                entry["ownerBondLevelCurrent"] = command["newBondLevel"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["changeReason"])))
                entry["lastBondChangeReason"] = GetNodeString(command["changeReason"]);
        }
    }

    private static void ApplyInventoryFateCardUnlockCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var fateCards = EnsureArray(entry, "fateCards");

            var card = new JsonObject
            {
                ["cardId"] = GetNodeString(command["cardId"]) ?? "",
                ["name"] = GetNodeString(command["cardName"]) ?? GetNodeString(command["cardId"]) ?? "card",
                ["isUnlocked"] = true
            };

            UpsertByIdentity(fateCards, card, "cardId", "name");
        }
    }

    private static void ApplyItemJournalCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var appendedEntry = GetNodeString(command["entryToAppend"]);
            if (string.IsNullOrWhiteSpace(appendedEntry))
                continue;

            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var journalEntries = EnsureArray(entry, "journalEntries");
            AddUniqueNode(journalEntries, JsonValue.Create(appendedEntry)!);
        }
    }

    private static void ApplyInventoryTextCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var textToAppend = GetNodeString(command["textToAppend"]);
            if (string.IsNullOrWhiteSpace(textToAppend))
                continue;

            var entry = GetOrCreateInventoryItemEntry(entries, command);
            var textContent = EnsureArray(entry, "textContent");
            textContent.Add(textToAppend);
        }
    }

    private static IEnumerable<JsonObject> CollectFactionEntryObjects(JsonNode? root, string propName)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
            {
                var clone = CloneObject(item);
                NormalizeStoredFactionReference(clone);
                yield return clone;
            }
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        if (obj[propName] is JsonArray arr)
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                var clone = CloneObject(item);
                NormalizeStoredFactionReference(clone);
                yield return clone;
            }
        }
    }

    private static IEnumerable<JsonObject> CollectFactionStructureEntriesFromCore(JsonNode? root)
    {
        foreach (var rawEntry in CollectFactionEntryObjects(root, "factions"))
        {
            var entry = NormalizeFactionCoreEntry(rawEntry);
            var hasRanks = entry["ranks"] is JsonObject;
            var hasStructuredBonuses = entry["structuredBonuses"] is JsonArray;
            if (!hasRanks && !hasStructuredBonuses)
                continue;

            var result = new JsonObject
            {
                ["factionId"] = entry["factionId"]?.DeepClone(),
                ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                ["name"] = entry["name"]?.DeepClone(),
                ["structuredBonuses"] = new JsonArray()
            };

            if (hasRanks && entry["ranks"] is JsonObject ranks)
                result["ranks"] = ranks.DeepClone();
            if (hasStructuredBonuses && entry["structuredBonuses"] is JsonArray structuredBonuses)
                result["structuredBonuses"] = structuredBonuses.DeepClone();
            yield return result;
        }

        foreach (var rawEntry in CollectFactionEntryObjects(root, "factionDataChanges"))
        {
            var entry = NormalizeFactionCoreEntry(rawEntry);
            var hasRanks = entry["ranks"] is JsonObject;
            var hasStructuredBonuses = entry["structuredBonuses"] is JsonArray;
            if (!hasRanks && !hasStructuredBonuses)
                continue;

            var result = new JsonObject
            {
                ["factionId"] = entry["factionId"]?.DeepClone(),
                ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                ["name"] = entry["name"]?.DeepClone(),
                ["structuredBonuses"] = new JsonArray()
            };

            if (hasRanks && entry["ranks"] is JsonObject ranks)
                result["ranks"] = ranks.DeepClone();
            if (hasStructuredBonuses && entry["structuredBonuses"] is JsonArray structuredBonuses)
                result["structuredBonuses"] = structuredBonuses.DeepClone();
            yield return result;
        }
    }

    private static IEnumerable<JsonObject> CollectFactionResourceEntriesFromCore(JsonNode? root)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                if (entry["resources"] is not JsonObject resources)
                    continue;

                var result = new JsonObject
                {
                    ["factionId"] = entry["factionId"]?.DeepClone(),
                    ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                    ["name"] = entry["name"]?.DeepClone(),
                    ["metaResources"] = new JsonArray(),
                    ["strategicGoods"] = new JsonArray()
                };

                if (resources["metaResources"] is JsonArray metaResources)
                    result["metaResources"] = metaResources.DeepClone();
                if (resources["strategicGoods"] is JsonArray strategicGoods)
                    result["strategicGoods"] = strategicGoods.DeepClone();

                if (result["metaResources"] != null || result["strategicGoods"] != null)
                    yield return result;
            }
        }
    }

    private static void CollectFactionProjectsFromCore(JsonNode? root, List<JsonObject> activeProjects, List<JsonObject> completedProjects)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                var factionId = GetNodeString(entry["factionId"]);
                var factionName = GetNodeString(entry["factionName"]) ?? GetNodeString(entry["name"]);

                if (entry["activeProjects"] is JsonArray activeArray)
                {
                    foreach (var project in activeArray.OfType<JsonObject>())
                    {
                        var projectClone = CloneObject(project);
                        projectClone["factionId"] = factionId;
                        projectClone["factionName"] = factionName;
                        UpsertProjectByIdentity(activeProjects, projectClone);
                    }
                }

                if (entry["completedProjects"] is JsonArray completedArray)
                {
                    foreach (var project in completedArray.OfType<JsonObject>())
                    {
                        var projectClone = CloneObject(project);
                        projectClone["factionId"] = factionId;
                        projectClone["factionName"] = factionName;
                        UpsertProjectByIdentity(completedProjects, projectClone);
                    }
                }
            }
        }
    }

    private static IEnumerable<JsonObject> CollectFactionCustomEntriesFromCore(JsonNode? root)
    {
        foreach (var propName in new[] { "factions", "factionDataChanges" })
        {
            foreach (var rawEntry in CollectFactionEntryObjects(root, propName))
            {
                var entry = NormalizeFactionCoreEntry(rawEntry);
                if (entry["customStates"] is not JsonArray customStates)
                    continue;

                yield return new JsonObject
                {
                    ["factionId"] = entry["factionId"]?.DeepClone(),
                    ["factionName"] = entry["factionName"]?.DeepClone() ?? entry["name"]?.DeepClone(),
                    ["name"] = entry["name"]?.DeepClone(),
                    ["customStates"] = customStates.DeepClone()
                };
            }
        }
    }

    private static void ApplyFactionRankChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var ranksRoot = entry["ranks"] as JsonObject ?? new JsonObject();
            var branches = EnsureArray(ranksRoot, "branches");

            if (command["branchesToRemove"] is JsonArray branchesToRemove)
            {
                foreach (var branchIdNode in branchesToRemove)
                {
                    var branchId = GetNodeString(branchIdNode);
                    if (string.IsNullOrWhiteSpace(branchId))
                        continue;

                    RemoveBranchById(branches, branchId);
                }
            }

            if (command["ranksToRemove"] is JsonArray ranksToRemove)
            {
                foreach (var rankRemoval in ranksToRemove.OfType<JsonObject>())
                    RemoveRankByIdentifier(branches, GetNodeString(rankRemoval["targetBranchId"]), GetNodeString(rankRemoval["rankIdentifier"]));
            }

            if (command["branchesToAdd"] is JsonArray branchesToAdd)
            {
                foreach (var branch in branchesToAdd.OfType<JsonObject>())
                {
                    var branchClone = CloneObject(branch);
                    EnsureArray(branchClone, "ranks");
                    UpsertByIdentity(branches, branchClone, "branchId", "displayName");
                }
            }

            if (command["ranksToAdd"] is JsonArray ranksToAdd)
            {
                foreach (var rankAdd in ranksToAdd.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(rankAdd["targetBranchId"]);
                    if (string.IsNullOrWhiteSpace(branchId) || rankAdd["rank"] is not JsonObject rank)
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    var rankArray = EnsureArray(branch, "ranks");
                    UpsertByIdentity(rankArray, CloneObject(rank), "rankNameMale", "rankNameFemale", "name");
                }
            }

            if (command["branchesToUpdate"] is JsonArray branchesToUpdate)
            {
                foreach (var branchUpdate in branchesToUpdate.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(branchUpdate["branchId"]);
                    if (string.IsNullOrWhiteSpace(branchId))
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    if (!string.IsNullOrWhiteSpace(GetNodeString(branchUpdate["newDisplayName"])))
                        branch["displayName"] = GetNodeString(branchUpdate["newDisplayName"]);
                }
            }

            if (command["ranksToUpdate"] is JsonArray ranksToUpdate)
            {
                foreach (var rankUpdate in ranksToUpdate.OfType<JsonObject>())
                {
                    var branchId = GetNodeString(rankUpdate["targetBranchId"]);
                    var rankIdentifier = GetNodeString(rankUpdate["rankIdentifier"]);
                    if (string.IsNullOrWhiteSpace(branchId) || string.IsNullOrWhiteSpace(rankIdentifier) || rankUpdate["update"] is not JsonObject update)
                        continue;

                    var branch = GetOrCreateBranch(branches, branchId);
                    var rankArray = EnsureArray(branch, "ranks");
                    var rank = rankArray
                        .OfType<JsonObject>()
                        .FirstOrDefault(item =>
                            string.Equals(GetNodeString(item["rankNameMale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(GetNodeString(item["rankNameFemale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(GetNodeString(item["name"]), rankIdentifier, StringComparison.OrdinalIgnoreCase));
                    if (rank == null)
                        continue;

                    ApplyRankUpdate(rank, update);
                }
            }

            entry["ranks"] = ranksRoot;
        }
    }

    private static void ApplyFactionBonusChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var bonuses = EnsureArray(entry, "structuredBonuses");

            if (command["bonusesToRemove"] is JsonArray bonusesToRemove)
            {
                foreach (var bonusIdNode in bonusesToRemove)
                {
                    var bonusId = GetNodeString(bonusIdNode);
                    if (string.IsNullOrWhiteSpace(bonusId))
                        continue;

                    RemoveByIdentity(bonuses, "bonusId", bonusId);
                }
            }

            if (command["bonusesToAddOrUpdate"] is JsonArray bonusesToAddOrUpdate)
            {
                foreach (var bonus in bonusesToAddOrUpdate.OfType<JsonObject>())
                    UpsertByIdentity(bonuses, CloneObject(bonus), "bonusId", "description", "bonusType", "target");
            }
        }
    }

    private static void ApplyFactionResourceChangeCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var metaResources = EnsureArray(entry, "metaResources");
            var strategicGoods = EnsureArray(entry, "strategicGoods");

            if (command["resourceChanges"] is not JsonArray resourceChanges)
                continue;

            foreach (var resourceChange in resourceChanges.OfType<JsonObject>())
            {
                var resourceName = GetNodeString(resourceChange["resourceName"]);
                if (string.IsNullOrWhiteSpace(resourceName))
                    continue;

                var targetArray = IsMetaResource(resourceName) ? metaResources : strategicGoods;
                var resource = targetArray
                    .OfType<JsonObject>()
                    .FirstOrDefault(item => string.Equals(GetNodeString(item["resourceName"]), resourceName, StringComparison.OrdinalIgnoreCase));
                if (resource == null)
                {
                    resource = new JsonObject
                    {
                        ["resourceName"] = resourceName,
                        ["currentStockpile"] = 0,
                        ["incomePerCycle"] = 0
                    };
                    if (IsMetaResource(resourceName))
                        resource["upkeepPerCycle"] = 0;
                    targetArray.Add(resource);
                }

                var currentStockpile = GetNodeInt(resource["currentStockpile"]);
                resource["currentStockpile"] = currentStockpile + GetNodeInt(resourceChange["changeAmount"]);
            }
        }
    }

    private static void CollectFactionProjectObjects(JsonObject? root, string propName, List<JsonObject> target)
    {
        if (root?[propName] is not JsonArray arr)
            return;

        foreach (var item in arr.OfType<JsonObject>())
            UpsertProjectByIdentity(target, CloneObject(item));
    }

    private static void CollectFactionProjectObjects(JsonNode? root, string propName, List<JsonObject> target)
    {
        if (root is JsonObject obj)
            CollectFactionProjectObjects(obj, propName, target);
    }

    private static void ApplyFactionProjectUpdateCommands(List<JsonObject> activeProjects, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            if (command["projectUpdate"] is not JsonObject projectUpdate)
                continue;

            var project = new JsonObject
            {
                ["factionId"] = command["factionId"]?.DeepClone() ?? command["initialFactionId"]?.DeepClone(),
                ["factionName"] = command["factionName"]?.DeepClone() ?? command["name"]?.DeepClone(),
                ["projectId"] = projectUpdate["projectId"]?.DeepClone()
            };

            MergeObject(project, projectUpdate);
            NormalizeStoredFactionReference(project);
            if (string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])) &&
                !string.IsNullOrWhiteSpace(GetNodeString(project["name"])))
            {
                project["projectName"] = GetNodeString(project["name"]);
            }
            if (string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])))
                project["projectName"] = GetNodeString(project["projectId"]) ?? "project";

            UpsertProjectByIdentity(activeProjects, project);
        }
    }

    private static void ApplyFactionProjectCompletionCommands(List<JsonObject> activeProjects, List<JsonObject> completedProjects, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var factionId = GetNodeString(command["factionId"]);
            var initialFactionId = GetNodeString(command["initialFactionId"]);
            var effectiveFactionId = ResolveFactionIdentity(factionId, initialFactionId);
            var factionName = GetNodeString(command["factionName"]) ?? GetNodeString(command["name"]);
            var projectId = GetNodeString(command["projectId"]);
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            var existing = activeProjects.FirstOrDefault(project =>
                string.Equals(GetNodeString(project["projectId"]), projectId, StringComparison.OrdinalIgnoreCase) &&
                FactionIdentityMatches(project, effectiveFactionId));

            var completed = existing != null ? CloneObject(existing) : new JsonObject();
            completed["factionId"] = effectiveFactionId;
            completed["factionName"] = factionName;
            completed["projectId"] = projectId;
            completed["projectName"] = GetNodeString(command["projectName"]) ?? GetNodeString(completed["projectName"]) ?? projectId;
            completed["finalState"] = GetNodeString(command["finalState"]) ?? "Completed";
            completed["completionTurn"] = GetNodeString(command["completionTurn"]) ?? GetNodeString(completed["completionTurn"]) ?? "";
            completed.Remove("activeState");
            completed.Remove("initialFactionId");

            if (existing != null)
                activeProjects.Remove(existing);

            UpsertProjectByIdentity(completedProjects, completed);
        }
    }

    private static void ApplyFactionCustomStateCommands(JsonArray entries, JsonArray commands)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var entry = GetOrCreateFactionEntry(entries, command);
            var customStates = EnsureArray(entry, "customStates");

            if (command["statesToRemove"] is JsonArray statesToRemove)
            {
                foreach (var stateIdNode in statesToRemove)
                {
                    var stateId = GetNodeString(stateIdNode);
                    if (string.IsNullOrWhiteSpace(stateId))
                        continue;

                    RemoveByIdentity(customStates, "stateId", stateId);
                }
            }

            if (command["statesToAddOrUpdate"] is JsonArray statesToAddOrUpdate)
            {
                foreach (var state in statesToAddOrUpdate.OfType<JsonObject>())
                    UpsertByIdentity(customStates, CloneObject(state), "stateId", "name", "title");
            }
        }
    }

    private static JsonObject GetOrCreateFactionEntry(JsonArray entries, JsonObject source)
    {
        var factionId = ResolveFactionIdentity(GetNodeString(source["factionId"]), GetNodeString(source["initialFactionId"]));
        var existing = entries
            .OfType<JsonObject>()
            .FirstOrDefault(item => FactionIdentityMatches(item, factionId));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(factionId))
                existing["factionId"] = factionId;
            existing.Remove("initialFactionId");
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["factionName"])))
                existing["factionName"] = source["factionName"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["name"])))
                existing["name"] = source["name"]?.DeepClone();
            return existing;
        }

        var created = new JsonObject
        {
            ["factionId"] = factionId,
            ["factionName"] = source["factionName"]?.DeepClone() ?? source["name"]?.DeepClone() ?? source["initialFactionId"]?.DeepClone(),
            ["name"] = source["name"]?.DeepClone() ?? source["factionName"]?.DeepClone()
        };
        entries.Add(created);
        return created;
    }

    private static JsonObject GetOrCreateInventoryItemEntry(JsonArray entries, JsonObject source)
    {
        var existing = entries
            .OfType<JsonObject>()
            .FirstOrDefault(item =>
                MatchesByAnyIdentity(item, source, "existedId", "itemId", "id", "itemName", "name"));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["existedId"])))
                existing["existedId"] = source["existedId"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["itemId"])))
                existing["itemId"] = source["itemId"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["id"])))
                existing["id"] = source["id"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["itemName"])))
                existing["itemName"] = source["itemName"]?.DeepClone();
            if (!string.IsNullOrWhiteSpace(GetNodeString(source["name"])))
                existing["name"] = source["name"]?.DeepClone();
            return existing;
        }

        var created = new JsonObject
        {
            ["existedId"] = source["existedId"]?.DeepClone() ?? source["itemId"]?.DeepClone() ?? source["id"]?.DeepClone(),
            ["itemId"] = source["itemId"]?.DeepClone() ?? source["existedId"]?.DeepClone() ?? source["id"]?.DeepClone(),
            ["id"] = source["id"]?.DeepClone() ?? source["itemId"]?.DeepClone() ?? source["existedId"]?.DeepClone(),
            ["itemName"] = source["itemName"]?.DeepClone() ?? source["name"]?.DeepClone(),
            ["name"] = source["name"]?.DeepClone() ?? source["itemName"]?.DeepClone()
        };
        entries.Add(created);
        return created;
    }

    private static JsonObject GetOrCreateBranch(JsonArray branches, string branchId)
    {
        var branch = branches
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["branchId"]), branchId, StringComparison.OrdinalIgnoreCase));
        if (branch != null)
            return branch;

        branch = new JsonObject
        {
            ["branchId"] = branchId,
            ["displayName"] = branchId,
            ["isCoreBranch"] = false,
            ["ranks"] = new JsonArray()
        };
        branches.Add(branch);
        return branch;
    }

    private static void ApplyRankUpdate(JsonObject rank, JsonObject update)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newRankNameMale"])))
            rank["rankNameMale"] = GetNodeString(update["newRankNameMale"]);
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newRankNameFemale"])))
            rank["rankNameFemale"] = GetNodeString(update["newRankNameFemale"]);
        if (update["newRequiredReputation"] != null)
            rank["requiredReputation"] = update["newRequiredReputation"]?.DeepClone();
        if (!string.IsNullOrWhiteSpace(GetNodeString(update["newUnlockCondition"])))
            rank["unlockCondition"] = GetNodeString(update["newUnlockCondition"]);
        if (update["newBenefits"] is JsonArray newBenefits)
            rank["benefits"] = newBenefits.DeepClone();
        if (update["newIsJunctionPoint"] != null)
            rank["isJunctionPoint"] = update["newIsJunctionPoint"]?.DeepClone();
        if (update["newAvailableBranches"] is JsonArray newAvailableBranches)
            rank["availableBranches"] = newAvailableBranches.DeepClone();
    }

    private static void RemoveBranchById(JsonArray branches, string? branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
            return;

        for (var i = branches.Count - 1; i >= 0; i--)
        {
            if (branches[i] is not JsonObject branch)
                continue;
            if (string.Equals(GetNodeString(branch["branchId"]), branchId, StringComparison.OrdinalIgnoreCase))
            {
                branches.RemoveAt(i);
                return;
            }
        }
    }

    private static void RemoveRankByIdentifier(JsonArray branches, string? targetBranchId, string? rankIdentifier)
    {
        if (string.IsNullOrWhiteSpace(targetBranchId) || string.IsNullOrWhiteSpace(rankIdentifier))
            return;

        var branch = branches
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["branchId"]), targetBranchId, StringComparison.OrdinalIgnoreCase));
        if (branch?["ranks"] is not JsonArray ranks)
            return;

        for (var i = ranks.Count - 1; i >= 0; i--)
        {
            if (ranks[i] is not JsonObject rank)
                continue;
            if (string.Equals(GetNodeString(rank["rankNameMale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(rank["rankNameFemale"]), rankIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(rank["name"]), rankIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                ranks.RemoveAt(i);
                return;
            }
        }
    }

    private static bool IsMetaResource(string? resourceName)
    {
        return string.Equals(resourceName, "Wealth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resourceName, "Influence", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resourceName, "Manpower", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveByIdentity(JsonArray items, string keyName, string expectedValue)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is not JsonObject item)
                continue;
            if (string.Equals(GetNodeString(item[keyName]), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                items.RemoveAt(i);
                return;
            }
        }
    }

    private static void UpsertProjectByIdentity(List<JsonObject> projects, JsonObject candidate)
    {
        NormalizeStoredFactionReference(candidate);
        var candidateFactionId = GetNodeString(candidate["factionId"]);
        var existing = projects.FirstOrDefault(project =>
            MatchesByAnyIdentity(project, candidate, "projectId") &&
            FactionIdentityMatches(project, candidateFactionId));

        if (existing != null)
        {
            MergeObject(existing, candidate);
            NormalizeStoredFactionReference(existing);
            return;
        }

        projects.Add(candidate.DeepClone()!.AsObject());
    }

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

        await _fs.WriteFileAtomicAsync(path, resultJson);
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
}
