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
    private async Task ValidateGuardianCrossReferencesAsync(List<ValidationIssue> issues)
    {
        var guardianJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardianJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(guardianJson);
            var root = doc.RootElement;
            var preTurnGuardiansJson = await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json");
            var preTurnGuardianIds = ReadGuardianIdsFromJson(preTurnGuardiansJson);
            var guardiansById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var guardiansByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var guardian in guardians.EnumerateArray())
                {
                    var guardianContext = $"game_state/meta/guardians.json.guardians[{index}]";
                    var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                    var guardianName = GuardianManifestation.GetDisplayName(guardian);
                    var guardianCanonicalName = GuardianManifestation.GetCanonicalName(guardian);
                    if (!string.IsNullOrWhiteSpace(guardianId))
                        guardiansById[guardianId] = guardianContext;
                    if (!string.IsNullOrWhiteSpace(guardianName))
                        guardiansByName[guardianName] = guardianContext;
                    if (!string.IsNullOrWhiteSpace(guardianCanonicalName))
                        guardiansByName[guardianCanonicalName] = guardianContext;

                    var guardianDomain = GetFirstNonEmptyString(guardian, "domain");
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        !preTurnGuardianIds.Contains(guardianId) &&
                        (string.IsNullOrWhiteSpace(guardianDomain) ||
                         string.Equals(guardianName, guardianId, StringComparison.OrdinalIgnoreCase)))
                    {
                        issues.Add(new ValidationIssue(
                            guardianContext,
                            IssueSeverity.Error,
                            $"Guardian '{guardianId}' появился без valid create surface; non-create команды не могут создавать placeholder guardian state.",
                            code: "guardian_unknown_non_create_target",
                            section: "UpdateGuardians",
                            repairHint: "Используй UpdateGuardians.create для нового Хранителя или ссылайся только на существующий guardianId."));
                    }

                    index++;
                }
            }

            if (root.TryGetProperty("activeGuardian", out var activeGuardian) &&
                activeGuardian.ValueKind == JsonValueKind.Object)
            {
                var activeGuardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id");
                var activeGuardianName = GuardianManifestation.GetDisplayName(activeGuardian);
                var activeGuardianCanonicalName = GuardianManifestation.GetCanonicalName(activeGuardian);
                var found = false;

                if (!string.IsNullOrWhiteSpace(activeGuardianId) && guardiansById.ContainsKey(activeGuardianId))
                    found = true;
                if (!found && !string.IsNullOrWhiteSpace(activeGuardianName) && guardiansByName.ContainsKey(activeGuardianName))
                    found = true;
                if (!found && !string.IsNullOrWhiteSpace(activeGuardianCanonicalName) && guardiansByName.ContainsKey(activeGuardianCanonicalName))
                    found = true;

                if (!found && (guardiansById.Count > 0 || guardiansByName.Count > 0))
                {
                    var activeGuardianRef = !string.IsNullOrWhiteSpace(activeGuardianId)
                        ? $"guardianId={activeGuardianId}"
                        : !string.IsNullOrWhiteSpace(activeGuardianName)
                            ? $"name={activeGuardianName}"
                            : "unknown activeGuardian reference";
                    issues.Add(new ValidationIssue(
                        "game_state/meta/guardians.json",
                        IssueSeverity.Error,
                        $"Активный хранитель '{activeGuardianName ?? activeGuardianId ?? "unknown"}' не найден в массиве guardians",
                        code: "active_guardian_missing_in_guardians_array",
                        section: "Guardians",
                        expected: "activeGuardian that resolves to an entry inside guardians[]",
                        actual: activeGuardianRef,
                        repairHint: "Синхронно обновляй activeGuardian тем же guardianId/name, который реально присутствует в массиве guardians[]. Не оставляй activeGuardian ссылкой на устаревший или несозданный guardian entry."));
                }
            }

            var pendingPresetId = TryReadPendingSystemGuardianPresetId(preTurnGuardiansJson);
            if (!string.IsNullOrWhiteSpace(pendingPresetId))
            {
                if (!root.TryGetProperty("activeGuardian", out var selectedActiveGuardian) ||
                    selectedActiveGuardian.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/guardians.json.activeGuardian",
                        IssueSeverity.Error,
                        "Системный guardian preset был выбран клиентом, но активный Хранитель не materialized.",
                        code: "system_guardian_pending_preset_missing_active_guardian",
                        section: "SystemGuardianPresets",
                        expected: $"activeGuardian.sourcePreset.presetId = {pendingPresetId}",
                        actual: "missing"));
                }
                else
                {
                    var activePresetId = TryReadGuardianSourcePresetId(selectedActiveGuardian);
                    if (!string.Equals(activePresetId, pendingPresetId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/guardians.json.activeGuardian.sourcePreset.presetId",
                            IssueSeverity.Error,
                            "GM проигнорировал client-selected system guardian preset при materialization Хранителя.",
                            code: "system_guardian_pending_preset_not_materialized",
                            section: "SystemGuardianPresets",
                            expected: pendingPresetId,
                            actual: string.IsNullOrWhiteSpace(activePresetId) ? "missing/empty" : activePresetId,
                            repairHint: "При system-preset guardian creation materialize именно выбранного Хранителя и запиши matching sourcePreset metadata."));
                    }
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private static string? TryReadPendingSystemGuardianPresetId(string? guardiansJson)
    {
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (!doc.RootElement.TryGetProperty("pendingGuardianCreation", out var pending) ||
                pending.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var mode = GetFirstNonEmptyString(pending, "mode");
            if (!string.Equals(mode, "system_preset", StringComparison.OrdinalIgnoreCase))
                return null;

            return GetFirstNonEmptyString(pending, "presetId");
        }
        catch
        {
            return null;
        }
    }


    private static string? TryReadGuardianSourcePresetId(JsonElement guardian)
    {
        if (!guardian.TryGetProperty("sourcePreset", out var sourcePreset) ||
            sourcePreset.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetFirstNonEmptyString(sourcePreset, "presetId");
    }


    private static HashSet<string> ReadGuardianIdsFromJson(string? guardiansJson)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return ids;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (doc.RootElement.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                {
                    var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                    if (!string.IsNullOrWhiteSpace(guardianId))
                        ids.Add(guardianId);
                }
            }

            if (doc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) &&
                activeGuardian.ValueKind == JsonValueKind.Object)
            {
                var guardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id");
                if (!string.IsNullOrWhiteSpace(guardianId))
                    ids.Add(guardianId);
            }
        }
        catch
        {
            // ignored
        }

        return ids;
    }


    private async Task<HashSet<string>> ReadKnownGuardianIdsAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var preTurnJson = await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                CollectGuardianIdsFromStateRoot(doc.RootElement, ids, includeCommandSurfaces: false);
            }
            catch
            {
                // ignored
            }
        }

        var currentJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentJson);
                CollectCreatedGuardianIdsFromStateRoot(doc.RootElement, ids);
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private async Task<(HashSet<string> Ids, HashSet<string> Names)> ReadKnownGuardianReferencesAsync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var preTurnJson = await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                CollectGuardianReferencesFromStateRoot(doc.RootElement, ids, names, includeCommandSurfaces: false);
            }
            catch
            {
                // ignored
            }
        }

        var currentJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentJson);
                CollectCreatedGuardianReferencesFromStateRoot(doc.RootElement, ids, names);
            }
            catch
            {
                // ignored
            }
        }

        return (ids, names);
    }


    private async Task ValidateGuardianNpcBoundaryAsync(List<ValidationIssue> issues, (HashSet<string> Ids, HashSet<string> Names) knownGuardianReferences)
    {
        if (knownGuardianReferences.Ids.Count == 0 && knownGuardianReferences.Names.Count == 0)
            return;

        var json = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene" })
            {
                if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                var index = 0;
                foreach (var npc in arr.EnumerateArray())
                {
                    var context = $"game_state/npcs/npc_core.json.{sectionName}[{index++}]";
                    if (npc.ValueKind != JsonValueKind.Object)
                        continue;

                    var npcId = GetFirstNonEmptyString(npc, "NPCId", "npcId", "id");
                    var npcName = GetFirstNonEmptyString(npc, "name", "npcName", "NPCName");
                    var idCollision = !string.IsNullOrWhiteSpace(npcId) && knownGuardianReferences.Ids.Contains(npcId);
                    var nameCollision = !string.IsNullOrWhiteSpace(npcName) && knownGuardianReferences.Names.Contains(npcName);
                    if (!idCollision && !nameCollision)
                        continue;

                    issues.Add(new ValidationIssue(
                        context,
                        IssueSeverity.Error,
                        "Guardians не должны попадать в NPC surfaces",
                        code: "guardian_leaked_into_npc_surface",
                        section: "Guardians",
                        expected: "Guardians only in UpdateGuardians / guardians.json",
                        actual: idCollision
                            ? $"guardianId collision: {npcId}"
                            : $"guardian name collision: {npcName}",
                        repairHint: "Не используй UpdateNPCs или NPCsInScene для Хранителей. Перенеси сущность в UpdateGuardians / game_state/meta/guardians.json."));
                }
            }
        }
        catch
        {
            // ignored
        }
    }


    private static void CollectGuardianReferencesFromStateRoot(JsonElement root, HashSet<string> ids, HashSet<string> names, bool includeCommandSurfaces = true)
    {
        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
                RegisterGuardianReference(guardian, ids, names);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
            RegisterGuardianReference(activeGuardian, ids, names);

        if (includeCommandSurfaces &&
            root.TryGetProperty("UpdateGuardians", out var updates) &&
            updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var command in updates.EnumerateArray())
            {
                if (command.ValueKind != JsonValueKind.Object)
                    continue;

                var commandName = GetFirstNonEmptyString(command, "command");
                if (string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) &&
                    command.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object)
                {
                    RegisterGuardianReference(data, ids, names);
                    continue;
                }

                RegisterGuardianReference(command, ids, names);
            }
        }
    }


    private static void RegisterGuardianReference(JsonElement guardian, HashSet<string> ids, HashSet<string> names)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        var canonicalName = GuardianManifestation.GetCanonicalName(guardian);
        if (!string.IsNullOrWhiteSpace(guardianId))
            ids.Add(guardianId);
        if (!string.IsNullOrWhiteSpace(guardianName))
            names.Add(guardianName);
        if (!string.IsNullOrWhiteSpace(canonicalName))
            names.Add(canonicalName);
    }


    private static void CollectCreatedGuardianIdsFromStateRoot(JsonElement root, HashSet<string> ids)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates) || updates.ValueKind != JsonValueKind.Array)
            return;

        foreach (var command in updates.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object)
                continue;

            var commandName = GetFirstNonEmptyString(command, "command");
            if (!string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) ||
                !command.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var guardianId = GetFirstNonEmptyString(data, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(guardianId))
                ids.Add(guardianId);
        }
    }


    private static void CollectCreatedGuardianReferencesFromStateRoot(JsonElement root, HashSet<string> ids, HashSet<string> names)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates) || updates.ValueKind != JsonValueKind.Array)
            return;

        foreach (var command in updates.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object)
                continue;

            var commandName = GetFirstNonEmptyString(command, "command");
            if (!string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) ||
                !command.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            RegisterGuardianReference(data, ids, names);
        }
    }


    private static void CollectCodexEntryIdsFromRoot(JsonElement root, HashSet<string> ids, bool includeStoredEntries)
    {
        if (includeStoredEntries &&
            root.TryGetProperty("entries", out var entries) &&
            entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = GetFirstNonEmptyString(entry, "entryId");
                if (!string.IsNullOrWhiteSpace(entryId))
                    ids.Add(entryId);
            }
        }

        if (root.TryGetProperty("loreCodexUpdates", out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in updates.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var command = GetFirstNonEmptyString(item, "command");
                var isSameTurnAdd = string.Equals(command, "add", StringComparison.OrdinalIgnoreCase);

                if (!isSameTurnAdd)
                    continue;

                if (item.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                {
                    var entryId = GetFirstNonEmptyString(entry, "entryId");
                    if (!string.IsNullOrWhiteSpace(entryId))
                        ids.Add(entryId);
                }
            }
        }
    }


    private async Task ValidateSoulQuestGuardianCrossReferencesAsync(List<ValidationIssue> issues, HashSet<string> knownGuardianIds)
    {
        var json = await _fs.ReadFileAsync("game_state/quests/soul_quests.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            string questCollectionName;
            if (doc.RootElement.TryGetProperty("quests", out var quests))
            {
                questCollectionName = "quests";
            }
            else if (doc.RootElement.TryGetProperty("UpdateSoulQuests", out quests))
            {
                questCollectionName = "UpdateSoulQuests";
            }
            else
            {
                return;
            }

            if (quests.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var quest in quests.EnumerateArray())
            {
                var guardianId = GetFirstNonEmptyString(quest, "guardianId");
                if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/quests/soul_quests.json.{questCollectionName}[{index}].guardianId",
                        IssueSeverity.Error,
                        $"Soul Quest ссылается на неизвестного guardianId '{guardianId}'",
                        code: "soul_quest_unknown_guardian_id",
                        section: "CrossReferences",
                        expected: "existing guardianId from canonical guardians state",
                        actual: guardianId,
                        repairHint: "Для Soul Quest используй существующий guardianId из canonical guardians state. Если Хранитель создаётся впервые, сначала сохрани его через UpdateGuardians.create, а затем ссылайся на его permanent guardianId."));
                }
                index++;
            }
        }
        catch
        {
            // ignored
        }
    }


    private async Task<HashSet<string>> ReadKnownSystemGuardianPresetIdsAsync()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootDirectory = Path.Combine(_fs.BasePath, SystemGuardianLibraryService.RootDirectoryName);
        if (!Directory.Exists(rootDirectory))
            return result;

        foreach (var manifestPath in Directory.EnumerateFiles(rootDirectory, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;

                var presetId = GetFirstNonEmptyString(doc.RootElement, "presetId");
                if (!string.IsNullOrWhiteSpace(presetId))
                    result.Add(presetId);
            }
            catch
            {
                // ignored
            }
        }

        return result;
    }


    private async Task ValidateRivalSoulArcCrossReferencesAsync(
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds,
        HashSet<string> knownSystemGuardianPresetIds)
    {
        var json = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            string collectionName;
            JsonElement arcs;
            if (doc.RootElement.TryGetProperty("arcs", out arcs))
            {
                collectionName = "arcs";
            }
            else if (doc.RootElement.TryGetProperty("UpdateRivalSoulArcs", out arcs))
            {
                collectionName = "UpdateRivalSoulArcs";
            }
            else
            {
                return;
            }

            if (arcs.ValueKind != JsonValueKind.Array)
                return;

            JsonDocument? worldEventsDoc = null;
            JsonElement worldEvents = default;
            string? worldEventCollectionName = null;
            var relatedWorldEventsByArcId = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
            var worldEventsJson = await _fs.ReadFileAsync("game_state/world/world_events.json");
            if (!string.IsNullOrWhiteSpace(worldEventsJson))
            {
                worldEventsDoc = JsonDocument.Parse(worldEventsJson);
                if (worldEventsDoc.RootElement.TryGetProperty("worldEventsLog", out worldEvents))
                {
                    worldEventCollectionName = "worldEventsLog";
                }
                else if (worldEventsDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    worldEvents = worldEventsDoc.RootElement;
                    worldEventCollectionName = "worldEventsLog";
                }

                if (worldEventCollectionName != null && worldEvents.ValueKind == JsonValueKind.Array)
                    relatedWorldEventsByArcId = BuildRelatedWorldEventsByArcId(worldEvents);
            }

            var knownArcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var knownArcSponsorGuardianIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var visibleBonusClueUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var countedVisibleBonusClueRevealKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var arc in arcs.EnumerateArray())
            {
                var arcContext = $"{RivalSoulArcService.StatePath}.{collectionName}[{index}]";
                var arcId = GetFirstNonEmptyString(arc, "arcId");
                if (!string.IsNullOrWhiteSpace(arcId))
                    knownArcIds.Add(arcId);

                if (arc.ValueKind != JsonValueKind.Object ||
                    !arc.TryGetProperty("sponsorGuardianRef", out var sponsorRef) ||
                    sponsorRef.ValueKind != JsonValueKind.Object)
                {
                    index++;
                    continue;
                }

                var mode = GetFirstNonEmptyString(sponsorRef, "mode");
                var sponsorGuardianId = string.Empty;
                if (string.Equals(mode, "guardianId", StringComparison.OrdinalIgnoreCase))
                {
                    sponsorGuardianId = GetFirstNonEmptyString(sponsorRef, "guardianId");
                    if (!string.IsNullOrWhiteSpace(sponsorGuardianId) && !knownGuardianIds.Contains(sponsorGuardianId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{arcContext}.sponsorGuardianRef.guardianId",
                            IssueSeverity.Error,
                            $"Rival soul arc ссылается на неизвестный guardianId '{sponsorGuardianId}'",
                            code: "rival_arc_unknown_guardian_id",
                            section: "RivalSoulArcs",
                            expected: "existing guardianId from canonical guardians state",
                            actual: sponsorGuardianId,
                            repairHint: "Для sponsorGuardianRef.mode=guardianId используй guardianId уже существующего Хранителя из canonical guardians state."));
                    }
                    else if (!string.IsNullOrWhiteSpace(arcId) && !string.IsNullOrWhiteSpace(sponsorGuardianId))
                    {
                        knownArcSponsorGuardianIds[arcId] = sponsorGuardianId;
                    }
                }
                else if (string.Equals(mode, "eternalPreset", StringComparison.OrdinalIgnoreCase))
                {
                    var presetId = GetFirstNonEmptyString(sponsorRef, "presetId");
                    if (!string.IsNullOrWhiteSpace(presetId) && !knownSystemGuardianPresetIds.Contains(presetId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{arcContext}.sponsorGuardianRef.presetId",
                            IssueSeverity.Error,
                            $"Rival soul arc ссылается на неизвестный Eternal Guardian preset '{presetId}'",
                            code: "rival_arc_unknown_eternal_guardian_preset",
                            section: "RivalSoulArcs",
                            expected: "existing presetId from system_guardians library",
                            actual: presetId,
                            repairHint: "Для sponsorGuardianRef.mode=eternalPreset используй реальный presetId из библиотеки извечных хранителей."));
                    }
                }

                ValidateHostileDirectTargetRivalArcClueContract(arc, arcContext, issues, relatedWorldEventsByArcId);

                if (arc.ValueKind == JsonValueKind.Object &&
                    arc.TryGetProperty("publicSignals", out var publicSignals) &&
                    publicSignals.ValueKind == JsonValueKind.Array)
                {
                    var signalIndex = 0;
                    foreach (var signal in publicSignals.EnumerateArray())
                    {
                        var signalContext = $"{arcContext}.publicSignals[{signalIndex++}]";
                        var sourceProjectId = GetFirstNonEmptyString(signal, "bonusClueSourceProjectId");
                        if (string.IsNullOrWhiteSpace(sourceProjectId))
                            continue;

                        var visibleToPlayer = signal.TryGetProperty("visibleToPlayer", out var visibleNode) &&
                                              visibleNode.ValueKind == JsonValueKind.True;
                        if (!string.Equals(mode, "guardianId", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(sponsorGuardianId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{signalContext}.bonusClueSourceProjectId",
                                IssueSeverity.Error,
                                "Bonus clue от lore_research допустим только для rival arc со sponsorGuardianRef.mode=guardianId",
                                code: "rival_arc_bonus_clue_requires_guardian_sponsor",
                                section: "RivalSoulArcs",
                                repairHint: "Используй bonusClueSourceProjectId только там, где sponsorGuardianRef указывает на materialized guardianId."));
                            continue;
                        }

                        if (!visibleToPlayer)
                            continue;

                        var clueCost = TryReadIntField(signal, "bonusClueCost", out var parsedCost) ? Math.Max(1, parsedCost) : 1;
                        var revealKey = BuildVisibleBonusClueRevealKey(arcId!, signal, isWorldEvent: false);
                        if (!countedVisibleBonusClueRevealKeys.Add(revealKey))
                            continue;

                        var usageKey = $"{sponsorGuardianId}::{sourceProjectId}";
                        visibleBonusClueUsage[usageKey] = visibleBonusClueUsage.GetValueOrDefault(usageKey) + clueCost;
                    }
                }

                index++;
            }

            if (visibleBonusClueUsage.Count > 0)
            {
                var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
                JsonDocument? trackerDoc = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(trackerJson))
                        trackerDoc = JsonDocument.Parse(trackerJson);
                }
                catch
                {
                    trackerDoc = null;
                }

                foreach (var usage in visibleBonusClueUsage)
                {
                    var parts = usage.Key.Split(new[] { "::" }, 2, StringSplitOptions.None);
                    if (parts.Length != 2)
                        continue;

                    var grantedBudget = ReadGrantedLoreResearchVisibleClueBudget(trackerDoc?.RootElement, parts[0], parts[1]);
                    if (grantedBudget <= 0)
                    {
                        issues.Add(new ValidationIssue(
                            $"{RivalSoulArcService.StatePath}.{collectionName}",
                            IssueSeverity.Error,
                            $"Rival arc signals используют bonus clue sourceProjectId '{parts[1]}', но у guardian '{parts[0]}' нет completed lore_research с clue budget",
                            code: "rival_arc_bonus_clue_unknown_source_project",
                            section: "RivalSoulArcs",
                            repairHint: "Для bonusClueSourceProjectId используй completed lore_research projectId того же guardian sponsor-а."));
                        continue;
                    }

                    if (usage.Value > grantedBudget)
                    {
                        issues.Add(new ValidationIssue(
                            $"{RivalSoulArcService.StatePath}.{collectionName}",
                            IssueSeverity.Error,
                            "Rival arc bonus clue usage превышает granted lore_research visible clue budget",
                            code: "rival_arc_bonus_clue_budget_exceeded",
                            section: "RivalSoulArcs",
                            expected: $"<= {grantedBudget}",
                            actual: usage.Value.ToString(),
                            repairHint: "Не раскрывай через bonusClueSourceProjectId больше player-visible extra clues, чем даёт completed lore_research project."));
                    }
                }

                trackerDoc?.Dispose();
            }

            if (knownArcIds.Count == 0)
            {
                worldEventsDoc?.Dispose();
                return;
            }

            var soulQuestJson = await _fs.ReadFileAsync("game_state/quests/soul_quests.json");
            if (!string.IsNullOrWhiteSpace(soulQuestJson))
            {
                using var soulQuestDoc = JsonDocument.Parse(soulQuestJson);
                JsonElement quests;
                string? questCollectionName = null;
                if (soulQuestDoc.RootElement.TryGetProperty("quests", out quests))
                {
                    questCollectionName = "quests";
                }
                else if (soulQuestDoc.RootElement.TryGetProperty("UpdateSoulQuests", out quests))
                {
                    questCollectionName = "UpdateSoulQuests";
                }
                else
                {
                    quests = default;
                }

                if (questCollectionName != null && quests.ValueKind == JsonValueKind.Array)
                {
                    var questIndex = 0;
                    foreach (var quest in quests.EnumerateArray())
                    {
                        var relatedArcId = GetFirstNonEmptyString(quest, "relatedRivalArcId");
                        if (!string.IsNullOrWhiteSpace(relatedArcId) && !knownArcIds.Contains(relatedArcId))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/quests/soul_quests.json.{questCollectionName}[{questIndex}].relatedRivalArcId",
                                IssueSeverity.Error,
                                $"Soul quest ссылается на неизвестный rival arc '{relatedArcId}'",
                                code: "soul_quest_unknown_rival_arc_id",
                                section: "RivalSoulArcs",
                                expected: "existing arcId from canonical rival_soul_arcs state",
                                actual: relatedArcId,
                                repairHint: "Для relatedRivalArcId используй существующий arcId из game_state/world/rival_soul_arcs.json."));
                        }

                        questIndex++;
                    }
                }
            }

            if (worldEventCollectionName == null || worldEvents.ValueKind != JsonValueKind.Array)
            {
                worldEventsDoc?.Dispose();
                return;
            }
            

            var eventIndex = 0;
            foreach (var worldEvent in worldEvents.EnumerateArray())
            {
                var eventContext = $"game_state/world/world_events.json.{worldEventCollectionName}[{eventIndex}]";
                ValidateOptionalString(worldEvent, eventContext, issues, "bonusClueSourceProjectId");
                ValidateOptionalString(worldEvent, eventContext, issues, "bonusClueRevealId");
                if (worldEvent.TryGetProperty("bonusClueCost", out _))
                    ValidateNonNegativeIntegerField(worldEvent, eventContext, issues, "bonusClueCost", "RivalSoulArcs");

                var relatedArcId = GetFirstNonEmptyString(worldEvent, "relatedRivalArcId");
                if (!string.IsNullOrWhiteSpace(relatedArcId) && !knownArcIds.Contains(relatedArcId))
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/world/world_events.json.{worldEventCollectionName}[{eventIndex}].relatedRivalArcId",
                        IssueSeverity.Error,
                        $"World event ссылается на неизвестный rival arc '{relatedArcId}'",
                        code: "world_event_unknown_rival_arc_id",
                        section: "RivalSoulArcs",
                        expected: "existing arcId from canonical rival_soul_arcs state",
                        actual: relatedArcId,
                        repairHint: "Если world event является сигналом чужой нити судьбы, используй существующий relatedRivalArcId из game_state/world/rival_soul_arcs.json."));
                }

                if (!string.IsNullOrWhiteSpace(relatedArcId))
                {
                    var visibility = GetFirstNonEmptyString(worldEvent, "visibility");
                    if (string.IsNullOrWhiteSpace(visibility))
                    {
                        issues.Add(new ValidationIssue(
                            $"{eventContext}.visibility",
                            IssueSeverity.Error,
                            "World event, связанный с rival soul arc, обязан явно указывать visibility",
                            code: "world_event_rival_arc_missing_visibility",
                            section: "RivalSoulArcs",
                            expected: "Public | Regional | Secret | Faction-Internal | player_known",
                            repairHint: "Для linked rival-thread world event всегда указывай visibility. Используй Public/Regional для обычных новостей, Secret/Faction-Internal для скрытых событий и player_known, если игрок уже добыл эту информацию через игру."));
                    }
                    else if (!IsRecognizedRivalWorldEventVisibility(visibility))
                    {
                        issues.Add(new ValidationIssue(
                            $"{eventContext}.visibility",
                            IssueSeverity.Error,
                            "World event, связанный с rival soul arc, использует неподдерживаемое visibility",
                            code: "world_event_rival_arc_invalid_visibility",
                            section: "RivalSoulArcs",
                            expected: "Public | Regional | Secret | Faction-Internal | player_known",
                            actual: visibility,
                            repairHint: "Для linked rival-thread world event используй только Public, Regional, Secret, Faction-Internal или player_known. Если игрок реально узнал о скрытом событии, переведи его в player_known."));
                    }
                }

                var sourceProjectId = GetFirstNonEmptyString(worldEvent, "bonusClueSourceProjectId");
                if (!string.IsNullOrWhiteSpace(sourceProjectId))
                {
                    var revealId = GetFirstNonEmptyString(worldEvent, "bonusClueRevealId");
                    if (string.IsNullOrWhiteSpace(revealId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{eventContext}.bonusClueRevealId",
                            IssueSeverity.Error,
                            "world event lore_research bonus clue должен иметь bonusClueRevealId для cross-surface dedupe",
                            code: "world_event_bonus_clue_missing_reveal_id",
                            section: "RivalSoulArcs",
                            repairHint: "Для linked world event clue с bonusClueSourceProjectId всегда передавай stable bonusClueRevealId. Если тот же clue mirrored в publicSignals, используй тот же reveal id."));
                    }

                    if (string.IsNullOrWhiteSpace(relatedArcId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{eventContext}.relatedRivalArcId",
                            IssueSeverity.Error,
                            "world event lore_research bonus clue требует relatedRivalArcId",
                            code: "world_event_bonus_clue_missing_related_arc",
                            section: "RivalSoulArcs",
                            repairHint: "Если world event тратит lore_research bonus clue, привяжи его к существующему rival arc через relatedRivalArcId."));
                    }
                    else if (knownArcSponsorGuardianIds.TryGetValue(relatedArcId, out var sponsorGuardianId))
                    {
                        if (IsPlayerVisibleRivalWorldEvent(worldEvent))
                        {
                            var clueCost = TryReadIntField(worldEvent, "bonusClueCost", out var parsedCost) ? Math.Max(1, parsedCost) : 1;
                            var revealKey = BuildVisibleBonusClueRevealKey(relatedArcId, worldEvent, isWorldEvent: true);
                            if (countedVisibleBonusClueRevealKeys.Add(revealKey))
                            {
                                var usageKey = $"{sponsorGuardianId}::{sourceProjectId}";
                                visibleBonusClueUsage[usageKey] = visibleBonusClueUsage.GetValueOrDefault(usageKey) + clueCost;
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(relatedArcId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{eventContext}.bonusClueSourceProjectId",
                            IssueSeverity.Error,
                            "Bonus clue от lore_research допустим только для world event, связанного с rival arc со sponsorGuardianRef.mode=guardianId",
                            code: "world_event_bonus_clue_requires_guardian_sponsor",
                            section: "RivalSoulArcs",
                            repairHint: "Используй bonusClueSourceProjectId только там, где relatedRivalArcId указывает на rival arc со sponsorGuardianRef.mode=guardianId."));
                    }
                }

                eventIndex++;
            }

            worldEventsDoc?.Dispose();
        }
        catch
        {
            // ignored
        }
    }


    private static string BuildVisibleBonusClueRevealKey(string arcId, JsonElement source, bool isWorldEvent)
    {
        var revealId = GetFirstNonEmptyString(source, "bonusClueRevealId");
        if (!string.IsNullOrWhiteSpace(revealId))
            return $"{arcId}::reveal::{revealId}";

        if (!isWorldEvent)
        {
            var signalId = GetFirstNonEmptyString(source, "signalId");
            if (!string.IsNullOrWhiteSpace(signalId))
                return $"{arcId}::signal::{signalId}";

            return $"{arcId}::signal::{GetIntOrDefault(source, "stage")}::{GetFirstNonEmptyString(source, "source")}::{GetFirstNonEmptyString(source, "description")}";
        }

        var worldEventId = GetFirstNonEmptyString(source, "eventId");
        if (!string.IsNullOrWhiteSpace(worldEventId))
            return $"{arcId}::world_event::{worldEventId}";

        return $"{arcId}::world_event::{GetFirstNonEmptyString(source, "eventTitle", "title", "name")}::{GetFirstNonEmptyString(source, "summary", "description")}";
    }


    private static Dictionary<string, List<JsonElement>> BuildRelatedWorldEventsByArcId(JsonElement worldEvents)
    {
        var result = new Dictionary<string, List<JsonElement>>(StringComparer.OrdinalIgnoreCase);
        if (worldEvents.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var worldEvent in worldEvents.EnumerateArray())
        {
            if (worldEvent.ValueKind != JsonValueKind.Object)
                continue;

            var relatedArcId = GetFirstNonEmptyString(worldEvent, "relatedRivalArcId");
            if (string.IsNullOrWhiteSpace(relatedArcId))
                continue;

            if (!result.TryGetValue(relatedArcId, out var entries))
            {
                entries = new List<JsonElement>();
                result[relatedArcId] = entries;
            }

            entries.Add(worldEvent);
        }

        return result;
    }


    private static bool IsPlayerVisibleRivalWorldEvent(JsonElement worldEvent)
    {
        if (worldEvent.ValueKind != JsonValueKind.Object)
            return false;

        var visibility = GetFirstNonEmptyString(worldEvent, "visibility");
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsRecognizedRivalWorldEventVisibility(string? visibility)
    {
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Faction-Internal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }


    private static int CountPlayerVisibleRivalClues(
        JsonElement arc,
        string arcId,
        IReadOnlyDictionary<string, List<JsonElement>> relatedWorldEventsByArcId)
    {
        var clueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (arc.TryGetProperty("publicSignals", out var publicSignals) &&
            publicSignals.ValueKind == JsonValueKind.Array)
        {
            foreach (var signal in publicSignals.EnumerateArray())
            {
                if (signal.ValueKind != JsonValueKind.Object ||
                    !signal.TryGetProperty("visibleToPlayer", out var visibleNode) ||
                    visibleNode.ValueKind != JsonValueKind.True)
                {
                    continue;
                }

                clueKeys.Add(BuildVisibleBonusClueRevealKey(arcId, signal, isWorldEvent: false));
            }
        }

        if (!string.IsNullOrWhiteSpace(arcId) &&
            relatedWorldEventsByArcId.TryGetValue(arcId, out var relatedWorldEvents))
        {
            foreach (var worldEvent in relatedWorldEvents)
            {
                if (!IsPlayerVisibleRivalWorldEvent(worldEvent))
                    continue;

                clueKeys.Add(BuildVisibleBonusClueRevealKey(arcId, worldEvent, isWorldEvent: true));
            }
        }

        return clueKeys.Count;
    }


    private static void ValidateHostileDirectTargetRivalArcClueContract(
        JsonElement arc,
        string arcContext,
        List<ValidationIssue> issues,
        IReadOnlyDictionary<string, List<JsonElement>> relatedWorldEventsByArcId)
    {
        if (arc.ValueKind != JsonValueKind.Object ||
            !arc.TryGetProperty("playerIntersection", out var playerIntersection) ||
            playerIntersection.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var targetsPlayerDirectly =
            playerIntersection.TryGetProperty("targetsPlayerDirectly", out var targetsNode) &&
            targetsNode.ValueKind == JsonValueKind.True;
        var arcType = GetFirstNonEmptyString(arc, "arcType");
        var status = GetFirstNonEmptyString(arc, "status");
        if (!targetsPlayerDirectly ||
            !string.Equals(arcType, "hostile_hunt", StringComparison.OrdinalIgnoreCase) ||
            (!string.Equals(status, "intersecting", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var visibleClues = CountPlayerVisibleRivalClues(arc, GetFirstNonEmptyString(arc, "arcId") ?? string.Empty, relatedWorldEventsByArcId);
        if (visibleClues >= 2)
            return;

        issues.Add(new ValidationIssue(
            $"{arcContext}.playerIntersection",
            IssueSeverity.Error,
            "Hostile rival soul arc, напрямую нацеленный на игрока, обязан оставить минимум два видимых следа до прямого столкновения",
            code: "rival_arc_hostile_direct_target_needs_two_visible_signals",
            section: "RivalSoulArcs",
            expected: ">= 2 player-visible clues across publicSignals/worldEventsLog with world event visibility Public|Regional|player_known before intersecting/resolved hostile collision",
            actual: visibleClues.ToString(),
            repairHint: "Добавь минимум два player-visible clue до прямого столкновения: publicSignals, связанные worldEventsLog с visibility=Public|Regional|player_known или их сочетание. Если игрок узнал secret/faction-internal событие через игру, переведи linked world event в visibility=player_known. Если один и тот же clue mirrored на обеих поверхностях, reuse bonusClueRevealId."));
    }
}
