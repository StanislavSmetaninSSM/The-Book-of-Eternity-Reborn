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

    private async Task ValidateDirectChaosSeaGachaOutcomeAsync(string playerAction, List<ValidationIssue> issues)
    {
        if (!playerAction.Contains("[CHAOS_SEA_DIRECT_GACHA]", StringComparison.OrdinalIgnoreCase))
            return;

        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "direct_chaos_gacha_missing_pre_turn_soul_state",
            section: "CHAOS_SEA_DIRECT_GACHA",
            message: "Direct Chaos Sea gacha требует validated pre-turn soul_state snapshot для проверки materialized relic.",
            repairHint: "Сохраняй validated snapshot copy of game_state/meta/soul_state.json перед direct /gacha accepted turn.");
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulJson) || string.IsNullOrWhiteSpace(currentSoulJson))
            return;

        try
        {
            if (JsonNode.Parse(preTurnSoulJson) is not JsonObject preTurnSoulRoot ||
                JsonNode.Parse(currentSoulJson) is not JsonObject currentSoulRoot)
            {
                return;
            }

            var preTurnRealm = GetNodeString(preTurnSoulRoot["currentRealm"]);
            if (!IsExactChaosSeaRealm(preTurnRealm))
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    "Direct Chaos Sea gacha допустим только из точного realm Моря Хаоса.",
                    code: "direct_chaos_gacha_invalid_realm",
                    section: "CHAOS_SEA_DIRECT_GACHA",
                    expected: "Chaos Sea",
                    actual: preTurnRealm ?? "unknown pre-turn realm",
                    repairHint: "Не запускай [CHAOS_SEA_DIRECT_GACHA] из Shining Abode, pending-bootstrap или смертного realm."));
            }

            var costMatch = InkFeatherCostRegex.Match(playerAction);
            if (!costMatch.Success || !int.TryParse(costMatch.Groups[1].Value, out var costInFeathers) || costInFeathers <= 0)
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    "Direct Chaos Sea gacha должен явно фиксировать положительную стоимость в Чернильных Перьях.",
                    code: "direct_chaos_gacha_missing_feather_cost",
                    section: "CHAOS_SEA_DIRECT_GACHA",
                    expected: "positive feather cost in playerAction",
                    actual: "missing or non-positive",
                    repairHint: "Передавай в [CHAOS_SEA_DIRECT_GACHA] точную фразу со списанной стоимостью, например 'тратит 25 Чернильных Перьев'."));
            }

            var preTurnFeathers = CurrentSoulFeathers(preTurnSoulRoot);
            var currentFeathers = CurrentSoulFeathers(currentSoulRoot);
            if (costMatch.Success && int.TryParse(costMatch.Groups[1].Value, out costInFeathers) && costInFeathers > 0)
            {
                var clientSpendAlreadyInSnapshot = await IsClientInkFeatherSpendAlreadyInSnapshotAsync(
                    preTurnFeathers,
                    costInFeathers);
                var expectedFeathers = clientSpendAlreadyInSnapshot
                    ? preTurnFeathers
                    : preTurnFeathers - costInFeathers;
                if (currentFeathers != expectedFeathers)
                {
                    issues.Add(new ValidationIssue(
                        "game_state/meta/soul_state.json.inkFeathers.current",
                        IssueSeverity.Error,
                        "Direct Chaos Sea gacha должен сохранять точный post-spend баланс Чернильных Перьев.",
                        code: "direct_chaos_gacha_feather_balance_mismatch",
                        section: "CHAOS_SEA_DIRECT_GACHA",
                        expected: expectedFeathers.ToString(),
                        actual: currentFeathers.ToString(),
                        repairHint: "После direct /gacha оставь баланс равным pre-turn inkFeathers.current минус заявленная стоимость; не возвращай и не списывай Перья повторно."));
                }
            }

            var preTurnRelicIds = CollectSoulRelicIds(preTurnSoulRoot);
            var currentRelicIds = CollectSoulRelicIds(currentSoulRoot);
            var newRelicIds = currentRelicIds.Except(preTurnRelicIds, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!preTurnRelicIds.IsSubsetOf(currentRelicIds))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Direct Chaos Sea gacha не должна удалять уже существующие Soul Relics.",
                    code: "direct_chaos_gacha_unexpected_existing_relic_removal",
                    section: "CHAOS_SEA_DIRECT_GACHA",
                    repairHint: "При direct /gacha только добавляй новую реликвию; не удаляй и не подменяй существующие Soul Relics."));
            }

            if (newRelicIds.Count != 1)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Direct Chaos Sea gacha должен materialize-ить ровно одну новую Soul Relic.",
                    code: "direct_chaos_gacha_missing_new_relic_materialization",
                    section: "CHAOS_SEA_DIRECT_GACHA",
                    expected: "exactly one new soul relic",
                    actual: newRelicIds.Count == 0 ? "no_new_relics" : string.Join(", ", newRelicIds),
                    repairHint: "Добавь результат direct /gacha в soul_state через metaStateUpdates.soulRelicOperations.addRelic."));
            }
            else
            {
                var newRelicId = newRelicIds.First();
                if (TryFindSoulRelicNode(currentSoulRoot, newRelicId, out var newRelic))
                    ValidateDirectChaosSeaGachaExactRarity(newRelic, newRelicId, issues);
            }
        }
        catch
        {
            // JSON shape issues are reported by normal state validation.
        }
    }

    private async Task ValidateChaosSeaTravelOutcomeAsync(string playerAction, List<ValidationIssue> issues)
    {
        if (!playerAction.Contains("[CHAOS_SEA_TRAVEL]", StringComparison.OrdinalIgnoreCase))
            return;

        var targetAbodeId = ExtractChaosSeaTravelActionValue(playerAction, "targetAbodeId");
        var targetGuardianId = ExtractChaosSeaTravelActionValue(playerAction, "targetGuardianId");
        if (string.IsNullOrWhiteSpace(targetAbodeId) || string.IsNullOrWhiteSpace(targetGuardianId))
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json.playerAction",
                IssueSeverity.Error,
                "CHAOS_SEA_TRAVEL должен явно фиксировать targetAbodeId и targetGuardianId.",
                code: "chaos_sea_travel_missing_target",
                section: "CHAOS_SEA_TRAVEL",
                expected: "targetAbodeId and targetGuardianId in playerAction",
                actual: playerAction,
                repairHint: "Не принимай travel turn без точной цели: playerAction обязан содержать targetAbodeId=<id> и targetGuardianId=<id>."));
            return;
        }

        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "chaos_sea_travel_missing_pre_turn_soul_state",
            section: "CHAOS_SEA_TRAVEL",
            message: "CHAOS_SEA_TRAVEL требует validated pre-turn soul_state snapshot для проверки realm.",
            repairHint: "Сохраняй validated snapshot copy of game_state/meta/soul_state.json перед accepted travel turn.");
        if (!string.IsNullOrWhiteSpace(preTurnSoulJson))
        {
            try
            {
                if (JsonNode.Parse(preTurnSoulJson) is JsonObject preTurnSoulRoot)
                {
                    var preTurnRealm = GetNodeString(preTurnSoulRoot["currentRealm"]);
                    if (!IsExactChaosSeaRealm(preTurnRealm))
                    {
                        issues.Add(new ValidationIssue(
                            "input/turn_request.json.playerAction",
                            IssueSeverity.Error,
                            "CHAOS_SEA_TRAVEL допустим только из точного realm Моря Хаоса.",
                            code: "chaos_sea_travel_invalid_realm",
                            section: "CHAOS_SEA_TRAVEL",
                            expected: "Chaos Sea",
                            actual: preTurnRealm ?? "unknown pre-turn realm",
                            repairHint: "Не используй [CHAOS_SEA_TRAVEL] из Shining Abode, pending-bootstrap или смертного realm."));
                    }
                }
            }
            catch
            {
                // JSON shape issues are reported by normal state validation.
            }
        }

        var preTurnGuardiansJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/guardians.json",
            issues,
            code: "chaos_sea_travel_missing_pre_turn_guardians_state",
            section: "CHAOS_SEA_TRAVEL",
            message: "CHAOS_SEA_TRAVEL требует validated pre-turn guardians snapshot для проверки уже открытой цели.",
            repairHint: "Сохраняй validated snapshot copy of game_state/meta/guardians.json перед accepted travel turn; travel не может сам открывать свою цель.");
        if (!string.IsNullOrWhiteSpace(preTurnGuardiansJson))
        {
            try
            {
                if (JsonNode.Parse(preTurnGuardiansJson) is JsonObject preTurnGuardiansRoot)
                {
                    if (!ContainsString(preTurnGuardiansRoot["chaosSeaNavigation"]?["discoveredAbodes"], targetAbodeId))
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json.chaosSeaNavigation.discoveredAbodes",
                            IssueSeverity.Error,
                            "CHAOS_SEA_TRAVEL должен выбирать targetAbodeId из pre-turn discoveredAbodes.",
                            code: "chaos_sea_travel_target_not_discovered",
                            section: "CHAOS_SEA_TRAVEL",
                            expected: $"pre-turn discoveredAbodes contains {targetAbodeId}",
                            actual: preTurnGuardiansRoot["chaosSeaNavigation"]?["discoveredAbodes"]?.ToJsonString() ?? "missing",
                            repairHint: "Не принимай travel turn к обители, которая не была открыта до хода; сначала нужен отдельный discovery/поиск, затем travel."));
                    }

                    var preTurnTargetGuardian = FindGuardianNode(preTurnGuardiansRoot["guardians"], targetGuardianId);
                    if (preTurnTargetGuardian?["abode"] is not JsonObject preTurnTargetAbode ||
                        preTurnTargetAbode["isDiscovered"]?.GetValueKind() != JsonValueKind.True)
                    {
                        issues.Add(new ValidationIssue(
                            "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json.guardians[].abode.isDiscovered",
                            IssueSeverity.Error,
                            "CHAOS_SEA_TRAVEL требует pre-turn discovered state у target guardian abode.",
                            code: "chaos_sea_travel_target_abode_not_previously_marked_discovered",
                            section: "CHAOS_SEA_TRAVEL",
                            expected: "true before the turn",
                            actual: preTurnTargetGuardian?["abode"]?["isDiscovered"]?.ToJsonString() ?? "missing",
                            repairHint: "Travel может вести только к уже materialized и открытой обители; не выставляй isDiscovered=true впервые в самом travel turn."));
                    }
                }
            }
            catch
            {
                // JSON shape issues are reported by normal state validation.
            }
        }

        var currentGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(currentGuardiansJson))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "CHAOS_SEA_TRAVEL должен materialize-ить guardians.json после перехода.",
                code: "chaos_sea_travel_missing_guardians_state",
                section: "CHAOS_SEA_TRAVEL",
                expected: "game_state/meta/guardians.json with activeGuardian and chaosSeaNavigation",
                actual: "missing or empty",
                repairHint: "После travel turn запиши activeGuardian, guardians[] и chaosSeaNavigation в game_state/meta/guardians.json."));
            return;
        }

        try
        {
            if (JsonNode.Parse(currentGuardiansJson) is not JsonObject root)
                return;

            var activeGuardianId = GetNodeString(root["activeGuardian"]?["guardianId"]) ??
                                   GetNodeString(root["activeGuardian"]?["id"]);
            if (!string.Equals(activeGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.activeGuardian.guardianId",
                    IssueSeverity.Error,
                    "CHAOS_SEA_TRAVEL должен сделать targetGuardianId активным Хранителем.",
                    code: "chaos_sea_travel_active_guardian_mismatch",
                    section: "CHAOS_SEA_TRAVEL",
                    expected: targetGuardianId,
                    actual: activeGuardianId ?? "missing",
                    repairHint: "Синхронно установи activeGuardian.guardianId в targetGuardianId из playerAction."));
            }

            var currentAbodeId = GetNodeString(root["chaosSeaNavigation"]?["currentAbodeId"]);
            if (!string.Equals(currentAbodeId, targetAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.chaosSeaNavigation.currentAbodeId",
                    IssueSeverity.Error,
                    "CHAOS_SEA_TRAVEL должен установить currentAbodeId в targetAbodeId.",
                    code: "chaos_sea_travel_current_abode_mismatch",
                    section: "CHAOS_SEA_TRAVEL",
                    expected: targetAbodeId,
                    actual: currentAbodeId ?? "missing",
                    repairHint: "Синхронно установи chaosSeaNavigation.currentAbodeId в targetAbodeId из playerAction."));
            }

            if (!ContainsString(root["chaosSeaNavigation"]?["discoveredAbodes"], targetAbodeId))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.chaosSeaNavigation.discoveredAbodes",
                    IssueSeverity.Error,
                    "CHAOS_SEA_TRAVEL должен оставлять targetAbodeId в discoveredAbodes.",
                    code: "chaos_sea_travel_target_not_discovered",
                    section: "CHAOS_SEA_TRAVEL",
                    expected: $"discoveredAbodes contains {targetAbodeId}",
                    actual: root["chaosSeaNavigation"]?["discoveredAbodes"]?.ToJsonString() ?? "missing",
                    repairHint: "Добавь targetAbodeId в chaosSeaNavigation.discoveredAbodes; travel не должен вести в неизвестную для навигации обитель."));
            }

            var targetGuardian = FindGuardianNode(root["guardians"], targetGuardianId);
            var targetGuardianAbodeId = GetNodeString(targetGuardian?["abode"]?["abodeId"]) ??
                                        GetNodeString(targetGuardian?["abode"]?["id"]);
            if (targetGuardian == null ||
                !string.Equals(targetGuardianAbodeId, targetAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.guardians",
                    IssueSeverity.Error,
                    "CHAOS_SEA_TRAVEL должен materialize-ить target guardian с target abode.",
                    code: "chaos_sea_travel_target_guardian_abode_mismatch",
                    section: "CHAOS_SEA_TRAVEL",
                    expected: $"{targetGuardianId} with abodeId {targetAbodeId}",
                    actual: targetGuardian == null ? "target guardian missing" : $"abodeId={targetGuardianAbodeId ?? "missing"}",
                    repairHint: "Убедись, что guardians[] содержит targetGuardianId, а его abode.abodeId совпадает с targetAbodeId из playerAction."));
            }

            if (targetGuardian?["abode"] is not JsonObject abode ||
                abode["isDiscovered"]?.GetValueKind() != JsonValueKind.True)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.guardians[].abode.isDiscovered",
                    IssueSeverity.Error,
                    "CHAOS_SEA_TRAVEL требует discovered state у target guardian abode.",
                    code: "chaos_sea_travel_target_abode_not_marked_discovered",
                    section: "CHAOS_SEA_TRAVEL",
                    expected: "true",
                    actual: targetGuardian?["abode"]?["isDiscovered"]?.ToJsonString() ?? "missing",
                    repairHint: "Установи abode.isDiscovered=true у target guardian, иначе travel state расходится с navigation discovery."));
            }
        }
        catch
        {
            // JSON shape issues are reported by normal state validation.
        }
    }

    private static string? ExtractChaosSeaTravelActionValue(string playerAction, string key)
    {
        var match = Regex.Match(
            playerAction,
            $@"\b{Regex.Escape(key)}\s*=\s*(?:'(?<quoted>[^']*)'|""(?<quoted>[^""]*)""|(?<plain>[^,\)\s]+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        var value = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static JsonObject? FindGuardianNode(JsonNode? guardiansNode, string guardianId)
    {
        if (guardiansNode is not JsonArray guardians)
            return null;

        foreach (var item in guardians)
        {
            if (item is not JsonObject guardian)
                continue;

            var currentGuardianId = GetNodeString(guardian["guardianId"]) ?? GetNodeString(guardian["id"]);
            if (string.Equals(currentGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                return guardian;
        }

        return null;
    }

    private static bool ContainsString(JsonNode? node, string expected)
    {
        if (node is not JsonArray array)
            return false;

        foreach (var item in array)
        {
            var value = GetNodeString(item);
            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<bool> IsClientInkFeatherSpendAlreadyInSnapshotAsync(
        int snapshotFeathers,
        int costInFeathers)
    {
        var payload = await LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync();
        if (payload?.RollbackBackups == null ||
            payload.RollbackBackupHashes == null ||
            !payload.RollbackBackups.TryGetValue("game_state/meta/soul_state.json", out var rollbackPath) ||
            !payload.RollbackBackupHashes.TryGetValue("game_state/meta/soul_state.json", out var expectedHash) ||
            string.IsNullOrWhiteSpace(rollbackPath) ||
            string.IsNullOrWhiteSpace(expectedHash) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(rollbackPath))
        {
            return false;
        }

        var rollbackJson = await _fs.ReadFileAsync(rollbackPath);
        if (string.IsNullOrWhiteSpace(rollbackJson) ||
            !string.Equals(ComputeSha256(rollbackJson), expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(rollbackJson) is not JsonObject rollbackSoulRoot)
                return false;

            return CurrentSoulFeathers(rollbackSoulRoot) == snapshotFeathers + costInFeathers;
        }
        catch
        {
            return false;
        }
    }

    private async Task ValidateAfterlifeClientPrepaidInkFeatherBalanceAsync(
        InkFeatherActionContext context,
        List<ValidationIssue> issues)
    {
        if (!context.ParsedCostInFeathers.HasValue || context.ParsedCostInFeathers.Value <= 0)
            return;

        var preTurnSoulJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json");
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(preTurnSoulJson) || string.IsNullOrWhiteSpace(currentSoulJson))
            return;

        try
        {
            if (JsonNode.Parse(preTurnSoulJson) is not JsonObject preTurnSoulRoot ||
                JsonNode.Parse(currentSoulJson) is not JsonObject currentSoulRoot)
            {
                return;
            }

            var preTurnFeathers = CurrentSoulFeathers(preTurnSoulRoot);
            if (!await IsClientInkFeatherSpendAlreadyInSnapshotAsync(preTurnFeathers, context.ParsedCostInFeathers.Value))
                return;

            var currentFeathers = CurrentSoulFeathers(currentSoulRoot);
            var repeatedStructuredSpend = 0;
            if (currentSoulRoot["metaStateUpdates"] is JsonObject metaStateUpdates &&
                metaStateUpdates["inkFeatherChanges"] is JsonObject inkFeatherChanges)
            {
                repeatedStructuredSpend = GetNodeInt(inkFeatherChanges["spend"]);
            }

            var hasRepeatedStructuredSpend = repeatedStructuredSpend > 0;
            if (currentFeathers == preTurnFeathers && !hasRepeatedStructuredSpend)
                return;

            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.inkFeathers.current",
                IssueSeverity.Error,
                "Afterlife Ink Feather action уже был списан клиентом до отправки хода; GM output не должен списывать Чернильные Перья повторно.",
                code: "afterlife_ink_feather_client_prepaid_double_spend",
                section: context.ActionTag,
                expected: preTurnFeathers.ToString(),
                actual: hasRepeatedStructuredSpend
                    ? $"current={currentFeathers}; metaStateUpdates.inkFeatherChanges.spend={repeatedStructuredSpend}"
                    : currentFeathers.ToString(),
                repairHint: "Убери metaStateUpdates.inkFeatherChanges.spend или любой ручной повторный расход для client-prepaid afterlife action; материализуй только promised non-feather outcome."));
        }
        catch
        {
            // JSON shape issues are reported by normal state validation.
        }
    }

    private void ValidateDirectChaosSeaGachaExactRarity(
        JsonObject newRelic,
        string newRelicId,
        List<ValidationIssue> issues)
    {
        var baseRarity = TryReadCurrentTurnGachaBaseRaritySync();
        if (string.IsNullOrWhiteSpace(baseRarity))
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json.gachaBaseResult.baseRarity",
                IssueSeverity.Error,
                "Direct Chaos Sea gacha требует client-computed gachaBaseResult.baseRarity для проверки редкости результата.",
                code: "direct_chaos_gacha_missing_base_rarity",
                section: "CHAOS_SEA_DIRECT_GACHA",
                expected: "Common | Uncommon | Rare | Epic | Legendary",
                actual: "missing",
                repairHint: "Перед direct /gacha клиент должен передать gachaBaseResult.baseRarity; GM не выбирает базовую редкость самостоятельно."));
            return;
        }

        var finalRarity = GetNodeString(newRelic["rarity"]) ?? GetNodeString(newRelic["quality"]);
        if (string.IsNullOrWhiteSpace(finalRarity))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "Direct Chaos Sea gacha должен materialize-ить новую Soul Relic с итоговой редкостью.",
                code: "direct_chaos_gacha_missing_result_rarity",
                section: "CHAOS_SEA_DIRECT_GACHA",
                expected: baseRarity,
                actual: $"new relic {newRelicId} has no rarity/quality",
                repairHint: "Сохрани у новой Soul Relic canonical rarity или quality exactly equal to gachaBaseResult.baseRarity."));
            return;
        }

        var baseRank = GetRarityRank(baseRarity);
        var finalRank = GetRarityRank(finalRarity);
        if (finalRank == 0)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "Direct Chaos Sea gacha должен materialize-ить новую Soul Relic с canonical итоговой редкостью.",
                code: "direct_chaos_gacha_result_rarity_mismatch",
                section: "CHAOS_SEA_DIRECT_GACHA",
                expected: baseRarity,
                actual: finalRarity,
                repairHint: "Для direct /gacha используй exact canonical rarity from gachaBaseResult.baseRarity; unknown rarity values are not valid outcomes."));
            return;
        }

        if (baseRank > 0 &&
            finalRank > 0 &&
            !string.Equals(finalRarity, baseRarity, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "Direct Chaos Sea gacha не имеет пути повышения или понижения редкости: итоговая редкость должна точно совпадать с client-computed gachaBaseResult.baseRarity.",
                code: "direct_chaos_gacha_result_rarity_mismatch",
                section: "CHAOS_SEA_DIRECT_GACHA",
                expected: baseRarity,
                actual: finalRarity,
                repairHint: "Для direct /gacha используй gachaBaseResult.baseRarity как exact final rarity; upgrades допустимы только в Guardian-mediated или Shining banner flow."));
        }
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

        var previousExperience = ReadPrimaryExperienceCounter(await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/player/experience.json"));
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
        var preSkillJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(skillFile);
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
        var previousEffectsJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/player/effects.json");
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
        if (string.IsNullOrWhiteSpace(guardianId) || !reputationChange.HasValue)
            return;

        var preGuardiansSnapshot = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotGuardiansAsync(
            "game_state/meta/guardians.json",
            context.ActionTag,
            "pre-turn guardian reputation baseline",
            issues,
            authorityProofScope: CreateGuardianPowerEventAuthorityScopeForGuardian(guardianId));
        if (preGuardiansSnapshot.Status != GuardianAcceptedTurnSnapshotStatus.Resolved)
            return;

        var previousGuardian = TryReadValidatedGuardianAcceptedTurnSnapshotGuardian(
            preGuardiansSnapshot,
            guardianId,
            "game_state/meta/guardians.json",
            context.ActionTag,
            issues);
        if (!previousGuardian.HasValue || HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            return;

        var previousReputation = TryReadGuardianCurrentReputation(previousGuardian.Value);

        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/guardians.json" });

        if (changedFiles.Count == 0 &&
            !HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "guardian reputation change", "game_state/meta/guardians.json должен реально измениться после DONATE_TO_GUARDIAN.");

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

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!TryEnsureCurrentGuardianAuthorityForPowerEventSensitiveOutcome(guardianPolicyContext, out var currentGuardianAuthorityFailure))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "DONATE_TO_GUARDIAN не может быть доказан: current guardian authority unreadable или unavailable для strict reputation proof.",
                code: "ink_feather_guardian_invalid_current_authority",
                section: context.ActionTag,
                expected: "readable current guardian authority root",
                actual: currentGuardianAuthorityFailure,
                repairHint: "Исправь current game_state/meta/guardians.json, validated guardian baselines и raw guardianPowerEvents так, чтобы kernel построил strict current guardian authority перед DONATE_TO_GUARDIAN proof."));
            return;
        }

        var currentReputation = TryReadGuardianCurrentReputationFromPolicyContext(guardianPolicyContext, guardianId);
        if (!currentReputation.HasValue ||
            !previousReputation.HasValue)
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
            return;
        }

        if (expectedReputationChange.HasValue)
        {
            var actualReputationChange = currentReputation.Value - previousReputation.Value;
            if (actualReputationChange != expectedReputationChange.Value)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json",
                    IssueSeverity.Error,
                    "После DONATE_TO_GUARDIAN репутация Хранителя должна измениться ровно на formula-backed reputationChange",
                    code: "ink_feather_guardian_reputation_delta_mismatch",
                    section: context.ActionTag,
                    expected: $"{previousReputation.Value} + {expectedReputationChange.Value} = {previousReputation.Value + expectedReputationChange.Value}",
                    actual: currentReputation.Value.ToString(),
                    repairHint: "Синхронизируй guardians.json с stateEvidence.reputationChange: currentReputation должен вырасти ровно на min(25, max(15, cost / 3))."));
            }
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

        var previousExperience = await ReadEnlightenmentExperienceAsync(await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json"));
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
        if (string.IsNullOrWhiteSpace(guardianId) || !reputationChange.HasValue)
            return;

        var preGuardiansSnapshot = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotGuardiansAsync(
            "game_state/meta/guardians.json",
            context.ActionTag,
            "pre-turn guardian reputation baseline",
            issues,
            authorityProofScope: CreateGuardianPowerEventAuthorityScopeForGuardian(guardianId));
        if (preGuardiansSnapshot.Status != GuardianAcceptedTurnSnapshotStatus.Resolved)
            return;

        var previousGuardian = TryReadValidatedGuardianAcceptedTurnSnapshotGuardian(
            preGuardiansSnapshot,
            guardianId,
            "game_state/meta/guardians.json",
            context.ActionTag,
            issues);
        if (!previousGuardian.HasValue || HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            return;

        var previousReputation = TryReadGuardianCurrentReputation(previousGuardian.Value);

        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            new[] { "game_state/meta/guardians.json" });

        if (changedFiles.Count == 0 &&
            !HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "guardian reputation change", "game_state/meta/guardians.json должен реально измениться после GUARDIAN_FAVOR.");

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!TryEnsureCurrentGuardianAuthorityForPowerEventSensitiveOutcome(guardianPolicyContext, out var currentGuardianAuthorityFailure))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "GUARDIAN_FAVOR не может быть доказан: current guardian authority unreadable или unavailable для strict reputation proof.",
                code: "ink_feather_guardian_invalid_current_authority",
                section: context.ActionTag,
                expected: "readable current guardian authority root",
                actual: currentGuardianAuthorityFailure,
                repairHint: "Исправь current game_state/meta/guardians.json, validated guardian baselines и raw guardianPowerEvents так, чтобы kernel построил strict current guardian authority перед GUARDIAN_FAVOR proof."));
            return;
        }

        var currentReputation = TryReadGuardianCurrentReputationFromPolicyContext(guardianPolicyContext, guardianId);
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
        if (string.IsNullOrWhiteSpace(guardianId) || !powerGain.HasValue || string.IsNullOrWhiteSpace(returnCycleId))
            return;

        var requestJson = await ReadRequiredValidatedPendingTurnSnapshotFileAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            issues,
            code: "abode_offering_missing_validated_snapshot_request",
            section: context.ActionTag,
            message: "ABODE_OFFERING требует current validated snapshot copy of pending_abode_offering.json; live current request не считается источником истины.",
            repairHint: "Сохраняй pending_abode_offering.json в validated pending turn snapshot и сверяй accepted-turn outcome именно с snapshot copy.");
        if (string.IsNullOrWhiteSpace(requestJson))
            return;

        JsonDocument? requestDoc = null;
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest? request;
        try
        {
            requestDoc = JsonDocument.Parse(requestJson);
            request = JsonSerializer.Deserialize<GuardianAbodeOfferingState.PendingAbodeOfferingRequest>(requestDoc.RootElement.GetRawText());
        }
        catch
        {
            request = null;
        }

        if (request == null)
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeOfferingState.PendingRequestPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot copy pending_abode_offering.json не читается как request contract",
                code: "abode_offering_invalid_validated_snapshot_request",
                section: context.ActionTag,
                expected: "readable validated snapshot request",
                actual: "snapshot request unreadable",
                repairHint: "Сохраняй в validated snapshot корректный JSON request для pending_abode_offering.json и не сверяй ABODE_OFFERING с live current файлом."));
            return;
        }
        using (requestDoc)
        {
            var issueCountBeforeRequestValidation = issues.Count;
            ValidatePendingAbodeOfferingContract(requestDoc.RootElement, GuardianAbodeOfferingState.PendingRequestPath, context.ActionTag, issues);
            if (issues.Count > issueCountBeforeRequestValidation)
                return;
        }

        var preGuardiansSnapshot = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotGuardiansAsync(
            "game_state/meta/guardians.json",
            context.ActionTag,
            "pre-turn guardian abode power baseline",
            issues,
            CreateGuardianPowerEventProofScopeForOffering(request),
            CreateGuardianPowerEventAuthorityScopeForGuardian(request.GuardianId));
        if (preGuardiansSnapshot.Status != GuardianAcceptedTurnSnapshotStatus.Resolved)
            return;

        var preJournalJson = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotFileAsync(
            GuardianPowerEventState.JournalPath,
            context.ActionTag,
            "pre-turn guardian abode power journal",
            issues);
        if (string.IsNullOrWhiteSpace(preJournalJson))
            return;

        var requiresSoulConsumptionProof =
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase);

        string? preSoulJson = null;
        if (requiresSoulConsumptionProof)
        {
            preSoulJson = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotFileAsync(
                "game_state/meta/soul_state.json",
                context.ActionTag,
                "pre-turn offering ownership baseline",
                issues);
            if (string.IsNullOrWhiteSpace(preSoulJson))
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

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase) &&
            context.ParsedCostInFeathers.HasValue &&
            context.ParsedCostInFeathers.Value != request.InkFeathersOffered)
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

        var expectedGain = GuardianAbodeOfferingState.ResolvePowerGainForPendingRequest(request);
        if (expectedGain <= 0)
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeOfferingState.PendingRequestPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot request не задаёт поддерживаемый offering contract с положительным power gain.",
                code: "abode_offering_invalid_validated_snapshot_request",
                section: context.ActionTag,
                expected: "supported offering contract with positive power gain",
                actual: "resolved offering gain <= 0",
                repairHint: "Сохраняй в validated snapshot canonical pending_abode_offering request с whitelisted offeringType и корректными offering fields."));
            return;
        }
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

        var preTurnJournalKnowledgeResult = await ReadValidatedPreTurnGuardianPowerJournalProofKnowledgeAsync(
            CreateGuardianPowerEventProofScopeForOffering(request));
        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotGuardians)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot guardians baseline unreadable или semantically invalid для strict journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_guardians",
                section: context.ActionTag,
                expected: "canonical validated snapshot guardians.json for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot guardians baseline invalid",
                repairHint: "Сохраняй в validated snapshot полный canonical game_state/meta/guardians.json; proof knowledge для ABODE_OFFERING не может строиться из partial или invalid guardian baseline."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot guardian project tracker unreadable или semantically invalid для strict journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_tracker",
                section: context.ActionTag,
                expected: $"canonical validated snapshot {GuardianProjectState.TrackerPath} for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot tracker baseline invalid",
                repairHint: $"Сохраняй в validated snapshot canonical {GuardianProjectState.TrackerPath}; offering proof knowledge не может строиться из partial или invalid tracker baseline."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot abode_power_journal baseline unreadable или semantically invalid для strict journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: context.ActionTag,
                expected: $"canonical validated snapshot {GuardianPowerEventState.JournalPath} for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot journal baseline invalid",
                repairHint: $"Сохраняй в validated snapshot canonical {GuardianPowerEventState.JournalPath}; offering proof knowledge не может строиться из missing, stale или invalid journal baseline."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Knowledge == null)
        {
            issues.Add(new ValidationIssue(
                PendingTurnSnapshotManifestPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot proof context unavailable для strict journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: context.ActionTag,
                expected: "usable validated snapshot proof knowledge context",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot context unavailable",
                repairHint: "Сохраняй current validated pending turn snapshot manifest и canonical guardian/tracker baselines перед strict ABODE_OFFERING proof."));
            return;
        }

        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var journalProof = SummarizeOfferingJournalProof(
            preJournalJson,
            postJournalJson,
            request,
            powerGain.Value,
            preTurnJournalKnowledgeResult.Knowledge,
            "offering",
            powerEventId);
        if (journalProof.Status == OfferingJournalProofStatus.InvalidValidatedBaseline)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть сверен: validated snapshot copy abode_power_journal.json unreadable или malformed.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: context.ActionTag,
                expected: "readable validated pre-turn abode_power_journal baseline",
                actual: "validated snapshot journal unreadable or malformed",
                repairHint: "Сохраняй в validated snapshot корректный JSON journal baseline и не доказывай ABODE_OFFERING через current-only journal."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentGuardianAuthority)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть доказан: current guardian authority unreadable или unavailable для strict journal proof.",
                code: "abode_offering_invalid_current_guardian_authority",
                section: context.ActionTag,
                expected: "readable current guardian authority root",
                actual: journalProof.FailureDescription ?? "current guardian authority unavailable",
                repairHint: "Исправь current game_state/meta/guardians.json и validated guardian baseline так, чтобы kernel построил strict current guardian authority перед ABODE_OFFERING proof."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentTrackerAuthority)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть доказан: current guardian project tracker authority unreadable или unavailable для strict journal proof.",
                code: "abode_offering_invalid_current_tracker_authority",
                section: context.ActionTag,
                expected: $"readable current authority root for {GuardianProjectState.TrackerPath}",
                actual: journalProof.FailureDescription ?? "current tracker authority unavailable",
                repairHint: $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил strict current tracker authority перед ABODE_OFFERING proof."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть доказан: current abode_power_journal.json unreadable или malformed.",
                code: "abode_offering_invalid_current_journal_proof",
                section: context.ActionTag,
                expected: "readable current abode_power_journal proof",
                actual: journalProof.FailureDescription ?? "current journal unreadable or malformed",
                repairHint: "Делай current abode_power_journal.json корректным JSON и materialize offering proof только через читаемый strict journal."));
            return;
        }

        var allowedAffectedFiles = new List<string>
        {
            "game_state/meta/guardians.json",
            GuardianPowerEventState.JournalPath
        };
        if (requiresSoulConsumptionProof)
            allowedAffectedFiles.Add("game_state/meta/soul_state.json");

        var changedFiles = await ValidateAffectedFilesChangedAsync(
            stateEvidence,
            context.ActionTag,
            issues,
            allowedAffectedFiles);

        if (changedFiles.Count == 0 &&
            !HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            AddMissingStateEvidenceIssue(issues, context.ActionTag, "abode power change", $"game_state/meta/guardians.json и {GuardianPowerEventState.JournalPath} должны реально измениться после ABODE_OFFERING.");

        if (requiresSoulConsumptionProof &&
            !HasListedAffectedFile(stateEvidence, "game_state/meta/soul_state.json"))
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                IssueSeverity.Error,
                "ABODE_OFFERING с relic/archive offering должен явно помечать soul_state.json как affected proof surface.",
                code: "abode_offering_missing_soul_state_affected_file",
                section: context.ActionTag,
                expected: "game_state/meta/soul_state.json listed in affectedFiles",
                actual: "soul_state.json missing from affectedFiles",
                repairHint: "Для Soul Relic и archive offering указывай game_state/meta/soul_state.json в stateEvidence.affectedFiles, потому что именно он доказывает consumption."));
        }

        if (journalProof.PreCycleInkFeathers + request.InkFeathersOffered > 150)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING превысил лимит 150 перьев на Хранителя в одном return cycle",
                code: "abode_offering_cycle_cap_exceeded",
                section: context.ActionTag,
                expected: "<= 150 total feathers per guardian per return cycle",
                actual: (journalProof.PreCycleInkFeathers + request.InkFeathersOffered).ToString(),
                repairHint: "Не превышай cap offering-а: максимум 150 Чернильных Перьев на одного Хранителя за одно возвращение."));
        }

        if (journalProof.PostCycleInkFeathers < journalProof.PreCycleInkFeathers + request.InkFeathersOffered)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "ABODE_OFFERING не оставил полного offering audit trail в abode_power_journal.json",
                code: "abode_offering_journal_missing_cycle_amount",
                section: context.ActionTag,
                expected: (journalProof.PreCycleInkFeathers + request.InkFeathersOffered).ToString(),
                actual: journalProof.PostCycleInkFeathers.ToString(),
                repairHint: "Каждое offering power event должно сохранять inkFeathersOffered и returnCycleId в audit journal entry."));
        }

        if (!journalProof.MatchingOfferingEventFound)
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

        var previousGuardian = TryReadValidatedGuardianAcceptedTurnSnapshotGuardian(
            preGuardiansSnapshot,
            guardianId,
            "game_state/meta/guardians.json",
            context.ActionTag,
            issues);
        if (!previousGuardian.HasValue || HasGuardianAcceptedTurnSnapshotContractFailure(issues, context.ActionTag))
            return;

        var previousPower = (int?)AbodePowerRules.GetCurrentPower(previousGuardian.Value);
        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!TryEnsureCurrentGuardianAuthorityForPowerEventSensitiveOutcome(guardianPolicyContext, out var currentGuardianAuthorityFailure))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "ABODE_OFFERING не может быть доказан: current guardian authority unreadable или unavailable для strict power proof.",
                code: "abode_offering_invalid_current_guardian_authority",
                section: context.ActionTag,
                expected: "readable current guardian authority root",
                actual: currentGuardianAuthorityFailure,
                repairHint: "Исправь current game_state/meta/guardians.json, validated guardian baselines и raw guardianPowerEvents так, чтобы kernel построил strict current guardian authority перед ABODE_OFFERING power proof."));
            return;
        }

        var currentPower = TryReadGuardianCurrentAbodePowerFromPolicyContext(guardianPolicyContext, guardianId);
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

        if (requiresSoulConsumptionProof)
        {
            var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            ValidatePendingAbodeOfferingConsumptionProof(
                preSoulJson,
                currentSoulJson,
                request,
                context.ActionTag,
                issues);
        }
    }

    private async Task ValidateSoulImprintOutcomeAsync(JsonElement receiptRoot, InkFeatherActionContext context, List<ValidationIssue> issues)
    {
        ValidateResolutionType(receiptRoot, context.ActionTag, "soulImprint", issues);
        if (!TryGetStateEvidence(receiptRoot, context.ActionTag, issues, out var stateEvidence))
            return;

        var imprintId = GetStringValue(stateEvidence, "imprintId");
        var companionName = GetStringValue(stateEvidence, "companionName");
        var sourceCompanionId = GetFirstString(
            stateEvidence,
            "sourceCompanionId",
            "companionId",
            "sourceCompanionRelicId");
        var sourceNpcId = GetFirstString(
            stateEvidence,
            "NPCId",
            "npcId",
            "sourceNpcId",
            "sourceNPCId");
        if (string.IsNullOrWhiteSpace(sourceCompanionId) && string.IsNullOrWhiteSpace(sourceNpcId))
        {
            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence",
                IssueSeverity.Error,
                "SOUL_IMPRINT receipt должен содержать source companion/NPC provenance, а не только имя imprint.",
                code: "ink_feather_soul_imprint_missing_source_provenance",
                section: context.ActionTag,
                expected: "stateEvidence.sourceCompanionId or stateEvidence.NPCId/sourceNpcId",
                actual: stateEvidence.ToString(),
                repairHint: "Добавь sourceCompanionId/companionId или NPCId/sourceNpcId в stateEvidence и в persisted soulImprint payload."));
        }

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
        else if (!await SoulImprintHasSourceProvenanceAsync(imprintId, companionName, sourceCompanionId, sourceNpcId))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulImprint",
                IssueSeverity.Error,
                "Persisted soulImprint должен сохранять source companion/NPC provenance из receipt.",
                code: "ink_feather_soul_imprint_persisted_source_provenance_missing",
                section: context.ActionTag,
                expected: string.IsNullOrWhiteSpace(sourceCompanionId)
                    ? $"NPCId/sourceNpcId={sourceNpcId}"
                    : $"sourceCompanionId/companionId={sourceCompanionId}",
                actual: "missing or mismatched source provenance",
                repairHint: "Сохрани sourceCompanionId/companionId или NPCId/sourceNpcId внутри soulImprint вместе с summary и traits."));
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
        if (RequiresValidatedGuardianSnapshotForAcceptedTurnInkFeatherAction(actionTag))
            return await ValidateGuardianAffectedFilesChangedAsync(affectedFiles, actionTag, issues, allowedSet);

        var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        if (manifest == null)
        {
            issues.Add(new ValidationIssue(
                PendingTurnSnapshotManifestPath,
                IssueSeverity.Error,
                $"Для проверки {actionTag} требуется current validated pending turn snapshot manifest",
                code: "ink_feather_missing_snapshot_manifest",
                section: actionTag,
                expected: PendingTurnSnapshotManifestPath,
                actual: "missing or invalid validated manifest",
                repairHint: "Accepted-turn validation feather action требует current validated pre-turn snapshot context."));
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

            switch (await DescribeTrackedFileChangeAgainstManifestAsync(manifest, file))
            {
                case ValidatedTrackedFileChangeStatus.Changed:
                    changedFiles.Add(file);
                    break;
                case ValidatedTrackedFileChangeStatus.MissingValidatedBaseline:
                    AddMissingValidatedTrackedBaselineIssue(
                        issues,
                        file,
                        "ink_feather_affected_file_missing_validated_baseline",
                        actionTag,
                        $"Для {actionTag} не удалось строго проверить affected file {file}: validated pre-turn baseline missing.",
                        "При создании pending turn snapshot сохраняй affected file в manifest.Files/snapshotFileHashes и не допускай missing validated baseline для ink-feather proof.");
                    break;
                default:
                    issues.Add(new ValidationIssue(
                        $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                        IssueSeverity.Error,
                        $"Для {actionTag} listed affected file не изменился реально: {file}",
                        code: "ink_feather_affected_file_unchanged",
                        section: actionTag,
                        expected: "changed file",
                        actual: file));
                    break;
            }
        }

        return changedFiles;
    }

    private async Task<List<string>> ValidateGuardianAffectedFilesChangedAsync(
        IReadOnlyList<string> affectedFiles,
        string actionTag,
        List<ValidationIssue> issues,
        ISet<string> allowedFiles)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            issues.Add(new ValidationIssue(
                PendingTurnSnapshotManifestPath,
                IssueSeverity.Error,
                $"Для проверки {actionTag} требуется current validated pending turn snapshot manifest.",
                code: "ink_feather_guardian_action_invalid_validated_snapshot_context",
                section: actionTag,
                expected: "current validated pending turn snapshot manifest",
                actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                repairHint: "Guardian-sensitive accepted-turn actions должны сверяться только с current validated pending turn snapshot. Не используй отсутствующий, stale или изменённый manifest."));
            return new List<string>();
        }

        var changedFiles = new List<string>();
        foreach (var file in affectedFiles)
        {
            if (!allowedFiles.Contains(file))
            {
                issues.Add(new ValidationIssue(
                    $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                    IssueSeverity.Error,
                    $"Для {actionTag} указан недопустимый affected file: {file}",
                    code: "ink_feather_unexpected_affected_file",
                    section: actionTag,
                    expected: string.Join(", ", allowedFiles.OrderBy(x => x)),
                    actual: file));
                continue;
            }

            var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, file);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                issues.Add(new ValidationIssue(
                    file,
                    IssueSeverity.Error,
                    $"Для {actionTag} отсутствует validated snapshot copy of {file}; guardian accepted-turn contract нельзя проверить строго.",
                    code: "ink_feather_guardian_action_missing_validated_snapshot_file",
                    section: actionTag,
                    expected: $"validated snapshot entry for {file}",
                    actual: "manifest.Files/snapshotFileHashes entry is missing or unreadable",
                    repairHint: $"Сохраняй {file} в current validated pending turn snapshot и сверяй {actionTag} с validated snapshot copy, а не с rollback backup или live-only state."));
                continue;
            }

            var current = await _fs.ReadFileAsync(file);
            if (!string.Equals(current ?? string.Empty, snapshotJson, StringComparison.Ordinal))
            {
                changedFiles.Add(file);
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{InkFeatherActionResultPath}.stateEvidence.affectedFiles",
                IssueSeverity.Error,
                $"Для {actionTag} listed affected file не изменился реально: {file}",
                code: "ink_feather_affected_file_unchanged",
                section: actionTag,
                expected: "changed file",
                actual: file));
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

    private static bool HasGuardianAcceptedTurnSnapshotContractFailure(IEnumerable<ValidationIssue> issues, string actionTag)
    {
        foreach (var issue in issues)
        {
            if (!string.Equals(issue.Section, actionTag, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "ink_feather_guardian_action_missing_validated_snapshot_file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "ink_feather_guardian_action_invalid_validated_snapshot_data", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "abode_offering_missing_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_journal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "abode_offering_invalid_validated_snapshot_soul_state", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            ManifestHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private async Task<string?> ReadPreTurnTrackedFileAsync(string relativePath)
    {
        return await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);
    }

    private static bool HasValidatedTrackedSnapshotRegistration(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath)
    {
        return PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(
            manifest,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            new[] { relativePath },
            out _);
    }

    private static bool IsTrackedByValidatedBaseline(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath)
    {
        return (manifest.RollbackBaselineFiles ?? new List<string>())
            .Any(path => string.Equals(path, relativePath, StringComparison.OrdinalIgnoreCase));
    }

    private void AddMissingValidatedTrackedBaselineIssue(
        List<ValidationIssue> issues,
        string filePath,
        string code,
        string section,
        string message,
        string repairHint)
    {
        issues.Add(new ValidationIssue(
            filePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: $"validated pre-turn snapshot baseline for {filePath}",
            actual: "tracked file should have a validated pre-turn snapshot entry, but snapshot file/hash is missing or unreadable",
            repairHint: repairHint));
    }

    private async Task ValidateAcceptedTurnTransientOutputFreshnessAsync(
        string relativePath,
        string section,
        string code,
        string expected,
        string repairHint,
        List<ValidationIssue> issues)
    {
        var preTurnContent = await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);
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

        var preSkillJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(skillPath);
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

        var preJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(npcSkillsPath);
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

    private ValidationPendingTurnSnapshotManifest? LoadValidatedCurrentPendingTurnSnapshotManifestSync()
    {
        var lookup = LoadValidatedPendingTurnSnapshotLookupSync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
            ? lookup.Manifest
            : null;
    }

    private ValidatedPendingTurnSnapshotLookup LoadValidatedPendingTurnSnapshotLookupSync()
    {
        var manifestExists = File.Exists(_fs.ResolvePath(PendingTurnSnapshotManifestPath));
        var manifest = LoadValidationPendingTurnSnapshotManifestSync();
        var authorityJson = ReadPendingTurnSnapshotAuthoritySync();
        if (manifest == null)
        {
            return new ValidatedPendingTurnSnapshotLookup(
                manifestExists ? ValidatedPendingTurnSnapshotStatus.Unusable : ValidatedPendingTurnSnapshotStatus.Missing,
                null);
        }

        return new ValidatedPendingTurnSnapshotLookup(
            IsValidatedPendingTurnSnapshotManifestUsable(manifest, authorityJson)
                ? ValidatedPendingTurnSnapshotStatus.Usable
                : ValidatedPendingTurnSnapshotStatus.Unusable,
            manifest);
    }

    private string? ReadPendingTurnSnapshotAuthoritySync()
    {
        var authorityPath = _fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath);
        if (!File.Exists(authorityPath))
            return null;

        try
        {
            return File.ReadAllText(authorityPath);
        }
        catch
        {
            return null;
        }
    }

    private string? ReadRelativeFileFromWorkspace(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private string? ReadPreTurnTrackedFileSync(string relativePath)
    {
        return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);
    }

    private async Task<string?> ReadValidatedCurrentPreTurnTrackedFileAsync(string relativePath)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null
            ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath)
            : null;
    }

    private string? ReadValidatedCurrentPreTurnTrackedFileSync(string relativePath)
    {
        var lookup = LoadValidatedPendingTurnSnapshotLookupSync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null
            ? ReadValidatedPendingTurnSnapshotFileSync(lookup.Manifest, relativePath)
            : null;
    }

    private bool HasLifecycleAuthorizedCurrentTriggerLifeEndSync()
    {
        return CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(
            ReadCurrentTrackedFileSync("game_state/control/life_transitions.json"),
            CanonicalStateNormalizer.TryReadStrictCurrentRealm(
                ReadValidatedCurrentPreTurnTrackedFileSync("game_state/meta/soul_state.json")),
            CanonicalStateNormalizer.TryReadStrictCurrentRealm(
                ReadCurrentTrackedFileSync("game_state/meta/soul_state.json")));
    }

    private async Task<string?> ReadValidatedPendingTurnSnapshotFileAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath))
        {
            return null;
        }

        if (manifest.SnapshotFileHashes == null ||
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return null;
        }

        var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        var actualSnapshotHash = ComputeSha256(snapshotJson);
        if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
            return null;

        return snapshotJson;
    }

    private string? ReadValidatedPendingTurnSnapshotFileSync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath)
    {
        if (manifest.Files == null ||
            !manifest.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath))
        {
            return null;
        }

        if (manifest.SnapshotFileHashes == null ||
            !manifest.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return null;
        }

        var resolvedSnapshotPath = _fs.ResolvePath(snapshotPath);
        if (!File.Exists(resolvedSnapshotPath))
            return null;

        try
        {
            var snapshotJson = File.ReadAllText(resolvedSnapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotJson))
                return null;

            var actualSnapshotHash = ComputeSha256(snapshotJson);
            if (!string.Equals(actualSnapshotHash, expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
                return null;

            return snapshotJson;
        }
        catch
        {
            return null;
        }
    }

    private string? ReadRelativeWorkspaceFileSync(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ValidatedCurrentPreTurnRealmResolution> ResolveValidatedCurrentPreTurnRealmAsync()
    {
        var context = await ResolveGuardianValidatedSnapshotContextAsync();
        return new ValidatedCurrentPreTurnRealmResolution(context.SnapshotStatus, context.PreTurnRealm);
    }

    private async Task<string?> ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
        string relativePath,
        List<ValidationIssue> issues,
        string code,
        string section,
        string message,
        string repairHint)
    {
        var currentJson = await _fs.ReadFileAsync(relativePath);
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            if (string.IsNullOrWhiteSpace(currentJson))
                return null;

            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                message,
                code: code,
                section: section,
                expected: $"current validated pending turn snapshot manifest with {relativePath}",
                actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                repairHint: repairHint));
            return null;
        }

        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath);
        if (!string.IsNullOrWhiteSpace(snapshotJson))
            return snapshotJson;

        if (string.IsNullOrWhiteSpace(currentJson))
            return null;

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: $"validated snapshot entry for {relativePath}",
            actual: "current request exists but manifest.Files/snapshotFileHashes entry is missing or unreadable",
            repairHint: repairHint));

        return null;
    }

    private async Task<string?> ReadRequiredValidatedPendingTurnSnapshotFileAsync(
        string relativePath,
        List<ValidationIssue> issues,
        string code,
        string section,
        string message,
        string repairHint)
    {
        var manifest = await LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
            relativePath,
            issues,
            code,
            section,
            message,
            repairHint);
        if (manifest == null)
            return null;

        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, relativePath);
        if (!string.IsNullOrWhiteSpace(snapshotJson))
            return snapshotJson;

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: $"validated snapshot entry for {relativePath}",
            actual: "manifest.Files/snapshotFileHashes entry is missing or unreadable",
            repairHint: repairHint));

        return null;
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
        string filePath,
        List<ValidationIssue> issues,
        string code,
        string section,
        string message,
        string repairHint)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null)
            return lookup.Manifest;

        issues.Add(new ValidationIssue(
            filePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: "current validated pending turn snapshot manifest",
            actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
            repairHint: repairHint));
        return null;
    }

    private async Task<string?> ReadRequiredValidatedPendingTurnSnapshotRealmAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string filePath,
        List<ValidationIssue> issues,
        string code,
        string section,
        string message,
        string repairHint)
    {
        var realm = await TryReadValidatedPendingTurnSnapshotRealmAsync(manifest);
        if (!string.IsNullOrWhiteSpace(realm))
            return realm;

        issues.Add(new ValidationIssue(
            filePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: "validated snapshot game_state/meta/soul_state.json with canonical currentRealm",
            actual: "validated snapshot game_state/meta/soul_state.json is missing, unreadable, or lacks canonical currentRealm",
            repairHint: repairHint));
        return null;
    }

    private async Task<string?> LoadRequiredValidatedCurrentPreTurnRealmAsync(
        string filePath,
        List<ValidationIssue> issues,
        string code,
        string section,
        string message,
        string repairHint)
    {
        var manifest = await LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
            filePath,
            issues,
            code,
            section,
            message,
            repairHint);
        if (manifest == null)
            return null;

        return await ReadRequiredValidatedPendingTurnSnapshotRealmAsync(
            manifest,
            filePath,
            issues,
            code,
            section,
            message,
            repairHint);
    }

    private sealed record PendingTurnRequestValidationContext(
        string SessionId,
        string RequestId,
        int TurnNumber);

    private enum ValidatedPendingTurnSnapshotStatus
    {
        Missing,
        Unusable,
        Usable
    }

    private sealed record ValidatedPendingTurnSnapshotLookup(
        ValidatedPendingTurnSnapshotStatus Status,
        ValidationPendingTurnSnapshotManifest? Manifest);

    private sealed record ValidatedCurrentPreTurnRealmResolution(
        ValidatedPendingTurnSnapshotStatus SnapshotStatus,
        string? Realm);

    private enum ValidatedTrackedFileChangeStatus
    {
        Changed,
        Unchanged,
        MissingValidatedBaseline
    }

    private sealed record GuardianValidatedSnapshotContext(
        ValidatedPendingTurnSnapshotStatus SnapshotStatus,
        ValidationPendingTurnSnapshotManifest? Manifest,
        string? PreTurnRealm);

    private enum GuardianAcceptedTurnSnapshotStatus
    {
        MissingValidatedSnapshot,
        InvalidValidatedSnapshot,
        Resolved
    }

    private enum GuardianSnapshotProofFailureKind
    {
        Guardians,
        Journal,
        Tracker
    }

    private enum SnapshotTrackerGuardianEffectDependencyStatus
    {
        NoTrackerDependency,
        TrackerRequiredAndResolved,
        MissingValidatedSnapshotTracker,
        InvalidValidatedSnapshotTracker
    }

    private sealed record GuardianAcceptedTurnSnapshotReadResult(
        GuardianAcceptedTurnSnapshotStatus Status,
        Dictionary<string, JsonElement>? GuardiansById);

    private enum GuardianPowerJournalProofKnowledgeStatus
    {
        MissingValidatedSnapshotContext,
        InvalidValidatedSnapshotGuardians,
        InvalidValidatedSnapshotJournal,
        InvalidValidatedSnapshotTracker,
        Resolved
    }

    private sealed record GuardianPowerJournalProofKnowledge(
        HashSet<string> KnownGuardianIds,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> KnownPoliticalProjects);

    private readonly record struct GuardianPowerEventProofScope(
        string? ReasonType,
        string? GuardianId,
        string? OfferingType,
        string? ReturnCycleId,
        int? InkFeathersOffered,
        string? RelicId,
        string? ArchiveId,
        bool GuardianBaselineScope);

    private static GuardianPowerEventProofScope CreateGuardianPowerEventProofScopeForReasonType(string reasonType)
        => new(
            reasonType,
            GuardianId: null,
            OfferingType: null,
            ReturnCycleId: null,
            InkFeathersOffered: null,
            RelicId: null,
            ArchiveId: null,
            GuardianBaselineScope: false);

    private static GuardianPowerEventProofScope CreateGuardianPowerEventProofScopeForOffering(
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request)
        => new(
            "offering",
            request.GuardianId,
            request.OfferingType,
            request.ReturnCycleId,
            request.InkFeathersOffered,
            request.RelicId,
            request.ArchiveId,
            GuardianBaselineScope: false);

    private static GuardianPowerEventProofScope CreateGuardianPowerEventAuthorityScopeForGuardian(string guardianId)
        => new(
            ReasonType: null,
            GuardianId: guardianId,
            OfferingType: null,
            ReturnCycleId: null,
            InkFeathersOffered: null,
            RelicId: null,
            ArchiveId: null,
            GuardianBaselineScope: true);

    private static GuardianPowerEventProofScope CreateGuardianPowerEventAuthorityScopeForAllGuardians()
        => new(
            ReasonType: null,
            GuardianId: null,
            OfferingType: null,
            ReturnCycleId: null,
            InkFeathersOffered: null,
            RelicId: null,
            ArchiveId: null,
            GuardianBaselineScope: true);

    private static string? GetGuardianPowerEventProofScopeReasonType(GuardianPowerEventProofScope? proofScope)
        => proofScope?.ReasonType;

    private sealed record GuardianPowerJournalProofKnowledgeReadResult(
        GuardianPowerJournalProofKnowledgeStatus Status,
        GuardianPowerJournalProofKnowledge? Knowledge,
        string? FailureDescription);

    private enum PendingResolutionContractStatus
    {
        NoPreTurnContract,
        MissingValidatedSnapshot,
        InvalidValidatedSnapshot,
        Resolved
    }

    private sealed record PendingResolutionContractReadResult<TContract>(
        PendingResolutionContractStatus Status,
        TContract? Contract)
        where TContract : class;

    private readonly record struct StrictOfferingJournalEntry(
        string EventId,
        string GuardianId,
        int Delta,
        string OfferingType,
        string ReturnCycleId,
        int InkFeathersOffered,
        string? RelicId,
        string? RelicName,
        string? RelicRarity,
        string? ArchiveId,
        string? ArchiveTitle,
        string? ArchiveEntryType,
        string? ArchiveRarity);

    private enum OfferingJournalProofStatus
    {
        Resolved,
        InvalidValidatedBaseline,
        InvalidCurrentGuardianAuthority,
        InvalidCurrentTrackerAuthority,
        InvalidCurrentJournal
    }

    private readonly record struct OfferingJournalProofSummary(
        OfferingJournalProofStatus Status,
        bool MatchingOfferingEventFound,
        int PreCycleInkFeathers,
        int PostCycleInkFeathers,
        string? FailureDescription = null);

    private enum GuardianPowerJournalCurrentProofStatus
    {
        Resolved,
        InvalidCurrentGuardianAuthority,
        InvalidCurrentTrackerAuthority,
        InvalidCurrentJournal
    }

    private sealed record GuardianPowerJournalCurrentProofReadResult(
        GuardianPowerJournalCurrentProofStatus Status,
        List<JsonElement>? Entries,
        string? FailureDescription);

    private enum SoulStateEntryPresenceStatus
    {
        Present,
        Absent,
        InvalidShape,
        Unreadable
    }

    private sealed record SoulRelicProofEntry(
        string RelicId,
        string RelicName,
        string RelicRarity);

    private sealed record ArchiveProofEntry(
        string ArchiveId,
        string ArchiveTitle,
        string ArchiveEntryType,
        string ArchiveRarity);

    private readonly record struct SoulRelicProofReadResult(
        SoulStateEntryPresenceStatus Status,
        SoulRelicProofEntry? Entry);

    private readonly record struct ArchiveProofReadResult(
        SoulStateEntryPresenceStatus Status,
        ArchiveProofEntry? Entry);

    private sealed record GuardianPowerJournalIdentityState(
        HashSet<string> EventIds,
        HashSet<string> ResonanceLifeScopeKeys);

    private enum GuardianPowerJournalIdentityBaselineStatus
    {
        Resolved,
        MissingValidatedSnapshotJournal,
        InvalidValidatedSnapshotJournal
    }

    private sealed record GuardianPowerJournalIdentityBaselineResolution(
        GuardianPowerJournalIdentityBaselineStatus Status,
        GuardianPowerJournalIdentityState? IdentityState,
        string FailureDescription);

    private PendingTurnRequestValidationContext? LoadPendingTurnRequestValidationContextSync(string requestPath)
    {
        if (!File.Exists(requestPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(requestPath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("turnNumber", out var turnNode) ||
                turnNode.ValueKind != JsonValueKind.Number ||
                !turnNode.TryGetInt32(out var turnNumber))
            {
                return null;
            }

            var sessionId = GetFirstNonEmptyString(doc.RootElement, "sessionId") ?? string.Empty;
            var requestId = GetFirstNonEmptyString(doc.RootElement, "requestId") ?? string.Empty;
            return new PendingTurnRequestValidationContext(sessionId, requestId, turnNumber);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ValidationPendingTurnSnapshotManifest?> LoadValidatedCurrentPendingTurnSnapshotManifestAsync()
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
            ? lookup.Manifest
            : null;
    }

    private async Task<GuardianValidatedSnapshotContext> ResolveGuardianValidatedSnapshotContextAsync()
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
            return new GuardianValidatedSnapshotContext(ValidatedPendingTurnSnapshotStatus.Missing, null, null);

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return new GuardianValidatedSnapshotContext(ValidatedPendingTurnSnapshotStatus.Unusable, lookup.Manifest, null);

        var snapshotRealm = await TryReadValidatedPendingTurnSnapshotRealmAsync(lookup.Manifest);
        if (string.IsNullOrWhiteSpace(snapshotRealm))
            return new GuardianValidatedSnapshotContext(ValidatedPendingTurnSnapshotStatus.Unusable, lookup.Manifest, null);

        return new GuardianValidatedSnapshotContext(ValidatedPendingTurnSnapshotStatus.Usable, lookup.Manifest, snapshotRealm);
    }

    private async Task<string?> TryReadValidatedPendingTurnSnapshotRealmAsync(ValidationPendingTurnSnapshotManifest manifest)
    {
        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("currentRealm", out var realm) &&
                realm.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(realm.GetString()))
            {
                return realm.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool RequiresValidatedGuardianSnapshotForAcceptedTurnInkFeatherAction(string actionTag) =>
        string.Equals(actionTag, "DONATE_TO_GUARDIAN", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actionTag, "GUARDIAN_FAVOR", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actionTag, GuardianAbodeOfferingState.ActionTag, StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ResolveAcceptedTurnInkFeatherRealmAsync(
        InkFeatherActionContext actionContext,
        List<ValidationIssue> issues)
    {
        if (!RequiresValidatedGuardianSnapshotForAcceptedTurnInkFeatherAction(actionContext.ActionTag))
        {
            var manifest = await LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
                "input/turn_request.json.playerAction",
                issues,
                code: "ink_feather_action_missing_validated_snapshot_context",
                section: "INK_FEATHER_ACTION",
                message: $"INK_FEATHER_ACTION {actionContext.ActionTag} требует current validated pending turn snapshot manifest.",
                repairHint: "Для accepted-turn INK_FEATHER_ACTION сохраняй current validated pending turn snapshot и не опирайся на отсутствующий или tampered manifest.");
            if (manifest == null)
                return null;

            return await ReadRequiredValidatedPendingTurnSnapshotRealmAsync(
                manifest,
                "input/turn_request.json.playerAction",
                issues,
                code: "ink_feather_action_invalid_validated_snapshot_realm",
                section: "INK_FEATHER_ACTION",
                message: $"INK_FEATHER_ACTION {actionContext.ActionTag} требует validated pre-turn realm из snapshot soul_state.",
                repairHint: "Для accepted-turn INK_FEATHER_ACTION сохраняй validated snapshot copy of game_state/meta/soul_state.json с canonical currentRealm.");
        }

        var snapshotContext = await ResolveGuardianValidatedSnapshotContextAsync();
        if (snapshotContext.SnapshotStatus == ValidatedPendingTurnSnapshotStatus.Usable &&
            !string.IsNullOrWhiteSpace(snapshotContext.PreTurnRealm))
        {
            return snapshotContext.PreTurnRealm;
        }

        issues.Add(new ValidationIssue(
            "input/turn_request.json.playerAction",
            IssueSeverity.Error,
            $"Guardian-sensitive INK_FEATHER_ACTION {actionContext.ActionTag} требует current validated pending turn snapshot pre-turn realm.",
            code: "ink_feather_guardian_action_invalid_validated_snapshot_context",
            section: "INK_FEATHER_ACTION",
            expected: "current validated pending turn snapshot with game_state/meta/soul_state.json",
            actual: DescribeValidatedPendingTurnSnapshotStatus(snapshotContext.SnapshotStatus),
            repairHint: "Для DONATE_TO_GUARDIAN, GUARDIAN_FAVOR и ABODE_OFFERING сохраняй current validated pending turn snapshot с game_state/meta/soul_state.json и не используй отсутствующий или stale snapshot."));
        return null;
    }

    private async Task<string?> ReadRequiredValidatedGuardianAcceptedTurnSnapshotFileAsync(
        string relativePath,
        string actionTag,
        string purpose,
        List<ValidationIssue> issues)
    {
        return await ReadRequiredValidatedPendingTurnSnapshotFileAsync(
            relativePath,
            issues,
            code: "ink_feather_guardian_action_missing_validated_snapshot_file",
            section: actionTag,
            message: $"Для {actionTag} требуется validated snapshot copy of {relativePath}; без неё нельзя строго проверить {purpose}.",
            repairHint: $"Сохраняй {relativePath} в current validated pending turn snapshot и сверяй {actionTag} с validated snapshot copy, а не с rollback backup или live-only state.");
    }

    private async Task<GuardianAcceptedTurnSnapshotReadResult> ReadRequiredValidatedGuardianAcceptedTurnSnapshotGuardiansAsync(
        string relativePath,
        string actionTag,
        string purpose,
        List<ValidationIssue> issues,
        GuardianPowerEventProofScope? proofScope = null,
        GuardianPowerEventProofScope? authorityProofScope = null)
    {
        var json = await ReadRequiredValidatedGuardianAcceptedTurnSnapshotFileAsync(
            relativePath,
            actionTag,
            purpose,
            issues);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GuardianAcceptedTurnSnapshotReadResult(
                GuardianAcceptedTurnSnapshotStatus.MissingValidatedSnapshot,
                null);
        }

        try
        {
            string? trackerJson = null;
            var hasTrackerSnapshotEntry = false;
            string? journalJson = null;
            string? soulStateJson = null;
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
            if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null)
            {
                hasTrackerSnapshotEntry = lookup.Manifest.Files != null &&
                                          lookup.Manifest.Files.ContainsKey(GuardianProjectState.TrackerPath);
                trackerJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, GuardianProjectState.TrackerPath);
                journalJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, GuardianPowerEventState.JournalPath);
                soulStateJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, "game_state/meta/soul_state.json");
            }

            if (TryReadCanonicalGuardianSnapshotForProof(
                    json,
                    relativePath,
                    trackerJson,
                    hasTrackerSnapshotEntry,
                    $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                    journalJson,
                    $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}",
                    soulStateJson,
                    proofScope,
                    authorityProofScope,
                    out _,
                    out var guardiansById,
                    out _,
                    out var failureDescription))
            {
                return new GuardianAcceptedTurnSnapshotReadResult(
                    GuardianAcceptedTurnSnapshotStatus.Resolved,
                    guardiansById);
            }

            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                $"Для {actionTag} validated snapshot copy {relativePath} readable, но canonical guardian baseline semantically invalid.",
                code: "ink_feather_guardian_action_invalid_validated_snapshot_data",
                section: actionTag,
                expected: "canonical guardian root baseline satisfying the full guardian contract",
                actual: failureDescription,
                repairHint: $"Сохраняй в validated snapshot полный canonical {relativePath}; partial или semantically invalid guardian root baseline не может authorizовать {actionTag}."));
            return new GuardianAcceptedTurnSnapshotReadResult(
                GuardianAcceptedTurnSnapshotStatus.InvalidValidatedSnapshot,
                null);
        }
        catch (JsonException)
        {
            // fail-closed below with explicit snapshot issue
        }

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            $"Для {actionTag} validated snapshot copy {relativePath} unreadable или malformed; strict guardian baseline нельзя проверить.",
            code: "ink_feather_guardian_action_invalid_validated_snapshot_data",
            section: actionTag,
            expected: $"validated snapshot object with guardians[] for {relativePath}",
            actual: "validated snapshot JSON unreadable or missing guardians[] array",
            repairHint: $"Сохраняй в validated snapshot корректный canonical {relativePath} с guardians[] и сверяй {actionTag} только с читаемым guardian baseline."));
        return new GuardianAcceptedTurnSnapshotReadResult(
            GuardianAcceptedTurnSnapshotStatus.InvalidValidatedSnapshot,
            null);
    }

    private JsonElement? TryReadValidatedGuardianAcceptedTurnSnapshotGuardian(
        GuardianAcceptedTurnSnapshotReadResult snapshot,
        string guardianId,
        string relativePath,
        string actionTag,
        List<ValidationIssue> issues)
    {
        if (snapshot.Status != GuardianAcceptedTurnSnapshotStatus.Resolved ||
            snapshot.GuardiansById == null)
        {
            return null;
        }

        if (snapshot.GuardiansById.TryGetValue(guardianId, out var guardian))
            return guardian.Clone();

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            $"Для {actionTag} validated snapshot copy {relativePath} не содержит guardian baseline для {guardianId}, относительно которого доказывается outcome.",
            code: "ink_feather_guardian_action_invalid_validated_snapshot_data",
            section: actionTag,
            expected: $"canonical guardian baseline entry for {guardianId}",
            actual: $"guardianId {guardianId} missing from validated snapshot guardians[]",
            repairHint: $"Сохраняй в validated snapshot canonical guardians[] entry для {guardianId}; readable snapshot без target guardian не является valid proof context для {actionTag}."));
        return null;
    }

    private bool TryReadCanonicalGuardianSnapshotForProof(
        string? guardiansJson,
        string contextPrefix,
        string? trackerJson,
        bool hasTrackerSnapshotEntry,
        string trackerContextPrefix,
        string? journalJson,
        string journalContextPrefix,
        string? soulStateJson,
        GuardianPowerEventProofScope? proofScope,
        GuardianPowerEventProofScope? authorityProofScope,
        out JsonElement authorityRoot,
        out Dictionary<string, JsonElement> guardiansById,
        out GuardianSnapshotProofFailureKind failureKind,
        out string failureDescription)
    {
        authorityRoot = default;
        failureKind = GuardianSnapshotProofFailureKind.Guardians;
        guardiansById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (!TryReadCanonicalGuardianSnapshotStateForProof(
                guardiansJson,
                contextPrefix,
                out var guardianRoot,
                out var rawGuardiansById,
                out var commandAuthorizationResult,
                out failureDescription))
        {
            return false;
        }

        var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
        var guardianRootObject = TryParseJsonObject(guardianRoot);
        if (guardianRootObject == null)
        {
            failureDescription = "validated snapshot guardian root cannot be materialized into authority";
            return false;
        }

        var authorizedCreateObjects = BuildGuardianCreateObjectsForSnapshotProof(commandAuthorizationResult);
        var baseAuthorityRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
            guardianRootObject.DeepClone().AsObject(),
            guardianRootObject.DeepClone().AsObject(),
            commandAuthorizationResult.AuthorizedCommands,
            authorizedCreateObjects,
            authorizedPowerEvents: null,
            currentTurn);
        var baseAuthorityElement = CloneJsonObjectToElement(baseAuthorityRoot);
        var baseGuardiansById = BuildGuardiansByIdFromAuthorityRoot(baseAuthorityElement);
        var effectiveAuthorityProofScope = authorityProofScope ?? proofScope;
        var proofRelevantPowerEvents = new List<(JsonElement Entry, string Context)>();
        var authorityRelevantPowerEvents = new List<(JsonElement Entry, string Context)>();
        var hasAuthorityRelevantPowerEvents = false;

        if (guardianRoot.TryGetProperty("guardianPowerEvents", out var powerEvents) &&
            powerEvents.ValueKind != JsonValueKind.Null)
        {
            if (powerEvents.ValueKind != JsonValueKind.Array)
            {
                failureDescription = $"{contextPrefix}.guardianPowerEvents must be an array when the property is present";
                failureKind = GuardianSnapshotProofFailureKind.Guardians;
                return false;
            }

            if (HasNonEmptyGuardianPowerEventArray(powerEvents))
            {
                if (!TryCollectGuardianPowerEventEntriesForProof(
                        powerEvents,
                        $"{contextPrefix}.guardianPowerEvents",
                        proofScope,
                        out proofRelevantPowerEvents,
                        out var proofSelectionFailureDescription))
                {
                    failureDescription = proofSelectionFailureDescription;
                    failureKind = GuardianSnapshotProofFailureKind.Guardians;
                    return false;
                }

                if (Nullable.Equals(effectiveAuthorityProofScope, proofScope))
                {
                    authorityRelevantPowerEvents = proofRelevantPowerEvents;
                }
                else if (!TryCollectGuardianPowerEventEntriesForProof(
                             powerEvents,
                             $"{contextPrefix}.guardianPowerEvents",
                             effectiveAuthorityProofScope,
                             out authorityRelevantPowerEvents,
                             out var authoritySelectionFailureDescription))
                {
                    failureDescription = authoritySelectionFailureDescription;
                    failureKind = GuardianSnapshotProofFailureKind.Guardians;
                    return false;
                }

                hasAuthorityRelevantPowerEvents = authorityRelevantPowerEvents.Count > 0;
            }
        }

        var requiresTrackerGuardianMaterialization = false;
        var trackerGuardianMaterializationStatus = ResolveSnapshotTrackerGuardianEffectDependency(
            trackerJson,
            hasTrackerSnapshotEntry,
            authorityProofScope,
            out requiresTrackerGuardianMaterialization,
            out var trackerGuardianMaterializationFailureDescription);
        if (trackerGuardianMaterializationStatus == SnapshotTrackerGuardianEffectDependencyStatus.MissingValidatedSnapshotTracker ||
            trackerGuardianMaterializationStatus == SnapshotTrackerGuardianEffectDependencyStatus.InvalidValidatedSnapshotTracker)
        {
            failureDescription = trackerGuardianMaterializationFailureDescription;
            failureKind = GuardianSnapshotProofFailureKind.Tracker;
            return false;
        }
        if (!hasAuthorityRelevantPowerEvents && !requiresTrackerGuardianMaterialization)
        {
            authorityRoot = baseAuthorityElement;
            guardiansById = baseGuardiansById;
            return true;
        }

        GuardianPowerJournalIdentityState? snapshotJournalIdentityState = null;
        if (hasAuthorityRelevantPowerEvents &&
            !TryReadGuardianPowerJournalIdentityStateForProof(
                journalJson,
                journalContextPrefix,
                out snapshotJournalIdentityState,
                out var journalFailureDescription))
        {
            failureDescription =
                $"guardianPowerEvents inside validated snapshot guardians.json require canonical validated snapshot journal baseline: {journalFailureDescription}";
            failureKind = GuardianSnapshotProofFailureKind.Journal;
            return false;
        }

        var knownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        var requiresTrackerProofKnowledgeForRawEvents =
            hasAuthorityRelevantPowerEvents &&
            GuardianPowerEventEntriesRequireProjectTrackerAuthority(authorityRelevantPowerEvents);

        if ((requiresTrackerProofKnowledgeForRawEvents || requiresTrackerGuardianMaterialization) &&
            !TryReadCanonicalGuardianProjectTrackerSnapshotForProof(
                trackerJson,
                trackerContextPrefix,
                soulStateJson,
                baseAuthorityElement,
                baseGuardiansById,
                out _,
                out _,
                out _,
                out knownPoliticalProjects,
                out var trackerFailureDescription))
        {
            failureDescription =
                $"validated snapshot guardian baseline requires canonical tracker materialization: {trackerFailureDescription}";
            failureKind = GuardianSnapshotProofFailureKind.Tracker;
            return false;
        }

        var authorityAfterRawElement = baseAuthorityElement;
        var guardiansAfterRawById = baseGuardiansById;
        if (hasAuthorityRelevantPowerEvents)
        {
            var scratchIssues = new List<ValidationIssue>();
            ValidateGuardianPowerEventEntriesAgainstKnownContext(
                authorityRelevantPowerEvents,
                scratchIssues,
                new HashSet<string>(baseGuardiansById.Keys, StringComparer.OrdinalIgnoreCase),
                knownPoliticalProjects,
                snapshotJournalIdentityState);
            if (scratchIssues.Count != 0)
            {
                failureDescription = DescribeGuardianAcceptedTurnSnapshotBaselineFailure(scratchIssues);
                failureKind = GuardianSnapshotProofFailureKind.Guardians;
                return false;
            }

            var powerEventAuthorizationContext = BuildGuardianSnapshotPowerEventAuthorizationContext(baseAuthorityElement, baseGuardiansById);
            if (!TryBuildAuthorizedGuardianPowerEventsForSnapshotProof(
                    authorityRelevantPowerEvents,
                    powerEventAuthorizationContext,
                    knownPoliticalProjects,
                    snapshotJournalIdentityState,
                    out var authorizedPowerEvents,
                    out var powerEventAuthorizationFailureDescription))
            {
                failureDescription = powerEventAuthorizationFailureDescription;
                failureKind = GuardianSnapshotProofFailureKind.Guardians;
                return false;
            }

            var authorityAfterRawRoot = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
                baseAuthorityRoot.DeepClone().AsObject(),
                guardianRootObject.DeepClone().AsObject(),
                authorizedCommands: null,
                authorizedCreateGuardiansById: null,
                authorizedPowerEvents,
                currentTurn);
            authorityAfterRawElement = CloneJsonObjectToElement(authorityAfterRawRoot);
            guardiansAfterRawById = BuildGuardiansByIdFromAuthorityRoot(authorityAfterRawElement);
        }

        if (!requiresTrackerGuardianMaterialization)
        {
            authorityRoot = authorityAfterRawElement;
            guardiansById = guardiansAfterRawById;
            return true;
        }

        if (!TryReadCanonicalGuardianProjectTrackerSnapshotForProof(
                trackerJson,
                trackerContextPrefix,
                soulStateJson,
                authorityAfterRawElement,
                guardiansAfterRawById,
                out _,
                out var guardianAuthorityAfterTracker,
                out var guardiansAfterTrackerById,
                out _,
                out var trackerAuthorityFailureDescription))
        {
            failureDescription =
                $"validated snapshot tracker side effects cannot be materialized into guardian baseline: {trackerAuthorityFailureDescription}";
            failureKind = GuardianSnapshotProofFailureKind.Tracker;
            return false;
        }

        authorityRoot = guardianAuthorityAfterTracker;
        guardiansById = guardiansAfterTrackerById;
        return true;
    }

    private bool TryReadCanonicalGuardianSnapshotStateForProof(
        string? guardiansJson,
        string contextPrefix,
        out JsonElement guardianRoot,
        out Dictionary<string, JsonElement> guardiansById,
        out GuardianCommandAuthorizationResult commandAuthorizationResult,
        out string failureDescription)
    {
        guardianRoot = default;
        guardiansById = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        commandAuthorizationResult = new GuardianCommandAuthorizationResult();
        failureDescription = "validated snapshot JSON unreadable or missing guardians[] array";
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("guardians", out var guardians) ||
                guardians.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var scratchIssues = new List<ValidationIssue>();
            var guardiansByIdWithContext = ValidateGuardianCanonicalRootStructureForProof(
                doc.RootElement,
                contextPrefix,
                scratchIssues);
            if (scratchIssues.Count == 0)
            {
                var snapshotPolicyContext = BuildGuardianSnapshotProofPolicyContext(doc.RootElement, guardiansByIdWithContext);
                commandAuthorizationResult = AuthorizeGuardianCommandsForPolicy(
                    doc.RootElement,
                    contextPrefix,
                    scratchIssues,
                    snapshotPolicyContext);
            }

            if (scratchIssues.Count != 0)
            {
                failureDescription = DescribeGuardianAcceptedTurnSnapshotBaselineFailure(scratchIssues);
                return false;
            }

            guardianRoot = doc.RootElement.Clone();
            guardiansById = guardiansByIdWithContext.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Guardian.Clone(),
                StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private GuardianPolicyContext BuildGuardianSnapshotProofPolicyContext(
        JsonElement root,
        IReadOnlyDictionary<string, (JsonElement Guardian, string Context)> guardiansByIdWithContext)
    {
        var context = new GuardianPolicyContext
        {
            CurrentStateReadable = true,
            HasPreTurnRoot = true,
            PreTurnRoot = root.Clone(),
            HasProofLocalCommandAuthorizationBaselineRoot = true,
            ProofLocalCommandAuthorizationBaselineRoot = root.Clone(),
            PreTurnGuardiansSnapshot = new(
                ValidatedPendingTurnSnapshotStatus.Usable,
                GuardianTrackedSnapshotFileStatus.Usable,
                null,
                root.GetRawText())
        };

        foreach (var (guardianId, guardianWithContext) in guardiansByIdWithContext)
            context.PreTurnGuardiansById[guardianId] = guardianWithContext.Guardian.Clone();

        return context;
    }

    private static IReadOnlyDictionary<string, JsonObject> BuildGuardianCreateObjectsForSnapshotProof(
        GuardianCommandAuthorizationResult commandAuthorizationResult)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var (guardianId, guardian) in commandAuthorizationResult.AuthorizedCreateGuardiansById)
        {
            if (guardian.ValueKind != JsonValueKind.Object)
                continue;

            var guardianObject = TryParseJsonObject(guardian);
            if (guardianObject != null)
                result[guardianId] = guardianObject;
        }

        return result;
    }

    private static Dictionary<string, JsonElement> BuildGuardiansByIdFromAuthorityRoot(JsonElement authorityRoot)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (authorityRoot.ValueKind != JsonValueKind.Object ||
            !authorityRoot.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(guardianId))
                result[guardianId] = guardian.Clone();
        }

        return result;
    }

    private static GuardianPolicyContext BuildGuardianSnapshotPowerEventAuthorizationContext(
        JsonElement authorityRoot,
        IReadOnlyDictionary<string, JsonElement> guardiansById)
    {
        var context = new GuardianPolicyContext
        {
            HasStrictCurrentAuthorityRoot = true,
            StrictCurrentAuthorityRoot = authorityRoot.Clone()
        };

        foreach (var guardianId in guardiansById.Keys)
            context.AuthoritativeGuardianIds.Add(guardianId);

        return context;
    }

    private bool TryBuildAuthorizedGuardianPowerEventsForSnapshotProof(
        IReadOnlyList<(JsonElement Entry, string Context)> events,
        GuardianPolicyContext guardianPolicyContext,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        GuardianPowerJournalIdentityState? preTurnJournalIdentityState,
        out List<JsonObject> authorizedPowerEvents,
        out string failureDescription)
    {
        authorizedPowerEvents = new List<JsonObject>();
        failureDescription = "validated snapshot guardianPowerEvents cannot be authorized into strict snapshot guardian authority";

        var seenEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenResonanceLifeScopeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preTurnJournalIdentityState != null)
        {
            seenEventIds.UnionWith(preTurnJournalIdentityState.EventIds);
            seenResonanceLifeScopeKeys.UnionWith(preTurnJournalIdentityState.ResonanceLifeScopeKeys);
        }

        foreach (var (entry, _) in events)
        {
            if (!TryAuthorizeGuardianPowerEventForAuthority(entry, guardianPolicyContext, knownPoliticalProjects, out var authorizedEvent))
                return false;

            var eventId = GuardianPowerEventState.GetEventId(authorizedEvent);
            if (!string.IsNullOrWhiteSpace(eventId) && !seenEventIds.Add(eventId))
            {
                failureDescription = $"validated snapshot guardianPowerEvents reuses append-only eventId '{eventId}'";
                return false;
            }

            if (string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase) &&
                TryBuildGuardianResonanceLifeScopeKey(entry, out var resonanceLifeScopeKey) &&
                !seenResonanceLifeScopeKeys.Add(resonanceLifeScopeKey))
            {
                failureDescription = $"validated snapshot guardianPowerEvents duplicates resonance life scope '{resonanceLifeScopeKey}'";
                return false;
            }

            authorizedPowerEvents.Add(authorizedEvent);
        }

        failureDescription = string.Empty;
        return true;
    }

    private Dictionary<string, (JsonElement Guardian, string Context)> ValidateGuardianCanonicalRootStructureForProof(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues)
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

                var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
                if (string.IsNullOrWhiteSpace(guardianId))
                    continue;

                if (guardiansById.ContainsKey(guardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"{guardianContext}.guardianId",
                        IssueSeverity.Error,
                        "Validated guardian snapshot must not contain duplicate guardianId entries.",
                        code: "guardian_duplicate_guardian_id",
                        section: "Guardians",
                        expected: "unique guardianId",
                        actual: guardianId));
                    continue;
                }

                guardiansById[guardianId] = (guardian.Clone(), guardianContext);
            }

            ValidateGuardianRelationshipNetwork(guardiansById, issues);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
        {
            var activeGuardianContext = $"{contextPrefix}.activeGuardian";
            ValidateGuardianCanonicalObject(activeGuardian, activeGuardianContext, issues);

            var activeGuardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(activeGuardianId) &&
                guardiansById.TryGetValue(activeGuardianId, out var guardianMatch))
            {
                CompareGuardianGachaState(activeGuardian, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
                CompareGuardianTradeState(activeGuardian, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
                ValidateActiveGuardianNavigationState(root, contextPrefix, activeGuardianContext, guardianMatch.Guardian, guardianMatch.Context, issues);
            }
            else if (!string.IsNullOrWhiteSpace(activeGuardianId))
            {
                var activeGuardianName = GuardianManifestation.GetDisplayName(activeGuardian);
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.activeGuardian.guardianId",
                    IssueSeverity.Error,
                    $"Активный хранитель '{activeGuardianName ?? activeGuardianId}' не найден в validated snapshot guardians[].",
                    code: "active_guardian_missing_in_guardians_array",
                    section: "Guardians",
                    expected: "activeGuardian.guardianId matches an entry inside guardians[]",
                    actual: $"guardianId={activeGuardianId}",
                    repairHint: "Сохраняй в validated snapshot activeGuardian только как strict mirror существующей canonical guardian entry из guardians[]."));
            }
        }

        return guardiansById;
    }

    private async Task<GuardianPowerJournalProofKnowledgeReadResult> ReadValidatedPreTurnGuardianPowerJournalProofKnowledgeAsync(
        GuardianPowerEventProofScope? proofScope = null)
    {
        var proofRelevantReasonType = GetGuardianPowerEventProofScopeReasonType(proofScope);
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            return new GuardianPowerJournalProofKnowledgeReadResult(
                GuardianPowerJournalProofKnowledgeStatus.MissingValidatedSnapshotContext,
                null,
                DescribeValidatedPendingTurnSnapshotStatus(lookup.Status));
        }

        if (lookup.Manifest.Files == null ||
            !lookup.Manifest.Files.ContainsKey("game_state/meta/guardians.json"))
        {
            return new GuardianPowerJournalProofKnowledgeReadResult(
                GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotGuardians,
                null,
                "validated snapshot manifest missing game_state/meta/guardians.json entry");
        }

        var guardiansJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, "game_state/meta/guardians.json");
        var trackerJson = lookup.Manifest.Files != null &&
                          lookup.Manifest.Files.ContainsKey(GuardianProjectState.TrackerPath)
            ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, GuardianProjectState.TrackerPath)
            : null;
        var preTurnJournalJson = lookup.Manifest.Files != null &&
                                 lookup.Manifest.Files.ContainsKey(GuardianPowerEventState.JournalPath)
            ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, GuardianPowerEventState.JournalPath)
            : null;
        var snapshotSoulJson = lookup.Manifest.Files != null &&
                               lookup.Manifest.Files.ContainsKey("game_state/meta/soul_state.json")
            ? await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, "game_state/meta/soul_state.json")
            : null;

        if (!TryReadCanonicalGuardianSnapshotForProof(
                guardiansJson,
                "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
                trackerJson,
                lookup.Manifest.Files != null && lookup.Manifest.Files.ContainsKey(GuardianProjectState.TrackerPath),
                $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                preTurnJournalJson,
                $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}",
                snapshotSoulJson,
                proofScope,
                null,
                out var snapshotGuardianAuthorityRoot,
                out var snapshotGuardiansById,
                out var snapshotGuardianFailureKind,
                out var guardianFailureDescription))
        {
            var status = snapshotGuardianFailureKind switch
            {
                GuardianSnapshotProofFailureKind.Journal => GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotJournal,
                GuardianSnapshotProofFailureKind.Tracker => GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker,
                _ => GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotGuardians
            };
            return new GuardianPowerJournalProofKnowledgeReadResult(
                status,
                null,
                guardianFailureDescription);
        }

        var requiresTrackerProofKnowledge = false;
        var nonPoliticalProofReasonType =
            string.Equals(proofRelevantReasonType, "offering", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(proofRelevantReasonType, "resonance", StringComparison.OrdinalIgnoreCase);
        if (!nonPoliticalProofReasonType &&
            !string.IsNullOrWhiteSpace(preTurnJournalJson))
        {
            if (TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(preTurnJournalJson, out var semanticPreTurnEntries) &&
                GuardianPowerJournalEntriesRequireProjectTrackerAuthority(
                    FilterGuardianPowerJournalEntriesForProof(semanticPreTurnEntries, proofRelevantReasonType)))
            {
                requiresTrackerProofKnowledge = true;
            }
        }

        var knownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (requiresTrackerProofKnowledge)
        {
            if (lookup.Manifest.Files == null ||
                !lookup.Manifest.Files.ContainsKey(GuardianProjectState.TrackerPath))
            {
                return new GuardianPowerJournalProofKnowledgeReadResult(
                    GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker,
                    null,
                    $"validated snapshot manifest missing {GuardianProjectState.TrackerPath} entry");
            }

            if (!TryReadCanonicalGuardianProjectTrackerSnapshotForProof(
                    trackerJson,
                    $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                    snapshotSoulJson,
                    snapshotGuardianAuthorityRoot,
                    snapshotGuardiansById,
                    out _,
                    out _,
                    out _,
                    out knownPoliticalProjects,
                    out var trackerFailureDescription))
            {
                return new GuardianPowerJournalProofKnowledgeReadResult(
                    GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker,
                    null,
                    trackerFailureDescription);
            }
        }

        return new GuardianPowerJournalProofKnowledgeReadResult(
            GuardianPowerJournalProofKnowledgeStatus.Resolved,
            new GuardianPowerJournalProofKnowledge(
                new HashSet<string>(snapshotGuardiansById.Keys, StringComparer.OrdinalIgnoreCase),
                knownPoliticalProjects),
            null);
    }

    private bool TryReadCanonicalGuardianProjectTrackerSnapshotForProof(
        string? trackerJson,
        string contextPrefix,
        string? soulStateJson,
        JsonElement guardianAuthorityRoot,
        IReadOnlyDictionary<string, JsonElement> guardiansById,
        out JsonElement trackerAuthorityRoot,
        out JsonElement guardianAuthorityRootAfterTracker,
        out Dictionary<string, JsonElement> guardiansByIdAfterTracker,
        out Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        out string failureDescription)
    {
        trackerAuthorityRoot = default;
        guardianAuthorityRootAfterTracker = default;
        guardiansByIdAfterTracker = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        knownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        failureDescription = "validated snapshot tracker JSON missing, unreadable or semantically invalid";
        if (string.IsNullOrWhiteSpace(trackerJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trackerJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var trackerRootObject = TryParseJsonObject(doc.RootElement);
            var guardianAuthorityRootObject = TryParseJsonObject(guardianAuthorityRoot);
            if (trackerRootObject == null || guardianAuthorityRootObject == null)
            {
                failureDescription = "validated snapshot tracker or guardian authority cannot be materialized into canonical project authority";
                return false;
            }

            var knownGuardianIds = new HashSet<string>(guardiansById.Keys, StringComparer.OrdinalIgnoreCase);
            var relationshipScores = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var guardian in guardiansById.Values)
                MergeGuardianIdentityValidationState(guardian, knownGuardianIds, relationshipScores);

            var scratchIssues = new List<ValidationIssue>();
            if (doc.RootElement.TryGetProperty("activeProjects", out var activeProjects))
            {
                ValidateGuardianProjectEntryArray(
                    activeProjects,
                    $"{contextPrefix}.activeProjects",
                    scratchIssues,
                    completed: false,
                    relationshipScores,
                    knownGuardianIds);
            }

            if (doc.RootElement.TryGetProperty("completedProjects", out var completedProjects))
            {
                ValidateGuardianProjectEntryArray(
                    completedProjects,
                    $"{contextPrefix}.completedProjects",
                    scratchIssues,
                    completed: true,
                    relationshipScores,
                    knownGuardianIds);
            }

            ValidateGuardianProjectIdentityCollisions(doc.RootElement, contextPrefix, scratchIssues);
            if (doc.RootElement.TryGetProperty("temporaryProjectModifiers", out var temporaryProjectModifiers))
            {
                ValidateGuardianProjectModifierAuthorityArray(
                    temporaryProjectModifiers,
                    $"{contextPrefix}.temporaryProjectModifiers",
                    scratchIssues,
                    knownGuardianIds);
            }

            JsonObject? preTurnTrackerAuthorityRootObject = null;
            if (scratchIssues.Count == 0 &&
                !TryBuildGuardianProjectTrackerPreTurnAuthorityRoot(
                    trackerRootObject.DeepClone().AsObject(),
                    guardianAuthorityRoot,
                    out preTurnTrackerAuthorityRootObject,
                    out failureDescription))
            {
                return false;
            }

            if (scratchIssues.Count == 0)
            {
                var knownProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var knownCompletedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var knownProjectDetails = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);
                var knownActiveProjectIdsByGuardian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var preTurnTrackerAuthorityJson = preTurnTrackerAuthorityRootObject!.ToJsonString();
                MergeKnownGuardianProjectKeysForValidation(knownProjects, preTurnTrackerAuthorityJson);
                MergeKnownCompletedGuardianProjectKeysForValidation(knownCompletedProjects, preTurnTrackerAuthorityJson);
                MergeKnownGuardianProjectsForValidation(knownProjectDetails, preTurnTrackerAuthorityJson);
                MergeKnownActiveGuardianProjectIdsByGuardian(knownActiveProjectIdsByGuardian, preTurnTrackerAuthorityJson);

                var startedThisTurn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var startedProjectDetails = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);
                if (doc.RootElement.TryGetProperty("startGuardianProjects", out var startCommands))
                {
                    ValidateGuardianProjectStartCommands(
                        startCommands,
                        $"{contextPrefix}.startGuardianProjects",
                        scratchIssues,
                        knownProjects,
                        knownCompletedProjects,
                        knownActiveProjectIdsByGuardian,
                        startedThisTurn,
                        startedProjectDetails,
                        relationshipScores,
                        knownGuardianIds);
                }

                if (doc.RootElement.TryGetProperty("guardianProjectUpdates", out var updateCommands))
                {
                    ValidateGuardianProjectUpdateCommands(
                        updateCommands,
                        $"{contextPrefix}.guardianProjectUpdates",
                        scratchIssues,
                        knownProjects,
                        startedThisTurn,
                        knownGuardianIds);
                }

                if (doc.RootElement.TryGetProperty("completeGuardianProjects", out var completeCommands))
                {
                    ValidateGuardianProjectCompletionCommands(
                        completeCommands,
                        $"{contextPrefix}.completeGuardianProjects",
                        scratchIssues,
                        knownProjects,
                        knownProjectDetails,
                        startedProjectDetails,
                        startedThisTurn,
                        relationshipScores,
                        knownGuardianIds);
                }
            }

            if (scratchIssues.Count != 0)
            {
                failureDescription = DescribeGuardianAcceptedTurnSnapshotBaselineFailure(scratchIssues);
                return false;
            }

            var currentTurn = ReadCurrentTurnNumberForProjectAuthority();
            var soulContextRequirements =
                CanonicalStateNormalizer.ResolveRequiredCurrentGuardianProjectSoulContext(
                    trackerRootObject,
                    preTurnTrackerAuthorityRootObject);
            if (!CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
                    soulStateJson,
                    null,
                    ReadCurrentTrackedFileSync("game_state/control/life_transitions.json"),
                    currentTurn,
                    soulContextRequirements,
                    out var currentIncarnation,
                    out var currentRealm,
                    out failureDescription))
            {
                return false;
            }

            var preTurnGuardianAuthorityRootObject = guardianAuthorityRootObject.DeepClone().AsObject();
            var currentGuardianAuthorityRootObject = guardianAuthorityRootObject.DeepClone().AsObject();
            var materializedTrackerAuthorityRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
                preTurnTrackerAuthorityRootObject!.DeepClone().AsObject(),
                trackerRootObject.DeepClone().AsObject(),
                preTurnGuardianAuthorityRootObject,
                currentGuardianAuthorityRootObject,
                currentTurn,
                currentIncarnation,
                currentRealm);
            trackerAuthorityRoot = CloneJsonObjectToElement(materializedTrackerAuthorityRoot);
            guardianAuthorityRootAfterTracker = CloneJsonObjectToElement(currentGuardianAuthorityRootObject);
            guardiansByIdAfterTracker = BuildGuardiansByIdFromAuthorityRoot(guardianAuthorityRootAfterTracker);

            var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MergeKnownPoliticalGuardianPowerEventProjectsForValidation(
                knownPoliticalProjects,
                ambiguousKeys,
                trackerAuthorityRoot.GetRawText());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string DescribeGuardianAcceptedTurnSnapshotBaselineFailure(List<ValidationIssue> issues)
    {
        if (issues.Count == 0)
            return "canonical guardian contract failure";

        var firstIssue = issues[0];
        if (!string.IsNullOrWhiteSpace(firstIssue.Code))
            return $"{firstIssue.Code} at {firstIssue.FilePath}";

        return $"{firstIssue.FilePath}: {firstIssue.Message}";
    }

    private async Task<ValidatedPendingTurnSnapshotLookup> LoadValidatedPendingTurnSnapshotLookupAsync()
    {
        var manifestExists = _fs.FileExists(PendingTurnSnapshotManifestPath);
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        var authorityJson = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        if (manifest == null)
        {
            return new ValidatedPendingTurnSnapshotLookup(
                manifestExists ? ValidatedPendingTurnSnapshotStatus.Unusable : ValidatedPendingTurnSnapshotStatus.Missing,
                null);
        }

        return new ValidatedPendingTurnSnapshotLookup(
            IsValidatedPendingTurnSnapshotManifestUsable(manifest, authorityJson)
                ? ValidatedPendingTurnSnapshotStatus.Usable
                : ValidatedPendingTurnSnapshotStatus.Unusable,
            manifest);
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadCurrentDetachedPendingTurnSnapshotAuthorityPayloadAsync()
    {
        var authorityJson = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        if (!PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(authorityJson, out var payload) || payload == null)
            return null;

        return IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(payload)
            ? payload
            : null;
    }

    private bool IsValidatedPendingTurnSnapshotManifestUsable(
        ValidationPendingTurnSnapshotManifest? manifest,
        string? authorityJson)
    {
        if (manifest == null)
            return false;

        if (!PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
                manifest,
                authorityJson,
                ManifestHashJsonOpts,
                static snapshotManifest => snapshotManifest.ManifestPayloadHash,
                static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
                static snapshotManifest => snapshotManifest.SessionId,
                static snapshotManifest => snapshotManifest.RequestId,
                static snapshotManifest => snapshotManifest.TurnNumber,
                static snapshotManifest => snapshotManifest.Files,
                static snapshotManifest => snapshotManifest.SnapshotFileHashes,
                static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
                static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
                static snapshotManifest => snapshotManifest.SourceLabel,
                static snapshotManifest => snapshotManifest.RollbackBackups,
                ReadRelativeFileFromWorkspace,
                out _,
                out _))
        {
            return false;
        }

        return IsValidatedPendingTurnSnapshotManifestCurrent(manifest);
    }

    private bool IsValidatedPendingTurnSnapshotManifestCurrent(ValidationPendingTurnSnapshotManifest manifest)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath(repairRequestPath));
        if (DoesPendingTurnRequestValidationContextMatchManifest(manifest, repairContext))
            return true;

        var turnContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath("input/turn_request.json"));
        return DoesPendingTurnRequestValidationContextMatchManifest(manifest, turnContext);
    }

    private bool IsCurrentDetachedPendingTurnSnapshotAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath(repairRequestPath));
        if (DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, repairContext))
            return true;

        var turnContext = LoadPendingTurnRequestValidationContextSync(_fs.ResolvePath("input/turn_request.json"));
        return DoesPendingTurnRequestValidationContextMatchAuthorityPayload(payload, turnContext);
    }

    private static bool DoesPendingTurnRequestValidationContextMatchManifest(
        ValidationPendingTurnSnapshotManifest manifest,
        PendingTurnRequestValidationContext? context)
    {
        if (context == null)
            return false;

        if (manifest.TurnNumber != context.TurnNumber)
            return false;

        if (!DoesPendingTurnContextIdMatch(manifest.SessionId, context.SessionId))
            return false;

        if (!DoesPendingTurnContextIdMatch(manifest.RequestId, context.RequestId))
            return false;

        return true;
    }

    private static bool DoesPendingTurnContextIdMatch(string manifestId, string contextId)
    {
        return PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifestId, contextId);
    }

    private static string DescribeValidatedPendingTurnSnapshotStatus(ValidatedPendingTurnSnapshotStatus status) => status switch
    {
        ValidatedPendingTurnSnapshotStatus.Missing => "validated pending turn snapshot manifest is missing",
        ValidatedPendingTurnSnapshotStatus.Unusable => "validated pending turn snapshot manifest is unreadable, detached-authority invalid, modified, missing required snapshot data, or not current for the active request context",
        _ => "validated pending turn snapshot is usable"
    };

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

    private async Task<ValidatedTrackedFileChangeStatus> DescribeTrackedFileChangeAgainstManifestAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string relativePath)
    {
        var current = await _fs.ReadFileAsync(relativePath) ?? string.Empty;
        var hasValidatedRegistration = HasValidatedTrackedSnapshotRegistration(manifest, relativePath);
        if (!hasValidatedRegistration)
        {
            return IsTrackedByValidatedBaseline(manifest, relativePath) || !string.IsNullOrWhiteSpace(current)
                ? ValidatedTrackedFileChangeStatus.MissingValidatedBaseline
                : ValidatedTrackedFileChangeStatus.Unchanged;
        }

        var previous = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, relativePath);
        if (previous == null)
            return ValidatedTrackedFileChangeStatus.MissingValidatedBaseline;

        return string.Equals(current, previous, StringComparison.Ordinal)
            ? ValidatedTrackedFileChangeStatus.Unchanged
            : ValidatedTrackedFileChangeStatus.Changed;
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

    private async Task<List<string>> GetChangedTrackedFilesAgainstManifestAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues,
        string missingBaselineCode,
        string section,
        string repairHint)
    {
        var changedFiles = new List<string>();
        foreach (var relativePath in EnumerateTrackedFilesForValidation(manifest))
        {
            switch (await DescribeTrackedFileChangeAgainstManifestAsync(manifest, relativePath))
            {
                case ValidatedTrackedFileChangeStatus.Changed:
                    changedFiles.Add(relativePath);
                    break;
                case ValidatedTrackedFileChangeStatus.MissingValidatedBaseline:
                    AddMissingValidatedTrackedBaselineIssue(
                        issues,
                        relativePath,
                        missingBaselineCode,
                        section,
                        $"Не удалось строго вычислить diff для {relativePath}: validated pre-turn baseline missing.",
                        repairHint);
                    break;
            }
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

    private OfferingJournalProofSummary SummarizeOfferingJournalProof(
        string? preJson,
        string? postJson,
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request,
        int expectedGain,
        GuardianPowerJournalProofKnowledge preTurnKnowledge,
        string? proofRelevantReasonType = null,
        string? expectedEventId = null)
    {
        if (!TryReadStrictGuardianPowerJournalEntriesForValidatedBaselineProof(
                preJson,
                preTurnKnowledge,
                proofRelevantReasonType,
                out var preEntries))
        {
            return new OfferingJournalProofSummary(OfferingJournalProofStatus.InvalidValidatedBaseline, false, 0, 0);
        }

        var currentProof = ReadStrictGuardianPowerJournalEntriesForCurrentProof(postJson, proofRelevantReasonType);
        if (currentProof.Status == GuardianPowerJournalCurrentProofStatus.InvalidCurrentGuardianAuthority)
        {
            return new OfferingJournalProofSummary(
                OfferingJournalProofStatus.InvalidCurrentGuardianAuthority,
                false,
                0,
                0,
                currentProof.FailureDescription);
        }

        if (currentProof.Status == GuardianPowerJournalCurrentProofStatus.InvalidCurrentTrackerAuthority)
        {
            return new OfferingJournalProofSummary(
                OfferingJournalProofStatus.InvalidCurrentTrackerAuthority,
                false,
                0,
                0,
                currentProof.FailureDescription);
        }

        if (currentProof.Status != GuardianPowerJournalCurrentProofStatus.Resolved ||
            currentProof.Entries == null)
        {
            return new OfferingJournalProofSummary(
                OfferingJournalProofStatus.InvalidCurrentJournal,
                false,
                0,
                0,
                currentProof.FailureDescription);
        }

        var postEntries = currentProof.Entries;
        if (!TryValidateGuardianPowerJournalAppendOnlyIdentity(preEntries, postEntries, out var appendOnlyFailureDescription))
        {
            return new OfferingJournalProofSummary(
                OfferingJournalProofStatus.InvalidCurrentJournal,
                false,
                0,
                0,
                appendOnlyFailureDescription);
        }

        var knownEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in preEntries)
        {
            var eventId = GetStringValue(entry, "eventId");
            if (!string.IsNullOrWhiteSpace(eventId))
                knownEventIds.Add(eventId);
        }

        var matchingOfferingEventFound = false;
        foreach (var entry in postEntries)
        {
            var eventId = GetStringValue(entry, "eventId");
            if (string.IsNullOrWhiteSpace(eventId) ||
                knownEventIds.Contains(eventId) ||
                !TryParseStrictOfferingJournalEntry(entry, out var strictEntry) ||
                !StrictOfferingJournalEntryMatchesPendingRequest(strictEntry, request, expectedGain, expectedEventId))
            {
                continue;
            }

            matchingOfferingEventFound = true;
            break;
        }

        return new OfferingJournalProofSummary(
            OfferingJournalProofStatus.Resolved,
            matchingOfferingEventFound,
            CountOfferingFeathersFromJournalEntries(preEntries, request.GuardianId, request.ReturnCycleId),
            CountOfferingFeathersFromJournalEntries(postEntries, request.GuardianId, request.ReturnCycleId));
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

    private bool TryReadStrictGuardianPowerJournalEntriesForValidatedBaselineProof(
        string? journalJson,
        GuardianPowerJournalProofKnowledge proofKnowledge,
        string? proofRelevantReasonType,
        out List<JsonElement> entries)
    {
        entries = new List<JsonElement>();
        if (!TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(journalJson, out var semanticEntries))
            return false;

        return TryReadStrictGuardianPowerJournalEntriesForProof(
            semanticEntries,
            proofKnowledge.KnownGuardianIds,
            proofKnowledge.KnownPoliticalProjects,
            proofRelevantReasonType,
            out entries);
    }

    private GuardianPowerJournalCurrentProofReadResult ReadStrictGuardianPowerJournalEntriesForCurrentProof(
        string? journalJson,
        string? proofRelevantReasonType = null)
    {
        if (!TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(journalJson, out var semanticEntries))
        {
            return new GuardianPowerJournalCurrentProofReadResult(
                GuardianPowerJournalCurrentProofStatus.InvalidCurrentJournal,
                null,
                "current journal unreadable, malformed or semantically invalid");
        }

        if (!TryReadCurrentGuardianPowerJournalProofKnowledge(
                GuardianPowerJournalEntriesRequireProjectTrackerAuthority(
                    FilterGuardianPowerJournalEntriesForProof(semanticEntries, proofRelevantReasonType)),
                out var knownGuardianIds,
                out var knownPoliticalProjects,
                out var authorityFailureStatus,
                out var authorityFailureDescription))
        {
            return new GuardianPowerJournalCurrentProofReadResult(
                authorityFailureStatus,
                null,
                authorityFailureDescription);
        }

        if (!TryReadStrictGuardianPowerJournalEntriesForProof(
                semanticEntries,
                knownGuardianIds,
                knownPoliticalProjects,
                proofRelevantReasonType,
                out var entries))
        {
            return new GuardianPowerJournalCurrentProofReadResult(
                GuardianPowerJournalCurrentProofStatus.InvalidCurrentJournal,
                null,
                "current journal unreadable, malformed or semantically invalid");
        }

        return new GuardianPowerJournalCurrentProofReadResult(
            GuardianPowerJournalCurrentProofStatus.Resolved,
            entries,
            null);
    }

    private bool TryReadCurrentGuardianPowerJournalProofKnowledge(
        bool requiresPoliticalProjectKnowledge,
        out HashSet<string> knownGuardianIds,
        out IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        out GuardianPowerJournalCurrentProofStatus failureStatus,
        out string failureDescription)
    {
        knownGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        knownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        failureStatus = GuardianPowerJournalCurrentProofStatus.InvalidCurrentGuardianAuthority;
        failureDescription = "current guardian authority unavailable";

        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        if (requiresPoliticalProjectKnowledge &&
            guardianPolicyContext.CurrentGuardianPowerEventAuthorityStatus != GuardianPowerEventAuthorityStatus.None &&
            guardianPolicyContext.CurrentGuardianPowerEventAuthorityStatus != GuardianPowerEventAuthorityStatus.Resolved)
        {
            failureDescription = string.IsNullOrWhiteSpace(guardianPolicyContext.CurrentGuardianPowerEventAuthorityFailureDescription)
                ? $"current guardian power-event authority unavailable: {guardianPolicyContext.CurrentGuardianPowerEventAuthorityStatus}"
                : guardianPolicyContext.CurrentGuardianPowerEventAuthorityFailureDescription!;
            return false;
        }

        if (!guardianPolicyContext.HasStrictCurrentAuthorityRoot ||
            guardianPolicyContext.StrictCurrentAuthorityRoot.ValueKind != JsonValueKind.Object ||
            !guardianPolicyContext.StrictCurrentAuthorityRoot.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            failureDescription = HasResolvedStrictPreTurnGuardianAuthority(guardianPolicyContext)
                ? guardianPolicyContext.HasStrictCurrentAuthorityRoot
                    ? "current guardian authority unreadable or missing canonical guardians[]"
                    : "current strict guardian authority unavailable"
                : DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext);
            return false;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(guardianId))
                knownGuardianIds.Add(guardianId);
        }

        if (!requiresPoliticalProjectKnowledge)
        {
            failureStatus = GuardianPowerJournalCurrentProofStatus.Resolved;
            failureDescription = string.Empty;
            return true;
        }

        if (!TryResolveStrictGuardianProjectTrackerAuthorityRootForProof(
                out var strictTrackerAuthorityRoot,
                out var strictTrackerAuthorityFailure))
        {
            failureStatus = GuardianPowerJournalCurrentProofStatus.InvalidCurrentTrackerAuthority;
            failureDescription = strictTrackerAuthorityFailure;
            return false;
        }

        var projects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        var ambiguousKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MergeKnownPoliticalGuardianPowerEventProjectsForValidation(
            projects,
            ambiguousKeys,
            strictTrackerAuthorityRoot.GetRawText());
        knownPoliticalProjects = projects;
        failureStatus = GuardianPowerJournalCurrentProofStatus.Resolved;
        failureDescription = string.Empty;
        return true;
    }

    private GuardianPowerJournalIdentityBaselineResolution ResolveValidatedPreTurnGuardianPowerJournalIdentityState()
    {
        var trackedResolution = ResolveValidatedGuardianTrackedSnapshotFileSync(GuardianPowerEventState.JournalPath);
        if (trackedResolution.FileStatus == GuardianTrackedSnapshotFileStatus.MissingManifest ||
            trackedResolution.FileStatus == GuardianTrackedSnapshotFileStatus.MissingSnapshotFile)
        {
            return new GuardianPowerJournalIdentityBaselineResolution(
                GuardianPowerJournalIdentityBaselineStatus.MissingValidatedSnapshotJournal,
                null,
                DescribeGuardianTrackedSnapshotFileStatus(GuardianPowerEventState.JournalPath, trackedResolution.FileStatus));
        }

        if (trackedResolution.FileStatus != GuardianTrackedSnapshotFileStatus.Usable)
        {
            return new GuardianPowerJournalIdentityBaselineResolution(
                GuardianPowerJournalIdentityBaselineStatus.InvalidValidatedSnapshotJournal,
                null,
                DescribeGuardianTrackedSnapshotFileStatus(GuardianPowerEventState.JournalPath, trackedResolution.FileStatus));
        }

        if (!TryReadGuardianPowerJournalIdentityStateForProof(
                trackedResolution.SnapshotJson,
                $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}",
                out var identityState,
                out var failureDescription))
        {
            return new GuardianPowerJournalIdentityBaselineResolution(
                GuardianPowerJournalIdentityBaselineStatus.InvalidValidatedSnapshotJournal,
                null,
                failureDescription);
        }

        return new GuardianPowerJournalIdentityBaselineResolution(
            GuardianPowerJournalIdentityBaselineStatus.Resolved,
            identityState,
            $"validated pending turn snapshot entry for {GuardianPowerEventState.JournalPath} is usable");
    }

    private bool TryReadStrictGuardianPowerJournalEntriesForProof(
        IReadOnlyList<JsonElement> semanticEntries,
        HashSet<string> knownGuardianIds,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        string? proofRelevantReasonType,
        out List<JsonElement> entries)
    {
        entries = new List<JsonElement>();
        var scratchIssues = new List<ValidationIssue>();
        var identityEntries = new List<(JsonElement Entry, string Context)>();

        var index = 0;
        foreach (var entry in semanticEntries)
        {
            var entryContext = $"{GuardianPowerEventState.JournalPath}.entries[{index++}]";
            if (!IsGuardianPowerJournalEntryRelevantForProof(entry, proofRelevantReasonType))
            {
                entries.Add(entry.Clone());
                identityEntries.Add((entry, entryContext));
                continue;
            }

            var repairResult = RepairGuardianPowerJournalEntryForValidation(entry, knownPoliticalProjects);
            if (repairResult.Status == GuardianPowerJournalRepairStatus.Canonicalized)
            {
                scratchIssues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "Validated journal baseline must already be canonical and cannot rely on repair during proof validation.",
                    code: "guardian_power_event_requires_canonical_repair",
                    section: "AbodePower"));
            }

            var effectiveEntry = repairResult.EffectiveEntry;
            ValidateGuardianPowerJournalEntryContract(
                effectiveEntry,
                entryContext,
                scratchIssues,
                knownGuardianIds,
                knownPoliticalProjects);
            entries.Add(effectiveEntry.Clone());
            identityEntries.Add((effectiveEntry, entryContext));
        }

        ValidateGuardianPowerJournalIdentityContract(identityEntries, scratchIssues);
        return scratchIssues.Count == 0;
    }

    private static bool TryReadGuardianPowerJournalEntries(string? journalJson, out List<JsonElement> entries)
    {
        entries = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(journalJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(journalJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("entries", out var journalEntries) ||
                journalEntries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var entry in journalEntries.EnumerateArray())
                entries.Add(entry.Clone());

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(string? journalJson, out List<JsonElement> entries)
    {
        entries = new List<JsonElement>();
        if (!TryReadGuardianPowerJournalEntries(journalJson, out var rawEntries))
            return false;

        var scratchIssues = new List<ValidationIssue>();
        var emptyKnownPoliticalProjects = new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        var identityEntries = new List<(JsonElement Entry, string Context)>();
        var index = 0;
        foreach (var entry in rawEntries)
        {
            var entryContext = $"{GuardianPowerEventState.JournalPath}.entries[{index++}]";
            if (!RequireObject(entry, entryContext, scratchIssues))
                continue;

            RequireString(entry, entryContext, scratchIssues, "entryId");
            RequireString(entry, entryContext, scratchIssues, "eventId");
            var guardianId = RequireString(entry, entryContext, scratchIssues, "guardianId");
            RequireString(entry, entryContext, scratchIssues, "guardianName");
            ValidateNonNegativeIntegerField(entry, entryContext, scratchIssues, "turn", "AbodePower");
            ValidateIntegerField(entry, entryContext, scratchIssues, "delta");

            var reasonType = RequireString(entry, entryContext, scratchIssues, "reasonType");
            if (!string.IsNullOrWhiteSpace(reasonType) && !GuardianPowerEventState.IsValidReasonType(reasonType))
            {
                scratchIssues.Add(new ValidationIssue(
                    $"{entryContext}.reasonType",
                    IssueSeverity.Error,
                    "abode_power_journal.reasonType использует неподдерживаемый тип события силы Обители",
                    code: "guardian_power_event_invalid_reason_type",
                    section: "AbodePower"));
            }
            else if (string.Equals(reasonType, "guardian_quest", StringComparison.OrdinalIgnoreCase))
            {
                scratchIssues.Add(new ValidationIssue(
                    $"{entryContext}.reasonType",
                    IssueSeverity.Error,
                    "guardian quest power change должен идти через UpdateGuardians.completeQuest.questPowerAudit, а не через raw abode_power_journal",
                    code: "guardian_power_event_guardian_quest_wrong_surface",
                    section: "AbodePower"));
            }

            var sourceSurface = RequireString(entry, entryContext, scratchIssues, "sourceSurface");
            var sourceId = RequireString(entry, entryContext, scratchIssues, "sourceId");
            RequireString(entry, entryContext, scratchIssues, "title");
            RequireString(entry, entryContext, scratchIssues, "summary");

            var visibility = RequireString(entry, entryContext, scratchIssues, "visibility");
            if (!string.IsNullOrWhiteSpace(visibility) && !GuardianPowerEventState.IsValidVisibility(visibility))
            {
                scratchIssues.Add(new ValidationIssue(
                    $"{entryContext}.visibility",
                    IssueSeverity.Error,
                    "abode_power_journal.visibility использует неподдерживаемое значение",
                    code: "guardian_power_event_invalid_visibility",
                    section: "AbodePower"));
            }

            ValidateOptionalNullableStringField(entry, entryContext, scratchIssues, "relatedGuardianId");
            var relatedGuardianId = GetFirstNonEmptyString(entry, "relatedGuardianId");

            var appliedAt = RequireString(entry, entryContext, scratchIssues, "appliedAt");
            if (!string.IsNullOrWhiteSpace(appliedAt) && !DateTimeOffset.TryParse(appliedAt, out _))
            {
                scratchIssues.Add(new ValidationIssue(
                    $"{entryContext}.appliedAt",
                    IssueSeverity.Error,
                    "abode_power_journal.appliedAt должен быть ISO 8601 timestamp",
                    code: "guardian_power_event_invalid_applied_at",
                    section: "AbodePower"));
            }

            if (!entry.TryGetProperty("audit", out var audit) || !RequireObject(audit, $"{entryContext}.audit", scratchIssues))
            {
                scratchIssues.Add(new ValidationIssue(
                    $"{entryContext}.audit",
                    IssueSeverity.Error,
                    "abode_power_journal entry обязан содержать audit object",
                    code: "guardian_power_event_missing_audit",
                    section: "AbodePower"));
            }
            else if (!string.IsNullOrWhiteSpace(reasonType))
            {
                ValidateGuardianPowerEventAudit(
                    guardianId,
                    relatedGuardianId,
                    sourceSurface,
                    sourceId,
                    reasonType,
                    TryReadInt(entry, "delta", out var currentDelta) ? currentDelta : null,
                    audit,
                    entryContext,
                    $"{entryContext}.audit",
                    scratchIssues,
                    emptyKnownPoliticalProjects,
                    authorityIndependentOnly: true);
                ValidateCompletionSourcedRivalStrikeEventContract(
                    entry,
                    entryContext,
                    guardianId,
                    relatedGuardianId,
                    sourceSurface,
                    sourceId,
                    reasonType,
                    audit,
                    $"{entryContext}.audit",
                    scratchIssues,
                    emptyKnownPoliticalProjects,
                    journalSurface: true,
                    authorityIndependentOnly: true);
                ValidateUpdateSourcedRivalStrikeEventContract(
                    entry,
                    entryContext,
                    sourceSurface,
                    reasonType,
                    scratchIssues);
            }

            entries.Add(entry.Clone());
            identityEntries.Add((entry, entryContext));
        }

        ValidateGuardianPowerJournalIdentityContract(identityEntries, scratchIssues);
        return scratchIssues.Count == 0;
    }

    private static bool GuardianPowerJournalEntriesRequireProjectTrackerAuthority(IEnumerable<JsonElement> entries)
    {
        foreach (var entry in entries)
        {
            if (IsPoliticalGuardianPowerEventReasonType(GetFirstNonEmptyString(entry, "reasonType")))
                return true;
        }

        return false;
    }

    private static IEnumerable<JsonElement> FilterGuardianPowerJournalEntriesForProof(
        IEnumerable<JsonElement> entries,
        string? proofRelevantReasonType)
    {
        foreach (var entry in entries)
        {
            if (IsGuardianPowerJournalEntryRelevantForProof(entry, proofRelevantReasonType))
                yield return entry;
        }
    }

    private static bool IsGuardianPowerJournalEntryRelevantForProof(
        JsonElement entry,
        string? proofRelevantReasonType)
    {
        return string.IsNullOrWhiteSpace(proofRelevantReasonType) ||
               string.Equals(
                   GetFirstNonEmptyString(entry, "reasonType"),
                   proofRelevantReasonType,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNonEmptyGuardianPowerEventArray(JsonElement events)
        => events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0;

    private static SnapshotTrackerGuardianEffectDependencyStatus ResolveSnapshotTrackerGuardianEffectDependency(
        string? trackerJson,
        bool hasTrackerSnapshotEntry,
        GuardianPowerEventProofScope? authorityProofScope,
        out bool shouldMaterialize,
        out string failureDescription)
    {
        shouldMaterialize = false;
        failureDescription = string.Empty;
        if (!authorityProofScope.HasValue)
            return SnapshotTrackerGuardianEffectDependencyStatus.NoTrackerDependency;

        if (CanSafelyProveSnapshotTrackerIrrelevantToGuardian(
                trackerJson,
                hasTrackerSnapshotEntry,
                authorityProofScope.Value))
        {
            return SnapshotTrackerGuardianEffectDependencyStatus.NoTrackerDependency;
        }

        if (!hasTrackerSnapshotEntry)
        {
            failureDescription = "validated snapshot tracker is missing and tracker irrelevance to the guardian baseline cannot be proven";
            return SnapshotTrackerGuardianEffectDependencyStatus.MissingValidatedSnapshotTracker;
        }

        if (string.IsNullOrWhiteSpace(trackerJson))
        {
            failureDescription = "validated snapshot tracker unreadable or empty when guardian baseline materialization may depend on tracker side effects";
            return SnapshotTrackerGuardianEffectDependencyStatus.InvalidValidatedSnapshotTracker;
        }

        try
        {
            using var doc = JsonDocument.Parse(trackerJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                failureDescription = "validated snapshot tracker must be a canonical object when guardian baseline materialization depends on tracker side effects";
                return SnapshotTrackerGuardianEffectDependencyStatus.InvalidValidatedSnapshotTracker;
            }

            var guardianId = authorityProofScope.Value.GuardianId;
            if (string.IsNullOrWhiteSpace(guardianId))
            {
                shouldMaterialize = true;
                return SnapshotTrackerGuardianEffectDependencyStatus.TrackerRequiredAndResolved;
            }

            shouldMaterialize = TrackerSnapshotTouchesGuardian(doc.RootElement, guardianId);
            return shouldMaterialize
                ? SnapshotTrackerGuardianEffectDependencyStatus.TrackerRequiredAndResolved
                : SnapshotTrackerGuardianEffectDependencyStatus.NoTrackerDependency;
        }
        catch (JsonException)
        {
            failureDescription = "validated snapshot tracker unreadable or malformed for guardian baseline materialization";
            return SnapshotTrackerGuardianEffectDependencyStatus.InvalidValidatedSnapshotTracker;
        }
    }

    private static bool CanSafelyProveSnapshotTrackerIrrelevantToGuardian(
        string? trackerJson,
        bool hasTrackerSnapshotEntry,
        GuardianPowerEventProofScope authorityProofScope)
    {
        if (!authorityProofScope.GuardianBaselineScope)
            return true;

        var guardianId = authorityProofScope.GuardianId;
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        if (!hasTrackerSnapshotEntry || string.IsNullOrWhiteSpace(trackerJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(trackerJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   !TrackerSnapshotTouchesGuardian(doc.RootElement, guardianId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TrackerSnapshotTouchesGuardian(JsonElement root, string guardianId)
    {
        if (TrackerSnapshotCollectionTouchesGuardian(root, "activeProjects", guardianId))
            return true;
        if (TrackerSnapshotCollectionTouchesGuardian(root, "completedProjects", guardianId))
            return true;
        if (TrackerSnapshotCollectionTouchesGuardian(root, "startGuardianProjects", guardianId))
            return true;
        if (TrackerSnapshotCollectionTouchesGuardian(root, "guardianProjectUpdates", guardianId))
            return true;
        if (TrackerSnapshotCollectionTouchesGuardian(root, "completeGuardianProjects", guardianId))
            return true;

        return false;
    }

    private static bool TrackerSnapshotCollectionTouchesGuardian(JsonElement root, string propertyName, string guardianId)
    {
        if (!root.TryGetProperty(propertyName, out var collection) || collection.ValueKind == JsonValueKind.Null)
            return false;
        if (collection.ValueKind != JsonValueKind.Array)
            return true;

        foreach (var entry in collection.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                return true;

            var entryGuardianId = GetFirstNonEmptyString(entry, "guardianId");
            if (!string.IsNullOrWhiteSpace(entryGuardianId) &&
                string.Equals(entryGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var directTargetGuardianId = GetFirstNonEmptyString(entry, "targetGuardianId");
            if (!string.IsNullOrWhiteSpace(directTargetGuardianId) &&
                string.Equals(directTargetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (entry.TryGetProperty("project", out var project) && project.ValueKind == JsonValueKind.Object)
            {
                var projectTargetGuardianId = GetFirstNonEmptyString(project, "targetGuardianId");
                if (!string.IsNullOrWhiteSpace(projectTargetGuardianId) &&
                    string.Equals(projectTargetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryReadGuardianPowerJournalIdentityStateForProof(
        string? journalJson,
        string journalContextPrefix,
        out GuardianPowerJournalIdentityState identityState,
        out string failureDescription)
    {
        identityState = new GuardianPowerJournalIdentityState(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        failureDescription = $"{journalContextPrefix} unreadable or semantically invalid for strict guardian power-event identity proof";
        if (!TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(journalJson, out var entries))
            return false;

        foreach (var entry in entries)
        {
            var eventId = GetStringValue(entry, "eventId");
            if (!string.IsNullOrWhiteSpace(eventId))
                identityState.EventIds.Add(eventId);

            if (string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase) &&
                TryBuildGuardianResonanceLifeScopeKey(entry, out var lifeScopeKey))
            {
                identityState.ResonanceLifeScopeKeys.Add(lifeScopeKey);
            }
        }

        failureDescription = string.Empty;
        return true;
    }

    private static bool TryValidateGuardianPowerJournalAppendOnlyIdentity(
        IReadOnlyList<JsonElement> preEntries,
        IReadOnlyList<JsonElement> postEntries,
        out string failureDescription)
    {
        failureDescription = string.Empty;

        var postByEntryId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in postEntries)
        {
            var entryId = GetStringValue(entry, "entryId");
            if (string.IsNullOrWhiteSpace(entryId))
                continue;

            postByEntryId[entryId] = BuildCanonicalJsonComparisonSignature(entry);
        }

        foreach (var preEntry in preEntries)
        {
            var entryId = GetStringValue(preEntry, "entryId");
            if (string.IsNullOrWhiteSpace(entryId))
                continue;

            var preSignature = BuildCanonicalJsonComparisonSignature(preEntry);
            if (!postByEntryId.TryGetValue(entryId, out var postSignature))
            {
                failureDescription = $"current journal is not append-only: baseline entryId '{entryId}' is missing";
                return false;
            }

            if (!string.Equals(preSignature, postSignature, StringComparison.Ordinal))
            {
                failureDescription = $"current journal rewrote baseline entryId '{entryId}' instead of preserving append-only identity";
                return false;
            }
        }

        return true;
    }

    private static bool DoesPendingTurnRequestValidationContextMatchAuthorityPayload(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        PendingTurnRequestValidationContext? context)
    {
        if (context == null)
            return false;

        if (payload.TurnNumber != context.TurnNumber)
            return false;

        if (!DoesPendingTurnContextIdMatch(payload.SessionId, context.SessionId))
            return false;

        if (!DoesPendingTurnContextIdMatch(payload.RequestId, context.RequestId))
            return false;

        return true;
    }

    private static string BuildCanonicalJsonComparisonSignature(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(
                ",",
                value.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => $"{JsonSerializer.Serialize(property.Name)}:{BuildCanonicalJsonComparisonSignature(property.Value)}")) + "}",
            JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(BuildCanonicalJsonComparisonSignature)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => value.GetRawText(),
            _ => value.GetRawText()
        };
    }

    private static void RequireIntegerFieldForCurrentJournalSemanticProof(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Current journal proof audit must include the required integer field.",
                code: "guardian_power_event_missing_audit_field",
                section: "AbodePower"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            return;

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.{propName}",
            IssueSeverity.Error,
            "Current journal proof audit field must be an integer.",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static void RequireNonNegativeIntegerFieldForCurrentJournalSemanticProof(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Current journal proof audit must include the required non-negative integer field.",
                code: "guardian_power_event_missing_audit_field",
                section: "AbodePower"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var intValue) &&
            intValue >= 0)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.{propName}",
            IssueSeverity.Error,
            "Current journal proof audit field must be a non-negative integer.",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static void RequirePositiveNumberFieldForCurrentJournalSemanticProof(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Current journal proof audit must include the required positive number field.",
                code: "guardian_power_event_missing_audit_field",
                section: "AbodePower"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var numericValue) &&
            numericValue > 0)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.{propName}",
            IssueSeverity.Error,
            "Current journal proof audit field must be a positive number.",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static int CountOfferingFeathersFromJournalEntries(IEnumerable<JsonElement> entries, string guardianId, string returnCycleId)
    {
        var total = 0;
        foreach (var entry in entries)
        {
            if (!TryParseStrictOfferingJournalEntry(entry, out var strictEntry) ||
                !string.Equals(strictEntry.GuardianId, guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(strictEntry.ReturnCycleId, returnCycleId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(strictEntry.OfferingType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            total += strictEntry.InkFeathersOffered;
        }

        return total;
    }

    private static bool JournalEntryMatchesPendingAbodeOffering(JsonElement entry, GuardianAbodeOfferingState.PendingAbodeOfferingRequest request, int expectedGain)
    {
        return TryParseStrictOfferingJournalEntry(entry, out var strictEntry) &&
               StrictOfferingJournalEntryMatchesPendingRequest(strictEntry, request, expectedGain);
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

    private static bool TryParseStrictOfferingJournalEntry(JsonElement entry, out StrictOfferingJournalEntry strictEntry)
    {
        strictEntry = default;
        if (entry.ValueKind != JsonValueKind.Object ||
            !TryReadRequiredStrictString(entry, "entryId", out _) ||
            !TryReadRequiredStrictString(entry, "eventId", out var eventId) ||
            !TryReadRequiredStrictString(entry, "guardianId", out var guardianId) ||
            !TryReadRequiredStrictString(entry, "guardianName", out _) ||
            !TryReadStrictInt32(entry, "turn", out _) ||
            !TryReadStrictInt32(entry, "delta", out var delta) ||
            !TryReadRequiredStrictString(entry, "reasonType", out var reasonType) ||
            !string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase) ||
            !TryReadRequiredStrictString(entry, "sourceSurface", out var sourceSurface) ||
            !TryGetExpectedNonPoliticalGuardianPowerEventSourceSurface(reasonType, out var expectedSourceSurface) ||
            !string.Equals(sourceSurface, expectedSourceSurface, StringComparison.OrdinalIgnoreCase) ||
            !TryReadRequiredStrictString(entry, "sourceId", out _) ||
            !TryReadRequiredStrictString(entry, "title", out _) ||
            !TryReadRequiredStrictString(entry, "summary", out _) ||
            !TryReadRequiredStrictString(entry, "visibility", out var visibility) ||
            !GuardianPowerEventState.IsValidVisibility(visibility) ||
            !TryReadRequiredStrictString(entry, "appliedAt", out var appliedAt) ||
            !DateTimeOffset.TryParse(appliedAt, out _) ||
            !entry.TryGetProperty("audit", out var audit) ||
            audit.ValueKind != JsonValueKind.Object ||
            !TryReadRequiredStrictString(audit, "offeringType", out var offeringType) ||
            !TryReadRequiredStrictString(audit, "returnCycleId", out var returnCycleId) ||
            !TryReadStrictInt32(audit, "baseDelta", out var baseDelta) ||
            !TryReadStrictInt32(audit, "finalDelta", out var finalDelta))
        {
            return false;
        }

        if (delta <= 0 || baseDelta <= 0 || finalDelta <= 0 || delta != finalDelta)
            return false;

        var inkFeathersOffered = 0;
        string? relicId = null;
        string? relicName = null;
        string? relicRarity = null;
        string? archiveId = null;
        string? archiveTitle = null;
        string? archiveEntryType = null;
        string? archiveRarity = null;

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadStrictPositiveInt32(audit, "inkFeathersOffered", out inkFeathersOffered) ||
                !TryReadStrictNonNegativeInt32(audit, "capRemainingBefore", out _))
            {
                return false;
            }
        }
        else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadRequiredStrictString(audit, "relicId", out relicId) ||
                !TryReadRequiredStrictString(audit, "relicName", out relicName) ||
                !TryReadRequiredStrictString(audit, "relicRarity", out relicRarity) ||
                !GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(relicRarity))
            {
                return false;
            }
        }
        else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadRequiredStrictString(audit, "archiveId", out archiveId) ||
                !TryReadRequiredStrictString(audit, "archiveTitle", out archiveTitle) ||
                !TryReadRequiredStrictString(audit, "archiveEntryType", out archiveEntryType) ||
                !TryReadRequiredStrictString(audit, "archiveRarity", out archiveRarity) ||
                !AfterlifeArchiveState.OfferingTypeMatchesEntryType(offeringType, archiveEntryType) ||
                GetRarityRank(archiveRarity) == 0)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        strictEntry = new StrictOfferingJournalEntry(
            eventId,
            guardianId,
            delta,
            offeringType,
            returnCycleId,
            inkFeathersOffered,
            relicId,
            relicName,
            relicRarity,
            archiveId,
            archiveTitle,
            archiveEntryType,
            archiveRarity);
        return true;
    }

    private static bool StrictOfferingJournalEntryMatchesPendingRequest(
        StrictOfferingJournalEntry entry,
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request,
        int expectedGain,
        string? expectedEventId = null)
    {
        if (!string.IsNullOrWhiteSpace(expectedEventId) &&
            !string.Equals(entry.EventId, expectedEventId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(entry.GuardianId, request.GuardianId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entry.OfferingType, request.OfferingType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entry.ReturnCycleId, request.ReturnCycleId, StringComparison.OrdinalIgnoreCase) ||
            entry.Delta != expectedGain)
        {
            return false;
        }

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            return entry.InkFeathersOffered == request.InkFeathersOffered;

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(entry.RelicId, request.RelicId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(entry.RelicName, request.RelicName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(entry.RelicRarity, request.RelicRarity, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(entry.ArchiveId, request.ArchiveId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(entry.ArchiveTitle, request.ArchiveTitle, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(entry.ArchiveEntryType, request.ArchiveEntryType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(entry.ArchiveRarity, request.ArchiveRarity, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool TryReadRequiredStrictString(JsonElement root, string propName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;

        value = prop.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadStrictInt32(JsonElement root, string propName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number &&
               prop.TryGetInt32(out value);
    }

    private static bool TryReadStrictPositiveInt32(JsonElement root, string propName, out int value)
        => TryReadStrictInt32(root, propName, out value) && value > 0;

    private static bool TryReadStrictNonNegativeInt32(JsonElement root, string propName, out int value)
        => TryReadStrictInt32(root, propName, out value) && value >= 0;

    private SoulRelicProofReadResult ReadSoulRelicProofEntry(string? soulJson, string? relicId, bool strictCurrentShape)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Absent, null);
        if (string.IsNullOrWhiteSpace(soulJson))
            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Unreadable, null);

        if (strictCurrentShape)
            return ReadStrictCurrentSoulRelicProofEntry(soulJson, relicId);

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (!doc.RootElement.TryGetProperty("soulRelics", out var relics))
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            if (relics.ValueKind == JsonValueKind.Array)
            {
                foreach (var relic in relics.EnumerateArray())
                {
                    if (relic.ValueKind != JsonValueKind.Object)
                        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                    if (string.Equals(GetStringValue(relic, "relicId"), relicId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryReadSoulRelicProofEntry(relic, out var proofEntry))
                            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                        return new SoulRelicProofReadResult(
                            SoulStateEntryPresenceStatus.Present,
                            proofEntry);
                    }
                }
            }
            else if (relics.ValueKind == JsonValueKind.Object)
            {
                var hasKnownContainer = false;
                foreach (var propName in new[] { "stored", "equipped" })
                {
                    if (!relics.TryGetProperty(propName, out var arr))
                        continue;

                    hasKnownContainer = true;
                    if (arr.ValueKind != JsonValueKind.Array)
                        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                    foreach (var relic in arr.EnumerateArray())
                    {
                        if (relic.ValueKind != JsonValueKind.Object)
                            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                        if (string.Equals(GetStringValue(relic, "relicId"), relicId, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!TryReadSoulRelicProofEntry(relic, out var proofEntry))
                                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                            return new SoulRelicProofReadResult(
                                SoulStateEntryPresenceStatus.Present,
                                proofEntry);
                        }
                    }
                }

                if (!hasKnownContainer)
                    return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }
            else
            {
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }
        }
        catch (JsonException)
        {
            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Unreadable, null);
        }

        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Absent, null);
    }

    private SoulRelicProofReadResult ReadStrictCurrentSoulRelicProofEntry(string soulJson, string relicId)
    {
        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            var hasCanonicalTriggerLifeEnd = HasLifecycleAuthorizedCurrentTriggerLifeEndSync();

            if (!GuardianPolicyContracts.TryReadStrictCurrentSoulRelicCollections(
                    soulRoot,
                    hasCanonicalTriggerLifeEnd,
                    out var equipped,
                    out var stored,
                    out _))
            {
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }

            if (equipped == null || stored == null)
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            var rootElement = CloneJsonObjectToElement(soulRoot);
            if (!rootElement.TryGetProperty("soulRelics", out var soulRelics) ||
                !soulRelics.TryGetProperty("stored", out var storedElement) ||
                storedElement.ValueKind != JsonValueKind.Array ||
                !soulRelics.TryGetProperty("equipped", out var equippedElement) ||
                equippedElement.ValueKind != JsonValueKind.Array)
            {
                return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }

            foreach (var collection in new[] { storedElement, equippedElement })
            {
                foreach (var relic in collection.EnumerateArray())
                {
                    if (relic.ValueKind != JsonValueKind.Object)
                        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                    if (!string.Equals(GetStringValue(relic, "relicId"), relicId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!TryReadSoulRelicProofEntry(relic, out var proofEntry))
                        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                    return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Present, proofEntry);
                }
            }
        }
        catch (JsonException)
        {
            return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Unreadable, null);
        }

        return new SoulRelicProofReadResult(SoulStateEntryPresenceStatus.Absent, null);
    }

    private ArchiveProofReadResult ReadAfterlifeArchiveProofEntry(string? soulJson, string? archiveId)
    {
        if (string.IsNullOrWhiteSpace(archiveId))
            return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.Absent, null);
        if (string.IsNullOrWhiteSpace(soulJson))
            return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.Unreadable, null);

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (!doc.RootElement.TryGetProperty("afterlifeArchive", out var archive))
                return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            if (archive.ValueKind != JsonValueKind.Object)
            {
                return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }

            if (!archive.TryGetProperty("stored", out var stored))
                return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            if (archive.TryGetProperty("actionReceipts", out var actionReceipts) &&
                actionReceipts.ValueKind != JsonValueKind.Array)
            {
                return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);
            }

            if (stored.ValueKind != JsonValueKind.Array)
                return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

            foreach (var entry in stored.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                if (string.Equals(GetStringValue(entry, "archiveId"), archiveId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryReadArchiveProofEntry(entry, out var proofEntry))
                        return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.InvalidShape, null);

                    return new ArchiveProofReadResult(
                        SoulStateEntryPresenceStatus.Present,
                        proofEntry);
                }
            }
        }
        catch (JsonException)
        {
            return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.Unreadable, null);
        }

        return new ArchiveProofReadResult(SoulStateEntryPresenceStatus.Absent, null);
    }

    private bool TryReadSoulRelicProofEntry(JsonElement relic, out SoulRelicProofEntry entry)
    {
        entry = null!;
        var scratchIssues = new List<ValidationIssue>();
        ValidateMinimalSoulRelicObject(relic, "game_state/meta/soul_state.json.soulRelics", scratchIssues, "SoulState");
        if (scratchIssues.Count != 0)
            return false;

        var relicId = GetStringValue(relic, "relicId");
        var relicName = GetFirstNonEmptyString(relic, "name") ?? GetStringValue(relic, "relicName");
        var relicRarity = GetFirstNonEmptyString(relic, "rarity", "quality", "relicRarity");
        if (string.IsNullOrWhiteSpace(relicId) ||
            string.IsNullOrWhiteSpace(relicName) ||
            string.IsNullOrWhiteSpace(relicRarity) ||
            !GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(relicRarity))
        {
            return false;
        }

        entry = new SoulRelicProofEntry(relicId, relicName, relicRarity);
        return true;
    }

    private bool TryReadArchiveProofEntry(JsonElement archiveEntry, out ArchiveProofEntry entry)
    {
        entry = null!;
        var scratchIssues = new List<ValidationIssue>();
        ValidateAfterlifeArchiveEntryObject(archiveEntry, "game_state/meta/soul_state.json.afterlifeArchive.stored", scratchIssues);
        if (scratchIssues.Count != 0)
            return false;

        var archiveId = GetStringValue(archiveEntry, "archiveId");
        var archiveTitle = GetStringValue(archiveEntry, "title");
        var archiveEntryType = GetStringValue(archiveEntry, "entryType");
        var archiveRarity = GetStringValue(archiveEntry, "rarity");
        if (string.IsNullOrWhiteSpace(archiveId) ||
            string.IsNullOrWhiteSpace(archiveTitle) ||
            string.IsNullOrWhiteSpace(archiveEntryType) ||
            string.IsNullOrWhiteSpace(archiveRarity) ||
            !AfterlifeArchiveState.IsAllowedEntryType(archiveEntryType) ||
            GetRarityRank(archiveRarity) == 0)
        {
            return false;
        }

        entry = new ArchiveProofEntry(archiveId, archiveTitle, archiveEntryType, archiveRarity);
        return true;
    }

    private static bool SoulRelicRequestMatchesProofEntry(
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request,
        SoulRelicProofEntry entry)
    {
        return string.Equals(request.RelicId, entry.RelicId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.RelicName, entry.RelicName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.RelicRarity, entry.RelicRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ArchiveRequestMatchesProofEntry(
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request,
        ArchiveProofEntry entry)
    {
        return string.Equals(request.ArchiveId, entry.ArchiveId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.ArchiveTitle, entry.ArchiveTitle, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.ArchiveEntryType, entry.ArchiveEntryType, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(request.ArchiveRarity, entry.ArchiveRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeSoulRelicProofEntry(SoulRelicProofEntry? entry)
        => entry == null
            ? "missing Soul Relic metadata"
            : $"{entry.RelicId} / {entry.RelicName} / {entry.RelicRarity}";

    private static string DescribeArchiveProofEntry(ArchiveProofEntry? entry)
        => entry == null
            ? "missing archive metadata"
            : $"{entry.ArchiveId} / {entry.ArchiveTitle} / {entry.ArchiveEntryType} / {entry.ArchiveRarity}";

    private static bool HasListedAffectedFile(JsonElement stateEvidence, string relativePath)
    {
        if (!stateEvidence.TryGetProperty("affectedFiles", out var affectedFiles) || affectedFiles.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in affectedFiles.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
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

    private async Task<bool> SoulImprintHasSourceProvenanceAsync(
        string? imprintId,
        string? companionName,
        string? sourceCompanionId,
        string? sourceNpcId)
    {
        if (string.IsNullOrWhiteSpace(sourceCompanionId) && string.IsNullOrWhiteSpace(sourceNpcId))
            return false;

        var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("soulImprint", out var imprint) || imprint.ValueKind == JsonValueKind.Null)
                return false;

            if (imprint.ValueKind == JsonValueKind.Object)
            {
                return SoulImprintObjectMatchesSourceProvenance(
                    imprint,
                    imprintId,
                    companionName,
                    sourceCompanionId,
                    sourceNpcId);
            }

            if (imprint.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in imprint.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        SoulImprintObjectMatchesSourceProvenance(
                            item,
                            imprintId,
                            companionName,
                            sourceCompanionId,
                            sourceNpcId))
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

    private static bool SoulImprintObjectMatchesSourceProvenance(
        JsonElement imprint,
        string? imprintId,
        string? companionName,
        string? sourceCompanionId,
        string? sourceNpcId)
    {
        if (!SoulImprintObjectMatchesIdentity(imprint, imprintId, companionName))
            return false;

        if (!string.IsNullOrWhiteSpace(sourceCompanionId))
        {
            var currentCompanionId = GetFirstString(
                imprint,
                "sourceCompanionId",
                "companionId",
                "sourceCompanionRelicId");
            return string.Equals(currentCompanionId, sourceCompanionId, StringComparison.OrdinalIgnoreCase);
        }

        var currentNpcId = GetFirstString(
            imprint,
            "NPCId",
            "npcId",
            "sourceNpcId",
            "sourceNPCId");
        return !string.IsNullOrWhiteSpace(sourceNpcId) &&
               string.Equals(currentNpcId, sourceNpcId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SoulImprintObjectMatchesIdentity(JsonElement imprint, string? imprintId, string? companionName)
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

        return true;
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

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        var guardianIdentityContext = BuildGuardianReasoningIdentityContext(guardianPolicyContext);
        var structuredActorExtraction = await CollectStructuredActorUpdatesAsync(guardianPolicyContext);
        var structuredActorUpdates = structuredActorExtraction.Updates;
        ValidateStructuredActorUpdatesAgainstScope(scope, structuredActorUpdates, issues);

        var requiresGuardianScopeValidation = RequiresGuardianReasoningScopeValidation(
            scopeMode,
            scope,
            guardianIdentityContext,
            structuredActorUpdates,
            structuredActorExtraction.DirectCanonicalGuardianDiffRequiredButSnapshotMissing);

        var guardianScopeSnapshotContext = requiresGuardianScopeValidation
            ? await ResolveGuardianValidatedSnapshotContextAsync()
            : new GuardianValidatedSnapshotContext(ValidatedPendingTurnSnapshotStatus.Missing, null, null);

        if (requiresGuardianScopeValidation &&
            guardianScopeSnapshotContext.SnapshotStatus != ValidatedPendingTurnSnapshotStatus.Usable)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json",
                IssueSeverity.Error,
                "Guardian-sensitive reasoning требует current validated pending turn snapshot pre-turn realm.",
                code: "guardian_scope_invalid_validated_snapshot_context",
                section: "npc_scope",
                expected: "current validated pending turn snapshot with game_state/meta/soul_state.json",
                actual: DescribeValidatedPendingTurnSnapshotStatus(guardianScopeSnapshotContext.SnapshotStatus),
                repairHint: "Для guardian-centric и guardian-relevant mixed reasoning используй current validated pending turn snapshot с корректными sessionId/requestId/turnNumber и snapshot copy game_state/meta/soul_state.json."));
        }

        if (requiresGuardianScopeValidation &&
            structuredActorExtraction.DirectCanonicalGuardianDiffRequiredButSnapshotMissing)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Guardian reasoning не может подтвердить direct canonical guardians[] touches без validated pre-turn guardians snapshot.",
                code: "guardian_scope_missing_validated_guardians_snapshot",
                section: "npc_scope",
                expected: "current validated pending turn snapshot with game_state/meta/guardians.json",
                actual: "missing or unusable validated pre-turn guardians snapshot",
                repairHint: "Если reasoning опирается на direct canonical guardians[] state, сохрани validated pre-turn snapshot copy game_state/meta/guardians.json. Без этого guardian diff contract считается непроверяемым."));
        }

        if (requiresGuardianScopeValidation &&
            guardianIdentityContext.ActiveGuardianStatus == GuardianReasoningActiveGuardianStatus.GuardianStateUnreadable)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Guardian-sensitive reasoning не может быть проверен: guardians.json не читается как authoritative guardian state.",
                code: "guardian_scope_unreadable_guardian_state",
                section: "npc_scope",
                expected: "readable guardians.json with canonical guardian state",
                actual: "guardians.json unreadable",
                repairHint: "Исправь guardians.json и оставь readable canonical guardian state. Guardian reasoning не использует fallback на partial mirror-only context."));
        }

        if (scopeMode == ReasoningScopeMode.GuardianCentric &&
            guardianIdentityContext.ActiveGuardianStatus == GuardianReasoningActiveGuardianStatus.NoActiveGuardian)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json.activeGuardian",
                IssueSeverity.Error,
                "Guardian-centric reasoning требует current activeGuardian, синхронизированного с canonical guardians[].",
                code: "guardian_scope_missing_active_guardian",
                section: "npc_scope",
                expected: "activeGuardian strict mirror of a canonical guardians[] entry",
                actual: "missing activeGuardian",
                repairHint: "Для guardian-centric reasoning materialize activeGuardian как strict mirror текущего canonical guardian entry из guardians[]."));
        }

        if (requiresGuardianScopeValidation &&
            guardianIdentityContext.ActiveGuardianStatus == GuardianReasoningActiveGuardianStatus.MirrorMissingCanonical)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json.activeGuardian",
                IssueSeverity.Error,
                "Guardian-sensitive reasoning не может опираться на activeGuardian без matching canonical guardian entry в guardians[].",
                code: "guardian_scope_invalid_active_guardian_identity",
                section: "npc_scope",
                expected: "activeGuardian.guardianId matches an entry inside guardians[]",
                actual: "activeGuardian exists without canonical guardian backing entry",
                repairHint: "Используй activeGuardian только как strict mirror/selector canonical guardian entry из guardians[]. Не authorизуй reasoning через orphan или stale activeGuardian."));
        }

        var activeGuardianNames = guardianScopeSnapshotContext.SnapshotStatus == ValidatedPendingTurnSnapshotStatus.Usable &&
                                  !string.IsNullOrWhiteSpace(guardianScopeSnapshotContext.PreTurnRealm) &&
                                  IsChaosSeaRealm(guardianScopeSnapshotContext.PreTurnRealm!) &&
                                  guardianIdentityContext.ActiveGuardianStatus == GuardianReasoningActiveGuardianStatus.CanonicalResolved
            ? new HashSet<string>(guardianIdentityContext.ActiveGuardianNames, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mirrorOnlyGuardianActors = FindMirrorOnlyGuardianScopeActors(scope, guardianIdentityContext);
        foreach (var staleGuardianActor in mirrorOnlyGuardianActors)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json",
                IssueSeverity.Error,
                $"Guardian-sensitive reasoning использует stale mirror-only alias '{staleGuardianActor}' вместо canonical guardian name.",
                code: "guardian_scope_stale_active_guardian_alias",
                actor: staleGuardianActor,
                section: "npc_scope",
                expected: guardianIdentityContext.ActiveGuardianNames.Count > 0
                    ? string.Join(", ", guardianIdentityContext.ActiveGuardianNames.OrderBy(name => name))
                    : "canonical active guardian alias",
                actual: staleGuardianActor,
                repairHint: "Для guardian reasoning используй canonical guardian aliases из guardians[]; stale mirror-only names из activeGuardian недопустимы."));
        }
        if (scopeMode == ReasoningScopeMode.Mixed)
        {
            foreach (var rawGuardianIdActor in FindRawGuardianIdScopeActors(scope, guardianIdentityContext))
            {
                var expectedGuardianAliases = guardianIdentityContext.CanonicalGuardianAliasLookup.TryGetValue(rawGuardianIdActor, out var aliases) &&
                                             aliases.Count > 0
                    ? string.Join(", ", aliases.OrderBy(name => name))
                    : "canonical guardian alias";
                issues.Add(new ValidationIssue(
                    "output/debug_logs.json",
                    IssueSeverity.Error,
                    $"Guardian-sensitive mixed reasoning использует raw guardianId '{rawGuardianIdActor}' вместо canonical guardian alias.",
                    code: "guardian_scope_uses_raw_guardian_id",
                    actor: rawGuardianIdActor,
                    section: "npc_scope",
                    expected: expectedGuardianAliases,
                    actual: rawGuardianIdActor,
                    repairHint: "В Relevant actors и reasoning blocks используй canonical guardian name/alias из authoritative guardian baseline, а не transport guardianId."));
            }
        }
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

    private static IReadOnlyCollection<string> FindMirrorOnlyGuardianScopeActors(
        ReasoningScopeManifest scope,
        GuardianReasoningIdentityContext guardianIdentityContext)
    {
        if (guardianIdentityContext.ActiveGuardianStatus != GuardianReasoningActiveGuardianStatus.CanonicalResolved)
            return Array.Empty<string>();

        return scope.RelevantActors
            .Where(actor =>
                guardianIdentityContext.MirrorGuardianAliases.Contains(actor) &&
                !guardianIdentityContext.ActiveGuardianNames.Contains(actor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<string> FindRawGuardianIdScopeActors(
        ReasoningScopeManifest scope,
        GuardianReasoningIdentityContext guardianIdentityContext)
    {
        return scope.RelevantActors
            .Where(actor =>
                guardianIdentityContext.AuthoritativeGuardianIds.Contains(actor) &&
                !guardianIdentityContext.CanonicalGuardianAliases.Contains(actor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool RequiresGuardianReasoningScopeValidation(
        ReasoningScopeMode scopeMode,
        ReasoningScopeManifest scope,
        GuardianReasoningIdentityContext guardianIdentityContext,
        IReadOnlyCollection<StructuredActorUpdate> structuredActorUpdates,
        bool directCanonicalGuardianDiffRequiredButSnapshotMissing)
    {
        if (scopeMode == ReasoningScopeMode.GuardianCentric)
            return true;

        if (scopeMode != ReasoningScopeMode.Mixed)
            return false;

        if (directCanonicalGuardianDiffRequiredButSnapshotMissing)
            return true;

        if (structuredActorUpdates.Any(update => string.Equals(update.ActorType, "Guardian", StringComparison.OrdinalIgnoreCase)))
            return true;

        return scope.RelevantActors.Any(actor =>
            guardianIdentityContext.CanonicalGuardianAliases.Contains(actor) ||
            guardianIdentityContext.MirrorGuardianAliases.Contains(actor) ||
            guardianIdentityContext.AuthoritativeGuardianIds.Contains(actor));
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
            await ValidateDirectChaosSeaGachaOutcomeAsync(playerAction, issues);
            await ValidateChaosSeaTravelOutcomeAsync(playerAction, issues);
            await ValidateAbodeResidentRelicGrantOutcomeAsync(playerAction, issues);
            await ValidateAbodeResidentQuestRequestOutcomeAsync(playerAction, issues);

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

            var currentRealm = await ResolveAcceptedTurnInkFeatherRealmAsync(actionContext, issues);
            if (string.IsNullOrWhiteSpace(currentRealm))
                return issues;
            var isAfterlifeRealm = RealmSemantics.IsAfterlifeRealm(currentRealm);
            if (isAfterlifeRealm && !AfterlifeGmInkFeatherActions.Contains(actionContext.ActionTag))
            {
                issues.Add(new ValidationIssue(
                    "input/turn_request.json.playerAction",
                    IssueSeverity.Error,
                    $"INK_FEATHER_ACTION {actionContext.ActionTag} запрещён в текущем realm {currentRealm}",
                    code: "ink_feather_wrong_realm",
                    section: "INK_FEATHER_ACTION",
                    expected: string.Join(", ", AfterlifeGmInkFeatherActions.OrderBy(x => x)),
                    actual: actionContext.ActionTag,
                    repairHint: "В Chaos Sea и Shining Abode используй только afterlife Ink Feather whitelist."));
                return issues;
            }

            if (isAfterlifeRealm)
                await ValidateAfterlifeClientPrepaidInkFeatherBalanceAsync(actionContext, issues);

            if (!isAfterlifeRealm && !MortalWorldGmInkFeatherActions.Contains(actionContext.ActionTag))
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
        var json = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json");
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

            var preTurnJson = ReadValidatedCurrentPreTurnTrackedFileSync("game_state/player/skills_passive.json");
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

    private async Task ValidateAbodeResidentRelicGrantOutcomeAsync(string playerAction, List<ValidationIssue> issues)
    {
        if (!playerAction.Contains("[ABODE_RESIDENT_RELIC_GRANT]", StringComparison.OrdinalIgnoreCase))
            return;

        var residentId = ExtractChaosSeaTravelActionValue(playerAction, "residentId");
        if (string.IsNullOrWhiteSpace(residentId))
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json.playerAction",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен явно фиксировать residentId.",
                code: "abode_resident_relic_grant_missing_resident_id",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: "residentId=<afterlife resident id>",
                actual: playerAction,
                repairHint: "Передавай direct resident reward marker только с residentId, guardianId и abodeId из текущей Обители."));
            return;
        }

        var soulRoot = await ReadJsonObjectForSpecialActionAsync("game_state/meta/soul_state.json", "ABODE_RESIDENT_RELIC_GRANT", issues);
        var residentRoot = await ReadJsonObjectForSpecialActionAsync(GuardianAbodeResidentState.StatePath, "ABODE_RESIDENT_RELIC_GRANT", issues);
        var preTurnSoulRoot = await ReadPreTurnJsonObjectForSpecialActionAsync("game_state/meta/soul_state.json", "ABODE_RESIDENT_RELIC_GRANT", issues);
        var preTurnResidentRoot = await ReadPreTurnJsonObjectForSpecialActionAsync(GuardianAbodeResidentState.StatePath, "ABODE_RESIDENT_RELIC_GRANT", issues);
        if (soulRoot == null || residentRoot == null || preTurnSoulRoot == null || preTurnResidentRoot == null)
            return;

        GuardianAbodeResidentState.NormalizeShape(residentRoot);
        GuardianAbodeResidentState.NormalizeShape(preTurnResidentRoot);
        var preTurnRelicIds = EnumerateSoulRelicObjects(preTurnSoulRoot)
            .Select(GetSoulRelicId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchingRelics = EnumerateSoulRelicObjects(soulRoot)
            .Where(relic =>
                string.Equals(GetNodeString(relic["relicType"]), GuardianAbodeResidentState.RelicTypeCompanionEcho, StringComparison.OrdinalIgnoreCase) &&
                relic["companionSeed"] is JsonObject seed &&
                string.Equals(GetNodeString(seed["sourceResidentId"]), residentId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var matchingRelic = matchingRelics.FirstOrDefault(relic =>
        {
            var candidateRelicId = GetSoulRelicId(relic);
            return !string.IsNullOrWhiteSpace(candidateRelicId) &&
                   !preTurnRelicIds.Contains(candidateRelicId);
        });
        var relicId = GetSoulRelicId(matchingRelic);
        if (matchingRelics.Count == 0 || matchingRelics.All(relic => string.IsNullOrWhiteSpace(GetSoulRelicId(relic))))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен materialize-ить companion_echo Soul Relic для указанного resident.",
                code: "abode_resident_relic_grant_missing_companion_echo_relic",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: $"companion_echo relic with companionSeed.sourceResidentId={residentId}",
                actual: "missing",
                repairHint: "Добавь Soul Relic с relicType=companion_echo и complete companionSeed.sourceResidentId/sourceGuardianId/companionNameHint/originWorldSummary/futureCompanionPrompt."));
        }
        else if (matchingRelic == null || string.IsNullOrWhiteSpace(relicId))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен добавить новую companion_echo Soul Relic в текущем accepted turn, а не ссылаться на старую.",
                code: "abode_resident_relic_grant_no_new_companion_echo_relic",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: $"new companion_echo relic absent from pre-turn snapshot with companionSeed.sourceResidentId={residentId}",
                actual: "only pre-existing matching relics found",
                repairHint: "Добавь новую Soul Relic с новым relicId и companionSeed.sourceResidentId указанного resident; ранее выданная реликвия не закрывает новый direct marker."));
        }

        var resident = FindResidentNode(residentRoot, residentId);
        var preTurnResident = FindResidentNode(preTurnResidentRoot, residentId);
        if (preTurnResident == null)
        {
            issues.Add(new ValidationIssue(
                $"{GuardianAbodeResidentState.StatePath}.entries",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен применяться к resident, существовавшему до хода.",
                code: "abode_resident_relic_grant_missing_pre_turn_resident",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: $"pre-turn residentId={residentId}",
                actual: "missing pre-turn resident",
                repairHint: "Direct resident relic grant закрывает награду существующего resident; не создавай нового resident как доказательство этого marker."));
        }

        if (resident == null)
        {
            issues.Add(new ValidationIssue(
                $"{GuardianAbodeResidentState.StatePath}.entries",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен обновить существующего afterlife resident.",
                code: "abode_resident_relic_grant_missing_resident_update",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: $"residentId={residentId}",
                actual: "missing resident",
                repairHint: "Сохрани resident в guardian_abode_residents.json и установи bondRewardState=granted плюс grantedRelicId."));
        }
        else
        {
            var rewardState = GetNodeString(resident["bondRewardState"]);
            var grantedRelicId = GetNodeString(resident["grantedRelicId"]);
            var preTurnRewardState = GetNodeString(preTurnResident?["bondRewardState"]);
            var preTurnGrantedRelicId = GetNodeString(preTurnResident?["grantedRelicId"]);
            var alreadyGrantedPreTurn = string.Equals(preTurnRewardState, "granted", StringComparison.OrdinalIgnoreCase) &&
                                        !string.IsNullOrWhiteSpace(grantedRelicId) &&
                                        string.Equals(preTurnGrantedRelicId, grantedRelicId, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(rewardState, "granted", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(grantedRelicId) ||
                (!string.IsNullOrWhiteSpace(relicId) && !string.Equals(grantedRelicId, relicId, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ValidationIssue(
                    $"{GuardianAbodeResidentState.StatePath}.entries[].bondRewardState",
                    IssueSeverity.Error,
                    "ABODE_RESIDENT_RELIC_GRANT должен отметить resident reward как granted и связать его с новой реликвией.",
                    code: "abode_resident_relic_grant_resident_reward_not_granted",
                    section: "ABODE_RESIDENT_RELIC_GRANT",
                    expected: string.IsNullOrWhiteSpace(relicId) ? "bondRewardState=granted and grantedRelicId=<new relic id>" : $"bondRewardState=granted and grantedRelicId={relicId}",
                    actual: $"bondRewardState={rewardState ?? "missing"}, grantedRelicId={grantedRelicId ?? "missing"}",
                    repairHint: "Обнови того же resident через UpdateGuardianAbodeResidents/entries: bondRewardState=granted и grantedRelicId=id companion_echo relic."));
            }
            else if (alreadyGrantedPreTurn)
            {
                issues.Add(new ValidationIssue(
                    $"{GuardianAbodeResidentState.StatePath}.entries[].bondRewardState",
                    IssueSeverity.Error,
                    "ABODE_RESIDENT_RELIC_GRANT должен перевести resident reward state в текущем accepted turn, а не переиспользовать уже выданную награду.",
                    code: "abode_resident_relic_grant_no_current_turn_resident_transition",
                    section: "ABODE_RESIDENT_RELIC_GRANT",
                    expected: "pre-turn resident reward was not already granted to the current relic id",
                    actual: $"pre-turn bondRewardState={preTurnRewardState}, grantedRelicId={preTurnGrantedRelicId}",
                    repairHint: "Не закрывай новый resident reward marker старым resident state; текущий ход должен выдать новую реликвию и обновить resident state."));
            }
        }

        if (!ResidentInteractionLogContainsNewEntry(residentRoot, preTurnResidentRoot, residentId))
        {
            issues.Add(new ValidationIssue(
                $"{GuardianAbodeResidentState.StatePath}.{GuardianAbodeResidentState.InteractionLogProperty}",
                IssueSeverity.Error,
                "ABODE_RESIDENT_RELIC_GRANT должен оставить новую residentInteractionLogUpdates/interactionLog память о даровании реликвии в текущем accepted turn.",
                code: "abode_resident_relic_grant_missing_new_interaction_log",
                section: "ABODE_RESIDENT_RELIC_GRANT",
                expected: $"new interaction log entry for residentId={residentId} absent from pre-turn snapshot",
                actual: "missing new entry",
                repairHint: "Добавь новую residentInteractionLogUpdates запись с entryId, residentId, title, summary, turn и timestamp."));
        }
    }

    private async Task ValidateAbodeResidentQuestRequestOutcomeAsync(string playerAction, List<ValidationIssue> issues)
    {
        if (!playerAction.Contains("[ABODE_RESIDENT_QUEST_REQUEST]", StringComparison.OrdinalIgnoreCase))
            return;

        var residentId = ExtractChaosSeaTravelActionValue(playerAction, "residentId");
        if (string.IsNullOrWhiteSpace(residentId))
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json.playerAction",
                IssueSeverity.Error,
                "ABODE_RESIDENT_QUEST_REQUEST должен явно фиксировать residentId.",
                code: "abode_resident_quest_request_missing_resident_id",
                section: "ABODE_RESIDENT_QUEST_REQUEST",
                expected: "residentId=<afterlife resident id>",
                actual: playerAction,
                repairHint: "Передавай direct resident quest marker только с residentId, guardianId и abodeId из текущей Обители."));
            return;
        }

        var residentRoot = await ReadJsonObjectForSpecialActionAsync(GuardianAbodeResidentState.StatePath, "ABODE_RESIDENT_QUEST_REQUEST", issues);
        var questsRoot = await ReadJsonObjectForSpecialActionAsync("game_state/quests/soul_quests.json", "ABODE_RESIDENT_QUEST_REQUEST", issues);
        var preTurnResidentRoot = await ReadPreTurnJsonObjectForSpecialActionAsync(GuardianAbodeResidentState.StatePath, "ABODE_RESIDENT_QUEST_REQUEST", issues);
        var preTurnQuestsRoot = await ReadPreTurnJsonObjectForSpecialActionAsync("game_state/quests/soul_quests.json", "ABODE_RESIDENT_QUEST_REQUEST", issues);
        if (residentRoot == null || questsRoot == null || preTurnResidentRoot == null || preTurnQuestsRoot == null)
            return;

        GuardianAbodeResidentState.NormalizeShape(residentRoot);
        GuardianAbodeResidentState.NormalizeShape(preTurnResidentRoot);
        var quest = EnumerateQuestObjects(questsRoot).FirstOrDefault(candidate =>
            string.Equals(GetNodeString(candidate["relatedAfterlifeResidentId"]), residentId, StringComparison.OrdinalIgnoreCase));
        var questId = GetNodeString(quest?["questId"]) ?? GetNodeString(quest?["id"]);
        if (quest == null || string.IsNullOrWhiteSpace(questId))
        {
            issues.Add(new ValidationIssue(
                "game_state/quests/soul_quests.json.UpdateSoulQuests",
                IssueSeverity.Error,
                "ABODE_RESIDENT_QUEST_REQUEST должен создать или обновить Soul Quest, связанный с afterlife resident.",
                code: "abode_resident_quest_request_missing_linked_soul_quest",
                section: "ABODE_RESIDENT_QUEST_REQUEST",
                expected: $"Soul Quest with relatedAfterlifeResidentId={residentId}",
                actual: "missing",
                repairHint: "Запиши UpdateSoulQuests/quests с questId, title/description/objectives и relatedAfterlifeResidentId того resident."));
        }
        else
        {
            var preTurnQuest = EnumerateQuestObjects(preTurnQuestsRoot).FirstOrDefault(candidate =>
                string.Equals(GetNodeString(candidate["questId"]) ?? GetNodeString(candidate["id"]), questId, StringComparison.OrdinalIgnoreCase));
            if (preTurnQuest != null && JsonNode.DeepEquals(preTurnQuest, quest))
            {
                issues.Add(new ValidationIssue(
                    "game_state/quests/soul_quests.json.UpdateSoulQuests",
                    IssueSeverity.Error,
                    "ABODE_RESIDENT_QUEST_REQUEST должен создать или продвинуть resident-linked Soul Quest в текущем accepted turn, а не переиспользовать старое состояние.",
                    code: "abode_resident_quest_request_no_current_turn_quest_change",
                    section: "ABODE_RESIDENT_QUEST_REQUEST",
                    expected: $"new or changed Soul Quest for residentId={residentId} absent from pre-turn snapshot",
                    actual: $"questId={questId} unchanged from pre-turn snapshot",
                    repairHint: "Создай новый Soul Quest с новым questId или измени существующий resident-linked quest через UpdateSoulQuests/objectives/progress/status."));
            }
        }

        if (!ResidentInteractionLogContainsNewEntry(residentRoot, preTurnResidentRoot, residentId))
        {
            issues.Add(new ValidationIssue(
                $"{GuardianAbodeResidentState.StatePath}.{GuardianAbodeResidentState.InteractionLogProperty}",
                IssueSeverity.Error,
                "ABODE_RESIDENT_QUEST_REQUEST должен оставить новую residentInteractionLogUpdates/interactionLog память о просьбе или принятом пути в текущем accepted turn.",
                code: "abode_resident_quest_request_missing_new_interaction_log",
                section: "ABODE_RESIDENT_QUEST_REQUEST",
                expected: $"new interaction log entry for residentId={residentId} absent from pre-turn snapshot",
                actual: "missing new entry",
                repairHint: "Добавь новую residentInteractionLogUpdates запись с entryId, residentId, title, summary, turn и timestamp."));
        }
    }

    private async Task<JsonObject?> ReadJsonObjectForSpecialActionAsync(
        string path,
        string section,
        List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                $"{section} требует readable canonical state file {path}.",
                code: $"{section.ToLowerInvariant()}_missing_state_file",
                section: section,
                expected: "JSON object",
                actual: "missing or empty",
                repairHint: $"Запиши {path} как JSON object с canonical state, требуемым для {section}."));
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                $"{section} требует readable JSON object in {path}.",
                code: $"{section.ToLowerInvariant()}_invalid_state_json",
                section: section,
                expected: "valid JSON object",
                actual: "invalid JSON",
                repairHint: $"Исправь {path}; special action validator не может доказать результат поверх malformed JSON."));
            return null;
        }
    }

    private async Task<JsonObject?> ReadPreTurnJsonObjectForSpecialActionAsync(
        string path,
        string section,
        List<ValidationIssue> issues)
    {
        var json = await ReadValidatedCurrentPreTurnTrackedFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                $"{section} требует validated pre-turn snapshot для доказательства current-turn изменения.",
                code: $"{section.ToLowerInvariant()}_missing_pre_turn_snapshot",
                section: section,
                expected: $"validated pre-turn snapshot for {path}",
                actual: "missing or empty",
                repairHint: "Accepted-turn validation должна иметь pending_turn_snapshot entry/hash для этого файла; без baseline нельзя принять stale no-op as valid result."));
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                $"{section} требует readable validated pre-turn JSON object in {path}.",
                code: $"{section.ToLowerInvariant()}_invalid_pre_turn_snapshot_json",
                section: section,
                expected: "valid pre-turn JSON object",
                actual: "invalid JSON",
                repairHint: "Исправь validated pre-turn snapshot; special action validator не может доказать same-turn delta поверх malformed baseline."));
            return null;
        }
    }

    private static IEnumerable<JsonObject> EnumerateSoulRelicObjects(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                    yield return relic;
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
                yield return relic;
        }
    }

    private static string? GetSoulRelicId(JsonObject? relic) =>
        GetNodeString(relic?["relicId"]) ?? GetNodeString(relic?["id"]);

    private static JsonObject? FindResidentNode(JsonObject residentRoot, string residentId)
    {
        if (residentRoot[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return null;

        return entries
            .OfType<JsonObject>()
            .FirstOrDefault(resident =>
                string.Equals(GetNodeString(resident["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ResidentInteractionLogContainsNewEntry(JsonObject residentRoot, JsonObject preTurnResidentRoot, string residentId)
    {
        var preTurnSignatures = EnumerateResidentInteractionLogEntries(preTurnResidentRoot, residentId)
            .Select(BuildResidentInteractionLogSignature)
            .ToHashSet(StringComparer.Ordinal);

        return EnumerateResidentInteractionLogEntries(residentRoot, residentId)
            .Select(BuildResidentInteractionLogSignature)
            .Any(signature => !preTurnSignatures.Contains(signature));
    }

    private static IEnumerable<JsonObject> EnumerateResidentInteractionLogEntries(JsonObject residentRoot, string residentId)
    {
        foreach (var propertyName in new[] { GuardianAbodeResidentState.InteractionLogProperty, GuardianAbodeResidentState.UpdateInteractionLogProperty })
        {
            if (residentRoot[propertyName] is not JsonArray log)
                continue;

            foreach (var entry in log.OfType<JsonObject>())
                if (string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))
                    yield return entry;
        }
    }

    private static string BuildResidentInteractionLogSignature(JsonObject entry)
    {
        var entryId = GetNodeString(entry["entryId"]);
        return !string.IsNullOrWhiteSpace(entryId)
            ? $"entryId:{entryId}"
            : entry.DeepClone().ToJsonString();
    }

    private static IEnumerable<JsonObject> EnumerateQuestObjects(JsonObject questsRoot)
    {
        foreach (var propertyName in new[] { "quests", "UpdateSoulQuests" })
        {
            if (questsRoot[propertyName] is not JsonArray quests)
                continue;

            foreach (var quest in quests.OfType<JsonObject>())
                yield return quest;
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

