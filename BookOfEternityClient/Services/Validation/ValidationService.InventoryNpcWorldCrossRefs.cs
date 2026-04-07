using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private async Task<HashSet<string>> ReadKnownLocationIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/world/current_location.json"),
                     await ReadPreTurnTrackedFileAsync("game_state/world/world_map.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var location in EnumerateLocationLikeObjects(doc.RootElement, includeLocationUpdates: false))
                {
                    var locationId = GetFirstNonEmptyString(location, "locationId");
                    if (!string.IsNullOrWhiteSpace(locationId))
                        ids.Add(locationId);
                }
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<WorldLocationStateIndex> ReadPreTurnWorldLocationStateIndexAsync()
    {
        var index = new WorldLocationStateIndex();

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/world/current_location.json"),
                     await ReadPreTurnTrackedFileAsync("game_state/world/world_map.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var location in EnumerateLocationLikeObjects(doc.RootElement, includeLocationUpdates: false))
                    RegisterWorldLocationState(index, location);
            }
            catch
            {
                // ignored
            }
        }

        return index;
    }


    private async Task<HashSet<string>> ReadKnownFactionIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var preTurnJson = await ReadPreTurnTrackedFileAsync("game_state/factions/faction_core.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                CollectFactionIdsFromStateRoot(doc.RootElement, ids, preTurnKnownIds: null);
            }
            catch
            {
                // ignored
            }
        }

        var currentJson = await _fs.ReadFileAsync("game_state/factions/faction_core.json");
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentJson);
                CollectFactionIdsFromStateRoot(doc.RootElement, ids, ids);
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<HashSet<string>> ReadKnownCodexEntryIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnJson = await ReadPreTurnTrackedFileAsync("lore/codex_entries.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                CollectCodexEntryIdsFromRoot(doc.RootElement, ids, includeStoredEntries: true);
            }
            catch
            {
                // ignored
            }
        }

        var currentJson = await _fs.ReadFileAsync("lore/codex_entries.json");
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentJson);
                CollectCodexEntryIdsFromRoot(doc.RootElement, ids, includeStoredEntries: false);
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<HashSet<string>> ReadKnownWorldStateFlagIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/world/world_flags.json"),
                     await _fs.ReadFileAsync("game_state/world/world_flags.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("worldStateFlags", out var flags) || flags.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var flag in flags.EnumerateArray())
                {
                    var flagId = GetFirstNonEmptyString(flag, "flagId");
                    if (!string.IsNullOrWhiteSpace(flagId))
                        ids.Add(flagId);
                }
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<HashSet<string>> ReadKnownVehicleIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/misc/vehicles.json"),
                     await _fs.ReadFileAsync("game_state/misc/vehicles.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("vehicles", out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var vehicle in arr.EnumerateArray())
                {
                    var vehicleId = GetFirstNonEmptyString(vehicle, "vehicleId");
                    if (!string.IsNullOrWhiteSpace(vehicleId))
                        ids.Add(vehicleId);
                }
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<(HashSet<string> Ids, HashSet<string> Names)> ReadKnownNpcReferencesAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var npcJson in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json"),
                     await _fs.ReadFileAsync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(npcJson))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(npcJson);
                foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                        var name = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                        if (!string.IsNullOrWhiteSpace(npcId))
                            ids.Add(npcId);
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return (ids, names);
    }


    private async Task<(HashSet<string> Ids, HashSet<string> Names)> ReadPreTurnNpcReferencesAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcJson = await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return (ids, names);

        try
        {
            using var doc = JsonDocument.Parse(npcJson);
            foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
            {
                if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var npc in arr.EnumerateArray())
                {
                    var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                    var name = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                    if (!string.IsNullOrWhiteSpace(npcId))
                        ids.Add(npcId);
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name);
                }
            }
        }
        catch
        {
            // ignored
        }

        return (ids, names);
    }


    private async Task<(Dictionary<string, HashSet<string>> ById, Dictionary<string, HashSet<string>> ByName)> ReadKnownNpcCurrentActivitiesAsync()
    {
        var byId = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var npcJson in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(npcJson))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(npcJson);
                foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        if (!npc.TryGetProperty("currentActivity", out var currentActivity) ||
                            currentActivity.ValueKind != JsonValueKind.Object)
                            continue;

                        var activityName = GetFirstNonEmptyString(currentActivity, "activityName");
                        if (string.IsNullOrWhiteSpace(activityName))
                            continue;

                        var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                        var npcName = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                        if (!string.IsNullOrWhiteSpace(npcId))
                            AddDictionarySetValue(byId, npcId, activityName);
                        if (!string.IsNullOrWhiteSpace(npcName))
                            AddDictionarySetValue(byName, npcName, activityName);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return (byId, byName);
    }


    private (HashSet<string> Ids, HashSet<string> Names) ReadKnownNpcReferencesSync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var npcJson in new[]
                 {
                     ReadPreTurnTrackedFileSync("game_state/npcs/npc_core.json"),
                     TryReadCurrentFileSync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(npcJson))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(npcJson);
                foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                        var name = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                        if (!string.IsNullOrWhiteSpace(npcId))
                            ids.Add(npcId);
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return (ids, names);
    }


    private string? TryReadCurrentFileSync(string relativePath)
    {
        try
        {
            var path = _fs.ResolvePath(relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }


    private (string? LocationId, string? InitialId) ReadCurrentSceneLocationAnchorSync()
    {
        var json = TryReadCurrentFileSync("game_state/world/current_location.json");
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("currentLocationData", out var currentLocationData) &&
                currentLocationData.ValueKind == JsonValueKind.Object)
            {
                root = currentLocationData;
            }

            var locationId = GetFirstNonEmptyString(root, "locationId");
            var initialId = GetFirstNonEmptyString(root, "initialId");
            return (
                string.IsNullOrWhiteSpace(locationId) ? null : locationId,
                string.IsNullOrWhiteSpace(initialId) ? null : initialId);
        }
        catch
        {
            return (null, null);
        }
    }


    private bool IsCurrentSceneNewLocationWithoutInitialIdSync()
    {
        var json = TryReadCurrentFileSync("game_state/world/current_location.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("currentLocationData", out var currentLocationData) &&
                currentLocationData.ValueKind == JsonValueKind.Object)
            {
                root = currentLocationData;
            }

            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("locationId", out var locationId) &&
                   locationId.ValueKind == JsonValueKind.Null &&
                   string.IsNullOrWhiteSpace(GetFirstNonEmptyString(root, "initialId"));
        }
        catch
        {
            return false;
        }
    }


    private Dictionary<string, HashSet<string>> ReadPreTurnNpcUnlockedMemoryIdsByNpcSync()
    {
        var idsByNpc = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var json = ReadPreTurnTrackedFileSync("game_state/npcs/npc_memory.json");
        if (string.IsNullOrWhiteSpace(json))
            return idsByNpc;

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement target;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("NPCUnlockedMemories", out var memories) &&
                memories.ValueKind == JsonValueKind.Array)
            {
                target = memories;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                target = doc.RootElement;
            }
            else
            {
                return idsByNpc;
            }

            foreach (var item in target.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var memoryId = GetFirstNonEmptyString(item, "memoryId");
                if (string.IsNullOrWhiteSpace(memoryId))
                    continue;

                RegisterNpcMemoryId(item, memoryId, idsByNpc);
            }
        }
        catch
        {
            // ignored
        }

        return idsByNpc;
    }


    private (HashSet<string> Ids, HashSet<string> Names) ReadKnownInventoryItemReferencesSync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnInventoryItemIds = ReadPreTurnInventoryItemIdsSync();
        RegisterKnownInventoryItemReferencesFromJson(
            ReadPreTurnTrackedFileSync("game_state/inventory/items.json"),
            ids,
            names,
            knownExistingItemIds: null,
            currentStateNewItemsOnly: false);
        RegisterKnownInventoryItemReferencesFromJson(
            TryReadCurrentFileSync("game_state/inventory/items.json"),
            ids,
            names,
            preTurnInventoryItemIds,
            currentStateNewItemsOnly: true);

        return (ids, names);
    }


    private async Task ValidateLocationCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownLocationIds)
    {
        if (knownLocationIds.Count == 0)
            return;

        var preTurnLocationState = await ReadPreTurnWorldLocationStateIndexAsync();
        var allKnownCoordinateKeys = FlattenCoordinateKeys(preTurnLocationState.CoordinateKeysByLocationId);
        var sameTurnNewLocationCoordinateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentLocationJson = await _fs.ReadFileAsync("game_state/world/current_location.json");
        if (!string.IsNullOrWhiteSpace(currentLocationJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentLocationJson);
                var root = doc.RootElement;
                var locationNode = root;
                var locationContext = "game_state/world/current_location.json";
                if (root.TryGetProperty("currentLocationData", out var currentLocationData) &&
                    currentLocationData.ValueKind == JsonValueKind.Object)
                {
                    locationNode = currentLocationData;
                    locationContext = "game_state/world/current_location.json.currentLocationData";
                }

                var currentLocationId = GetFirstNonEmptyString(locationNode, "locationId");
                if (!string.IsNullOrWhiteSpace(currentLocationId) && !knownLocationIds.Contains(currentLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{locationContext}.locationId",
                        IssueSeverity.Error,
                        $"current_location.locationId '{currentLocationId}' не найден среди известных world locations",
                        code: "current_location_unknown_location_id",
                        section: "Location",
                        expected: "existing locationId from canonical world state",
                        actual: currentLocationId,
                        repairHint: "Для known currentLocationData используй существующий locationId из canonical world state. Если локация действительно новая, передай locationId = null и полный location object."));
                }

                if (!string.IsNullOrWhiteSpace(currentLocationId) &&
                    locationNode.TryGetProperty("coordinates", out var currentCoordinates) &&
                    TryGetNormalizedLocationCoordinatesKey(currentCoordinates, out var currentCoordinateKey) &&
                    preTurnLocationState.CoordinateKeysByLocationId.TryGetValue(currentLocationId, out var knownCoordinatesForLocation) &&
                    knownCoordinatesForLocation.Count > 0 &&
                    !knownCoordinatesForLocation.Contains(currentCoordinateKey))
                {
                    issues.Add(new ValidationIssue(
                        $"{locationContext}.coordinates",
                        IssueSeverity.Error,
                        "known currentLocationData использует coordinates, которые не совпадают с canonical coordinates этой locationId",
                        code: "current_location_coordinates_mismatch",
                        section: "Location",
                        expected: "existing coordinates for the specified locationId from canonical world state",
                        actual: currentCoordinateKey,
                        repairHint: "Для known location передавай exact coordinates из canonical world state этой locationId. Если локация действительно новая, используй locationId = null и полный location object."));
                }
                else if (string.IsNullOrWhiteSpace(currentLocationId) &&
                         locationNode.TryGetProperty("coordinates", out var currentCoordinatesForNewLocation) &&
                         TryGetNormalizedLocationCoordinatesKey(currentCoordinatesForNewLocation, out var newLocationCoordinateKey))
                {
                    ValidateNewLocationCoordinateKey(
                        newLocationCoordinateKey,
                        $"{locationContext}.coordinates",
                        allKnownCoordinateKeys,
                        sameTurnNewLocationCoordinateKeys,
                        issues);
                }

                ValidateAdjacencyTargets(locationNode, locationContext, knownLocationIds, issues);
            }
            catch
            {
                // ignored
            }
        }

        var worldMapJson = await _fs.ReadFileAsync("game_state/world/world_map.json");
        if (!string.IsNullOrWhiteSpace(worldMapJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(worldMapJson);
                var sameTurnLocationInitialIds = CollectSameTurnLocationInitialIds(doc.RootElement);
                var index = 0;
                foreach (var location in EnumerateLocationLikeObjects(doc.RootElement))
                {
                    ValidateAdjacencyTargets(location, $"game_state/world/world_map.json.locations[{index++}]", knownLocationIds, issues);
                }

                var updatesRoot = doc.RootElement.TryGetProperty("worldMapUpdates", out var worldMapUpdates) &&
                                  worldMapUpdates.ValueKind == JsonValueKind.Object
                    ? worldMapUpdates
                    : doc.RootElement;

                if (updatesRoot.TryGetProperty("newLocations", out var newLocations) && newLocations.ValueKind == JsonValueKind.Array)
                {
                    var newLocationIndex = 0;
                    foreach (var newLocation in newLocations.EnumerateArray())
                    {
                        var newLocationContext = $"game_state/world/world_map.json.worldMapUpdates.newLocations[{newLocationIndex++}]";
                        if (newLocation.ValueKind != JsonValueKind.Object ||
                            !newLocation.TryGetProperty("coordinates", out var coordinates) ||
                            !TryGetNormalizedLocationCoordinatesKey(coordinates, out var coordinateKey))
                        {
                            continue;
                        }

                        ValidateNewLocationCoordinateKey(
                            coordinateKey,
                            $"{newLocationContext}.coordinates",
                            allKnownCoordinateKeys,
                            sameTurnNewLocationCoordinateKeys,
                            issues);
                    }
                }

                ValidateWorldMapCommandTargetLocationIds(
                    updatesRoot,
                    "game_state/world/world_map.json",
                    knownLocationIds,
                    preTurnLocationState,
                    sameTurnLocationInitialIds,
                    allKnownCoordinateKeys,
                    sameTurnNewLocationCoordinateKeys,
                    issues);
            }
            catch
            {
                // ignored
            }
        }
    }


    private void ValidateWorldMapCommandTargetLocationIds(
        JsonElement updatesRoot,
        string fileContext,
        HashSet<string> knownLocationIds,
        WorldLocationStateIndex preTurnLocationState,
        HashSet<string> sameTurnLocationInitialIds,
        HashSet<string> knownCoordinateKeys,
        HashSet<string> sameTurnNewLocationCoordinateKeys,
        List<ValidationIssue> issues)
    {
        void ValidateKnownLocationTargetArray(string propName, string fieldName, string code)
        {
            if (!updatesRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.{propName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var locationId = GetFirstNonEmptyString(item, fieldName);
                if (string.IsNullOrWhiteSpace(locationId) || knownLocationIds.Contains(locationId))
                    continue;

                issues.Add(new ValidationIssue(
                    $"{itemContext}.{fieldName}",
                    IssueSeverity.Error,
                    $"{propName} ссылается на неизвестную локацию '{locationId}'",
                    code: code,
                    section: "WorldMap",
                    expected: "existing locationId from canonical world state",
                    actual: locationId,
                    repairHint: $"Для {propName} используй только существующий locationId из canonical world state. Same-turn new locations адресуй через initialId только там, где это явно разрешено rules contract."));
            }
        }

        ValidateKnownLocationTargetArray("locationUpdates", "locationId", "world_map_location_update_unknown_target");
        ValidateKnownLocationTargetArray("storageUpdates", "targetLocationId", "world_map_storage_update_unknown_target");
        ValidateKnownLocationTargetArray("storagesToRemove", "targetLocationId", "world_map_storage_remove_unknown_target");
        ValidateKnownLocationTargetArray("linkUpdates", "sourceLocationId", "world_map_link_update_unknown_source");
        ValidateKnownLocationTargetArray("linksToRemove", "sourceLocationId", "world_map_link_remove_unknown_source");
        ValidateKnownLocationTargetArray("threatsToUpdate", "targetLocationId", "world_map_threat_update_unknown_target");
        ValidateKnownLocationTargetArray("threatsToRemove", "targetLocationId", "world_map_threat_remove_unknown_target");
        ValidateKnownLocationTargetArray("completeThreatActivities", "targetLocationId", "world_map_threat_complete_unknown_target");
        ValidateExistingStorageTargetArray("storageUpdates", "world_map_storage_update_unknown_storage");
        ValidateExistingStorageTargetArray("storagesToRemove", "world_map_storage_remove_unknown_storage");

        void ValidateExistingLinkTargetArray(string propName, string code)
        {
            if (!updatesRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.{propName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var sourceLocationId = GetFirstNonEmptyString(item, "sourceLocationId");
                if (string.IsNullOrWhiteSpace(sourceLocationId) || !knownLocationIds.Contains(sourceLocationId))
                    continue;

                if (!item.TryGetProperty("targetCoordinates", out var targetCoordinates) ||
                    !TryGetNormalizedLocationCoordinatesKey(targetCoordinates, out var targetCoordinateKey))
                {
                    continue;
                }

                if (preTurnLocationState.LinkTargetCoordinateKeysBySourceLocationId.TryGetValue(sourceLocationId, out var targetKeys) &&
                    targetKeys.Contains(targetCoordinateKey))
                {
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{itemContext}.targetCoordinates",
                    IssueSeverity.Error,
                    $"{propName} ссылается на несуществующую canonical link для указанного sourceLocationId",
                    code: code,
                    section: "WorldMap",
                    expected: "existing adjacency link identified by sourceLocationId + targetCoordinates",
                    actual: $"{sourceLocationId} -> {targetCoordinateKey}",
                    repairHint: $"Для {propName} адресуй только реально существующую ссылку из pre-turn adjacencyMap. Если путь создаётся впервые, используй newLinks; если меняется существующий путь, используй exact sourceLocationId + targetCoordinates этой связи."));
            }
        }

        void ValidateExistingStorageTargetArray(string propName, string code)
        {
            if (!updatesRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.{propName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var targetLocationId = GetFirstNonEmptyString(item, "targetLocationId");
                if (string.IsNullOrWhiteSpace(targetLocationId) || !knownLocationIds.Contains(targetLocationId))
                    continue;

                var storageId = GetFirstNonEmptyString(item, "storageId");
                if (string.IsNullOrWhiteSpace(storageId))
                    continue;

                if (preTurnLocationState.StorageIdsByLocationId.TryGetValue(targetLocationId, out var storageIds) &&
                    storageIds.Contains(storageId))
                {
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{itemContext}.storageId",
                    IssueSeverity.Error,
                    $"{propName} ссылается на storageId '{storageId}', которого нет в canonical locationStorages целевой локации",
                    code: code,
                    section: "WorldMap",
                    expected: "existing storageId from canonical locationStorages of the specified targetLocationId",
                    actual: storageId,
                    repairHint: $"Для {propName} используй storageId уже существующего хранилища внутри canonical locationStorages этой targetLocationId. Если хранилище ещё не существует, сначала создай/покажи его через location storage state, а не обновляй или удаляй несуществующий storageId."));
            }
        }

        void ValidateExistingThreatTargetArray(
            string propName,
            string code,
            Func<JsonElement, (string? ThreatId, string ThreatPathSuffix)> threatInfoResolver)
        {
            if (!updatesRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.{propName}[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var targetLocationId = GetFirstNonEmptyString(item, "targetLocationId");
                if (string.IsNullOrWhiteSpace(targetLocationId) || !knownLocationIds.Contains(targetLocationId))
                    continue;

                var threatInfo = threatInfoResolver(item);
                var threatId = threatInfo.ThreatId;
                if (string.IsNullOrWhiteSpace(threatId))
                    continue;

                if (preTurnLocationState.ThreatIdsByLocationId.TryGetValue(targetLocationId, out var knownThreatIds) &&
                    knownThreatIds.Contains(threatId))
                {
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{itemContext}.{threatInfo.ThreatPathSuffix}",
                    IssueSeverity.Error,
                    $"{propName} ссылается на несуществующую canonical threat в указанной локации",
                    code: code,
                    section: "WorldMap",
                    expected: "existing threatId from activeThreats of targetLocationId",
                    actual: $"{targetLocationId}:{threatId}",
                    repairHint: $"Для {propName} используй threatId реально существующей угрозы из activeThreats выбранной локации. Новую угрозу создавай через threatsToAdd, а не через update/remove/complete command."));
            }
        }

        ValidateExistingLinkTargetArray("linkUpdates", "world_map_link_update_unknown_existing_link");
        ValidateExistingLinkTargetArray("linksToRemove", "world_map_link_remove_unknown_existing_link");

        ValidateExistingThreatTargetArray(
            "threatsToUpdate",
            "world_map_threat_update_unknown_existing_threat",
            item =>
            {
                if (!item.TryGetProperty("threatUpdate", out var threatUpdate) || threatUpdate.ValueKind != JsonValueKind.Object)
                    return (null, "threatUpdate.threatId");

                return (GetFirstNonEmptyString(threatUpdate, "threatId"), "threatUpdate.threatId");
            });
        ValidateExistingThreatTargetArray(
            "threatsToRemove",
            "world_map_threat_remove_unknown_existing_threat",
            item => (GetFirstNonEmptyString(item, "threatId"), "threatId"));
        ValidateExistingThreatTargetArray(
            "completeThreatActivities",
            "world_map_threat_complete_unknown_existing_threat",
            item => (GetFirstNonEmptyString(item, "threatId"), "threatId"));

        if (updatesRoot.TryGetProperty("completeThreatActivities", out var threatCompletions) &&
            threatCompletions.ValueKind == JsonValueKind.Array)
        {
            var completionIndex = 0;
            foreach (var item in threatCompletions.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.completeThreatActivities[{completionIndex++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var targetLocationId = GetFirstNonEmptyString(item, "targetLocationId");
                var threatId = GetFirstNonEmptyString(item, "threatId");
                if (string.IsNullOrWhiteSpace(targetLocationId) || string.IsNullOrWhiteSpace(threatId))
                    continue;

                if (preTurnLocationState.ThreatIdsWithCurrentActivityByLocationId.TryGetValue(targetLocationId, out var activeThreatIds) &&
                    activeThreatIds.Contains(threatId))
                    continue;

                issues.Add(new ValidationIssue(
                    $"{itemContext}.threatId",
                    IssueSeverity.Error,
                    "completeThreatActivities ссылается на угрозу без active currentActivity в canonical world_map state",
                    code: "world_map_threat_complete_without_active_current_activity",
                    section: "WorldMap",
                    expected: "existing threatId with non-null currentActivity in targetLocationId.activeThreats",
                    actual: $"{targetLocationId}:{threatId}",
                    repairHint: "Используй completeThreatActivities только для угрозы, у которой уже есть active currentActivity в canonical world_map state. Idle threat сначала обнови обычным non-terminal flow."));
            }
        }

        if (updatesRoot.TryGetProperty("threatsToAdd", out var threatAdds) && threatAdds.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in threatAdds.EnumerateArray())
            {
                var itemContext = $"{fileContext}.worldMapUpdates.threatsToAdd[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var targetLocationId = GetFirstNonEmptyString(item, "targetLocationId");
                if (!string.IsNullOrWhiteSpace(targetLocationId) && !knownLocationIds.Contains(targetLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.targetLocationId",
                        IssueSeverity.Error,
                        $"threatsToAdd.targetLocationId '{targetLocationId}' не найден среди известных локаций",
                        code: "world_map_threat_add_unknown_target",
                        section: "WorldMap",
                        expected: "existing locationId from canonical world state",
                        actual: targetLocationId,
                        repairHint: "Для existing off-screen location используй targetLocationId из canonical world state. Для same-turn new off-screen location используй initialTargetLocationId."));
                }

                var initialTargetLocationId = GetFirstNonEmptyString(item, "initialTargetLocationId");
                if (!string.IsNullOrWhiteSpace(initialTargetLocationId) && !sameTurnLocationInitialIds.Contains(initialTargetLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialTargetLocationId",
                        IssueSeverity.Error,
                        $"threatsToAdd.initialTargetLocationId '{initialTargetLocationId}' не найден среди same-turn new locations",
                        code: "world_map_threat_add_unknown_same_turn_initial_id",
                        section: "WorldMap",
                        expected: "initialId from same-turn newLocations/currentLocationData",
                        actual: initialTargetLocationId,
                        repairHint: "Для same-turn новой локации используй exact initialId, который ты сам создал в newLocations/currentLocationData. Иначе адресуй existing location через targetLocationId."));
                }
            }
        }
    }


    private async Task ValidateWeatherContextHintsAsync(List<ValidationIssue> issues)
    {
        var currentLocationJson = await _fs.ReadFileAsync("game_state/world/current_location.json");
        var weatherJson = await _fs.ReadFileAsync("game_state/world/weather.json");
        if (string.IsNullOrWhiteSpace(currentLocationJson) || string.IsNullOrWhiteSpace(weatherJson))
            return;

        try
        {
            using var locationDoc = JsonDocument.Parse(currentLocationJson);
            var locationRoot = locationDoc.RootElement;
            if (locationRoot.TryGetProperty("currentLocationData", out var currentLocationData) &&
                currentLocationData.ValueKind == JsonValueKind.Object)
            {
                locationRoot = currentLocationData;
            }

            var locationType = GetFirstNonEmptyString(locationRoot, "locationType");
            var biome = GetFirstNonEmptyString(locationRoot, "biome");
            var currentLocationId = GetFirstNonEmptyString(locationRoot, "locationId");
            if ((!string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(biome)) &&
                !string.IsNullOrWhiteSpace(currentLocationId))
            {
                var preTurnLocationState = await ReadPreTurnWorldLocationStateIndexAsync();
                if (!string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) &&
                    preTurnLocationState.LocationTypesByLocationId.TryGetValue(currentLocationId, out var resolvedLocationType))
                {
                    locationType = resolvedLocationType;
                }

                if (string.IsNullOrWhiteSpace(biome) &&
                    preTurnLocationState.BiomesByLocationId.TryGetValue(currentLocationId, out var resolvedBiome))
                {
                    biome = resolvedBiome;
                }
            }

            if (!string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(biome) ||
                !WeatherJumpCommandsByBiome.TryGetValue(biome, out var allowedCommands))
            {
                return;
            }

            using var weatherDoc = JsonDocument.Parse(weatherJson);
            var weatherRoot = weatherDoc.RootElement;
            if (weatherRoot.TryGetProperty("weatherChange", out var weatherChange) &&
                weatherChange.ValueKind == JsonValueKind.Object)
            {
                weatherRoot = weatherChange;
            }

            var tendency = GetFirstNonEmptyString(weatherRoot, "tendency");
            if (string.IsNullOrWhiteSpace(tendency) || !tendency.StartsWith("JUMP_TO_", StringComparison.Ordinal))
                return;

            if (!allowedCommands.Contains(tendency))
            {
                issues.Add(new ValidationIssue(
                    "game_state/world/weather.json.tendency",
                    IssueSeverity.Warning,
                    $"weatherChange.tendency '{tendency}' не выглядит совместимым с текущим outdoor biome '{biome}'",
                    code: "weather_change_biome_command_mismatch_warning",
                    section: "Weather",
                    expected: string.Join(" | ", allowedCommands),
                    actual: tendency,
                    repairHint: $"Если текущая локация действительно outdoor biome '{biome}', используй совместимый JUMP_TO_* command из Rule 27. Для неоднозначных или явно магических случаев перепроверь, что narrative и выбранный tendency описывают одну и ту же погоду."));
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateNpcLocationCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownLocationIds)
    {
        if (knownLocationIds.Count == 0)
            return;

        var npcJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(npcJson);
            foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
            {
                if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                var index = 0;
                foreach (var npc in arr.EnumerateArray())
                {
                    var locationId = GetFirstNonEmptyString(npc, "currentLocationId");
                    if (string.IsNullOrWhiteSpace(locationId))
                    {
                        index++;
                        continue;
                    }

                    if (!knownLocationIds.Contains(locationId))
                    {
                        issues.Add(new ValidationIssue(
                            $"game_state/npcs/npc_core.json.{sectionName}[{index}].currentLocationId",
                            IssueSeverity.Error,
                            $"NPC currentLocationId '{locationId}' не найден среди известных локаций",
                            code: "npc_unknown_current_location_id",
                            section: "NPC",
                            expected: "existing currentLocationId from canonical world state",
                            actual: locationId,
                            repairHint: "Для NPC используй существующий locationId из canonical world state. Если NPC находится в same-turn новой локации, используй initialLocationId-linking вместо несуществующего currentLocationId."));
                    }

                    index++;
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateFactionReferenceCrossReferencesAsync(
        List<ValidationIssue> issues,
        HashSet<string> knownFactionIds,
        HashSet<string> knownLocationIds)
    {
        var json = await _fs.ReadFileAsync("game_state/factions/faction_core.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var propName in new[] { "factionDataChanges", "factions" })
            {
                if (!doc.RootElement.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                var factionIndex = 0;
                foreach (var faction in arr.EnumerateArray())
                {
                    var factionContext = $"game_state/factions/faction_core.json.{propName}[{factionIndex++}]";
                    if (faction.ValueKind != JsonValueKind.Object)
                        continue;

                    if (faction.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
                    {
                        var relationIndex = 0;
                        foreach (var relation in relations.EnumerateArray())
                        {
                            var relationContext = $"{factionContext}.relations[{relationIndex++}]";
                            var targetFactionId = GetFirstNonEmptyString(relation, "targetFactionId");
                            if (!string.IsNullOrWhiteSpace(targetFactionId) && !knownFactionIds.Contains(targetFactionId))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{relationContext}.targetFactionId",
                                    IssueSeverity.Error,
                                    $"Faction relation targetFactionId '{targetFactionId}' не найден среди известных factionId",
                                    code: "faction_relation_unknown_target",
                                    section: "CrossReferences",
                                    expected: "existing factionId from canonical faction_core.json",
                                    actual: targetFactionId,
                                    repairHint: "Используй существующий canonical factionId из faction_core.json. Если цель создаётся в этом же accepted turn, сначала материализуй её в faction_core с permanent factionId, а потом ссылайся на неё из relations."));
                            }
                        }
                    }

                    if (faction.TryGetProperty("controlledTerritories", out var territories) && territories.ValueKind == JsonValueKind.Array)
                    {
                        var territoryIndex = 0;
                        foreach (var territory in territories.EnumerateArray())
                        {
                            var territoryContext = $"{factionContext}.controlledTerritories[{territoryIndex++}]";
                            var locationId = GetFirstNonEmptyString(territory, "locationId");
                            if (!string.IsNullOrWhiteSpace(locationId) && !knownLocationIds.Contains(locationId))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{territoryContext}.locationId",
                                    IssueSeverity.Error,
                                $"Faction controlledTerritories locationId '{locationId}' не найден среди известных локаций",
                                code: "faction_controlled_territory_unknown_location",
                                section: "CrossReferences",
                                expected: "existing locationId from canonical world state",
                                actual: locationId,
                                repairHint: "Используй уже существующий canonical locationId из world state. Не ссылай controlledTerritories на временную или ещё не материализованную локацию."));
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateCodexRelatedEntryCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownCodexEntryIds)
    {
        var json = await _fs.ReadFileAsync("lore/codex_entries.json");
        if (string.IsNullOrWhiteSpace(json) || knownCodexEntryIds.Count == 0)
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                var entryIndex = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    var entryContext = $"lore/codex_entries.json.entries[{entryIndex++}]";
                    if (!entry.TryGetProperty("relatedEntries", out var relatedEntries) || relatedEntries.ValueKind != JsonValueKind.Array)
                        continue;

                    ValidateRelatedCodexEntriesArray(relatedEntries, $"{entryContext}.relatedEntries", knownCodexEntryIds, issues);
                }
            }

            if (doc.RootElement.TryGetProperty("loreCodexUpdates", out var updates) && updates.ValueKind == JsonValueKind.Array)
            {
                var updateIndex = 0;
                foreach (var item in updates.EnumerateArray())
                {
                    var itemContext = $"lore/codex_entries.json.loreCodexUpdates[{updateIndex++}]";
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var command = GetFirstNonEmptyString(item, "command");
                    if (string.Equals(command, "add", StringComparison.OrdinalIgnoreCase) &&
                        item.TryGetProperty("entry", out var entry) &&
                        entry.ValueKind == JsonValueKind.Object &&
                        entry.TryGetProperty("relatedEntries", out var addRelatedEntries) &&
                        addRelatedEntries.ValueKind == JsonValueKind.Array)
                    {
                        ValidateRelatedCodexEntriesArray(addRelatedEntries, $"{itemContext}.entry.relatedEntries", knownCodexEntryIds, issues);
                    }
                    else if (string.Equals(command, "update", StringComparison.OrdinalIgnoreCase) &&
                             item.TryGetProperty("updates", out var updatePayload) &&
                             updatePayload.ValueKind == JsonValueKind.Object &&
                             updatePayload.TryGetProperty("relatedEntries", out var updatedRelatedEntries) &&
                             updatedRelatedEntries.ValueKind == JsonValueKind.Array)
                    {
                        ValidateRelatedCodexEntriesArray(updatedRelatedEntries, $"{itemContext}.updates.relatedEntries", knownCodexEntryIds, issues);
                    }
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateCodexUpdateTargetCrossReferencesAsync(List<ValidationIssue> issues)
    {
        var knownExistingEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void CollectStoredEntriesOnly(JsonElement candidateRoot, HashSet<string> target)
        {
            if (!candidateRoot.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = GetFirstNonEmptyString(entry, "entryId");
                if (!string.IsNullOrWhiteSpace(entryId))
                    target.Add(entryId);
            }
        }

        static void CollectSameTurnAddEntries(JsonElement candidateRoot, HashSet<string> target)
        {
            if (!candidateRoot.TryGetProperty("loreCodexUpdates", out var updates) || updates.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in updates.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var command = GetFirstNonEmptyString(item, "command");
                if (!string.Equals(command, "add", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (item.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                {
                    var entryId = GetFirstNonEmptyString(entry, "entryId");
                    if (!string.IsNullOrWhiteSpace(entryId))
                        target.Add(entryId);
                }
            }
        }

        var preTurnJson = await ReadPreTurnTrackedFileAsync("lore/codex_entries.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var preTurnDoc = JsonDocument.Parse(preTurnJson);
                CollectStoredEntriesOnly(preTurnDoc.RootElement, knownExistingEntryIds);
            }
            catch
            {
                // ignored
            }
        }

        var currentJson = await _fs.ReadFileAsync("lore/codex_entries.json");
        if (string.IsNullOrWhiteSpace(currentJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(currentJson);
            CollectStoredEntriesOnly(doc.RootElement, knownExistingEntryIds);
            CollectSameTurnAddEntries(doc.RootElement, knownExistingEntryIds);

            if (!doc.RootElement.TryGetProperty("loreCodexUpdates", out var updates) || updates.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in updates.EnumerateArray())
            {
                var itemContext = $"lore/codex_entries.json.loreCodexUpdates[{index++}]";
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var command = GetFirstNonEmptyString(item, "command");
                if (!string.Equals(command, "update", StringComparison.OrdinalIgnoreCase))
                    continue;

                var entryId = GetFirstNonEmptyString(item, "entryId");
                if (string.IsNullOrWhiteSpace(entryId))
                    continue;

                if (!knownExistingEntryIds.Contains(entryId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.entryId",
                        IssueSeverity.Error,
                        $"loreCodexUpdates.update ссылается на entryId '{entryId}', которого нет в canonical codex state",
                        code: "codex_update_unknown_target_entry",
                        section: "Codex",
                        expected: "existing entryId from lore/codex_entries.json or same-turn add entryId",
                        actual: entryId,
                        repairHint: "Для existing lore entry используй реальный entryId из codex_entries.json. Если запись создаётся впервые, используй command=add с полным entry object."));
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private static void ValidateRelatedCodexEntriesArray(JsonElement relatedEntries, string context, HashSet<string> knownCodexEntryIds, List<ValidationIssue> issues)
    {
        var relatedIndex = 0;
        foreach (var relatedEntry in relatedEntries.EnumerateArray())
        {
            var relatedContext = $"{context}[{relatedIndex++}]";
            if (relatedEntry.ValueKind != JsonValueKind.String)
                continue;

            var relatedId = relatedEntry.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relatedId))
                continue;

            if (!knownCodexEntryIds.Contains(relatedId))
            {
                issues.Add(new ValidationIssue(
                    relatedContext,
                    IssueSeverity.Error,
                    $"Codex relatedEntries ссылается на entryId '{relatedId}', которого нет в lore/codex_entries.json",
                    code: "codex_related_entry_unknown_target",
                    section: "CrossReferences",
                    expected: "existing entryId from lore/codex_entries.json",
                    actual: relatedId,
                    repairHint: "Используй существующий entryId из codex_entries.json или сначала создай связанную запись в этом же accepted turn."));
            }
        }
    }


    private async Task ValidateNpcCommandCrossReferencesAsync(
        List<ValidationIssue> issues,
        (HashSet<string> Ids, HashSet<string> Names) knownNpcReferences,
        GuardianReferenceValidationState knownGuardianReferences)
    {
        var preTurnNpcReferences = await ReadPreTurnNpcReferencesAsync();
        var knownNpcCurrentActivities = await ReadKnownNpcCurrentActivitiesAsync();
        var guardianBoundaryUnavailable =
            knownGuardianReferences.Ids.Count == 0 &&
            knownGuardianReferences.Names.Count == 0 &&
            knownGuardianReferences.BaselineFailureKind != GuardianBaselineFailureKind.None;
        var emittedGuardianBaselineIssue = false;
        foreach (var (path, sections) in new[]
                 {
                     ("game_state/npcs/npc_inventory.json", new[] { "NPCInventoryAdds", "NPCInventoryUpdates", "NPCInventoryRemovals", "NPCEquipmentChanges", "NPCInventoryResourcesChanges" }),
                     ("game_state/npcs/npc_goals.json", new[] { "NPCGoalUpdates", "NPCQuestUpdates" }),
                     ("game_state/npcs/npc_activities.json", new[] { "NPCActivityUpdates", "completeNPCActivities" })
                 })
        {
            var json = await _fs.ReadFileAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var section in sections)
                {
                    if (!doc.RootElement.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    if (guardianBoundaryUnavailable &&
                        arr.GetArrayLength() > 0 &&
                        !emittedGuardianBaselineIssue)
                    {
                        issues.Add(new ValidationIssue(
                            $"{path}.{section}",
                            IssueSeverity.Error,
                            "Guardian/NPC command boundary validation требует kernel-backed validated pre-turn guardians baseline и не может silently disappear при broken guardian provenance.",
                            code: "guardian_npc_command_crossrefs_missing_validated_preturn_guardians_snapshot",
                            section: "Guardians",
                            expected: "validated pre-turn guardians baseline for guardian/NPC collision checks",
                            actual: knownGuardianReferences.BaselineFailureDescription,
                            repairHint: "Сохраняй readable validated snapshot copy game_state/meta/guardians.json, чтобы guardian ids и names не могли silently попадать в NPC command surfaces."));
                        emittedGuardianBaselineIssue = true;
                    }

                    var index = 0;
                    foreach (var item in arr.EnumerateArray())
                    {
                        var itemContext = $"{path}.{section}[{index}]";
                        var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
                        if (!string.IsNullOrWhiteSpace(npcId) && knownGuardianReferences.Ids.Contains(npcId))
                        {
                            issues.Add(new ValidationIssue(
                                itemContext,
                                IssueSeverity.Error,
                                "Guardians не должны попадать в NPC surfaces",
                                code: "guardian_leaked_into_npc_surface",
                                section: "Guardians",
                                expected: "Guardians only in UpdateGuardians / guardians.json",
                                actual: $"guardianId collision: {npcId}",
                                repairHint: "Не используй NPCInventory/NPCGoal/NPCQuest surfaces для Хранителей. Перенеси сущность в UpdateGuardians / game_state/meta/guardians.json."));
                            index++;
                            continue;
                        }

                        if (!NpcReferenceExists(item, knownNpcReferences))
                        {
                            var sectionName = string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase)
                                ? "NPCInventory"
                                : string.Equals(path, "game_state/npcs/npc_goals.json", StringComparison.OrdinalIgnoreCase)
                                    ? "NPCGoals"
                                    : "NPCActivities";
                            var actorReference = GetFirstNonEmptyString(item, "NPCId", "npcId", "id", "NPCName", "npcName", "name");
                            issues.Add(new ValidationIssue(
                                itemContext,
                                IssueSeverity.Error,
                                "NPC command ссылается на NPC, которого нет в canonical npc_core state",
                                code: "npc_command_unknown_npc_reference",
                                section: sectionName,
                                expected: "existing NPC reference from pre-turn npc_core or a same-turn NPC already created through UpdateNPCs",
                                actual: string.IsNullOrWhiteSpace(actorReference) ? "unknown NPC reference" : actorReference,
                                repairHint: "Ссылайся только на NPC, который уже существует в canonical npc_core state. Если NPC создаётся в этом же ходу, сначала создай его через UpdateNPCs, а затем используй его корректный permanent reference."));
                        }
                        else if ((preTurnNpcReferences.Ids.Count > 0 || preTurnNpcReferences.Names.Count > 0) &&
                                 (string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(path, "game_state/npcs/npc_activities.json", StringComparison.OrdinalIgnoreCase)) &&
                                 !NpcReferenceExists(item, preTurnNpcReferences))
                        {
                            issues.Add(new ValidationIssue(
                                itemContext,
                                IssueSeverity.Error,
                                string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase)
                                    ? "Новый NPC не должен изменяться через atomic NPCInventory* surfaces в тот же ход создания"
                                    : "Новый NPC не должен изменяться через atomic NPC activity surfaces в тот же ход создания",
                                code: string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase)
                                    ? "npc_new_inventory_atomic_split_forbidden"
                                    : "npc_new_activity_atomic_split_forbidden",
                                section: string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase)
                                    ? "NPCInventory"
                                    : "NPCActivities",
                                repairHint: string.Equals(path, "game_state/npcs/npc_inventory.json", StringComparison.OrdinalIgnoreCase)
                                    ? "Для newly created NPC задай полный initial inventory только внутри UpdateNPCs.inventory. NPCInventoryAdds/Updates/Removals/Equipment/Resources используй только для уже существующих NPC."
                                    : "Для newly created NPC не используй NPCActivityUpdates/completeNPCActivities в тот же ход создания. Новый NPC должен начинать с currentActivity = null; дальнейшие activity changes применяй только к уже существующему NPC."));
                        }

                        if (string.Equals(path, "game_state/npcs/npc_activities.json", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(section, "NPCActivityUpdates", StringComparison.OrdinalIgnoreCase) &&
                            NpcReferenceExists(item, knownNpcReferences))
                        {
                            var knownActivityNames = ResolveKnownNpcActivityNames(item, knownNpcCurrentActivities);
                            if (knownActivityNames == null || knownActivityNames.Count == 0)
                            {
                                issues.Add(new ValidationIssue(
                                    $"{itemContext}.activityUpdate",
                                    IssueSeverity.Error,
                                    "NPCActivityUpdates ссылается на NPC, у которого нет активной currentActivity в canonical npc_core state",
                                    code: "npc_activity_update_without_active_current_activity",
                                    section: "NPCActivities",
                                    expected: "existing NPC with non-null currentActivity before non-terminal activity update",
                                    actual: "missing active currentActivity",
                                    repairHint: "Используй NPCActivityUpdates только для already existing NPC с ненулевой currentActivity в canonical npc_core state. Newly created same-turn NPC должен стартовать с currentActivity = null, а terminal completion оформляй через completeNPCActivities."));
                            }
                        }

                        if (string.Equals(path, "game_state/npcs/npc_activities.json", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(section, "completeNPCActivities", StringComparison.OrdinalIgnoreCase) &&
                            NpcReferenceExists(item, knownNpcReferences))
                        {
                            var activityName = GetFirstNonEmptyString(item, "activityName");
                            if (!string.IsNullOrWhiteSpace(activityName))
                            {
                                var knownActivityNames = ResolveKnownNpcActivityNames(item, knownNpcCurrentActivities);
                                if (knownActivityNames == null || knownActivityNames.Count == 0)
                                {
                                    issues.Add(new ValidationIssue(
                                        $"{itemContext}.activityName",
                                        IssueSeverity.Error,
                                        "completeNPCActivities ссылается на NPC, у которого нет активной currentActivity в canonical npc_core state",
                                        code: "npc_complete_activity_without_active_current_activity",
                                        section: "NPCActivities",
                                        expected: "existing NPC with non-null currentActivity before completion",
                                        actual: activityName,
                                        repairHint: "Используй completeNPCActivities только для NPC, у которого уже есть active currentActivity в canonical npc_core state. Если активность ещё не была создана, сначала задай/обнови её корректным non-terminal flow."));
                                }
                                else if (!knownActivityNames.Contains(activityName))
                                {
                                    issues.Add(new ValidationIssue(
                                        $"{itemContext}.activityName",
                                        IssueSeverity.Error,
                                        "completeNPCActivities.activityName не совпадает с активной currentActivity целевого NPC",
                                        code: "npc_complete_activity_name_mismatch",
                                        section: "NPCActivities",
                                        expected: string.Join(" | ", knownActivityNames),
                                        actual: activityName,
                                        repairHint: "Завершай через completeNPCActivities именно ту activityName, которая сейчас стоит в canonical npc_core.currentActivity у этого NPC. Если меняешь активность, сначала обнови её корректным non-terminal способом."));
                                }
                            }
                        }

                        index++;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }


    private static HashSet<string>? ResolveKnownNpcActivityNames(
        JsonElement item,
        (Dictionary<string, HashSet<string>> ById, Dictionary<string, HashSet<string>> ByName) knownNpcCurrentActivities)
    {
        var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
        if (!string.IsNullOrWhiteSpace(npcId) &&
            knownNpcCurrentActivities.ById.TryGetValue(npcId, out var byId))
            return byId;

        var npcName = GetFirstNonEmptyString(item, "NPCName", "npcName", "name");
        if (!string.IsNullOrWhiteSpace(npcName) &&
            knownNpcCurrentActivities.ByName.TryGetValue(npcName, out var byName))
            return byName;

        return null;
    }


    private async Task ValidateWorldStateFlagCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownFlagIds)
    {
        if (knownFlagIds.Count == 0)
            return;

        var json = await _fs.ReadFileAsync("game_state/world/world_flags.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("removeWorldStateFlags", out var removals) || removals.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var flagIdNode in removals.EnumerateArray())
            {
                if (flagIdNode.ValueKind != JsonValueKind.String)
                {
                    index++;
                    continue;
                }

                var flagId = flagIdNode.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(flagId) && !knownFlagIds.Contains(flagId))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/world/world_flags.json.removeWorldStateFlags[{index}]",
                        IssueSeverity.Error,
                        $"removeWorldStateFlags ссылается на неизвестный flagId '{flagId}'",
                        code: "world_state_flag_remove_unknown_target",
                        section: "WorldStateFlags",
                        expected: "existing flagId from pre-turn/current worldStateFlags",
                        actual: flagId,
                        repairHint: "Удаляй только реально существующий flagId из Context.worldStateFlags. Не придумывай новый id в removeWorldStateFlags."));
                }

                index++;
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateVehicleCrossReferencesAsync(
        List<ValidationIssue> issues,
        HashSet<string> knownVehicleIds,
        HashSet<string> knownLocationIds)
    {
        var json = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var resolvableVehicleIds = new HashSet<string>(knownVehicleIds, StringComparer.OrdinalIgnoreCase);
            var sameTurnRemovedVehicleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("UpdateVehicles", out var updates) && updates.ValueKind == JsonValueKind.Array)
            {
                var updateIndex = 0;
                foreach (var vehicle in updates.EnumerateArray())
                {
                    var updateContext = $"game_state/misc/vehicles.json.UpdateVehicles[{updateIndex++}]";
                    if (vehicle.ValueKind != JsonValueKind.Object)
                        continue;

                    var vehicleId = GetFirstNonEmptyString(vehicle, "vehicleId");
                    if (string.IsNullOrWhiteSpace(vehicleId) || knownVehicleIds.Contains(vehicleId))
                        continue;

                    var missingFields = GetMissingVehicleFullObjectFields(vehicle);
                    if (missingFields.Count > 0)
                    {
                        issues.Add(new ValidationIssue(
                            updateContext,
                            IssueSeverity.Error,
                            "UpdateVehicles с новым preassigned vehicleId должен передавать полный Vehicle Object",
                            code: "vehicle_new_preassigned_id_requires_full_object",
                            section: "Vehicles",
                            expected: "Full Vehicle Object for brand-new vehicle with preassigned vehicleId",
                            actual: string.Join(", ", missingFields),
                            repairHint: "Если выдаёшь новый транспорт с уже заданным vehicleId, передай полный Vehicle Object по Block 10. Partial updates допустимы только для уже существующих vehicleId."));
                        continue;
                    }

                    resolvableVehicleIds.Add(vehicleId);
                }
            }

            if (doc.RootElement.TryGetProperty("removeVehicles", out var removals) && removals.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var vehicleIdNode in removals.EnumerateArray())
                {
                    if (vehicleIdNode.ValueKind != JsonValueKind.String)
                    {
                        index++;
                        continue;
                    }

                    var vehicleId = vehicleIdNode.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(vehicleId) && !resolvableVehicleIds.Contains(vehicleId))
                    {
                        issues.Add(new ValidationIssue(
                            $"game_state/misc/vehicles.json.removeVehicles[{index}]",
                            IssueSeverity.Error,
                            $"removeVehicles ссылается на неизвестный vehicleId '{vehicleId}'",
                            code: "vehicle_remove_unknown_target",
                            section: "Vehicles",
                            expected: "existing vehicleId from pre-turn/current vehicles state",
                            actual: vehicleId,
                            repairHint: "Удаляй только реально существующий vehicleId из canonical vehicles state."));
                    }

                    if (!string.IsNullOrWhiteSpace(vehicleId))
                        sameTurnRemovedVehicleIds.Add(vehicleId);

                    index++;
                }
            }

            resolvableVehicleIds.ExceptWith(sameTurnRemovedVehicleIds);

            if (doc.RootElement.TryGetProperty("activeVehicleChange", out var activeVehicle) &&
                activeVehicle.ValueKind == JsonValueKind.String)
            {
                var vehicleId = activeVehicle.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(vehicleId) && sameTurnRemovedVehicleIds.Contains(vehicleId))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/misc/vehicles.json.activeVehicleChange",
                        IssueSeverity.Error,
                        $"activeVehicleChange не может ссылаться на vehicleId '{vehicleId}', который удаляется в этом же accepted turn",
                        code: "vehicle_active_change_removed_same_turn",
                        section: "Vehicles",
                        expected: "null or surviving vehicleId after same-turn removeVehicles processing",
                        actual: vehicleId,
                        repairHint: "Если транспорт удаляется в removeVehicles, не назначай его active в этом же ходе. Для destroyed/sold active vehicle сбрось activeVehicleChange в null или выбери другой surviving vehicleId."));
                }
                else
                if (!string.IsNullOrWhiteSpace(vehicleId) && !resolvableVehicleIds.Contains(vehicleId))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/misc/vehicles.json.activeVehicleChange",
                        IssueSeverity.Error,
                        $"activeVehicleChange ссылается на неизвестный vehicleId '{vehicleId}'",
                        code: "vehicle_active_change_unknown_target",
                        section: "Vehicles",
                        expected: "existing vehicleId from pre-turn/current vehicles state",
                        actual: vehicleId,
                        repairHint: "Для activeVehicleChange используй существующий vehicleId из canonical vehicles state или сначала создай новый транспорт в UpdateVehicles."));
                }
            }

            foreach (var vehicleArraySpec in new[]
                     {
                         ("UpdateVehicles", "game_state/misc/vehicles.json.UpdateVehicles"),
                         ("vehicles", "game_state/misc/vehicles.json.vehicles")
                     })
            {
                if (!doc.RootElement.TryGetProperty(vehicleArraySpec.Item1, out var vehicles) || vehicles.ValueKind != JsonValueKind.Array)
                    continue;

                var vehicleIndex = 0;
                foreach (var vehicle in vehicles.EnumerateArray())
                {
                    var vehicleContext = $"{vehicleArraySpec.Item2}[{vehicleIndex++}]";
                    if (vehicle.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!vehicle.TryGetProperty("currentLocationId", out var currentLocationNode) ||
                        currentLocationNode.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var currentLocationId = currentLocationNode.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(currentLocationId) || knownLocationIds.Contains(currentLocationId))
                        continue;

                    issues.Add(new ValidationIssue(
                        $"{vehicleContext}.currentLocationId",
                        IssueSeverity.Error,
                        $"Vehicle currentLocationId '{currentLocationId}' не найден среди известных локаций",
                        code: "vehicle_unknown_current_location",
                        section: "Vehicles",
                        expected: "existing locationId from canonical world state or null",
                        actual: currentLocationId,
                        repairHint: "Для parked vehicle указывай существующий locationId из canonical world state. Для availability=Active/Pocket оставляй currentLocationId = null."));
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private static List<string> GetMissingVehicleFullObjectFields(JsonElement vehicle)
    {
        var missingFields = new List<string>();
        foreach (var stringField in new[] { "name", "description", "image_prompt", "type", "availability", "maxHealth", "currentHealth" })
        {
            if (!HasNonEmptyString(vehicle, stringField))
                missingFields.Add(stringField);
        }

        foreach (var presentField in new[] { "isSentient", "currentLocationId", "speedBonus", "actions", "resistances", "inventory" })
        {
            if (!vehicle.TryGetProperty(presentField, out _))
                missingFields.Add(presentField);
        }

        return missingFields;
    }


    private async Task<(HashSet<string> Ids, HashSet<string> Names)> ReadKnownInventoryItemReferencesAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnInventoryItemIds = await ReadPreTurnInventoryItemIdsAsync();
        RegisterKnownInventoryItemReferencesFromJson(
            await ReadPreTurnTrackedFileAsync("game_state/inventory/items.json"),
            ids,
            names,
            knownExistingItemIds: null,
            currentStateNewItemsOnly: false);
        RegisterKnownInventoryItemReferencesFromJson(
            await _fs.ReadFileAsync("game_state/inventory/items.json"),
            ids,
            names,
            preTurnInventoryItemIds,
            currentStateNewItemsOnly: true);

        return (ids, names);
    }


    private async Task ValidateInventoryItemSidecarCrossReferencesAsync(
        List<ValidationIssue> issues,
        (HashSet<string> Ids, HashSet<string> Names) inventoryRefs,
        (HashSet<string> Ids, HashSet<string> Names) npcInventoryRefs)
    {
        foreach (var (path, collections) in new[]
                 {
                     ("game_state/inventory/item_resources.json", new[] { "entries", "inventoryItemsResources" }),
                     ("game_state/inventory/item_bonds.json", new[] { "entries", "itemBondLevelChanges", "itemFateCardUnlocks" }),
                     ("game_state/npcs/item_journals.json", new[] { "entries", "itemJournals", "itemJournalUpdates" })
                 })
        {
            var json = await _fs.ReadFileAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var collection in collections)
                {
                    if (!doc.RootElement.TryGetProperty(collection, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    var index = 0;
                    foreach (var item in arr.EnumerateArray())
                    {
                        var allowNpcInventoryRefs = string.Equals(path, "game_state/npcs/item_journals.json", StringComparison.OrdinalIgnoreCase);
                        var referenceExists = InventorySidecarReferenceExists(item, inventoryRefs) ||
                                              (allowNpcInventoryRefs && InventorySidecarReferenceExists(item, npcInventoryRefs));
                        if (!referenceExists)
                        {
                            var referenceSummary = DescribeInventoryReference(item);
                            issues.Add(new ValidationIssue(
                                $"{path}.{collection}[{index}]",
                                IssueSeverity.Error,
                                allowNpcInventoryRefs
                                    ? "Item journal entry ссылается на предмет, которого нет ни в inventory/items.json, ни в npc_inventory.json"
                                    : "Item sidecar entry ссылается на предмет, которого нет в inventory/items.json",
                                code: allowNpcInventoryRefs
                                    ? "item_journal_unknown_item_reference"
                                    : "item_sidecar_unknown_item_reference",
                                section: allowNpcInventoryRefs ? "ItemJournals" : "ItemSidecars",
                                expected: allowNpcInventoryRefs
                                    ? "Existing itemId/itemName from inventory/items.json or game_state/npcs/npc_inventory.json"
                                    : "Existing itemId/itemName from inventory/items.json",
                                actual: referenceSummary,
                                repairHint: allowNpcInventoryRefs
                                    ? "Сошлись на реальный предмет из inventory/items.json или npc_inventory.json. Если предмет создаётся в этом же accepted turn, сначала запиши его canonical inventory state, а потом journal entry."
                                    : "Сошлись на реальный предмет из inventory/items.json. Не создавай orphan sidecar entry без соответствующего inventory item."));
                        }
                        index++;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }


    private async Task ValidatePlayerInventoryCrossReferencesAsync(
        List<ValidationIssue> issues,
        (HashSet<string> Ids, HashSet<string> Names) inventoryRefs)
    {
        var preTurnInventoryItemIds = await ReadPreTurnInventoryItemIdsAsync();
        var preTurnInventoryItemFateCards = await ReadPreTurnInventoryItemFateCardsAsync();
        var preTurnInventoryItemBondLevels = await ReadPreTurnInventoryItemBondLevelsAsync();
        var inventoryItemFateCards = await ReadCurrentInventoryItemFateCardsAsync();
        var inventoryItemBondLevels = await ReadCurrentInventoryItemBondLevelsAsync();
        var inventoryFullObjectCoverage = await ReadCurrentInventoryFullObjectCoverageByItemIdAsync();
        var reportedItemBondLevels = await ReadCurrentItemBondLevelChangesAsync();
        var reportedFateCardUnlocks = await ReadCurrentItemFateCardUnlockEventsAsync();
        var preTurnItemBondStateBondLevels = await ReadPreTurnItemBondStateBondLevelsAsync();
        var currentItemBondStateBondLevels = await ReadCurrentItemBondStateBondLevelsAsync();
        var preTurnItemBondStateFateCards = await ReadPreTurnItemBondStateFateCardsAsync();
        var currentItemBondStateFateCards = await ReadCurrentItemBondStateFateCardsAsync();
        foreach (var (path, collections, section, message, code, repairHint) in new[]
                 {
                     ("game_state/inventory/item_text_updates.json", new[] { "entries", "updateItemTextContents" }, "Inventory", "updateItemTextContents ссылается на предмет, которого нет в inventory/items.json", "inventory_text_update_unknown_item_reference", "Сошлись на существующий itemId/itemName из inventory/items.json. Не создавай orphan text update без соответствующего предмета."),
                     ("game_state/inventory/item_movements.json", new[] { "moveInventoryItems" }, "Inventory", "moveInventoryItems ссылается на предмет, которого нет в inventory/items.json", "inventory_move_unknown_item_reference", "Сошлись на существующий movedItemId/itemName из inventory/items.json. Сначала создай предмет в UpdateInventory, потом перемещай его."),
                     ("game_state/inventory/item_removals.json", new[] { "removeInventoryItems" }, "Inventory", "removeInventoryItems ссылается на предмет, которого нет в inventory/items.json", "inventory_remove_unknown_item_reference", "Сошлись на существующий removedItemId/itemName из inventory/items.json. Не удаляй несуществующий стек."),
                     ("game_state/inventory/item_bonds.json", new[] { "itemFateCardUnlocks" }, "Inventory", "itemFateCardUnlocks ссылается на предмет, которого нет в inventory/items.json", "inventory_fate_card_unknown_item_reference", "Сошлись на существующий itemId/itemName из inventory/items.json и синхронно обнови состояние самого предмета.")
                 })
        {
            var json = await _fs.ReadFileAsync(path);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var collection in collections)
                {
                    if (!doc.RootElement.TryGetProperty(collection, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    var index = 0;
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            index++;
                            continue;
                        }

                        if (!InventoryReferenceExists(item, inventoryRefs))
                        {
                            issues.Add(new ValidationIssue(
                                $"{path}.{collection}[{index}]",
                                IssueSeverity.Error,
                                message,
                                code: code,
                                section: section,
                                expected: "Existing itemId/itemName from inventory/items.json",
                                actual: DescribeInventoryReference(item),
                                repairHint: repairHint));
                        }
                        else if (string.Equals(path, "game_state/inventory/item_bonds.json", StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(collection, "itemFateCardUnlocks", StringComparison.OrdinalIgnoreCase))
                        {
                            var itemId = GetFirstNonEmptyString(item, "itemId");
                            var cardId = GetFirstNonEmptyString(item, "cardId");
                            if (!string.IsNullOrWhiteSpace(itemId) &&
                                !string.IsNullOrWhiteSpace(cardId) &&
                                (!inventoryItemFateCards.TryGetValue(itemId, out var unlockedCards) || !unlockedCards.Contains(cardId)))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{path}.{collection}[{index}]",
                                    IssueSeverity.Error,
                                    "itemFateCardUnlocks должен сопровождаться обновлённым item state с этим unlocked fate card в inventory/items.json",
                                    code: "item_fate_card_unlock_missing_inventory_state_sync",
                                    section: "Inventory",
                                    expected: $"inventory/items.json item {itemId} contains unlocked fateCard {cardId}",
                                    actual: "matching unlocked fateCard not found in current item state",
                                    repairHint: "Для fate card unlock передай не только event в itemFateCardUnlocks, но и обновлённый полный item object в UpdateInventory с уже unlocked card."));
                            }

                            if (!string.IsNullOrWhiteSpace(itemId) &&
                                !string.IsNullOrWhiteSpace(cardId) &&
                                preTurnInventoryItemFateCards.TryGetValue(itemId, out var preTurnUnlockedCards) &&
                                preTurnUnlockedCards.Contains(cardId))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{path}.{collection}[{index}]",
                                    IssueSeverity.Error,
                                    "itemFateCardUnlocks не должен повторно сигналить уже открытую Fate Card",
                                    code: "item_fate_card_unlock_already_unlocked_pre_turn",
                                    section: "Inventory",
                                    expected: "new unlock relative to pre-turn item state",
                                    actual: $"item {itemId} already had unlocked fateCard {cardId}",
                                    repairHint: "Сообщай в itemFateCardUnlocks только новые Fate Card unlock события текущего хода. Если карта уже была открыта до хода, обновляй только item state без повторного unlock event."));
                            }

                            if (!string.IsNullOrWhiteSpace(itemId) &&
                                (!inventoryFullObjectCoverage.TryGetValue(itemId, out var hasFullObjectCoverage) || !hasFullObjectCoverage))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{path}.{collection}[{index}]",
                                    IssueSeverity.Error,
                                    "itemFateCardUnlocks не должен сопровождаться только partial UpdateInventory patch",
                                    code: "item_fate_card_unlock_requires_full_item_object",
                                    section: "Inventory",
                                    expected: $"Full updated Item Object for item {itemId} in game_state/inventory/items.json",
                                    actual: "matching full item object not found in current inventory payload",
                                    repairHint: "При Fate Card unlock передай полное обновлённое состояние предмета в UpdateInventory/items.json. Partial delta existing item с одним fateCards fragment здесь недостаточен."));
                            }
                        }

                        index++;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        foreach (var (itemId, currentBondLevel) in inventoryItemBondLevels)
        {
            if (!preTurnInventoryItemIds.Contains(itemId) ||
                !preTurnInventoryItemBondLevels.TryGetValue(itemId, out var preTurnBondLevel) ||
                preTurnBondLevel == currentBondLevel)
            {
                continue;
            }

            if (reportedItemBondLevels.TryGetValue(itemId, out var reportedBondLevel) &&
                reportedBondLevel == currentBondLevel)
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                $"game_state/inventory/items.json.item:{itemId}.ownerBondLevelCurrent",
                IssueSeverity.Error,
                "Изменение ownerBondLevelCurrent у существующего предмета должно сопровождаться itemBondLevelChanges",
                code: "inventory_bond_level_change_missing_sidecar_event",
                section: "Inventory",
                expected: $"itemBondLevelChanges entry for item {itemId} with newBondLevel {currentBondLevel}",
                actual: $"existing item bond changed from {preTurnBondLevel} to {currentBondLevel} without matching itemBondLevelChanges",
                repairHint: "Если у уже существующего Rare+ предмета изменился ownerBondLevelCurrent, оставь resulting item state в UpdateInventory и отдельно добавь itemBondLevelChanges с тем же newBondLevel."));
        }

        foreach (var (itemId, currentUnlockedCards) in inventoryItemFateCards)
        {
            if (!preTurnInventoryItemIds.Contains(itemId))
                continue;

            var preTurnUnlockedCards = preTurnInventoryItemFateCards.TryGetValue(itemId, out var existingUnlockedCards)
                ? existingUnlockedCards
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reportedUnlocks = reportedFateCardUnlocks.TryGetValue(itemId, out var currentReportedUnlocks)
                ? currentReportedUnlocks
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cardId in currentUnlockedCards)
            {
                if (preTurnUnlockedCards.Contains(cardId) || reportedUnlocks.Contains(cardId))
                    continue;

                issues.Add(new ValidationIssue(
                    $"game_state/inventory/items.json.item:{itemId}.fateCards",
                    IssueSeverity.Error,
                    "Новый unlocked Fate Card у существующего предмета должен сопровождаться itemFateCardUnlocks",
                    code: "inventory_fate_card_unlock_missing_sidecar_event",
                    section: "Inventory",
                    expected: $"itemFateCardUnlocks entry for item {itemId} and card {cardId}",
                    actual: $"existing item state contains newly unlocked fateCard {cardId} without matching itemFateCardUnlocks",
                    repairHint: "Если у уже существующего предмета в этом ходе unlock'нулась Fate Card, сохрани updated full item state в UpdateInventory и отдельно добавь event в itemFateCardUnlocks."));
            }
        }

        foreach (var (itemId, currentBondLevel) in currentItemBondStateBondLevels)
        {
            if (!preTurnItemBondStateBondLevels.TryGetValue(itemId, out var preTurnBondLevel) ||
                preTurnBondLevel == currentBondLevel)
            {
                continue;
            }

            if (reportedItemBondLevels.TryGetValue(itemId, out var reportedBondLevel) &&
                reportedBondLevel == currentBondLevel)
            {
                continue;
            }

            var inventoryStateAlreadyCarriesThisChange =
                preTurnInventoryItemBondLevels.TryGetValue(itemId, out var preTurnInventoryBondLevel) &&
                inventoryItemBondLevels.TryGetValue(itemId, out var currentInventoryBondLevel) &&
                preTurnInventoryBondLevel != currentInventoryBondLevel;
            if (inventoryStateAlreadyCarriesThisChange)
                continue;

            issues.Add(new ValidationIssue(
                $"game_state/inventory/item_bonds.json.entries.item:{itemId}.ownerBondLevelCurrent",
                IssueSeverity.Error,
                "Изменение ownerBondLevelCurrent в canonical item_bonds entry должно сопровождаться itemBondLevelChanges",
                code: "item_bond_state_change_missing_sidecar_event",
                section: "Inventory",
                expected: $"itemBondLevelChanges entry for item {itemId} with newBondLevel {currentBondLevel}",
                actual: $"item_bonds entry changed from {preTurnBondLevel} to {currentBondLevel} without matching itemBondLevelChanges",
                repairHint: "Если меняешь ownerBondLevelCurrent у уже существующего item_bonds entry, передай matching event в itemBondLevelChanges. Не мутируй canonical item_bonds state молча."));
        }

        foreach (var (itemId, currentUnlockedCards) in currentItemBondStateFateCards)
        {
            if (!preTurnItemBondStateFateCards.TryGetValue(itemId, out var preTurnUnlockedCards))
                continue;

            var reportedUnlocks = reportedFateCardUnlocks.TryGetValue(itemId, out var currentReportedUnlocks)
                ? currentReportedUnlocks
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cardId in currentUnlockedCards)
            {
                if (preTurnUnlockedCards.Contains(cardId) || reportedUnlocks.Contains(cardId))
                    continue;

                var inventoryStateAlreadyCarriesThisUnlock =
                    inventoryItemFateCards.TryGetValue(itemId, out var currentInventoryUnlockedCards) &&
                    currentInventoryUnlockedCards.Contains(cardId) &&
                    (!preTurnInventoryItemFateCards.TryGetValue(itemId, out var preTurnInventoryUnlockedCards) ||
                     !preTurnInventoryUnlockedCards.Contains(cardId));
                if (inventoryStateAlreadyCarriesThisUnlock)
                    continue;

                issues.Add(new ValidationIssue(
                    $"game_state/inventory/item_bonds.json.entries.item:{itemId}.fateCards",
                    IssueSeverity.Error,
                    "Новый unlocked Fate Card в canonical item_bonds entry должен сопровождаться itemFateCardUnlocks",
                    code: "item_bond_state_fate_card_unlock_missing_event",
                    section: "Inventory",
                    expected: $"itemFateCardUnlocks entry for item {itemId} and card {cardId}",
                    actual: $"item_bonds entry contains newly unlocked fateCard {cardId} without matching itemFateCardUnlocks",
                    repairHint: "Если в canonical item_bonds entry появляется новая Fate Card, передай matching unlock event в itemFateCardUnlocks. Не мутируй Fate Card unlock state молча."));
            }
        }
    }


    private async Task<HashSet<string>> ReadPreTurnInventoryItemIdsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync("game_state/inventory/items.json");
        return ReadInventoryItemIdsFromJson(json);
    }


    private HashSet<string> ReadPreTurnInventoryItemIdsSync()
    {
        var json = ReadPreTurnTrackedFileSync("game_state/inventory/items.json");
        return ReadInventoryItemIdsFromJson(json);
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadCurrentInventoryItemFateCardsAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var preTurnInventoryItemIds = await ReadPreTurnInventoryItemIdsAsync();
        return ReadInventoryItemFateCardsFromJson(json, preTurnInventoryItemIds, currentStateNewItemsOnly: true);
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadPreTurnInventoryItemFateCardsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync("game_state/inventory/items.json");
        return ReadInventoryItemFateCardsFromJson(json, knownExistingItemIds: null, currentStateNewItemsOnly: false);
    }


    private async Task<Dictionary<string, int>> ReadCurrentInventoryItemBondLevelsAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var preTurnInventoryItemIds = await ReadPreTurnInventoryItemIdsAsync();
        return ReadInventoryItemBondLevelsFromJson(json, preTurnInventoryItemIds, currentStateNewItemsOnly: true);
    }


    private async Task<Dictionary<string, int>> ReadPreTurnInventoryItemBondLevelsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync("game_state/inventory/items.json");
        return ReadInventoryItemBondLevelsFromJson(json, knownExistingItemIds: null, currentStateNewItemsOnly: false);
    }


    private async Task<Dictionary<string, int>> ReadCurrentItemBondLevelChangesAsync()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var json = await _fs.ReadFileAsync("game_state/inventory/item_bonds.json");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("itemBondLevelChanges", out var changes) || changes.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in changes.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemId = GetFirstNonEmptyString(item, "itemId");
                if (string.IsNullOrWhiteSpace(itemId) || !TryReadInt(item, "newBondLevel", out var bondLevel))
                    continue;

                result[itemId] = bondLevel;
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private async Task<Dictionary<string, int>> ReadCurrentItemBondStateBondLevelsAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/inventory/item_bonds.json");
        return ReadItemBondStateBondLevelsFromJson(json);
    }


    private async Task<Dictionary<string, int>> ReadPreTurnItemBondStateBondLevelsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync("game_state/inventory/item_bonds.json");
        return ReadItemBondStateBondLevelsFromJson(json);
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadCurrentItemFateCardUnlockEventsAsync()
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var json = await _fs.ReadFileAsync("game_state/inventory/item_bonds.json");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("itemFateCardUnlocks", out var unlocks) || unlocks.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in unlocks.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemId = GetFirstNonEmptyString(item, "itemId");
                var cardId = GetFirstNonEmptyString(item, "cardId");
                if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(cardId))
                    continue;

                if (!result.TryGetValue(itemId, out var cardIds))
                {
                    cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[itemId] = cardIds;
                }

                cardIds.Add(cardId);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadCurrentItemBondStateFateCardsAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/inventory/item_bonds.json");
        return ReadItemBondStateFateCardsFromJson(json);
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadPreTurnItemBondStateFateCardsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync("game_state/inventory/item_bonds.json");
        return ReadItemBondStateFateCardsFromJson(json);
    }


    private async Task<Dictionary<string, bool>> ReadCurrentInventoryFullObjectCoverageByItemIdAsync()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var json = await _fs.ReadFileAsync("game_state/inventory/items.json");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        var preTurnInventoryItemIds = await ReadPreTurnInventoryItemIdsAsync();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) &&
                !doc.RootElement.TryGetProperty("UpdateInventory", out items))
            {
                return result;
            }

            if (items.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemId = GetInventoryReferenceCandidateId(item, currentStateNewItemsOnly: true);
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (!ShouldAcceptInventoryReferenceCandidate(item, preTurnInventoryItemIds, currentStateNewItemsOnly: true))
                    continue;

                var isFullObject = IsLikelyFullInventoryItemObject(item);
                if (!result.TryGetValue(itemId, out var currentCoverage) || (!currentCoverage && isFullObject))
                    result[itemId] = isFullObject;
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static HashSet<string> ReadInventoryItemIdsFromJson(string? json)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) &&
                !doc.RootElement.TryGetProperty("UpdateInventory", out items))
            {
                return result;
            }

            if (items.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in items.EnumerateArray())
            {
                var itemId = GetFirstNonEmptyString(item, "existedId", "itemId", "id");
                if (!string.IsNullOrWhiteSpace(itemId))
                    result.Add(itemId);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static Dictionary<string, int> ReadInventoryItemBondLevelsFromJson(
        string? json,
        HashSet<string>? knownExistingItemIds,
        bool currentStateNewItemsOnly)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) &&
                !doc.RootElement.TryGetProperty("UpdateInventory", out items))
            {
                return result;
            }

            if (items.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemId = GetInventoryReferenceCandidateId(item, currentStateNewItemsOnly);
                if (string.IsNullOrWhiteSpace(itemId) || !TryReadInt(item, "ownerBondLevelCurrent", out var bondLevel))
                    continue;

                if (!ShouldAcceptInventoryReferenceCandidate(item, knownExistingItemIds, currentStateNewItemsOnly))
                    continue;

                result[itemId] = bondLevel;
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static Dictionary<string, HashSet<string>> ReadInventoryItemFateCardsFromJson(
        string? json,
        HashSet<string>? knownExistingItemIds,
        bool currentStateNewItemsOnly)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) &&
                !doc.RootElement.TryGetProperty("UpdateInventory", out items))
            {
                return result;
            }

            if (items.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemId = GetInventoryReferenceCandidateId(item, currentStateNewItemsOnly);
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !item.TryGetProperty("fateCards", out var fateCards) ||
                    fateCards.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (!ShouldAcceptInventoryReferenceCandidate(item, knownExistingItemIds, currentStateNewItemsOnly))
                    continue;

                if (!result.TryGetValue(itemId, out var cardIds))
                {
                    cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[itemId] = cardIds;
                }

                foreach (var fateCard in fateCards.EnumerateArray())
                {
                    var cardId = GetFirstNonEmptyString(fateCard, "cardId");
                    var isUnlocked = fateCard.TryGetProperty("isUnlocked", out var isUnlockedNode) && isUnlockedNode.ValueKind == JsonValueKind.True;
                    if (!string.IsNullOrWhiteSpace(cardId) && isUnlocked)
                        cardIds.Add(cardId);
                }
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static Dictionary<string, int> ReadItemBondStateBondLevelsFromJson(string? json)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entry in entries.EnumerateArray())
            {
                var itemId = GetFirstNonEmptyString(entry, "itemId", "existedId", "id");
                if (string.IsNullOrWhiteSpace(itemId) || !TryReadInt(entry, "ownerBondLevelCurrent", out var bondLevel))
                    continue;

                result[itemId] = bondLevel;
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static Dictionary<string, HashSet<string>> ReadItemBondStateFateCardsFromJson(string? json)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entry in entries.EnumerateArray())
            {
                var itemId = GetFirstNonEmptyString(entry, "itemId", "existedId", "id");
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !entry.TryGetProperty("fateCards", out var fateCards) ||
                    fateCards.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (!result.TryGetValue(itemId, out var cardIds))
                {
                    cardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[itemId] = cardIds;
                }

                foreach (var fateCard in fateCards.EnumerateArray())
                {
                    var cardId = GetFirstNonEmptyString(fateCard, "cardId");
                    var isUnlocked = fateCard.TryGetProperty("isUnlocked", out var isUnlockedNode) && isUnlockedNode.ValueKind == JsonValueKind.True;
                    if (!string.IsNullOrWhiteSpace(cardId) && isUnlocked)
                        cardIds.Add(cardId);
                }
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


	    private async Task<(HashSet<string> Ids, HashSet<string> Names)> ReadKnownNpcInventoryItemReferencesAsync()
	    {
	        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_inventory.json"),
                     await _fs.ReadFileAsync("game_state/npcs/npc_inventory.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);

	                if (doc.RootElement.TryGetProperty("NPCInventoryAdds", out var adds) && adds.ValueKind == JsonValueKind.Array)
	                {
	                    foreach (var item in adds.EnumerateArray())
	                    {
	                        if (!item.TryGetProperty("item", out var inventoryItem) || inventoryItem.ValueKind != JsonValueKind.Object)
	                            continue;

	                        RegisterInventoryReference(inventoryItem, ids, names);
	                    }
	                }

	            }
	            catch
            {
                // ignored
            }
        }

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        if (!npc.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var inventoryItem in inventory.EnumerateArray())
                        {
                            if (inventoryItem.ValueKind != JsonValueKind.Object)
                                continue;

                            RegisterInventoryReference(inventoryItem, ids, names);
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return (ids, names);
    }


    private async Task<Dictionary<string, (HashSet<string> Ids, HashSet<string> Names)>> ReadKnownNpcInventoryItemReferencesByNpcAsync()
    {
        var refsByNpc = new Dictionary<string, (HashSet<string> Ids, HashSet<string> Names)>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        if (!npc.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
                            continue;

                        var aliases = GetNpcAliases(npc);
                        foreach (var inventoryItem in inventory.EnumerateArray())
                        {
                            if (inventoryItem.ValueKind != JsonValueKind.Object)
                                continue;

                            RegisterNpcScopedInventoryReference(inventoryItem, aliases, refsByNpc);
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_inventory.json"),
                     await _fs.ReadFileAsync("game_state/npcs/npc_inventory.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("NPCInventoryAdds", out var adds) || adds.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in adds.EnumerateArray())
                {
                    if (!item.TryGetProperty("item", out var inventoryItem) || inventoryItem.ValueKind != JsonValueKind.Object)
                        continue;

                    RegisterNpcScopedInventoryReference(inventoryItem, GetNpcAliases(item), refsByNpc);
                }
            }
            catch
            {
                // ignored
            }
        }

        return refsByNpc;
    }


    private async Task<Dictionary<string, HashSet<string>>> ReadKnownNpcInventoryContainerIdsByNpcAsync()
    {
        var containerIdsByNpc = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_core.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                {
                    if (!doc.RootElement.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var npc in arr.EnumerateArray())
                    {
                        if (!npc.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
                            continue;

                        var aliases = GetNpcAliases(npc);
                        foreach (var inventoryItem in inventory.EnumerateArray())
                        {
                            if (inventoryItem.ValueKind != JsonValueKind.Object || !IsContainerInventoryItem(inventoryItem))
                                continue;

                            var containerId = GetFirstNonEmptyString(inventoryItem, "existedId", "itemId", "id");
                            if (string.IsNullOrWhiteSpace(containerId))
                                continue;

                            RegisterNpcScopedContainerId(containerId, aliases, containerIdsByNpc);
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        foreach (var json in new[]
                 {
                     await ReadPreTurnTrackedFileAsync("game_state/npcs/npc_inventory.json"),
                     await _fs.ReadFileAsync("game_state/npcs/npc_inventory.json")
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("NPCInventoryAdds", out var adds) || adds.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in adds.EnumerateArray())
                {
                    if (!item.TryGetProperty("item", out var inventoryItem) ||
                        inventoryItem.ValueKind != JsonValueKind.Object ||
                        !IsContainerInventoryItem(inventoryItem))
                    {
                        continue;
                    }

                    var containerId = GetFirstNonEmptyString(inventoryItem, "existedId", "itemId", "id");
                    if (string.IsNullOrWhiteSpace(containerId))
                        continue;

                    RegisterNpcScopedContainerId(containerId, GetNpcAliases(item), containerIdsByNpc);
                }
            }
            catch
            {
                // ignored
            }
        }

        return containerIdsByNpc;
    }


    private async Task ValidateNpcInventoryCrossReferencesAsync(
        List<ValidationIssue> issues,
        (HashSet<string> Ids, HashSet<string> Names) knownNpcReferences,
        Dictionary<string, (HashSet<string> Ids, HashSet<string> Names)> npcInventoryRefsByNpc,
        Dictionary<string, HashSet<string>> npcInventoryContainerIdsByNpc)
    {
        var json = await _fs.ReadFileAsync("game_state/npcs/npc_inventory.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var (section, selector) in new (string Section, Func<JsonElement, JsonElement?> Selector)[]
                     {
                         ("NPCInventoryUpdates", item => item.TryGetProperty("itemUpdate", out var itemUpdate) ? itemUpdate : (JsonElement?)null),
                         ("NPCInventoryRemovals", item => item),
                         ("NPCEquipmentChanges", item => item),
                         ("NPCInventoryResourcesChanges", item => item)
                     })
            {
                if (!doc.RootElement.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                var index = 0;
                foreach (var item in arr.EnumerateArray())
                {
                    if (!NpcReferenceExists(item, knownNpcReferences))
                    {
                        index++;
                        continue;
                    }

                    var targetNpcRefs = GetNpcScopedInventoryReferences(item, npcInventoryRefsByNpc);
                    var selected = selector(item);
                    if (!selected.HasValue)
                    {
                        index++;
                        continue;
                    }

                    if (!InventoryReferenceExists(selected.Value, targetNpcRefs))
                    {
                        issues.Add(new ValidationIssue(
                            $"game_state/npcs/npc_inventory.json.{section}[{index}]",
                            IssueSeverity.Error,
                            "NPC inventory command ссылается на itemId/itemName, который не найден в inventory state целевого NPC",
                            code: "npc_inventory_unknown_item_reference",
                            section: "NPCInventory",
                            expected: "itemId/itemName from the target NPC inventory or same-turn NPCInventoryAdds link",
                            actual: DescribeInventoryReference(selected.Value),
                            repairHint: "Ссылайся только на предмет из inventory целевого NPC. Для same-turn нового предмета сначала создай его через NPCInventoryAdds, а затем используй тот же itemName/itemId в связанных NPC inventory командах."));
                    }

                    index++;
                }
            }

            if (doc.RootElement.TryGetProperty("NPCInventoryAdds", out var adds) && adds.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in adds.EnumerateArray())
                {
                    var destinationContainerId = GetFirstNonEmptyString(item, "destinationContainerId");
                    if (string.IsNullOrWhiteSpace(destinationContainerId) || !NpcReferenceExists(item, knownNpcReferences))
                    {
                        index++;
                        continue;
                    }

                    var knownContainerIds = GetNpcScopedContainerIds(item, npcInventoryContainerIdsByNpc);
                    if (!knownContainerIds.Contains(destinationContainerId))
                    {
                        issues.Add(new ValidationIssue(
                            $"game_state/npcs/npc_inventory.json.NPCInventoryAdds[{index}].destinationContainerId",
                            IssueSeverity.Error,
                            "NPCInventoryAdds.destinationContainerId не найден среди контейнеров inventory целевого NPC",
                            code: "npc_inventory_add_unknown_destination_container",
                            section: "NPCInventory",
                            expected: "destinationContainerId from the target NPC's existing container inventory",
                            actual: destinationContainerId,
                            repairHint: "Ссылайся только на существующий container item целевого NPC. Если контейнера нет в его текущем inventory state, добавь предмет в root inventory вместо destinationContainerId."));
                    }

                    index++;
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task ValidateNpcQuestCrossReferencesAsync(
        List<ValidationIssue> issues,
        (HashSet<string> Ids, HashSet<string> Names) knownNpcReferences)
    {
        var json = await _fs.ReadFileAsync("game_state/npcs/npc_goals.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("NPCQuestUpdates", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var item in arr.EnumerateArray())
            {
                var itemContext = $"game_state/npcs/npc_goals.json.NPCQuestUpdates[{index}]";
                if (!NpcReferenceExists(item, knownNpcReferences))
                {
                    index++;
                    continue;
                }

                index++;
            }
        }
        catch
        {
            // ignored
        }
    }


    private static IEnumerable<JsonElement> EnumerateLocationLikeObjects(JsonElement root, bool includeLocationUpdates = true)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("currentLocationData", out var currentLocationData) &&
            currentLocationData.ValueKind == JsonValueKind.Object)
        {
            yield return currentLocationData;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            (root.TryGetProperty("locationId", out _) || root.TryGetProperty("locationType", out _)))
        {
            yield return root;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("worldMapUpdates", out var worldMapUpdates) &&
            worldMapUpdates.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in EnumerateLocationLikeObjects(worldMapUpdates, includeLocationUpdates))
                yield return item;
        }

        foreach (var propName in includeLocationUpdates
                     ? new[] { "newLocations", "locations", "locationUpdates" }
                     : new[] { "newLocations", "locations" })
        {
            if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    yield return item;
            }
        }
    }


    private static void RegisterWorldLocationState(WorldLocationStateIndex index, JsonElement location)
    {
        var locationId = GetFirstNonEmptyString(location, "locationId");
        if (string.IsNullOrWhiteSpace(locationId))
            return;

        var locationType = GetFirstNonEmptyString(location, "locationType");
        if (!string.IsNullOrWhiteSpace(locationType))
            index.LocationTypesByLocationId[locationId] = locationType;

        var biome = GetFirstNonEmptyString(location, "biome");
        if (!string.IsNullOrWhiteSpace(biome))
            index.BiomesByLocationId[locationId] = biome;

        if (location.TryGetProperty("coordinates", out var coordinates) &&
            TryGetNormalizedLocationCoordinatesKey(coordinates, out var coordinateKey))
        {
            AddDictionarySetValue(index.CoordinateKeysByLocationId, locationId, coordinateKey);
        }

        if (location.TryGetProperty("adjacencyMap", out var adjacencyMap) &&
            adjacencyMap.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in adjacencyMap.EnumerateArray())
            {
                if (link.ValueKind != JsonValueKind.Object ||
                    !link.TryGetProperty("targetCoordinates", out var targetCoordinates) ||
                    !TryGetNormalizedLocationCoordinatesKey(targetCoordinates, out var targetCoordinateKey))
                {
                    continue;
                }

                AddDictionarySetValue(index.LinkTargetCoordinateKeysBySourceLocationId, locationId, targetCoordinateKey);
            }
        }

        if (location.TryGetProperty("locationStorages", out var storages) &&
            storages.ValueKind == JsonValueKind.Array)
        {
            foreach (var storage in storages.EnumerateArray())
            {
                var storageId = GetFirstNonEmptyString(storage, "storageId");
                if (!string.IsNullOrWhiteSpace(storageId))
                    AddDictionarySetValue(index.StorageIdsByLocationId, locationId, storageId);
            }
        }

        if (location.TryGetProperty("activeThreats", out var activeThreats) &&
            activeThreats.ValueKind == JsonValueKind.Array)
        {
            foreach (var threat in activeThreats.EnumerateArray())
            {
                var threatId = GetFirstNonEmptyString(threat, "threatId");
                if (!string.IsNullOrWhiteSpace(threatId))
                {
                    AddDictionarySetValue(index.ThreatIdsByLocationId, locationId, threatId);
                    if (threat.TryGetProperty("currentActivity", out var currentActivity) &&
                        currentActivity.ValueKind == JsonValueKind.Object)
                    {
                        AddDictionarySetValue(index.ThreatIdsWithCurrentActivityByLocationId, locationId, threatId);
                    }
                }
            }
        }
    }


    private static bool TryGetNormalizedLocationCoordinatesKey(JsonElement coordinates, out string coordinateKey)
    {
        coordinateKey = string.Empty;
        if (coordinates.ValueKind != JsonValueKind.Object ||
            !TryGetCoordinateInt(coordinates, "x", out var x) ||
            !TryGetCoordinateInt(coordinates, "y", out var y))
        {
            return false;
        }

        var z = 0;
        if (coordinates.TryGetProperty("z", out var zNode))
        {
            if (!TryGetCoordinateInt(coordinates, "z", out z))
                return false;
        }

        coordinateKey = $"{x}:{y}:{z}";
        return true;
    }


    private static bool TryGetCoordinateInt(JsonElement coordinates, string propName, out int value)
    {
        value = 0;
        if (!coordinates.TryGetProperty(propName, out var node) ||
            node.ValueKind != JsonValueKind.Number ||
            !node.TryGetInt32(out value))
        {
            return false;
        }

        return true;
    }


    private static void AddDictionarySetValue(
        Dictionary<string, HashSet<string>> dictionary,
        string key,
        string value)
    {
        if (!dictionary.TryGetValue(key, out var values))
        {
            values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dictionary[key] = values;
        }

        values.Add(value);
    }


    private static void CollectFactionIdsFromStateRoot(JsonElement root, HashSet<string> ids, HashSet<string>? preTurnKnownIds)
    {
        if (preTurnKnownIds == null &&
            root.TryGetProperty("factions", out var factions) &&
            factions.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factions.EnumerateArray())
            {
                var factionId = GetFirstNonEmptyString(item, "factionId");
                if (!string.IsNullOrWhiteSpace(factionId))
                    ids.Add(factionId);
            }
        }

        if (!root.TryGetProperty("factionDataChanges", out var factionDataChanges) ||
            factionDataChanges.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in factionDataChanges.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var factionId = GetFirstNonEmptyString(item, "factionId");
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var initialId = GetFirstNonEmptyString(item, "initialId");
            var isNewFaction = item.TryGetProperty("isNewFaction", out var isNewFactionNode) &&
                               isNewFactionNode.ValueKind == JsonValueKind.True;

            if (preTurnKnownIds == null ||
                preTurnKnownIds.Contains(factionId) ||
                ((!string.IsNullOrWhiteSpace(initialId) || isNewFaction) && !preTurnKnownIds.Contains(factionId)))
            {
                ids.Add(factionId);
            }
        }
    }


    private static HashSet<string> FlattenCoordinateKeys(Dictionary<string, HashSet<string>> coordinateKeysByLocationId)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var values in coordinateKeysByLocationId.Values)
            keys.UnionWith(values);

        return keys;
    }


    private static void ValidateNewLocationCoordinateKey(
        string coordinateKey,
        string context,
        HashSet<string> knownCoordinateKeys,
        HashSet<string> sameTurnCoordinateKeys,
        List<ValidationIssue> issues)
    {
        if (knownCoordinateKeys.Contains(coordinateKey))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Новая локация использует coordinates, которые уже заняты существующей canonical location",
                code: "world_map_new_location_coordinates_conflict_existing",
                section: "WorldMap",
                expected: "coordinates that do not conflict with existing known locations",
                actual: coordinateKey,
                repairHint: "Для новой локации выбери coordinates, которые не совпадают с уже известной canonical location. Если это existing location, используй её locationId и canonical coordinates вместо создания новой записи."));
            return;
        }

        if (!sameTurnCoordinateKeys.Add(coordinateKey))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Две same-turn новые локации используют один и тот же canonical coordinate key",
                code: "world_map_new_location_coordinates_duplicate_same_turn",
                section: "WorldMap",
                expected: "unique coordinates per newly created location",
                actual: coordinateKey,
                repairHint: "Не создавай несколько новых локаций с одинаковыми coordinates в одном accepted turn. Разведи их coordinate keys или объедини в одну canonical location."));
        }
    }


    private static void ValidateAdjacencyTargets(JsonElement location, string context, HashSet<string> knownLocationIds, List<ValidationIssue> issues)
    {
        if (!location.TryGetProperty("adjacencyMap", out var adjacencyMap) || adjacencyMap.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in adjacencyMap.EnumerateArray())
        {
            var targetLocationId = GetFirstNonEmptyString(entry, "targetLocationId");
            if (string.IsNullOrWhiteSpace(targetLocationId))
            {
                index++;
                continue;
            }

            if (!knownLocationIds.Contains(targetLocationId))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.adjacencyMap[{index}].targetLocationId",
                    IssueSeverity.Error,
                    $"adjacencyMap targetLocationId '{targetLocationId}' не найден среди известных локаций",
                    code: "world_map_adjacency_unknown_target",
                    section: "WorldMap",
                    expected: "existing locationId from canonical world state",
                    actual: targetLocationId,
                    repairHint: "Используй уже существующий canonical locationId из world state. Не адресуй adjacencyMap через временный или ещё не материализованный location target."));
            }
            index++;
        }
    }


    private static bool InventorySidecarReferenceExists(JsonElement item, (HashSet<string> Ids, HashSet<string> Names) inventoryRefs)
    {
        foreach (var key in new[] { "existedId", "itemId", "id" })
        {
            var value = GetFirstNonEmptyString(item, key);
            if (!string.IsNullOrWhiteSpace(value))
                return inventoryRefs.Ids.Contains(value);
        }

        var name = GetFirstNonEmptyString(item, "itemName", "name");
        return !string.IsNullOrWhiteSpace(name) && inventoryRefs.Names.Contains(name);
    }


    private static bool InventoryReferenceExists(JsonElement item, (HashSet<string> Ids, HashSet<string> Names) inventoryRefs)
    {
        return InventorySidecarReferenceExists(item, inventoryRefs);
    }


    private static bool NpcReferenceExists(JsonElement item, (HashSet<string> Ids, HashSet<string> Names) knownNpcReferences)
    {
        foreach (var key in new[] { "NPCId", "npcId", "id" })
        {
            var value = GetFirstNonEmptyString(item, key);
            if (!string.IsNullOrWhiteSpace(value))
                return knownNpcReferences.Ids.Contains(value);
        }

        var name = GetFirstNonEmptyString(item, "NPCName", "npcName", "name");
        return !string.IsNullOrWhiteSpace(name) && knownNpcReferences.Names.Contains(name);
    }


    private static bool NpcMemoryIdExists(
        JsonElement item,
        string memoryId,
        Dictionary<string, HashSet<string>> idsByNpc)
    {
        foreach (var alias in GetNpcAliases(item))
        {
            if (idsByNpc.TryGetValue(alias, out var ids) && ids.Contains(memoryId))
                return true;
        }

        return false;
    }


    private static void RegisterNpcMemoryId(
        JsonElement item,
        string memoryId,
        Dictionary<string, HashSet<string>> idsByNpc)
    {
        if (string.IsNullOrWhiteSpace(memoryId))
            return;

        foreach (var alias in GetNpcAliases(item))
        {
            if (!idsByNpc.TryGetValue(alias, out var ids))
            {
                ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                idsByNpc[alias] = ids;
            }

            ids.Add(memoryId);
        }
    }


    private static IEnumerable<string> GetNpcAliases(JsonElement item)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "NPCId", "npcId", "id", "NPCName", "npcName", "name" })
        {
            var value = GetFirstNonEmptyString(item, key);
            if (!string.IsNullOrWhiteSpace(value))
                aliases.Add(value);
        }

        return aliases;
    }


    private static void RegisterNpcScopedInventoryReference(
        JsonElement inventoryItem,
        IEnumerable<string> npcAliases,
        Dictionary<string, (HashSet<string> Ids, HashSet<string> Names)> refsByNpc)
    {
        foreach (var alias in npcAliases)
        {
            if (!refsByNpc.TryGetValue(alias, out var refs))
                refs = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            RegisterInventoryReference(inventoryItem, refs.Ids, refs.Names);
            refsByNpc[alias] = refs;
        }
    }


    private static (HashSet<string> Ids, HashSet<string> Names) GetNpcScopedInventoryReferences(
        JsonElement item,
        Dictionary<string, (HashSet<string> Ids, HashSet<string> Names)> refsByNpc)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in GetNpcAliases(item))
        {
            if (!refsByNpc.TryGetValue(alias, out var refs))
                continue;

            ids.UnionWith(refs.Ids);
            names.UnionWith(refs.Names);
        }

        return (ids, names);
    }


    private static void RegisterNpcScopedContainerId(
        string containerId,
        IEnumerable<string> npcAliases,
        Dictionary<string, HashSet<string>> containerIdsByNpc)
    {
        foreach (var alias in npcAliases)
        {
            if (!containerIdsByNpc.TryGetValue(alias, out var ids))
            {
                ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                containerIdsByNpc[alias] = ids;
            }

            ids.Add(containerId);
        }
    }


    private static HashSet<string> GetNpcScopedContainerIds(
        JsonElement item,
        Dictionary<string, HashSet<string>> containerIdsByNpc)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in GetNpcAliases(item))
        {
            if (!containerIdsByNpc.TryGetValue(alias, out var aliasIds))
                continue;

            ids.UnionWith(aliasIds);
        }

        return ids;
    }


    private static bool IsContainerInventoryItem(JsonElement inventoryItem)
    {
        return inventoryItem.TryGetProperty("isContainer", out var isContainer) &&
               isContainer.ValueKind == JsonValueKind.True;
    }


    private static void RegisterInventoryReference(JsonElement item, HashSet<string> ids, HashSet<string> names)
    {
        foreach (var key in new[] { "existedId", "itemId", "id" })
        {
            var value = GetFirstNonEmptyString(item, key);
            if (!string.IsNullOrWhiteSpace(value))
                ids.Add(value);
        }

        var name = GetFirstNonEmptyString(item, "itemName", "name");
        if (!string.IsNullOrWhiteSpace(name))
            names.Add(name);
    }


    private static bool TryGetInventoryItemsArrayForKnownReferenceRead(JsonElement root, out JsonElement items, out bool fullObjectOnly)
    {
        if (root.TryGetProperty("items", out items))
        {
            fullObjectOnly = true;
            return true;
        }

        if (root.TryGetProperty("UpdateInventory", out items))
        {
            fullObjectOnly = true;
            return true;
        }

        fullObjectOnly = false;
        return false;
    }


    private static bool ShouldAcceptInventoryReferenceCandidate(
        JsonElement item,
        HashSet<string>? knownExistingItemIds,
        bool currentStateNewItemsOnly)
    {
        if (!currentStateNewItemsOnly)
        {
            var existedId = GetFirstNonEmptyString(item, "existedId");
            return string.IsNullOrWhiteSpace(existedId) ||
                   knownExistingItemIds == null ||
                   knownExistingItemIds.Count == 0 ||
                   knownExistingItemIds.Contains(existedId);
        }

        if (!item.TryGetProperty("itemId", out var itemIdNode) ||
            itemIdNode.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(itemIdNode.GetString()))
        {
            return false;
        }

        var stableItemId = itemIdNode.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stableItemId))
            return false;

        var existedIdForCurrentState = GetFirstNonEmptyString(item, "existedId");
        if (!string.IsNullOrWhiteSpace(existedIdForCurrentState))
            return false;

        return knownExistingItemIds == null || !knownExistingItemIds.Contains(stableItemId);
    }


    private static string? GetInventoryReferenceCandidateId(JsonElement item, bool currentStateNewItemsOnly)
    {
        if (!currentStateNewItemsOnly)
            return GetFirstNonEmptyString(item, "existedId", "itemId", "id");

        return item.TryGetProperty("itemId", out var itemIdNode) &&
               itemIdNode.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(itemIdNode.GetString())
            ? itemIdNode.GetString()
            : null;
    }


    private static void RegisterKnownInventoryItemReferencesFromJson(
        string? json,
        HashSet<string> ids,
        HashSet<string> names,
        HashSet<string>? knownExistingItemIds,
        bool currentStateNewItemsOnly)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetInventoryItemsArrayForKnownReferenceRead(doc.RootElement, out var items, out var fullObjectOnly) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (fullObjectOnly && !IsLikelyFullInventoryItemObject(item))
                    continue;

                if (!ShouldAcceptInventoryReferenceCandidate(item, knownExistingItemIds, currentStateNewItemsOnly))
                    continue;

                RegisterInventoryReference(item, ids, names);
            }
        }
        catch
        {
            // ignored
        }
    }


    private static InventoryEquipProfile? TryResolveInventoryItemEquipProfileFromJson(
        string? json,
        string itemId,
        HashSet<string>? knownExistingItemIds,
        bool currentStateNewItemsOnly)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(itemId))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetInventoryItemsArrayForKnownReferenceRead(doc.RootElement, out var items, out var fullObjectOnly) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var currentItem in items.EnumerateArray())
            {
                if (currentItem.ValueKind != JsonValueKind.Object)
                    continue;

                var currentItemId = GetInventoryReferenceCandidateId(currentItem, currentStateNewItemsOnly);
                if (!string.Equals(currentItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fullObjectOnly && !IsLikelyFullInventoryItemObject(currentItem))
                    continue;

                if (!ShouldAcceptInventoryReferenceCandidate(currentItem, knownExistingItemIds, currentStateNewItemsOnly))
                    continue;

                return ReadInventoryEquipProfile(currentItem);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
