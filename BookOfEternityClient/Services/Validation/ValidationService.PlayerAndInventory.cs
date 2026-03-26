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
    private async Task ValidatePlayerStateFiles(List<ValidationIssue> issues)
    {
        await ValidatePlayerFile("game_state/core/player_status.json", issues);
        await ValidatePlayerFile("game_state/player/experience.json", issues);
        await ValidatePlayerFile("game_state/player/status_changes.json", issues);
        await ValidatePlayerFile("game_state/player/effects.json", issues);
        await ValidatePlayerFile("game_state/player/wounds.json", issues);
        await ValidatePlayerFile("game_state/player/custom_states.json", issues);
        await ValidatePlayerFile("game_state/player/stealth.json", issues);
        await ValidatePlayerFile("game_state/player/weight_calc.json", issues);
        await ValidatePlayerFile("game_state/player/transformation.json", issues);
        await ValidatePlayerFile("game_state/misc/characteristics.json", issues);
        await ValidatePlayerContractFile("game_state/player/skills_active.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "activeSkillChanges", "removeActiveSkills"
            }, issues);
        await ValidatePlayerContractFile("game_state/player/skills_passive.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "passiveSkillChanges", "removePassiveSkills"
            }, issues);
        await ValidatePlayerContractFile("game_state/player/skill_mastery.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "skillMasteryChanges"
            }, issues);
        await ValidatePlayerContractFile("game_state/inventory/items.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UpdateInventory", "items", "equipmentChanges", "equipment", "equippedItems", "money", "resources",
                "totalWeight", "maxWeight", "isOverloaded"
            }, issues);
        await ValidateFlexibleStateFile("game_state/inventory/item_resources.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "inventoryItemsResources", "entries"
            }, issues, ValidateInventoryItemResourcesStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/inventory/item_resources.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "entries"
            }, issues);
        await ValidateFlexibleStateFile("game_state/inventory/item_text_updates.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "updateItemTextContents", "entries"
            }, issues, ValidateInventoryItemTextStateFile);
        await ValidatePlayerContractFile("game_state/inventory/item_movements.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "moveInventoryItems"
            }, issues);
        await ValidatePlayerContractFile("game_state/inventory/item_removals.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "removeInventoryItems"
            }, issues);
        await ValidateFlexibleStateFile("game_state/inventory/item_bonds.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "itemBondLevelChanges", "itemFateCardUnlocks", "entries"
            }, issues, ValidateInventoryItemBondsStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/inventory/item_bonds.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "entries"
            }, issues);
        await ValidatePlayerContractFile("game_state/inventory/recipes.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "addOrUpdateRecipes", "removeRecipes"
            }, issues);
        await ValidatePlayerContractFile("game_state/inventory/storage_operations.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "moveToLocationStorage", "retrieveFromLocationStorage"
            }, issues);
    }

    private async Task ValidatePlayerContractFile(string filePath, HashSet<string> allowedKeys, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Файл должен иметь корневой JSON object",
                    code: "player_contract_invalid_root",
                    section: "PlayerContractFile",
                    expected: "JSON object",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Сохрани {filePath} как JSON object с допустимыми top-level ключами: {string.Join(", ", allowedKeys.OrderBy(x => x))}."));
                return;
            }

            var visibleProps = doc.RootElement.EnumerateObject()
                .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (visibleProps.Count > 0 && !visibleProps.Any(prop => allowedKeys.Contains(prop.Name)))
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Файл не содержит ни одного допустимого top-level ключа для player contract",
                    code: "player_contract_missing_allowed_top_level_key",
                    section: "PlayerContractFile",
                    expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                    actual: string.Join(", ", visibleProps.Select(prop => prop.Name)),
                    repairHint: "Используй canonical top-level command names для этого player state file и не подменяй их произвольными alias-ами."));
                return;
            }

            foreach (var prop in visibleProps)
            {
                if (!allowedKeys.Contains(prop.Name))
                {
                    issues.Add(new ValidationIssue(
                        $"{filePath}.{prop.Name}",
                        IssueSeverity.Error,
                        $"Недопустимый top-level ключ: {prop.Name}",
                        code: "player_contract_unknown_top_level_key",
                        section: "PlayerContractFile",
                        expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                        actual: prop.Name,
                        repairHint: "Удали неподдерживаемый top-level ключ и используй только canonical player contract surfaces для этого файла."));
                }
            }

            ValidatePlayerContract(doc.RootElement, filePath, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                $"Невалидный JSON: {ex.Message}",
                code: "player_contract_invalid_json",
                section: "PlayerContractFile",
                expected: "valid JSON object",
                actual: "invalid JSON",
                repairHint: $"Исправь {filePath} до валидного JSON-объекта, не меняя его player contract."));
        }
    }

    private async Task<List<ValidationIssue>> ValidateAcceptedTurnQteOfferInternalAsync()
    {
        var issues = new List<ValidationIssue>();
        var offerJson = await _fs.ReadFileAsync(QteSceneService.QteOfferPath);
        if (string.IsNullOrWhiteSpace(offerJson))
            return issues;

        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();

        var qteEnabled = await ReadQteEnabledAsync();
        if (!qteEnabled)
        {
            issues.Add(new ValidationIssue(
                QteSceneService.QteOfferPath,
                IssueSeverity.Error,
                "QTE offer запрещён: qteEventsEnabled = false в настройках клиента",
                code: "qte_disabled_by_settings",
                section: "QTE",
                expected: "qteEventsEnabled = true",
                actual: "false"));
            return issues;
        }

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        if (!string.IsNullOrWhiteSpace(preTurnRealm) && IsChaosSeaRealm(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                QteSceneService.QteOfferPath,
                IssueSeverity.Error,
                "QTE offer разрешён только в Mortal World",
                code: "qte_wrong_realm",
                section: "QTE",
                expected: "Mortal World",
                actual: preTurnRealm));
            return issues;
        }

        if (manifest == null)
        {
            issues.Add(new ValidationIssue(
                QteSceneService.QteOfferPath,
                IssueSeverity.Error,
                "QTE offer требует pending turn manifest обычного игрокского хода",
                code: "qte_missing_pending_manifest",
                section: "QTE",
                expected: PendingTurnSnapshotManifestPath,
                actual: "missing"));
        }
        else
        {
            if (!QteSceneService.IsEligibleOfferSourceLabel(manifest.SourceLabel))
            {
                issues.Add(new ValidationIssue(
                    QteSceneService.QteOfferPath,
                    IssueSeverity.Error,
                    "QTE offer разрешён только на обычном Mortal World ходу игрока, а не на system/transition flow",
                    code: "qte_invalid_turn_context",
                    section: "QTE",
                    expected: QteSceneService.OrdinaryPlayerTurnSourceLabel,
                    actual: manifest.SourceLabel ?? "missing"));
            }

            var changedTrackedFiles = await GetChangedTrackedFilesAgainstManifestAsync(manifest);
            foreach (var changedFile in changedTrackedFiles
                         .Where(path => !AllowedQteOfferOnlyOutputFiles.Contains(path) &&
                                        !string.Equals(path, InkFeatherActionResultPath, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ValidationIssue(
                    changedFile,
                    IssueSeverity.Error,
                    "QTE-offer turn не должен одновременно менять обычное состояние мира/игрока/NPC. Разрешены только output/narrative_response.json, output/interface_updates.json, output/debug_logs.json и output/qte_offer.json.",
                    code: "qte_offer_mixed_turn_state_mutation",
                    section: "QTE",
                    repairHint: "Убери обычные state changes из этого хода и оставь только QTE offer + UI/output-описание."));
            }

            if (await DidFileChangeAgainstManifestAsync(manifest, InkFeatherActionResultPath))
            {
                issues.Add(new ValidationIssue(
                    InkFeatherActionResultPath,
                    IssueSeverity.Error,
                    "QTE-offer turn не может одновременно резолвить Ink Feather action или другой отдельный sidecar outcome.",
                    code: "qte_offer_forbidden_sidecar_output",
                    section: "QTE",
                    repairHint: "Раздели QTE offer и обычный outcome на разные GM turns."));
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(offerJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    QteSceneService.QteOfferPath,
                    IssueSeverity.Error,
                    "qte_offer.json должен быть JSON object",
                    code: "qte_offer_invalid_root",
                    section: "QTE",
                    expected: "JSON object",
                    actual: root.ValueKind.ToString(),
                    repairHint: "Сохрани output/qte_offer.json как JSON object перед заполнением qteId, chapters и terminalOutcomes."));
                return issues;
            }

            var missingOfferFields = GetMissingRequiredNonEmptyStringProperties(root, "qteId", "title", "offerText", "introNarrative", "startChapterId");
            if (missingOfferFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    QteSceneService.QteOfferPath,
                    IssueSeverity.Error,
                    "QTE offer не содержит обязательные корневые текстовые поля",
                    code: "qte_missing_required_root_fields",
                    section: "QTE",
                    expected: "Non-empty qteId, title, offerText, introNarrative, startChapterId",
                    actual: string.Join(", ", missingOfferFields),
                    repairHint: "Заполни корневые поля QTE offer перед детализацией chapters/terminalOutcomes. Без них клиент не сможет корректно показать и идентифицировать сцену."));
                return issues;
            }

            var qteId = GetFirstNonEmptyString(root, "qteId") ?? string.Empty;
            var startChapterId = GetFirstNonEmptyString(root, "startChapterId") ?? string.Empty;

            if (!root.TryGetProperty("chapters", out var chapters))
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.chapters",
                    IssueSeverity.Error,
                    "QTE offer должен содержать chapters array",
                    code: "qte_chapters_missing",
                    section: "QTE",
                    expected: "chapters array",
                    actual: "missing",
                    repairHint: "Передай хотя бы одну главу сцены в chapters[] до проверки startChapterId и routing graph."));
                return issues;
            }
            if (chapters.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.chapters",
                    IssueSeverity.Error,
                    "QTE offer должен содержать chapters array",
                    code: "qte_chapters_invalid_shape",
                    section: "QTE",
                    expected: "chapters array",
                    actual: chapters.ValueKind.ToString(),
                    repairHint: "Используй chapters как массив объектов глав, а не как scalar/object другого типа."));
                return issues;
            }
            if (!root.TryGetProperty("terminalOutcomes", out var terminalOutcomes))
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.terminalOutcomes",
                    IssueSeverity.Error,
                    "QTE offer должен содержать terminalOutcomes array",
                    code: "qte_terminal_outcomes_missing",
                    section: "QTE",
                    expected: "terminalOutcomes array",
                    actual: "missing",
                    repairHint: "Передай хотя бы один terminal outcome до проверки routing targets."));
                return issues;
            }
            if (terminalOutcomes.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.terminalOutcomes",
                    IssueSeverity.Error,
                    "QTE offer должен содержать terminalOutcomes array",
                    code: "qte_terminal_outcomes_invalid_shape",
                    section: "QTE",
                    expected: "terminalOutcomes array",
                    actual: terminalOutcomes.ValueKind.ToString(),
                    repairHint: "Используй terminalOutcomes как массив terminal outcome objects."));
                return issues;
            }

            var chaptersEmpty = chapters.GetArrayLength() == 0;
            if (chaptersEmpty)
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.chapters",
                    IssueSeverity.Error,
                    "QTE offer должен содержать хотя бы одну главу",
                    code: "qte_empty_chapters",
                    section: "QTE",
                    expected: ">= 1 chapter",
                    actual: "0",
                    repairHint: "Сначала добавь хотя бы одну главу в chapters[], а уже потом указывай startChapterId и routing graph."));
            }
            var terminalOutcomesEmpty = terminalOutcomes.GetArrayLength() == 0;
            if (terminalOutcomesEmpty)
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.terminalOutcomes",
                    IssueSeverity.Error,
                    "QTE offer должен содержать хотя бы один terminal outcome",
                    code: "qte_empty_terminal_outcomes",
                    section: "QTE",
                    expected: ">= 1 terminal outcome",
                    actual: "0",
                    repairHint: "Сначала добавь хотя бы один terminal outcome, а уже потом направляй routing.*.terminalOutcomeId на outcome graph."));
            }
            if (chaptersEmpty || terminalOutcomesEmpty)
                return issues;

            var chapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outcomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chapterElements = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var outcomeElements = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            var chapterIndex = 0;
            foreach (var chapter in chapters.EnumerateArray())
            {
                var chapterContext = $"{QteSceneService.QteOfferPath}.chapters[{chapterIndex++}]";
                if (!RequireObject(chapter, chapterContext, issues))
                    continue;

                var missingChapterFields = GetMissingRequiredNonEmptyStringProperties(chapter, "chapterId", "narrative");
                if (missingChapterFields.Count > 0)
                {
                    issues.Add(new ValidationIssue(
                        chapterContext,
                        IssueSeverity.Error,
                        "QTE chapter не содержит обязательные корневые поля",
                        code: "qte_chapter_missing_required_fields",
                        section: "QTE",
                        expected: "Non-empty chapterId and narrative",
                        actual: string.Join(", ", missingChapterFields),
                        repairHint: "Заполни chapterId и narrative до проверки actions/routing. Без них глава не может участвовать в графе QTE."));
                    continue;
                }

                var chapterId = GetFirstNonEmptyString(chapter, "chapterId") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(chapterId) && !chapterIds.Add(chapterId))
                {
                    issues.Add(new ValidationIssue(
                        $"{chapterContext}.chapterId",
                        IssueSeverity.Error,
                        "chapterId должен быть уникальным",
                        code: "qte_duplicate_chapter_id",
                        section: "QTE",
                        actual: chapterId));
                }
                else if (!string.IsNullOrWhiteSpace(chapterId))
                {
                    chapterElements[chapterId] = chapter;
                }

                if (!chapter.TryGetProperty("actions", out var actions))
                {
                    issues.Add(new ValidationIssue(
                        $"{chapterContext}.actions",
                        IssueSeverity.Error,
                        "QTE chapter должен содержать actions array",
                        code: "qte_chapter_actions_missing",
                        section: "QTE",
                        expected: "actions array",
                        actual: "missing",
                        repairHint: "Добавь хотя бы одно action в chapter.actions[] до детализации check/routing."));
                    continue;
                }
                if (actions.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new ValidationIssue(
                        $"{chapterContext}.actions",
                        IssueSeverity.Error,
                        "QTE chapter должен содержать actions array",
                        code: "qte_chapter_actions_invalid_shape",
                        section: "QTE",
                        expected: "actions array",
                        actual: actions.ValueKind.ToString(),
                        repairHint: "Используй actions как массив action objects внутри главы."));
                    continue;
                }
                if (actions.GetArrayLength() == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{chapterContext}.actions",
                        IssueSeverity.Error,
                        "QTE chapter должен содержать хотя бы одно действие",
                        code: "qte_empty_actions",
                        section: "QTE",
                        expected: ">= 1 action",
                        actual: "0",
                        repairHint: "Добавь хотя бы одно действие в actions[] этой главы."));
                    continue;
                }

                var actionIndex = 0;
                foreach (var action in actions.EnumerateArray())
                {
                    var actionContext = $"{chapterContext}.actions[{actionIndex++}]";
                    if (!RequireObject(action, actionContext, issues))
                        continue;

                    RequireString(action, actionContext, issues, "actionId");
                    RequireString(action, actionContext, issues, "label");
                    if (!action.TryGetProperty("check", out var check))
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check",
                            IssueSeverity.Error,
                            "QTE action должен содержать check object",
                            code: "qte_action_check_missing",
                            section: "QTE"));
                        continue;
                    }
                    if (!RequireObject(check, $"{actionContext}.check", issues))
                        continue;

                    var checkType = RequireString(check, $"{actionContext}.check", issues, "type");
                    if (!check.TryGetProperty("baseDifficulty", out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check.baseDifficulty",
                            IssueSeverity.Error,
                            "QTE check должен содержать baseDifficulty",
                            code: "qte_check_base_difficulty_missing",
                            section: "QTE"));
                    }
                    ValidatePositiveNumberField(check, $"{actionContext}.check", issues, "baseDifficulty");
                    if (TryReadInt(check, "baseDifficulty", out var baseDifficulty) &&
                        (baseDifficulty < 1 || baseDifficulty > 5))
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check.baseDifficulty",
                            IssueSeverity.Error,
                            "QTE baseDifficulty должен быть в диапазоне 1..5",
                            code: "qte_invalid_base_difficulty_range",
                            section: "QTE",
                            expected: "1..5",
                            actual: baseDifficulty.ToString()));
                    }
                    var primaryCharacteristic = RequireString(check, $"{actionContext}.check", issues, "primaryCharacteristic");
                    if (!string.IsNullOrWhiteSpace(checkType) &&
                        checkType is not "BranchChoice" and not "TimingBar" and not "PromptChain" and not "BalanceMeter" and not "ChargeRelease")
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check.type",
                            IssueSeverity.Error,
                            "Неподдерживаемый QTE check type",
                            code: "qte_invalid_check_type",
                            section: "QTE",
                            expected: "BranchChoice | TimingBar | PromptChain | BalanceMeter | ChargeRelease",
                            actual: checkType));
                    }

                    if (!string.IsNullOrWhiteSpace(primaryCharacteristic) &&
                        !Characteristics.All.Contains(primaryCharacteristic, StringComparer.Ordinal))
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check.primaryCharacteristic",
                            IssueSeverity.Error,
                            "QTE primaryCharacteristic должен быть одним из canonical lowercase ids характеристик",
                            code: "qte_invalid_primary_characteristic",
                            section: "QTE",
                            expected: string.Join(" | ", Characteristics.All),
                            actual: primaryCharacteristic,
                            repairHint: "Используй canonical lowercase key из системы характеристик, например dexterity или speed."));
                    }

                    if (string.Equals(checkType, "BranchChoice", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!check.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object ||
                            !config.TryGetProperty("choiceGrade", out var choiceGrade) || choiceGrade.ValueKind != JsonValueKind.String)
                        {
                            issues.Add(new ValidationIssue(
                                $"{actionContext}.check.config.choiceGrade",
                                IssueSeverity.Error,
                                "BranchChoice требует check.config.choiceGrade",
                                code: "qte_branch_choice_grade_missing",
                                section: "QTE"));
                        }
                        else
                        {
                            var choiceGradeValue = choiceGrade.GetString() ?? string.Empty;
                            if (!AllowedQteChoiceGrades.Contains(choiceGradeValue))
                            {
                                issues.Add(new ValidationIssue(
                                    $"{actionContext}.check.config.choiceGrade",
                                    IssueSeverity.Error,
                                    "BranchChoice choiceGrade должен быть одним из strict enum значений success | partial | fail",
                                    code: "qte_branch_choice_grade_invalid",
                                    section: "QTE",
                                    expected: "success | partial | fail",
                                    actual: choiceGradeValue,
                                    repairHint: "Используй точное lowercase значение без лишних пробелов."));
                            }
                        }
                    }

                    if (!action.TryGetProperty("routing", out var routing))
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.routing",
                            IssueSeverity.Error,
                            "QTE action должен содержать routing object",
                            code: "qte_action_routing_missing",
                            section: "QTE"));
                        continue;
                    }
                    if (!RequireObject(routing, $"{actionContext}.routing", issues))
                        continue;

                    foreach (var branchName in new[] { "success", "partial", "fail" })
                    {
                        if (!routing.TryGetProperty(branchName, out var branch))
                        {
                            issues.Add(new ValidationIssue(
                                $"{actionContext}.routing.{branchName}",
                                IssueSeverity.Error,
                                $"QTE routing обязан содержать branch object '{branchName}'",
                                code: "qte_missing_required_branch",
                                section: "QTE"));
                            continue;
                        }
                        if (!RequireObject(branch, $"{actionContext}.routing.{branchName}", issues))
                            continue;

                        var nextChapterId = GetFirstNonEmptyString(branch, "nextChapterId");
                        var terminalOutcomeId = GetFirstNonEmptyString(branch, "terminalOutcomeId");
                        var hasNext = !string.IsNullOrWhiteSpace(nextChapterId);
                        var hasOutcome = !string.IsNullOrWhiteSpace(terminalOutcomeId);
                        if (hasNext == hasOutcome)
                        {
                            issues.Add(new ValidationIssue(
                                $"{actionContext}.routing.{branchName}",
                                IssueSeverity.Error,
                                "Каждая QTE branch routing должна указывать ровно один target: nextChapterId или terminalOutcomeId",
                                code: "qte_invalid_branch_target",
                                section: "QTE"));
                        }
                    }
                }
            }

            var outcomeIndex = 0;
            foreach (var outcome in terminalOutcomes.EnumerateArray())
            {
                var outcomeContext = $"{QteSceneService.QteOfferPath}.terminalOutcomes[{outcomeIndex++}]";
                if (!RequireObject(outcome, outcomeContext, issues))
                    continue;

                var missingOutcomeFields = GetMissingRequiredNonEmptyStringProperties(outcome, "outcomeId", "title", "finalNarrative", "gmSummary");
                if (missingOutcomeFields.Count > 0)
                {
                    issues.Add(new ValidationIssue(
                        outcomeContext,
                        IssueSeverity.Error,
                        "QTE terminalOutcome не содержит обязательные корневые поля",
                        code: "qte_terminal_outcome_missing_required_fields",
                        section: "QTE",
                        expected: "Non-empty outcomeId, title, finalNarrative, gmSummary",
                        actual: string.Join(", ", missingOutcomeFields),
                        repairHint: "Сначала заполни корневые поля terminal outcome, и только потом responseFragment и доп. метаданные."));
                    continue;
                }

                var outcomeId = GetFirstNonEmptyString(outcome, "outcomeId") ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(outcomeId) && !outcomeIds.Add(outcomeId))
                {
                    issues.Add(new ValidationIssue(
                        $"{outcomeContext}.outcomeId",
                        IssueSeverity.Error,
                        "outcomeId должен быть уникальным",
                        code: "qte_duplicate_outcome_id",
                        section: "QTE",
                        actual: outcomeId));
                }
                else if (!string.IsNullOrWhiteSpace(outcomeId))
                {
                    outcomeElements[outcomeId] = outcome;
                }

                if (!outcome.TryGetProperty("responseFragment", out var responseFragment) || responseFragment.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new ValidationIssue(
                        $"{outcomeContext}.responseFragment",
                        IssueSeverity.Error,
                        "terminalOutcome должен содержать responseFragment object для локального разрешения сцены",
                        code: "qte_terminal_response_fragment_missing",
                        section: "QTE"));
                }
                else
                {
                    foreach (var fragmentIssue in ValidateResponse(responseFragment))
                    {
                        issues.Add(new ValidationIssue(
                            $"{outcomeContext}.responseFragment.{fragmentIssue.FilePath}",
                            fragmentIssue.Severity,
                            fragmentIssue.Message,
                            fragmentIssue.Code,
                            fragmentIssue.Actor,
                            "QTE",
                            fragmentIssue.Expected,
                            fragmentIssue.Actual,
                            fragmentIssue.RepairHint));
                    }

                    if (responseFragment.TryGetProperty("image_prompt", out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{outcomeContext}.responseFragment.image_prompt",
                            IssueSeverity.Error,
                            "QTE responseFragment не может использовать обычный image_prompt channel. Для QTE используй sceneImagePrompt / chapterImagePrompt / outcomeImagePrompt.",
                            code: "qte_response_fragment_image_prompt_forbidden",
                            section: "QTE",
                            repairHint: "Перенеси изображение в outcomeImagePrompt или chapterImagePrompt и убери image_prompt из responseFragment."));
                    }
                }
            }

            foreach (var chapter in chapters.EnumerateArray())
            {
                if (!chapter.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var action in actions.EnumerateArray())
                {
                    if (!action.TryGetProperty("routing", out var routing) || routing.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var branchName in new[] { "success", "partial", "fail" })
                    {
                        if (!routing.TryGetProperty(branchName, out var branch) || branch.ValueKind != JsonValueKind.Object)
                            continue;

                        var nextChapterId = GetFirstNonEmptyString(branch, "nextChapterId");
                        var terminalOutcomeId = GetFirstNonEmptyString(branch, "terminalOutcomeId");
                        if (!string.IsNullOrWhiteSpace(nextChapterId) && !chapterIds.Contains(nextChapterId))
                            issues.Add(new ValidationIssue(
                                $"{QteSceneService.QteOfferPath}.chapters",
                                IssueSeverity.Error,
                                $"QTE branch указывает на неизвестный chapterId '{nextChapterId}'",
                                code: "qte_unknown_branch_chapter_target",
                                section: "QTE",
                                expected: "Existing chapterId from qte_offer.json.chapters[]",
                                actual: nextChapterId,
                                repairHint: "Исправь nextChapterId в routing или добавь соответствующую главу в chapters[]."));
                        if (!string.IsNullOrWhiteSpace(terminalOutcomeId) && !outcomeIds.Contains(terminalOutcomeId))
                            issues.Add(new ValidationIssue(
                                $"{QteSceneService.QteOfferPath}.terminalOutcomes",
                                IssueSeverity.Error,
                                $"QTE branch указывает на неизвестный outcomeId '{terminalOutcomeId}'",
                                code: "qte_unknown_branch_outcome_target",
                                section: "QTE",
                                expected: "Existing outcomeId from qte_offer.json.terminalOutcomes[]",
                                actual: terminalOutcomeId,
                                repairHint: "Исправь terminalOutcomeId в routing или добавь соответствующий terminal outcome в terminalOutcomes[]."));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(startChapterId) && !chapterIds.Contains(startChapterId))
            {
                issues.Add(new ValidationIssue(
                    $"{QteSceneService.QteOfferPath}.startChapterId",
                    IssueSeverity.Error,
                    "QTE startChapterId должен ссылаться на существующий chapterId",
                    code: "qte_invalid_start_chapter_id",
                    section: "QTE",
                    actual: startChapterId));
            }

            var runtimeJson = await _fs.ReadFileAsync(QteSceneService.QteRuntimePath);
            if (!string.IsNullOrWhiteSpace(runtimeJson))
            {
                try
                {
                    using var runtimeDoc = JsonDocument.Parse(runtimeJson);
                    if (runtimeDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var declinedId = GetFirstNonEmptyString(runtimeDoc.RootElement, "lastDeclinedQteId");
                        if (!string.IsNullOrWhiteSpace(qteId) &&
                            !string.IsNullOrWhiteSpace(declinedId) &&
                            string.Equals(qteId, declinedId, StringComparison.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(
                                QteSceneService.QteOfferPath,
                                IssueSeverity.Error,
                                $"QTE offer {qteId} уже был отклонён и не может быть предложен повторно до обычного разрешения сцены",
                                code: "qte_reoffered_after_decline",
                                section: "QTE",
                                actual: qteId));
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Игнорируется повреждённый qte_runtime.json во время QTE-offer validation");
                }
            }

            if (!string.IsNullOrWhiteSpace(startChapterId) && chapterIds.Contains(startChapterId))
            {
                var reachableChapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectReachableQteChapterIds(startChapterId, chapterElements, reachableChapterIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (var chapterId in chapterIds.Where(id => !reachableChapterIds.Contains(id)))
                {
                    issues.Add(new ValidationIssue(
                        $"{QteSceneService.QteOfferPath}.chapters",
                        IssueSeverity.Error,
                        $"QTE chapter '{chapterId}' недостижим из startChapterId '{startChapterId}'",
                        code: "qte_unreachable_chapter",
                        section: "QTE",
                        repairHint: "Исправь routing граф или startChapterId, чтобы каждая глава сцены была достижима."));
                }
            }

            var successOutcomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(startChapterId))
                CollectQteSuccessOutcomeIds(startChapterId, chapterElements, successOutcomeIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            if (successOutcomeIds.Count == 0)
            {
                issues.Add(new ValidationIssue(
                    QteSceneService.QteOfferPath,
                    IssueSeverity.Error,
                    "QTE offer должен иметь хотя бы один terminal outcome, достижимый по success-веткам",
                    code: "qte_missing_success_terminal_path",
                    section: "QTE"));
            }

            foreach (var successOutcomeId in successOutcomeIds)
            {
                if (!outcomeElements.TryGetValue(successOutcomeId, out var outcome))
                    continue;
                if (!outcome.TryGetProperty("responseFragment", out var responseFragment) || responseFragment.ValueKind != JsonValueKind.Object)
                    continue;

                if (!TryReadInt(responseFragment, "experienceGained", out var experienceGained) || experienceGained <= 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{QteSceneService.QteOfferPath}.terminalOutcomes[{successOutcomeId}].responseFragment.experienceGained",
                        IssueSeverity.Error,
                        "Успешный terminal outcome QTE должен как минимум давать положительный experienceGained",
                        code: "qte_success_outcome_requires_xp",
                        section: "QTE",
                        expected: "> 0",
                        actual: responseFragment.TryGetProperty("experienceGained", out var xpNode) ? xpNode.ToString() : "missing"));
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                QteSceneService.QteOfferPath,
                IssueSeverity.Error,
                $"Невалидный JSON QTE offer: {ex.Message}",
                code: "qte_offer_invalid_json",
                section: "QTE"));
        }

        return issues;
    }

    private async Task ValidatePlayerFile(string filePath, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (filePath.EndsWith("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase))
            {
                await ValidateFileFields(filePath,
                    new[] { "healthPercentage", "energyPercentage", "poisePercentage", "currentCondition", "money" }, issues);
                ValidatePercentageField(doc.RootElement, "healthPercentage", issues);
                ValidatePercentageField(doc.RootElement, "energyPercentage", issues);
                ValidatePercentageField(doc.RootElement, "poisePercentage", issues);
                if (doc.RootElement.TryGetProperty("money", out _))
                    ValidateNonNegativeNumberField(doc.RootElement, filePath, issues, "money");
                return;
            }

            ValidatePlayerContract(doc.RootElement, filePath, issues);

            if (filePath.EndsWith("game_state/misc/characteristics.json", StringComparison.OrdinalIgnoreCase))
                ValidateBaseCharacteristicsFile(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/experience.json", StringComparison.OrdinalIgnoreCase))
                ValidateExperienceFile(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/effects.json", StringComparison.OrdinalIgnoreCase))
                ValidateEffectsContainer(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/wounds.json", StringComparison.OrdinalIgnoreCase))
                ValidateWoundsContainer(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/custom_states.json", StringComparison.OrdinalIgnoreCase))
                ValidateCustomStatesContainer(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/stealth.json", StringComparison.OrdinalIgnoreCase))
                ValidateStealthState(doc.RootElement, filePath, issues);
            else if (filePath.EndsWith("game_state/player/weight_calc.json", StringComparison.OrdinalIgnoreCase))
                ValidateWeightData(doc.RootElement, filePath, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                filePath, IssueSeverity.Error,
                $"Невалидный JSON: {ex.Message}"));
        }
    }

    private void ValidatePlayerContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidatePlayerStatus(root, contextPrefix, issues);
        ValidatePlayerChangeNumber(root, contextPrefix, issues, "currentPoiseChange");
        ValidatePlayerChangeNumber(root, contextPrefix, issues, "currentEnergyChange");
        ValidatePlayerChangeNumber(root, contextPrefix, issues, "currentHealthChange");
        ValidatePlayerChangeNumber(root, contextPrefix, issues, "experienceGained");
        ValidatePlayerChangeNumber(root, contextPrefix, issues, "moneyChange");
        ValidatePlayerStatArray(root, contextPrefix, issues, "statsIncreased");
        ValidatePlayerStatArray(root, contextPrefix, issues, "statsDecreased");
        ValidateSetCharacteristics(root, contextPrefix, issues);
        ValidatePlayerSkillChanges(root, contextPrefix, issues, "activeSkillChanges");
        ValidatePlayerSkillChanges(root, contextPrefix, issues, "passiveSkillChanges");
        ValidatePlayerSkillNameArray(root, contextPrefix, issues, "removeActiveSkills");
        ValidatePlayerSkillNameArray(root, contextPrefix, issues, "removePassiveSkills");
        ValidatePlayerSkillMastery(root, contextPrefix, issues);
        ValidatePlayerInventoryCommands(root, contextPrefix, issues, "UpdateInventory");
        ValidatePlayerInventoryCommands(root, contextPrefix, issues, "items");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "inventoryItemsResources");
        ValidateItemTextUpdateCommands(root, contextPrefix, issues);
        ValidateMoveInventoryItems(root, contextPrefix, issues);
        ValidateRemoveInventoryItems(root, contextPrefix, issues);
        ValidateEquipmentChanges(root, contextPrefix, issues);
        if (root.TryGetProperty("equippedItems", out var equippedItems))
            ValidatePlayerEquippedItemsObject(equippedItems, $"{contextPrefix}.equippedItems", issues);
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "itemBondLevelChanges");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "itemFateCardUnlocks");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "addOrUpdateRecipes");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "removeRecipes");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "moveToLocationStorage");
        ValidatePlayerInventoryArray(root, contextPrefix, issues, "retrieveFromLocationStorage");
        ValidateEffectsProperty(root, contextPrefix, issues, "playerActiveEffectsChanges");
        ValidateWoundsProperty(root, contextPrefix, issues, "playerWoundChanges");
        ValidateCustomStatesProperty(root, contextPrefix, issues, "customStateChanges");
        ValidateStealthProperty(root, contextPrefix, issues, "playerStealthStateChange");
        ValidateEffortTracker(root, contextPrefix, issues);
        ValidateWeightProperty(root, contextPrefix, issues, "calculatedWeightData");
        ValidateOptionalString(root, contextPrefix, issues, "playerAppearanceChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerRaceChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerRaceDescriptionChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerClassChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerClassDescriptionChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerCharacterNameChange");
        ValidateOptionalString(root, contextPrefix, issues, "playerAutoCombatSkillChange");
    }

    private void ValidatePlayerStatus(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("playerStatus", out var status))
            return;

        var context = $"{contextPrefix}.playerStatus";
        if (!RequireObject(status, context, issues))
            return;

        RequireString(status, context, issues, "healthPercentage");
        RequireString(status, context, issues, "energyPercentage");
        RequireString(status, context, issues, "poisePercentage");
        RequireString(status, context, issues, "currentCondition");
        ValidateOptionalString(status, context, issues, "currentConditionDescription");

        if (status.TryGetProperty("activeConditions", out var activeConditions))
            RequireArrayOfStrings(activeConditions, $"{context}.activeConditions", issues);
    }

    private void ValidatePlayerChangeNumber(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Number)
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            "Поле должно быть числом"));
    }

    private void ValidatePlayerStatArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
                "Поле должно быть массивом строк"));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()) ||
                !Characteristics.All.Contains(item.GetString() ?? "", StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue($"{contextPrefix}.{propName}[{index}]",
                    IssueSeverity.Error, "Элемент должен быть строкой с допустимым именем характеристики"));
            }
            index++;
        }
    }

    private void ValidateSetCharacteristics(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("setCharacteristics", out var value))
            return;

        var context = $"{contextPrefix}.setCharacteristics";
        if (!RequireObject(value, context, issues))
            return;

        foreach (var prop in value.EnumerateObject())
        {
            if (!Characteristics.All.Contains(prop.Name, StringComparer.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{prop.Name}",
                    IssueSeverity.Error,
                    "setCharacteristics использует неподдерживаемое имя характеристики",
                    code: "set_characteristics_invalid_key",
                    section: "Characteristics",
                    expected: string.Join(" | ", Characteristics.All),
                    actual: prop.Name,
                    repairHint: "Используй в setCharacteristics только English lowercase system names вроде strength или wisdom."));
                continue;
            }

            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{prop.Name}",
                    IssueSeverity.Error,
                    "setCharacteristics должен присваивать integer value",
                    code: "set_characteristics_non_integer_value",
                    section: "Characteristics",
                    expected: "integer new value",
                    actual: prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.ToString() : prop.Value.ValueKind.ToString(),
                    repairHint: "Передавай в setCharacteristics только новые целые значения характеристик без строк и дробей."));
            }
        }
    }

    private async Task ValidateGuardianResonancePowerEventsAsync(List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (manifest == null)
            return;

        var preJournalJson = await ReadPreTurnTrackedFileAsync(GuardianPowerEventState.JournalPath);
        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var newResonanceEntries = CollectNewGuardianPowerJournalEntries(preJournalJson, postJournalJson)
            .Where(entry => string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(manifest.SourceLabel))
        {
            foreach (var entry in newResonanceEntries)
            {
                issues.Add(new ValidationIssue(
                    GuardianPowerEventState.JournalPath,
                    IssueSeverity.Error,
                    "resonance power event допустим только на отдельном Life Evaluation turn",
                    code: "guardian_resonance_wrong_turn_context",
                    section: "LifeEvaluation",
                    expected: "no new resonance events outside life evaluation",
                    actual: GetFirstNonEmptyString(entry, "eventId") ?? "unknown resonance event",
                    repairHint: "Не эмить guardianPowerEvents.reasonType=resonance вне отдельного Life Evaluation turn."));
            }

            return;
        }

        foreach (var group in newResonanceEntries.GroupBy(entry => GetFirstNonEmptyString(entry, "guardianId") ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1)
                continue;

            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "За одну завершённую жизнь допустим максимум один resonance power event на одного Хранителя",
                code: "guardian_resonance_duplicate_for_same_life",
                section: "LifeEvaluation",
                expected: "at most one resonance event per guardian per completed life",
                actual: $"{group.Key}: {group.Count()} resonance events",
                repairHint: "Не дублируй resonance для одного и того же Хранителя в рамках одной оценки жизни."));
        }
    }

    private void ValidatePlayerSkillChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var isActiveSkillArray = string.Equals(propName, "activeSkillChanges", StringComparison.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            if (isActiveSkillArray)
                ValidateActiveSkillObject(item, itemContext, issues);
            else
                ValidatePassiveSkillObject(item, itemContext, issues);
        }
    }

    private void ValidateActiveSkillObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "skillName");
        RequireString(item, itemContext, issues, "skillDescription");
        RequireString(item, itemContext, issues, "rarity");
        ValidateSkillActionCostField(item, itemContext, issues, "actionCost", "Skills.Active");

        if (!item.TryGetProperty("combatEffect", out var combatEffect))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.combatEffect",
                IssueSeverity.Error,
                "Active Skill Object должен содержать combatEffect",
                code: "active_skill_missing_combat_effect",
                section: "Skills.Active",
                expected: "combatEffect object",
                actual: "missing combatEffect",
                repairHint: "Передай полный Active Skill Object с combatEffect object по canonical rules contract."));
        }
        else if (!RequireObject(combatEffect, $"{itemContext}.combatEffect", issues))
        {
            // RequireObject already emitted a root-cause error.
        }
        else
        {
            ValidateCombatActionObject(
                combatEffect,
                $"{itemContext}.combatEffect",
                issues,
                requireActionNameForActivatedEffect: false,
                section: "Skills.Active");

            if (!combatEffect.TryGetProperty("isActivatedEffect", out var isActivatedEffect))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.combatEffect.isActivatedEffect",
                    IssueSeverity.Error,
                    "combatEffect для active skill должен явно указывать isActivatedEffect=true",
                    code: "active_skill_missing_activation_flag",
                    section: "Skills.Active",
                    expected: "true",
                    actual: "missing",
                    repairHint: "Для active skill сохрани canonical combatEffect с isActivatedEffect=true."));
            }
            else if (isActivatedEffect.ValueKind != JsonValueKind.True)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.combatEffect.isActivatedEffect",
                    IssueSeverity.Error,
                    "combatEffect для active skill должен иметь isActivatedEffect=true",
                    code: "active_skill_invalid_activation_flag",
                    section: "Skills.Active",
                    expected: "true",
                    actual: isActivatedEffect.ValueKind.ToString(),
                    repairHint: "Active skill должен использовать canonical activated combatEffect с isActivatedEffect=true."));
            }
        }

        if (item.TryGetProperty("scalingCharacteristic", out var scalingCharacteristic) &&
            scalingCharacteristic.ValueKind != JsonValueKind.Null)
        {
            if (scalingCharacteristic.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(scalingCharacteristic.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.scalingCharacteristic",
                    IssueSeverity.Error,
                    "scalingCharacteristic должен быть непустой строкой или null",
                    code: "active_skill_invalid_scaling_characteristic",
                    section: "Skills.Active",
                    repairHint: "Укажи canonical характеристику строкой или явно передай null, если навык не масштабируется от характеристики."));
            }
            else
            {
                var characteristic = scalingCharacteristic.GetString() ?? string.Empty;
                if (!Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.scalingCharacteristic",
                        IssueSeverity.Error,
                        "scalingCharacteristic должен быть одной из canonical характеристик",
                        code: "active_skill_unknown_scaling_characteristic",
                        section: "Skills.Active",
                        expected: string.Join(" | ", Characteristics.All),
                        actual: characteristic,
                        repairHint: "Используй scalingCharacteristic только из canonical списка характеристик игрового контракта."));
                }
            }
        }

        ValidateOptionalBool(item, itemContext, issues, "scalesValue");
        ValidateOptionalBool(item, itemContext, issues, "scalesDuration");
        ValidateOptionalBool(item, itemContext, issues, "scalesChance");
        ValidateOptionalNumberOrString(item, itemContext, issues, "energyCost");
        ValidateOptionalNumberOrString(item, itemContext, issues, "cooldownTurns");
        ValidateOptionalNumberOrString(item, itemContext, issues, "timeCost");
    }

    private void ValidatePassiveSkillObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "skillName");
        RequireString(item, itemContext, issues, "skillDescription");
        RequireString(item, itemContext, issues, "rarity");
        var passiveType = RequireString(item, itemContext, issues, "type");
        RequireString(item, itemContext, issues, "group");
        if (!item.TryGetProperty("masteryLevel", out _))
            issues.Add(new ValidationIssue(
                $"{itemContext}.masteryLevel",
                IssueSeverity.Error,
                "Отсутствует обязательное поле passive skill: masteryLevel",
                code: "passive_skill_missing_mastery_level",
                section: "Skills.Passive",
                expected: "integer masteryLevel",
                actual: "missing",
                repairHint: "Добавь в passive skill canonical поле masteryLevel как целое число."));
        else
            ValidateIntegerField(item, itemContext, issues, "masteryLevel");
        if (!item.TryGetProperty("maxMasteryLevel", out _))
            issues.Add(new ValidationIssue(
                $"{itemContext}.maxMasteryLevel",
                IssueSeverity.Error,
                "Отсутствует обязательное поле passive skill: maxMasteryLevel",
                code: "passive_skill_missing_max_mastery_level",
                section: "Skills.Passive",
                expected: "integer maxMasteryLevel",
                actual: "missing",
                repairHint: "Добавь в passive skill canonical поле maxMasteryLevel как целое число."));
        else
            ValidateIntegerField(item, itemContext, issues, "maxMasteryLevel");

        if (!string.IsNullOrWhiteSpace(passiveType) && !AllowedPassiveSkillTypes.Contains(passiveType))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.type",
                IssueSeverity.Error,
                "Passive skill type должен быть одним из canonical enum значений",
                code: "passive_skill_invalid_type",
                section: "Skills.Passive",
                expected: string.Join(" | ", AllowedPassiveSkillTypes),
                actual: passiveType,
                repairHint: "Используй passive skill type только из contract enum: KnowledgeBased, CharacteristicBonus, BodyModification, CombatEnhancement, Utility."));
        }

        if (!item.TryGetProperty("structuredBonuses", out var structuredBonuses))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.structuredBonuses",
                IssueSeverity.Error,
                "Passive Skill Object должен содержать structuredBonuses как canonical primary bonus source",
                code: "passive_skill_missing_structured_bonuses",
                section: "Skills.Passive",
                expected: "structuredBonuses array or null",
                actual: "missing structuredBonuses",
                repairHint: "Добавь поле structuredBonuses в Passive Skill Object. Для purely narrative skill можешь явно передать null."));
        }
        else if (structuredBonuses.ValueKind != JsonValueKind.Null)
        {
            RequireArrayOfObjects(structuredBonuses, $"{itemContext}.structuredBonuses", issues);
        }

        if (item.TryGetProperty("playerStatBonus", out var playerStatBonus) &&
            playerStatBonus.ValueKind != JsonValueKind.Null &&
            (playerStatBonus.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(playerStatBonus.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerStatBonus",
                IssueSeverity.Error,
                "playerStatBonus должен быть непустой строкой или null",
                code: "passive_skill_invalid_player_stat_bonus",
                section: "Skills.Passive",
                repairHint: "Если используешь playerStatBonus, передай display-only summary строкой; иначе явно передай null."));
        }

        if (structuredBonuses.ValueKind == JsonValueKind.Array &&
            structuredBonuses.GetArrayLength() > 0 &&
            (!item.TryGetProperty("playerStatBonus", out var playerStatBonusMirror) ||
             playerStatBonusMirror.ValueKind != JsonValueKind.String ||
             string.IsNullOrWhiteSpace(playerStatBonusMirror.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerStatBonus",
                IssueSeverity.Error,
                "Passive skill с mechanical structuredBonuses должен содержать playerStatBonus summary",
                code: "passive_skill_missing_player_stat_bonus_mirror",
                section: "Skills.Passive",
                expected: "non-empty playerStatBonus when structuredBonuses are present",
                actual: !item.TryGetProperty("playerStatBonus", out _) ? "missing" : "null/empty",
                repairHint: "Если passive skill несёт mechanical structuredBonuses, сохрани рядом непустой playerStatBonus как canonical summary для UI и rules contract."));
        }

        if (item.TryGetProperty("combatEffect", out var combatEffect))
        {
            if (RequireObject(combatEffect, $"{itemContext}.combatEffect", issues))
            {
                ValidateCombatActionObject(
                    combatEffect,
                    $"{itemContext}.combatEffect",
                    issues,
                    requireActionNameForActivatedEffect: false,
                    section: "Skills.Passive");

                if (combatEffect.TryGetProperty("isActivatedEffect", out var isActivatedEffect) &&
                    isActivatedEffect.ValueKind == JsonValueKind.True)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.combatEffect.isActivatedEffect",
                        IssueSeverity.Error,
                        "Passive skill combatEffect не должен быть activated effect",
                        code: "passive_skill_invalid_activation_flag",
                        section: "Skills.Passive",
                        expected: "false or omitted",
                        actual: "true",
                        repairHint: "Для passive skill оставь isActivatedEffect=false или не указывай его вовсе."));
                }
            }
        }

        ValidateOptionalString(item, itemContext, issues, "effectDetails");
        ValidateOptionalString(item, itemContext, issues, "knowledgeDomain");
        ValidateOptionalNumberOrString(item, itemContext, issues, "unlockedActiveSkillsCount");
        ValidateOptionalNumberOrString(item, itemContext, issues, "maxUnlockableActiveSkills");
    }

    private void ValidateSkillActionCostField(JsonElement item, string itemContext, List<ValidationIssue> issues, string propName,
        string section)
    {
        if (!item.TryGetProperty(propName, out var actionCostNode))
            return;

        if (actionCostNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(actionCostNode.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть непустой строкой",
                code: "skill_invalid_action_cost_type",
                section: section));
            return;
        }

        var actionCost = actionCostNode.GetString() ?? string.Empty;
        if (!AllowedCombatActionCosts.Contains(actionCost))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть одним из canonical enum значений",
                code: "skill_invalid_action_cost",
                section: section,
                expected: string.Join(" | ", AllowedCombatActionCosts),
                actual: actionCost,
                repairHint: "Используй actionCost только из canonical skill contract: Main, Fast или Free."));
        }
    }

    private void ValidatePlayerSkillNameArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        RequireArrayOfStrings(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidatePlayerSkillMastery(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "skillMasteryChanges", $"{contextPrefix}.skillMasteryChanges", issues, out var arr))
            return;

        var knownActiveSkills = ReadCurrentPlayerActiveSkillNamesSync();
        knownActiveSkills.UnionWith(ParsePlayerSkillNames(
            ReadPreTurnTrackedFileSync("game_state/player/skills_active.json"),
            "activeSkillChanges"));
        if (root.TryGetProperty("removeActiveSkills", out var removeActiveSkills) &&
            removeActiveSkills.ValueKind == JsonValueKind.Array)
        {
            foreach (var removedSkill in removeActiveSkills.EnumerateArray())
            {
                if (removedSkill.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(removedSkill.GetString()))
                {
                    knownActiveSkills.Remove(removedSkill.GetString()!);
                }
            }
        }
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.skillMasteryChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;
            var skillName = RequireString(item, itemContext, issues, "skillName");
            if (!item.TryGetProperty("newMasteryLevel", out _))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newMasteryLevel",
                    IssueSeverity.Error,
                    "skillMasteryChanges требует поле newMasteryLevel",
                    code: "skill_mastery_change_missing_new_mastery_level",
                    section: "Skills.Mastery",
                    expected: "integer newMasteryLevel",
                    actual: "missing",
                    repairHint: "Добавь в skillMasteryChanges canonical поле newMasteryLevel как целое число."));
            else
                ValidateIntegerField(item, itemContext, issues, "newMasteryLevel");
            if (!item.TryGetProperty("newCurrentMasteryProgress", out _))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newCurrentMasteryProgress",
                    IssueSeverity.Error,
                    "skillMasteryChanges требует поле newCurrentMasteryProgress",
                    code: "skill_mastery_change_missing_current_progress",
                    section: "Skills.Mastery",
                    expected: "integer newCurrentMasteryProgress",
                    actual: "missing",
                    repairHint: "Добавь в skillMasteryChanges canonical поле newCurrentMasteryProgress как целое число."));
            else
                ValidateIntegerField(item, itemContext, issues, "newCurrentMasteryProgress");
            if (!item.TryGetProperty("newMasteryProgressNeeded", out _))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newMasteryProgressNeeded",
                    IssueSeverity.Error,
                    "skillMasteryChanges требует поле newMasteryProgressNeeded",
                    code: "skill_mastery_change_missing_progress_needed",
                    section: "Skills.Mastery",
                    expected: "integer newMasteryProgressNeeded",
                    actual: "missing",
                    repairHint: "Добавь в skillMasteryChanges canonical поле newMasteryProgressNeeded как целое число."));
            else
                ValidateIntegerField(item, itemContext, issues, "newMasteryProgressNeeded");
            RequireBooleanField(item, itemContext, issues, "masteryLeveledUp");

            if (!string.IsNullOrWhiteSpace(skillName) && !knownActiveSkills.Contains(skillName))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.skillName",
                    IssueSeverity.Error,
                    "skillMasteryChanges не может ссылаться на навык, которого нет в canonical active skills state",
                    code: "skill_mastery_unknown_active_skill",
                    section: "Skills.Active",
                    expected: "existing skillName from game_state/player/skills_active.json",
                    actual: skillName,
                    repairHint: "Сохраняй mastery только для реально существующих active skills. Если навык получен в этом же ходу, сначала добавь его в skills_active.json."));
            }

            if (TryReadInt(item, "newMasteryLevel", out var newMasteryLevel) && newMasteryLevel <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newMasteryLevel",
                    IssueSeverity.Error,
                    "newMasteryLevel должен быть положительным целым числом",
                    code: "skill_mastery_non_positive_level",
                    section: "Skills.Active",
                    expected: "> 0",
                    actual: newMasteryLevel.ToString(),
                    repairHint: "Используй в skillMasteryChanges только положительные mastery levels начиная с 1."));
            }

            if (TryReadInt(item, "newCurrentMasteryProgress", out var newCurrentProgress) && newCurrentProgress < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newCurrentMasteryProgress",
                    IssueSeverity.Error,
                    "newCurrentMasteryProgress не может быть отрицательным",
                    code: "skill_mastery_negative_progress",
                    section: "Skills.Active",
                    expected: ">= 0",
                    actual: newCurrentProgress.ToString(),
                    repairHint: "Сохраняй mastery progress как неотрицательное число очков прогресса."));
            }

            if (TryReadInt(item, "newMasteryProgressNeeded", out var newProgressNeeded) && newProgressNeeded <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newMasteryProgressNeeded",
                    IssueSeverity.Error,
                    "newMasteryProgressNeeded должен быть положительным целым числом",
                    code: "skill_mastery_non_positive_progress_needed",
                    section: "Skills.Active",
                    expected: "> 0",
                    actual: newProgressNeeded.ToString(),
                    repairHint: "Сохраняй mastery threshold как положительное число очков, необходимых до следующего уровня."));
            }
        }
    }

    private void ValidatePlayerInventoryArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (propName == "removeRecipes" && value.ValueKind == JsonValueKind.Array)
        {
            RequireArrayOfStrings(value, $"{contextPrefix}.{propName}", issues);
            return;
        }

        RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        if (string.Equals(propName, "itemFateCardUnlocks", StringComparison.OrdinalIgnoreCase))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var itemContext = $"{contextPrefix}.{propName}[{index++}]";
                if (!RequireObject(item, itemContext, issues))
                    continue;

                RequireString(item, itemContext, issues, "itemId");
                RequireString(item, itemContext, issues, "cardId");
                RequireString(item, itemContext, issues, "cardName");
            }
        }
    }

    private void ValidatePlayerInventoryCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var preTurnInventoryItemIds = ReadPreTurnInventoryItemIdsSync();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            if (string.Equals(propName, "items", StringComparison.OrdinalIgnoreCase))
            {
                ValidateFullInventoryItemObject(
                    item,
                    itemContext,
                    issues,
                    requireStringExistedId: true);
                continue;
            }

            var existedId = GetFirstNonEmptyString(item, "existedId");
            if (string.IsNullOrWhiteSpace(existedId))
            {
                ValidateFullInventoryItemObject(
                    item,
                    itemContext,
                    issues,
                    requireStringExistedId: false);
                continue;
            }

            if (preTurnInventoryItemIds.Count > 0 &&
                !preTurnInventoryItemIds.Contains(existedId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.existedId",
                    IssueSeverity.Error,
                    "UpdateInventory ссылается на existing item, которого нет в pre-turn canonical inventory state",
                    code: "inventory_existing_update_unknown_item",
                    section: "Inventory",
                    expected: "existing itemId/existedId from pre-turn inventory/items.json or null for a genuinely new item",
                    actual: existedId,
                    repairHint: "Для existing item используй реальный existedId из pre-turn inventory/items.json. Для genuinely new item передай full Item Object с existedId = null."));
            }

            if (IsLikelyFullInventoryItemObject(item))
            {
                ValidateFullInventoryItemObject(
                    item,
                    itemContext,
                    issues,
                    requireStringExistedId: true);
                continue;
            }

            ValidatePartialInventoryItemUpdate(item, itemContext, issues);
        }
    }

    private void ValidateFullInventoryItemObject(JsonElement item, string itemContext, List<ValidationIssue> issues, bool requireStringExistedId)
    {
        var quality = GetFirstNonEmptyString(item, "quality");
        if (!item.TryGetProperty("existedId", out var existedId))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.existedId",
                IssueSeverity.Error,
                "Item object должен содержать existedId"));
        }
        else if (requireStringExistedId && (existedId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(existedId.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.existedId",
                IssueSeverity.Error,
                "Persisted item object должен содержать непустой existedId"));
        }
        else if (!requireStringExistedId &&
                 existedId.ValueKind != JsonValueKind.Null &&
                 existedId.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.existedId",
                IssueSeverity.Error,
                "existedId должен быть строкой или null"));
        }
        else if (!requireStringExistedId &&
                 existedId.ValueKind == JsonValueKind.String &&
                 string.IsNullOrWhiteSpace(existedId.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.existedId",
                IssueSeverity.Error,
                "existedId должен быть непустой строкой или null"));
        }

        RequireString(item, itemContext, issues, "name");
        RequireString(item, itemContext, issues, "description");
        RequireString(item, itemContext, issues, "image_prompt");
        ValidateRequiredItemQualityField(item, itemContext, issues, "quality");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "price", "Inventory");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "count", "Inventory");
        ValidateNonNegativeNumericField(item, itemContext, issues, "weight");
        ValidateNonNegativeNumericField(item, itemContext, issues, "volume");
        ValidateRequiredNullableStringArrayField(item, itemContext, issues, "contentsPath");
        RequireBooleanField(item, itemContext, issues, "isContainer");
        RequireBooleanField(item, itemContext, issues, "isConsumption");
        RequireBooleanField(item, itemContext, issues, "requiresTwoHands");
        ValidateRequiredItemDurabilityField(item, itemContext, issues, "durability");
        ValidateOptionalString(item, itemContext, issues, "type");
        ValidateOptionalString(item, itemContext, issues, "group");
        ValidateOptionalNullableIntegerField(item, itemContext, issues, "capacity");
        ValidateOptionalNullableNonNegativeNumericField(item, itemContext, issues, "containerWeight");
        ValidateOptionalNullableNonNegativeNumericField(item, itemContext, issues, "weightReduction");

        if (item.TryGetProperty("textContent", out var textContent) && textContent.ValueKind != JsonValueKind.Null)
            RequireArrayOfStrings(textContent, $"{itemContext}.textContent", issues);
        if (item.TryGetProperty("journalEntries", out var journalEntries) && journalEntries.ValueKind != JsonValueKind.Null)
            RequireArrayOfStrings(journalEntries, $"{itemContext}.journalEntries", issues);
        if (item.TryGetProperty("bonuses", out var bonuses))
            RequireArrayOfStrings(bonuses, $"{itemContext}.bonuses", issues);
        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses))
            RequireArrayOfObjects(structuredBonuses, $"{itemContext}.structuredBonuses", issues);
        if (item.TryGetProperty("customProperties", out var customProperties))
            RequireArrayOfObjects(customProperties, $"{itemContext}.customProperties", issues);
        if (item.TryGetProperty("combatEffect", out var combatEffect))
            ValidateCombatActionArray(combatEffect, $"{itemContext}.combatEffect", issues);
        if (item.TryGetProperty("disassembleTo", out var disassembleTo) && disassembleTo.ValueKind != JsonValueKind.Null)
            ValidateItemDisassemblyArray(disassembleTo, $"{itemContext}.disassembleTo", issues);
        ValidateItemBondAndFateCardContract(item, itemContext, issues, quality);
        if (!item.TryGetProperty("equipmentSlot", out var equipmentSlot))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.equipmentSlot",
                IssueSeverity.Error,
                "Item object должен содержать equipmentSlot",
                code: "item_missing_equipment_slot",
                section: "Inventory"));
        }
        else if (equipmentSlot.ValueKind == JsonValueKind.Array)
        {
            ValidateEquipmentSlotArray(equipmentSlot, $"{itemContext}.equipmentSlot", issues);
        }
        else if (equipmentSlot.ValueKind == JsonValueKind.String)
        {
            var equipmentSlotName = equipmentSlot.GetString() ?? string.Empty;
            if (!AllowedEquipmentSlots.Contains(equipmentSlotName))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.equipmentSlot",
                    IssueSeverity.Error,
                    "equipmentSlot должен использовать canonical slot name",
                    code: "item_invalid_equipment_slot",
                    section: "Inventory",
                    expected: string.Join(" | ", AllowedEquipmentSlots),
                    actual: equipmentSlotName));
            }
        }
        else if (equipmentSlot.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.equipmentSlot",
                IssueSeverity.Error,
                "equipmentSlot должен быть строкой, массивом строк или null"));
        }

        ValidateEquipmentSlotRequiresTwoHandsContract(ReadInventoryEquipProfile(item), itemContext, issues);

        if (!item.TryGetProperty("accessoryForSlot", out var accessoryForSlot))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.accessoryForSlot",
                IssueSeverity.Error,
                "Item object должен содержать accessoryForSlot",
                code: "item_missing_accessory_for_slot",
                section: "Inventory"));
        }
        else if (accessoryForSlot.ValueKind == JsonValueKind.Array)
        {
            ValidateEquipmentSlotArray(accessoryForSlot, $"{itemContext}.accessoryForSlot", issues);
        }
        else if (accessoryForSlot.ValueKind == JsonValueKind.String)
        {
            var accessorySlotName = accessoryForSlot.GetString() ?? string.Empty;
            if (!AllowedEquipmentSlots.Contains(accessorySlotName))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.accessoryForSlot",
                    IssueSeverity.Error,
                    "accessoryForSlot должен использовать canonical slot name",
                    code: "item_invalid_accessory_slot",
                    section: "Inventory",
                    expected: string.Join(" | ", AllowedEquipmentSlots),
                    actual: accessorySlotName));
            }
        }
        else if (accessoryForSlot.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.accessoryForSlot",
                IssueSeverity.Error,
                "accessoryForSlot должен быть строкой, массивом строк или null"));
        }
    }

    private void ValidateRequiredItemQualityField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        var quality = RequireString(root, contextPrefix, issues, propName);
        if (!string.IsNullOrWhiteSpace(quality) && !AllowedItemQualities.Contains(quality))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть одним из canonical item quality значений",
                code: "item_invalid_quality",
                section: "Inventory",
                expected: string.Join(" | ", AllowedItemQualities),
                actual: quality,
                repairHint: "Используй для quality только canonical item quality values из Block 10."));
        }
    }

    private void ValidateRequiredItemDurabilityField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное поле: {propName}",
                code: "item_missing_durability",
                section: "Inventory",
                repairHint: "Передай durability как percentage string, например 100%, по item contract."));
            return;
        }

        ValidatePercentageStringField(root, contextPrefix, issues, propName, requirePositive: false);
    }

    private void ValidateNonNegativeNumericField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var numericValue))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть неотрицательным числом"));
            return;
        }

        if (numericValue < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} не может быть отрицательным"));
        }
    }

    private void ValidateOptionalNullableIntegerField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var intValue) || intValue < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть null или неотрицательным целым числом"));
        }
    }

    private void ValidateOptionalNullableNonNegativeNumericField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var numericValue) || numericValue < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть null или неотрицательным числом"));
        }
    }

    private void ValidateItemDisassemblyArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var material in value.EnumerateArray())
        {
            var materialContext = $"{context}[{index++}]";
            if (!RequireObject(material, materialContext, issues))
                continue;

            RequireString(material, materialContext, issues, "materialName");
            ValidatePositiveIntegerField(material, materialContext, issues, "quantity");
            ValidateNonNegativeNumericField(material, materialContext, issues, "weight");
            ValidateNonNegativeNumericField(material, materialContext, issues, "volume");
            ValidateNonNegativeNumericField(material, materialContext, issues, "price");
            ValidateOptionalString(material, materialContext, issues, "description");
        }
    }

    private void ValidateItemFateCardArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var card in value.EnumerateArray())
        {
            var cardContext = $"{context}[{index++}]";
            if (!RequireObject(card, cardContext, issues))
                continue;

            RequireString(card, cardContext, issues, "cardId");
            RequireString(card, cardContext, issues, "name");
            var imagePrompt = RequireString(card, cardContext, issues, "image_prompt");
            RequireString(card, cardContext, issues, "description");
            RequireBooleanField(card, cardContext, issues, "isUnlocked");

            if (!string.IsNullOrWhiteSpace(imagePrompt) && !LooksLikeEnglishImagePrompt(imagePrompt))
            {
                issues.Add(new ValidationIssue(
                    $"{cardContext}.image_prompt",
                    IssueSeverity.Error,
                    "Item Fate Card image_prompt должен быть English-only и не длиннее 150 символов",
                    code: "item_fate_card_invalid_image_prompt",
                    section: "Inventory",
                    expected: "English prompt, <= 150 chars",
                    actual: imagePrompt.Length > 150 ? $">150 chars ({imagePrompt.Length})" : imagePrompt,
                    repairHint: "Используй для Item Fate Card краткий English-only image_prompt без кириллицы и не длиннее 150 символов."));
            }

            if (card.TryGetProperty("unlockConditions", out var unlockConditions) &&
                unlockConditions.ValueKind != JsonValueKind.Null &&
                RequireObject(unlockConditions, $"{cardContext}.unlockConditions", issues))
            {
                if (unlockConditions.TryGetProperty("ownerBondLevel", out _))
                    ValidateItemBondLevelField(unlockConditions, $"{cardContext}.unlockConditions", issues, "ownerBondLevel", required: false, allowNull: false);
                if (unlockConditions.TryGetProperty("requiredMaterials", out var requiredMaterials))
                    ValidateItemRequiredMaterialsArray(requiredMaterials, $"{cardContext}.unlockConditions.requiredMaterials", issues);
                if (unlockConditions.TryGetProperty("conjunction", out var conjunction) &&
                    conjunction.ValueKind == JsonValueKind.String)
                {
                    var conjunctionValue = conjunction.GetString() ?? string.Empty;
                    if (!string.Equals(conjunctionValue, "AND", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(conjunctionValue, "OR", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{cardContext}.unlockConditions.conjunction",
                            IssueSeverity.Error,
                            "unlockConditions.conjunction должен быть AND или OR",
                            code: "item_fate_card_invalid_conjunction",
                            section: "Inventory",
                            expected: "AND | OR",
                            actual: conjunctionValue,
                            repairHint: "Используй в Item Fate Card unlockConditions.conjunction только AND или OR."));
                    }
                }
            }

            if (!card.TryGetProperty("rewards", out var rewards) || !RequireObject(rewards, $"{cardContext}.rewards", issues))
                continue;

            RequireString(rewards, $"{cardContext}.rewards", issues, "description");
            if (rewards.TryGetProperty("improvedBonuses", out var improvedBonuses))
                RequireArrayOfStrings(improvedBonuses, $"{cardContext}.rewards.improvedBonuses", issues);
            if (rewards.TryGetProperty("newCombatEffects", out var newCombatEffects))
                ValidateCombatActionArray(newCombatEffects, $"{cardContext}.rewards.newCombatEffects", issues);
            if (rewards.TryGetProperty("statBoostsToItemItself", out var statBoosts))
                RequireArrayOfStrings(statBoosts, $"{cardContext}.rewards.statBoostsToItemItself", issues);
            ValidateOptionalString(rewards, $"{cardContext}.rewards", issues, "changesDescriptionTo");
            ValidateOptionalString(rewards, $"{cardContext}.rewards", issues, "changesImagePromptTo");
            ValidateOptionalString(rewards, $"{cardContext}.rewards", issues, "otherNarrativeChanges");
        }
    }

    private void ValidateItemRequiredMaterialsArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var material in value.EnumerateArray())
        {
            var materialContext = $"{context}[{index++}]";
            if (!RequireObject(material, materialContext, issues))
                continue;

            RequireString(material, materialContext, issues, "materialName");
            ValidatePositiveIntegerField(material, materialContext, issues, "quantity");
        }
    }

    private void ValidateItemBondAndFateCardContract(JsonElement item, string itemContext, List<ValidationIssue> issues, string? quality)
    {
        var rareOrHigher = string.IsNullOrWhiteSpace(quality) ? (bool?)null : IsRareOrHigherItemQuality(quality);

        if (item.TryGetProperty("ownerBondLevelCurrent", out var ownerBondLevelCurrent))
        {
            if (ownerBondLevelCurrent.ValueKind != JsonValueKind.Null)
            {
                ValidateItemBondLevelField(item, itemContext, issues, "ownerBondLevelCurrent", required: false, allowNull: true);
            }

            if (rareOrHigher == false && ownerBondLevelCurrent.ValueKind != JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.ownerBondLevelCurrent",
                    IssueSeverity.Error,
                    "ownerBondLevelCurrent допустим только для Rare+ item quality",
                    code: "item_bond_level_forbidden_for_non_rare_quality",
                    section: "Inventory",
                    expected: "Rare | Epic | Legendary | Unique quality when ownerBondLevelCurrent is present",
                    actual: quality ?? "unknown quality",
                    repairHint: "Не сохраняй ownerBondLevelCurrent для Trash/Common/Uncommon/Good items. Bond system применяется только к Rare+ предметам."));
            }
        }

        if (item.TryGetProperty("fateCards", out var fateCards) && fateCards.ValueKind != JsonValueKind.Null)
        {
            if (rareOrHigher == false)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.fateCards",
                    IssueSeverity.Error,
                    "fateCards допустимы только для Rare+ item quality",
                    code: "item_fate_cards_forbidden_for_non_rare_quality",
                    section: "Inventory",
                    expected: "Rare | Epic | Legendary | Unique quality when fateCards are present",
                    actual: quality ?? "unknown quality",
                    repairHint: "Не добавляй fateCards предметам ниже Rare. Item Fate Cards существуют только у Rare+ предметов."));
            }

            ValidateItemFateCardArray(fateCards, $"{itemContext}.fateCards", issues);
        }
    }

    private void ValidateItemBondLevelField(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName,
        bool required,
        bool allowNull)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            if (required)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{propName}",
                    IssueSeverity.Error,
                    $"Отсутствует обязательное поле: {propName}",
                    code: "item_bond_level_missing",
                    section: "Inventory",
                    expected: "integer bond level 0..100",
                    actual: "missing",
                    repairHint: "Передай canonical bond level как целое число от 0 до 100."));
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!allowNull)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{propName}",
                    IssueSeverity.Error,
                    $"{propName} не должен быть null",
                    code: "item_bond_level_null_forbidden",
                    section: "Inventory",
                    expected: "integer bond level 0..100",
                    actual: "null",
                    repairHint: "Передай canonical bond level как целое число от 0 до 100."));
            }

            return;
        }

        if (!TryReadInt(root, propName, out var bondLevel))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть целым числом в диапазоне 0..100",
                code: "item_bond_level_invalid_type",
                section: "Inventory",
                expected: "integer 0..100",
                actual: value.ValueKind.ToString(),
                repairHint: "Используй bond level только как целое число от 0 до 100."));
            return;
        }

        if (bondLevel < 0 || bondLevel > 100)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть в диапазоне 0..100",
                code: "item_bond_level_out_of_bounds",
                section: "Inventory",
                expected: "0..100",
                actual: bondLevel.ToString(),
                repairHint: "Держи ownerBondLevelCurrent и unlockConditions.ownerBondLevel в canonical диапазоне 0..100."));
        }
    }

    private string? TryResolveCurrentInventoryItemQualitySync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        var preTurnInventoryItemIds = ReadPreTurnInventoryItemIdsSync();
        var preTurnQuality = TryResolveInventoryItemQualityFromJson(
            ReadPreTurnTrackedFileSync("game_state/inventory/items.json"),
            itemId,
            knownExistingItemIds: null,
            currentStateNewItemsOnly: false);
        if (!string.IsNullOrWhiteSpace(preTurnQuality) || preTurnInventoryItemIds.Contains(itemId))
            return preTurnQuality;

        try
        {
            var path = _fs.ResolvePath("game_state/inventory/items.json");
            if (!File.Exists(path))
                return null;

            var resolvedQuality = TryResolveInventoryItemQualityFromJson(
                File.ReadAllText(path),
                itemId,
                preTurnInventoryItemIds,
                currentStateNewItemsOnly: true);
            if (!string.IsNullOrWhiteSpace(resolvedQuality))
                return resolvedQuality;
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private string? TryResolveInventoryItemQualityFromJson(
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

                if (fullObjectOnly && !IsLikelyFullInventoryItemObject(currentItem))
                    continue;

                var currentItemId = GetInventoryReferenceCandidateId(currentItem, currentStateNewItemsOnly);
                if (!string.Equals(currentItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ShouldAcceptInventoryReferenceCandidate(currentItem, knownExistingItemIds, currentStateNewItemsOnly))
                    continue;

                return GetFirstNonEmptyString(currentItem, "quality");
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private sealed class InventoryEquipProfile
    {
        public string[]? EquipmentSlots { get; init; }
        public bool EquipmentSlotExplicitlyNull { get; init; }
        public bool? RequiresTwoHands { get; init; }
    }

    private sealed class VehicleStateSnapshot
    {
        public string? Availability { get; init; }
        public bool HasCurrentLocationNode { get; init; }
        public bool CurrentLocationExplicitNull { get; init; }
        public string? CurrentLocationId { get; init; }
    }

    private InventoryEquipProfile? TryResolveCurrentInventoryItemEquipProfileSync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        var preTurnInventoryItemIds = ReadPreTurnInventoryItemIdsSync();
        var preTurnProfile = TryResolveInventoryItemEquipProfileFromJson(
            ReadPreTurnTrackedFileSync("game_state/inventory/items.json"),
            itemId,
            knownExistingItemIds: null,
            currentStateNewItemsOnly: false);
        if (preTurnProfile != null || preTurnInventoryItemIds.Contains(itemId))
            return preTurnProfile;

        try
        {
            var path = _fs.ResolvePath("game_state/inventory/items.json");
            if (File.Exists(path))
            {
                var resolved = TryResolveInventoryItemEquipProfileFromJson(
                    File.ReadAllText(path),
                    itemId,
                    preTurnInventoryItemIds,
                    currentStateNewItemsOnly: true);
                if (resolved != null)
                    return resolved;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static InventoryEquipProfile ReadInventoryEquipProfile(JsonElement item)
    {
        string[]? equipmentSlots = null;
        var slotExplicitlyNull = false;
        if (item.TryGetProperty("equipmentSlot", out var equipmentSlot))
        {
            if (equipmentSlot.ValueKind == JsonValueKind.Null)
            {
                slotExplicitlyNull = true;
            }
            else if (equipmentSlot.ValueKind == JsonValueKind.String)
            {
                var slotName = equipmentSlot.GetString();
                if (!string.IsNullOrWhiteSpace(slotName))
                    equipmentSlots = [slotName];
            }
            else if (equipmentSlot.ValueKind == JsonValueKind.Array)
            {
                var slots = new List<string>();
                foreach (var slot in equipmentSlot.EnumerateArray())
                {
                    if (slot.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(slot.GetString()))
                        slots.Add(slot.GetString()!);
                }

                equipmentSlots = slots.Count > 0 ? [.. slots] : null;
            }
        }

        bool? requiresTwoHands = null;
        if (item.TryGetProperty("requiresTwoHands", out var requiresTwoHandsNode) &&
            requiresTwoHandsNode.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            requiresTwoHands = requiresTwoHandsNode.GetBoolean();
        }

        return new InventoryEquipProfile
        {
            EquipmentSlots = equipmentSlots,
            EquipmentSlotExplicitlyNull = slotExplicitlyNull,
            RequiresTwoHands = requiresTwoHands
        };
    }

    private void ValidateEquipmentSlotRequiresTwoHandsContract(
        InventoryEquipProfile? profile,
        string contextPrefix,
        List<ValidationIssue> issues,
        string section = "Inventory")
    {
        if (profile == null || profile.RequiresTwoHands != true)
            return;

        if (profile.EquipmentSlotExplicitlyNull)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.requiresTwoHands",
                IssueSeverity.Error,
                "requiresTwoHands=true недопустим для non-equippable item с equipmentSlot=null",
                code: "item_requires_two_hands_with_null_equipment_slot",
                section: section,
                expected: "equipmentSlot = [MainHand, OffHand] when requiresTwoHands = true",
                actual: "equipmentSlot = null",
                repairHint: "Для non-equippable items используй requiresTwoHands=false. Двуручный предмет должен иметь equipmentSlot = [\"MainHand\", \"OffHand\"]."));
            return;
        }

        if (!IsExactMainHandOffHandPair(profile.EquipmentSlots))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.equipmentSlot",
                IssueSeverity.Error,
                "requiresTwoHands=true допустим только для exact hand-pair equipment slot",
                code: "item_requires_two_hands_invalid_equipment_slot_pair",
                section: section,
                expected: "[MainHand, OffHand]",
                actual: DescribeEquipmentSlots(profile),
                repairHint: "Если предмет действительно двуручный, задай equipmentSlot = [\"MainHand\", \"OffHand\"]. Для любых других slot combinations оставь requiresTwoHands=false."));
        }
    }

    private static bool IsExactMainHandOffHandPair(string[]? slots)
    {
        if (slots == null || slots.Length != 2)
            return false;

        var seenMainHand = false;
        var seenOffHand = false;
        foreach (var slot in slots)
        {
            if (string.Equals(slot, "MainHand", StringComparison.OrdinalIgnoreCase))
                seenMainHand = true;
            else if (string.Equals(slot, "OffHand", StringComparison.OrdinalIgnoreCase))
                seenOffHand = true;
        }

        return seenMainHand && seenOffHand;
    }

    private static string DescribeEquipmentSlots(InventoryEquipProfile profile)
    {
        if (profile.EquipmentSlotExplicitlyNull)
            return "null";

        if (profile.EquipmentSlots == null || profile.EquipmentSlots.Length == 0)
            return "missing/unknown";

        return "[" + string.Join(", ", profile.EquipmentSlots) + "]";
    }

    private static bool IsRareOrHigherItemQuality(string quality)
    {
        return string.Equals(quality, "Rare", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(quality, "Epic", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(quality, "Legendary", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(quality, "Unique", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidatePartialInventoryItemUpdate(
        JsonElement item,
        string itemContext,
        List<ValidationIssue> issues,
        string section = "Inventory",
        bool forbidContentsPathMutation = true)
    {
        RequireString(item, itemContext, issues, "existedId");
        var currentEquipProfile = TryResolveCurrentInventoryItemEquipProfileSync(GetFirstNonEmptyString(item, "existedId"));
        var effectiveQuality = GetFirstNonEmptyString(item, "quality");
        if (string.IsNullOrWhiteSpace(effectiveQuality))
            effectiveQuality = TryResolveCurrentInventoryItemQualitySync(GetFirstNonEmptyString(item, "existedId"));

        var visibleProps = item.EnumerateObject()
            .Where(prop => !string.Equals(prop.Name, "existedId", StringComparison.OrdinalIgnoreCase) &&
                           !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (visibleProps.Count == 0)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "Partial UpdateInventory item должен содержать хотя бы одно изменяемое поле",
                code: "inventory_partial_update_missing_changes",
                section: section,
                repairHint: "Для existing item в UpdateInventory передай existedId и только реально изменившиеся поля."));
            return;
        }

        if (forbidContentsPathMutation && item.TryGetProperty("contentsPath", out var contentsPath))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.contentsPath",
                IssueSeverity.Error,
                "Запрещено двигать existing item через UpdateInventory.contentsPath",
                code: "inventory_partial_update_moves_contents_path",
                section: section,
                expected: "moveInventoryItems for item relocation",
                actual: contentsPath.ValueKind == JsonValueKind.Null ? "null" : contentsPath.ValueKind.ToString(),
                repairHint: "Для перемещения existing item используй moveInventoryItems, а UpdateInventory оставляй только для изменения собственных свойств предмета."));
        }

        foreach (var prop in visibleProps)
        {
            switch (prop.Name)
            {
                case "name":
                case "description":
                case "image_prompt":
                case "type":
                case "group":
                    if (prop.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.Value.GetString()))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.{prop.Name}",
                            IssueSeverity.Error,
                            $"{prop.Name} должен быть непустой строкой"));
                    }
                    break;
                case "quality":
                    ValidateRequiredItemQualityField(item, itemContext, issues, "quality");
                    break;
                case "durability":
                    ValidateRequiredItemDurabilityField(item, itemContext, issues, "durability");
                    break;
                case "price":
                    ValidateNonNegativeIntegerField(item, itemContext, issues, "price", section);
                    break;
                case "weight":
                case "volume":
                    ValidateNonNegativeNumericField(item, itemContext, issues, prop.Name);
                    break;
                case "capacity":
                    ValidateOptionalNullableIntegerField(item, itemContext, issues, "capacity");
                    break;
                case "containerWeight":
                case "weightReduction":
                    ValidateOptionalNullableNonNegativeNumericField(item, itemContext, issues, prop.Name);
                    break;
                case "count":
                    ValidateNonNegativeIntegerField(item, itemContext, issues, "count", section);
                    break;
                case "isContainer":
                case "isConsumption":
                case "requiresTwoHands":
                    if (prop.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.{prop.Name}",
                            IssueSeverity.Error,
                            $"{prop.Name} должен быть boolean"));
                    }
                    break;
                case "textContent":
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.textContent",
                        IssueSeverity.Error,
                        "Partial UpdateInventory не должен менять textContent append-каналом",
                        code: "inventory_partial_update_text_content_forbidden",
                        section: section,
                        expected: "updateItemTextContents for append, full item object only for create/full rewrite",
                        actual: "textContent partial patch",
                        repairHint: "Для простого добавления записи используй updateItemTextContents. textContent в UpdateInventory оставляй только для new item или редкого полного rewrite item object, а не для partial delta existing item."));
                    break;
                case "journalEntries":
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.journalEntries",
                        IssueSeverity.Error,
                        "Partial UpdateInventory не должен менять journalEntries",
                        code: "inventory_partial_update_journal_entries_forbidden",
                        section: section,
                        expected: "itemJournalUpdates append-only channel",
                        actual: "journalEntries partial patch",
                        repairHint: "Для sentient/read-only journal surfaces используй itemJournalUpdates. Не меняй journalEntries через partial UpdateInventory existing item."));
                    break;
                case "bonuses":
                    if (prop.Value.ValueKind != JsonValueKind.Null)
                        RequireArrayOfStrings(prop.Value, $"{itemContext}.{prop.Name}", issues);
                    break;
                case "structuredBonuses":
                case "customProperties":
                    RequireArrayOfObjects(prop.Value, $"{itemContext}.{prop.Name}", issues);
                    break;
                case "combatEffect":
                    ValidateCombatActionArray(prop.Value, $"{itemContext}.combatEffect", issues, section: section);
                    break;
                case "disassembleTo":
                    if (prop.Value.ValueKind != JsonValueKind.Null)
                        ValidateItemDisassemblyArray(prop.Value, $"{itemContext}.disassembleTo", issues);
                    break;
                case "fateCards":
                case "ownerBondLevelCurrent":
                    break;
                case "contentsPath":
                    if (!forbidContentsPathMutation)
                        ValidateRequiredNullableStringArrayField(item, itemContext, issues, "contentsPath");
                    break;
                case "equipmentSlot":
                case "accessoryForSlot":
                    ValidateOptionalStringOrStringArrayField(item, itemContext, issues, prop.Name);
                    break;
                default:
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.{prop.Name}",
                        IssueSeverity.Error,
                        "Partial item update содержит неподдерживаемое поле",
                        code: "inventory_partial_update_unknown_field",
                        section: section,
                        expected: "Only documented Item Object fields that actually changed this turn",
                        actual: prop.Name,
                        repairHint: "Передай в partial item update только existedId и реально изменившиеся documented fields Item Object без опечаток и post-processing keys."));
                    break;
            }
        }

        if (visibleProps.Any(prop =>
                string.Equals(prop.Name, "fateCards", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "ownerBondLevelCurrent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "quality", StringComparison.OrdinalIgnoreCase)))
        {
            ValidateItemBondAndFateCardContract(item, itemContext, issues, effectiveQuality);
        }

        var patchEquipProfile = ReadInventoryEquipProfile(item);
        var effectiveEquipProfile = new InventoryEquipProfile
        {
            EquipmentSlots = patchEquipProfile.EquipmentSlots ?? currentEquipProfile?.EquipmentSlots,
            EquipmentSlotExplicitlyNull = patchEquipProfile.EquipmentSlotExplicitlyNull ||
                                          (patchEquipProfile.EquipmentSlots == null && currentEquipProfile?.EquipmentSlotExplicitlyNull == true),
            RequiresTwoHands = patchEquipProfile.RequiresTwoHands ?? currentEquipProfile?.RequiresTwoHands
        };
        if (patchEquipProfile.EquipmentSlots != null ||
            patchEquipProfile.EquipmentSlotExplicitlyNull ||
            currentEquipProfile != null)
        {
            ValidateEquipmentSlotRequiresTwoHandsContract(effectiveEquipProfile, itemContext, issues, section);
        }
    }

    private static bool IsLikelyFullInventoryItemObject(JsonElement item)
    {
        return HasRequiredNonEmptyStrings(item, "name", "description", "image_prompt", "quality", "durability") &&
               HasRequiredProperties(item, "price", "count", "weight", "volume", "contentsPath", "isContainer", "isConsumption", "requiresTwoHands");
    }

    private void ValidateOptionalStringOrStringArrayField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Null)
            return;

        if (value.ValueKind == JsonValueKind.String)
        {
            if (string.IsNullOrWhiteSpace(value.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{propName}",
                    IssueSeverity.Error,
                    $"{propName} не должен быть пустой строкой"));
            }
            return;
        }

        RequireArrayOfStrings(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateMoveInventoryItems(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        const string propName = "moveInventoryItems";
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "movedItemId");
            RequireString(item, itemContext, issues, "itemName");
            ValidateRequiredNullableStringArrayField(item, itemContext, issues, "currentContentsPath");
            ValidateRequiredNullableStringField(item, itemContext, issues, "destinationContainerId");
            ValidateRequiredNullableStringField(item, itemContext, issues, "destinationContainerName");
            ValidateRequiredNullableStringArrayField(item, itemContext, issues, "destinationContentsPath");
        }
    }

    private void ValidateRemoveInventoryItems(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        const string propName = "removeInventoryItems";
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "removedItemId");
            RequireString(item, itemContext, issues, "itemName");
            ValidateRequiredNullableStringArrayField(item, itemContext, issues, "currentContentsPath");

            var unexpectedFields = item.EnumerateObject()
                .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(prop.Name, "removedItemId", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(prop.Name, "itemName", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(prop.Name, "currentContentsPath", StringComparison.OrdinalIgnoreCase))
                .Select(prop => prop.Name)
                .ToArray();
            if (unexpectedFields.Length > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "removeInventoryItems предназначен только для полного удаления стека и не должен нести дополнительные delta-поля",
                    code: "remove_inventory_items_partial_semantics_forbidden",
                    section: "Inventory",
                    expected: "removedItemId + itemName + currentContentsPath only",
                    actual: string.Join(", ", unexpectedFields),
                    repairHint: "Для частичного расхода или gameplay consumption меняй count/resource через UpdateInventory или inventoryItemsResources. removeInventoryItems используй только для discard полной стопки."));
            }
        }
    }

    private void ValidateEquipmentChanges(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetArray(root, "equipmentChanges", $"{contextPrefix}.equipmentChanges", issues, out var arr))
            return;

        var simulatedEquippedItems = TryResolvePreTurnPlayerEquippedItemsSync() ?? TryResolveCurrentPlayerEquippedItemsSync();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.equipmentChanges[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var action = RequireString(item, itemContext, issues, "action");
            var itemId = RequireString(item, itemContext, issues, "itemId");
            RequireString(item, itemContext, issues, "itemName");

            if (string.Equals(action, "equip", StringComparison.OrdinalIgnoreCase))
            {
                if (!item.TryGetProperty("targetSlots", out var targetSlots))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.targetSlots",
                        IssueSeverity.Error,
                        "equipmentChanges.equip должен содержать targetSlots",
                        code: "equipment_change_missing_target_slots",
                        section: "Inventory",
                        expected: "targetSlots array",
                        actual: "missing",
                        repairHint: "Для equip передай targetSlots как непустой массив canonical equipment slots."));
                    continue;
                }

                if (!TryGetArray(item, "targetSlots", $"{itemContext}.targetSlots", issues, out targetSlots))
                    continue;
                ValidateEquipmentSlotArray(targetSlots, $"{itemContext}.targetSlots", issues);
                var equipCompatible = ValidateEquipmentChangeEquipCompatibility(itemId, targetSlots, itemContext, issues);
                var equipOccupancyValid = ValidateEquipmentChangeEquipOccupancy(simulatedEquippedItems, itemId, targetSlots, itemContext, issues);
                if (equipCompatible && equipOccupancyValid)
                    ApplyEquipmentChangeEquipSimulation(simulatedEquippedItems, itemId, targetSlots);
            }
            else if (string.Equals(action, "unequip", StringComparison.OrdinalIgnoreCase))
            {
                if (!item.TryGetProperty("sourceSlots", out var sourceSlots))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.sourceSlots",
                        IssueSeverity.Error,
                        "equipmentChanges.unequip должен содержать sourceSlots",
                        code: "equipment_change_missing_source_slots",
                        section: "Inventory",
                        expected: "sourceSlots array",
                        actual: "missing",
                        repairHint: "Для unequip передай sourceSlots как непустой массив canonical equipment slots."));
                    continue;
                }

                if (!TryGetArray(item, "sourceSlots", $"{itemContext}.sourceSlots", issues, out sourceSlots))
                    continue;
                ValidateEquipmentSlotArray(sourceSlots, $"{itemContext}.sourceSlots", issues);
                ValidateEquipmentChangeUnequipOccupancy(simulatedEquippedItems, itemId, sourceSlots, itemContext, issues);
                ApplyEquipmentChangeUnequipSimulation(simulatedEquippedItems, sourceSlots);
            }
            else if (!string.IsNullOrWhiteSpace(action))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.action",
                    IssueSeverity.Error,
                    "equipmentChanges.action должен быть equip или unequip",
                    code: "equipment_change_invalid_action",
                    section: "Inventory",
                    expected: "equip | unequip",
                    actual: action,
                    repairHint: "Используй в equipmentChanges только canonical action equip или unequip."));
            }
        }
    }

    private void ValidateEquipmentSlotArray(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfStrings(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var slot in arr.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(slot.GetString()))
            {
                index++;
                continue;
            }

            var slotName = slot.GetString() ?? string.Empty;
            if (!AllowedEquipmentSlots.Contains(slotName))
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Equipment slot должен быть одним из canonical slot names",
                    code: "equipment_slot_invalid",
                    section: "Inventory",
                    expected: string.Join(" | ", AllowedEquipmentSlots),
                    actual: slotName,
                    repairHint: "Используй только canonical equipment slot names из Block 10."));
            }

            index++;
        }
    }

    private Dictionary<string, string?>? TryResolvePreTurnPlayerEquippedItemsSync()
        => TryReadPlayerEquippedItemsStateFromJson(ReadPreTurnTrackedFileSync("game_state/inventory/items.json"));

    private Dictionary<string, string?>? TryResolveCurrentPlayerEquippedItemsSync()
    {
        try
        {
            var path = _fs.ResolvePath("game_state/inventory/items.json");
            if (!File.Exists(path))
                return null;

            return TryReadPlayerEquippedItemsStateFromJson(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string?>? TryReadPlayerEquippedItemsStateFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("equippedItems", out var equippedItems) ||
                equippedItems.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var slot in equippedItems.EnumerateObject())
            {
                if (slot.Value.ValueKind == JsonValueKind.String)
                    map[slot.Name] = slot.Value.GetString();
                else if (slot.Value.ValueKind == JsonValueKind.Null)
                    map[slot.Name] = null;
            }

            return map;
        }
        catch
        {
            return null;
        }
    }

    private bool ValidateEquipmentChangeEquipCompatibility(
        string? itemId,
        JsonElement targetSlots,
        string itemContext,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(itemId) || targetSlots.ValueKind != JsonValueKind.Array)
            return false;

        var profile = TryResolveCurrentInventoryItemEquipProfileSync(itemId);
        if (profile == null)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.itemId",
                IssueSeverity.Error,
                "equipmentChanges.equip ссылается на itemId, которого нет в canonical inventory state",
                code: "equipment_change_unknown_item_reference",
                section: "Inventory",
                expected: "existing pre-turn itemId or same-turn newly created full item already present in current canonical inventory state",
                actual: itemId,
                repairHint: "Экипируй только предметы, которые реально существуют в pre-turn inventory/items.json, либо same-turn новый полный Item Object уже должен присутствовать в текущем canonical inventory state."));
            return false;
        }

        var targetSlotNames = targetSlots.EnumerateArray()
            .Where(slot => slot.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(slot.GetString()))
            .Select(slot => slot.GetString()!)
            .ToArray();
        if (targetSlotNames.Length == 0)
            return false;

        if (profile.EquipmentSlotExplicitlyNull || profile.EquipmentSlots == null || profile.EquipmentSlots.Length == 0)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.targetSlots",
                IssueSeverity.Error,
                "equipmentChanges.equip ссылается на предмет, который не является equippable item",
                code: "equipment_change_equip_non_equippable_item",
                section: "Inventory",
                expected: "item with non-null equipmentSlot",
                actual: itemId,
                repairHint: "Экипируй только предметы, у которых в canonical inventory state задан equipmentSlot. Для non-equippable item не создавай equipmentChanges.equip."));
            return false;
        }

        if (profile.RequiresTwoHands == true)
        {
            if (!IsExactMainHandOffHandPair(targetSlotNames))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.targetSlots",
                    IssueSeverity.Error,
                    "Двуручный предмет должен экипироваться только в MainHand + OffHand",
                    code: "equipment_change_two_handed_target_slots_invalid",
                    section: "Inventory",
                    expected: "[MainHand, OffHand]",
                    actual: string.Join(", ", targetSlotNames),
                    repairHint: "Для requiresTwoHands=true используй exact targetSlots = [\"MainHand\", \"OffHand\"]."));
                return false;
            }

            return true;
        }

        if (profile.EquipmentSlots.Length == 1)
        {
            var expectedSlot = profile.EquipmentSlots[0];
            if (targetSlotNames.Length != 1 || !string.Equals(targetSlotNames[0], expectedSlot, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.targetSlots",
                    IssueSeverity.Error,
                    "equipmentChanges.equip использует slot, несовместимый с canonical item equipmentSlot",
                    code: "equipment_change_target_slot_mismatch",
                    section: "Inventory",
                    expected: expectedSlot,
                    actual: string.Join(", ", targetSlotNames),
                    repairHint: "Синхронизируй targetSlots с equipmentSlot предмета из inventory/items.json."));
                return false;
            }

            return true;
        }

        var incompatibleSlots = targetSlotNames
            .Where(slot => !profile.EquipmentSlots.Contains(slot, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (incompatibleSlots.Length > 0)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.targetSlots",
                IssueSeverity.Error,
                "equipmentChanges.equip использует slot, который не входит в допустимые equipmentSlot предмета",
                code: "equipment_change_target_slot_outside_item_profile",
                section: "Inventory",
                expected: string.Join(" | ", profile.EquipmentSlots),
                actual: string.Join(", ", incompatibleSlots),
                repairHint: "Выбирай targetSlots только из canonical equipmentSlot списка этого предмета."));
            return false;
        }

        return true;
    }

    private static bool ValidateEquipmentChangeEquipOccupancy(
        Dictionary<string, string?>? equippedItems,
        string? itemId,
        JsonElement targetSlots,
        string itemContext,
        List<ValidationIssue> issues)
    {
        if (equippedItems == null || string.IsNullOrWhiteSpace(itemId) || targetSlots.ValueKind != JsonValueKind.Array)
            return true;

        var conflictedSlots = new List<string>();
        foreach (var slot in targetSlots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(slot.GetString()))
                continue;

            var slotName = slot.GetString()!;
            if (equippedItems.TryGetValue(slotName, out var occupantId) &&
                !string.IsNullOrWhiteSpace(occupantId) &&
                !string.Equals(occupantId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                conflictedSlots.Add($"{slotName}={occupantId}");
            }
        }

        if (conflictedSlots.Count == 0)
            return true;

        issues.Add(new ValidationIssue(
            $"{itemContext}.targetSlots",
            IssueSeverity.Error,
            "equipmentChanges.equip должен явно отражать auto-unequip для вытесняемых предметов",
            code: "equipment_change_missing_auto_unequip",
            section: "Inventory",
            expected: "preceding unequip entries for all conflicting occupied slots",
            actual: string.Join(", ", conflictedSlots),
            repairHint: "Сначала добавь explicit equipmentChanges.unequip для предметов, уже занятых в targetSlots, и только потом выполняй equip. Для two-handed equip это тоже обязательно для MainHand/OffHand конфликтов."));
        return false;
    }

    private static void ValidateEquipmentChangeUnequipOccupancy(
        Dictionary<string, string?>? equippedItems,
        string? itemId,
        JsonElement sourceSlots,
        string itemContext,
        List<ValidationIssue> issues)
    {
        if (equippedItems == null || string.IsNullOrWhiteSpace(itemId) || sourceSlots.ValueKind != JsonValueKind.Array)
            return;

        var mismatchedSlots = new List<string>();
        foreach (var slot in sourceSlots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(slot.GetString()))
                continue;

            var slotName = slot.GetString()!;
            if (!equippedItems.TryGetValue(slotName, out var occupantId) ||
                !string.Equals(occupantId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                mismatchedSlots.Add(slotName);
            }
        }

        if (mismatchedSlots.Count > 0)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.sourceSlots",
                IssueSeverity.Error,
                "equipmentChanges.unequip ссылается на slot, где этот itemId не был экипирован в pre-turn state",
                code: "equipment_change_unequip_unknown_occupancy",
                section: "Inventory",
                expected: $"pre-turn equippedItems contains {itemId} in listed sourceSlots",
                actual: string.Join(", ", mismatchedSlots),
                repairHint: "Для unequip используй только те sourceSlots, где этот itemId действительно был экипирован до хода. Если предмет только что экипируется, сначала отрази корректный equip/auto-unequip порядок."));
        }
    }

    private static void ApplyEquipmentChangeEquipSimulation(
        Dictionary<string, string?>? equippedItems,
        string? itemId,
        JsonElement targetSlots)
    {
        if (equippedItems == null || string.IsNullOrWhiteSpace(itemId) || targetSlots.ValueKind != JsonValueKind.Array)
            return;

        foreach (var slot in targetSlots.EnumerateArray())
        {
            if (slot.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(slot.GetString()))
                equippedItems[slot.GetString()!] = itemId;
        }
    }

    private static void ApplyEquipmentChangeUnequipSimulation(
        Dictionary<string, string?>? equippedItems,
        JsonElement sourceSlots)
    {
        if (equippedItems == null || sourceSlots.ValueKind != JsonValueKind.Array)
            return;

        foreach (var slot in sourceSlots.EnumerateArray())
        {
            if (slot.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(slot.GetString()))
                equippedItems[slot.GetString()!] = null;
        }
    }

    private void ValidateRequiredNullableStringField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное поле: {propName}",
                code: "missing_required_nullable_string_field",
                expected: $"{propName} as string or null",
                actual: "missing",
                repairHint: $"Добавь поле {propName}; оно должно быть непустой строкой или null по canonical contract."));
            return;
        }

        if (value.ValueKind != JsonValueKind.Null &&
            (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть непустой строкой или null",
                code: "invalid_nullable_string_field",
                expected: $"{propName} as non-empty string or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Исправь {propName}: передай непустую строку или null."));
        }
    }

    private void ValidateOptionalNullableStringField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Null &&
            (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть непустой строкой или null"));
        }
    }

    private void ValidateRequiredNullableStringArrayField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное поле: {propName}"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return;

        RequireArrayOfStrings(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidatePercentageStringArrayValues(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !TryParsePercentageString(item.GetString(), requirePositive: false, out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Элемент должен быть non-negative percentage string",
                    code: "percentage_string_array_invalid_entry",
                    repairHint: "Используй в массиве только строки вида '91%' или '0%' без произвольного текста."));
            }

            index++;
        }
    }

    private void ValidateItemTextUpdateCommands(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        const string propName = "updateItemTextContents";
        if (!root.TryGetProperty(propName, out var value))
            return;

        RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingTextUpdateFields = GetMissingRequiredNonEmptyStringProperties(item, "itemId", "itemName", "textToAppend");
            if (missingTextUpdateFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "updateItemTextContents использует append-only contract с обязательными itemId, itemName и textToAppend",
                    code: "item_text_update_missing_required_fields",
                    section: "ItemSidecars",
                    expected: "Non-empty itemId, itemName, textToAppend",
                    actual: string.Join(", ", missingTextUpdateFields),
                    repairHint: "Для updateItemTextContents передай itemId, itemName и textToAppend. Не полагайся на общий name/id shorthand для этого append-only command."));
                continue;
            }
        }
    }

    private void ValidateOptionalObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues,
        string propName, bool allowArray)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Object)
            return;

        if (allowArray && value.ValueKind == JsonValueKind.Array)
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            allowArray ? "Поле должно быть объектом или массивом" : "Поле должно быть объектом"));
    }

    private void ValidateOptionalString(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
                "Поле должно быть строкой"));
        }
    }

    private void ValidateRequiredIsoTimestampField(JsonElement root, string contextPrefix, List<ValidationIssue> issues,
        string propName, string section, string missingCode, string invalidCode, string repairHint)
    {
        var fieldPath = $"{contextPrefix}.{propName}";
        if (!root.TryGetProperty(propName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new ValidationIssue(
                fieldPath,
                IssueSeverity.Error,
                $"{propName} обязателен и должен быть ISO 8601 timestamp",
                code: missingCode,
                section: section,
                expected: "ISO 8601 timestamp",
                actual: !root.TryGetProperty(propName, out var actualValue)
                    ? "missing"
                    : actualValue.ValueKind == JsonValueKind.String
                        ? "missing/empty"
                        : actualValue.ValueKind.ToString(),
                repairHint: repairHint));
            return;
        }

        var timestamp = value.GetString() ?? string.Empty;
        if (!DateTimeOffset.TryParse(timestamp, out _))
        {
            issues.Add(new ValidationIssue(
                fieldPath,
                IssueSeverity.Error,
                $"{propName} должен быть ISO 8601 timestamp",
                code: invalidCode,
                section: section,
                expected: "ISO 8601 timestamp",
                actual: timestamp,
                repairHint: repairHint));
        }
    }

    private void ValidateBaseCharacteristicsFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        var hasCommandSurface = root.TryGetProperty("setCharacteristics", out _);
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (hasCommandSurface && string.Equals(prop.Name, "setCharacteristics", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!Characteristics.All.Contains(prop.Name, StringComparer.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{prop.Name}",
                    IssueSeverity.Error,
                    "characteristics.json использует неподдерживаемый top-level ключ",
                    code: "characteristics_invalid_key",
                    section: "Characteristics",
                    expected: string.Join(" | ", Characteristics.All),
                    actual: prop.Name,
                    repairHint: "Используй в characteristics.json только canonical English lowercase characteristic names из setCharacteristics contract."));
                continue;
            }

            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out _))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{prop.Name}",
                    IssueSeverity.Error,
                    "Значение характеристики должно быть integer",
                    code: "characteristics_non_integer_value",
                    section: "Characteristics",
                    expected: "integer characteristic value",
                    actual: prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.ToString() : prop.Value.ValueKind.ToString(),
                    repairHint: "Сохраняй характеристики как целые числа без строк, процентов и дробных значений."));
            }
        }

        if (hasCommandSurface)
            return;

        foreach (var characteristic in Characteristics.All)
        {
            if (!root.TryGetProperty(characteristic, out var value))
            {
                issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                    $"Отсутствует характеристика: {characteristic}"));
                continue;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{characteristic}",
                    IssueSeverity.Error,
                    "Значение характеристики должно быть integer",
                    code: "characteristics_non_integer_value",
                    section: "Characteristics",
                    expected: "integer characteristic value",
                    actual: value.ValueKind == JsonValueKind.Number ? value.ToString() : value.ValueKind.ToString(),
                    repairHint: "Сохраняй характеристики как целые числа без строк, процентов и дробных значений."));
                continue;
            }

            if (parsed < 1 || parsed > 100)
            {
                issues.Add(new ValidationIssue($"{contextPrefix}.{characteristic}", IssueSeverity.Warning,
                    $"Характеристика вне диапазона 1-100: {parsed}"));
            }
        }
    }

    private void ValidateExperienceFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        ValidateOptionalNumberOrString(root, contextPrefix, issues, "experienceGained");
        ValidateOptionalNumberOrString(root, contextPrefix, issues, "totalExperience");
        ValidateOptionalNumberOrString(root, contextPrefix, issues, "experienceForNextLevel");

        if (!HasNumberOrString(root, "level", "playerLevel"))
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Желательно наличие level или playerLevel в experience.json"));
        }

        if (root.TryGetProperty("playerEffortTrackerChange", out var effort) &&
            effort.ValueKind != JsonValueKind.Null)
        {
            ValidateEffortTrackerObject(effort, $"{contextPrefix}.playerEffortTrackerChange", issues);
        }
    }

    private void ValidateEffectsProperty(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            ValidateEffectsContainer(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateWoundsProperty(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            ValidateWoundsContainer(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateCustomStatesProperty(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            ValidateCustomStatesContainer(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateStealthProperty(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            ValidateStealthState(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateEffortTracker(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("playerEffortTrackerChange", out var effort) &&
            effort.ValueKind != JsonValueKind.Null)
        {
            ValidateEffortTrackerObject(effort, $"{contextPrefix}.playerEffortTrackerChange", issues);
        }
    }

    private void ValidateWeightProperty(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            ValidateWeightData(value, $"{contextPrefix}.{propName}", issues);
    }

    private void ValidateEffectsContainer(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            ValidateArrayItems(root, contextPrefix, issues, ValidateEffectObject);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Error,
                "Эффекты должны быть объектом или массивом"));
            return;
        }

        if (LooksLikeEffectObject(root))
        {
            ValidateEffectObject(root, contextPrefix, issues);
            return;
        }

        var validatedAny = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            validatedAny = true;
            ValidateArrayItems(prop.Value, $"{contextPrefix}.{prop.Name}", issues, ValidateEffectObject);
        }

        if (!validatedAny)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Файл/поле эффектов не содержит массивов эффектов"));
        }
    }

    private void ValidateWoundsContainer(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            ValidateArrayItems(root, contextPrefix, issues, ValidateWoundObject);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Error,
                "Раны должны быть объектом или массивом"));
            return;
        }

        if (HasAnyNonEmptyString(root, "woundName", "name"))
        {
            ValidateWoundObject(root, contextPrefix, issues);
            return;
        }

        var validatedAny = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            validatedAny = true;
            ValidateArrayItems(prop.Value, $"{contextPrefix}.{prop.Name}", issues, ValidateWoundObject);
        }

        if (!validatedAny)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Файл/поле ран не содержит массивов ран"));
        }
    }

    private void ValidateCustomStatesContainer(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            ValidateArrayItems(root, contextPrefix, issues, ValidateCustomStateObject);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Error,
                "Особые состояния должны быть объектом или массивом"));
            return;
        }

        if (HasAnyNonEmptyString(root, "stateName", "stateKey", "key", "name"))
        {
            ValidateCustomStateObject(root, contextPrefix, issues);
            return;
        }

        var validatedAny = false;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            validatedAny = true;
            ValidateArrayItems(prop.Value, $"{contextPrefix}.{prop.Name}", issues, ValidateCustomStateObject);
        }

        if (!validatedAny)
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Файл/поле custom states не содержит массивов состояний"));
        }
    }

    private void ValidateStealthState(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        ValidateOptionalNumberOrString(root, contextPrefix, issues, "detectionLevel");
        ValidateOptionalString(root, contextPrefix, issues, "description");
        ValidateOptionalString(root, contextPrefix, issues, "state");
        ValidateOptionalBool(root, contextPrefix, issues, "isActive");
        ValidateOptionalBool(root, contextPrefix, issues, "isHidden");
    }

    private void ValidateEffortTrackerObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        ValidateOptionalNullableStringField(root, contextPrefix, issues, "lastUsedCharacteristic");
        ValidateOptionalNumberOrString(root, contextPrefix, issues, "consecutivePartialSuccesses");
    }

    private void ValidateWeightData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(root, contextPrefix, issues))
            return;

        if (!HasNumberOrString(root, "totalWeight", "currentWeight"))
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Желательно наличие totalWeight или currentWeight"));
        }

        if (!HasNumberOrString(root, "maxWeight", "maximumWeight"))
        {
            issues.Add(new ValidationIssue(contextPrefix, IssueSeverity.Warning,
                "Желательно наличие maxWeight или maximumWeight"));
        }

        ValidateOptionalNumberOrString(root, contextPrefix, issues, "additionalEnergyExpenditure");
        ValidateOptionalBool(root, contextPrefix, issues, "isOverloaded");
        ValidateOptionalBool(root, contextPrefix, issues, "overloaded");
    }

    private void ValidateEffectObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(item, context, issues))
            return;

        if (!HasAnyNonEmptyString(item, "effectType", "effectDescription", "description", "name"))
        {
            issues.Add(new ValidationIssue(context, IssueSeverity.Error,
                "Эффект должен содержать хотя бы effectType/effectDescription/description/name"));
        }

        ValidateOptionalNumberOrString(item, context, issues, "duration");
        ValidateOptionalString(item, context, issues, "targetType");
        ValidateOptionalString(item, context, issues, "targetTypeDisplayName");
        ValidateOptionalString(item, context, issues, "sourceSkill");
        ValidateOptionalString(item, context, issues, "source");
    }

    private void ValidateCombatActionArray(JsonElement value, string context, List<ValidationIssue> issues,
        bool requireActionNameForActivatedEffect = true, string section = "Inventory")
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            ValidateCombatActionObject(item, $"{context}[{index++}]", issues, requireActionNameForActivatedEffect, section);
        }
    }

    private void ValidateCombatActionObject(JsonElement action, string context, List<ValidationIssue> issues,
        bool requireActionNameForActivatedEffect = true, string section = "Inventory")
    {
        if (!RequireObject(action, context, issues))
            return;

        if (action.TryGetProperty("isActivatedEffect", out _))
            ValidateOptionalBool(action, context, issues, "isActivatedEffect");

        var isActivated = action.TryGetProperty("isActivatedEffect", out var activatedEffect) &&
                          activatedEffect.ValueKind == JsonValueKind.True;
        if (requireActionNameForActivatedEffect || isActivated)
            RequireString(action, context, issues, "actionName");
        else
            ValidateOptionalString(action, context, issues, "actionName");

        if (action.TryGetProperty("actionCost", out var actionCostNode))
        {
            if (actionCostNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(actionCostNode.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.actionCost",
                    IssueSeverity.Error,
                    "Combat Action actionCost должен быть непустой строкой"));
            }
            else
            {
                var actionCost = actionCostNode.GetString() ?? string.Empty;
                if (!AllowedCombatActionCosts.Contains(actionCost))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.actionCost",
                        IssueSeverity.Error,
                        "Combat Action actionCost должен быть одним из canonical enum значений",
                        code: "combat_action_invalid_action_cost",
                        section: section,
                        expected: string.Join(" | ", AllowedCombatActionCosts),
                        actual: actionCost,
                        repairHint: "Используй actionCost только из Combat Action contract: Main, Fast или Free."));
                }
            }
        }

        if (!TryGetArray(action, "effects", $"{context}.effects", issues, out var effects))
            return;
        if (effects.GetArrayLength() == 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.effects",
                IssueSeverity.Error,
                "Combat Action effects не должен быть пустым",
                code: "combat_action_empty_effects",
                section: section,
                repairHint: "Добавь хотя бы один effect object в Combat Action effects."));
            return;
        }

        ValidateOptionalString(action, context, issues, "targetPriority");
        if (action.TryGetProperty("scalingCharacteristic", out var scalingCharacteristic) &&
            scalingCharacteristic.ValueKind != JsonValueKind.Null &&
            (scalingCharacteristic.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(scalingCharacteristic.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{context}.scalingCharacteristic",
                IssueSeverity.Error,
                "Combat Action scalingCharacteristic должен быть непустой строкой или null"));
        }

        var index = 0;
        foreach (var effect in effects.EnumerateArray())
            ValidateCombatActionEffectObject(effect, $"{context}.effects[{index++}]", issues, section);
    }

    private void ValidateCombatActionEffectObject(JsonElement effect, string context, List<ValidationIssue> issues, string section)
    {
        if (!RequireObject(effect, context, issues))
            return;

        var effectType = RequireString(effect, context, issues, "effectType");
        ValidatePercentageStringField(effect, context, issues, "value", requirePositive: false);
        RequireString(effect, context, issues, "targetType");
        RequireString(effect, context, issues, "effectDescription");
        ValidateOptionalString(effect, context, issues, "targetTypeDisplayName");
        if (effect.TryGetProperty("targetsCount", out _))
            ValidatePositiveIntegerField(effect, context, issues, "targetsCount");

        if (!string.IsNullOrWhiteSpace(effectType) && !AllowedCombatEffectTypes.Contains(effectType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.effectType",
                IssueSeverity.Error,
                "Combat Action effectType должен быть одним из canonical enum значений",
                code: "combat_action_invalid_effect_type",
                section: section,
                expected: string.Join(" | ", AllowedCombatEffectTypes),
                actual: effectType,
                repairHint: "Используй effectType только из Combat Action contract."));
        }

        var requiresDuration =
            string.Equals(effectType, "DamageOverTime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "HealOverTime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Buff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Debuff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "DamageReduction", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Control", StringComparison.OrdinalIgnoreCase);

        if (requiresDuration)
        {
            if (!effect.TryGetProperty("duration", out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.duration",
                    IssueSeverity.Error,
                    "Combat Action effect с таким effectType должен содержать duration"));
            }
            else
            {
                ValidateNumberField(effect, context, issues, "duration");
            }
        }
        else if (effect.TryGetProperty("duration", out _))
        {
            ValidateNumberField(effect, context, issues, "duration");
        }

        if (string.Equals(effectType, "Damage", StringComparison.OrdinalIgnoreCase))
        {
            RequireString(effect, context, issues, "poiseDamage");
        }
        else if (effect.TryGetProperty("poiseDamage", out var poiseDamage) && poiseDamage.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.poiseDamage",
                IssueSeverity.Error,
                "poiseDamage допустим только для effectType = Damage",
                code: "combat_action_poise_damage_wrong_effect_type",
                section: section,
                expected: "Omit poiseDamage for non-Damage effects",
                actual: effectType,
                repairHint: "Убери poiseDamage у не-Damage effect и оставь его только для effectType=Damage."));
        }

        if (effect.TryGetProperty("damageThreshold", out var damageThreshold))
        {
            if (!string.Equals(effectType, "DamageReduction", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.damageThreshold",
                    IssueSeverity.Error,
                    "damageThreshold допустим только для effectType = DamageReduction",
                    code: "combat_action_damage_threshold_wrong_effect_type",
                    section: section,
                    expected: "damageThreshold only on DamageReduction effects",
                    actual: effectType,
                    repairHint: "Убери damageThreshold у не-DamageReduction effect и используй его только для DamageReduction."));
            }
            else if (damageThreshold.ValueKind != JsonValueKind.Null)
            {
                ValidateIntegerField(effect, context, issues, "damageThreshold");
            }
        }
    }

    private void ValidateCombatResistanceArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var resistance in value.EnumerateArray())
        {
            var resistanceContext = $"{context}[{index++}]";
            if (!RequireObject(resistance, resistanceContext, issues))
                continue;

            RequireString(resistance, resistanceContext, issues, "resistanceName");
            ValidatePercentageStringField(resistance, resistanceContext, issues, "resistanceValue", requirePositive: false);
            RequireString(resistance, resistanceContext, issues, "resistType");
            RequireString(resistance, resistanceContext, issues, "resistTypeDisplayName");

            if (resistance.TryGetProperty("resistanceValue", out var resistanceValueNode) &&
                resistanceValueNode.ValueKind == JsonValueKind.String &&
                TryParsePercentageString(resistanceValueNode.GetString(), requirePositive: false, out var resistanceValue) &&
                resistanceValue > 90)
            {
                issues.Add(new ValidationIssue(
                    $"{resistanceContext}.resistanceValue",
                    IssueSeverity.Error,
                    "resistanceValue не должен превышать 90%",
                    code: "combat_resistance_value_cap_exceeded",
                    section: "Combat",
                    expected: "<= 90%",
                    actual: $"{resistanceValue}%",
                    repairHint: "Сохрани resistanceValue как percentage string и не поднимай его выше 90% по combat resistance contract."));
            }
        }
    }

    private void ValidateCombatantActiveEffectArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var effect in value.EnumerateArray())
        {
            var effectContext = $"{context}[{index++}]";
            if (!RequireObject(effect, effectContext, issues))
                continue;

            ValidateCombatantActiveEffectObject(effect, effectContext, issues);
        }
    }

    private void ValidateCombatantActiveEffectObject(JsonElement effect, string context, List<ValidationIssue> issues)
    {
        ValidateOptionalNullableStringField(effect, context, issues, "effectId");
        var effectType = RequireString(effect, context, issues, "effectType");
        ValidatePercentageStringField(effect, context, issues, "value", requirePositive: false);
        RequireString(effect, context, issues, "description");

        if (!string.IsNullOrWhiteSpace(effectType) && !AllowedCombatantActiveEffectTypes.Contains(effectType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.effectType",
                IssueSeverity.Error,
                "Combat active effect effectType должен быть одним из canonical enum значений",
                code: "combatant_effect_invalid_effect_type",
                section: "Combat",
                expected: string.Join(" | ", AllowedCombatantActiveEffectTypes),
                actual: effectType,
                repairHint: "Используй activeBuffs/activeDebuffs effectType только из Combat Effect contract, включая WoundReference для wound-linked effect."));
        }

        var isWoundReference = string.Equals(effectType, "WoundReference", StringComparison.OrdinalIgnoreCase);
        if (isWoundReference)
        {
            if (effect.TryGetProperty("targetType", out var woundTargetType) && woundTargetType.ValueKind != JsonValueKind.Null)
            {
                if (woundTargetType.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(woundTargetType.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.targetType",
                        IssueSeverity.Error,
                        "WoundReference effect targetType должен быть null/omitted или непустой строкой"));
                }
            }
        }
        else
        {
            RequireString(effect, context, issues, "targetType");
        }

        var requiresDuration =
            isWoundReference ||
            string.Equals(effectType, "DamageOverTime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "HealOverTime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Buff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Debuff", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(effectType, "Control", StringComparison.OrdinalIgnoreCase);

        if (requiresDuration)
        {
            if (!effect.TryGetProperty("duration", out _))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.duration",
                    IssueSeverity.Error,
                    "Combat active effect с таким effectType должен содержать duration"));
            }
            else
            {
                ValidateNonNegativeNumberField(effect, context, issues, "duration");
            }
        }
        else if (effect.TryGetProperty("duration", out var durationNode) && durationNode.ValueKind != JsonValueKind.Null)
        {
            ValidateNonNegativeNumberField(effect, context, issues, "duration");
        }

        if (effect.TryGetProperty("sourceSkill", out var sourceSkill) && sourceSkill.ValueKind != JsonValueKind.Null)
            RequireString(effect, context, issues, "sourceSkill");

        if (effect.TryGetProperty("sourceWoundId", out var sourceWoundId) && sourceWoundId.ValueKind != JsonValueKind.Null)
            RequireString(effect, context, issues, "sourceWoundId");

        if (string.Equals(effectType, "WoundReference", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasAnyNonEmptyString(effect, "sourceWoundId"))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.sourceWoundId",
                    IssueSeverity.Error,
                    "WoundReference effect должен содержать sourceWoundId",
                    code: "combatant_effect_missing_source_wound",
                    section: "Combat",
                    expected: "sourceWoundId for WoundReference effect",
                    actual: "missing sourceWoundId",
                    repairHint: "Для activeBuffs/activeDebuffs с effectType=WoundReference передай sourceWoundId связанной раны."));
            }

            if (effect.TryGetProperty("duration", out var durationNode) &&
                durationNode.ValueKind == JsonValueKind.Number &&
                durationNode.TryGetInt32(out var duration) &&
                duration != 999)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.duration",
                    IssueSeverity.Error,
                    "WoundReference effect должен иметь duration = 999",
                    code: "combatant_effect_invalid_wound_reference_duration",
                    section: "Combat",
                    expected: "999",
                    actual: duration.ToString(),
                    repairHint: "Для persistent wound-linked effect ставь duration = 999, пока связанная рана активна."));
            }
        }
    }

    private void ValidateWoundObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(item, context, issues))
            return;

        RequireAnyString(item, context, issues, "woundName", "name");
        ValidateOptionalString(item, context, issues, "severity");
        ValidateOptionalString(item, context, issues, "descriptionOfEffects");
        ValidateOptionalString(item, context, issues, "description");

        if (item.TryGetProperty("generatedEffects", out var generatedEffects))
            RequireArrayOfObjects(generatedEffects, $"{context}.generatedEffects", issues);
        if (item.TryGetProperty("healingState", out var healingState) && healingState.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue($"{context}.healingState", IssueSeverity.Error,
                "Поле должно быть объектом"));
        }
    }

    private void ValidateCustomStateObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(item, context, issues))
            return;

        RequireAnyString(item, context, issues, "stateName", "stateKey", "key", "name");
        ValidateOptionalString(item, context, issues, "description");
        ValidateOptionalNumberOrString(item, context, issues, "currentValue");
        ValidateOptionalNumberOrString(item, context, issues, "minValue");
        ValidateOptionalNumberOrString(item, context, issues, "maxValue");

        if (item.TryGetProperty("progressionRule", out var progressionRule) && progressionRule.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue($"{context}.progressionRule", IssueSeverity.Error,
                "Поле должно быть объектом"));
        }

        if (item.TryGetProperty("thresholds", out var thresholds))
            RequireArrayOfObjects(thresholds, $"{context}.thresholds", issues);
    }

    private static void ValidateArrayItems(JsonElement array, string context, List<ValidationIssue> issues,
        Action<JsonElement, string, List<ValidationIssue>> validator)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(context, IssueSeverity.Error, "Поле должно быть массивом"));
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
            validator(item, $"{context}[{index++}]", issues);
    }

    private static bool LooksLikeEffectObject(JsonElement item)
    {
        return item.ValueKind == JsonValueKind.Object &&
               (HasAnyNonEmptyString(item, "effectType", "effectDescription", "description", "name") ||
                item.TryGetProperty("duration", out _));
    }

    private static bool TryReadNumericLike(JsonElement value, out int parsed)
    {
        parsed = 0;
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt32(out parsed);

        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), out parsed);
    }

    private static bool HasNumberOrString(JsonElement item, params string[] props)
    {
        foreach (var prop in props)
        {
            if (!item.TryGetProperty(prop, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number)
                return true;

            if (value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                return true;
        }

        return false;
    }

    private void ValidateOptionalNumberOrString(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Number)
            return;

        if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            "Поле должно быть числом или непустой строкой"));
    }

    private void ValidateOptionalBool(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            "Поле должно быть bool"));
    }

}

