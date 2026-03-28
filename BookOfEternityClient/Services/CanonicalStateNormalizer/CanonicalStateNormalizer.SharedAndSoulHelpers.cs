using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class CanonicalStateNormalizer
{
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

    private void ApplyGuardianCommands(JsonObject root, JsonArray updates, int currentTurn, List<JsonObject> pendingPowerEvents)
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
                        AbodePowerRules.EnsureCanonicalState(data);
                        GuardianGachaChargeRules.NormalizeGuardianGachaState(data);
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
                        var questId = GetNodeString(commandNode["questId"]);
                        var completedQuestSnapshot = FindGuardianQuestSnapshot(questManagement, questId);
                        var completed = EnsureArray(questManagement, "completedQuests");
                        completed.Add(new JsonObject
                        {
                            ["questId"] = questId ?? "",
                            ["result"] = GetNodeString(commandNode["outcome"]) ?? "",
                            ["completionDate"] = DateTime.UtcNow.ToString("o"),
                            ["difficulty"] = completedQuestSnapshot != null ? NormalizeGuardianQuestDifficulty(GetNodeString(completedQuestSnapshot["difficulty"])) : null,
                            ["questOrigin"] = completedQuestSnapshot != null ? GetNodeString(completedQuestSnapshot["questOrigin"]) : null,
                            ["sourceProjectId"] = completedQuestSnapshot != null ? GetNodeString(completedQuestSnapshot["sourceProjectId"]) : null,
                            ["sourceArchiveId"] = completedQuestSnapshot != null ? GetNodeString(completedQuestSnapshot["sourceArchiveId"]) : null,
                            ["sourceArchiveTitle"] = completedQuestSnapshot != null ? GetNodeString(completedQuestSnapshot["sourceArchiveTitle"]) : null
                        });

                        RemoveQuestFromArray(questManagement["activeQuests"] as JsonArray, questId);
                        RemoveQuestFromArray(questManagement["availableQuests"] as JsonArray, questId);
                        guardian["questManagement"] = questManagement;
                        if (commandNode["questPowerAudit"] is JsonObject questPowerAudit)
                        {
                            var evt = BuildGuardianQuestPowerEvent(guardian, commandNode, questPowerAudit, completedQuestSnapshot, currentTurn);
                            if (evt != null)
                                pendingPowerEvents.Add(evt);
                        }
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
                        var eventId = $"guardian_gacha_{guardianId}_{currentTurn}_{Guid.NewGuid():N}";
                        history.Add(new JsonObject
                        {
                            ["eventId"] = eventId,
                            ["relicId"] = resultNode != null ? GetNodeString(resultNode["relicId"]) ?? GetNodeString(resultNode["name"]) ?? "" : "",
                            ["costInFeathers"] = GetNodeInt(commandNode["inkFeathersSpent"]),
                            ["finalRarity"] = resultNode != null ? GetNodeString(resultNode["rarity"]) ?? "" : "",
                            ["timestamp"] = DateTime.UtcNow.ToString("o"),
                            ["gachaBonusAudit"] = commandNode["gachaBonusAudit"]?.DeepClone()
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

    private JsonObject? BuildGuardianQuestPowerEvent(
        JsonObject guardian,
        JsonObject commandNode,
        JsonObject questPowerAudit,
        JsonObject? questSnapshot,
        int currentTurn)
    {
        var guardianId = GetNodeString(commandNode["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId))
            return null;

        var finalDelta = GetNodeInt(questPowerAudit["finalDelta"]);
        if (finalDelta == 0)
            return null;

        var questId = GetNodeString(commandNode["questId"]);
        var questName = questSnapshot != null
            ? GetNodeString(questSnapshot["questName"]) ?? GetNodeString(questSnapshot["name"])
            : null;
        if (string.IsNullOrWhiteSpace(questName))
            questName = !string.IsNullOrWhiteSpace(questId) ? questId : "guardian quest";

        var outcome = GetNodeString(commandNode["outcome"]);
        var title = finalDelta > 0
            ? $"Квест Хранителя усилил Обитель: {questName}"
            : $"Провал квеста ослабил Обитель: {questName}";
        var summary = finalDelta > 0
            ? $"Guardian quest '{questName}' завершён с outcome={outcome}; Сила Обители изменилась на +{finalDelta}."
            : $"Guardian quest '{questName}' завершён с outcome={outcome}; Сила Обители изменилась на {finalDelta}.";
        var audit = CloneObject(questPowerAudit);
        audit["questId"] ??= questId;
        audit["questName"] ??= questName;
        audit["turn"] ??= currentTurn;

        return GuardianPowerEventState.BuildEvent(
            $"guardian_quest_{guardianId}_{questId}_{currentTurn}",
            guardianId,
            finalDelta,
            "guardian_quest",
            "UpdateGuardians.completeQuest",
            questId ?? string.Empty,
            title,
            summary,
            audit);
    }

    private static JsonObject? FindGuardianQuestSnapshot(JsonObject questManagement, string? questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
            return null;

        foreach (var arrayName in new[] { "activeQuests", "availableQuests" })
        {
            if (questManagement[arrayName] is not JsonArray questArray)
                continue;

            foreach (var quest in questArray.OfType<JsonObject>())
            {
                if (string.Equals(GetNodeString(quest["questId"]), questId, StringComparison.OrdinalIgnoreCase))
                    return CloneObject(quest);
            }
        }

        return null;
    }

    private static string NormalizeGuardianQuestDifficulty(string? difficulty) =>
        AbodePowerRules.NormalizeGuardianQuestDifficulty(difficulty);

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

    private async Task<JsonNode?> ReadBackupNodeAsync(string originalRelativePath, IReadOnlyDictionary<string, string>? backups)
    {
        if (backups == null || !backups.TryGetValue(originalRelativePath, out var backupPath))
            return null;

        return await ReadNodeAsync(backupPath);
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

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentEntries(JsonNode? root)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return CloneObject(item);
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.EntriesProperty, GuardianAbodeResidentState.UpdateProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("residentId"))
            yield return CloneObject(obj);
    }

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentInteractionReceipts(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.InteractionReceiptsProperty, GuardianAbodeResidentState.UpdateInteractionReceiptsProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("requestId") && obj.ContainsKey("residentId") && obj.ContainsKey("interactionType"))
            yield return CloneObject(obj);
    }

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentRosterReceipts(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.RosterReceiptsProperty, GuardianAbodeResidentState.UpdateRosterReceiptsProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("requestId") && obj.ContainsKey("guardianId") && obj.ContainsKey("abodeId") && obj.ContainsKey("rosterCount"))
            yield return CloneObject(obj);
    }

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentHistoryLogEntries(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.HistoryLogProperty, GuardianAbodeResidentState.UpdateHistoryLogProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("entryId") && obj.ContainsKey("residentId") && obj.ContainsKey("title"))
            yield return CloneObject(obj);
    }

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentThoughtJournalEntries(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.ThoughtJournalProperty, GuardianAbodeResidentState.UpdateThoughtJournalProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("entryId") && obj.ContainsKey("residentId") && obj.ContainsKey("summary") && obj.ContainsKey("intent"))
            yield return CloneObject(obj);
    }

    private static IEnumerable<JsonObject> CollectGuardianAbodeResidentInteractionLogEntries(JsonNode? root)
    {
        if (root is not JsonObject obj)
            yield break;

        foreach (var propName in new[] { GuardianAbodeResidentState.InteractionLogProperty, GuardianAbodeResidentState.UpdateInteractionLogProperty })
        {
            if (obj[propName] is not JsonArray arr)
                continue;

            foreach (var item in arr.OfType<JsonObject>())
                yield return CloneObject(item);
        }

        if (obj.ContainsKey("entryId") && obj.ContainsKey("residentId") && obj.ContainsKey("summary") && obj.ContainsKey("eventType"))
            yield return CloneObject(obj);
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

}

