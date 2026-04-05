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
    private sealed class GuardianCommandAuthorizationResult
    {
        public Dictionary<string, JsonElement> AuthorizedCreateGuardiansById { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<JsonObject> AuthorizedCommands { get; } = new();
        public List<JsonObject> AuthorizedNonCreateCommands { get; } = new();
    }

    private void ValidateGuardianCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        AuthorizeGuardianCommandsForPolicy(root, contextPrefix, issues);
    }

    private GuardianCommandAuthorizationResult AuthorizeGuardianCommandsForPolicy(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue>? issues = null,
        GuardianPolicyContext? guardianPolicyContext = null)
    {
        var result = new GuardianCommandAuthorizationResult();
        if (!root.TryGetProperty("UpdateGuardians", out var updates))
            return result;
        if (updates.ValueKind == JsonValueKind.Null)
            return result;

        var issueSink = issues ?? new List<ValidationIssue>();
        if (!TryGetArray(root, "UpdateGuardians", $"{contextPrefix}.UpdateGuardians", issueSink, out var arr))
            return result;

        guardianPolicyContext ??= _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        var hasUsableValidatedPreTurnBaseline = HasUsableValidatedPreTurnGuardianBaseline(guardianPolicyContext);
        var containsNonCreateCommand = arr.EnumerateArray()
            .Any(item =>
                item.ValueKind == JsonValueKind.Object &&
                !string.Equals(GetFirstNonEmptyString(item, "command"), "create", StringComparison.OrdinalIgnoreCase));

        if (containsNonCreateCommand && !hasUsableValidatedPreTurnBaseline && issues != null)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.UpdateGuardians",
                IssueSeverity.Error,
                "Non-create UpdateGuardians commands требуют readable validated pre-turn guardians baseline и не используют current guardians[] как authority fallback.",
                code: "guardian_commands_missing_validated_preturn_guardians_snapshot",
                section: "UpdateGuardians",
                expected: "validated pre-turn guardians baseline or earlier valid same-turn create",
                actual: $"{guardianPolicyContext.PreTurnGuardiansSnapshot.ManifestStatus}/{guardianPolicyContext.PreTurnGuardiansSnapshot.FileStatus}",
                repairHint: "Для non-create UpdateGuardians сохраняй readable validated snapshot copy game_state/meta/guardians.json. Без этого команды должны fail-closed вместо вывода authority из current guardians[]."));
        }

        var knownGuardianIds = CollectKnownGuardianIds(guardianPolicyContext);
        var createConflictGuardianIds = CollectKnownGuardianIdsForCreateConflictValidation(guardianPolicyContext);
        var guardianSequentialStates = CollectKnownGuardianSequentialStatesForCommandValidation(guardianPolicyContext);
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.UpdateGuardians[{index++}]";
            if (!RequireObject(item, itemContext, issueSink))
                continue;

            var command = RequireString(item, itemContext, issueSink, "command");
            if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase))
            {
                if (!item.TryGetProperty("data", out var data) || !RequireObject(data, $"{itemContext}.data", issueSink))
                {
                    issueSink.Add(new ValidationIssue(
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

                var issuesBeforeCreateValidation = issueSink.Count;
                var createdGuardianId = GetFirstNonEmptyString(data, "guardianId");
                if (!string.IsNullOrWhiteSpace(createdGuardianId) && createConflictGuardianIds.Contains(createdGuardianId))
                {
                    issueSink.Add(new ValidationIssue(
                        $"{itemContext}.data.guardianId",
                        IssueSeverity.Error,
                        $"UpdateGuardians.create не может повторно создавать guardianId '{createdGuardianId}'",
                        code: "guardian_create_duplicate_guardian_id",
                        section: "UpdateGuardians.create",
                        expected: "new guardianId not present in guardians.json or earlier valid create commands",
                        actual: createdGuardianId,
                        repairHint: "Для существующего Хранителя используй non-create команды. UpdateGuardians.create допустим только для нового уникального guardianId."));
                }
                ValidateGuardianCanonicalObject(data, $"{itemContext}.data", issueSink);
                if (issueSink.Count == issuesBeforeCreateValidation &&
                    !string.IsNullOrWhiteSpace(createdGuardianId))
                {
                    knownGuardianIds.Add(createdGuardianId);
                    createConflictGuardianIds.Add(createdGuardianId);
                    guardianSequentialStates[createdGuardianId] = ParseGuardianSequentialState(data);
                    result.AuthorizedCreateGuardiansById[createdGuardianId] = data.Clone();
                    var authorizedCreateCommand = TryParseJsonObject(item);
                    if (authorizedCreateCommand != null)
                        result.AuthorizedCommands.Add(authorizedCreateCommand);
                }
                continue;
            }

            if (!hasUsableValidatedPreTurnBaseline)
                continue;

            var issuesBeforeCommandValidation = issueSink.Count;
            var guardianId = RequireString(item, itemContext, issueSink, "guardianId");
            var proposedGuardianState = !string.IsNullOrWhiteSpace(guardianId) &&
                                       guardianSequentialStates.TryGetValue(guardianId, out var currentGuardianState)
                ? CloneGuardianSequentialState(currentGuardianState)
                : null;
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
            {
                issueSink.Add(new ValidationIssue(
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
                    ValidateIntegerField(item, itemContext, issueSink, "reputationChange");
                    RequireString(item, itemContext, issueSink, "reason");
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        proposedGuardianState != null &&
                        item.TryGetProperty("reputationChange", out var reputationDeltaNode) &&
                        reputationDeltaNode.ValueKind == JsonValueKind.Number &&
                        reputationDeltaNode.TryGetInt32(out var reputationDelta) &&
                        proposedGuardianState.CurrentReputation.HasValue)
                    {
                        proposedGuardianState.CurrentReputation += reputationDelta;
                    }
                    RejectLegacyGuardianDataShape(item, itemContext, issueSink, command,
                        "Top-level reputationChange + reason",
                        "Убери data и вынеси reputationChange/reason на верхний уровень updateReputation.");
                    break;

                case "completeQuest":
                    var questId = RequireString(item, itemContext, issueSink, "questId");
                    var outcome = RequireString(item, itemContext, issueSink, "outcome");
                    if (!string.IsNullOrWhiteSpace(outcome) && !AllowedGuardianQuestOutcomes.Contains(outcome))
                    {
                        issueSink.Add(new ValidationIssue(
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
                        proposedGuardianState != null)
                    {
                        var knownQuest =
                            proposedGuardianState.AvailableQuestIds.Contains(questId) ||
                            proposedGuardianState.ActiveQuestIds.Contains(questId);
                        if (!knownQuest)
                        {
                            issueSink.Add(new ValidationIssue(
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
                            proposedGuardianState.AvailableQuestIds.Remove(questId);
                            proposedGuardianState.ActiveQuestIds.Remove(questId);
                        }

                        ValidateGuardianQuestPowerAudit(
                            item,
                            itemContext,
                            guardianId,
                            questId,
                            outcome,
                            proposedGuardianState,
                            issueSink);
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
                            issueSink);
                    }
                    RejectLegacyGuardianDataShape(item, itemContext, issueSink, command,
                        "Top-level questId + outcome",
                        "Убери data и вынеси questId/outcome на верхний уровень completeQuest.");
                    break;

                case "processGacha":
                    ValidatePositiveNumberField(item, itemContext, issueSink, "inkFeathersSpent");
                    if (!string.IsNullOrWhiteSpace(guardianId) &&
                        proposedGuardianState != null &&
                        proposedGuardianState.CurrentReputation.HasValue)
                    {
                        var chargesPerReturn = GetExpectedGuardianGachaCharges(proposedGuardianState.CurrentReputation.Value, proposedGuardianState.CurrentAbodePower);
                        if (proposedGuardianState.ChargesUsedThisReturn >= chargesPerReturn)
                        {
                            issueSink.Add(new ValidationIssue(
                                $"{itemContext}.guardianId",
                                IssueSeverity.Error,
                                "processGacha нельзя вызывать для Хранителя без оставшихся charges в текущем return cycle",
                                code: "guardian_process_gacha_no_remaining_charges",
                                section: "UpdateGuardians.processGacha",
                                expected: $"chargesUsedThisReturn < chargesPerReturn ({proposedGuardianState.ChargesUsedThisReturn} < {chargesPerReturn})",
                                actual: $"chargesUsedThisReturn={proposedGuardianState.ChargesUsedThisReturn}, chargesPerReturn={chargesPerReturn}, currentReputation={proposedGuardianState.CurrentReputation.Value}",
                                repairHint: "Не эмить processGacha, если у этого Хранителя уже нет оставшихся попыток в текущем возвращении. Используй другого Хранителя или direct /gacha без guardian-mediated command."));
                        }
                        else
                        {
                            proposedGuardianState.ChargesUsedThisReturn++;
                        }
                    }

                    if (!item.TryGetProperty("result", out var resultNode) || !RequireObject(resultNode, $"{itemContext}.result", issueSink))
                    {
                        issueSink.Add(new ValidationIssue(
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
                        ValidateMinimalSoulRelicObject(resultNode, $"{itemContext}.result", issueSink, "UpdateGuardians.processGacha");

                        var baseRarity = TryReadCurrentTurnGachaBaseRaritySync();
                        var finalRarity = GetFirstNonEmptyString(resultNode, "rarity", "quality");
                        if (!string.IsNullOrWhiteSpace(baseRarity))
                        {
                            if (string.IsNullOrWhiteSpace(finalRarity))
                            {
                                issueSink.Add(new ValidationIssue(
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
                                issueSink.Add(new ValidationIssue(
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

                        var currentPower = proposedGuardianState?.CurrentAbodePower ?? 0;
                        ValidateGuardianGachaBonusAudit(
                            item,
                            itemContext,
                            guardianId ?? string.Empty,
                            baseRarity,
                            finalRarity,
                            currentPower,
                            issueSink);
                    }

                    RejectLegacyGuardianDataShape(item, itemContext, issueSink, command,
                        "Top-level inkFeathersSpent + result",
                        "Убери data и вынеси inkFeathersSpent/result на верхний уровень processGacha.");
                    break;

                case "addMusings":
                    ValidateGuardianMusingsCommand(item, itemContext, issueSink);
                    break;

                case "updateProject":
                    issueSink.Add(new ValidationIssue(
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
                        !RequireObject(loreFragment, $"{itemContext}.loreFragment", issueSink))
                    {
                        issueSink.Add(new ValidationIssue(
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
                        ValidateGuardianLoreFragmentObject(loreFragment, $"{itemContext}.loreFragment", issueSink, allowNullableContent: false);
                    }
                    break;

                case "setMood":
                    if (!item.TryGetProperty("mood", out var mood) ||
                        !RequireObject(mood, $"{itemContext}.mood", issueSink))
                    {
                        issueSink.Add(new ValidationIssue(
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
                        ValidateGuardianMoodObject(mood, $"{itemContext}.mood", issueSink);
                    }
                    break;

                default:
                    issueSink.Add(new ValidationIssue(
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

            if (issueSink.Count == issuesBeforeCommandValidation)
            {
                var authorizedCommand = TryParseJsonObject(item);
                if (authorizedCommand != null)
                {
                    result.AuthorizedCommands.Add(authorizedCommand);
                    result.AuthorizedNonCreateCommands.Add(authorizedCommand);
                }

                if (!string.IsNullOrWhiteSpace(guardianId) && proposedGuardianState != null)
                    guardianSequentialStates[guardianId] = proposedGuardianState;
            }
        }

        return result;
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


    private HashSet<string> CollectKnownGuardianIds(GuardianPolicyContext? guardianPolicyContext = null)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        guardianPolicyContext ??= _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();

        if (HasUsableValidatedPreTurnGuardianBaseline(guardianPolicyContext))
        {
            foreach (var guardianId in guardianPolicyContext.PreTurnGuardiansById.Keys)
                ids.Add(guardianId);
        }
        return ids;
    }

    private HashSet<string> CollectKnownGuardianIdsForCreateConflictValidation(GuardianPolicyContext? guardianPolicyContext = null)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        guardianPolicyContext ??= _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();

        foreach (var guardianId in guardianPolicyContext.PreTurnGuardiansById.Keys)
            ids.Add(guardianId);

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
        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        var guardiansById = new Dictionary<string, (JsonElement Guardian, string Context)>(StringComparer.OrdinalIgnoreCase);
        var validatedPreTurnGuardianIds = new HashSet<string>(guardianPolicyContext.PreTurnGuardiansById.Keys, StringComparer.OrdinalIgnoreCase);
        if (guardianPolicyContext.HasCurrentGuardiansArray &&
            TryGetGuardianBaselineFailureKind(guardianPolicyContext, out _))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Current canonical guardians[] требует readable validated pre-turn guardians baseline и не использует raw current guardians[] как authority fallback.",
                code: "guardian_missing_validated_preturn_guardians_snapshot",
                section: "Guardians",
                expected: "current validated pending turn snapshot entry for game_state/meta/guardians.json",
                actual: DescribeGuardianTrackedSnapshotFileStatus(guardianPolicyContext.PreTurnGuardiansSnapshot.FileStatus),
                repairHint: "Если current turn materializes guardians[], сохраняй readable validated snapshot copy game_state/meta/guardians.json и не допускай missing/unusable guardian baseline."));
        }

        ValidateGuardianMaterializedStateAgainstAuthority(root, guardianPolicyContext, contextPrefix, issues);

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
                    var guardianId = guardianIdNode.GetString()!;
                    guardiansById[guardianId] = (guardian, guardianContext);
                    if (guardianPolicyContext.HasUsableValidatedPreTurnGuardiansSnapshot &&
                        !validatedPreTurnGuardianIds.Contains(guardianId) &&
                        !guardianPolicyContext.AuthorizedSameTurnCreateGuardianIds.Contains(guardianId))
                    {
                        issues.Add(new ValidationIssue(
                            $"{guardianContext}.guardianId",
                            IssueSeverity.Error,
                            "Current canonical guardians[] не может materialize нового Хранителя поверх validated pre-turn guardian baseline без explicit create surface.",
                            code: "guardian_materialized_without_create_surface",
                            section: "Guardians",
                            expected: "guardianId already present in validated pre-turn guardians[]",
                            actual: guardianId,
                            repairHint: "Нового Хранителя вводи через valid UpdateGuardians.create. Не materialize новый guardian напрямую в current guardians[] поверх уже существующего validated pre-turn canonical state."));
                    }
                }
            }

            ValidateGuardianRelationshipNetwork(guardiansById, issues);
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

    private void ValidateGuardianMaterializedStateAgainstAuthority(
        JsonElement currentGuardiansRoot,
        GuardianPolicyContext guardianPolicyContext,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!guardianPolicyContext.HasCurrentAuthorityRoot)
            return;

        ValidateGuardianMaterializedGuardiansArrayAgainstAuthority(
            currentGuardiansRoot,
            guardianPolicyContext.CurrentAuthorityRoot,
            contextPrefix,
            issues);
        ValidateGuardianMaterializedActiveGuardianAgainstAuthority(
            currentGuardiansRoot,
            guardianPolicyContext.CurrentAuthorityRoot,
            contextPrefix,
            issues);
    }

    private void ValidateGuardianMaterializedGuardiansArrayAgainstAuthority(
        JsonElement currentGuardiansRoot,
        JsonElement authorityRoot,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!currentGuardiansRoot.TryGetProperty("guardians", out var currentGuardians) ||
            currentGuardians.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var currentEntries = BuildGuardianMaterializedEntryMap(currentGuardians);
        if (currentEntries == null)
            return;

        Dictionary<string, JsonElement> authorityEntries;
        if (authorityRoot.ValueKind == JsonValueKind.Object &&
            authorityRoot.TryGetProperty("guardians", out var authorityGuardians) &&
            authorityGuardians.ValueKind == JsonValueKind.Array)
        {
            authorityEntries = BuildGuardianMaterializedEntryMap(authorityGuardians) ??
                               new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            authorityEntries = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        var matchesAuthority =
            currentEntries.Count == authorityEntries.Count &&
            currentEntries.All(pair =>
                authorityEntries.TryGetValue(pair.Key, out var authorityEntry) &&
                JsonElementsSemanticallyEqual(pair.Value, authorityEntry));

        if (matchesAuthority)
            return;

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.guardians",
            IssueSeverity.Error,
            "Current guardians[] must match kernel-authoritative guardian state reconstructed from validated pre-turn baseline and authorized same-turn guardian mutations.",
            code: "guardian_materialized_state_outside_authority",
            section: "Guardians",
            expected: "kernel-authoritative guardians[] only",
            actual: "materialized current guardians[] diverges from kernel authority view",
            repairHint: "Rewrite current guardians[] to match the guardian state reconstructed from validated pre-turn baseline plus authorized same-turn guardian mutations. Raw current guardians[] is a materialized surface, not an authority source."));
    }

    private void ValidateGuardianMaterializedActiveGuardianAgainstAuthority(
        JsonElement currentGuardiansRoot,
        JsonElement authorityRoot,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!currentGuardiansRoot.TryGetProperty("activeGuardian", out var currentActiveGuardian) ||
            currentActiveGuardian.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (authorityRoot.ValueKind != JsonValueKind.Object ||
            !authorityRoot.TryGetProperty("activeGuardian", out var authorityActiveGuardian) ||
            authorityActiveGuardian.ValueKind != JsonValueKind.Object ||
            !JsonElementsSemanticallyEqual(currentActiveGuardian, authorityActiveGuardian))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.activeGuardian",
                IssueSeverity.Error,
                "Current activeGuardian must match kernel-authoritative guardian mirror state.",
                code: "guardian_materialized_state_outside_authority",
                section: "Guardians",
                expected: "kernel-authoritative activeGuardian only",
                actual: "materialized current activeGuardian diverges from kernel authority view",
                repairHint: "Rewrite current activeGuardian to match the mirror reconstructed from kernel-authoritative guardian state. Raw current activeGuardian is not an authority source by itself."));
        }
    }

    private static Dictionary<string, JsonElement>? BuildGuardianMaterializedEntryMap(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var guardian in array.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;
            if (result.ContainsKey(guardianId))
                return null;

            result[guardianId] = guardian;
        }

        return result;
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

        ValidateGuardianCanonicalNameIdentity(guardian, guardianContext, issues);

        ValidateGuardianSourcePreset(guardian, guardianContext, issues);
        ValidateGuardianNameVariants(guardian, guardianContext, issues);
        ValidateGuardianManifestation(guardian, guardianContext, issues);
        ValidateGuardianManifestationHistory(guardian, guardianContext, issues);
        ValidateGuardianPersonalityProfile(guardian, guardianContext, issues);
        ValidateGuardianSocialProfile(guardian, guardianContext, issues);
        ValidateGuardianInterGuardianRelationships(guardian, guardianContext, issues);
        ValidateGuardianRelationshipData(guardian, guardianContext, issues);
        ValidateGuardianQuestManagement(guardian, guardianContext, issues);
        ValidateGuardianGachaState(guardian, guardianContext, issues);
        ValidateGuardianTradeState(guardian, guardianContext, issues);
        ValidateGuardianStoredInnerLifeState(guardian, guardianContext, issues);
    }

    private void ValidateGuardianCanonicalNameIdentity(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(guardianId))
            return;

        var canonicalFacingNames = EnumerateGuardianAliases(guardian, includeGuardianId: false)
            .Cast<string>()
            .ToList();
        if (canonicalFacingNames.Count == 0)
            return;

        if (canonicalFacingNames.All(name => string.Equals(name, guardianId, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new ValidationIssue(
                guardianContext,
                IssueSeverity.Error,
                "Canonical guardian identity должен иметь хотя бы один canonical alias, отличный от raw guardianId.",
                code: "guardian_canonical_name_collapses_to_guardian_id",
                section: "Guardians",
                expected: "canonicalName, nameVariants.default or manifestation.currentDisplayName distinct from guardianId",
                actual: guardianId,
                repairHint: "Используй для canonical guardian identity человекочитаемое имя/alias. Raw guardianId допустим как technical identifier, но не как единственный canonical display alias."));
        }
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

    private void ValidateGuardianSocialProfile(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("socialProfile", out var socialProfile) || socialProfile.ValueKind == JsonValueKind.Null)
            return;

        if (!RequireObject(socialProfile, $"{guardianContext}.socialProfile", issues))
            return;

        var socialContext = $"{guardianContext}.socialProfile";
        ValidateGuardianSocialFactor(socialProfile, socialContext, issues, "jealousyFactor");
        ValidateGuardianSocialFactor(socialProfile, socialContext, issues, "curiosityFactor");
        ValidateGuardianSocialFactor(socialProfile, socialContext, issues, "competitiveFactor");
        ValidateGuardianSocialFactor(socialProfile, socialContext, issues, "generosityFactor");
        ValidateGuardianSocialFactor(socialProfile, socialContext, issues, "isolationistTendency");
    }

    private void ValidateGuardianSocialFactor(JsonElement socialProfile, string socialContext, List<ValidationIssue> issues, string fieldName)
    {
        if (!socialProfile.TryGetProperty(fieldName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        if (!value.TryGetInt32(out var parsed))
        {
            issues.Add(new ValidationIssue(
                $"{socialContext}.{fieldName}",
                IssueSeverity.Error,
                "Guardian socialProfile factor должен быть integer",
                code: "guardian_social_profile_factor_invalid_type",
                section: "Guardians",
                expected: "integer 0..100",
                actual: value.ValueKind.ToString(),
                repairHint: $"Сохраняй {fieldName} как integer 0..100."));
            return;
        }

        if (parsed < 0 || parsed > 100)
        {
            issues.Add(new ValidationIssue(
                $"{socialContext}.{fieldName}",
                IssueSeverity.Error,
                "Guardian socialProfile factor должен быть в диапазоне 0..100",
                code: "guardian_social_profile_factor_out_of_bounds",
                section: "Guardians",
                expected: "0..100",
                actual: parsed.ToString(),
                repairHint: $"Ограничь {fieldName} canonical диапазоном 0..100."));
        }
    }

    private void ValidateGuardianInterGuardianRelationships(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("guardianRelationships", out var relationships) || relationships.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.guardianRelationships",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать guardianRelationships array",
                code: "guardian_missing_inter_guardian_relationships",
                section: "Guardians",
                expected: "guardianRelationships array",
                actual: "missing",
                repairHint: "Сохраняй guardianRelationships как canonical межхранительскую сеть, даже если она пока пуста."));
            return;
        }

        RequireArrayOfObjects(relationships, $"{guardianContext}.guardianRelationships", issues);
        if (relationships.ValueKind != JsonValueKind.Array)
            return;

        var selfGuardianId = GetFirstNonEmptyString(guardian, "guardianId", "id") ?? string.Empty;
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var relationship in relationships.EnumerateArray())
        {
            var relationshipContext = $"{guardianContext}.guardianRelationships[{index++}]";
            if (!RequireObject(relationship, relationshipContext, issues))
                continue;

            var targetGuardianId = RequireString(relationship, relationshipContext, issues, "targetGuardianId");
            ValidateOptionalNullableStringField(relationship, relationshipContext, issues, "targetName");
            RequireString(relationship, relationshipContext, issues, "reason");
            ValidateOptionalNullableStringField(relationship, relationshipContext, issues, "lastChangedAt");
            ValidateOptionalNullableStringField(relationship, relationshipContext, issues, "awarenessLevel");

            if (!string.IsNullOrWhiteSpace(targetGuardianId) &&
                string.Equals(targetGuardianId, selfGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.targetGuardianId",
                    IssueSeverity.Error,
                    "guardianRelationships не должен ссылаться на самого себя",
                    code: "guardian_relationship_self_reference",
                    section: "Guardians",
                    actual: targetGuardianId,
                    repairHint: "Не создавай inter-guardian standing entry на самого себя."));
            }

            if (!string.IsNullOrWhiteSpace(targetGuardianId) && !seenTargets.Add(targetGuardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.targetGuardianId",
                    IssueSeverity.Error,
                    "guardianRelationships не должен содержать duplicate targetGuardianId",
                    code: "guardian_relationship_duplicate_target",
                    section: "Guardians",
                    actual: targetGuardianId,
                    repairHint: "Для каждой пары Хранителей сохраняй только одну directed entry."));
            }

            if (!relationship.TryGetProperty("attitudeScore", out var scoreNode) ||
                scoreNode.ValueKind != JsonValueKind.Number ||
                !scoreNode.TryGetInt32(out var attitudeScore))
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.attitudeScore",
                    IssueSeverity.Error,
                    "guardianRelationships entry должен содержать integer attitudeScore",
                    code: "guardian_relationship_missing_attitude_score",
                    section: "Guardians",
                    expected: "integer -100..100",
                    actual: relationship.TryGetProperty("attitudeScore", out var actualNode) ? actualNode.ValueKind.ToString() : "missing",
                    repairHint: "Сохраняй attitudeScore как canonical numeric standing между Хранителями."));
                continue;
            }

            if (attitudeScore < GuardianRelationshipRules.MinAttitudeScore || attitudeScore > GuardianRelationshipRules.MaxAttitudeScore)
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.attitudeScore",
                    IssueSeverity.Error,
                    "guardianRelationships.attitudeScore должен быть в диапазоне -100..100",
                    code: "guardian_relationship_attitude_score_out_of_bounds",
                    section: "Guardians",
                    expected: "-100..100",
                    actual: attitudeScore.ToString(),
                    repairHint: "Не выводи attitudeScore за пределы canonical scale -100..100."));
            }

            var attitudeTier = RequireString(relationship, relationshipContext, issues, "attitudeTier");
            if (!string.IsNullOrWhiteSpace(attitudeTier) && !GuardianRelationshipRules.IsValidAttitudeTier(attitudeTier))
            {
                issues.Add(new ValidationIssue(
                    $"{relationshipContext}.attitudeTier",
                    IssueSeverity.Error,
                    "guardianRelationships.attitudeTier должен быть canonical tier value",
                    code: "guardian_relationship_invalid_attitude_tier",
                    section: "Guardians",
                    expected: "trusted|ally|neutral|competitive|rival|enemy",
                    actual: attitudeTier,
                    repairHint: "Используй только canonical attitude tier значения для межхранительской сети."));
            }
            else if (!string.IsNullOrWhiteSpace(attitudeTier))
            {
                var expectedTier = GuardianRelationshipRules.ResolveAttitudeTier(attitudeScore);
                if (!string.Equals(attitudeTier, expectedTier, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{relationshipContext}.attitudeTier",
                        IssueSeverity.Error,
                        "guardianRelationships.attitudeTier должен совпадать с derived tier от attitudeScore",
                        code: "guardian_relationship_attitude_tier_mismatch",
                        section: "Guardians",
                        expected: expectedTier,
                        actual: attitudeTier,
                        repairHint: "Синхронизируй attitudeTier с canonical derived tier от attitudeScore."));
                }
            }

            if (relationship.TryGetProperty("lastChangedAt", out var lastChangedAt) && lastChangedAt.ValueKind == JsonValueKind.String)
            {
                var lastChangedAtValue = lastChangedAt.GetString();
                if (!string.IsNullOrWhiteSpace(lastChangedAtValue) && !DateTimeOffset.TryParse(lastChangedAtValue, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{relationshipContext}.lastChangedAt",
                        IssueSeverity.Error,
                        "guardianRelationships.lastChangedAt должен быть ISO 8601 timestamp или null",
                        code: "guardian_relationship_invalid_last_changed_at",
                        section: "Guardians",
                        expected: "ISO 8601 timestamp or null",
                        actual: lastChangedAtValue,
                        repairHint: "Используй для lastChangedAt ISO 8601 timestamp или null."));
                }
            }
        }
    }

    private void ValidateGuardianRelationshipNetwork(
        Dictionary<string, (JsonElement Guardian, string Context)> guardiansById,
        List<ValidationIssue> issues)
    {
        if (guardiansById.Count <= 1)
            return;

        foreach (var (guardianId, guardianEntry) in guardiansById)
        {
            if (!guardianEntry.Guardian.TryGetProperty("guardianRelationships", out var relationships) ||
                relationships.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var targets = relationships.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => GetFirstNonEmptyString(item, "targetGuardianId"))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var targetGuardianId in targets)
            {
                if (!guardiansById.ContainsKey(targetGuardianId!))
                {
                    issues.Add(new ValidationIssue(
                        $"{guardianEntry.Context}.guardianRelationships",
                        IssueSeverity.Error,
                        "guardianRelationships ссылается на неизвестного Хранителя",
                        code: "guardian_relationship_unknown_target",
                        section: "Guardians",
                        actual: targetGuardianId,
                        repairHint: "Используй в targetGuardianId только существующий guardianId из canonical guardians state."));
                    continue;
                }
            }

            foreach (var otherGuardianId in guardiansById.Keys)
            {
                if (string.Equals(otherGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!targets.Contains(otherGuardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"{guardianEntry.Context}.guardianRelationships",
                        IssueSeverity.Error,
                        "Canonical inter-guardian network должен содержать directed entry для каждого другого Хранителя",
                        code: "guardian_relationship_missing_network_edge",
                        section: "Guardians",
                        expected: otherGuardianId,
                        actual: "missing",
                        repairHint: "Для каждой пары Хранителей materialize directed guardianRelationships entry в обе стороны."));
                }
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
        var hasTrackerValidationRoot = TryResolveGuardianProjectTrackerValidationRootSync(out var trackerRoot, out _);

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
            var derivedState = hasTrackerValidationRoot
                ? GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot)
                : GuardianProjectState.ResolveGuardianDerivedState(guardian);
            if (availableQuests.ValueKind == JsonValueKind.Array &&
                availableQuests.GetArrayLength() > derivedState.GuardianQuestCap)
            {
                issues.Add(new ValidationIssue(
                    $"{questContext}.availableQuests",
                    IssueSeverity.Error,
                    "Guardian questManagement превышает cap доступных квестов для текущей силы Обители",
                    code: "guardian_available_quests_limit_exceeded",
                    section: "Guardians",
                    expected: $"0..{derivedState.GuardianQuestCap} available quests",
                    actual: availableQuests.GetArrayLength().ToString(),
                    repairHint: "Синхронизируй число availableQuests с shared derived guardianQuestCap, а не с локальной ad-hoc формулой."));
            }
            ValidateGuardianAvailableQuestDifficultyCeiling(
                availableQuests,
                $"{questContext}.availableQuests",
                derivedState.GuardianQuestDifficultyCeiling,
                issues);

            if (hasTrackerValidationRoot)
                ValidateGuardianLoreResearchQuestOrigins(availableQuests, $"{questContext}.availableQuests", guardianId ?? string.Empty, trackerRoot, issues);
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
            if (hasTrackerValidationRoot)
                ValidateGuardianLoreResearchQuestOrigins(activeQuests, $"{questContext}.activeQuests", guardianId ?? string.Empty, trackerRoot, issues);
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
            trackerRoot,
            hasTrackerValidationRoot,
            issues);
    }


    private void ValidateGuardianLoreResearchQuestOrigins(
        JsonElement questArray,
        string arrayContext,
        string guardianId,
        JsonElement trackerRoot,
        List<ValidationIssue> issues)
    {
        if (questArray.ValueKind != JsonValueKind.Array || string.IsNullOrWhiteSpace(guardianId))
            return;

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

            var grantedTokens = ReadGrantedLoreResearchQuestTokens(trackerRoot, guardianId, sourceProjectId, questOrigin ?? string.Empty);
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

            if (currentUsage.Item1 > ReadGrantedLoreResearchQuestTokens(trackerRoot, guardianId, sourceProjectId, GuardianProjectState.LoreResearchHookOrigin) ||
                currentUsage.Item2 > ReadGrantedLoreResearchQuestTokens(trackerRoot, guardianId, sourceProjectId, GuardianProjectState.LoreResearchSpecialLineOrigin) ||
                currentUsage.Item3 > ReadGrantedLoreResearchQuestTokens(trackerRoot, guardianId, sourceProjectId, GuardianProjectState.ArchiveConsultationHookOrigin))
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

    private void ValidateGuardianAbodeResidentsStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateProperty, out var updates))
        {
            RequireArrayOfObjects(updates, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateProperty}", issues);
            if (updates.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var resident in updates.EnumerateArray())
                {
                    var residentContext = $"{contextPrefix}.{GuardianAbodeResidentState.UpdateProperty}[{index++}]";
                    if (!RequireObject(resident, residentContext, issues))
                        continue;

                    ValidateGuardianAbodeResidentObject(resident, residentContext, issues);
                }
            }
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.EntriesProperty, out var entries))
        {
            RequireArrayOfObjects(entries, $"{contextPrefix}.{GuardianAbodeResidentState.EntriesProperty}", issues);
            if (entries.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var resident in entries.EnumerateArray())
                {
                    var residentContext = $"{contextPrefix}.{GuardianAbodeResidentState.EntriesProperty}[{index++}]";
                    if (!RequireObject(resident, residentContext, issues))
                        continue;

                    ValidateGuardianAbodeResidentObject(resident, residentContext, issues);
                }
            }
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateRosterReceiptsProperty, out var updateRosterReceipts))
        {
            RequireArrayOfObjects(updateRosterReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateRosterReceiptsProperty}", issues);
            if (updateRosterReceipts.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentRosterReceipts(updateRosterReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateRosterReceiptsProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.RosterReceiptsProperty, out var rosterReceipts))
        {
            RequireArrayOfObjects(rosterReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.RosterReceiptsProperty}", issues);
            if (rosterReceipts.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentRosterReceipts(rosterReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.RosterReceiptsProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateInteractionReceiptsProperty, out var updateInteractionReceipts))
        {
            RequireArrayOfObjects(updateInteractionReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateInteractionReceiptsProperty}", issues);
            if (updateInteractionReceipts.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentInteractionReceipts(updateInteractionReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateInteractionReceiptsProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.InteractionReceiptsProperty, out var interactionReceipts))
        {
            RequireArrayOfObjects(interactionReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.InteractionReceiptsProperty}", issues);
            if (interactionReceipts.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentInteractionReceipts(interactionReceipts, $"{contextPrefix}.{GuardianAbodeResidentState.InteractionReceiptsProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateHistoryLogProperty, out var updateHistoryLog))
        {
            RequireArrayOfObjects(updateHistoryLog, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateHistoryLogProperty}", issues);
            if (updateHistoryLog.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentHistoryLog(updateHistoryLog, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateHistoryLogProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.HistoryLogProperty, out var historyLog))
        {
            RequireArrayOfObjects(historyLog, $"{contextPrefix}.{GuardianAbodeResidentState.HistoryLogProperty}", issues);
            if (historyLog.ValueKind == JsonValueKind.Array)
                ValidateGuardianAbodeResidentHistoryLog(historyLog, $"{contextPrefix}.{GuardianAbodeResidentState.HistoryLogProperty}", issues);
        }

        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateThoughtJournalProperty, out var updateThoughtJournal))
            ValidateActorJournalEntriesArray(updateThoughtJournal, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateThoughtJournalProperty}", issues, "residentId", "ResidentThoughtJournal");

        if (root.TryGetProperty(GuardianAbodeResidentState.ThoughtJournalProperty, out var thoughtJournal))
            ValidateActorJournalEntriesArray(thoughtJournal, $"{contextPrefix}.{GuardianAbodeResidentState.ThoughtJournalProperty}", issues, "residentId", "ResidentThoughtJournal");

        if (root.TryGetProperty(GuardianAbodeResidentState.UpdateInteractionLogProperty, out var updateInteractionLog))
            ValidateActorJournalEntriesArray(updateInteractionLog, $"{contextPrefix}.{GuardianAbodeResidentState.UpdateInteractionLogProperty}", issues, "residentId", "ResidentInteractionLog");

        if (root.TryGetProperty(GuardianAbodeResidentState.InteractionLogProperty, out var interactionLog))
            ValidateActorJournalEntriesArray(interactionLog, $"{contextPrefix}.{GuardianAbodeResidentState.InteractionLogProperty}", issues, "residentId", "ResidentInteractionLog");
    }

    private void ValidatePendingGuardianAbodeResidentsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(GuardianAbodeResidentRequestState.ResidentsRequestsProperty, out var requests))
        {
            RequireArrayOfObjects(requests, $"{contextPrefix}.{GuardianAbodeResidentRequestState.ResidentsRequestsProperty}", issues);
            if (requests.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var request in requests.EnumerateArray())
            {
                var requestContext = $"{contextPrefix}.{GuardianAbodeResidentRequestState.ResidentsRequestsProperty}[{index++}]";
                if (!RequireObject(request, requestContext, issues))
                    continue;

                ValidatePendingGuardianAbodeResidentsRequestObject(request, requestContext, issues);
            }

            return;
        }

        ValidatePendingGuardianAbodeResidentsRequestObject(root, contextPrefix, issues);
    }

    private void ValidatePendingGuardianAbodeResidentInteractionsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(GuardianAbodeResidentRequestState.InteractionRequestsProperty, out var requests))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{GuardianAbodeResidentRequestState.InteractionRequestsProperty}",
                IssueSeverity.Error,
                "pending_guardian_abode_resident_interactions.json должен содержать requests array",
                code: "pending_abode_resident_interactions_missing_requests",
                section: "AfterlifeResidents",
                expected: "requests array",
                actual: "missing",
                repairHint: "Сохраняй pending resident interaction requests как object с requests[]."));
            return;
        }

        RequireArrayOfObjects(requests, $"{contextPrefix}.{GuardianAbodeResidentRequestState.InteractionRequestsProperty}", issues);
        if (requests.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var request in requests.EnumerateArray())
        {
            var requestContext = $"{contextPrefix}.{GuardianAbodeResidentRequestState.InteractionRequestsProperty}[{index++}]";
            if (!RequireObject(request, requestContext, issues))
                continue;

            ValidatePendingGuardianAbodeResidentInteractionRequestObject(request, requestContext, issues);
        }
    }

    private void ValidatePendingResidentCompanionManifestationRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(GuardianAbodeResidentRequestState.ManifestationRequestsProperty, out var requests))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{GuardianAbodeResidentRequestState.ManifestationRequestsProperty}",
                IssueSeverity.Error,
                "pending_resident_companion_manifestation_request.json должен содержать requests array",
                code: "pending_resident_companion_manifestation_missing_requests",
                section: "AfterlifeResidents",
                expected: "requests array",
                actual: "missing",
                repairHint: "Сохраняй pending manifestation requests как object с requests[]."));
            return;
        }

        RequireArrayOfObjects(requests, $"{contextPrefix}.{GuardianAbodeResidentRequestState.ManifestationRequestsProperty}", issues);
        if (requests.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        var seenRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests.EnumerateArray())
        {
            var requestContext = $"{contextPrefix}.{GuardianAbodeResidentRequestState.ManifestationRequestsProperty}[{index++}]";
            if (!RequireObject(request, requestContext, issues))
                continue;

            RequireString(request, requestContext, issues, "requestId");
            RequireString(request, requestContext, issues, "manifestationSource");
            RequireString(request, requestContext, issues, "relicId");
            RequireString(request, requestContext, issues, "relicName");
            ValidateOptionalString(request, requestContext, issues, "sourceResidentId");
            ValidateOptionalString(request, requestContext, issues, "sourceImprintId");
            ValidateOptionalString(request, requestContext, issues, "sourceGuardianId");
            ValidateOptionalString(request, requestContext, issues, "sourceGuardianName");
            ValidatePositiveNumberField(request, requestContext, issues, "targetIncarnation");
            RequireString(request, requestContext, issues, "companionNameHint");
            RequireString(request, requestContext, issues, "originWorldSummary");
            RequireString(request, requestContext, issues, "futureCompanionPrompt");
            ValidateOptionalString(request, requestContext, issues, "bondReason");
            if (request.TryGetProperty("coreTraits", out var coreTraits))
                RequireArrayOfStrings(coreTraits, $"{requestContext}.coreTraits", issues);
            if (request.TryGetProperty("archetypeHints", out var archetypeHints))
                RequireArrayOfStrings(archetypeHints, $"{requestContext}.archetypeHints", issues);
            if (request.TryGetProperty("appearanceMotifs", out var appearanceMotifs))
                RequireArrayOfStrings(appearanceMotifs, $"{requestContext}.appearanceMotifs", issues);
            ValidateRequiredIsoTimestampField(
                request,
                requestContext,
                issues,
                "createdAtUtc",
                "AfterlifeResidents",
                "pending_resident_companion_manifestation_missing_created_at_utc",
                "pending_resident_companion_manifestation_invalid_created_at_utc",
                "Каждый pending resident companion manifestation request должен содержать createdAtUtc в ISO 8601 формате.");

            var manifestationSource = GetFirstNonEmptyString(request, "manifestationSource");
            if (!string.Equals(manifestationSource, "resident_relic", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(manifestationSource, "imprint_relic", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{requestContext}.manifestationSource",
                    IssueSeverity.Error,
                    "manifestationSource должен быть canonical resident companion source",
                    code: "pending_resident_companion_manifestation_invalid_source",
                    section: "AfterlifeResidents",
                    expected: "resident_relic | imprint_relic",
                    actual: manifestationSource,
                    repairHint: "Используй manifestationSource = resident_relic для companion_echo и imprint_relic для реликвий со слепком НПС."));
            }

            if (string.Equals(manifestationSource, "resident_relic", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(GetFirstNonEmptyString(request, "sourceResidentId")))
            {
                issues.Add(new ValidationIssue(
                    $"{requestContext}.sourceResidentId",
                    IssueSeverity.Error,
                    "resident_relic manifestation request должен содержать sourceResidentId",
                    code: "pending_resident_companion_manifestation_missing_resident_id",
                    section: "AfterlifeResidents",
                    repairHint: "Для companion_echo-реликвии сохраняй sourceResidentId из companionSeed."));
            }

            if (string.Equals(manifestationSource, "imprint_relic", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(GetFirstNonEmptyString(request, "sourceImprintId")))
            {
                issues.Add(new ValidationIssue(
                    $"{requestContext}.sourceImprintId",
                    IssueSeverity.Error,
                    "imprint_relic manifestation request должен содержать sourceImprintId",
                    code: "pending_resident_companion_manifestation_missing_imprint_id",
                    section: "AfterlifeResidents",
                    repairHint: "Для реликвии со слепком НПС сохраняй sourceImprintId из embedded soulImprint/npcSoulImprint."));
            }

            var relicId = GetFirstNonEmptyString(request, "relicId");
            if (!string.IsNullOrWhiteSpace(relicId) && !seenRelicIds.Add(relicId))
            {
                issues.Add(new ValidationIssue(
                    $"{requestContext}.relicId",
                    IssueSeverity.Error,
                    "pending resident companion manifestation requests не должны дублировать relicId",
                    code: "pending_resident_companion_manifestation_duplicate_relic_id",
                    section: "AfterlifeResidents",
                    expected: "unique relicId per pending request",
                    actual: relicId,
                    repairHint: "Не создавай несколько pending manifestation requests для одной и той же реликвии души."));
            }
        }
    }

    private void ValidateGuardianAbodeResidentObject(JsonElement resident, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(resident, contextPrefix, issues, "residentId");
        RequireString(resident, contextPrefix, issues, "guardianId");
        RequireString(resident, contextPrefix, issues, "abodeId");
        RequireString(resident, contextPrefix, issues, "displayName");
        RequireString(resident, contextPrefix, issues, "residentKind");
        RequireString(resident, contextPrefix, issues, "originType");
        ValidateOptionalString(resident, contextPrefix, issues, "roleLabel");
        ValidateOptionalString(resident, contextPrefix, issues, "summary");
        ValidateNonNegativeIntegerField(resident, contextPrefix, issues, "bondLevel", "AfterlifeResidents");
        RequireString(resident, contextPrefix, issues, "bondTier");
        RequireBooleanField(resident, contextPrefix, issues, "canGrantCompanionRelic");
        RequireString(resident, contextPrefix, issues, "bondRewardState");
        ValidateOptionalString(resident, contextPrefix, issues, "linkedSoulQuestId");
        ValidateOptionalString(resident, contextPrefix, issues, "grantedRelicId");
        if (resident.TryGetProperty("historyRevealed", out var historyRevealed))
            RequireBooleanField(resident, contextPrefix, issues, "historyRevealed");
        RequireBooleanField(resident, contextPrefix, issues, "isPresent");

        var residentKind = GetFirstNonEmptyString(resident, "residentKind");
        if (!string.IsNullOrWhiteSpace(residentKind) && !GuardianAbodeResidentState.IsSupportedResidentKind(residentKind))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.residentKind",
                IssueSeverity.Error,
                "residentKind должен быть canonical afterlife resident kind",
                code: "guardian_abode_resident_invalid_kind",
                section: "AfterlifeResidents",
                expected: "junior_spirit | attendant_spirit | wayfaring_soul | bound_soul",
                actual: residentKind,
                repairHint: "Используй для residentKind только canonical afterlife-resident kinds."));
        }

        var originType = GetFirstNonEmptyString(resident, "originType");
        if (!string.IsNullOrWhiteSpace(originType) && !GuardianAbodeResidentState.IsSupportedOriginType(originType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.originType",
                IssueSeverity.Error,
                "originType должен быть canonical resident origin type",
                code: "guardian_abode_resident_invalid_origin_type",
                section: "AfterlifeResidents",
                expected: "native_spirit | traveler_soul",
                actual: originType,
                repairHint: "Используй для originType только native_spirit или traveler_soul."));
        }

        var bondLevel = resident.TryGetProperty("bondLevel", out var bondNode) && bondNode.ValueKind == JsonValueKind.Number && bondNode.TryGetInt32(out var parsedBond)
            ? parsedBond
            : 0;
        var expectedTier = GuardianAbodeResidentState.ResolveBondTier(bondLevel);
        var actualTier = GetFirstNonEmptyString(resident, "bondTier");
        if (!string.IsNullOrWhiteSpace(actualTier) && !GuardianAbodeResidentState.IsSupportedBondTier(actualTier))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.bondTier",
                IssueSeverity.Error,
                "bondTier должен быть canonical afterlife resident tier",
                code: "guardian_abode_resident_invalid_bond_tier",
                section: "AfterlifeResidents",
                expected: "stranger | familiar | trusted | bound",
                actual: actualTier,
                repairHint: "Используй для bondTier только stranger, familiar, trusted или bound."));
        }
        else if (!string.IsNullOrWhiteSpace(actualTier) &&
                 !string.Equals(actualTier, expectedTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.bondTier",
                IssueSeverity.Error,
                "bondTier должен совпадать с tier, выведенным из bondLevel",
                code: "guardian_abode_resident_bond_tier_mismatch",
                section: "AfterlifeResidents",
                expected: expectedTier,
                actual: actualTier,
                repairHint: "Синхронизируй bondTier с bondLevel: 0-24 stranger, 25-49 familiar, 50-74 trusted, 75-100 bound."));
        }

        var rewardState = GetFirstNonEmptyString(resident, "bondRewardState");
        if (!string.IsNullOrWhiteSpace(rewardState) && !GuardianAbodeResidentState.IsSupportedRewardState(rewardState))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.bondRewardState",
                IssueSeverity.Error,
                "bondRewardState должен быть canonical resident reward state",
                code: "guardian_abode_resident_invalid_reward_state",
                section: "AfterlifeResidents",
                expected: "none | eligible | granted | consumed",
                actual: rewardState,
                repairHint: "Используй для bondRewardState только none, eligible, granted или consumed."));
        }
        else if (!string.IsNullOrWhiteSpace(rewardState) &&
                 !string.Equals(rewardState, GuardianAbodeResidentState.RewardStateNone, StringComparison.OrdinalIgnoreCase) &&
                 !(resident.TryGetProperty("canGrantCompanionRelic", out var canGrantProp) && canGrantProp.ValueKind == JsonValueKind.True))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.bondRewardState",
                IssueSeverity.Error,
                "Resident без права даровать companion relic не может иметь reward state выше none",
                code: "guardian_abode_resident_reward_state_without_grant_permission",
                section: "AfterlifeResidents",
                expected: GuardianAbodeResidentState.RewardStateNone,
                actual: rewardState,
                repairHint: "Если resident не может даровать реликвию связи, сохраняй canGrantCompanionRelic=false и bondRewardState=none."));
        }

        if ((string.Equals(rewardState, GuardianAbodeResidentState.RewardStateGranted, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(rewardState, GuardianAbodeResidentState.RewardStateConsumed, StringComparison.OrdinalIgnoreCase)) &&
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(resident, "grantedRelicId")))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.grantedRelicId",
                IssueSeverity.Error,
                "Resident с granted/consumed reward state должен хранить grantedRelicId",
                code: "guardian_abode_resident_missing_granted_relic_id",
                section: "AfterlifeResidents",
                repairHint: "Когда resident дарует реликвию связи, сохраняй grantedRelicId и не очищай его после будущих воплощений."));
        }

        if (!resident.TryGetProperty("mortalWorldImprint", out var imprint) ||
            !RequireObject(imprint, $"{contextPrefix}.mortalWorldImprint", issues))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.mortalWorldImprint",
                IssueSeverity.Error,
                "afterlife resident должен содержать mortalWorldImprint object",
                code: "guardian_abode_resident_missing_imprint",
                section: "AfterlifeResidents",
                expected: "mortalWorldImprint object",
                actual: !resident.TryGetProperty("mortalWorldImprint", out var actualImprint) ? "missing" : actualImprint.ValueKind.ToString(),
                repairHint: "Сохраняй у каждого resident mortalWorldImprint с originWorldSummary и futureCompanionPrompt."));
            return;
        }

        RequireString(imprint, $"{contextPrefix}.mortalWorldImprint", issues, "originWorldSummary");
        RequireString(imprint, $"{contextPrefix}.mortalWorldImprint", issues, "futureCompanionPrompt");
        ValidateOptionalString(imprint, $"{contextPrefix}.mortalWorldImprint", issues, "bondReason");
        if (imprint.TryGetProperty("coreTraits", out var coreTraits))
            RequireArrayOfStrings(coreTraits, $"{contextPrefix}.mortalWorldImprint.coreTraits", issues);
        if (imprint.TryGetProperty("archetypeHints", out var archetypeHints))
            RequireArrayOfStrings(archetypeHints, $"{contextPrefix}.mortalWorldImprint.archetypeHints", issues);
        if (imprint.TryGetProperty("appearanceMotifs", out var appearanceMotifs))
            RequireArrayOfStrings(appearanceMotifs, $"{contextPrefix}.mortalWorldImprint.appearanceMotifs", issues);

        if (resident.TryGetProperty("availableInteractions", out var availableInteractions))
        {
            RequireArrayOfStrings(availableInteractions, $"{contextPrefix}.availableInteractions", issues);
            if (availableInteractions.ValueKind == JsonValueKind.Array)
            {
                var interactionIndex = 0;
                foreach (var interaction in availableInteractions.EnumerateArray())
                {
                    var interactionValue = interaction.ValueKind == JsonValueKind.String ? interaction.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(interactionValue) || GuardianAbodeResidentState.IsSupportedInteractionType(interactionValue) || string.Equals(interactionValue, "quest", StringComparison.OrdinalIgnoreCase) || string.Equals(interactionValue, "reward", StringComparison.OrdinalIgnoreCase))
                    {
                        interactionIndex++;
                        continue;
                    }

                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.availableInteractions[{interactionIndex}]",
                        IssueSeverity.Error,
                        "availableInteractions должен использовать canonical resident interaction token",
                        code: "guardian_abode_resident_invalid_interaction_token",
                        section: "AfterlifeResidents",
                        expected: "talk | history | quest | reward",
                        actual: interactionValue,
                        repairHint: "Для resident.availableInteractions используй только talk, history, quest или reward."));
                    interactionIndex++;
                }
            }
        }
    }

    private void ValidatePendingGuardianAbodeResidentsRequestObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "requestId");
        RequireString(root, contextPrefix, issues, "guardianId");
        RequireString(root, contextPrefix, issues, "guardianName");
        RequireString(root, contextPrefix, issues, "abodeId");
        ValidateOptionalString(root, contextPrefix, issues, "abodeName");
        ValidateIntegerField(root, contextPrefix, issues, "currentReputation");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "AfterlifeResidents");
        ValidateRequiredIsoTimestampField(
            root,
            contextPrefix,
            issues,
            "createdAtUtc",
            "AfterlifeResidents",
            "pending_abode_residents_request_missing_created_at_utc",
            "pending_abode_residents_request_invalid_created_at_utc",
            "pending_guardian_abode_residents_request.json должен содержать createdAtUtc в ISO 8601 формате.");
    }

    private void ValidatePendingGuardianAbodeResidentInteractionRequestObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "requestId");
        RequireString(root, contextPrefix, issues, "guardianId");
        ValidateOptionalString(root, contextPrefix, issues, "guardianName");
        RequireString(root, contextPrefix, issues, "abodeId");
        ValidateOptionalString(root, contextPrefix, issues, "abodeName");
        RequireString(root, contextPrefix, issues, "residentId");
        ValidateOptionalString(root, contextPrefix, issues, "residentName");
        var interactionType = RequireString(root, contextPrefix, issues, "interactionType");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "AfterlifeResidents");
        ValidateRequiredIsoTimestampField(
            root,
            contextPrefix,
            issues,
            "createdAtUtc",
            "AfterlifeResidents",
            "pending_abode_resident_interaction_missing_created_at_utc",
            "pending_abode_resident_interaction_invalid_created_at_utc",
            "pending_guardian_abode_resident_interactions.json должен содержать createdAtUtc в ISO 8601 формате.");

        if (!string.IsNullOrWhiteSpace(interactionType) && !GuardianAbodeResidentState.IsSupportedInteractionType(interactionType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.interactionType",
                IssueSeverity.Error,
                "resident interaction request должен использовать canonical interactionType",
                code: "pending_abode_resident_interaction_invalid_type",
                section: "AfterlifeResidents",
                expected: $"{GuardianAbodeResidentState.InteractionTypeTalk} | {GuardianAbodeResidentState.InteractionTypeHistory}",
                actual: interactionType,
                repairHint: "Для pending resident interaction request используй interactionType = talk или history."));
        }
    }

    private void ValidateGuardianAbodeResidentInteractionReceipts(JsonElement receipts, string contextPrefix, List<ValidationIssue> issues)
    {
        var receiptIndex = 0;
        foreach (var receipt in receipts.EnumerateArray())
        {
            var receiptContext = $"{contextPrefix}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            RequireString(receipt, receiptContext, issues, "requestId");
            RequireString(receipt, receiptContext, issues, "residentId");
            RequireString(receipt, receiptContext, issues, "guardianId");
            RequireString(receipt, receiptContext, issues, "abodeId");
            var interactionType = RequireString(receipt, receiptContext, issues, "interactionType");
            var status = RequireString(receipt, receiptContext, issues, "status");
            ValidateOptionalString(receipt, receiptContext, issues, "guardianName");
            ValidateOptionalString(receipt, receiptContext, issues, "abodeName");
            ValidateOptionalString(receipt, receiptContext, issues, "residentName");
            ValidateOptionalString(receipt, receiptContext, issues, "responseMode");
            ValidateOptionalString(receipt, receiptContext, issues, "historyEntryId");
            ValidateOptionalString(receipt, receiptContext, issues, "reason");
            ValidateNonNegativeIntegerField(receipt, receiptContext, issues, "resolvedAtTurn", "AfterlifeResidents");
            ValidateRequiredIsoTimestampField(
                receipt,
                receiptContext,
                issues,
                "resolvedAtUtc",
                "AfterlifeResidents",
                "guardian_abode_resident_receipt_missing_resolved_at_utc",
                "guardian_abode_resident_receipt_invalid_resolved_at_utc",
                "Resident interaction receipt должен содержать resolvedAtUtc в ISO 8601 формате.");

            if (!string.IsNullOrWhiteSpace(interactionType) && !GuardianAbodeResidentState.IsSupportedInteractionType(interactionType))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.interactionType",
                    IssueSeverity.Error,
                    "Resident interaction receipt использует неподдерживаемый interactionType",
                    code: "guardian_abode_resident_receipt_invalid_interaction_type",
                    section: "AfterlifeResidents",
                    expected: $"{GuardianAbodeResidentState.InteractionTypeTalk} | {GuardianAbodeResidentState.InteractionTypeHistory}",
                    actual: interactionType));
            }

            if (!string.IsNullOrWhiteSpace(status) && !GuardianAbodeResidentState.IsSupportedInteractionStatus(status))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.status",
                    IssueSeverity.Error,
                    "Resident interaction receipt использует неподдерживаемый status",
                    code: "guardian_abode_resident_receipt_invalid_status",
                    section: "AfterlifeResidents",
                    expected: $"{GuardianAbodeResidentState.InteractionStatusAccepted} | {GuardianAbodeResidentState.InteractionStatusRejected} | {GuardianAbodeResidentState.InteractionStatusCancelled}",
                    actual: status));
            }

            var responseMode = GetFirstNonEmptyString(receipt, "responseMode");
            if (!string.IsNullOrWhiteSpace(responseMode) && !GuardianAbodeResidentState.IsSupportedResponseMode(responseMode))
            {
                issues.Add(new ValidationIssue(
                    $"{receiptContext}.responseMode",
                    IssueSeverity.Error,
                    "Resident interaction receipt использует неподдерживаемый responseMode",
                    code: "guardian_abode_resident_receipt_invalid_response_mode",
                    section: "AfterlifeResidents",
                    expected: "talk_scene | history_revealed | history_refused | history_partial | bond_shift_only",
                    actual: responseMode));
            }
        }
    }

    private void ValidateGuardianAbodeResidentRosterReceipts(JsonElement receipts, string contextPrefix, List<ValidationIssue> issues)
    {
        var receiptIndex = 0;
        foreach (var receipt in receipts.EnumerateArray())
        {
            var receiptContext = $"{contextPrefix}[{receiptIndex++}]";
            if (!RequireObject(receipt, receiptContext, issues))
                continue;

            RequireString(receipt, receiptContext, issues, "requestId");
            RequireString(receipt, receiptContext, issues, "guardianId");
            RequireString(receipt, receiptContext, issues, "abodeId");
            ValidateOptionalString(receipt, receiptContext, issues, "guardianName");
            ValidateOptionalString(receipt, receiptContext, issues, "abodeName");
            ValidateNonNegativeIntegerField(receipt, receiptContext, issues, "rosterCount", "AfterlifeResidents");
            ValidateNonNegativeIntegerField(receipt, receiptContext, issues, "resolvedAtTurn", "AfterlifeResidents");
            ValidateRequiredIsoTimestampField(
                receipt,
                receiptContext,
                issues,
                "resolvedAtUtc",
                "AfterlifeResidents",
                "guardian_abode_resident_roster_receipt_missing_resolved_at_utc",
                "guardian_abode_resident_roster_receipt_invalid_resolved_at_utc",
                "Resident roster receipt должен содержать resolvedAtUtc в ISO 8601 формате.");
        }
    }

    private void ValidateGuardianAbodeResidentHistoryLog(JsonElement historyLog, string contextPrefix, List<ValidationIssue> issues)
    {
        var historyIndex = 0;
        foreach (var historyEntry in historyLog.EnumerateArray())
        {
            var historyContext = $"{contextPrefix}[{historyIndex++}]";
            if (!RequireObject(historyEntry, historyContext, issues))
                continue;

            RequireString(historyEntry, historyContext, issues, "entryId");
            RequireString(historyEntry, historyContext, issues, "residentId");
            RequireString(historyEntry, historyContext, issues, "title");
            RequireString(historyEntry, historyContext, issues, "summary");
            if (historyEntry.TryGetProperty("tags", out var tags))
                RequireArrayOfStrings(tags, $"{historyContext}.tags", issues);
            ValidateNonNegativeIntegerField(historyEntry, historyContext, issues, "revealedAtTurn", "AfterlifeResidents");
            ValidateRequiredIsoTimestampField(
                historyEntry,
                historyContext,
                issues,
                "revealedAtUtc",
                "AfterlifeResidents",
                "guardian_abode_resident_history_missing_revealed_at_utc",
                "guardian_abode_resident_history_invalid_revealed_at_utc",
                "Resident historyLog entry должен содержать revealedAtUtc в ISO 8601 формате.");
        }
    }
}
