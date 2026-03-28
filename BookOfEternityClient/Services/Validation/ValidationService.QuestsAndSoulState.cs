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
    private void ValidateQuestObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        ValidateRequiredNullableStringField(item, itemContext, issues, "questId");
        RequireString(item, itemContext, issues, "questName");
        var status = RequireString(item, itemContext, issues, "status");
        RequireString(item, itemContext, issues, "questGiver");
        RequireString(item, itemContext, issues, "questBackground");
        RequireString(item, itemContext, issues, "description");
        ValidateQuestObjectivesArray(
            item,
            itemContext,
            issues,
            AllowedQuestObjectiveStatuses,
            "Quest",
            "Используй для objectives только status = Active, Completed или Failed.");

        if (!string.IsNullOrWhiteSpace(status) && !AllowedQuestStatuses.Contains(status))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.status",
                IssueSeverity.Error,
                "Quest status должен быть одним из canonical enum значений",
                code: "quest_invalid_status",
                section: "Quest",
                expected: string.Join(" | ", AllowedQuestStatuses),
                actual: status,
                repairHint: "Используй для regular quests только status = Active, Completed, Failed или Updated."));
        }

        if (!item.TryGetProperty("detailsLog", out var detailsLog))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.detailsLog",
                IssueSeverity.Error,
                "Квест должен содержать detailsLog",
                code: "quest_missing_details_log",
                section: "Quests",
                expected: "detailsLog array",
                repairHint: "Для полного Quest Object передавай detailsLog как массив записей истории квеста, даже если он пока пустой."));
            return;
        }
        if (detailsLog.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.detailsLog",
                IssueSeverity.Error,
                "detailsLog должен быть массивом",
                code: "quest_invalid_details_log",
                section: "Quests",
                expected: "array",
                actual: detailsLog.ValueKind.ToString(),
                repairHint: "Передавай detailsLog как массив записей по canonical quest contract."));
            return;
        }
        RequireArrayOfStrings(detailsLog, $"{itemContext}.detailsLog", issues);
        if (detailsLog.ValueKind == JsonValueKind.Array)
        {
            var logIndex = 0;
            foreach (var entry in detailsLog.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String)
                {
                    logIndex++;
                    continue;
                }

                var entryValue = entry.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(entryValue) && !MatchesHistoricalEntryContract(entryValue))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.detailsLog[{logIndex}]",
                        IssueSeverity.Error,
                        "Quest detailsLog entry должен начинаться с canonical turn anchor",
                        code: "quest_details_log_entry_prefix_invalid",
                        section: "Quest",
                        expected: "#[turn_number]. ... or #[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                        actual: entryValue,
                        repairHint: "Для full quest object сохраняй в detailsLog только turn-anchored записи. Минимально допустим legacy prefix '#[turn_number]. ...'; полный historical timestamp тоже допустим."));
                }

                logIndex++;
            }
        }
        ValidateQuestRewardsObject(item, itemContext, issues);
        ValidateOptionalString(item, itemContext, issues, "failureConsequences");
    }


    private void ValidateQuestPartialStateUpdateObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireAnyString(item, itemContext, issues, "questId", "initialId");

        var hasAnyUpdateField =
            HasAnyNonEmptyString(item, "newDetailsLogEntry", "status", "questName", "questGiver", "questBackground", "description", "failureConsequences") ||
            item.TryGetProperty("objectives", out _) ||
            item.TryGetProperty("rewards", out _);
        if (!hasAnyUpdateField)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "Quest partial update должен содержать хотя бы одно изменяемое поле",
                code: "quest_partial_update_missing_changes",
                section: "Quest",
                repairHint: "Для обновления существующего квеста передай questId или same-turn initialId и только реально изменившиеся поля; для append в журнал используй newDetailsLogEntry."));
            return;
        }

        ValidateOptionalString(item, itemContext, issues, "newDetailsLogEntry");
        var status = item.TryGetProperty("status", out _) ? RequireString(item, itemContext, issues, "status") : string.Empty;
        ValidateOptionalString(item, itemContext, issues, "questName");
        ValidateOptionalString(item, itemContext, issues, "questGiver");
        ValidateOptionalString(item, itemContext, issues, "questBackground");
        ValidateOptionalString(item, itemContext, issues, "description");
        ValidateOptionalString(item, itemContext, issues, "failureConsequences");
        if (item.TryGetProperty("objectives", out _))
            ValidateQuestObjectivesArray(
                item,
                itemContext,
                issues,
                AllowedQuestObjectiveStatuses,
                "Quest",
                "Используй для objectives только status = Active, Completed или Failed.");
        ValidateQuestRewardsObject(item, itemContext, issues);

        if (item.TryGetProperty("detailsLog", out _))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.detailsLog",
                IssueSeverity.Error,
                "Quest partial update не должен пересылать detailsLog целиком",
                code: "quest_partial_update_resends_details_log",
                section: "Quest",
                repairHint: "Для дописывания журнала используй questId или same-turn initialId + newDetailsLogEntry; полный detailsLog пересылай только при создании нового квеста."));
        }

        var newDetailsLogEntry = GetFirstNonEmptyString(item, "newDetailsLogEntry");
        if (!string.IsNullOrWhiteSpace(newDetailsLogEntry) &&
            !MatchesHistoricalEntryContract(newDetailsLogEntry))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.newDetailsLogEntry",
                IssueSeverity.Error,
                "newDetailsLogEntry должен начинаться с canonical turn anchor",
                code: "quest_log_entry_prefix_invalid",
                section: "Quest",
                expected: "#[turn_number]. ... or #[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                actual: newDetailsLogEntry,
                repairHint: "Для append-only записи журнала квеста используй canonical historical-entry timestamp из Block 18.A. Legacy prefix '#[turn_number]. ...' всё ещё принимается для старого контента, но не обязателен."));
        }

        if (!string.IsNullOrWhiteSpace(status) && !AllowedQuestStatuses.Contains(status))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.status",
                IssueSeverity.Error,
                "Quest status должен быть одним из canonical enum значений",
                code: "quest_invalid_status",
                section: "Quest",
                expected: string.Join(" | ", AllowedQuestStatuses),
                actual: status,
                repairHint: "Используй для regular quests только status = Active, Completed, Failed или Updated."));
        }
    }


    private void ValidateSoulQuestObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "questId");
        RequireString(item, itemContext, issues, "guardianId");
        RequireString(item, itemContext, issues, "title");
        RequireString(item, itemContext, issues, "description");
        ValidateOptionalString(item, itemContext, issues, "relatedRivalArcId");
        ValidateOptionalString(item, itemContext, issues, "relatedAfterlifeResidentId");
        if (item.TryGetProperty("counterToRivalArc", out _))
            RequireBooleanField(item, itemContext, issues, "counterToRivalArc");
        ValidateQuestObjectivesArray(
            item,
            itemContext,
            issues,
            AllowedSoulQuestObjectiveStatuses,
            "SoulQuests",
            "Используй для soul quest objectives только status = Active, Pending, Completed или Failed.");
        var status = RequireString(item, itemContext, issues, "status");

        if (!string.IsNullOrWhiteSpace(status) && !AllowedSoulQuestStatuses.Contains(status))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.status",
                IssueSeverity.Error,
                "Soul quest status должен быть одним из canonical enum значений",
                code: "soul_quest_invalid_status",
                section: "SoulQuests",
                expected: string.Join(" | ", AllowedSoulQuestStatuses),
                actual: status,
                repairHint: "Используй для soul quests только status = active, completed, failed или abandoned."));
        }

        if (!item.TryGetProperty("progress", out var progress) ||
            !RequireObject(progress, $"{itemContext}.progress", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.progress",
                IssueSeverity.Error,
                "Soul quest должен содержать progress object",
                code: "soul_quest_missing_progress_object",
                section: "SoulQuests",
                expected: "progress object",
                actual: !item.TryGetProperty("progress", out var missingProgressNode) ? "missing" : missingProgressNode.ValueKind.ToString(),
                repairHint: "Добавь в soul quest canonical объект progress с integer-полями completed и total."));
        }
        else
        {
            ValidateNonNegativeIntegerField(progress, $"{itemContext}.progress", issues, "completed", "SoulQuests");
            ValidateNonNegativeIntegerField(progress, $"{itemContext}.progress", issues, "total", "SoulQuests");
        }

        if (!item.TryGetProperty("rewards", out var rewards) ||
            !RequireObject(rewards, $"{itemContext}.rewards", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.rewards",
                IssueSeverity.Error,
                "Soul quest должен содержать rewards object",
                code: "soul_quest_missing_rewards_object",
                section: "SoulQuests",
                expected: "rewards object",
                actual: !item.TryGetProperty("rewards", out var missingRewardsNode) ? "missing" : missingRewardsNode.ValueKind.ToString(),
                repairHint: "Добавь в soul quest canonical объект rewards. Даже минимальные награды должны приходить как object, а не как missing/scalar payload."));
        }
        else
        {
            ValidateNonNegativeIntegerField(rewards, $"{itemContext}.rewards", issues, "inkFeathers", "SoulQuests");
            ValidateNonNegativeIntegerField(rewards, $"{itemContext}.rewards", issues, "enlightenmentExperience", "SoulQuests");
            if (rewards.TryGetProperty("soulRelics", out var soulRelics))
            {
                RequireArrayOfObjects(soulRelics, $"{itemContext}.rewards.soulRelics", issues);
                if (soulRelics.ValueKind == JsonValueKind.Array)
                {
                    var relicIndex = 0;
                    foreach (var relic in soulRelics.EnumerateArray())
                    {
                        var relicContext = $"{itemContext}.rewards.soulRelics[{relicIndex++}]";
                        if (!RequireObject(relic, relicContext, issues))
                            continue;

                        ValidateMinimalSoulRelicObject(relic, relicContext, issues, "SoulQuests");
                    }
                }
            }
            if (rewards.TryGetProperty("reputationChanges", out var reputationChanges))
            {
                RequireArrayOfObjects(reputationChanges, $"{itemContext}.rewards.reputationChanges", issues);
                if (reputationChanges.ValueKind == JsonValueKind.Array)
                {
                    var repIndex = 0;
                    foreach (var repChange in reputationChanges.EnumerateArray())
                    {
                        var repContext = $"{itemContext}.rewards.reputationChanges[{repIndex++}]";
                        if (!RequireObject(repChange, repContext, issues))
                            continue;

                        RequireString(repChange, repContext, issues, "guardianId");
                        ValidateIntegerField(repChange, repContext, issues, "change");
                    }
                }
            }
        }

        RequireBooleanField(item, itemContext, issues, "crossIncarnation");
        if (!item.TryGetProperty("completionTimestamp", out var completionTimestamp) ||
            completionTimestamp.ValueKind == JsonValueKind.Null)
            return;

        if (completionTimestamp.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.completionTimestamp",
                IssueSeverity.Error,
                "completionTimestamp должен быть строкой ISO 8601 timestamp или null",
                code: "soul_quest_completion_timestamp_invalid_type",
                section: "SoulQuests",
                expected: "ISO 8601 timestamp or null",
                actual: completionTimestamp.ValueKind.ToString(),
                repairHint: "Для незавершённого soul quest оставляй completionTimestamp = null. Для завершённого передай completionTimestamp как ISO 8601 строку."));
            return;
        }

        var completionTimestampValue = completionTimestamp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(completionTimestampValue) || !DateTimeOffset.TryParse(completionTimestampValue, out _))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.completionTimestamp",
                IssueSeverity.Error,
                "completionTimestamp должен быть ISO 8601 timestamp или null",
                code: "soul_quest_completion_timestamp_invalid",
                section: "SoulQuests",
                expected: "ISO 8601 timestamp or null",
                actual: string.IsNullOrWhiteSpace(completionTimestampValue) ? "empty string" : completionTimestampValue,
                repairHint: "Для незавершённого soul quest оставляй completionTimestamp = null. Для завершённого передай completionTimestamp как ISO 8601 строку."));
        }
    }


    private void ValidateNpcPersonalQuestObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "questName");
        RequireString(item, itemContext, issues, "questBackground");
        RequireString(item, itemContext, issues, "description");
        var status = RequireString(item, itemContext, issues, "status");
        RequireString(item, itemContext, issues, "source");
        ValidateQuestObjectivesArray(
            item,
            itemContext,
            issues,
            AllowedQuestObjectiveStatuses,
            "NPCQuest",
            "Используй для NPC personal quest objectives только status = Active, Completed или Failed.");
        RequireString(item, itemContext, issues, "rewards");
        RequireString(item, itemContext, issues, "failureConsequences");

        if (!string.IsNullOrWhiteSpace(status) && !AllowedNpcPersonalQuestStatuses.Contains(status))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.status",
                IssueSeverity.Error,
                "NPC personal quest status должен быть одним из canonical enum значений",
                code: "npc_personal_quest_invalid_status",
                section: "NPCQuest",
                expected: string.Join(" | ", AllowedNpcPersonalQuestStatuses),
                actual: status,
                repairHint: "Используй для NPC personal quests только status = Active, Completed, Failed или Abandoned."));
        }
    }


    private void ValidateQuestObjectivesArray(
        JsonElement item,
        string itemContext,
        List<ValidationIssue> issues,
        HashSet<string> allowedStatuses,
        string section,
        string repairHint)
    {
        if (!item.TryGetProperty("objectives", out var objectives))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.objectives",
                IssueSeverity.Error,
                "Квест должен содержать objectives"));
            return;
        }
        if (objectives.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.objectives",
                IssueSeverity.Error,
                "objectives должен быть массивом"));
            return;
        }

        var objectiveIndex = 0;
        foreach (var objective in objectives.EnumerateArray())
        {
            var objectiveContext = $"{itemContext}.objectives[{objectiveIndex++}]";
            if (!RequireObject(objective, objectiveContext, issues))
                continue;

            RequireString(objective, objectiveContext, issues, "description");
            var objectiveStatus = RequireString(objective, objectiveContext, issues, "status");
            ValidateRequiredNullableStringField(objective, objectiveContext, issues, "objectiveId");

            if (!string.IsNullOrWhiteSpace(objectiveStatus) && !allowedStatuses.Contains(objectiveStatus))
            {
                issues.Add(new ValidationIssue(
                    $"{objectiveContext}.status",
                    IssueSeverity.Error,
                    "Quest objective status должен быть одним из canonical enum значений",
                    code: "quest_objective_invalid_status",
                    section: section,
                    expected: string.Join(" | ", allowedStatuses),
                    actual: objectiveStatus,
                    repairHint: repairHint));
            }
        }
    }


    private void ValidateQuestRewardsObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("rewards", out var rewards))
            return;

        if (!RequireObject(rewards, $"{itemContext}.rewards", issues))
            return;

        ValidateNonNegativeIntegerField(rewards, $"{itemContext}.rewards", issues, "experience", "Quest");
        ValidateNonNegativeIntegerField(rewards, $"{itemContext}.rewards", issues, "money", "Quest");
        if (rewards.TryGetProperty("items", out var rewardItems))
            RequireArrayOfStrings(rewardItems, $"{itemContext}.rewards.items", issues);
        ValidateOptionalString(rewards, $"{itemContext}.rewards", issues, "other");
    }


    private static bool LooksLikeFullQuestObject(JsonElement item)
    {
        return HasNonEmptyString(item, "status") &&
               HasNonEmptyString(item, "questGiver") &&
               HasNonEmptyString(item, "questBackground") &&
               HasNonEmptyString(item, "description") &&
               item.TryGetProperty("objectives", out var objectives) &&
               objectives.ValueKind == JsonValueKind.Array &&
               item.TryGetProperty("detailsLog", out var detailsLog) &&
               detailsLog.ValueKind == JsonValueKind.Array;
    }


    private void ValidateQuestArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var isSoulQuestCollection =
            string.Equals(propName, "UpdateSoulQuests", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(propName, "quests", StringComparison.OrdinalIgnoreCase) &&
             contextPrefix.EndsWith("game_state/quests/soul_quests.json", StringComparison.OrdinalIgnoreCase));
        var knownRegularQuestIds =
            string.Equals(propName, "UpdateQuests", StringComparison.OrdinalIgnoreCase)
                ? ReadKnownRegularQuestIdsFromPreTurnSync()
                : null;
        var sameTurnNewQuestInitialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;

            var questId = GetFirstNonEmptyString(item, "questId");
            var initialId = GetFirstNonEmptyString(item, "initialId");
            var isQuestLogAppend = IsQuestPartialLogAppend(item);
            var isQuestPartialStateUpdate = IsQuestPartialStateUpdate(item);
            var isSameTurnInitialLink = !string.IsNullOrWhiteSpace(initialId) && sameTurnNewQuestInitialIds.Contains(initialId);
            var usesInitialIdQuestLink =
                string.IsNullOrWhiteSpace(questId) &&
                !string.IsNullOrWhiteSpace(initialId) &&
                (isQuestLogAppend || isQuestPartialStateUpdate);

            if (!item.TryGetProperty("questId", out _) && !usesInitialIdQuestLink)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.questId",
                    IssueSeverity.Error,
                    "Quest object должен содержать обязательное поле questId",
                    code: "quest_missing_quest_id_field",
                    section: "Quest",
                    expected: "questId field (GUID/string for existing quest, null for genuinely new quest)",
                    actual: "missing",
                    repairHint: "Передай questId явно: используй existing questId для известного квеста или questId = null для genuinely new quest/full object в этом accepted turn."));
            }

            if (knownRegularQuestIds != null)
            {
                if (!string.IsNullOrWhiteSpace(questId) && !knownRegularQuestIds.Contains(questId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.questId",
                        IssueSeverity.Error,
                        "UpdateQuests ссылается на questId, которого нет в pre-turn regular_quests state",
                        code: "quest_update_unknown_existing_quest",
                        section: "Quest",
                        expected: "existing questId from pre-turn regular_quests.json",
                        actual: questId,
                        repairHint: "Для existing quest update используй questId из pre-turn regular_quests.json. Для genuinely new quest оставь questId = null и передай полный Quest Object."));
                }

                if (string.IsNullOrWhiteSpace(questId) &&
                    !string.IsNullOrWhiteSpace(initialId) &&
                    (isQuestLogAppend || isQuestPartialStateUpdate) &&
                    !isSameTurnInitialLink)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialId",
                        IssueSeverity.Error,
                        "Quest partial update использует initialId, который не был создан более ранним full Quest Object в этом же accepted turn",
                        code: "quest_partial_unknown_same_turn_initial_id",
                        section: "Quest",
                        expected: "initialId of a new full quest object created earlier in the same UpdateQuests array",
                        actual: initialId,
                        repairHint: "Для same-turn linking сначала создай новый Quest Object с questId = null и initialId, а уже потом ссылайся на него через initialId в partial update или log append."));
                }
            }

            if (isSoulQuestCollection)
            {
                ValidateSoulQuestObject(item, itemContext, issues);
            }
            else if (isQuestLogAppend)
            {
                ValidateQuestPartialLogUpdateObject(item, itemContext, issues);
            }
            else if (string.Equals(propName, "UpdateQuests", StringComparison.OrdinalIgnoreCase) && isQuestPartialStateUpdate)
            {
                ValidateQuestPartialStateUpdateObject(item, itemContext, issues);
            }
            else
            {
                ValidateQuestObject(item, itemContext, issues);
                if (string.Equals(propName, "UpdateQuests", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(questId) &&
                    !string.IsNullOrWhiteSpace(initialId))
                {
                    sameTurnNewQuestInitialIds.Add(initialId);
                }
            }
        }

        if (isSoulQuestCollection)
        {
            var activeSoulQuestCount = CountMergedActiveSoulQuests(contextPrefix, propName, arr);
            if (activeSoulQuestCount > 8)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{propName}",
                    IssueSeverity.Error,
                    "UpdateSoulQuests не должен активировать более 8 soul quests за одну жизнь",
                    code: "soul_quests_active_cap_exceeded",
                    section: "SoulQuests",
                    expected: "<= 8 active soul quests",
                    actual: activeSoulQuestCount.ToString(),
                repairHint: "Сохрани не более 8 active soul quests. Заверши, отмени или не активируй лишние задачи в этой жизни."));
            }
        }
    }


    private HashSet<string> ReadKnownRegularQuestIdsFromPreTurnSync()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnJson = ReadPreTurnTrackedFileSync("game_state/quests/regular_quests.json");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return ids;

        try
        {
            using var doc = JsonDocument.Parse(preTurnJson);
            foreach (var propName in new[] { "quests", "UpdateQuests" })
            {
                if (!doc.RootElement.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in arr.EnumerateArray())
                {
                    var questId = GetFirstNonEmptyString(item, "questId");
                    if (!string.IsNullOrWhiteSpace(questId))
                        ids.Add(questId);
                }
            }
        }
        catch
        {
            // ignored
        }

        return ids;
    }


    private static bool IsQuestPartialStateUpdate(JsonElement item)
    {
        if (!HasAnyNonEmptyString(item, "questId", "initialId"))
            return false;

        if (item.TryGetProperty("detailsLog", out _))
            return false;

        return !LooksLikeFullQuestObject(item);
    }


    private int CountMergedActiveSoulQuests(string contextPrefix, string propName, JsonElement currentArray)
    {
        static void CollectStatusesFromArray(JsonElement array, Dictionary<string, string> statuses)
        {
            if (array.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var questId = GetFirstNonEmptyString(item, "questId");
                var status = GetFirstNonEmptyString(item, "status");
                if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(status))
                    continue;

                statuses[questId] = status;
            }
        }

        if (string.Equals(propName, "quests", StringComparison.OrdinalIgnoreCase) &&
            contextPrefix.EndsWith("game_state/quests/soul_quests.json", StringComparison.OrdinalIgnoreCase))
        {
            return currentArray.EnumerateArray().Count(item =>
                item.ValueKind == JsonValueKind.Object &&
                string.Equals(GetFirstNonEmptyString(item, "status"), "active", StringComparison.OrdinalIgnoreCase));
        }

        var mergedStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var preTurnJson = ReadPreTurnTrackedFileSync("game_state/quests/soul_quests.json");
        if (!string.IsNullOrWhiteSpace(preTurnJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                if (doc.RootElement.TryGetProperty("quests", out var storedQuests))
                    CollectStatusesFromArray(storedQuests, mergedStatuses);
                else if (doc.RootElement.TryGetProperty("UpdateSoulQuests", out var legacyUpdates))
                    CollectStatusesFromArray(legacyUpdates, mergedStatuses);
            }
            catch
            {
                // Ignore malformed pre-turn snapshot here; generic state validation will surface it separately.
            }
        }

        CollectStatusesFromArray(currentArray, mergedStatuses);
        return mergedStatuses.Values.Count(status => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase));
    }


    private void ValidateQuestHistoryData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("questHistory", out var questHistory))
        {
            if (questHistory.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.questHistory",
                    IssueSeverity.Error,
                    "questHistory должен быть массивом",
                    code: "quest_history_invalid_array",
                    section: "QuestHistory",
                    expected: "array of quest history entries",
                    actual: questHistory.ValueKind.ToString(),
                    repairHint: "Сохраняй questHistory как массив записей завершённых или заархивированных квестов, а не как scalar/object заглушку."));
            }
            else
            {
                var index = 0;
                foreach (var item in questHistory.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.questHistory[{index++}]";
                    if (!RequireObject(item, itemContext, issues))
                        continue;

                    if (!HasAnyNonEmptyString(item, "questId", "questName", "title", "name"))
                    {
                        issues.Add(new ValidationIssue(
                            itemContext,
                            IssueSeverity.Error,
                            "История квеста должна содержать questId или title/name",
                            code: "quest_history_entry_missing_identity",
                            section: "QuestHistory",
                            expected: "questId or questName/title/name",
                            actual: "missing identity fields",
                            repairHint: "Для каждой записи questHistory передай questId и/или читаемое имя квеста, чтобы запись можно было однозначно связать с canonical quest history."));
                    }

                    RequireString(item, itemContext, issues, "outcome");
                    ValidateOptionalString(item, itemContext, issues, "completionDate");
                    ValidateOptionalString(item, itemContext, issues, "reputation");
                    ValidateNonNegativeNumberField(item, itemContext, issues, "experience");
                    ValidateNonNegativeNumberField(item, itemContext, issues, "incarnationNumber");
                }
            }
        }

        if (root.TryGetProperty("questRewards", out var questRewards))
        {
            if (questRewards.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.questRewards",
                    IssueSeverity.Error,
                    "questRewards должен быть массивом",
                    code: "quest_rewards_invalid_array",
                    section: "QuestHistory",
                    expected: "array of quest reward records",
                    actual: questRewards.ValueKind.ToString(),
                    repairHint: "Сохраняй questRewards как массив reward records, а не как scalar/object заглушку."));
            }
            else
            {
                var index = 0;
                foreach (var item in questRewards.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.questRewards[{index++}]";
                    if (!RequireObject(item, itemContext, issues))
                        continue;

                    RequireString(item, itemContext, issues, "questId");
                    if (item.TryGetProperty("itemsReceived", out var itemsReceived))
                        RequireArrayOfStrings(itemsReceived, $"{itemContext}.itemsReceived", issues);
                    if (item.TryGetProperty("skillsUnlocked", out var skillsUnlocked))
                        RequireArrayOfStrings(skillsUnlocked, $"{itemContext}.skillsUnlocked", issues);
                    if (item.TryGetProperty("relationshipChanges", out var relationshipChanges))
                        RequireArrayOfStrings(relationshipChanges, $"{itemContext}.relationshipChanges", issues);
                }
            }
        }

        if (root.TryGetProperty("questChains", out var questChains))
        {
            if (questChains.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.questChains",
                    IssueSeverity.Error,
                    "questChains должен быть массивом",
                    code: "quest_chains_invalid_array",
                    section: "QuestHistory",
                    expected: "array of quest chain records",
                    actual: questChains.ValueKind.ToString(),
                    repairHint: "Сохраняй questChains как массив chain records, а не как scalar/object заглушку."));
            }
            else
            {
                var index = 0;
                foreach (var item in questChains.EnumerateArray())
                {
                    var itemContext = $"{contextPrefix}.questChains[{index++}]";
                    if (!RequireObject(item, itemContext, issues))
                        continue;

                    RequireString(item, itemContext, issues, "chainId");
                    RequireString(item, itemContext, issues, "currentQuest");
                    ValidateOptionalString(item, itemContext, issues, "progress");
                    if (item.TryGetProperty("unlocked", out var unlocked) &&
                        unlocked.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.unlocked",
                            IssueSeverity.Error,
                            "questChains.unlocked должен быть boolean",
                            code: "quest_chain_unlocked_invalid",
                            section: "QuestHistory",
                            expected: "true or false",
                            actual: unlocked.ValueKind.ToString(),
                            repairHint: "Сохраняй questChains.unlocked как boolean-флаг, а не как число/строку."));
                    }
                }
            }
        }
    }


    private void ValidateQuestLog(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("questLog", out var value))
            return;

        var context = $"{contextPrefix}.questLog";
        if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind is not (JsonValueKind.Object or JsonValueKind.String))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}[{index}]",
                        IssueSeverity.Error,
                        "Элемент questLog должен быть объектом или строкой",
                        code: "quest_log_entry_invalid_shape",
                        section: "QuestHistory",
                        expected: "questLog entry as object or string",
                        actual: item.ValueKind.ToString(),
                        repairHint: "В legacy questLog используй либо строковые заметки, либо object entries. Не смешивай с number/bool/null."));
                }
                index++;
            }
            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "questLog должен быть объектом или массивом",
                code: "quest_log_invalid_shape",
                section: "QuestHistory",
                expected: "legacy questLog object or array",
                actual: value.ValueKind.ToString(),
                repairHint: "Если используешь legacy questLog shorthand, сохраняй его как object или array. Для canonical stored shape предпочитай questHistory, questRewards и questChains."));
        }
    }


    private static bool IsQuestPartialLogAppend(JsonElement item)
    {
        return HasAnyNonEmptyString(item, "questId", "initialId") &&
               item.TryGetProperty("newDetailsLogEntry", out var newEntry) &&
               newEntry.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(newEntry.GetString());
    }


    private void ValidateQuestPartialLogUpdateObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireAnyString(item, itemContext, issues, "questId", "initialId");
        var newDetailsLogEntry = RequireString(item, itemContext, issues, "newDetailsLogEntry");

        if (!string.IsNullOrWhiteSpace(newDetailsLogEntry) &&
            !MatchesHistoricalEntryContract(newDetailsLogEntry))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.newDetailsLogEntry",
                IssueSeverity.Error,
                "newDetailsLogEntry должен начинаться с canonical turn anchor",
                code: "quest_log_entry_prefix_invalid",
                section: "Quest",
                expected: "#[turn_number]. ... or #[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                actual: newDetailsLogEntry,
                repairHint: "Для append-only записи журнала квеста используй canonical historical-entry timestamp из Block 18.A. Legacy prefix '#[turn_number]. ...' всё ещё принимается для старого контента, но не обязателен."));
        }

        if (item.TryGetProperty("detailsLog", out _))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.detailsLog",
                IssueSeverity.Error,
                "Quest partial log update не должен пересылать detailsLog вместе с newDetailsLogEntry",
                code: "quest_partial_update_resends_details_log",
                section: "Quest",
                repairHint: "Для append-only обновления журнала отправляй questId или same-turn initialId вместе с newDetailsLogEntry без полного detailsLog."));
        }
    }


    private void ValidatePlotOutline(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("plotOutline", out var value))
            return;

        var context = $"{contextPrefix}.plotOutline";
        if (!RequireObject(value, context, issues))
            return;

        if (!value.TryGetProperty("mainArc", out var mainArc) || !RequireObject(mainArc, $"{context}.mainArc", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.mainArc",
                IssueSeverity.Error,
                "plotOutline должен содержать объект mainArc",
                code: "plot_outline_missing_main_arc",
                section: "PlotOutline",
                expected: "mainArc object",
                actual: value.TryGetProperty("mainArc", out var actualMainArc) ? actualMainArc.ValueKind.ToString() : "missing",
                repairHint: "Сохрани canonical plotOutline object с обязательным mainArc, characterSubplots, loomingThreatsOrOpportunities и lastUpdatedTurn."));
            return;
        }

        RequireString(mainArc, $"{context}.mainArc", issues, "summary");
        RequireString(mainArc, $"{context}.mainArc", issues, "nextImmediateStep");
        RequireString(mainArc, $"{context}.mainArc", issues, "potentialClimax");

        if (!TryGetArray(value, "characterSubplots", $"{context}.characterSubplots", issues, out var characterSubplots))
            return;

        var subplotIndex = 0;
        foreach (var subplot in characterSubplots.EnumerateArray())
        {
            var subplotContext = $"{context}.characterSubplots[{subplotIndex++}]";
            if (!RequireObject(subplot, subplotContext, issues))
                continue;

            RequireString(subplot, subplotContext, issues, "characterName");
            RequireString(subplot, subplotContext, issues, "arcSummary");
            RequireString(subplot, subplotContext, issues, "nextStep");
            RequireString(subplot, subplotContext, issues, "potentialConflictOrResolution");
        }

        if (!value.TryGetProperty("loomingThreatsOrOpportunities", out var threats) || threats.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.loomingThreatsOrOpportunities",
                IssueSeverity.Error,
                "plotOutline должен содержать массив loomingThreatsOrOpportunities",
                code: "plot_outline_missing_threats",
                section: "PlotOutline",
                expected: "array of strings",
                actual: value.TryGetProperty("loomingThreatsOrOpportunities", out var actualThreats) ? actualThreats.ValueKind.ToString() : "missing",
                repairHint: "Заполни loomingThreatsOrOpportunities массивом строк с текущими фоновыми угрозами или возможностями."));
        }
        else
        {
            RequireArrayOfStrings(threats, $"{context}.loomingThreatsOrOpportunities", issues);
        }

        if (!value.TryGetProperty("lastUpdatedTurn", out var lastUpdatedTurn))
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastUpdatedTurn",
                IssueSeverity.Error,
                "plotOutline должен содержать integer lastUpdatedTurn",
                code: "plot_outline_missing_last_updated_turn",
                section: "PlotOutline",
                expected: "integer lastUpdatedTurn",
                actual: "missing",
                repairHint: "Сохрани в plotOutline текущий integer turn number в поле lastUpdatedTurn."));
        }
        else if (lastUpdatedTurn.ValueKind != JsonValueKind.Number || !lastUpdatedTurn.TryGetInt32(out var parsedTurn) || parsedTurn < 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastUpdatedTurn",
                IssueSeverity.Error,
                "plotOutline.lastUpdatedTurn должен быть неотрицательным integer turn number",
                code: "plot_outline_invalid_last_updated_turn",
                section: "PlotOutline",
                expected: "non-negative integer",
                actual: lastUpdatedTurn.ValueKind == JsonValueKind.Number ? lastUpdatedTurn.ToString() : lastUpdatedTurn.ValueKind.ToString(),
                repairHint: "Передай в lastUpdatedTurn текущий номер хода как неотрицательное целое число."));
        }
    }
}
