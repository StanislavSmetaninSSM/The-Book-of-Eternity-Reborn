using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeFactionCoreChangesAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode =
            await ReadNodeAsync(
                FactionCoreChangesContract.FactionCorePath);
        if (currentNode is not JsonObject currentRoot ||
            !currentRoot.ContainsKey(
                FactionCoreChangesContract.PropertyName))
        {
            return;
        }

        var preTurnRoot = await ReadBackupObjectAsync(
            FactionCoreChangesContract.FactionCorePath,
            backups);
        if (preTurnRoot == null)
            return;

        var authority =
            await ReadFactionCoreChangesAuthorityAsync(
                currentRoot,
                preTurnRoot,
                backups);
        var evaluation = FactionCoreChangesContract.Evaluate(
            currentRoot,
            preTurnRoot,
            authority);
        if (!evaluation.CanApply)
            return;

        var result = CloneObject(currentRoot);
        FactionCoreChangesContract.Apply(result, evaluation);
        await WriteIfChangedAsync(
            FactionCoreChangesContract.FactionCorePath,
            currentNode,
            result);
    }

    private async Task<FactionCoreChangesContract.Authority>
        ReadFactionCoreChangesAuthorityAsync(
            JsonObject currentRoot,
            JsonObject preTurnRoot,
            IReadOnlyDictionary<string, string>? backups)
    {
        var factionIds = new HashSet<string>(
            StringComparer.Ordinal);
        CollectFactionCoreChangesFactionIds(
            currentRoot,
            factionIds);
        CollectFactionCoreChangesFactionIds(
            preTurnRoot,
            factionIds);

        const string npcCorePath =
            "game_state/npcs/npc_core.json";
        var npcIds = new HashSet<string>(StringComparer.Ordinal);
        FactionCoreChangesContract.CollectKnownMortalNpcIds(
            await ReadNodeAsync(npcCorePath),
            npcIds);
        FactionCoreChangesContract.CollectKnownMortalNpcIds(
            await ReadBackupObjectAsync(
                npcCorePath,
                backups),
            npcIds);
        return new FactionCoreChangesContract.Authority(
            factionIds,
            npcIds);
    }

    private static void CollectFactionCoreChangesFactionIds(
        JsonObject root,
        HashSet<string> result)
    {
        foreach (var section in new[]
                 {
                     "factions",
                     "factionDataChanges"
                 })
        {
            if (root[section] is not JsonArray factions)
                continue;
            foreach (var faction in factions.OfType<JsonObject>())
            {
                var factionId = GetNodeString(
                    faction["factionId"]);
                if (!string.IsNullOrWhiteSpace(factionId))
                    result.Add(factionId);
            }
        }
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
            UpsertFactionByIdentity(
                factions,
                NormalizeFactionCoreEntry(faction));
        foreach (var faction in CollectFactionCoreEntries(currentNode))
            UpsertFactionByIdentity(
                factions,
                NormalizeFactionCoreEntry(faction));

        foreach (var faction in factions)
            RemoveMaterializedFactionCarrierFields(faction);

        result["factions"] = ToArray(factions);
        result.Remove("factionDataChanges");
        if (currentNode is JsonObject currentRoot &&
            currentRoot.ContainsKey(
                FactionCoreChangesContract.PropertyName))
        {
            result[FactionCoreChangesContract.PropertyName] =
                currentRoot[FactionCoreChangesContract.PropertyName]
                    ?.DeepClone();
        }
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
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionStructureEntriesFromCore(factionCorePrevious))
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionStructureEntriesFromCore(factionCoreCurrent))
            UpsertFactionByIdentity(entries, entry);

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertFactionByIdentity(entries, entry);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertFactionByIdentity(entries, entry);
        }

        foreach (var entry in
                 CollectMaterializedFactionGovernanceAndLeadershipFromCore(
                     factionCoreCurrent))
        {
            UpsertFactionByIdentity(entries, entry);
        }

        if (currentNode is JsonObject currentWithCommands)
        {
            if (currentWithCommands["factionRankChanges"]
                    is JsonArray factionRankChanges)
            {
                ApplyFactionRankChangeCommands(entries, factionRankChanges);
            }

            if (currentWithCommands["factionBonusChanges"]
                    is JsonArray factionBonusChanges)
            {
                ApplyFactionBonusChangeCommands(entries, factionBonusChanges);
            }
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
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionResourceEntriesFromCore(factionCorePrevious))
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionResourceEntriesFromCore(factionCoreCurrent))
            UpsertFactionByIdentity(entries, entry);

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertFactionByIdentity(entries, entry);

            if (currentObj["factionResourceChanges"] is JsonArray factionResourceChanges)
                ApplyFactionResourceChangeCommands(entries, factionResourceChanges);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertFactionByIdentity(entries, entry);
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
        var hasExplicitProjectSurface =
            HasExplicitMaterializedFactionProjectSurface(factionCorePrevious) ||
            HasExplicitMaterializedFactionProjectSurface(factionCoreCurrent);

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

        if (currentNode == null &&
            previous == null &&
            activeProjects.Count == 0 &&
            completedProjects.Count == 0 &&
            !hasExplicitProjectSurface)
        {
            return;
        }

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
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionCustomEntriesFromCore(factionCorePrevious))
            UpsertFactionByIdentity(entries, entry);
        foreach (var entry in CollectFactionCustomEntriesFromCore(factionCoreCurrent))
            UpsertFactionByIdentity(entries, entry);

        if (currentNode is JsonObject currentObj)
        {
            foreach (var entry in CollectFactionEntryObjects(currentObj, "entries"))
                UpsertFactionByIdentity(entries, entry);

            if (currentObj["factionCustomStateChanges"] is JsonArray factionCustomStateChanges)
                ApplyFactionCustomStateCommands(entries, factionCustomStateChanges);
        }
        else
        {
            foreach (var entry in CollectFactionEntryObjects(currentNode, "entries"))
                UpsertFactionByIdentity(entries, entry);
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
        var factionCoreCurrent =
            await ReadNodeAsync("game_state/factions/faction_core.json");
        var factionCorePrevious =
            await ReadBackupObjectAsync(
                "game_state/factions/faction_core.json",
                backups);

        var previous = await ReadBackupObjectAsync(path, backups);
        var initialPreviousEntries =
            CollectInitialFactionChronicleEntries(factionCorePrevious).ToArray();
        var initialCurrentEntries =
            CollectInitialFactionChronicleEntries(factionCoreCurrent).ToArray();
        if (currentNode == null &&
            previous == null &&
            initialPreviousEntries.Length == 0 &&
            initialCurrentEntries.Length == 0)
        {
            return;
        }

        var result = CloneObject(previous ?? new JsonObject());
        var entries = EnsureArray(result, "entries");

        foreach (var entry in CollectFactionChronicleEntries(previous))
            AddUniqueNode(entries, entry);
        foreach (var entry in CollectFactionChronicleEntries(currentNode))
            AddUniqueNode(entries, entry);
        foreach (var entry in initialPreviousEntries)
            AddUniqueFactionChronicleEntry(entries, entry);
        foreach (var entry in initialCurrentEntries)
            AddUniqueFactionChronicleEntry(entries, entry);

        result.Remove("factionChronicleUpdates");
        await WriteIfChangedAsync(path, currentNode, result);
    }

}

