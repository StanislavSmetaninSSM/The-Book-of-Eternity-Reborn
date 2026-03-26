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
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnNarrativePayloadInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/narrative_response.json",
            "Narrative",
            "accepted_turn_stale_narrative_response",
            "output/narrative_response.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/narrative_response.json. Перезапиши narrative_response.json свежим ответом именно для текущего accepted turn.",
            issues);
        var json = await _fs.ReadFileAsync("output/narrative_response.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
                "Accepted GM turn должен содержать свежий output/narrative_response.json с непустым response",
                code: "accepted_turn_missing_narrative_response",
                section: "Narrative",
                expected: "output/narrative_response.json with non-empty response",
                actual: "missing or empty",
                repairHint: "Запиши output/narrative_response.json с полем response для текущего accepted turn."));
            return issues;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "output/narrative_response.json",
                    IssueSeverity.Error,
                    "output/narrative_response.json должен быть JSON object",
                    code: "accepted_turn_invalid_narrative_json_root",
                    section: "Narrative",
                    expected: "JSON object with response and timestamp",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: "Оставь output/narrative_response.json валидным JSON-объектом с полями response и timestamp."));
                return issues;
            }

            string responseText = string.Empty;
            if (!doc.RootElement.TryGetProperty("response", out var responseProp) ||
                responseProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(responseProp.GetString()))
            {
                issues.Add(new ValidationIssue(
                    "output/narrative_response.json.response",
                    IssueSeverity.Error,
                    "Accepted GM turn должен содержать непустой narrative response",
                    code: "accepted_turn_empty_narrative_response",
                    section: "Narrative",
                    expected: "non-empty response string",
                    actual: "missing or empty",
                    repairHint: "Поле response в output/narrative_response.json должно содержать свежий текст нарратива для текущего хода."));
            }
            else
            {
                responseText = responseProp.GetString() ?? string.Empty;
            }

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/narrative_response.json",
                issues,
                "timestamp",
                "Narrative",
                "narrative_response_missing_timestamp",
                "narrative_response_invalid_timestamp",
                "Добавь в output/narrative_response.json поле timestamp в ISO 8601 формате вместе со свежим response для текущего accepted turn.");

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(prop.Name, "response", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
                    continue;

                issues.Add(new ValidationIssue(
                    $"output/narrative_response.json.{prop.Name}",
                    IssueSeverity.Error,
                    "output/narrative_response.json содержит неподдерживаемое top-level поле",
                    code: "narrative_response_unknown_field",
                    section: "Narrative",
                    expected: "response | timestamp",
                    actual: prop.Name,
                    repairHint: "Пиши в output/narrative_response.json только response и timestamp для текущего хода."));
            }

            if (!string.IsNullOrWhiteSpace(responseText))
                await ValidateAchievementUnlockNarrativeMarkersAsync(responseText, issues);
        }
        catch (JsonException)
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
                "output/narrative_response.json должен быть валидным JSON",
                code: "accepted_turn_invalid_narrative_json",
                section: "Narrative",
                expected: "valid JSON object with response and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/narrative_response.json и оставь в нём валидный JSON-объект с полями response и timestamp."));
        }

        return issues;
    }

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnInterfacePayloadInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        var interfacePath = "output/interface_updates.json";
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            interfacePath,
            "InterfaceUpdates",
            "accepted_turn_stale_interface_updates",
            "output/interface_updates.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/interface_updates.json. Перезапиши interface_updates.json заново для текущего accepted turn, даже если в нём только dialogueOptions или image_prompt.",
            issues);

        if (!_fs.FileExists(interfacePath))
            return issues;

        var json = await _fs.ReadFileAsync(interfacePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                interfacePath,
                IssueSeverity.Error,
                "Если output/interface_updates.json записан, он должен быть непустым JSON object с dialogueOptions и/или image_prompt",
                code: "accepted_turn_empty_interface_updates",
                section: "InterfaceUpdates",
                expected: "non-empty JSON object with dialogueOptions and/or image_prompt",
                actual: "empty file",
                repairHint: "Либо удали output/interface_updates.json, если интерфейсных обновлений в этом ходу нет, либо запиши в него dialogueOptions и/или image_prompt текущего accepted turn."));
            return issues;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "output/interface_updates.json",
                    IssueSeverity.Error,
                    "output/interface_updates.json должен быть JSON object",
                    code: "accepted_turn_invalid_interface_updates_root",
                    section: "InterfaceUpdates",
                    expected: "JSON object with dialogueOptions/image_prompt and timestamp",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: "Оставь output/interface_updates.json валидным JSON-объектом. Используй только поля dialogueOptions, image_prompt и timestamp."));
                return issues;
            }

            ValidateDialogueOptionsData(doc.RootElement, interfacePath, issues);
            ValidateOptionalNullableStringField(doc.RootElement, interfacePath, issues, "image_prompt");

            var hasDialogueOptions = doc.RootElement.TryGetProperty("dialogueOptions", out _);
            var hasImagePrompt = doc.RootElement.TryGetProperty("image_prompt", out _);
            if (!hasDialogueOptions && !hasImagePrompt)
            {
                issues.Add(new ValidationIssue(
                    interfacePath,
                    IssueSeverity.Error,
                    "output/interface_updates.json не должен быть timestamp-only stub без интерфейсного payload",
                    code: "interface_updates_missing_payload",
                    section: "InterfaceUpdates",
                    expected: "dialogueOptions and/or image_prompt, or the file should be absent when unused",
                    actual: "timestamp-only or unrelated object",
                    repairHint: "Если интерфейсных обновлений нет, не записывай output/interface_updates.json. Если файл нужен, добавь dialogueOptions и/или image_prompt для текущего хода."));
            }

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                interfacePath,
                issues,
                "timestamp",
                "InterfaceUpdates",
                "interface_updates_missing_timestamp",
                "interface_updates_invalid_timestamp",
                "Добавь в output/interface_updates.json поле timestamp в ISO 8601 формате вместе с dialogueOptions и/или image_prompt текущего accepted turn.");

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(prop.Name, "dialogueOptions", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "image_prompt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{interfacePath}.{prop.Name}",
                    IssueSeverity.Error,
                    "output/interface_updates.json содержит неподдерживаемое top-level поле",
                    code: "interface_updates_unknown_field",
                    section: "InterfaceUpdates",
                    expected: "dialogueOptions | image_prompt | timestamp",
                    actual: prop.Name,
                    repairHint: "Пиши в output/interface_updates.json только dialogueOptions, image_prompt и timestamp для текущего хода."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                interfacePath,
                IssueSeverity.Error,
                $"output/interface_updates.json должен быть валидным JSON: {ex.Message}",
                code: "accepted_turn_invalid_interface_updates_json",
                section: "InterfaceUpdates",
                expected: "valid JSON object with dialogueOptions/image_prompt and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/interface_updates.json и оставь в нём валидный JSON-объект с dialogueOptions и/или image_prompt вместе с timestamp."));
        }

        return issues;
    }

    private InkFeatherActionContext? ParseInkFeatherActionContext(JsonElement requestRoot, string playerAction)
    {
        var match = InkFeatherActionTagRegex.Match(playerAction);
        if (!match.Success)
            return null;

        int? parsedCost = null;
        var costMatch = InkFeatherCostRegex.Match(playerAction);
        if (costMatch.Success && int.TryParse(costMatch.Groups[1].Value, out var cost))
            parsedCost = cost;

        var sessionId = requestRoot.TryGetProperty("sessionId", out var sessionEl) && sessionEl.ValueKind == JsonValueKind.String
            ? sessionEl.GetString() ?? string.Empty
            : string.Empty;
        var requestId = requestRoot.TryGetProperty("requestId", out var requestEl) && requestEl.ValueKind == JsonValueKind.String
            ? requestEl.GetString() ?? string.Empty
            : string.Empty;
        var turnNumber = requestRoot.TryGetProperty("turnNumber", out var turnEl) && turnEl.ValueKind == JsonValueKind.Number && turnEl.TryGetInt32(out var parsedTurn)
            ? parsedTurn
            : 0;

        return new InkFeatherActionContext
        {
            ActionTag = match.Groups[1].Value,
            ParsedCostInFeathers = parsedCost,
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber
        };
    }

    private async Task<(List<ValidationIssue> Issues, JsonDocument? ReceiptDoc)> ValidateInkFeatherActionReceiptAsync(InkFeatherActionContext context)
    {
        var issues = new List<ValidationIssue>();
        var receiptJson = await _fs.ReadFileAsync(InkFeatherActionResultPath);
        if (string.IsNullOrWhiteSpace(receiptJson))
        {
            issues.Add(new ValidationIssue(
                InkFeatherActionResultPath,
                IssueSeverity.Error,
                $"После {context.ActionTag} отсутствует обязательный structured receipt output/ink_feather_action_result.json",
                code: "ink_feather_result_missing",
                section: context.ActionTag,
                expected: "output/ink_feather_action_result.json",
                actual: "missing file",
                repairHint: "Для каждого GM-side INK_FEATHER_ACTION обязательно запиши output/ink_feather_action_result.json с actionTag, metadata, resolutionType, summary и stateEvidence."));
            return (issues, null);
        }

        try
        {
            var doc = JsonDocument.Parse(receiptJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    InkFeatherActionResultPath,
                    IssueSeverity.Error,
                    "ink_feather_action_result.json должен быть JSON object",
                    code: "ink_feather_result_not_object",
                    section: context.ActionTag));
                doc.Dispose();
                return (issues, null);
            }

            var sessionId = RequireString(root, InkFeatherActionResultPath, issues, "sessionId");
            var requestId = RequireString(root, InkFeatherActionResultPath, issues, "requestId");
            var actionTag = RequireString(root, InkFeatherActionResultPath, issues, "actionTag");
            RequireString(root, InkFeatherActionResultPath, issues, "resolutionType");
            RequireString(root, InkFeatherActionResultPath, issues, "summary");

            var resolved = root.TryGetProperty("resolved", out var resolvedEl) &&
                           (resolvedEl.ValueKind == JsonValueKind.True || resolvedEl.ValueKind == JsonValueKind.False)
                ? resolvedEl.GetBoolean()
                : (bool?)null;
            if (resolved != true)
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.resolved",
                    IssueSeverity.Error,
                    "ink_feather_action_result.json должен явно подтверждать resolved = true",
                    code: "ink_feather_result_not_resolved",
                    section: context.ActionTag,
                    expected: "true",
                    actual: resolved?.ToString() ?? "missing"));
            }

            if (!root.TryGetProperty("turnNumber", out var turnEl) || turnEl.ValueKind != JsonValueKind.Number || !turnEl.TryGetInt32(out var turnNumber))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.turnNumber",
                    IssueSeverity.Error,
                    "ink_feather_action_result.json должен содержать корректный turnNumber",
                    code: "ink_feather_result_missing_turn",
                    section: context.ActionTag));
            }
            else if (turnNumber != context.TurnNumber)
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.turnNumber",
                    IssueSeverity.Error,
                    "turnNumber в ink_feather_action_result.json не совпадает с текущим turn_request",
                    code: "ink_feather_result_turn_mismatch",
                    section: context.ActionTag,
                    expected: context.TurnNumber.ToString(),
                    actual: turnNumber.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(context.SessionId) &&
                !string.IsNullOrWhiteSpace(sessionId) &&
                !string.Equals(sessionId, context.SessionId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.sessionId",
                    IssueSeverity.Error,
                    "sessionId в ink_feather_action_result.json не совпадает с текущим turn_request",
                    code: "ink_feather_result_session_mismatch",
                    section: context.ActionTag,
                    expected: context.SessionId,
                    actual: sessionId));
            }

            if (!string.IsNullOrWhiteSpace(context.RequestId) &&
                !string.IsNullOrWhiteSpace(requestId) &&
                !string.Equals(requestId, context.RequestId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.requestId",
                    IssueSeverity.Error,
                    "requestId в ink_feather_action_result.json не совпадает с текущим turn_request",
                    code: "ink_feather_result_request_mismatch",
                    section: context.ActionTag,
                    expected: context.RequestId,
                    actual: requestId));
            }

            if (!string.IsNullOrWhiteSpace(actionTag) &&
                !string.Equals(actionTag, context.ActionTag, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.actionTag",
                    IssueSeverity.Error,
                    "actionTag в ink_feather_action_result.json не совпадает с INK_FEATHER_ACTION текущего хода",
                    code: "ink_feather_result_action_mismatch",
                    section: context.ActionTag,
                    expected: context.ActionTag,
                    actual: actionTag));
            }

            if (root.TryGetProperty("costInFeathers", out var costEl) &&
                costEl.ValueKind == JsonValueKind.Number &&
                costEl.TryGetInt32(out var actualCost))
            {
                if (actualCost <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{InkFeatherActionResultPath}.costInFeathers",
                        IssueSeverity.Error,
                        "costInFeathers в ink_feather_action_result.json должен быть положительным числом",
                        code: "ink_feather_result_invalid_cost",
                        section: context.ActionTag));
                }
                else if (context.ParsedCostInFeathers.HasValue && context.ParsedCostInFeathers.Value != actualCost)
                {
                    issues.Add(new ValidationIssue(
                        $"{InkFeatherActionResultPath}.costInFeathers",
                        IssueSeverity.Error,
                        "costInFeathers в ink_feather_action_result.json не совпадает с суммой из playerAction",
                        code: "ink_feather_result_cost_mismatch",
                        section: context.ActionTag,
                        expected: context.ParsedCostInFeathers.Value.ToString(),
                        actual: actualCost.ToString()));
                }
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.costInFeathers",
                    IssueSeverity.Error,
                    "ink_feather_action_result.json должен содержать costInFeathers",
                    code: "ink_feather_result_missing_cost",
                    section: context.ActionTag));
            }

            if (!TryGetStateEvidence(root, context.ActionTag, issues, out var stateEvidence))
            {
                doc.Dispose();
                return (issues, null);
            }

            return (issues, doc);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                InkFeatherActionResultPath,
                IssueSeverity.Error,
                $"Не удалось разобрать ink_feather_action_result.json: {ex.Message}",
                code: "ink_feather_result_parse_failed",
                section: context.ActionTag,
                repairHint: "Исправь output/ink_feather_action_result.json; файл должен быть валидным JSON object с metadata, actionTag, resolutionType, summary и stateEvidence."));
            return (issues, null);
        }
    }

    private async Task ValidateSacrificeToChaosOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "worldEvent", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "eventSummary");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[]
            {
                "game_state/world/current_location.json",
                "game_state/world/world_events.json",
                "game_state/world/world_flags.json",
                "game_state/world/world_map.json",
                "game_state/world/weather.json"
            });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "world event", "Одно из world-state файлов должно реально измениться после SACRIFICE_TO_CHAOS.");
    }

    private async Task ValidateAbsorbFeathersOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "experience", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var experienceGain = RequirePositiveEvidenceNumber(stateEvidence, context.ActionTag, "experienceGained", issues);
        if (!experienceGain.HasValue)
            return;

        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/player/experience.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "experience", "game_state/player/experience.json должен реально измениться после ABSORB_FEATHERS.");

        var previousExperience = ReadPrimaryExperienceCounter(await ReadPreTurnTrackedFileAsync("game_state/player/experience.json"));
        var currentExperience = ReadPrimaryExperienceCounter(await _fs.ReadFileAsync("game_state/player/experience.json"));
        if (!currentExperience.HasValue)
        {
            issues.Add(new ValidationIssue(
                "game_state/player/experience.json",
                IssueSeverity.Error,
                "После ABSORB_FEATHERS не найден authoritative XP counter для подтверждения прироста опыта",
                code: "ink_feather_experience_counter_missing",
                section: context.ActionTag,
                expected: "totalExperience or currentExperience or experience",
                actual: "missing recognized XP counter",
                repairHint: "После ABSORB_FEATHERS experience.json должен содержать и реально увеличить authoritative XP counter."));
        }
        else if (currentExperience.Value <= (previousExperience ?? 0))
        {
            issues.Add(new ValidationIssue(
                "game_state/player/experience.json",
                IssueSeverity.Error,
                "После ABSORB_FEATHERS опыт игрока не вырос",
                code: "ink_feather_experience_not_increased",
                section: context.ActionTag,
                expected: $">{(previousExperience ?? 0)}",
                actual: currentExperience.Value.ToString(),
                repairHint: "После ABSORB_FEATHERS experience.json должен показывать реальный прирост опыта, а не только формальный receipt."));
        }
    }

    private async Task ValidateLearnSkillOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "skillGrant", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var skillName = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "skillName");
        var skillKind = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "skillKind");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[]
            {
                "game_state/player/skills_active.json",
                "game_state/player/skills_passive.json"
            });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "skill grant", "Один из player skill files должен реально измениться после LEARN_SKILL.");

        if (string.IsNullOrWhiteSpace(skillName) || string.IsNullOrWhiteSpace(skillKind))
            return;

        string skillFile;
        string skillArray;
        if (string.Equals(skillKind, "active", StringComparison.OrdinalIgnoreCase))
        {
            skillFile = "game_state/player/skills_active.json";
            skillArray = "activeSkillChanges";
        }
        else if (string.Equals(skillKind, "passive", StringComparison.OrdinalIgnoreCase))
        {
            skillFile = "game_state/player/skills_passive.json";
            skillArray = "passiveSkillChanges";
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.skillKind",
                IssueSeverity.Error,
                "Для LEARN_SKILL skillKind должен быть 'active' или 'passive'",
                code: "ink_feather_skill_invalid_kind",
                section: context.ActionTag,
                expected: "active or passive",
                actual: skillKind));
            return;
        }

        var postSkillJson = await _fs.ReadFileAsync(skillFile);
        var preSkillJson = await ReadPreTurnTrackedFileAsync(skillFile);
        var skillExists = SkillExistsInJson(postSkillJson, skillArray, skillName);
        var skillExistedBefore = SkillExistsInJson(preSkillJson, skillArray, skillName);

        if (!skillExists)
        {
            issues.Add(new ValidationIssue(
                "game_state/player/skills_*.json",
                IssueSeverity.Error,
                "После LEARN_SKILL новый навык не найден в состоянии игрока",
                code: "ink_feather_skill_missing",
                section: context.ActionTag,
                expected: skillName,
                actual: "missing skill object",
                repairHint: "Выданный через LEARN_SKILL навык должен реально появиться в skills_active.json или skills_passive.json."));
        }
        else if (skillExistedBefore)
        {
            issues.Add(new ValidationIssue(
                skillFile,
                IssueSeverity.Error,
                "После LEARN_SKILL найден только уже существовавший до хода навык",
                code: "ink_feather_skill_not_new",
                section: context.ActionTag,
                expected: $"new skill {skillName}",
                actual: "skill already existed before the turn",
                repairHint: "LEARN_SKILL должен создавать новый навык этого хода, а не ссылаться на уже существующий skill object."));
        }
    }

    private async Task ValidateFateShieldOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "buffGrant", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var effectName = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "effectName");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/player/effects.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "buff grant", "game_state/player/effects.json должен реально измениться после FATE_SHIELD.");

        var currentEffectsJson = await _fs.ReadFileAsync("game_state/player/effects.json");
        var previousEffectsJson = await ReadPreTurnTrackedFileAsync("game_state/player/effects.json");
        var effectExistsNow = !string.IsNullOrWhiteSpace(effectName) &&
                              JsonContainsNamedObject(currentEffectsJson, effectName, "effectName", "name");
        var effectExistedBefore = !string.IsNullOrWhiteSpace(effectName) &&
                                  JsonContainsNamedObject(previousEffectsJson, effectName, "effectName", "name");

        if (!effectExistsNow)
        {
            issues.Add(new ValidationIssue(
                "game_state/player/effects.json",
                IssueSeverity.Error,
                "После FATE_SHIELD эффект не найден в player effects",
                code: "ink_feather_fate_shield_missing_effect",
                section: context.ActionTag,
                expected: effectName,
                actual: "missing effect",
                repairHint: "После FATE_SHIELD добавь эффект 'Щит Судьбы' в game_state/player/effects.json."));
        }
        else if (effectExistedBefore)
        {
            issues.Add(new ValidationIssue(
                "game_state/player/effects.json",
                IssueSeverity.Error,
                "После FATE_SHIELD найден только уже существовавший до хода Щит Судьбы",
                code: "ink_feather_fate_shield_not_new",
                section: context.ActionTag,
                expected: "newly added Fate Shield effect",
                actual: "effect already existed before the turn",
                repairHint: "Новая трата на FATE_SHIELD должна создавать новый effect instance, а не ссылаться на уже существующий щит."));
        }
    }

    private async Task ValidateSealInInkOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "pendingUpgrade", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var pendingActionId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "pendingActionId");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { PendingInkActionsPath });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "pending upgrade request", $"{PendingInkActionsPath} должен реально измениться после SEAL_IN_INK.");

        if (!string.IsNullOrWhiteSpace(pendingActionId) &&
            !await PendingInkActionExistsAsync(
                pendingActionId,
                "SEAL_IN_INK",
                "awaiting-item-choice",
                context.ParsedCostInFeathers ?? 0,
                1))
        {
            issues.Add(new ValidationIssue(
                PendingInkActionsPath,
                IssueSeverity.Error,
                "После SEAL_IN_INK не найден ожидаемый persisted pending ink action",
                code: "ink_feather_seal_missing_pending_action",
                section: context.ActionTag,
                expected: pendingActionId,
                actual: "missing pending action",
                repairHint: "SEAL_IN_INK должен создать pending_ink_actions.json с actionId, actionTag=SEAL_IN_INK, status=awaiting-item-choice, правильным costInFeathers и upgradeTierDelta = 1."));
        }
    }

    private async Task ValidateDonateToGuardianOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "guardianReputation", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var guardianId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "guardianId");
        var reputationChange = RequirePositiveEvidenceNumber(stateEvidence, context.ActionTag, "reputationChange", issues);
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/guardians.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "guardian reputation change", "game_state/meta/guardians.json должен реально измениться после DONATE_TO_GUARDIAN.");

        if (string.IsNullOrWhiteSpace(guardianId) || !reputationChange.HasValue)
            return;

        var expectedReputationChange = context.ParsedCostInFeathers.HasValue
            ? Math.Min(25, Math.Max(15, context.ParsedCostInFeathers.Value / 3))
            : (int?)null;

        if (!expectedReputationChange.HasValue)
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.reputationChange",
                IssueSeverity.Error,
                "После DONATE_TO_GUARDIAN не удалось вычислить expected reputationChange из playerAction cost",
                code: "ink_feather_guardian_reputation_expected_missing",
                section: context.ActionTag,
                expected: "exact formula result from costInFeathers",
                actual: reputationChange.Value.ToString(),
                repairHint: "Validation DONATE_TO_GUARDIAN должна видеть читаемый costInFeathers в playerAction."));
        }
        else if (reputationChange.Value != expectedReputationChange.Value)
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.reputationChange",
                IssueSeverity.Error,
                "После DONATE_TO_GUARDIAN reputationChange должен точно совпадать с формулой из правил",
                code: "ink_feather_guardian_reputation_formula_mismatch",
                section: context.ActionTag,
                expected: expectedReputationChange.Value.ToString(),
                actual: reputationChange.Value.ToString(),
                repairHint: "Следуй формуле из правил: reputation_change = min(25, max(15, cost / 3))."));
        }

        var previousReputation = await ReadGuardianReputationAsync(await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json"), guardianId);
        var currentReputation = await ReadGuardianReputationAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json"), guardianId);
        if (!currentReputation.HasValue ||
            !previousReputation.HasValue ||
            currentReputation.Value <= previousReputation.Value)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "После DONATE_TO_GUARDIAN репутация Хранителя не выросла",
                code: "ink_feather_guardian_reputation_missing",
                section: context.ActionTag,
                expected: $">{previousReputation?.ToString() ?? "pre-turn reputation"}",
                actual: currentReputation?.ToString() ?? "missing guardian",
                repairHint: "После DONATE_TO_GUARDIAN увеличь репутацию текущего Хранителя и зафиксируй это в guardians.json."));
        }
    }

    private async Task ValidateCultivateEnlightenmentOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "enlightenmentProgress", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var experienceGain = RequirePositiveEvidenceNumber(stateEvidence, context.ActionTag, "experienceGain", issues);
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/soul_state.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "enlightenment progress", "game_state/meta/soul_state.json должен реально измениться после CULTIVATE_ENLIGHTENMENT.");

        if (!experienceGain.HasValue)
            return;

        if (context.ParsedCostInFeathers.HasValue && experienceGain.Value != context.ParsedCostInFeathers.Value * 2)
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.experienceGain",
                IssueSeverity.Error,
                "После CULTIVATE_ENLIGHTENMENT experienceGain должен быть равен costInFeathers * 2",
                code: "ink_feather_enlightenment_gain_mismatch",
                section: context.ActionTag,
                expected: (context.ParsedCostInFeathers.Value * 2).ToString(),
                actual: experienceGain.Value.ToString(),
                repairHint: "Следуй формуле из правил: enlightenment_xp_gain = cost * 2."));
        }

        var previousExperience = await ReadEnlightenmentExperienceAsync(await ReadPreTurnTrackedFileAsync("game_state/meta/soul_state.json"));
        var currentExperience = await ReadEnlightenmentExperienceAsync(await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        if (!currentExperience.HasValue ||
            !previousExperience.HasValue ||
            currentExperience.Value <= previousExperience.Value)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "После CULTIVATE_ENLIGHTENMENT просветление не получило прогресса",
                code: "ink_feather_enlightenment_missing",
                section: context.ActionTag,
                expected: $">{previousExperience?.ToString() ?? "pre-turn enlightenment"}",
                actual: currentExperience?.ToString() ?? "missing enlightenment",
                repairHint: "После CULTIVATE_ENLIGHTENMENT увеличь enlightenment.experience в soul_state.json."));
        }
        else if (currentExperience.Value - previousExperience.Value < experienceGain.Value)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "После CULTIVATE_ENLIGHTENMENT реальный рост enlightenment меньше, чем обещанный experienceGain",
                code: "ink_feather_enlightenment_growth_too_small",
                section: context.ActionTag,
                expected: $">= {experienceGain.Value}",
                actual: (currentExperience.Value - previousExperience.Value).ToString(),
                repairHint: "Убедись, что enlightenment.experience реально увеличился минимум на amount из stateEvidence.experienceGain."));
        }
    }

    private async Task ValidateGuardianFavorOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "guardianReputation", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var guardianId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "guardianId");
        var reputationChange = RequirePositiveEvidenceNumber(stateEvidence, context.ActionTag, "reputationChange", issues);
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/guardians.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "guardian reputation change", "game_state/meta/guardians.json должен реально измениться после GUARDIAN_FAVOR.");

        if (string.IsNullOrWhiteSpace(guardianId) || !reputationChange.HasValue)
            return;

        var previousReputation = await ReadGuardianReputationAsync(await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json"), guardianId);
        var currentReputation = await ReadGuardianReputationAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json"), guardianId);
        if (!currentReputation.HasValue ||
            !previousReputation.HasValue ||
            currentReputation.Value <= previousReputation.Value)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "После GUARDIAN_FAVOR репутация Хранителя не выросла",
                code: "ink_feather_guardian_favor_reputation_missing",
                section: context.ActionTag,
                expected: $">{previousReputation?.ToString() ?? "pre-turn reputation"}",
                actual: currentReputation?.ToString() ?? "missing guardian",
                repairHint: "GUARDIAN_FAVOR обязан как минимум повысить репутацию текущего Хранителя в guardians.json."));
        }
    }

    private async Task ValidateAbodeOfferingOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "abodeOffering", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var guardianId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "guardianId");
        var powerGain = RequirePositiveEvidenceNumber(stateEvidence, context.ActionTag, "powerGain", issues);
        var returnCycleId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "returnCycleId");
        var powerEventId = RequireString(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "powerEventId");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/guardians.json", GuardianPowerEventState.JournalPath });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "abode power change", $"game_state/meta/guardians.json и {GuardianPowerEventState.JournalPath} должны реально измениться после ABODE_OFFERING.");

        if (string.IsNullOrWhiteSpace(guardianId) || !powerGain.HasValue || string.IsNullOrWhiteSpace(returnCycleId))
            return;

        var request = await GuardianAbodeOfferingState.ReadAsync(_fs);
        if (request == null)
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeOfferingState.PendingRequestPath,
                IssueSeverity.Error,
                "После ABODE_OFFERING отсутствует client-authored pending_abode_offering.json для сверки результата",
                code: "abode_offering_missing_pending_request",
                section: context.ActionTag,
                expected: GuardianAbodeOfferingState.PendingRequestPath,
                actual: "missing or unreadable",
                repairHint: "Клиент должен сохранить pending_abode_offering.json перед ходом; GM не должен удалять или перезаписывать этот файл."));
            return;
        }

        if (!string.Equals(request.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.guardianId",
                IssueSeverity.Error,
                "ABODE_OFFERING применён не к тому Хранителю, который указан в pending_abode_offering.json",
                code: "abode_offering_guardian_mismatch",
                section: context.ActionTag,
                expected: request.GuardianId,
                actual: guardianId,
                repairHint: "Применяй offering только к guardianId из pending_abode_offering.json."));
        }

        if (!string.Equals(request.ReturnCycleId, returnCycleId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.returnCycleId",
                IssueSeverity.Error,
                "ABODE_OFFERING использует другой returnCycleId, чем client-authored pending request",
                code: "abode_offering_return_cycle_mismatch",
                section: context.ActionTag,
                expected: request.ReturnCycleId,
                actual: returnCycleId,
                repairHint: "Не меняй returnCycleId offering-а относительно pending_abode_offering.json."));
        }

        if (context.ParsedCostInFeathers.HasValue && context.ParsedCostInFeathers.Value != request.InkFeathersOffered)
        {
            issues.Add(new ValidationIssue(
                InkFeatherActionResultPath,
                IssueSeverity.Error,
                "ABODE_OFFERING использует сумму из playerAction, которая не совпадает с pending_abode_offering.json",
                code: "abode_offering_cost_mismatch",
                section: context.ActionTag,
                expected: request.InkFeathersOffered.ToString(),
                actual: context.ParsedCostInFeathers.Value.ToString(),
                repairHint: "Сохраняй одну и ту же сумму offering-а в playerAction и pending_abode_offering.json."));
        }

        var expectedGain = GuardianAbodeOfferingState.ResolvePowerGainForInkFeatherOffering(request.InkFeathersOffered);
        if (powerGain.Value != expectedGain)
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.powerGain",
                IssueSeverity.Error,
                "ABODE_OFFERING powerGain не совпадает с формулой Ink Feather offering",
                code: "abode_offering_power_gain_mismatch",
                section: context.ActionTag,
                expected: expectedGain.ToString(),
                actual: powerGain.Value.ToString(),
                repairHint: "Следуй формуле offering-а: 50/100/150 перьев -> +1/+2/+3 силы Обители."));
        }

        var preJournalJson = await ReadPreTurnTrackedFileAsync(GuardianPowerEventState.JournalPath);
        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var preCycleFeathers = CountOfferingFeathersFromJournal(preJournalJson, guardianId, returnCycleId);
        var postCycleFeathers = CountOfferingFeathersFromJournal(postJournalJson, guardianId, returnCycleId);
        if (preCycleFeathers + request.InkFeathersOffered > 150)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING превысил лимит 150 перьев на Хранителя в одном return cycle",
                code: "abode_offering_cycle_cap_exceeded",
                section: context.ActionTag,
                expected: "<= 150 total feathers per guardian per return cycle",
                actual: (preCycleFeathers + request.InkFeathersOffered).ToString(),
                repairHint: "Не превышай cap offering-а: максимум 150 Чернильных Перьев на одного Хранителя за одно возвращение."));
        }

        if (postCycleFeathers < preCycleFeathers + request.InkFeathersOffered)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не оставил полного offering audit trail в abode_power_journal.json",
                code: "abode_offering_journal_missing_cycle_amount",
                section: context.ActionTag,
                expected: (preCycleFeathers + request.InkFeathersOffered).ToString(),
                actual: postCycleFeathers.ToString(),
                repairHint: "Каждое offering power event должно сохранять inkFeathersOffered и returnCycleId в audit journal entry."));
        }

        if (!await GuardianPowerJournalContainsEventAsync(powerEventId, guardianId, "offering", powerGain.Value, returnCycleId))
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не оставил ожидаемый offering event в abode_power_journal.json",
                code: "abode_offering_missing_power_event_journal",
                section: context.ActionTag,
                expected: powerEventId,
                actual: "missing offering journal event",
                repairHint: "ABODE_OFFERING должен materialize guardianPowerEvents с reasonType=offering и journal entry с тем же eventId."));
        }

        var previousPower = await ReadGuardianAbodePowerAsync(await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json"), guardianId);
        var currentPower = await ReadGuardianAbodePowerAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json"), guardianId);
        if (!previousPower.HasValue || !currentPower.HasValue || currentPower.Value - previousPower.Value < powerGain.Value)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "После ABODE_OFFERING реальный рост силы Обители меньше, чем обещанный powerGain",
                code: "abode_offering_power_missing",
                section: context.ActionTag,
                expected: $">= +{powerGain.Value}",
                actual: previousPower.HasValue && currentPower.HasValue ? (currentPower.Value - previousPower.Value).ToString() : "missing guardian power",
                repairHint: "ABODE_OFFERING должен реально увеличить guardian.abodePower.currentPower и записать это через guardianPowerEvents."));
        }
    }

    private async Task ValidateSoulImprintOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "soulImprint", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var imprintId = GetStringValue(stateEvidence, "imprintId");
        var companionName = GetStringValue(stateEvidence, "companionName");
        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/soul_state.json" });

        if (changedFiles.Count == 0)
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "soul imprint", "game_state/meta/soul_state.json должен реально измениться после SOUL_IMPRINT.");

        if (!await SoulImprintExistsAsync(imprintId, companionName))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "После SOUL_IMPRINT не найден soulImprint в состоянии души",
                code: "ink_feather_soul_imprint_missing",
                section: context.ActionTag,
                expected: imprintId ?? companionName ?? "soulImprint entry",
                actual: "missing soulImprint",
                repairHint: "SOUL_IMPRINT должен создать soulImprint entry в soul_state.json, а не ограничиваться нарративом."));
        }
        else if (!await SoulImprintHasMinimalCoreTraitsAsync(imprintId, companionName))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "После SOUL_IMPRINT найден imprint, но в нём нет минимально проверяемых core traits / summary данных",
                code: "ink_feather_soul_imprint_missing_core_traits",
                section: context.ActionTag,
                expected: "name/id + description/summary + preserved core traits/personality markers",
                actual: "weak imprint object",
                repairHint: "SOUL_IMPRINT должен сохранять не только идентификатор, но и минимально осмысленные core traits или personality markers текущего компаньона."));
        }
    }

    private void ValidateResolutionType(JsonElement receiptRoot, string actionTag, string expectedResolutionType, List<ValidationIssue> issues)
    {
        var actual = GetStringValue(receiptRoot, "resolutionType");
        if (!string.Equals(actual, expectedResolutionType, StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.resolutionType",
                IssueSeverity.Error,
                $"resolutionType для {actionTag} должен быть {expectedResolutionType}",
                code: "ink_feather_result_resolution_type_mismatch",
                section: actionTag,
                expected: expectedResolutionType,
                actual: actual));
        }
    }

    private bool TryGetStateEvidence(JsonElement receiptRoot, string actionTag, List<ValidationIssue> issues, out JsonElement stateEvidence)
    {
        if (receiptRoot.TryGetProperty("stateEvidence", out stateEvidence) && stateEvidence.ValueKind == JsonValueKind.Object)
            return true;

        issues.Add(new ValidationIssue(
            $"{InkFeatherActionResultPath}.stateEvidence",
            IssueSeverity.Error,
            $"Для {actionTag} обязателен объект stateEvidence",
            code: "ink_feather_result_missing_state_evidence",
            section: actionTag,
            expected: "stateEvidence object",
            actual: stateEvidence.ValueKind.ToString()));
        return false;
    }

    private async Task<List<string>> ValidateAffectedFilesChangedAsync(JsonElement stateEvidence, string actionTag, List<ValidationIssue> issues, IEnumerable<string> allowedFiles)
    {
        var affectedFiles = ReadAffectedFiles(stateEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues);
        if (affectedFiles.Count == 0)
            return new List<string>();

        var allowedSet = new HashSet<string>(allowedFiles, StringComparer.OrdinalIgnoreCase);
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null)
        {
            issues.Add(new ValidationIssue(
                PendingTurnSnapshotManifestPath,
                IssueSeverity.Error,
                $"Для проверки {actionTag} отсутствует pending turn snapshot manifest",
                code: "ink_feather_missing_snapshot_manifest",
                section: actionTag,
                expected: PendingTurnSnapshotManifestPath,
                actual: "missing",
                repairHint: "Accepted-turn validation feather action требует pre-turn snapshot context."));
            return new List<string>();
        }

        var changedFiles = new List<string>();
        foreach (var file in affectedFiles)
        {
            if (!allowedSet.Contains(file))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                    IssueSeverity.Error,
                    $"Для {actionTag} указан недопустимый affected file: {file}",
                    code: "ink_feather_unexpected_affected_file",
                    section: actionTag,
                    expected: string.Join(", ", allowedSet.OrderBy(x => x)),
                    actual: file));
                continue;
            }

            if (await DidFileChangeAgainstManifestAsync(manifest, file))
            {
                changedFiles.Add(file);
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                    IssueSeverity.Error,
                    $"Для {actionTag} listed affected file не изменился реально: {file}",
                    code: "ink_feather_affected_file_unchanged",
                    section: actionTag,
                    expected: "changed file",
                    actual: file));
            }
        }

        return changedFiles;
    }

    private static List<string> ReadAffectedFiles(JsonElement stateEvidence, string context, List<ValidationIssue> issues)
    {
        if (!stateEvidence.TryGetProperty("affectedFiles", out var affectedFiles) || affectedFiles.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.affectedFiles",
                IssueSeverity.Error,
                "stateEvidence должен содержать affectedFiles как массив строк"));
            return new List<string>();
        }

        var result = new List<string>();
        var index = 0;
        foreach (var item in affectedFiles.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.affectedFiles[{index}]",
                    IssueSeverity.Error,
                    "affectedFiles должен содержать только непустые строки"));
            }
            else
            {
                result.Add(item.GetString()!);
            }
            index++;
        }

        return result;
    }

    private int? RequirePositiveEvidenceNumber(JsonElement stateEvidence, string actionTag, string propertyName, List<ValidationIssue> issues)
    {
        if (!stateEvidence.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.Number ||
            !prop.TryGetInt32(out var value) ||
            value <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.{propertyName}",
                IssueSeverity.Error,
                $"Для {actionTag} поле {propertyName} должно быть положительным числом",
                code: "ink_feather_result_invalid_evidence_number",
                section: actionTag,
                expected: "positive integer",
                actual: prop.ValueKind.ToString()));
            return null;
        }

        return value;
    }

    private void AddMissingStateEvidenceIssue(List<ValidationIssue> issues, string actionTag, string effectLabel, string repairHint)
    {
        issues.Add(new ValidationIssue(
            InkFeatherActionResultPath,
            IssueSeverity.Error,
            $"После {actionTag} не найден минимальный stateful результат ({effectLabel})",
            code: "ink_feather_missing_state_effect",
            section: actionTag,
            repairHint: repairHint));
    }

    private static string GetStringValue(JsonElement root, string propName)
    {
        return root.TryGetProperty(propName, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidationPendingTurnSnapshotManifestAsync()
    {
        var json = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationPendingTurnSnapshotManifest>(json, ManifestJsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeManifestPayloadHash(ValidationPendingTurnSnapshotManifest manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = string.Empty;
        var payload = JsonSerializer.Serialize(manifest, ManifestHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private async Task<string?> ReadPreTurnTrackedFileAsync(string relativePath)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null)
            return null;

        if (manifest.RollbackBackups.TryGetValue(relativePath, out var backupPath))
            return await _fs.ReadFileAsync(backupPath);

        return null;
    }

    private async Task ValidateAcceptedTurnTransientOutputFreshnessAsync(
        string relativePath,
        string section,
        string code,
        string expected,
        string repairHint,
        List<ValidationIssue> issues)
    {
        var preTurnContent = await ReadPreTurnTrackedFileAsync(relativePath);
        if (preTurnContent == null)
            return;

        var currentContent = await _fs.ReadFileAsync(relativePath) ?? string.Empty;
        if (!string.Equals(currentContent, preTurnContent, StringComparison.Ordinal))
            return;

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Warning,
            $"{Path.GetFileName(relativePath)} совпадает с pre-turn snapshot и выглядит как возможный stale reuse",
            code: code,
            section: section,
            expected: expected,
            actual: "content unchanged from pre-turn snapshot",
            repairHint: repairHint + " Если текст был сознательно сгенерирован заново и просто совпал байт-в-байт, проверь, что он действительно описывает текущий запрос, а не stale payload."));
    }

    private async Task ValidateSkillContractConsistencyAsync(List<ValidationIssue> issues)
    {
        await ValidatePlayerActiveSkillMasteryInitializationAsync(issues);
        await ValidateNpcSkillMasteryInitializationAsync(
            issues,
            skillChangesSection: "NPCActiveSkillChanges",
            masterySection: "NPCSkillMasteryChanges",
            section: "Skills.Active");
        await ValidateNpcSkillMasteryInitializationAsync(
            issues,
            skillChangesSection: "NPCPassiveSkillChanges",
            masterySection: "NPCPassiveSkillMasteryChanges",
            section: "Skills.Passive");
    }

    private async Task ValidatePlayerActiveSkillMasteryInitializationAsync(List<ValidationIssue> issues)
    {
        const string skillPath = "game_state/player/skills_active.json";
        const string masteryPath = "game_state/player/skill_mastery.json";

        var preSkillJson = await ReadPreTurnTrackedFileAsync(skillPath);
        var postSkillJson = await _fs.ReadFileAsync(skillPath);
        var masteryJson = await _fs.ReadFileAsync(masteryPath);

        var preSkills = ParsePlayerSkillNames(preSkillJson, "activeSkillChanges");
        var postSkills = ParsePlayerSkillNames(postSkillJson, "activeSkillChanges");
        if (postSkills.Count == 0)
            return;

        var masteryNames = ParsePlayerSkillMasteryNames(masteryJson);
        foreach (var newSkill in postSkills.Where(skill => !preSkills.Contains(skill)).OrderBy(skill => skill, StringComparer.OrdinalIgnoreCase))
        {
            if (masteryNames.Contains(newSkill))
                continue;

            issues.Add(new ValidationIssue(
                masteryPath,
                IssueSeverity.Error,
                $"Новый active skill '{newSkill}' должен иметь mastery initialization в skillMasteryChanges",
                code: "player_active_skill_missing_mastery_initialization",
                section: "Skills.Active",
                expected: $"skillMasteryChanges contains {newSkill}",
                actual: "matching mastery entry is missing",
                repairHint: "Если игрок впервые получает active skill, добавь отдельную mastery initialization запись в game_state/player/skill_mastery.json -> skillMasteryChanges."));
        }
    }

    private async Task ValidateNpcSkillMasteryInitializationAsync(
        List<ValidationIssue> issues,
        string skillChangesSection,
        string masterySection,
        string section)
    {
        const string npcSkillsPath = "game_state/npcs/npc_skills.json";

        var preJson = await ReadPreTurnTrackedFileAsync(npcSkillsPath);
        var postJson = await _fs.ReadFileAsync(npcSkillsPath);

        var preSkillsByNpc = ParseNpcSkillChangesByActor(preJson, skillChangesSection);
        var postSkillsByNpc = ParseNpcSkillChangesByActor(postJson, skillChangesSection);
        if (postSkillsByNpc.Count == 0)
            return;

        var masteryByNpc = ParseNpcSkillMasteryByActor(postJson, masterySection);
        foreach (var (npcKey, skillNames) in postSkillsByNpc)
        {
            preSkillsByNpc.TryGetValue(npcKey, out var preSkillNames);
            masteryByNpc.TryGetValue(npcKey, out var masterySkillNames);

            foreach (var skillName in skillNames.Where(skill => preSkillNames == null || !preSkillNames.Contains(skill)))
            {
                if (masterySkillNames != null && masterySkillNames.Contains(skillName))
                    continue;

                issues.Add(new ValidationIssue(
                    $"{npcSkillsPath}.{masterySection}",
                    IssueSeverity.Error,
                    $"Новый NPC skill '{skillName}' для {npcKey} должен иметь mastery initialization в {masterySection}",
                    code: "npc_skill_missing_mastery_initialization",
                    actor: npcKey,
                    section: section,
                    expected: $"{masterySection} contains {npcKey} + {skillName}",
                    actual: "matching mastery entry is missing",
                    repairHint: $"Если NPC впервые получает skill через {skillChangesSection}, добавь matching mastery initialization в {masterySection}."));
            }
        }
    }

    private static HashSet<string> ParsePlayerSkillNames(string? json, string arrayName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in arr.EnumerateArray())
            {
                var skillName = GetFirstNonEmptyString(item, "skillName");
                if (!string.IsNullOrWhiteSpace(skillName))
                    result.Add(skillName);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }

    private static HashSet<string> ParsePlayerSkillMasteryNames(string? json)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("skillMasteryChanges", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in arr.EnumerateArray())
            {
                var skillName = GetFirstNonEmptyString(item, "skillName");
                if (!string.IsNullOrWhiteSpace(skillName))
                    result.Add(skillName);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }

    private HashSet<string> ReadCurrentPlayerActiveSkillNamesSync()
    {
        try
        {
            var path = _fs.ResolvePath("game_state/player/skills_active.json");
            if (!File.Exists(path))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return ParsePlayerSkillNames(File.ReadAllText(path), "activeSkillChanges");
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, HashSet<string>> ParseNpcSkillChangesByActor(string? json, string sectionName)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var actorEntry in arr.EnumerateArray())
            {
                var actorKey = BuildNpcIdentityKey(actorEntry);
                if (string.IsNullOrWhiteSpace(actorKey) ||
                    !actorEntry.TryGetProperty("skillChanges", out var skillChanges) ||
                    skillChanges.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (!result.TryGetValue(actorKey, out var skillNames))
                {
                    skillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[actorKey] = skillNames;
                }

                foreach (var skill in skillChanges.EnumerateArray())
                {
                    var skillName = GetFirstNonEmptyString(skill, "skillName");
                    if (!string.IsNullOrWhiteSpace(skillName))
                        skillNames.Add(skillName);
                }
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> ParseNpcSkillMasteryByActor(string? json, string sectionName)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(sectionName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in arr.EnumerateArray())
            {
                var actorKey = BuildNpcIdentityKey(item);
                var skillName = GetFirstNonEmptyString(item, "skillName");
                if (string.IsNullOrWhiteSpace(actorKey) || string.IsNullOrWhiteSpace(skillName))
                    continue;

                if (!result.TryGetValue(actorKey, out var skillNames))
                {
                    skillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[actorKey] = skillNames;
                }

                skillNames.Add(skillName);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }

    private static string? BuildNpcIdentityKey(JsonElement item)
    {
        var npcId = GetFirstNonEmptyString(item, "NPCId", "npcId", "id");
        if (!string.IsNullOrWhiteSpace(npcId))
            return $"id:{npcId}";

        var npcName = GetFirstNonEmptyString(item, "NPCName", "npcName", "name");
        if (!string.IsNullOrWhiteSpace(npcName))
            return $"name:{npcName}";

        return null;
    }

    private ValidationPendingTurnSnapshotManifest? LoadValidationPendingTurnSnapshotManifestSync()
    {
        var manifestPath = _fs.ResolvePath("game_state/control/pending_turn_snapshot.json");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ValidationPendingTurnSnapshotManifest>(
                File.ReadAllText(manifestPath),
                ManifestJsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private string? ReadPreTurnTrackedFileSync(string relativePath)
    {
        var manifest = LoadValidationPendingTurnSnapshotManifestSync();
        if (manifest == null)
            return null;

        if (!manifest.RollbackBackups.TryGetValue(relativePath, out var backupPath))
            return null;

        var resolvedBackupPath = _fs.ResolvePath(backupPath);
        if (!File.Exists(resolvedBackupPath))
            return null;

        try
        {
            return File.ReadAllText(resolvedBackupPath);
        }
        catch
        {
            return null;
        }
    }

    private string? ReadCurrentTrackedFileSync(string relativePath)
    {
        var resolvedPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(resolvedPath))
            return null;

        try
        {
            return File.ReadAllText(resolvedPath);
        }
        catch
        {
            return null;
        }
    }

    private async Task ValidateJsonFileHasMeaningfulContentAsync(string relativePath, List<ValidationIssue> issues, string section)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!ElementHasMeaningfulContent(doc.RootElement))
            {
                issues.Add(new ValidationIssue(
                    relativePath,
                    IssueSeverity.Error,
                    $"Bootstrap-файл {relativePath} существует, но не содержит осмысленного содержимого.",
                    code: "bootstrap_file_empty_content",
                    section: section,
                    repairHint: "Заполни bootstrap-файл реальным lore/codex содержимым, а не пустой заглушкой."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                $"Не удалось проверить содержимое bootstrap-файла: {ex.Message}",
                code: "bootstrap_file_unreadable",
                section: section));
        }
    }

    private async Task ValidateCodexBootstrapEntriesAsync(List<ValidationIssue> issues, string section, string? requiredSourcePrefix = null)
    {
        var json = await _fs.ReadFileAsync("lore/codex_entries.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var bootstrapEntries = new List<JsonElement>();
            if (doc.RootElement.TryGetProperty("entries", out var entries) &&
                entries.ValueKind == JsonValueKind.Array)
            {
                bootstrapEntries.AddRange(entries.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object));
            }

            if (bootstrapEntries.Count == 0 &&
                doc.RootElement.TryGetProperty("loreCodexUpdates", out var updates) &&
                updates.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in updates.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var command = GetFirstNonEmptyString(item, "command");
                    if (!string.Equals(command, "add", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (item.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                        bootstrapEntries.Add(entry);
                }
            }

            if (bootstrapEntries.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    "lore/codex_entries.json",
                    IssueSeverity.Error,
                    "Bootstrap должен создавать хотя бы одну стартовую запись в codex_entries.json.",
                    code: "bootstrap_codex_entries_missing",
                    section: section,
                    repairHint: "Добавь стартовые loreCodexUpdates / entries для знаний, которые игрок уже должен знать на этом этапе."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(requiredSourcePrefix))
            {
                var hasMatchingSource = bootstrapEntries.Any(entry =>
                    entry.TryGetProperty("sourceFile", out var sourceFile) &&
                    sourceFile.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(sourceFile.GetString()) &&
                    sourceFile.GetString()!.StartsWith(requiredSourcePrefix, StringComparison.OrdinalIgnoreCase));

                if (!hasMatchingSource)
                {
                    issues.Add(new ValidationIssue(
                        "lore/codex_entries.json",
                        IssueSeverity.Error,
                        "Bootstrap codex не содержит ни одной записи, привязанной к текущему world-lore bootstrap",
                        code: "bootstrap_codex_missing_current_world_entries",
                        section: section,
                        expected: $"at least one codex entry with sourceFile starting with '{requiredSourcePrefix}'",
                        actual: "no matching sourceFile entries",
                        repairHint: "Добавь в lore/codex_entries.json как минимум одну запись о текущем мире с sourceFile из current_world/*, а не только старые chaos/currently unrelated entries."));
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "lore/codex_entries.json",
                IssueSeverity.Error,
                $"Не удалось проверить codex bootstrap: {ex.Message}",
                code: "bootstrap_codex_unreadable",
                section: section));
        }
    }

    private async Task<bool> DidFileChangeAgainstManifestAsync(ValidationPendingTurnSnapshotManifest manifest, string relativePath)
    {
        var current = await _fs.ReadFileAsync(relativePath);
        if (manifest.RollbackBackups.TryGetValue(relativePath, out var backupPath))
        {
            var previous = await _fs.ReadFileAsync(backupPath);
            return !string.Equals(current ?? string.Empty, previous ?? string.Empty, StringComparison.Ordinal);
        }

        return !string.IsNullOrWhiteSpace(current);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles()
    {
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            yield break;

        foreach (var file in Directory.EnumerateFiles(storiesRoot, "*.jsonl", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(_fs.ResolvePath(""), file).Replace('\\', '/');
    }

    private static bool IsClientOwnedHistoryValidationPath(string normalizedPath)
    {
        return normalizedPath.Equals("game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("stories/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ElementHasMeaningfulContent(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ElementHasMeaningfulContent(prop.Value))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ElementHasMeaningfulContent(item))
                        return true;
                }
                return false;
            case JsonValueKind.String:
                return !string.IsNullOrWhiteSpace(element.GetString());
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return false;
            default:
                return false;
        }
    }

    private static bool DidChronicleGainEntries(string? preChronicleJson, string? postChronicleJson)
    {
        return ReadChronicleEntriesCount(postChronicleJson) > ReadChronicleEntriesCount(preChronicleJson);
    }

    private static bool DidChronicleGainMeaningfulSummaryEntry(string? preChronicleJson, string? postChronicleJson)
    {
        var preEntries = ReadChronicleEntrySummaries(preChronicleJson);
        var postEntries = ReadChronicleEntrySummaries(postChronicleJson);
        if (postEntries.Count <= preEntries.Count)
            return false;

        if (!preEntries.SequenceEqual(postEntries.Take(preEntries.Count)))
            return false;

        return postEntries.Skip(preEntries.Count).Any(entry => !string.IsNullOrWhiteSpace(entry));
    }

    private static int ReadChronicleEntriesCount(string? chronicleJson)
    {
        if (string.IsNullOrWhiteSpace(chronicleJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(chronicleJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.GetArrayLength();

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("entries", out var entries) &&
                entries.ValueKind == JsonValueKind.Array)
            {
                return entries.GetArrayLength();
            }
        }
        catch
        {
            // ignored
        }

        return 0;
    }

    private static List<string> ReadChronicleEntrySummaries(string? chronicleJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(chronicleJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(chronicleJson);
            JsonElement entries = doc.RootElement;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("entries", out var entriesProp) &&
                entriesProp.ValueKind == JsonValueKind.Array)
            {
                entries = entriesProp;
            }

            if (entries.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    result.Add(entry.GetString() ?? string.Empty);
                    continue;
                }

                if (entry.ValueKind != JsonValueKind.Object)
                {
                    result.Add(string.Empty);
                    continue;
                }

                result.Add(
                    GetFirstNonEmptyString(entry, "summary", "description", "text", "title", "content", "entry")
                    ?? string.Empty);
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }

    private IEnumerable<string> EnumerateTrackedFilesForValidation(ValidationPendingTurnSnapshotManifest manifest)
    {
        var gameSessionRoot = _fs.ResolvePath("");
        var files = new HashSet<string>(
            (manifest.RollbackBaselineFiles ?? new List<string>())
            .Where(path => !IsClientOwnedSurfaceValidationPath(path.Replace('\\', '/'))),
            StringComparer.OrdinalIgnoreCase);

        foreach (var absoluteFile in _fs.GetAllGameStateFiles())
        {
            var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
            if (string.Equals(relative, PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, "game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
                IsClientOwnedSurfaceValidationPath(relative) ||
                relative.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files.Add(relative);
        }

        foreach (var relativeDir in new[] { "lore", "stories" })
        {
            var absoluteDir = _fs.ResolvePath(relativeDir);
            if (!Directory.Exists(absoluteDir))
                continue;

            foreach (var absoluteFile in Directory.GetFiles(absoluteDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
                if (relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase) ||
                    IsClientOwnedSurfaceValidationPath(relative))
                    continue;

                files.Add(relative);
            }
        }

        foreach (var outputFile in AllowedQteOfferOnlyOutputFiles.Concat(new[] { InkFeatherActionResultPath }))
        {
            if (_fs.FileExists(outputFile) || files.Contains(outputFile))
                files.Add(outputFile);
        }

        return files;
    }

    private async Task<List<string>> GetChangedTrackedFilesAgainstManifestAsync(ValidationPendingTurnSnapshotManifest manifest)
    {
        var changedFiles = new List<string>();
        foreach (var relativePath in EnumerateTrackedFilesForValidation(manifest))
        {
            if (await DidFileChangeAgainstManifestAsync(manifest, relativePath))
                changedFiles.Add(relativePath);
        }

        return changedFiles;
    }

    private async Task<bool> SkillExistsAsync(string filePath, string arrayName, string skillName)
    {
        var json = await _fs.ReadFileAsync(filePath);
        return SkillExistsInJson(json, arrayName, skillName);
    }

    private async Task<bool> JsonFileContainsNamedObjectAsync(string filePath, string expectedName, params string[] propertyNames)
    {
        var json = await _fs.ReadFileAsync(filePath);
        return JsonContainsNamedObject(json, expectedName, propertyNames);
    }

    private static bool SkillExistsInJson(string? json, string arrayName, string skillName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object &&
                    item.TryGetProperty("skillName", out var nameEl) &&
                    nameEl.ValueKind == JsonValueKind.String &&
                    string.Equals(nameEl.GetString(), skillName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool JsonContainsNamedObject(string? json, string expectedName, params string[] propertyNames)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ElementContainsNamedObject(doc.RootElement, expectedName, propertyNames);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ElementContainsNamedObject(JsonElement element, string expectedName, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out var valueEl) &&
                    valueEl.ValueKind == JsonValueKind.String &&
                    string.Equals(valueEl.GetString(), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                if (ElementContainsNamedObject(prop.Value, expectedName, propertyNames))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ElementContainsNamedObject(item, expectedName, propertyNames))
                    return true;
            }
        }

        return false;
    }

    private async Task<int?> ReadGuardianReputationAsync(string? guardiansJson, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (doc.RootElement.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                {
                    if (guardian.ValueKind != JsonValueKind.Object)
                        continue;
                    var currentGuardianId = GetStringValue(guardian, "guardianId");
                    if (!string.Equals(currentGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
                        relationshipData.ValueKind == JsonValueKind.Object &&
                        relationshipData.TryGetProperty("currentReputation", out var reputationNode) &&
                        reputationNode.ValueKind == JsonValueKind.Number &&
                        reputationNode.TryGetInt32(out var relationshipReputation))
                    {
                        return relationshipReputation;
                    }

                    if (guardian.TryGetProperty("reputation", out var reputationEl) &&
                        reputationEl.ValueKind == JsonValueKind.Number &&
                        reputationEl.TryGetInt32(out var reputation))
                    {
                        return reputation;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<int?> ReadGuardianAbodePowerAsync(string? guardiansJson, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (doc.RootElement.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                {
                    if (guardian.ValueKind != JsonValueKind.Object)
                        continue;

                    var currentGuardianId = GetStringValue(guardian, "guardianId");
                    if (!string.Equals(currentGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return AbodePowerRules.GetCurrentPower(guardian);
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<int?> ReadEnlightenmentExperienceAsync(string? soulJson)
    {
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (doc.RootElement.TryGetProperty("enlightenment", out var enlightenment) &&
                enlightenment.ValueKind == JsonValueKind.Object &&
                enlightenment.TryGetProperty("experience", out var expEl) &&
                expEl.ValueKind == JsonValueKind.Number &&
                expEl.TryGetInt32(out var enlightenmentExp))
            {
                return enlightenmentExp;
            }

            if (doc.RootElement.TryGetProperty("soulProgression", out var soulProgression) &&
                soulProgression.ValueKind == JsonValueKind.Object &&
                soulProgression.TryGetProperty("totalExperience", out var totalExpEl) &&
                totalExpEl.ValueKind == JsonValueKind.Number &&
                totalExpEl.TryGetInt32(out var soulExp))
            {
                return soulExp;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<JsonElement> CollectNewGuardianPowerJournalEntries(string? preJson, string? postJson)
    {
        var knownEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(preJson))
        {
            try
            {
                using var preDoc = JsonDocument.Parse(preJson);
                if (preDoc.RootElement.TryGetProperty("entries", out var preEntries) && preEntries.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in preEntries.EnumerateArray())
                    {
                        var eventId = GetStringValue(entry, "eventId");
                        if (!string.IsNullOrWhiteSpace(eventId))
                            knownEventIds.Add(eventId);
                    }
                }
            }
            catch (JsonException)
            {
                // ignored
            }
        }

        if (string.IsNullOrWhiteSpace(postJson))
            yield break;

        JsonDocument? postDoc = null;
        try
        {
            postDoc = JsonDocument.Parse(postJson);
            if (!postDoc.RootElement.TryGetProperty("entries", out var postEntries) || postEntries.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var entry in postEntries.EnumerateArray())
            {
                var eventId = GetStringValue(entry, "eventId");
                if (string.IsNullOrWhiteSpace(eventId) || knownEventIds.Contains(eventId))
                    continue;

                yield return entry.Clone();
            }
        }
        finally
        {
            postDoc?.Dispose();
        }
    }

    private static int CountOfferingFeathersFromJournal(string? journalJson, string guardianId, string returnCycleId)
    {
        if (string.IsNullOrWhiteSpace(journalJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(journalJson);
            return GuardianAbodeOfferingState.CountOfferedInkFeathersForReturnCycle(doc.RootElement, guardianId, returnCycleId);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static bool JournalEntryMatchesPendingAbodeOffering(JsonElement entry, GuardianAbodeOfferingState.PendingAbodeOfferingRequest request, int expectedGain)
    {
        if (!string.Equals(GetStringValue(entry, "guardianId"), request.GuardianId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetStringValue(entry, "reasonType"), "offering", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entry.TryGetProperty("delta", out var deltaNode) &&
            deltaNode.ValueKind == JsonValueKind.Number &&
            deltaNode.TryGetInt32(out var parsedDelta) &&
            parsedDelta != expectedGain)
        {
            return false;
        }

        if (!entry.TryGetProperty("audit", out var audit) || audit.ValueKind != JsonValueKind.Object)
            return false;

        if (!string.Equals(GetStringValue(audit, "offeringType"), request.OfferingType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetStringValue(audit, "returnCycleId"), request.ReturnCycleId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            return GetIntOrDefault(audit, "inkFeathersOffered") == request.InkFeathersOffered;

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetStringValue(audit, "relicId"), request.RelicId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetStringValue(audit, "relicName"), request.RelicName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetStringValue(audit, "relicRarity"), request.RelicRarity, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetStringValue(audit, "archiveId"), request.ArchiveId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetStringValue(audit, "archiveTitle"), request.ArchiveTitle, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetStringValue(audit, "archiveEntryType"), request.ArchiveEntryType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetStringValue(audit, "archiveRarity"), request.ArchiveRarity, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool JournalEntryContainsDetail(JsonElement entry, string fragment)
    {
        if (entry.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(fragment) ||
            !entry.TryGetProperty("details", out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var detail in details.EnumerateArray())
        {
            if (detail.ValueKind == JsonValueKind.String &&
                detail.GetString()?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> GuardianPowerJournalContainsEventAsync(string eventId, string guardianId, string reasonType, int delta, string? returnCycleId = null)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        var json = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!string.Equals(GetStringValue(entry, "eventId"), eventId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetStringValue(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetStringValue(entry, "reasonType"), reasonType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.TryGetProperty("delta", out var deltaNode) &&
                    deltaNode.ValueKind == JsonValueKind.Number &&
                    deltaNode.TryGetInt32(out var parsedDelta) &&
                    parsedDelta != delta)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(returnCycleId))
                {
                    if (!entry.TryGetProperty("audit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                        return false;

                    if (!string.Equals(GetStringValue(audit, "returnCycleId"), returnCycleId, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool SoulStateContainsSoulRelic(string? soulJson, string? relicId)
    {
        if (string.IsNullOrWhiteSpace(soulJson) || string.IsNullOrWhiteSpace(relicId))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (!doc.RootElement.TryGetProperty("soulRelics", out var relics))
                return false;

            if (relics.ValueKind == JsonValueKind.Array)
            {
                foreach (var relic in relics.EnumerateArray())
                {
                    if (string.Equals(GetStringValue(relic, "relicId"), relicId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            else if (relics.ValueKind == JsonValueKind.Object)
            {
                foreach (var propName in new[] { "stored", "equipped" })
                {
                    if (!relics.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var relic in arr.EnumerateArray())
                    {
                        if (string.Equals(GetStringValue(relic, "relicId"), relicId, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool SoulStateContainsAfterlifeArchiveEntry(string? soulJson, string? archiveId)
    {
        if (string.IsNullOrWhiteSpace(soulJson) || string.IsNullOrWhiteSpace(archiveId))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (!doc.RootElement.TryGetProperty("afterlifeArchive", out var archive) || archive.ValueKind != JsonValueKind.Object)
                return false;

            if (!archive.TryGetProperty("stored", out var stored) || stored.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in stored.EnumerateArray())
            {
                if (string.Equals(GetStringValue(entry, "archiveId"), archiveId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static int? ReadPrimaryExperienceCounter(string? experienceJson)
    {
        if (string.IsNullOrWhiteSpace(experienceJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(experienceJson);
            foreach (var propName in new[] { "totalExperience", "currentExperience", "experience" })
            {
                if (doc.RootElement.TryGetProperty(propName, out var valueEl) &&
                    valueEl.ValueKind == JsonValueKind.Number &&
                    valueEl.TryGetInt32(out var value))
                {
                    return value;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<bool> JsonFileContainsPropertyValueAsync(string filePath, string expectedValue, params string[] propertyNames)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ElementContainsNamedObject(doc.RootElement, expectedValue, propertyNames);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<bool> SoulImprintExistsAsync(string? imprintId, string? companionName)
    {
        var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("soulImprint", out var imprint) || imprint.ValueKind == JsonValueKind.Null)
                return false;

            if (!string.IsNullOrWhiteSpace(imprintId) &&
                ElementContainsNamedObject(imprint, imprintId!, "imprintId", "id"))
                return true;

            if (!string.IsNullOrWhiteSpace(companionName) &&
                ElementContainsNamedObject(imprint, companionName!, "NPCName", "name", "companionName"))
                return true;

            return !string.IsNullOrWhiteSpace(imprintId) || !string.IsNullOrWhiteSpace(companionName)
                ? false
                : imprint.ValueKind == JsonValueKind.Object || imprint.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<bool> PendingInkActionExistsAsync(string actionId, string actionTag, string expectedStatus, int expectedCostInFeathers, int expectedUpgradeTierDelta)
    {
        var json = await _fs.ReadFileAsync(PendingInkActionsPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("pendingActions", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var currentActionId = GetStringValue(item, "actionId");
                var currentActionTag = GetStringValue(item, "actionTag");
                var currentStatus = GetStringValue(item, "status");
                var currentCost = item.TryGetProperty("costInFeathers", out var costEl) && costEl.ValueKind == JsonValueKind.Number && costEl.TryGetInt32(out var parsedCost)
                    ? parsedCost
                    : int.MinValue;
                var currentUpgradeTierDelta = item.TryGetProperty("upgradeTierDelta", out var deltaEl) && deltaEl.ValueKind == JsonValueKind.Number && deltaEl.TryGetInt32(out var parsedDelta)
                    ? parsedDelta
                    : int.MinValue;
                if (string.Equals(currentActionId, actionId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentActionTag, actionTag, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(currentStatus, expectedStatus, StringComparison.OrdinalIgnoreCase) &&
                    currentCost == expectedCostInFeathers &&
                    currentUpgradeTierDelta == expectedUpgradeTierDelta)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private async Task<bool> SoulImprintHasMinimalCoreTraitsAsync(string? imprintId, string? companionName)
    {
        var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("soulImprint", out var imprint) || imprint.ValueKind == JsonValueKind.Null)
                return false;

            if (imprint.ValueKind == JsonValueKind.Object)
                return SoulImprintObjectMatchesMinimalContract(imprint, imprintId, companionName);

            if (imprint.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in imprint.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        SoulImprintObjectMatchesMinimalContract(item, imprintId, companionName))
                    {
                        return true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool SoulImprintObjectMatchesMinimalContract(JsonElement imprint, string? imprintId, string? companionName)
    {
        if (!string.IsNullOrWhiteSpace(imprintId))
        {
            var currentId = GetFirstString(imprint, "imprintId", "id");
            if (!string.Equals(currentId, imprintId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(companionName))
        {
            var currentName = GetFirstString(imprint, "NPCName", "name", "companionName", "originalName");
            if (!string.Equals(currentName, companionName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var hasName = !string.IsNullOrWhiteSpace(GetFirstString(imprint, "NPCName", "name", "companionName", "originalName"));
        var hasReference = !string.IsNullOrWhiteSpace(GetFirstString(imprint, "sourceCompanionId", "companionId", "NPCId")) ||
                           !string.IsNullOrWhiteSpace(imprintId) ||
                           !string.IsNullOrWhiteSpace(companionName);
        var hasDescription = !string.IsNullOrWhiteSpace(GetFirstString(imprint, "description", "summary", "backgroundStory", "history"));
        var hasCoreTraitsArray =
            imprint.TryGetProperty("coreTraitsPreserved", out var traits) &&
            traits.ValueKind == JsonValueKind.Array &&
            traits.GetArrayLength() > 0;
        var hasPersonalityTraits =
            imprint.TryGetProperty("personalityTraits", out var personalityTraits) &&
            personalityTraits.ValueKind == JsonValueKind.Array &&
            personalityTraits.GetArrayLength() > 0;

        return (hasName || hasReference) && hasDescription && (hasCoreTraitsArray || hasPersonalityTraits);
    }

    private static string GetFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var valueEl) &&
                valueEl.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(valueEl.GetString()))
            {
                return valueEl.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Strict post-turn validation for GM reasoning blocks in debug_logs.json.
    /// Used for accepted-turn enforcement, not for generic offline state validation.
    /// </summary>
    private async Task<List<ValidationIssue>> ValidateAcceptedTurnReasoningInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/debug_logs.json",
            "gm_thoughts_markdown",
            "accepted_turn_stale_debug_logs",
            "output/debug_logs.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/debug_logs.json. Перезапиши debug_logs.json заново для текущего accepted turn вместе с актуальным gm_thoughts_markdown.",
            issues);
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (string.IsNullOrWhiteSpace(debugJson))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return issues;
        }

        string gmThoughts;
        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "output/debug_logs.json", IssueSeverity.Error,
                    "output/debug_logs.json должен быть JSON object",
                    code: "invalid_debug_logs_json_root",
                    section: "gm_thoughts_markdown",
                    expected: "JSON object with gm_thoughts_markdown and timestamp",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: "Оставь output/debug_logs.json валидным JSON-объектом с полями gm_thoughts_markdown и timestamp."));
                return issues;
            }

            gmThoughts = doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gm) &&
                         gm.ValueKind == JsonValueKind.String
                ? gm.GetString() ?? string.Empty
                : string.Empty;

            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/debug_logs.json",
                issues,
                "timestamp",
                "gm_thoughts_markdown",
                "debug_logs_missing_timestamp",
                "debug_logs_invalid_timestamp",
                "Добавь в output/debug_logs.json поле timestamp в ISO 8601 формате вместе с актуальным gm_thoughts_markdown.");

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(prop.Name, "gm_thoughts_markdown", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
                    continue;

                issues.Add(new ValidationIssue(
                    $"output/debug_logs.json.{prop.Name}",
                    IssueSeverity.Error,
                    "output/debug_logs.json содержит неподдерживаемое top-level поле",
                    code: "debug_logs_unknown_field",
                    section: "gm_thoughts_markdown",
                    expected: "gm_thoughts_markdown | timestamp",
                    actual: prop.Name,
                    repairHint: "Пиши в output/debug_logs.json только gm_thoughts_markdown и timestamp для текущего хода."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"output/debug_logs.json должен быть валидным JSON: {ex.Message}",
                code: "invalid_debug_logs_json",
                section: "gm_thoughts_markdown",
                expected: "valid JSON object with gm_thoughts_markdown and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/debug_logs.json и оставь в нём валидный JSON-объект с полями gm_thoughts_markdown и timestamp."));
            return issues;
        }

        if (string.IsNullOrWhiteSpace(gmThoughts))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return issues;
        }

        var normalizedThoughts = gmThoughts.Replace("\r\n", "\n");
        if (!TryParseReasoningScope(normalizedThoughts, out var scope))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует обязательная секция 'Охват NPC-анализа' / 'NPC Scope' в gm_thoughts_markdown",
                code: "missing_npc_scope",
                section: "npc_scope",
                expected: "NPC scope declaration section",
                actual: "missing",
                repairHint: "Добавь секцию '## Охват NPC-анализа' с режимом, релевантными акторами и обоснованием."));
            return issues;
        }

        var scopeMode = ParseReasoningScopeMode(scope.Mode);
        if (!scope.HasModeField || scopeMode == ReasoningScopeMode.Unknown)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode)
                    ? "В секции охвата NPC-анализа отсутствует поле 'Режим' / 'Mode'"
                    : $"Неизвестный режим NPC scope: '{scope.Mode}'",
                code: !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode) ? "missing_scope_mode" : "invalid_scope_mode",
                section: "npc_scope",
                expected: "Mode = Scene-local, World-progression, Guardian-centric or Mixed",
                actual: !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode) ? "missing" : scope.Mode,
                repairHint: "Укажи режим: Scene-local, World-progression, Guardian-centric или Mixed."));
        }

        if (!scope.HasRelevantActorsField)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует поле 'Релевантные акторы' / 'Relevant actors'",
                code: "missing_relevant_actors_field",
                section: "npc_scope",
                expected: "Relevant actors field",
                actual: "missing",
                repairHint: "Явно укажи 'Релевантные акторы: ...' или 'Релевантные акторы: нет' для Scene-local хода."));
        }

        if (!scope.HasWhyRelevantField || string.IsNullOrWhiteSpace(scope.WhyRelevant))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует обоснование релевантных акторов",
                code: "missing_scope_relevance_reason",
                section: "npc_scope",
                expected: "Why relevant field",
                actual: "missing",
                repairHint: "Добавь строку 'Почему они релевантны' с объяснением выбора акторов."));
        }

        if (!scope.HasOutOfScopeActorsField)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует поле 'Акторы вне охвата' / 'Actors outside scope'",
                code: "missing_out_of_scope_actors_field",
                section: "npc_scope",
                expected: "Actors outside scope field",
                actual: "missing",
                repairHint: "Явно укажи 'Акторы вне охвата: ...' или 'Акторы вне охвата: нет'."));
        }

        if (!scope.HasOutOfScopeReasonField || string.IsNullOrWhiteSpace(scope.OutOfScopeReason))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует обоснование акторов вне охвата",
                code: "missing_scope_out_of_scope_reason",
                section: "npc_scope",
                expected: "Why outside scope field",
                actual: "missing",
                repairHint: "Добавь строку 'Почему они вне охвата' с объяснением, почему остальные акторы не обрабатываются."));
        }

        var actorType = scopeMode == ReasoningScopeMode.GuardianCentric ? "Guardian" : "Actor";

        if ((scopeMode == ReasoningScopeMode.WorldProgression ||
             scopeMode == ReasoningScopeMode.GuardianCentric ||
             scopeMode == ReasoningScopeMode.Mixed) &&
            scope.RelevantActors.Count == 0)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Режим '{scope.Mode}' требует непустой список релевантных акторов",
                code: "empty_relevant_actors_for_mode",
                section: "npc_scope",
                expected: "At least one relevant actor",
                actual: "empty relevant actor list",
                repairHint: "Либо добавь релевантных акторов, либо используй режим Scene-local с явным обоснованием пустого scope."));
        }

        if (scope.RelevantActors.Count > 0 &&
            !ContainsAny(normalizedThoughts.ToLowerInvariant(),
                "## размышления npc", "## 🎭 обязательно: размышления npc", "## npc thoughts",
                "## npc brain", "## reasoning", "## размышления акторов", "## guardian thoughts"))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Для задекларированных акторов отсутствует допустимая reasoning section",
                code: "missing_actor_reasoning_section",
                section: "npc_reasoning",
                expected: "Reasoning section with actor blocks",
                actual: "missing",
                repairHint: "Добавь отдельную reasoning section ('Размышления NPC', 'Размышления акторов', 'Guardian Thoughts' или эквивалентный heading) и подпункты ### [Actor Name] для всех релевантных акторов."));
        }

        var structuredActorUpdates = await CollectStructuredActorUpdatesAsync();
        ValidateStructuredActorUpdatesAgainstScope(scope, structuredActorUpdates, issues);

        var preTurnGuardianScopeRealm = await TryResolvePreTurnRealmAsync();
        var activeGuardianNames = string.IsNullOrWhiteSpace(preTurnGuardianScopeRealm)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : await CollectImportantGuardianNamesAsync(preTurnGuardianScopeRealm);
        var knownNpcReferences = await ReadKnownNpcReferencesAsync();
        var knownNpcActorAliases = new HashSet<string>(knownNpcReferences.Names, StringComparer.OrdinalIgnoreCase);
        foreach (var npcId in knownNpcReferences.Ids)
            knownNpcActorAliases.Add(npcId);

        if (scopeMode == ReasoningScopeMode.GuardianCentric &&
            activeGuardianNames.Count > 0 &&
            !scope.RelevantActors.Any(actor => activeGuardianNames.Contains(actor)))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Guardian-centric режим не включает активного Хранителя в declared relevant actors",
                code: "active_guardian_missing_from_scope",
                section: "npc_scope",
                expected: "Active Guardian in relevant actors",
                actual: "active guardian absent from scope",
                repairHint: "В guardian-centric режиме включи активного Хранителя в список релевантных акторов."));
        }

        foreach (var actorName in scope.RelevantActors)
        {
            var requiresNpcLocationAudit = knownNpcActorAliases.Contains(actorName) &&
                                           !activeGuardianNames.Contains(actorName);
            ValidateActorReasoningBlock(normalizedThoughts, actorName, actorType, requiresNpcLocationAudit, issues);
        }

        return issues;
    }

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnSpecialActionOutcomesInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(requestJson))
            return issues;

        try
        {
            using var requestDoc = JsonDocument.Parse(requestJson);
            if (!requestDoc.RootElement.TryGetProperty("playerAction", out var actionEl) ||
                actionEl.ValueKind != JsonValueKind.String)
                return issues;

            var playerAction = actionEl.GetString() ?? string.Empty;
            var actionContext = ParseInkFeatherActionContext(requestDoc.RootElement, playerAction);
            if (actionContext == null)
                return issues;

            if (ClientSideInkFeatherActions.Contains(actionContext.ActionTag))
                return issues;

            if (!GmSideInkFeatherActions.Contains(actionContext.ActionTag))
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    $"Неподдерживаемый INK_FEATHER_ACTION: {actionContext.ActionTag}",
                    code: "unsupported_ink_feather_action",
                    section: "INK_FEATHER_ACTION",
                    expected: string.Join(", ", GmSideInkFeatherActions.OrderBy(x => x)),
                    actual: actionContext.ActionTag,
                    repairHint: "Используй только поддерживаемые GM-side Ink Feather action tags."));
                return issues;
            }

            var currentRealm = await TryResolvePreTurnRealmAsync();
            if (string.IsNullOrWhiteSpace(currentRealm))
                return issues;
            var isChaosSea = IsChaosSeaRealm(currentRealm);
            if (isChaosSea && !ChaosSeaGmInkFeatherActions.Contains(actionContext.ActionTag))
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    $"INK_FEATHER_ACTION {actionContext.ActionTag} запрещён в текущем realm {currentRealm}",
                    code: "ink_feather_wrong_realm",
                    section: "INK_FEATHER_ACTION",
                    expected: string.Join(", ", ChaosSeaGmInkFeatherActions.OrderBy(x => x)),
                    actual: actionContext.ActionTag,
                    repairHint: "В Chaos Sea используй только Chaos-Sea Ink Feather whitelist."));
                return issues;
            }

            if (!isChaosSea && !MortalWorldGmInkFeatherActions.Contains(actionContext.ActionTag))
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    $"INK_FEATHER_ACTION {actionContext.ActionTag} запрещён в текущем realm {currentRealm}",
                    code: "ink_feather_wrong_realm",
                    section: "INK_FEATHER_ACTION",
                    expected: string.Join(", ", MortalWorldGmInkFeatherActions.OrderBy(x => x)),
                    actual: actionContext.ActionTag,
                    repairHint: "В Mortal World используй только Mortal-World Ink Feather whitelist."));
                return issues;
            }

            JsonDocument? receiptDoc = null;
            try
            {
                var (receiptIssues, parsedReceiptDoc) = await ValidateInkFeatherActionReceiptAsync(actionContext);
                issues.AddRange(receiptIssues);
                if (parsedReceiptDoc == null)
                    return issues;

                receiptDoc = parsedReceiptDoc;
                var receiptRoot = receiptDoc.RootElement;

                switch (actionContext.ActionTag)
                {
                    case "SACRIFICE_TO_CHAOS":
                        await ValidateSacrificeToChaosOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "ABSORB_FEATHERS":
                        await ValidateAbsorbFeathersOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "LEARN_SKILL":
                        await ValidateLearnSkillOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "FATE_SHIELD":
                        await ValidateFateShieldOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "SEAL_IN_INK":
                        await ValidateSealInInkOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "DONATE_TO_GUARDIAN":
                        await ValidateDonateToGuardianOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "CULTIVATE_ENLIGHTENMENT":
                        await ValidateCultivateEnlightenmentOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "GUARDIAN_FAVOR":
                        await ValidateGuardianFavorOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case GuardianAbodeOfferingState.ActionTag:
                        await ValidateAbodeOfferingOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "SOUL_IMPRINT":
                        await ValidateSoulImprintOutcomeAsync(receiptRoot, actionContext, issues);
                        return issues;

                    case "MEMORY_GATES":
                        ValidateResolutionType(receiptRoot, actionContext.ActionTag, "memoryLegacy", issues);
                        if (TryGetStateEvidence(receiptRoot, actionContext.ActionTag, issues, out var memoryLegacyEvidence))
                        {
                            RequireString(memoryLegacyEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "legacyId");
                            RequireString(memoryLegacyEvidence, $"{InkFeatherActionResultPath}.stateEvidence", issues, "legacyType");
                            var changedFiles = await ValidateAffectedFilesChangedAsync(
                                memoryLegacyEvidence,
                                actionContext.ActionTag,
                                issues,
                                new[] { "game_state/meta/soul_state.json" });
                            if (changedFiles.Count == 0)
                                AddMissingStateEvidenceIssue(issues, actionContext.ActionTag, "memory legacy grant", "game_state/meta/soul_state.json должен реально измениться после MEMORY_GATES.");
                        }
                        break;
                }
            }
            finally
            {
                receiptDoc?.Dispose();
            }

            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                    "После MEMORY_GATES отсутствует soul_state.json с pendingMemoryLegacy",
                code: "memory_gates_missing_soul_state",
                section: "MEMORY_GATES",
                expected: "soul_state.json с valid pendingMemoryLegacy",
                actual: "missing file",
                repairHint: "После INK_FEATHER_ACTION: MEMORY_GATES создай metaStateUpdates.memoryLegacyGrant и сохрани в soul_state canonical pendingMemoryLegacy с legacyId, legacyType, sourceLifeHint, grantSource=memoryLegacyGrant, applicationState и grantSnapshot."));
            return issues;
        }

            using var soulDoc = JsonDocument.Parse(soulJson);
            if (!soulDoc.RootElement.TryGetProperty("pendingMemoryLegacy", out var pendingLegacy) ||
                pendingLegacy.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.pendingMemoryLegacy",
                    IssueSeverity.Error,
                "После MEMORY_GATES должен быть создан pendingMemoryLegacy с механической наградой следующей жизни",
                code: "memory_gates_missing_legacy",
                section: "MEMORY_GATES",
                expected: "pendingMemoryLegacy object",
                actual: pendingLegacy.ValueKind.ToString(),
                repairHint: "После INK_FEATHER_ACTION: MEMORY_GATES обязательно создай metaStateUpdates.memoryLegacyGrant и сохрани в soul_state canonical pendingMemoryLegacy с legacyId, legacyType, sourceLifeHint, grantSource=memoryLegacyGrant, applicationState и grantSnapshot."));
            return issues;
        }

            if (!pendingLegacy.TryGetProperty("grantSource", out var grantSource) ||
                grantSource.ValueKind != JsonValueKind.String ||
                !string.Equals(grantSource.GetString(), "memoryLegacyGrant", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.pendingMemoryLegacy.grantSource",
                    IssueSeverity.Error,
                    "После MEMORY_GATES pendingMemoryLegacy должен происходить из structured metaStateUpdates.memoryLegacyGrant",
                    code: "memory_gates_missing_grant_source",
                    section: "MEMORY_GATES",
                    expected: "grantSource = memoryLegacyGrant",
                    actual: grantSource.ValueKind == JsonValueKind.String ? grantSource.GetString() ?? "" : grantSource.ValueKind.ToString(),
                    repairHint: "Не записывай pendingMemoryLegacy вручную как самостоятельный финальный результат. Используй structured metaStateUpdates.memoryLegacyGrant."));
            }

            if (!pendingLegacy.TryGetProperty("grantSnapshot", out var grantSnapshot) ||
                grantSnapshot.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.pendingMemoryLegacy.grantSnapshot",
                    IssueSeverity.Error,
                    "После MEMORY_GATES pendingMemoryLegacy должен содержать grantSnapshot, полученный из structured memoryLegacyGrant",
                    code: "memory_gates_missing_grant_snapshot",
                    section: "MEMORY_GATES",
                    expected: "grantSnapshot object",
                    actual: grantSnapshot.ValueKind.ToString(),
                    repairHint: "Canonical pendingMemoryLegacy должен сохранять grantSnapshot от memoryLegacyGrant, а не только итоговые поля."));
            }
            else
            {
                var snapshotIsCanonical = ValidateMemoryLegacyGrantObject(grantSnapshot, "game_state/meta/soul_state.json.pendingMemoryLegacy.grantSnapshot", issues);
                if (!snapshotIsCanonical)
                    return issues;

                var pendingLegacyType = pendingLegacy.TryGetProperty("legacyType", out var pendingLegacyTypeEl) && pendingLegacyTypeEl.ValueKind == JsonValueKind.String
                    ? pendingLegacyTypeEl.GetString() ?? string.Empty
                    : string.Empty;
                var snapshotLegacyType = grantSnapshot.TryGetProperty("legacyType", out var snapshotLegacyTypeEl) && snapshotLegacyTypeEl.ValueKind == JsonValueKind.String
                    ? snapshotLegacyTypeEl.GetString() ?? string.Empty
                    : string.Empty;
                var pendingLegacyId = pendingLegacy.TryGetProperty("legacyId", out var legacyIdEl) && legacyIdEl.ValueKind == JsonValueKind.String
                    ? legacyIdEl.GetString() ?? string.Empty
                    : string.Empty;
                var snapshotLegacyId = grantSnapshot.TryGetProperty("legacyId", out var snapshotLegacyIdEl) && snapshotLegacyIdEl.ValueKind == JsonValueKind.String
                    ? snapshotLegacyIdEl.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(pendingLegacyType) &&
                    !string.IsNullOrWhiteSpace(snapshotLegacyType) &&
                    !string.Equals(pendingLegacyType, snapshotLegacyType, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.pendingMemoryLegacy.legacyType",
                        IssueSeverity.Error,
                        "legacyType в pendingMemoryLegacy должен совпадать с legacyType в grantSnapshot",
                        code: "memory_gates_legacy_type_mismatch",
                        section: "MEMORY_GATES",
                        expected: snapshotLegacyType,
                        actual: pendingLegacyType,
                        repairHint: "Синхронизируй canonical pendingMemoryLegacy с данным structured memoryLegacyGrant."));
                }

                if (!string.IsNullOrWhiteSpace(pendingLegacyId) &&
                    !string.IsNullOrWhiteSpace(snapshotLegacyId) &&
                    !string.Equals(pendingLegacyId, snapshotLegacyId, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.pendingMemoryLegacy.legacyId",
                        IssueSeverity.Error,
                        "legacyId в pendingMemoryLegacy должен совпадать с legacyId в grantSnapshot",
                        code: "memory_gates_legacy_id_mismatch",
                        section: "MEMORY_GATES",
                        expected: snapshotLegacyId,
                        actual: pendingLegacyId,
                        repairHint: "Синхронизируй canonical pendingMemoryLegacy с данным structured memoryLegacyGrant."));
                }

                if (string.Equals(snapshotLegacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
                {
                    var pendingCharacteristic = pendingLegacy.TryGetProperty("characteristic", out var pendingCharacteristicEl) && pendingCharacteristicEl.ValueKind == JsonValueKind.String
                        ? pendingCharacteristicEl.GetString() ?? string.Empty
                        : string.Empty;
                    var snapshotCharacteristic = grantSnapshot.TryGetProperty("characteristic", out var snapshotCharacteristicEl) && snapshotCharacteristicEl.ValueKind == JsonValueKind.String
                        ? snapshotCharacteristicEl.GetString() ?? string.Empty
                        : string.Empty;
                    var pendingBonus = pendingLegacy.TryGetProperty("bonus", out var pendingBonusEl) && pendingBonusEl.ValueKind == JsonValueKind.Number && pendingBonusEl.TryGetInt32(out var pb)
                        ? pb
                        : (int?)null;
                    var snapshotBonus = grantSnapshot.TryGetProperty("bonus", out var snapshotBonusEl) && snapshotBonusEl.ValueKind == JsonValueKind.Number && snapshotBonusEl.TryGetInt32(out var sb)
                        ? sb
                        : (int?)null;

                    if (!string.Equals(pendingCharacteristic, snapshotCharacteristic, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/soul_state.json.pendingMemoryLegacy.characteristic",
                            IssueSeverity.Error,
                            "characteristic в pendingMemoryLegacy должен совпадать с grantSnapshot",
                            code: "memory_gates_characteristic_mismatch",
                            section: "MEMORY_GATES",
                            expected: snapshotCharacteristic,
                            actual: pendingCharacteristic,
                            repairHint: "Не меняй характеристику между structured grant и canonical pendingMemoryLegacy."));
                    }

                    if (pendingBonus != snapshotBonus)
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/soul_state.json.pendingMemoryLegacy.bonus",
                            IssueSeverity.Error,
                            "bonus в pendingMemoryLegacy должен совпадать с grantSnapshot",
                            code: "memory_gates_bonus_mismatch",
                            section: "MEMORY_GATES",
                            expected: snapshotBonus?.ToString() ?? "missing",
                            actual: pendingBonus?.ToString() ?? "missing",
                            repairHint: "Не меняй размер бонуса между structured grant и canonical pendingMemoryLegacy."));
                    }
                }
                else if (string.Equals(snapshotLegacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
                {
                    var fields = new[]
                    {
                        ("skillName", "memory_gates_skill_name_mismatch", "имя навыка"),
                        ("group", "memory_gates_skill_group_mismatch", "группу навыка")
                    };

                    foreach (var (fieldName, code, label) in fields)
                    {
                        var pendingValue = pendingLegacy.TryGetProperty(fieldName, out var pendingValueEl) && pendingValueEl.ValueKind == JsonValueKind.String
                            ? pendingValueEl.GetString() ?? string.Empty
                            : string.Empty;
                        var snapshotValue = grantSnapshot.TryGetProperty(fieldName, out var snapshotValueEl) && snapshotValueEl.ValueKind == JsonValueKind.String
                            ? snapshotValueEl.GetString() ?? string.Empty
                            : string.Empty;

                        if (!string.Equals(pendingValue, snapshotValue, StringComparison.Ordinal))
                        {
                            issues.Add(new ValidationIssue(
                                $"game_state/meta/soul_state.json.pendingMemoryLegacy.{fieldName}",
                                IssueSeverity.Error,
                                $"{label} в pendingMemoryLegacy должна совпадать с grantSnapshot",
                                code: code,
                                section: "MEMORY_GATES",
                                expected: snapshotValue,
                                actual: pendingValue,
                                repairHint: "Canonical pendingMemoryLegacy должен faithfully отражать structured grant для skill-based Наследия Памяти."));
                        }
                    }

                    var pendingPlayerStatBonus = pendingLegacy.TryGetProperty("playerStatBonus", out var pendingPlayerStatBonusEl) && pendingPlayerStatBonusEl.ValueKind == JsonValueKind.String
                        ? pendingPlayerStatBonusEl.GetString() ?? string.Empty
                        : string.Empty;
                    var snapshotPlayerStatBonus = grantSnapshot.TryGetProperty("playerStatBonus", out var snapshotPlayerStatBonusEl) && snapshotPlayerStatBonusEl.ValueKind == JsonValueKind.String
                        ? snapshotPlayerStatBonusEl.GetString() ?? string.Empty
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(snapshotPlayerStatBonus) && string.IsNullOrWhiteSpace(pendingPlayerStatBonus))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/soul_state.json.pendingMemoryLegacy.playerStatBonus",
                            IssueSeverity.Error,
                            "playerStatBonus в pendingMemoryLegacy не должен исчезать после MEMORY_GATES для startingPassiveKnowledgeSkill",
                            code: "memory_gates_skill_player_stat_bonus_missing",
                            section: "MEMORY_GATES",
                            expected: "non-empty playerStatBonus summary",
                            actual: "missing or empty",
                            repairHint: "Сохрани в pendingMemoryLegacy непустой playerStatBonus summary и не убирай это поле относительно grantSnapshot."));
                    }

                    var pendingBonusesJson = pendingLegacy.TryGetProperty("structuredBonuses", out var pendingBonusesEl) && pendingBonusesEl.ValueKind == JsonValueKind.Array
                        ? StructuredBonusCanonicalizer.Canonicalize(pendingBonusesEl)
                        : string.Empty;
                    var snapshotBonusesJson = grantSnapshot.TryGetProperty("structuredBonuses", out var snapshotBonusesEl) && snapshotBonusesEl.ValueKind == JsonValueKind.Array
                        ? StructuredBonusCanonicalizer.Canonicalize(snapshotBonusesEl)
                        : string.Empty;
                    if (!string.Equals(pendingBonusesJson, snapshotBonusesJson, StringComparison.Ordinal))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/meta/soul_state.json.pendingMemoryLegacy.structuredBonuses",
                            IssueSeverity.Error,
                            "structuredBonuses в pendingMemoryLegacy должны совпадать с grantSnapshot",
                            code: "memory_gates_skill_bonuses_mismatch",
                            section: "MEMORY_GATES",
                            expected: snapshotBonusesJson,
                            actual: pendingBonusesJson,
                            repairHint: "Canonical pendingMemoryLegacy должен сохранять тот же набор structuredBonuses, что и structured memoryLegacyGrant."));
                    }
                }
            }

            var previousLegacyJson = await ReadPreviousPendingMemoryLegacyJsonAsync();
            var currentLegacyJson = BuildPendingMemoryLegacyComparisonSignature(pendingLegacy);
            if (!string.IsNullOrWhiteSpace(previousLegacyJson) &&
                string.Equals(previousLegacyJson, currentLegacyJson, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.pendingMemoryLegacy",
                    IssueSeverity.Error,
                    "После MEMORY_GATES старое active pendingMemoryLegacy не было заменено новым наследием",
                    code: "memory_gates_legacy_not_replaced",
                    section: "MEMORY_GATES",
                    expected: "New pendingMemoryLegacy distinct from the pre-turn one",
                    actual: "Unchanged pre-turn pendingMemoryLegacy",
                    repairHint: "После новой траты на MEMORY_GATES обязательно замени старое наследие новым объектом с новым или изменённым содержанием."));
            }

            return issues;
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json",
                IssueSeverity.Error,
                $"Не удалось проверить MEMORY_GATES из-за невалидного turn_request.json: {ex.Message}",
                code: "memory_gates_request_parse_failed",
                section: "MEMORY_GATES",
                expected: "Valid current turn_request.json",
                actual: "Invalid JSON",
                repairHint: "Это client/protocol input failure: validation MEMORY_GATES не смогла прочитать playerAction из input/turn_request.json. GM не должен чинить state-файлы вместо broken turn_request lifecycle."));
        }

        return issues;
    }

    private async Task<List<ValidationIssue>> ValidatePendingMemoryLegacyApplicationInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
                return issues;

            using var soulDoc = JsonDocument.Parse(soulJson);
            if (!soulDoc.RootElement.TryGetProperty("pendingMemoryLegacy", out var pendingLegacy) ||
                pendingLegacy.ValueKind != JsonValueKind.Object)
                return issues;

            var applicationState = pendingLegacy.TryGetProperty("applicationState", out var applicationStateEl) &&
                                   applicationStateEl.ValueKind == JsonValueKind.String
                ? applicationStateEl.GetString() ?? "pending"
                : "pending";
            if (!string.Equals(applicationState, "applied-awaiting-turn-accept", StringComparison.OrdinalIgnoreCase))
                return issues;

            if (!pendingLegacy.TryGetProperty("applicationAudit", out var audit) ||
                audit.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.pendingMemoryLegacy.applicationAudit",
                    IssueSeverity.Error,
                    "Активированное Наследие Памяти должно содержать applicationAudit до завершения хода воплощения",
                    code: "memory_legacy_missing_application_audit",
                    section: "MEMORY_GATES",
                    expected: "applicationAudit object",
                    actual: audit.ValueKind.ToString(),
                    repairHint: "Клиент должен сохранить applicationAudit перед отправкой хода воплощения; GM не должен затирать pendingMemoryLegacy."));
                return issues;
            }

            var legacyType = pendingLegacy.TryGetProperty("legacyType", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                ? typeEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
            {
                var characteristic = pendingLegacy.TryGetProperty("characteristic", out var characteristicEl) && characteristicEl.ValueKind == JsonValueKind.String
                    ? characteristicEl.GetString() ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(characteristic))
                    return issues;

                if (!audit.TryGetProperty("expectedCharacteristicValue", out var expectedEl) ||
                    expectedEl.ValueKind != JsonValueKind.Number ||
                    !expectedEl.TryGetInt32(out var expectedValue))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.pendingMemoryLegacy.applicationAudit.expectedCharacteristicValue",
                        IssueSeverity.Error,
                        "Для characteristic-based Наследия Памяти должен быть сохранён expectedCharacteristicValue",
                        code: "memory_legacy_missing_expected_characteristic_value",
                        section: "MEMORY_GATES",
                        expected: "expectedCharacteristicValue number in applicationAudit",
                        actual: "missing",
                        repairHint: "При локальном применении characteristic-based Memory Legacy клиент должен сохранить expectedCharacteristicValue в applicationAudit до завершения accepted incarnation turn."));
                    return issues;
                }

                var currentValue = await ReadCurrentCharacteristicValueAsync(characteristic);
                if (!currentValue.HasValue || currentValue.Value < expectedValue)
                {
                    issues.Add(new ValidationIssue(
                        $"game_state/misc/characteristics.json.{characteristic}",
                        IssueSeverity.Error,
                        "После хода воплощения локально применённый бонус Наследия Памяти к характеристике был утерян или затёрт",
                        code: "memory_legacy_characteristic_lost",
                        section: "MEMORY_GATES",
                        expected: $">= {expectedValue}",
                        actual: currentValue?.ToString() ?? "missing",
                        repairHint: "Сохрани применённый бонус к характеристике; не перезаписывай characteristics.json без уже активированного Наследия Памяти."));
                }
            }
            else if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
            {
                if (!audit.TryGetProperty("expectedPassiveSkillName", out var expectedSkillNameEl) ||
                    expectedSkillNameEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(expectedSkillNameEl.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.pendingMemoryLegacy.applicationAudit.expectedPassiveSkillName",
                        IssueSeverity.Error,
                        "Для skill-based Наследия Памяти должен быть сохранён expectedPassiveSkillName",
                        code: "memory_legacy_missing_expected_passive_skill_name",
                        section: "MEMORY_GATES",
                        expected: "expectedPassiveSkillName string in applicationAudit",
                        actual: "missing",
                        repairHint: "При локальном применении passive-skill Memory Legacy клиент должен сохранить expectedPassiveSkillName в applicationAudit до завершения accepted incarnation turn."));
                    return issues;
                }

                var expectedSkillName = expectedSkillNameEl.GetString()!;
                var expectedGroup = audit.TryGetProperty("expectedGroup", out var expectedGroupEl) && expectedGroupEl.ValueKind == JsonValueKind.String
                    ? expectedGroupEl.GetString() ?? "Knowledge"
                    : "Knowledge";
                var expectedPlayerStatBonus = audit.TryGetProperty("expectedPlayerStatBonus", out var expectedPlayerStatBonusEl) && expectedPlayerStatBonusEl.ValueKind == JsonValueKind.String
                    ? expectedPlayerStatBonusEl.GetString() ?? string.Empty
                    : string.Empty;
                var expectedStructuredBonusesCount = audit.TryGetProperty("expectedStructuredBonusesCount", out var expectedStructuredBonusesCountEl) &&
                                                     expectedStructuredBonusesCountEl.ValueKind == JsonValueKind.Number &&
                                                     expectedStructuredBonusesCountEl.TryGetInt32(out var count)
                    ? count
                    : 0;
                var expectedStructuredBonusesCanonical = audit.TryGetProperty("expectedStructuredBonusesCanonical", out var expectedStructuredBonusesCanonicalEl) &&
                                                         expectedStructuredBonusesCanonicalEl.ValueKind == JsonValueKind.String
                    ? expectedStructuredBonusesCanonicalEl.GetString() ?? string.Empty
                    : string.Empty;

                var skillStillMatches = await PassiveSkillMatchesExpectedShapeAsync(
                    expectedSkillName,
                    expectedGroup,
                    expectedPlayerStatBonus,
                    expectedStructuredBonusesCount,
                    expectedStructuredBonusesCanonical);

                if (!skillStillMatches)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/player/skills_passive.json",
                        IssueSeverity.Error,
                        "После хода воплощения локально применённый пассивный навык Наследия Памяти был утерян или затёрт",
                        code: "memory_legacy_skill_lost",
                        section: "MEMORY_GATES",
                        expected: $"{expectedSkillName} / group={expectedGroup} / bonuses>={expectedStructuredBonusesCount}",
                        actual: "missing or degraded shape",
                        repairHint: "Сохрани уже активированный пассивный навык Наследия Памяти с group=Knowledge, непустым playerStatBonus и непустыми structuredBonuses."));
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                $"Не удалось проверить применение Наследия Памяти из-за невалидного soul_state.json: {ex.Message}",
                code: "memory_legacy_application_state_parse_failed",
                section: "MEMORY_GATES",
                expected: "Valid current soul_state.json",
                actual: "Invalid JSON",
                repairHint: "Исправь persisted pendingMemoryLegacy state; validation применения Наследия Памяти должна опираться на applicationState, а не на текст playerAction."));
        }

        return issues;
    }

    private async Task<string?> ReadPreviousPendingMemoryLegacyJsonAsync()
    {
        const string snapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        var json = await _fs.ReadFileAsync(snapshotPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("pendingMemoryLegacy", out var pendingLegacy) &&
                pendingLegacy.ValueKind == JsonValueKind.Object)
            {
                return BuildPendingMemoryLegacyComparisonSignature(pendingLegacy);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string BuildPendingMemoryLegacyComparisonSignature(JsonElement pendingLegacy)
    {
        var parts = new List<string>();
        foreach (var property in pendingLegacy.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var valueSignature = property.Name.Equals("structuredBonuses", StringComparison.Ordinal) &&
                                 property.Value.ValueKind == JsonValueKind.Array
                ? StructuredBonusCanonicalizer.Canonicalize(property.Value)
                : BuildCanonicalJsonSignature(property.Value);
            parts.Add($"{property.Name}:{valueSignature}");
        }

        return "{" + string.Join(",", parts) + "}";
    }

    private async Task<int?> ReadCurrentCharacteristicValueAsync(string characteristic)
    {
        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(characteristic, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed))
            {
                return parsed;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<bool> PassiveSkillMatchesExpectedShapeAsync(
        string skillName,
        string expectedGroup,
        string expectedPlayerStatBonus,
        int expectedStructuredBonusesCount,
        string expectedStructuredBonusesCanonical)
    {
        var json = await _fs.ReadFileAsync("game_state/player/skills_passive.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("passiveSkillChanges", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            if (!PassiveSkillArrayContainsExpectedShape(
                    arr,
                    skillName,
                    expectedGroup,
                    expectedPlayerStatBonus,
                    expectedStructuredBonusesCount,
                    expectedStructuredBonusesCanonical))
            {
                return false;
            }

            var preTurnJson = ReadPreTurnTrackedFileSync("game_state/player/skills_passive.json");
            if (string.IsNullOrWhiteSpace(preTurnJson))
                return true;

            try
            {
                using var preTurnDoc = JsonDocument.Parse(preTurnJson);
                if (preTurnDoc.RootElement.TryGetProperty("passiveSkillChanges", out var preTurnArr) &&
                    preTurnArr.ValueKind == JsonValueKind.Array &&
                    PassiveSkillArrayContainsExpectedShape(
                        preTurnArr,
                        skillName,
                        expectedGroup,
                        expectedPlayerStatBonus,
                        expectedStructuredBonusesCount,
                        expectedStructuredBonusesCanonical))
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                // Ignore malformed pre-turn snapshot here; generic state validation surfaces it separately.
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool PassiveSkillArrayContainsExpectedShape(
        JsonElement arr,
        string skillName,
        string expectedGroup,
        string expectedPlayerStatBonus,
        int expectedStructuredBonusesCount,
        string expectedStructuredBonusesCanonical)
    {
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            if (item.TryGetProperty("skillName", out var nameEl) &&
                nameEl.ValueKind == JsonValueKind.String &&
                string.Equals(nameEl.GetString(), skillName, StringComparison.OrdinalIgnoreCase))
            {
                var group = item.TryGetProperty("group", out var groupEl) && groupEl.ValueKind == JsonValueKind.String
                    ? groupEl.GetString() ?? string.Empty
                    : string.Empty;
                var playerStatBonus = item.TryGetProperty("playerStatBonus", out var playerStatBonusEl) && playerStatBonusEl.ValueKind == JsonValueKind.String
                    ? playerStatBonusEl.GetString() ?? string.Empty
                    : string.Empty;
                var structuredBonusesCount = item.TryGetProperty("structuredBonuses", out var bonusesEl) && bonusesEl.ValueKind == JsonValueKind.Array
                    ? bonusesEl.GetArrayLength()
                    : 0;

                return string.Equals(group, expectedGroup, StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(playerStatBonus) &&
                       structuredBonusesCount >= expectedStructuredBonusesCount &&
                       structuredBonusesCount > 0 &&
                       (string.IsNullOrWhiteSpace(expectedStructuredBonusesCanonical) ||
                        (item.TryGetProperty("structuredBonuses", out var structuredBonusesEl) &&
                         structuredBonusesEl.ValueKind == JsonValueKind.Array &&
                         string.Equals(StructuredBonusCanonicalizer.Canonicalize(structuredBonusesEl), expectedStructuredBonusesCanonical, StringComparison.Ordinal)));
            }
        }

        return false;
    }

    /// <summary>
    /// Quick validation that a file contains valid JSON.
    /// </summary>
    public async Task<bool> IsValidJsonFileAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            JsonDocument.Parse(json).Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

}

