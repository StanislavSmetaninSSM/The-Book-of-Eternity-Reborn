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
    private void ValidateGuardianCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("UpdateGuardians", out var updates))
            return;
        if (updates.ValueKind == JsonValueKind.Null)
            return;
        if (!TryGetArray(root, "UpdateGuardians", $"{contextPrefix}.UpdateGuardians", issues, out var arr))
            return;

        var knownGuardianIds = CollectKnownGuardianIds(root);
        var guardianSequentialStates = CollectKnownGuardianSequentialStatesForCommandValidation();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.UpdateGuardians[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            var command = RequireString(item, itemContext, issues, "command");

            if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase))
            {
                if (!item.TryGetProperty("data", out var data) || !RequireObject(data, $"{itemContext}.data", issues))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.data",
                        IssueSeverity.Error,
                        "UpdateGuardians.create должен использовать nested data object с полным объектом Хранителя",
                        code: "guardian_create_missing_data",
                        section: "UpdateGuardians.create",
                        expected: "Nested data object",
                        actual: item.TryGetProperty("data", out var actualData) ? actualData.ValueKind.ToString() : "missing",
                        repairHint: "Используй create как special-case команду: command=create и data={ полный Guardian object }."));
                    continue;
                }

                var issuesBeforeCreateValidation = issues.Count;
                ValidateGuardianCanonicalObject(data, $"{itemContext}.data", issues);
                var createdGuardianId = GetFirstNonEmptyString(data, "guardianId");
                if (issues.Count == issuesBeforeCreateValidation &&
                    !string.IsNullOrWhiteSpace(createdGuardianId))
                {
                    knownGuardianIds.Add(createdGuardianId);
                    guardianSequentialStates[createdGuardianId] = ParseGuardianSequentialState(data);
                }
                continue;
            }

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.guardianId",
                    IssueSeverity.Error,
                    $"UpdateGuardians.{command} не может ссылаться на неизвестный guardianId '{guardianId}'",
                    code: "guardian_non_create_unknown_guardian",
                    section: $"UpdateGuardians.{command}",
                    expected: "existing guardianId from guardians.json or earlier create command in the same array",
                    actual: guardianId,
                    repairHint: "Используй существующий guardianId или сначала создай нового Хранителя через UpdateGuardians.create раньше в этом же массиве."));
            }

            switch (command)
            {
                case "updateReputation":
                    ValidateIntegerField(item, itemContext, issues, "reputationChange");
                    RequireString(item, itemContext, issues, "reason");
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        guardianSequentialStates.TryGetValue(guardianId, out var guardianState) &&
                        item.TryGetProperty("reputationChange", out var reputationDeltaNode) &&
                        reputationDeltaNode.ValueKind == JsonValueKind.Number &&
                        reputationDeltaNode.TryGetInt32(out var reputationDelta) &&
                        guardianState.CurrentReputation.HasValue)
                    {
                        guardianState.CurrentReputation += reputationDelta;
                    }
                    RejectLegacyGuardianDataShape(item, itemContext, issues, command,
                        "Top-level reputationChange + reason",
                        "Убери data и вынеси reputationChange/reason на верхний уровень updateReputation.");
                    break;

                case "completeQuest":
                    var questId = RequireString(item, itemContext, issues, "questId");
                    var outcome = RequireString(item, itemContext, issues, "outcome");
                    if (!string.IsNullOrWhiteSpace(outcome) && !AllowedGuardianQuestOutcomes.Contains(outcome))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.outcome",
                            IssueSeverity.Error,
                            "completeQuest.outcome должен быть success, failure или partial",
                            code: "guardian_complete_quest_invalid_outcome",
                            section: "UpdateGuardians.completeQuest",
                            expected: "success | failure | partial",
                            actual: outcome));
                    }
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        !string.IsNullOrWhiteSpace(questId) &&
                        guardianSequentialStates.TryGetValue(guardianId, out var questState))
                    {
                        var knownQuest =
                            questState.AvailableQuestIds.Contains(questId) ||
                            questState.ActiveQuestIds.Contains(questId);
                        if (!knownQuest)
                        {
                            issues.Add(new ValidationIssue(
                                $"{itemContext}.questId",
                                IssueSeverity.Error,
                                "UpdateGuardians.completeQuest ссылается на questId, которого нет в canonical questManagement этого Хранителя",
                                code: "guardian_complete_quest_unknown_quest_id",
                                section: "UpdateGuardians.completeQuest",
                                expected: "questId from this Guardian's tracked questManagement state",
                                actual: questId,
                                repairHint: "Завершай только тот guardian quest, который реально существует у этого Хранителя в canonical questManagement. Сначала добавь квест в questManagement этого Хранителя, затем закрывай его через completeQuest."));
                        }
                        else
                        {
                            questState.AvailableQuestIds.Remove(questId);
                            questState.ActiveQuestIds.Remove(questId);
                        }

                        ValidateGuardianQuestPowerAudit(
                            item,
                            itemContext,
                            guardianId,
                            questId,
                            outcome,
                            questState,
                            issues);
                    }
                    else
                    {
                        ValidateGuardianQuestPowerAudit(
                            item,
                            itemContext,
                            guardianId,
                            questId,
                            outcome,
                            null,
                            issues);
                    }
                    RejectLegacyGuardianDataShape(item, itemContext, issues, command,
                        "Top-level questId + outcome",
                        "Убери data и вынеси questId/outcome на верхний уровень completeQuest.");
                    break;

                case "processGacha":
                    ValidatePositiveNumberField(item, itemContext, issues, "inkFeathersSpent");
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        guardianSequentialStates.TryGetValue(guardianId, out var gachaState) &&
                        gachaState.CurrentReputation.HasValue)
                    {
                        var chargesPerReturn = GetExpectedGuardianGachaCharges(gachaState.CurrentReputation.Value, gachaState.CurrentAbodePower);
                        if (gachaState.ChargesUsedThisReturn >= chargesPerReturn)
                        {
                            issues.Add(new ValidationIssue(
                                $"{itemContext}.guardianId",
                                IssueSeverity.Error,
                                "processGacha нельзя вызывать для Хранителя без оставшихся charges в текущем return cycle",
                                code: "guardian_process_gacha_no_remaining_charges",
                                section: "UpdateGuardians.processGacha",
                                expected: $"chargesUsedThisReturn < chargesPerReturn ({gachaState.ChargesUsedThisReturn} < {chargesPerReturn})",
                                actual: $"chargesUsedThisReturn={gachaState.ChargesUsedThisReturn}, chargesPerReturn={chargesPerReturn}, currentReputation={gachaState.CurrentReputation.Value}",
                                repairHint: "Не эмить processGacha, если у этого Хранителя уже нет оставшихся попыток в текущем возвращении. Используй другого Хранителя или direct /gacha без guardian-mediated command."));
                        }
                        else
                        {
                            gachaState.ChargesUsedThisReturn++;
                        }
                    }

                    if (!item.TryGetProperty("result", out var result) || !RequireObject(result, $"{itemContext}.result", issues))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.result",
                            IssueSeverity.Error,
                            "processGacha должен использовать top-level поле result с объектом реликвии",
                            code: "guardian_process_gacha_missing_result",
                            section: "UpdateGuardians.processGacha",
                            expected: "Top-level result object",
                            actual: item.TryGetProperty("result", out var actualResult) ? actualResult.ValueKind.ToString() : "missing",
                            repairHint: "Используй processGacha с top-level полями inkFeathersSpent и result, а не legacy nested data shape."));
                    }
                    else
                    {
                        ValidateMinimalSoulRelicObject(result, $"{itemContext}.result", issues, "UpdateGuardians.processGacha");

                        var baseRarity = TryReadCurrentTurnGachaBaseRaritySync();
                        var finalRarity = GetFirstNonEmptyString(result, "rarity", "quality");
                        if (!string.IsNullOrWhiteSpace(baseRarity))
                        {
                            if (string.IsNullOrWhiteSpace(finalRarity))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{itemContext}.result.rarity",
                                    IssueSeverity.Error,
                                    "processGacha.result должен явно сохранять final rarity реликвии",
                                    code: "guardian_process_gacha_missing_result_rarity",
                                    section: "UpdateGuardians.processGacha",
                                    expected: $"rarity >= {baseRarity}",
                                    actual: "missing rarity/quality",
                                    repairHint: "Сохрани в result финальную редкость реликвии и не опускай её ниже client-computed gachaBaseResult.baseRarity."));
                            }
                            else if (GetRarityRank(finalRarity) < GetRarityRank(baseRarity))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{itemContext}.result.rarity",
                                    IssueSeverity.Error,
                                    "processGacha не может понизить редкость ниже client-computed gachaBaseResult.baseRarity",
                                    code: "guardian_process_gacha_result_below_base_rarity",
                                    section: "UpdateGuardians.processGacha",
                                    expected: $">= {baseRarity}",
                                    actual: finalRarity,
                                    repairHint: "Используй gachaBaseResult.baseRarity как минимум. Guardian modifiers могут только повышать итоговую редкость, но не понижать её."));
                            }
                        }

                        var currentPower = guardianSequentialStates.TryGetValue(guardianId ?? "", out var processGachaState)
                            ? processGachaState.CurrentAbodePower
                            : 0;
                        ValidateGuardianGachaBonusAudit(
                            item,
                            itemContext,
                            guardianId ?? string.Empty,
                            baseRarity,
                            finalRarity,
                            currentPower,
                            issues);
                    }

                    RejectLegacyGuardianDataShape(item, itemContext, issues, command,
                        "Top-level inkFeathersSpent + result",
                        "Убери data и вынеси inkFeathersSpent/result на верхний уровень processGacha.");
                    break;

                case "addMusings":
                    ValidateGuardianMusingsCommand(item, itemContext, issues);
                    break;

                case "updateProject":
                    issues.Add(new ValidationIssue(
                        itemContext,
                        IssueSeverity.Error,
                        "Guardian project lifecycle больше не поддерживается через UpdateGuardians.updateProject",
                        code: "guardian_legacy_update_project_command_forbidden",
                        section: "UpdateGuardians.updateProject",
                        expected: "startGuardianProjects / guardianProjectUpdates / completeGuardianProjects",
                        actual: "legacy UpdateGuardians.updateProject",
                        repairHint: "Вынеси lifecycle проекта в отдельные top-level surfaces: startGuardianProjects, guardianProjectUpdates и completeGuardianProjects. UpdateGuardians больше не является source-of-truth для project tracker logic."));
                    break;

                case "unlockLore":
                    if (!item.TryGetProperty("loreFragment", out var loreFragment) ||
                        !RequireObject(loreFragment, $"{itemContext}.loreFragment", issues))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.loreFragment",
                            IssueSeverity.Error,
                            "unlockLore должен содержать top-level объект loreFragment",
                            code: "guardian_unlock_lore_missing_fragment",
                            section: "UpdateGuardians.unlockLore",
                            expected: "Top-level loreFragment object",
                            actual: item.TryGetProperty("loreFragment", out var actualFragment) ? actualFragment.ValueKind.ToString() : "missing",
                            repairHint: "Передай unlockLore как top-level loreFragment object с fragmentId, category, title, content и requiredReputation, а не как произвольный текст или nested legacy shape."));
                    }
                    else
                    {
                        ValidateGuardianLoreFragmentObject(loreFragment, $"{itemContext}.loreFragment", issues, allowNullableContent: false);
                    }
                    break;

                case "setMood":
                    if (!item.TryGetProperty("mood", out var mood) ||
                        !RequireObject(mood, $"{itemContext}.mood", issues))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.mood",
                            IssueSeverity.Error,
                            "setMood должен содержать top-level объект mood",
                            code: "guardian_set_mood_missing_object",
                            section: "UpdateGuardians.setMood",
                            expected: "Top-level mood object",
                            actual: item.TryGetProperty("mood", out var actualMood) ? actualMood.ValueKind.ToString() : "missing",
                            repairHint: "Передай setMood как top-level mood object с current, intensity, reason и since, чтобы canonical guardian mood можно было синхронно обновить."));
                    }
                    else
                    {
                        ValidateGuardianMoodObject(mood, $"{itemContext}.mood", issues);
                    }
                    break;

                default:
                    issues.Add(new ValidationIssue(
                        itemContext,
                        IssueSeverity.Error,
                        $"Неподдерживаемая guardian command: {command}",
                        code: "guardian_unsupported_command",
                        section: "UpdateGuardians",
                        expected: "Supported commands: create, updateReputation, completeQuest, processGacha, addMusings, unlockLore, setMood",
                        actual: command,
                        repairHint: "Используй только те guardian commands, которые реально поддерживаются current CLI contract."));
                    break;
            }
        }
    }


    private void RejectLegacyGuardianDataShape(JsonElement item, string itemContext, List<ValidationIssue> issues,
        string command, string expected, string repairHint)
    {
        if (!item.TryGetProperty("data", out _))
            return;

        issues.Add(new ValidationIssue(
            itemContext,
            IssueSeverity.Error,
            $"Legacy nested data shape больше не допустим для UpdateGuardians.{command}",
            code: $"guardian_{command.ToLowerInvariant()}_legacy_shape",
            section: $"UpdateGuardians.{command}",
            expected: expected,
            actual: "Nested data object",
            repairHint: repairHint));
    }


    private HashSet<string> CollectKnownGuardianIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preTurnGuardiansJson = ReadPreTurnTrackedFileSync("game_state/meta/guardians.json");
        if (!string.IsNullOrWhiteSpace(preTurnGuardiansJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnGuardiansJson);
                CollectGuardianIdsFromStateRoot(doc.RootElement, ids, includeCommandSurfaces: false);
            }
            catch
            {
                // ignored
            }
        }

        var ignoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectCreatedGuardianReferencesFromStateRoot(root, ids, ignoredNames);

        return ids;
    }


    private static void CollectGuardianIdsFromStateRoot(JsonElement root, HashSet<string> ids, bool includeCommandSurfaces = true)
    {
        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
            {
                var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                if (!string.IsNullOrWhiteSpace(guardianId))
                    ids.Add(guardianId);
            }
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
        {
            var guardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id");
                if (!string.IsNullOrWhiteSpace(guardianId))
                    ids.Add(guardianId);
        }

        if (includeCommandSurfaces &&
            root.TryGetProperty("UpdateGuardians", out var updates) &&
            updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var command in updates.EnumerateArray())
            {
                if (command.ValueKind != JsonValueKind.Object)
                    continue;

                var commandName = GetFirstNonEmptyString(command, "command");
                var payload = string.Equals(commandName, "create", StringComparison.OrdinalIgnoreCase) &&
                              command.TryGetProperty("data", out var data) &&
                              data.ValueKind == JsonValueKind.Object
                    ? data
                    : command;

                var guardianId = GetFirstNonEmptyString(payload, "guardianId", "id");
                if (!string.IsNullOrWhiteSpace(guardianId))
                    ids.Add(guardianId);
            }
        }
    }


    private void ValidateGuardianMusingsCommand(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("musings", out var musings))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.musings",
                IssueSeverity.Error,
                "addMusings должен содержать top-level массив musings",
                code: "guardian_add_musings_missing_array",
                section: "UpdateGuardians.addMusings",
                expected: "Top-level musings array",
                actual: "missing musings",
                repairHint: "Передай addMusings как массив musings[] с объектами turn/topic/mood/text, а не как одиночную строку или nested data shape."));
            return;
        }

        RequireArrayOfObjects(musings, $"{itemContext}.musings", issues);
        if (musings.ValueKind != JsonValueKind.Array)
            return;

        if (musings.GetArrayLength() == 0 || musings.GetArrayLength() > 2)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.musings",
                IssueSeverity.Error,
                "addMusings должен содержать 1 или 2 новых размышления Хранителя за ход",
                code: "guardian_add_musings_invalid_count",
                section: "UpdateGuardians.addMusings",
                expected: "1..2 musings per turn",
                actual: musings.GetArrayLength().ToString(),
                repairHint: "Передавай в addMusings только 1-2 новые записи за текущий ход. Старые размышления хранятся в canonical guardians.json и не должны пересылаться заново."));
            if (musings.GetArrayLength() == 0)
                return;
        }

        var index = 0;
        foreach (var musing in musings.EnumerateArray())
        {
            var musingContext = $"{itemContext}.musings[{index++}]";
            if (!RequireObject(musing, musingContext, issues))
                continue;

            if (!musing.TryGetProperty("turn", out var turn) ||
                turn.ValueKind != JsonValueKind.Number ||
                !turn.TryGetInt32(out _))
            {
                issues.Add(new ValidationIssue(
                    $"{musingContext}.turn",
                    IssueSeverity.Error,
                    "Guardian musing должен содержать integer turn",
                    code: "guardian_musing_missing_turn",
                    section: "UpdateGuardians.addMusings",
                    expected: "Integer turn",
                    actual: musing.TryGetProperty("turn", out var actualTurn) ? actualTurn.ValueKind.ToString() : "missing",
                    repairHint: "Каждая запись musings[] должна явно указывать номер хода в поле turn, чтобы журнал размышлений был отсортирован и воспроизводим."));
            }

            var topic = RequireString(musing, musingContext, issues, "topic");
            var mood = RequireString(musing, musingContext, issues, "mood");
            if (!string.IsNullOrWhiteSpace(topic) && !AllowedGuardianMusingTopics.Contains(topic))
            {
                issues.Add(new ValidationIssue(
                    $"{musingContext}.topic",
                    IssueSeverity.Error,
                    "Guardian musing.topic должен быть одним из canonical topic enums",
                    code: "guardian_musing_invalid_topic",
                    section: "UpdateGuardians.addMusings",
                    expected: string.Join(" | ", AllowedGuardianMusingTopics),
                    actual: topic,
                    repairHint: "Используй topic только из guardian inner-life contract: soul_assessment, domain_insight, guardian_politics, chaos_sea, personal_reflection, quest_planning."));
            }

            if (!string.IsNullOrWhiteSpace(mood) && !AllowedGuardianMusingMoods.Contains(mood))
            {
                issues.Add(new ValidationIssue(
                    $"{musingContext}.mood",
                    IssueSeverity.Error,
                    "Guardian musing.mood должен быть одним из canonical mood enums",
                    code: "guardian_musing_invalid_mood",
                    section: "UpdateGuardians.addMusings",
                    expected: string.Join(" | ", AllowedGuardianMusingMoods),
                    actual: mood,
                    repairHint: "Используй mood только из documented musings mood palette, а не произвольную строку."));
            }
            if (!HasAnyNonEmptyString(musing, "thought", "text"))
            {
                issues.Add(new ValidationIssue(
                    musingContext,
                    IssueSeverity.Error,
                    "Guardian musing должен содержать thought или text",
                    code: "guardian_musing_missing_text",
                    section: "UpdateGuardians.addMusings",
                    expected: "thought or text",
                    actual: "missing textual musing payload",
                    repairHint: "Заполни thought или text у каждой записи musings[], чтобы клиенту было что показывать в журнале размышлений Хранителя."));
            }
        }
    }


    private void ValidateGuardianStoredInnerLifeState(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (guardian.TryGetProperty("currentProject", out var currentProject) &&
            currentProject.ValueKind != JsonValueKind.Null)
        {
            RequireObject(currentProject, $"{guardianContext}.currentProject", issues);
        }

        if (!guardian.TryGetProperty("mood", out var mood) || mood.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.mood",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать mood object",
                code: "guardian_state_missing_mood",
                section: "Guardians",
                repairHint: "Сохраняй в guardian state текущий mood object с current, intensity, reason и при наличии since."));
        }
        else if (RequireObject(mood, $"{guardianContext}.mood", issues))
        {
            ValidateGuardianMoodObject(mood, $"{guardianContext}.mood", issues);
        }

        if (!guardian.TryGetProperty("loreFragments", out var loreFragments) || loreFragments.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.loreFragments",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать pre-planned loreFragments array",
                code: "guardian_state_missing_lore_fragments",
                section: "Guardians",
                repairHint: "Сохраняй в guardian state массив loreFragments с полным набором pre-planned fragment objects."));
        }
        else
        {
            RequireArrayOfObjects(loreFragments, $"{guardianContext}.loreFragments", issues);
            if (loreFragments.ValueKind == JsonValueKind.Array)
            {
                if (loreFragments.GetArrayLength() < 7)
                {
                    issues.Add(new ValidationIssue(
                        $"{guardianContext}.loreFragments",
                        IssueSeverity.Error,
                        "Canonical guardian state должен хранить как минимум 7 pre-planned lore fragments",
                        code: "guardian_state_lore_fragments_below_minimum",
                        section: "Guardians",
                        expected: ">= 7 lore fragments",
                        actual: loreFragments.GetArrayLength().ToString(),
                        repairHint: "Добавь pre-planned lore fragments в guardian state до минимального набора из 7 записей."));
                }

                var index = 0;
                foreach (var loreFragment in loreFragments.EnumerateArray())
                    ValidateGuardianLoreFragmentObject(loreFragment, $"{guardianContext}.loreFragments[{index++}]", issues, allowNullableContent: true);
            }
        }

        if (guardian.TryGetProperty("musings", out var musings) &&
            musings.ValueKind != JsonValueKind.Null)
        {
            RequireArrayOfObjects(musings, $"{guardianContext}.musings", issues);
            if (musings.ValueKind == JsonValueKind.Array)
            {
                if (musings.GetArrayLength() > 15)
                {
                    issues.Add(new ValidationIssue(
                        $"{guardianContext}.musings",
                        IssueSeverity.Error,
                        "Canonical guardian musings не должны хранить более 15 последних записей",
                        code: "guardian_state_musings_limit_exceeded",
                        section: "Guardians",
                        expected: "<= 15 musings entries",
                        actual: musings.GetArrayLength().ToString(),
                        repairHint: "Обрежь canonical musings[] до 15 самых новых записей, как требует Guardian inner-life contract."));
                }

                var index = 0;
                foreach (var musing in musings.EnumerateArray())
                {
                    var musingContext = $"{guardianContext}.musings[{index++}]";
                    if (!RequireObject(musing, musingContext, issues))
                        continue;

                    if (!musing.TryGetProperty("turn", out var turn) ||
                        turn.ValueKind != JsonValueKind.Number ||
                        !turn.TryGetInt32(out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{musingContext}.turn",
                            IssueSeverity.Error,
                            "Guardian musing должен содержать integer turn",
                            code: "guardian_musing_missing_turn",
                            section: "Guardians",
                            expected: "Integer turn",
                            actual: musing.TryGetProperty("turn", out var actualTurn) ? actualTurn.ValueKind.ToString() : "missing",
                            repairHint: "Каждая canonical musings[] entry должна явно указывать turn, чтобы журнал размышлений был отсортирован и воспроизводим."));
                    }

                    var topic = RequireString(musing, musingContext, issues, "topic");
                    var moodValue = RequireString(musing, musingContext, issues, "mood");
                    if (!string.IsNullOrWhiteSpace(topic) && !AllowedGuardianMusingTopics.Contains(topic))
                    {
                        issues.Add(new ValidationIssue(
                            $"{musingContext}.topic",
                            IssueSeverity.Error,
                            "Guardian musing.topic должен быть одним из canonical topic enums",
                            code: "guardian_musing_invalid_topic",
                            section: "Guardians",
                            expected: string.Join(" | ", AllowedGuardianMusingTopics),
                            actual: topic,
                            repairHint: "Используй topic только из guardian inner-life contract: soul_assessment, domain_insight, guardian_politics, chaos_sea, personal_reflection, quest_planning."));
                    }

                    if (!string.IsNullOrWhiteSpace(moodValue) && !AllowedGuardianMusingMoods.Contains(moodValue))
                    {
                        issues.Add(new ValidationIssue(
                            $"{musingContext}.mood",
                            IssueSeverity.Error,
                            "Guardian musing.mood должен быть одним из canonical mood enums",
                            code: "guardian_musing_invalid_mood",
                            section: "Guardians",
                            expected: string.Join(" | ", AllowedGuardianMusingMoods),
                            actual: moodValue,
                            repairHint: "Используй mood только из documented musings mood palette, а не произвольную строку."));
                    }

                    if (!HasAnyNonEmptyString(musing, "thought", "text"))
                    {
                        issues.Add(new ValidationIssue(
                            musingContext,
                            IssueSeverity.Error,
                            "Guardian musing должен содержать thought или text",
                            code: "guardian_musing_missing_text",
                            section: "Guardians",
                            expected: "thought or text",
                            actual: "missing textual musing payload",
                            repairHint: "Заполни thought или text у каждой canonical musings[] entry, чтобы клиенту было что показывать в журнале размышлений Хранителя."));
                    }
                }
            }
        }
    }


    private void ValidateGuardianAbodePowerObject(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("abodePower", out var abodePower) || abodePower.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.abodePower",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать abodePower object",
                code: "guardian_missing_abode_power",
                section: "Guardians",
                repairHint: "Сохраняй для каждого Хранителя abodePower с currentPower, tier, lastUpdatedAt и history."));
            return;
        }

        if (!RequireObject(abodePower, $"{guardianContext}.abodePower", issues))
            return;

        var powerContext = $"{guardianContext}.abodePower";
        ValidateNonNegativeIntegerField(abodePower, powerContext, issues, "currentPower", "Guardians");
        if (TryReadInt(abodePower, "currentPower", out var parsedPower) &&
            (parsedPower < AbodePowerRules.MinPower || parsedPower > AbodePowerRules.MaxPower))
        {
            issues.Add(new ValidationIssue(
                $"{powerContext}.currentPower",
                IssueSeverity.Error,
                "abodePower.currentPower должен быть в диапазоне 0..100",
                code: "guardian_abode_power_out_of_bounds",
                section: "Guardians",
                expected: "0..100",
                actual: parsedPower.ToString(),
                repairHint: "Сохраняй currentPower только в canonical диапазоне 0..100."));
        }

        RequireString(abodePower, powerContext, issues, "tier");
        var expectedTier = AbodePowerRules.GetTierLabel(AbodePowerRules.GetCurrentPower(guardian));
        var actualTier = GetFirstNonEmptyString(abodePower, "tier");
        if (!string.IsNullOrWhiteSpace(actualTier) &&
            !string.Equals(actualTier, expectedTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{powerContext}.tier",
                IssueSeverity.Error,
                "abodePower.tier должен совпадать с currentPower",
                code: "guardian_abode_power_tier_mismatch",
                section: "Guardians",
                expected: expectedTier,
                actual: actualTier,
                repairHint: "Синхронизируй abodePower.tier с derived tier label от currentPower."));
        }

        var lastUpdatedAt = RequireString(abodePower, powerContext, issues, "lastUpdatedAt");
        if (!string.IsNullOrWhiteSpace(lastUpdatedAt) && !DateTimeOffset.TryParse(lastUpdatedAt, out _))
        {
            issues.Add(new ValidationIssue(
                $"{powerContext}.lastUpdatedAt",
                IssueSeverity.Error,
                "abodePower.lastUpdatedAt должен быть ISO 8601 timestamp",
                code: "guardian_abode_power_invalid_timestamp",
                section: "Guardians",
                expected: "ISO 8601 timestamp",
                actual: lastUpdatedAt,
                repairHint: "Сохраняй abodePower.lastUpdatedAt как ISO 8601 timestamp."));
        }

        if (!abodePower.TryGetProperty("history", out var history) || history.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{powerContext}.history",
                IssueSeverity.Error,
                "abodePower должен содержать history array",
                code: "guardian_abode_power_history_missing",
                section: "Guardians",
                repairHint: "Сохраняй историю силы Обители как массив canonical change entries."));
            return;
        }

        RequireArrayOfObjects(history, $"{powerContext}.history", issues);
        if (history.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in history.EnumerateArray())
        {
            var entryContext = $"{powerContext}.history[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            RequireString(entry, entryContext, issues, "timestamp");
            ValidateIntegerField(entry, entryContext, issues, "change");
            RequireString(entry, entryContext, issues, "reason");
            RequireString(entry, entryContext, issues, "source");
            ValidateOptionalNullableStringField(entry, entryContext, issues, "relatedGuardianId");
            ValidateOptionalNullableStringField(entry, entryContext, issues, "relatedQuestId");
            ValidateOptionalNullableStringField(entry, entryContext, issues, "relatedProjectId");
        }
    }


    private void ValidateGuardianStateData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var guardiansById = new Dictionary<string, (JsonElement Guardian, string Context)>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var guardian in guardians.EnumerateArray())
            {
                var guardianContext = $"{contextPrefix}.guardians[{index++}]";
                if (!RequireObject(guardian, guardianContext, issues))
                    continue;

                ValidateGuardianCanonicalObject(guardian, guardianContext, issues);

                if (guardian.TryGetProperty("guardianId", out var guardianIdNode) &&
                    guardianIdNode.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(guardianIdNode.GetString()))
                {
                    guardiansById[guardianIdNode.GetString()!] = (guardian, guardianContext);
                }
            }
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
        {
            const string activeGuardianContextSuffix = ".activeGuardian";
            var activeGuardianContext = $"{contextPrefix}{activeGuardianContextSuffix}";
            ValidateGuardianCanonicalObject(activeGuardian, activeGuardianContext, issues);

            if (activeGuardian.TryGetProperty("guardianId", out var activeGuardianIdNode) &&
                activeGuardianIdNode.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(activeGuardianIdNode.GetString()) &&
                guardiansById.TryGetValue(activeGuardianIdNode.GetString()!, out var guardianMatch))
            {
                CompareGuardianGachaState(activeGuardian, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
                CompareGuardianTradeState(activeGuardian, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
                ValidateActiveGuardianNavigationState(root, contextPrefix, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
            }
        }
    }


    private void ValidateActiveGuardianNavigationState(
        JsonElement root,
        string contextPrefix,
        string activeGuardianContext,
        JsonElement guardianFromArray,
        string guardianArrayContext,
        List<ValidationIssue> issues)
    {
        var expectedAbodeId = "";
        if (guardianFromArray.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
            expectedAbodeId = GetFirstNonEmptyString(abode, "abodeId", "id");

        if (string.IsNullOrWhiteSpace(expectedAbodeId))
            return;

        var hasNavigationObject = root.TryGetProperty("chaosSeaNavigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object;
        if (!hasNavigationObject)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chaosSeaNavigation",
                IssueSeverity.Error,
                "Активный Хранитель требует materialized chaosSeaNavigation.currentAbodeId",
                code: "active_guardian_missing_current_abode_id",
                section: "Guardians",
                expected: $"chaosSeaNavigation.currentAbodeId = {expectedAbodeId}",
                actual: "chaosSeaNavigation missing",
                repairHint: $"Когда душа находится у активного Хранителя, materialize chaosSeaNavigation.currentAbodeId и синхронизируй его с abodeId из {guardianArrayContext}.abode."));
        }

        var actualAbodeId = hasNavigationObject ? GetFirstNonEmptyString(navigation, "currentAbodeId") : null;
        if (hasNavigationObject && string.IsNullOrWhiteSpace(actualAbodeId))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chaosSeaNavigation.currentAbodeId",
                IssueSeverity.Error,
                "Активный Хранитель требует непустой currentAbodeId",
                code: "active_guardian_missing_current_abode_id",
                section: "Guardians",
                expected: expectedAbodeId,
                actual: "missing or empty",
                repairHint: $"При создании/активации Хранителя сразу записывай chaosSeaNavigation.currentAbodeId = {expectedAbodeId}, иначе локальная торговля и abode-bound UX будут недоступны."));
        }

        if (hasNavigationObject &&
            !string.IsNullOrWhiteSpace(actualAbodeId) &&
            !string.Equals(actualAbodeId, expectedAbodeId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chaosSeaNavigation.currentAbodeId",
                IssueSeverity.Error,
                "currentAbodeId должен совпадать с abodeId активного Хранителя",
                code: "active_guardian_current_abode_mismatch",
                section: "Guardians",
                expected: expectedAbodeId,
                actual: actualAbodeId,
                repairHint: $"Синхронизируй chaosSeaNavigation.currentAbodeId с abodeId активного Хранителя из {guardianArrayContext}.abode, иначе guardian trade и abode-specific UI будут расходиться с canonical state."));
        }

        JsonElement discoveredAbodes = default;
        var hasDiscoveredAbodesArray = hasNavigationObject &&
                                       navigation.TryGetProperty("discoveredAbodes", out discoveredAbodes) &&
                                       discoveredAbodes.ValueKind == JsonValueKind.Array;

        if (!hasDiscoveredAbodesArray)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chaosSeaNavigation.discoveredAbodes",
                IssueSeverity.Error,
                "Активный Хранитель требует materialized discoveredAbodes с текущей обителью",
                code: "active_guardian_missing_from_discovered_abodes",
                section: "Guardians",
                expected: $"discoveredAbodes contains {expectedAbodeId}",
                actual: !hasNavigationObject ? "chaosSeaNavigation missing" : !navigation.TryGetProperty("discoveredAbodes", out _) ? "missing" : discoveredAbodes.ValueKind.ToString(),
                repairHint: $"Когда активный Хранитель уже materialized, добавляй его abodeId {expectedAbodeId} в chaosSeaNavigation.discoveredAbodes. Иначе navigation/travel UX будет неполным."));
        }
        else
        {
            var containsExpectedAbode = discoveredAbodes.EnumerateArray()
                .Any(node => node.ValueKind == JsonValueKind.String &&
                             string.Equals(node.GetString(), expectedAbodeId, StringComparison.OrdinalIgnoreCase));

            if (!containsExpectedAbode)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.chaosSeaNavigation.discoveredAbodes",
                    IssueSeverity.Error,
                    "discoveredAbodes должен содержать abodeId активного Хранителя",
                    code: "active_guardian_missing_from_discovered_abodes",
                    section: "Guardians",
                    expected: expectedAbodeId,
                    actual: BuildCanonicalJsonSignature(discoveredAbodes),
                    repairHint: $"При создании/активации Хранителя сразу включай его abodeId {expectedAbodeId} в chaosSeaNavigation.discoveredAbodes, иначе игрок не считается знающим текущую обитель полностью."));
            }
        }

        if (!abode.TryGetProperty("isDiscovered", out var isDiscoveredNode) || isDiscoveredNode.ValueKind != JsonValueKind.True)
        {
            issues.Add(new ValidationIssue(
                $"{guardianArrayContext}.abode.isDiscovered",
                IssueSeverity.Error,
                "Активный Хранитель требует discovered abode state",
                code: "active_guardian_abode_not_marked_discovered",
                section: "Guardians",
                expected: "true",
                actual: !abode.TryGetProperty("isDiscovered", out _) ? "missing" : isDiscoveredNode.ToString(),
                repairHint: $"Если Хранитель уже materialized как текущий активный, его abode.isDiscovered должен быть true. Не оставляй текущую обитель скрытой для игрока."));
        }
    }


    private void ValidateGuardianCanonicalObject(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        RequireString(guardian, guardianContext, issues, "guardianId");
        RequireString(guardian, guardianContext, issues, "canonicalName");
        ValidateGuardianAbodePowerObject(guardian, guardianContext, issues);
        RequireString(guardian, guardianContext, issues, "domain");

        ValidateGuardianSourcePreset(guardian, guardianContext, issues);
        ValidateGuardianNameVariants(guardian, guardianContext, issues);
        ValidateGuardianManifestation(guardian, guardianContext, issues);
        ValidateGuardianManifestationHistory(guardian, guardianContext, issues);
        ValidateGuardianPersonalityProfile(guardian, guardianContext, issues);
        ValidateGuardianRelationshipData(guardian, guardianContext, issues);
        ValidateGuardianQuestManagement(guardian, guardianContext, issues);
        ValidateGuardianGachaState(guardian, guardianContext, issues);
        ValidateGuardianTradeState(guardian, guardianContext, issues);
        ValidateGuardianStoredInnerLifeState(guardian, guardianContext, issues);
    }


    private void ValidateGuardianSourcePreset(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("sourcePreset", out var sourcePreset) || sourcePreset.ValueKind == JsonValueKind.Null)
            return;

        if (!RequireObject(sourcePreset, $"{guardianContext}.sourcePreset", issues))
            return;

        RequireString(sourcePreset, $"{guardianContext}.sourcePreset", issues, "presetId");
        RequireString(sourcePreset, $"{guardianContext}.sourcePreset", issues, "displayName");
        RequireString(sourcePreset, $"{guardianContext}.sourcePreset", issues, "version");
        RequireString(sourcePreset, $"{guardianContext}.sourcePreset", issues, "library");
    }


    private void ValidateGuardianNameVariants(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("nameVariants", out var nameVariants) ||
            nameVariants.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.nameVariants",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать nameVariants object",
                code: "guardian_missing_name_variants",
                section: "Guardians",
                expected: "nameVariants object with default plus optional feminine/masculine/neutral variants",
                actual: guardian.TryGetProperty("nameVariants", out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй nameVariants как объект c default и optional feminine/masculine/neutral display variants."));
            return;
        }

        var variantsContext = $"{guardianContext}.nameVariants";
        RequireString(nameVariants, variantsContext, issues, "default");
        ValidateOptionalString(nameVariants, variantsContext, issues, "feminine");
        ValidateOptionalString(nameVariants, variantsContext, issues, "masculine");
        ValidateOptionalString(nameVariants, variantsContext, issues, "neutral");
    }


    private void ValidateGuardianManifestation(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("manifestation", out var manifestation) ||
            manifestation.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.manifestation",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать manifestation object",
                code: "guardian_missing_manifestation",
                section: "Guardians",
                expected: "manifestation object with currentDisplayName, formFlexibility, currentPresentationStyle, currentPronouns and appearanceDescription",
                actual: guardian.TryGetProperty("manifestation", out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй manifestation как canonical текущую форму проявления Хранителя."));
            return;
        }

        var manifestationContext = $"{guardianContext}.manifestation";
        RequireString(manifestation, manifestationContext, issues, "currentDisplayName");
        var formFlexibility = RequireString(manifestation, manifestationContext, issues, "formFlexibility");
        RequireString(manifestation, manifestationContext, issues, "currentPresentationStyle");
        RequireString(manifestation, manifestationContext, issues, "currentPronouns");
        RequireString(manifestation, manifestationContext, issues, "appearanceDescription");
        ValidateOptionalString(manifestation, manifestationContext, issues, "presentationReason");

        if (!string.IsNullOrWhiteSpace(formFlexibility) && !GuardianManifestation.IsValidFormFlexibility(formFlexibility))
        {
            issues.Add(new ValidationIssue(
                $"{manifestationContext}.formFlexibility",
                IssueSeverity.Error,
                "guardian.manifestation.formFlexibility должен быть canonical flexibility value",
                code: "guardian_manifestation_invalid_form_flexibility",
                section: "Guardians",
                expected: "fixed | selective | adaptive",
                actual: formFlexibility,
                repairHint: "Используй для formFlexibility только fixed, selective или adaptive."));
        }
    }


    private void ValidateGuardianManifestationHistory(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("manifestationHistory", out var manifestationHistory) ||
            manifestationHistory.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.manifestationHistory",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать manifestationHistory array",
                code: "guardian_missing_manifestation_history",
                section: "Guardians",
                expected: "manifestationHistory array",
                actual: "missing",
                repairHint: "Сохраняй manifestationHistory как массив прежних display-форм, даже если он пока пустой."));
            return;
        }

        RequireArrayOfObjects(manifestationHistory, $"{guardianContext}.manifestationHistory", issues);
        if (manifestationHistory.ValueKind != JsonValueKind.Array)
            return;

        var currentDisplayName = GuardianManifestation.GetDisplayName(guardian);
        var currentPresentationStyle = GuardianManifestation.GetPresentationStyle(guardian);
        var currentPronouns = GuardianManifestation.GetPronouns(guardian);
        var currentKey = $"{currentDisplayName}|{currentPresentationStyle}|{currentPronouns}";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var entry in manifestationHistory.EnumerateArray())
        {
            var entryContext = $"{guardianContext}.manifestationHistory[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var displayName = RequireString(entry, entryContext, issues, "displayName");
            var presentationStyle = RequireString(entry, entryContext, issues, "presentationStyle");
            var pronouns = RequireString(entry, entryContext, issues, "pronouns");
            RequireString(entry, entryContext, issues, "appearanceDescription");
            ValidateOptionalString(entry, entryContext, issues, "reason");
            ValidateOptionalString(entry, entryContext, issues, "changedAtUtc");

            var historyKey = $"{displayName}|{presentationStyle}|{pronouns}";
            if (!string.IsNullOrWhiteSpace(displayName) &&
                !string.IsNullOrWhiteSpace(presentationStyle) &&
                !string.IsNullOrWhiteSpace(pronouns) &&
                !seen.Add(historyKey))
            {
                issues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "manifestationHistory не должен содержать дубликаты форм",
                    code: "guardian_manifestation_history_duplicate_entry",
                    section: "Guardians",
                    actual: historyKey,
                    repairHint: "Каждую прежнюю форму проявления Хранителя сохраняй только один раз."));
            }

            if (!string.IsNullOrWhiteSpace(currentDisplayName) &&
                string.Equals(historyKey, currentKey, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "manifestationHistory не должен содержать текущую форму проявления",
                    code: "guardian_manifestation_history_contains_current_form",
                    section: "Guardians",
                    actual: historyKey,
                    repairHint: "В manifestationHistory храни только прошлые формы, а текущую оставляй только в manifestation."));
            }
        }
    }


    private void ValidateGuardianPersonalityProfile(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("personalityProfile", out var personalityProfile) ||
            personalityProfile.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.personalityProfile",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать personalityProfile object",
                code: "guardian_missing_personality_profile",
                section: "Guardians",
                expected: "personalityProfile object with archetype, speechPattern, coreValues",
                actual: guardian.TryGetProperty("personalityProfile", out var actualProfile) ? actualProfile.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй для каждого Хранителя personalityProfile с archetype, speechPattern и массивом coreValues, как требует Block 32."));
            return;
        }

        var profileContext = $"{guardianContext}.personalityProfile";
        RequireString(personalityProfile, profileContext, issues, "archetype");
        RequireString(personalityProfile, profileContext, issues, "speechPattern");

        if (!personalityProfile.TryGetProperty("coreValues", out var coreValues) || coreValues.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{profileContext}.coreValues",
                IssueSeverity.Error,
                "Guardian personalityProfile должен содержать coreValues array",
                code: "guardian_personality_missing_core_values",
                section: "Guardians",
                expected: "array of 3..5 strings",
                actual: "missing",
                repairHint: "Сохраняй coreValues как массив из 3-5 ценностей, согласованных с доменом и архетипом Хранителя."));
            return;
        }

        RequireArrayOfStrings(coreValues, $"{profileContext}.coreValues", issues);
        if (coreValues.ValueKind == JsonValueKind.Array)
        {
            var count = coreValues.GetArrayLength();
            if (count < 3 || count > 5)
            {
                issues.Add(new ValidationIssue(
                    $"{profileContext}.coreValues",
                    IssueSeverity.Error,
                    "Guardian personalityProfile.coreValues должен содержать 3-5 ценностей",
                    code: "guardian_personality_core_values_count_invalid",
                    section: "Guardians",
                    expected: "3..5 strings",
                    actual: count.ToString(),
                    repairHint: "Используй для coreValues ровно 3-5 значимых ценностей, а не пустой или перегруженный список."));
            }
        }
    }


    private void ValidateGuardianRelationshipData(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("relationshipData", out var relationshipData) ||
            relationshipData.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.relationshipData",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать relationshipData object",
                code: "guardian_missing_relationship_data",
                section: "Guardians",
                expected: "relationshipData object with currentReputation, reputationHistory, lastInteraction",
                actual: guardian.TryGetProperty("relationshipData", out var actualRelationshipData) ? actualRelationshipData.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй relationshipData с currentReputation, reputationHistory и lastInteraction для каждого Хранителя."));
            return;
        }

        var relationshipContext = $"{guardianContext}.relationshipData";
        if (!relationshipData.TryGetProperty("currentReputation", out var currentReputation) ||
            currentReputation.ValueKind != JsonValueKind.Number ||
            !currentReputation.TryGetInt32(out var parsedReputation))
        {
            issues.Add(new ValidationIssue(
                $"{relationshipContext}.currentReputation",
                IssueSeverity.Error,
                "Guardian relationshipData должен содержать integer currentReputation",
                code: "guardian_relationship_missing_reputation",
                section: "Guardians",
                expected: "Integer currentReputation within -100..300",
                actual: relationshipData.TryGetProperty("currentReputation", out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй canonical guardian relationshipData.currentReputation как integer из диапазона -100..300."));
            return;
        }

        if (parsedReputation < -100 || parsedReputation > 300)
        {
            issues.Add(new ValidationIssue(
                $"{relationshipContext}.currentReputation",
                IssueSeverity.Error,
                "Guardian currentReputation должен быть в диапазоне -100..300",
                code: "guardian_relationship_reputation_out_of_bounds",
                section: "Guardians",
                expected: "-100..300",
                actual: parsedReputation.ToString(),
                repairHint: "Не выводи репутацию Хранителя за пределы canonical guardian scale -100..300."));
        }

        if (!relationshipData.TryGetProperty("reputationHistory", out var reputationHistory) ||
            reputationHistory.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{relationshipContext}.reputationHistory",
                IssueSeverity.Error,
                "Guardian relationshipData должен содержать reputationHistory array",
                code: "guardian_relationship_missing_history",
                section: "Guardians",
                expected: "reputationHistory array",
                actual: "missing",
                repairHint: "Сохраняй reputationHistory как массив изменений репутации, даже если он пока пустой."));
        }
        else
        {
            RequireArrayOfObjects(reputationHistory, $"{relationshipContext}.reputationHistory", issues);
            if (reputationHistory.ValueKind == JsonValueKind.Array)
            {
                var historyIndex = 0;
                foreach (var entry in reputationHistory.EnumerateArray())
                {
                    var entryContext = $"{relationshipContext}.reputationHistory[{historyIndex++}]";
                    if (!RequireObject(entry, entryContext, issues))
                        continue;

                    var timestamp = RequireString(entry, entryContext, issues, "timestamp");
                    ValidateIntegerField(entry, entryContext, issues, "change");
                    RequireString(entry, entryContext, issues, "reason");
                    ValidateOptionalString(entry, entryContext, issues, "questId");
                    if (!string.IsNullOrWhiteSpace(timestamp) && !DateTimeOffset.TryParse(timestamp, out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{entryContext}.timestamp",
                            IssueSeverity.Error,
                            "Guardian reputationHistory.timestamp должен быть ISO 8601 timestamp",
                            code: "guardian_relationship_history_invalid_timestamp",
                            section: "Guardians",
                            expected: "ISO 8601 timestamp",
                            actual: timestamp,
                            repairHint: "Сохраняй timestamp в reputationHistory как ISO 8601 строку для воспроизводимой chronology."));
                    }
                }
            }
        }

        if (!relationshipData.TryGetProperty("lastInteraction", out var lastInteraction))
        {
            issues.Add(new ValidationIssue(
                $"{relationshipContext}.lastInteraction",
                IssueSeverity.Error,
                "Guardian relationshipData должен содержать lastInteraction",
                code: "guardian_relationship_missing_last_interaction",
                section: "Guardians",
                expected: "ISO 8601 timestamp or null",
                actual: "missing",
                repairHint: "Сохраняй lastInteraction как ISO timestamp последнего meaningful interaction или null, если контакта ещё не было."));
        }
        else if (lastInteraction.ValueKind != JsonValueKind.Null)
        {
            var lastInteractionValue = RequireString(relationshipData, relationshipContext, issues, "lastInteraction");
            if (!string.IsNullOrWhiteSpace(lastInteractionValue) && !DateTimeOffset.TryParse(lastInteractionValue, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.lastInteraction",
                    IssueSeverity.Error,
                    "Guardian relationshipData.lastInteraction должен быть ISO 8601 timestamp или null",
                    code: "guardian_relationship_invalid_last_interaction",
                    section: "Guardians",
                    expected: "ISO 8601 timestamp or null",
                    actual: lastInteractionValue,
                    repairHint: "Используй для lastInteraction ISO 8601 timestamp или null, если взаимодействий ещё не было."));
            }
        }
    }


    private void ValidateGuardianQuestManagement(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("questManagement", out var questManagement) ||
            questManagement.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.questManagement",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать questManagement object",
                code: "guardian_missing_quest_management",
                section: "Guardians",
                expected: "questManagement object with availableQuests, activeQuests and completedQuests",
                actual: guardian.TryGetProperty("questManagement", out var actualQuestManagement) ? actualQuestManagement.ValueKind.ToString() : "missing",
                repairHint: "Сохраняй questManagement с массивами availableQuests, activeQuests и completedQuests даже для нового Хранителя."));
            return;
        }

        var guardianId = GetFirstNonEmptyString(guardian, "guardianId");
        var questContext = $"{guardianContext}.questManagement";
        if (!questManagement.TryGetProperty("availableQuests", out var availableQuests) ||
            availableQuests.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{questContext}.availableQuests",
                IssueSeverity.Error,
                "Guardian questManagement должен содержать availableQuests array",
                code: "guardian_quest_management_missing_available_quests",
                section: "Guardians",
                expected: "availableQuests array",
                actual: "missing",
                repairHint: "Сохраняй availableQuests как массив, даже если у Хранителя сейчас нет доступных квестов."));
        }
        else
        {
            RequireArrayOfObjects(availableQuests, $"{questContext}.availableQuests", issues);
            var derivedState = ResolveGuardianDerivedStateForValidation(guardian);
            var questCap = derivedState.GuardianQuestCap;
            if (availableQuests.ValueKind == JsonValueKind.Array && availableQuests.GetArrayLength() > questCap)
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.availableQuests",
                    IssueSeverity.Error,
                    "Guardian questManagement превышает cap доступных квестов для текущей силы Обители",
                    code: "guardian_available_quests_limit_exceeded",
                    section: "Guardians",
                    expected: $"0..{questCap} available quests",
                    actual: availableQuests.GetArrayLength().ToString(),
                    repairHint: "Синхронизируй число availableQuests с shared derived guardianQuestCap, а не с локальной ad-hoc формулой."));
            }
            ValidateGuardianAvailableQuestDifficultyCeiling(
                availableQuests,
                $"{questContext}.availableQuests",
                derivedState.GuardianQuestDifficultyCeiling,
                issues);
            ValidateGuardianLoreResearchQuestOrigins(availableQuests, $"{questContext}.availableQuests", guardianId ?? string.Empty, issues);
        }

        if (!questManagement.TryGetProperty("activeQuests", out var activeQuests) ||
            activeQuests.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{questContext}.activeQuests",
                IssueSeverity.Error,
                "Guardian questManagement должен содержать activeQuests array",
                code: "guardian_quest_management_missing_active_quests",
                section: "Guardians",
                expected: "activeQuests array",
                actual: "missing",
                repairHint: "Сохраняй activeQuests как массив, даже если у Хранителя сейчас нет активных квестов."));
        }
        else
        {
            RequireArrayOfObjects(activeQuests, $"{questContext}.activeQuests", issues);
            ValidateGuardianLoreResearchQuestOrigins(activeQuests, $"{questContext}.activeQuests", guardianId ?? string.Empty, issues);
        }

        if (!questManagement.TryGetProperty("completedQuests", out var completedQuests) ||
            completedQuests.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{questContext}.completedQuests",
                IssueSeverity.Error,
                "Guardian questManagement должен содержать completedQuests array",
                code: "guardian_quest_management_missing_completed_quests",
                section: "Guardians",
                expected: "completedQuests array",
                actual: "missing",
                repairHint: "Сохраняй completedQuests как массив завершённых квестов, даже если он пока пустой."));
        }
        else
        {
            RequireArrayOfObjects(completedQuests, $"{questContext}.completedQuests", issues);
            if (completedQuests.ValueKind == JsonValueKind.Array)
            {
                var completedIndex = 0;
                foreach (var completedQuest in completedQuests.EnumerateArray())
                {
                    var completedContext = $"{questContext}.completedQuests[{completedIndex++}]";
                    if (!RequireObject(completedQuest, completedContext, issues))
                        continue;

                    RequireString(completedQuest, completedContext, issues, "questId");
                    ValidateOptionalString(completedQuest, completedContext, issues, "questOrigin");
                    ValidateOptionalString(completedQuest, completedContext, issues, "sourceProjectId");
                    ValidateOptionalString(completedQuest, completedContext, issues, "sourceArchiveId");
                    ValidateOptionalString(completedQuest, completedContext, issues, "sourceArchiveTitle");
                    var result = GetFirstNonEmptyString(completedQuest, "result");
                    if (!string.IsNullOrWhiteSpace(result) && !AllowedGuardianQuestOutcomes.Contains(result))
                    {
                        issues.Add(new ValidationIssue(
                            $"{completedContext}.result",
                            IssueSeverity.Error,
                            "Guardian completedQuests.result должен быть success, failure или partial",
                            code: "guardian_completed_quest_invalid_result",
                            section: "Guardians",
                            expected: "success | failure | partial",
                            actual: result,
                            repairHint: "Для completedQuests сохраняй result только как canonical guardian quest outcome: success, failure или partial."));
                    }

                    var completionDate = RequireString(completedQuest, completedContext, issues, "completionDate");
                    if (!string.IsNullOrWhiteSpace(completionDate) && !DateTimeOffset.TryParse(completionDate, out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{completedContext}.completionDate",
                            IssueSeverity.Error,
                            "Guardian completedQuests.completionDate должен быть ISO 8601 timestamp",
                            code: "guardian_completed_quest_invalid_completion_date",
                            section: "Guardians",
                            expected: "ISO 8601 timestamp",
                            actual: completionDate,
                            repairHint: "Для completedQuests сохраняй completionDate в ISO 8601 формате, чтобы chronological unlock logic и audit trail оставались стабильными."));
                    }
                }
            }
        }

        ValidateGuaranteedArchiveConsultationQuestPresence(
            questManagement,
            questContext,
            guardianId ?? string.Empty,
            issues);
    }


    private void ValidateGuardianLoreResearchQuestOrigins(JsonElement questArray, string arrayContext, string guardianId, List<ValidationIssue> issues)
    {
        if (questArray.ValueKind != JsonValueKind.Array || string.IsNullOrWhiteSpace(guardianId))
            return;

        var trackerJson = ReadCurrentTrackedFileSync(GuardianProjectState.TrackerPath);
        using var trackerDoc = !string.IsNullOrWhiteSpace(trackerJson) ? JsonDocument.Parse(trackerJson) : null;
        var loreUsageCounts = new Dictionary<string, (int hookCount, int specialCount, int archiveCount)>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var quest in questArray.EnumerateArray())
        {
            var questContext = $"{arrayContext}[{index++}]";
            if (!RequireObject(quest, questContext, issues))
                continue;

            var questId = GetFirstNonEmptyString(quest, "questId");
            if (string.IsNullOrWhiteSpace(questId))
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.questId",
                    IssueSeverity.Error,
                    "Guardian available/active quest должен содержать обязательный questId",
                    code: "guardian_live_quest_missing_quest_id",
                    section: "Guardians",
                    expected: "non-empty questId string",
                    actual: quest.TryGetProperty("questId", out var actualQuestId) ? actualQuestId.ValueKind.ToString() : "missing",
                    repairHint: "Материализуй любой guardian quest в availableQuests/activeQuests только с непустым questId; не используй title/sourceProjectId как surrogate identity."));
            }

            ValidateOptionalString(quest, questContext, issues, "questOrigin");
            ValidateOptionalString(quest, questContext, issues, "sourceProjectId");

            var questOrigin = GetFirstNonEmptyString(quest, "questOrigin");
            if (!string.Equals(questOrigin, GuardianProjectState.LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceProjectId = GetFirstNonEmptyString(quest, "sourceProjectId");
            if (string.IsNullOrWhiteSpace(sourceProjectId))
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.sourceProjectId",
                    IssueSeverity.Error,
                    "Guardian quest от lore_research обязан ссылаться на sourceProjectId",
                    code: "guardian_lore_research_quest_missing_source_project_id",
                    section: "Guardians",
                    repairHint: "Для questOrigin = lore_research_hook, lore_research_special_line или archive_consultation_hook сохраняй sourceProjectId completed lore_research проекта."));
                continue;
            }

            var grantedTokens = ReadGrantedLoreResearchQuestTokens(trackerDoc?.RootElement, guardianId, sourceProjectId, questOrigin ?? string.Empty);
            if (grantedTokens <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.sourceProjectId",
                    IssueSeverity.Error,
                    "Guardian quest пытается использовать lore_research sourceProjectId без выданного machine-readable ресурса",
                    code: "guardian_lore_research_quest_token_exhausted",
                    section: "Guardians",
                    repairHint: "Не создавай lore_research/archive consultation quest без выданного token или guaranteed quest grant в effectState completed проекта."));
                continue;
            }

            var currentUsage = loreUsageCounts.TryGetValue(sourceProjectId, out var usage) ? usage : (0, 0, 0);
            currentUsage = string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase)
                ? (currentUsage.Item1, currentUsage.Item2 + 1, currentUsage.Item3)
                : string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase)
                    ? (currentUsage.Item1, currentUsage.Item2, currentUsage.Item3 + 1)
                    : (currentUsage.Item1 + 1, currentUsage.Item2, currentUsage.Item3);
            loreUsageCounts[sourceProjectId] = currentUsage;

            if (currentUsage.Item1 > ReadGrantedLoreResearchQuestTokens(trackerDoc?.RootElement, guardianId, sourceProjectId, GuardianProjectState.LoreResearchHookOrigin) ||
                currentUsage.Item2 > ReadGrantedLoreResearchQuestTokens(trackerDoc?.RootElement, guardianId, sourceProjectId, GuardianProjectState.LoreResearchSpecialLineOrigin) ||
                currentUsage.Item3 > ReadGrantedLoreResearchQuestTokens(trackerDoc?.RootElement, guardianId, sourceProjectId, GuardianProjectState.ArchiveConsultationHookOrigin))
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.sourceProjectId",
                    IssueSeverity.Error,
                    "Число guardian quest-ов, привязанных к одному lore_research проекту, превышает выданные tokens",
                    code: "guardian_lore_research_quest_token_overallocated",
                    section: "Guardians",
                    repairHint: "Не превышай число quest hook / special line / guaranteed archive quest tokens, выданных completed lore_research проектом."));
            }
        }
    }
}
