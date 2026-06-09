using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Globalization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private static readonly Regex InventoryMechanicalSummaryNumericRegex = new(
        @"[+\-]\s*\d+|\d+\s*%",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InventoryMechanicalSummaryValueRegex = new(
        @"(?<sign>[+\-])?\s*(?<number>\d+(?:[.,]\d+)?)\s*(?<percent>%?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] InventoryMechanicalSummaryTerms =
    {
        "damage", "heal", "healing", "health", "energy", "mana", "buff", "debuff",
        "duration", "turn", "round", "activated", "reputation", "stealth", "strength",
        "dexterity", "constitution", "intelligence", "wisdom", "faith", "luck",
        "speed", "perception",
        "урон", "восстанавлива", "исцел", "лечен", "здоров", "энерги", "мана", "маны",
        "репутац", "скрытност", "сила", "ловкост", "выносливост", "интеллект",
        "мудрост", "вера", "удач", "скорост", "восприяти", "аркановед",
        "бафф", "дебафф", "штраф", "бонус", "перезаряд", "длительност",
        "активируем", "активац"
    };

    private static readonly string[][] InventoryMechanicalAuthorityTargetAliases =
    {
        new[] { "strength", "сила", "силы" },
        new[] { "dexterity", "ловкость", "ловкости" },
        new[] { "constitution", "выносливость", "выносливости" },
        new[] { "intelligence", "интеллект", "интеллекта" },
        new[] { "wisdom", "мудрость", "мудрости" },
        new[] { "faith", "вера", "веры" },
        new[] { "luck", "удача", "удачи" },
        new[] { "speed", "скорость", "скорости" },
        new[] { "perception", "восприятие", "восприятия" },
        new[] { "stealth", "скрытность", "скрытности" },
        new[] { "arcana", "arcanum", "аркановедение", "аркановед" },
        new[] { "reputation", "репутация", "репутац" },
        new[] { "health", "hp", "heal", "healing", "здоровье", "здоровья", "исцел", "восстанавлива" },
        new[] { "damage", "урон", "поврежден" },
        new[] { "duration", "turn", "round", "длительность", "ход", "раунд" },
        new[] { "condition", "state", "состояние", "условие" }
    };

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

        var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();

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

        if (manifest == null)
        {
            issues.Add(new ValidationIssue(
                QteSceneService.QteOfferPath,
                IssueSeverity.Error,
                "QTE offer требует current validated pending turn manifest обычного игрокского хода",
                code: "qte_missing_pending_manifest",
                section: "QTE",
                expected: PendingTurnSnapshotManifestPath,
                actual: "missing or invalid validated manifest"));
        }
        else
        {
            var preTurnRealm = await ReadRequiredValidatedPendingTurnSnapshotRealmAsync(
                manifest,
                QteSceneService.QteOfferPath,
                issues,
                code: "qte_invalid_validated_snapshot_realm",
                section: "QTE",
                message: "QTE offer требует validated pre-turn realm из snapshot soul_state.",
                repairHint: "QTE offer допустим только при current validated pending turn snapshot с canonical pre-turn soul_state.json.");
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

            var changedTrackedFiles = await GetChangedTrackedFilesAgainstManifestAsync(
                manifest,
                issues,
                "qte_offer_missing_validated_tracked_baseline",
                "QTE",
                "Для QTE-offer validation tracked pre-turn files должны иметь validated snapshot entry/hash; missing baseline недопустим.");
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

            switch (await DescribeTrackedFileChangeAgainstManifestAsync(manifest, InkFeatherActionResultPath))
            {
                case ValidatedTrackedFileChangeStatus.Changed:
                    issues.Add(new ValidationIssue(
                        InkFeatherActionResultPath,
                        IssueSeverity.Error,
                        "QTE-offer turn не может одновременно резолвить Ink Feather action или другой отдельный sidecar outcome.",
                        code: "qte_offer_forbidden_sidecar_output",
                        section: "QTE",
                        repairHint: "Раздели QTE offer и обычный outcome на разные GM turns."));
                    break;
                case ValidatedTrackedFileChangeStatus.MissingValidatedBaseline:
                    AddMissingValidatedTrackedBaselineIssue(
                        issues,
                        InkFeatherActionResultPath,
                        "qte_offer_missing_validated_tracked_baseline",
                        "QTE",
                        "QTE-offer validation не может строго определить, был ли создан Ink Feather sidecar outcome: validated pre-turn baseline missing.",
                        "Для QTE-offer validation tracked output surfaces должны иметь validated snapshot baseline или отсутствовать как truly new outputs.");
                    break;
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
                        checkType is not "BranchChoice" and not "TimingBar" and not "PromptChain" and not "BalanceMeter" and not "ChargeRelease" and not "MashInput" and not "PatternMemory" and not "RhythmPulse" and not "PrecisionChoice")
                    {
                        issues.Add(new ValidationIssue(
                            $"{actionContext}.check.type",
                            IssueSeverity.Error,
                            "Неподдерживаемый QTE check type",
                            code: "qte_invalid_check_type",
                            section: "QTE",
                            expected: "BranchChoice | TimingBar | PromptChain | BalanceMeter | ChargeRelease | MashInput | PatternMemory | RhythmPulse | PrecisionChoice",
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
                    else if (string.Equals(checkType, "MashInput", StringComparison.Ordinal))
                    {
                        ValidateMashInputConfig(check, actionContext, issues);
                    }
                    else if (string.Equals(checkType, "PatternMemory", StringComparison.Ordinal))
                    {
                        ValidatePatternMemoryConfig(check, actionContext, issues);
                    }
                    else if (string.Equals(checkType, "RhythmPulse", StringComparison.Ordinal))
                    {
                        ValidateRhythmPulseConfig(check, actionContext, issues);
                    }
                    else if (string.Equals(checkType, "PrecisionChoice", StringComparison.Ordinal))
                    {
                        ValidatePrecisionChoiceConfig(check, actionContext, issues);
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

    private static void ValidateMashInputConfig(JsonElement check, string actionContext, List<ValidationIssue> issues)
    {
        var configContext = $"{actionContext}.check.config";
        if (!check.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                configContext,
                IssueSeverity.Error,
                "MashInput требует check.config object",
                code: "qte_mash_input_config_missing",
                section: "QTE",
                expected: "config object with keys, durationMs, targetPresses, partialThreshold",
                actual: check.TryGetProperty("config", out var existingConfig) ? existingConfig.ValueKind.ToString() : "missing"));
            return;
        }

        ValidateMashInputKeys(config, configContext, issues);

        var hasDuration = TryReadRequiredMashInputInteger(
            config,
            configContext,
            issues,
            "durationMs",
            "qte_mash_input_duration_missing",
            "qte_mash_input_duration_invalid",
            out var durationMs);
        if (hasDuration &&
            (durationMs < QteSceneService.MashInputMinDurationMs || durationMs > QteSceneService.MashInputMaxDurationMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.durationMs",
                IssueSeverity.Error,
                "MashInput durationMs должен быть в игровом диапазоне",
                code: "qte_mash_input_duration_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.MashInputMinDurationMs}..{QteSceneService.MashInputMaxDurationMs}",
                actual: durationMs.ToString(CultureInfo.InvariantCulture),
                repairHint: "Используй короткое кинематографическое окно, пригодное для локальной QTE-сцены."));
        }

        var hasTarget = TryReadRequiredMashInputInteger(
            config,
            configContext,
            issues,
            "targetPresses",
            "qte_mash_input_target_missing",
            "qte_mash_input_target_invalid",
            out var targetPresses);
        if (hasTarget && targetPresses <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.targetPresses",
                IssueSeverity.Error,
                "MashInput targetPresses должен быть положительным целым числом",
                code: "qte_mash_input_target_invalid",
                section: "QTE",
                expected: "> 0",
                actual: targetPresses.ToString(CultureInfo.InvariantCulture)));
            hasTarget = false;
        }

        if (hasTarget && targetPresses > QteSceneService.MashInputMaxTargetPresses)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.targetPresses",
                IssueSeverity.Error,
                "MashInput targetPresses превышает поддерживаемый максимум",
                code: "qte_mash_input_target_out_of_range",
                section: "QTE",
                expected: $"<= {QteSceneService.MashInputMaxTargetPresses}",
                actual: targetPresses.ToString(CultureInfo.InvariantCulture)));
            hasTarget = false;
        }

        if (hasDuration && hasTarget && durationMs >= QteSceneService.MashInputMinDurationMs)
        {
            var maxTarget = QteSceneService.ComputeMashInputMaxTargetPressesForDuration(durationMs);
            if (targetPresses > maxTarget)
            {
                issues.Add(new ValidationIssue(
                    $"{configContext}.targetPresses",
                    IssueSeverity.Error,
                    "MashInput targetPresses невозможен для заданного durationMs",
                    code: "qte_mash_input_target_impossible",
                    section: "QTE",
                    expected: $"<= {maxTarget} for durationMs={durationMs}",
                    actual: targetPresses.ToString(CultureInfo.InvariantCulture),
                    repairHint: "Уменьши targetPresses или увеличь durationMs, чтобы требование оставалось физически выполнимым."));
            }
        }

        if (!config.TryGetProperty("partialThreshold", out var partialThreshold))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.partialThreshold",
                IssueSeverity.Error,
                "MashInput требует partialThreshold",
                code: "qte_mash_input_partial_threshold_missing",
                section: "QTE",
                expected: "number > 0 and <= 1",
                actual: "missing"));
        }
        else if (partialThreshold.ValueKind != JsonValueKind.Number || !partialThreshold.TryGetDouble(out var threshold))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.partialThreshold",
                IssueSeverity.Error,
                "MashInput partialThreshold должен быть числом",
                code: "qte_mash_input_partial_threshold_invalid",
                section: "QTE",
                expected: "number > 0 and <= 1",
                actual: partialThreshold.ValueKind.ToString()));
        }
        else if (threshold <= 0 || threshold > 1)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.partialThreshold",
                IssueSeverity.Error,
                "MashInput partialThreshold должен быть в диапазоне (0..1]",
                code: "qte_mash_input_partial_threshold_out_of_range",
                section: "QTE",
                expected: "> 0 and <= 1",
                actual: threshold.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void ValidateMashInputKeys(JsonElement config, string configContext, List<ValidationIssue> issues)
    {
        if (!config.TryGetProperty("keys", out var keys))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.keys",
                IssueSeverity.Error,
                "MashInput требует keys array",
                code: "qte_mash_input_keys_missing",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: "missing"));
            return;
        }

        if (keys.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.keys",
                IssueSeverity.Error,
                "MashInput keys должен быть массивом canonical QTE key tokens",
                code: "qte_mash_input_keys_invalid",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: keys.ValueKind.ToString()));
            return;
        }

        if (keys.GetArrayLength() == 0)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.keys",
                IssueSeverity.Error,
                "MashInput требует хотя бы одну QTE клавишу",
                code: "qte_mash_input_keys_empty",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: "empty array"));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var key in keys.EnumerateArray())
        {
            var keyContext = $"{configContext}.keys[{index++}]";
            if (key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString()))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "MashInput key token должен быть непустой строкой",
                    code: "qte_mash_input_key_invalid",
                    section: "QTE",
                    expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                    actual: key.ValueKind.ToString()));
                continue;
            }

            var token = key.GetString()!.Trim();
            if (!string.Equals(token, token.ToLowerInvariant(), StringComparison.Ordinal) ||
                !QteKeyInput.IsSupportedToken(token))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "MashInput key token должен быть canonical supported QTE key",
                    code: "qte_mash_input_key_invalid",
                    section: "QTE",
                    expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                    actual: token));
                continue;
            }

            if (!seen.Add(token))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "MashInput keys не должен содержать повторяющиеся клавиши",
                    code: "qte_mash_input_key_duplicate",
                    section: "QTE",
                    expected: "unique QTE key tokens",
                    actual: token));
            }
        }
    }

    private static void ValidatePatternMemoryConfig(JsonElement check, string actionContext, List<ValidationIssue> issues)
    {
        var configContext = $"{actionContext}.check.config";
        if (!check.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                configContext,
                IssueSeverity.Error,
                "PatternMemory требует check.config object",
                code: "qte_pattern_memory_config_missing",
                section: "QTE",
                expected: "config object with alphabet, sequenceLength, revealMs, inputTimeoutMs, allowedMistakes",
                actual: check.TryGetProperty("config", out var existingConfig) ? existingConfig.ValueKind.ToString() : "missing"));
            return;
        }

        ValidatePatternMemoryAlphabet(config, configContext, issues);

        var hasSequenceLength = TryReadRequiredPatternMemoryInteger(
            config,
            configContext,
            issues,
            "sequenceLength",
            "qte_pattern_memory_sequence_length_missing",
            "qte_pattern_memory_sequence_length_invalid",
            out var sequenceLength);
        if (hasSequenceLength &&
            (sequenceLength < QteSceneService.PatternMemoryMinSequenceLength ||
             sequenceLength > QteSceneService.PatternMemoryMaxSequenceLength))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.sequenceLength",
                IssueSeverity.Error,
                "PatternMemory sequenceLength должен быть в игровом диапазоне",
                code: "qte_pattern_memory_sequence_length_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.PatternMemoryMinSequenceLength}..{QteSceneService.PatternMemoryMaxSequenceLength}",
                actual: sequenceLength.ToString(CultureInfo.InvariantCulture)));
            hasSequenceLength = false;
        }

        var hasRevealMs = TryReadRequiredPatternMemoryInteger(
            config,
            configContext,
            issues,
            "revealMs",
            "qte_pattern_memory_reveal_ms_missing",
            "qte_pattern_memory_reveal_ms_invalid",
            out var revealMs);
        if (hasRevealMs &&
            (revealMs < QteSceneService.PatternMemoryMinRevealMs ||
             revealMs > QteSceneService.PatternMemoryMaxRevealMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.revealMs",
                IssueSeverity.Error,
                "PatternMemory revealMs должен быть в игровом диапазоне",
                code: "qte_pattern_memory_reveal_ms_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.PatternMemoryMinRevealMs}..{QteSceneService.PatternMemoryMaxRevealMs}",
                actual: revealMs.ToString(CultureInfo.InvariantCulture),
                repairHint: "Используй короткую фазу показа, которую можно запомнить в кинематографической сцене."));
        }

        var hasInputTimeoutMs = TryReadRequiredPatternMemoryInteger(
            config,
            configContext,
            issues,
            "inputTimeoutMs",
            "qte_pattern_memory_input_timeout_ms_missing",
            "qte_pattern_memory_input_timeout_ms_invalid",
            out var inputTimeoutMs);
        if (hasInputTimeoutMs &&
            (inputTimeoutMs < QteSceneService.PatternMemoryMinInputTimeoutMs ||
             inputTimeoutMs > QteSceneService.PatternMemoryMaxInputTimeoutMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.inputTimeoutMs",
                IssueSeverity.Error,
                "PatternMemory inputTimeoutMs должен быть в игровом диапазоне",
                code: "qte_pattern_memory_input_timeout_ms_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.PatternMemoryMinInputTimeoutMs}..{QteSceneService.PatternMemoryMaxInputTimeoutMs}",
                actual: inputTimeoutMs.ToString(CultureInfo.InvariantCulture)));
            hasInputTimeoutMs = false;
        }

        if (hasSequenceLength && hasInputTimeoutMs)
        {
            var minimumTimeoutMs = sequenceLength * QteSceneService.PatternMemoryMinInputMsPerSymbol;
            if (inputTimeoutMs < minimumTimeoutMs)
            {
                issues.Add(new ValidationIssue(
                    $"{configContext}.inputTimeoutMs",
                    IssueSeverity.Error,
                    "PatternMemory inputTimeoutMs невозможен для заданного sequenceLength",
                    code: "qte_pattern_memory_input_timeout_ms_impossible",
                    section: "QTE",
                    expected: $">= {minimumTimeoutMs} for sequenceLength={sequenceLength}",
                    actual: inputTimeoutMs.ToString(CultureInfo.InvariantCulture),
                    repairHint: "Увеличь inputTimeoutMs или сократи sequenceLength, чтобы у игрока было время повторить всю последовательность."));
            }
        }

        var hasAllowedMistakes = TryReadRequiredPatternMemoryInteger(
            config,
            configContext,
            issues,
            "allowedMistakes",
            "qte_pattern_memory_allowed_mistakes_missing",
            "qte_pattern_memory_allowed_mistakes_invalid",
            out var allowedMistakes);
        if (hasAllowedMistakes && hasSequenceLength &&
            (allowedMistakes < 0 || allowedMistakes >= sequenceLength))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.allowedMistakes",
                IssueSeverity.Error,
                "PatternMemory allowedMistakes должен оставлять возможность провала",
                code: "qte_pattern_memory_allowed_mistakes_out_of_range",
                section: "QTE",
                expected: $"0..{sequenceLength - 1}",
                actual: allowedMistakes.ToString(CultureInfo.InvariantCulture)));
        }
        else if (hasAllowedMistakes && allowedMistakes < 0)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.allowedMistakes",
                IssueSeverity.Error,
                "PatternMemory allowedMistakes не может быть отрицательным",
                code: "qte_pattern_memory_allowed_mistakes_out_of_range",
                section: "QTE",
                expected: ">= 0",
                actual: allowedMistakes.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static void ValidateRhythmPulseConfig(JsonElement check, string actionContext, List<ValidationIssue> issues)
    {
        var configContext = $"{actionContext}.check.config";
        if (!check.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                configContext,
                IssueSeverity.Error,
                "RhythmPulse требует check.config object",
                code: "qte_rhythm_pulse_config_missing",
                section: "QTE",
                expected: "config object with pulseCount, beatIntervalMs, hitWindowMs, allowedMisses",
                actual: check.TryGetProperty("config", out var existingConfig) ? existingConfig.ValueKind.ToString() : "missing"));
            return;
        }

        var hasPulseCount = TryReadRequiredRhythmPulseInteger(
            config,
            configContext,
            issues,
            "pulseCount",
            "qte_rhythm_pulse_pulse_count_missing",
            "qte_rhythm_pulse_pulse_count_invalid",
            out var pulseCount);
        if (hasPulseCount &&
            (pulseCount < QteSceneService.RhythmPulseMinPulseCount ||
             pulseCount > QteSceneService.RhythmPulseMaxPulseCount))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.pulseCount",
                IssueSeverity.Error,
                "RhythmPulse pulseCount должен быть в игровом диапазоне",
                code: "qte_rhythm_pulse_pulse_count_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.RhythmPulseMinPulseCount}..{QteSceneService.RhythmPulseMaxPulseCount}",
                actual: pulseCount.ToString(CultureInfo.InvariantCulture),
                repairHint: "Используй короткий ритмический рисунок с несколькими пульсами."));
            hasPulseCount = false;
        }

        var hasBeatInterval = TryReadRequiredRhythmPulseInteger(
            config,
            configContext,
            issues,
            "beatIntervalMs",
            "qte_rhythm_pulse_beat_interval_ms_missing",
            "qte_rhythm_pulse_beat_interval_ms_invalid",
            out var beatIntervalMs);
        if (hasBeatInterval &&
            (beatIntervalMs < QteSceneService.RhythmPulseMinBeatIntervalMs ||
             beatIntervalMs > QteSceneService.RhythmPulseMaxBeatIntervalMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.beatIntervalMs",
                IssueSeverity.Error,
                "RhythmPulse beatIntervalMs должен быть в игровом диапазоне",
                code: "qte_rhythm_pulse_beat_interval_ms_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.RhythmPulseMinBeatIntervalMs}..{QteSceneService.RhythmPulseMaxBeatIntervalMs}",
                actual: beatIntervalMs.ToString(CultureInfo.InvariantCulture),
                repairHint: "Используй ритм, который можно увидеть и нажать в короткой QTE-сцене."));
            hasBeatInterval = false;
        }

        var hasHitWindow = TryReadRequiredRhythmPulseInteger(
            config,
            configContext,
            issues,
            "hitWindowMs",
            "qte_rhythm_pulse_hit_window_ms_missing",
            "qte_rhythm_pulse_hit_window_ms_invalid",
            out var hitWindowMs);
        if (hasHitWindow &&
            (hitWindowMs < QteSceneService.RhythmPulseMinHitWindowMs ||
             hitWindowMs > QteSceneService.RhythmPulseMaxHitWindowMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.hitWindowMs",
                IssueSeverity.Error,
                "RhythmPulse hitWindowMs должен быть в игровом диапазоне",
                code: "qte_rhythm_pulse_hit_window_ms_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.RhythmPulseMinHitWindowMs}..{QteSceneService.RhythmPulseMaxHitWindowMs}",
                actual: hitWindowMs.ToString(CultureInfo.InvariantCulture)));
            hasHitWindow = false;
        }

        if (hasBeatInterval && hasHitWindow && hitWindowMs * 2 >= beatIntervalMs)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.hitWindowMs",
                IssueSeverity.Error,
                "RhythmPulse hitWindowMs не должен перекрывать соседние пульсы",
                code: "qte_rhythm_pulse_hit_window_ms_overlaps",
                section: "QTE",
                expected: $"hitWindowMs * 2 < beatIntervalMs ({beatIntervalMs})",
                actual: hitWindowMs.ToString(CultureInfo.InvariantCulture),
                repairHint: "Сократи hitWindowMs или увеличь beatIntervalMs, чтобы окна попадания оставались различимыми."));
        }

        var hasAllowedMisses = TryReadRequiredRhythmPulseInteger(
            config,
            configContext,
            issues,
            "allowedMisses",
            "qte_rhythm_pulse_allowed_misses_missing",
            "qte_rhythm_pulse_allowed_misses_invalid",
            out var allowedMisses);
        if (hasAllowedMisses && hasPulseCount &&
            (allowedMisses < 0 || allowedMisses >= pulseCount))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.allowedMisses",
                IssueSeverity.Error,
                "RhythmPulse allowedMisses должен оставлять возможность провала",
                code: "qte_rhythm_pulse_allowed_misses_out_of_range",
                section: "QTE",
                expected: $"0..{pulseCount - 1}",
                actual: allowedMisses.ToString(CultureInfo.InvariantCulture)));
        }
        else if (hasAllowedMisses && allowedMisses < 0)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.allowedMisses",
                IssueSeverity.Error,
                "RhythmPulse allowedMisses не может быть отрицательным",
                code: "qte_rhythm_pulse_allowed_misses_out_of_range",
                section: "QTE",
                expected: ">= 0",
                actual: allowedMisses.ToString(CultureInfo.InvariantCulture)));
        }

        if (!config.TryGetProperty("patternVariation", out var variation) ||
            variation.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (variation.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(variation.GetString()) ||
            !QteSceneService.RhythmPulsePatternVariations.Contains(variation.GetString()!.Trim(), StringComparer.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.patternVariation",
                IssueSeverity.Error,
                "RhythmPulse patternVariation должен быть поддерживаемым canonical token или null",
                code: "qte_rhythm_pulse_pattern_variation_invalid",
                section: "QTE",
                expected: string.Join(" | ", QteSceneService.RhythmPulsePatternVariations),
                actual: variation.ValueKind == JsonValueKind.String ? variation.GetString() : variation.ValueKind.ToString()));
        }
    }

    private static void ValidatePrecisionChoiceConfig(JsonElement check, string actionContext, List<ValidationIssue> issues)
    {
        var configContext = $"{actionContext}.check.config";
        if (!check.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                configContext,
                IssueSeverity.Error,
                "PrecisionChoice требует check.config object",
                code: "qte_precision_choice_config_missing",
                section: "QTE",
                expected: "config object with choices, correctChoiceId, timeoutMs, optional timeoutGrade and decoyHints",
                actual: check.TryGetProperty("config", out var existingConfig) ? existingConfig.ValueKind.ToString() : "missing"));
            return;
        }

        var choiceGradesById = ValidatePrecisionChoiceChoices(config, configContext, issues);
        ValidatePrecisionChoiceCorrectChoice(config, configContext, choiceGradesById, issues);

        if (choiceGradesById.Count > 0)
        {
            var successChoices = choiceGradesById
                .Where(pair => string.Equals(pair.Value, "success", StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();
            if (successChoices.Length != 1)
            {
                issues.Add(new ValidationIssue(
                    $"{configContext}.choices",
                    IssueSeverity.Error,
                    "PrecisionChoice должен иметь ровно один success choice",
                    code: "qte_precision_choice_success_choice_count_invalid",
                    section: "QTE",
                    expected: "exactly one choice.grade = success",
                    actual: successChoices.Length.ToString(CultureInfo.InvariantCulture)));
            }

            if (successChoices.Length == choiceGradesById.Count)
            {
                issues.Add(new ValidationIssue(
                    $"{configContext}.choices",
                    IssueSeverity.Error,
                    "PrecisionChoice должен содержать хотя бы один non-success вариант",
                    code: "qte_precision_choice_missing_decoy_choice",
                    section: "QTE",
                    expected: "at least one partial or fail choice",
                    actual: "all choices are success"));
            }
        }

        ValidatePrecisionChoiceTimeout(config, configContext, issues);
        ValidatePrecisionChoiceTimeoutGrade(config, configContext, issues);
        ValidatePrecisionChoiceDecoyHints(config, configContext, choiceGradesById, issues);
    }

    private static Dictionary<string, string> ValidatePrecisionChoiceChoices(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues)
    {
        var choiceGradesById = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!config.TryGetProperty("choices", out var choices))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.choices",
                IssueSeverity.Error,
                "PrecisionChoice требует choices array",
                code: "qte_precision_choice_choices_missing",
                section: "QTE",
                expected: $"{QteSceneService.PrecisionChoiceMinChoices}..{QteSceneService.PrecisionChoiceMaxChoices} choice objects",
                actual: "missing"));
            return choiceGradesById;
        }

        if (choices.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.choices",
                IssueSeverity.Error,
                "PrecisionChoice choices должен быть массивом",
                code: "qte_precision_choice_choices_invalid",
                section: "QTE",
                expected: "array of choice objects",
                actual: choices.ValueKind.ToString()));
            return choiceGradesById;
        }

        var choiceCount = choices.GetArrayLength();
        if (choiceCount < QteSceneService.PrecisionChoiceMinChoices ||
            choiceCount > QteSceneService.PrecisionChoiceMaxChoices)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.choices",
                IssueSeverity.Error,
                "PrecisionChoice choices должен быть в игровом диапазоне",
                code: "qte_precision_choice_choices_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.PrecisionChoiceMinChoices}..{QteSceneService.PrecisionChoiceMaxChoices}",
                actual: choiceCount.ToString(CultureInfo.InvariantCulture)));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var choice in choices.EnumerateArray())
        {
            var choiceContext = $"{configContext}.choices[{index++}]";
            if (choice.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    choiceContext,
                    IssueSeverity.Error,
                    "PrecisionChoice choice должен быть object",
                    code: "qte_precision_choice_choice_invalid",
                    section: "QTE",
                    expected: "choice object",
                    actual: choice.ValueKind.ToString()));
                continue;
            }

            var id = ReadRequiredPrecisionChoiceString(
                choice,
                choiceContext,
                issues,
                "id",
                "qte_precision_choice_choice_id_missing",
                "PrecisionChoice choice требует id");
            var idIsUnique = false;
            if (!string.IsNullOrWhiteSpace(id))
            {
                idIsUnique = seenIds.Add(id);
                if (!idIsUnique)
                {
                    issues.Add(new ValidationIssue(
                        $"{choiceContext}.id",
                        IssueSeverity.Error,
                        "PrecisionChoice choice id должен быть уникальным",
                        code: "qte_precision_choice_choice_id_duplicate",
                        section: "QTE",
                        expected: "unique choice ids",
                        actual: id));
                }
            }

            ReadRequiredPrecisionChoiceString(
                choice,
                choiceContext,
                issues,
                "label",
                "qte_precision_choice_choice_label_missing",
                "PrecisionChoice choice требует label");

            var grade = ReadRequiredPrecisionChoiceString(
                choice,
                choiceContext,
                issues,
                "grade",
                "qte_precision_choice_choice_grade_invalid",
                "PrecisionChoice choice требует grade");
            if (!string.IsNullOrWhiteSpace(grade) && !AllowedQteChoiceGrades.Contains(grade))
            {
                issues.Add(new ValidationIssue(
                    $"{choiceContext}.grade",
                    IssueSeverity.Error,
                    "PrecisionChoice choice grade должен быть success | partial | fail",
                    code: "qte_precision_choice_choice_grade_invalid",
                    section: "QTE",
                    expected: "success | partial | fail",
                    actual: grade,
                    repairHint: "Используй точное lowercase значение без лишних пробелов."));
            }
            else if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(grade) && idIsUnique)
            {
                choiceGradesById[id] = grade;
            }
        }

        return choiceGradesById;
    }

    private static void ValidatePrecisionChoiceCorrectChoice(
        JsonElement config,
        string configContext,
        IReadOnlyDictionary<string, string> choiceGradesById,
        List<ValidationIssue> issues)
    {
        var correctChoiceId = ReadRequiredPrecisionChoiceString(
            config,
            configContext,
            issues,
            "correctChoiceId",
            "qte_precision_choice_correct_choice_missing",
            "PrecisionChoice требует correctChoiceId");
        if (string.IsNullOrWhiteSpace(correctChoiceId))
            return;

        if (!choiceGradesById.TryGetValue(correctChoiceId, out var correctGrade))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.correctChoiceId",
                IssueSeverity.Error,
                "PrecisionChoice correctChoiceId должен ссылаться на существующий choice id",
                code: "qte_precision_choice_correct_choice_unknown",
                section: "QTE",
                expected: "one configured choice id",
                actual: correctChoiceId));
            return;
        }

        if (!string.Equals(correctGrade, "success", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.correctChoiceId",
                IssueSeverity.Error,
                "PrecisionChoice correctChoiceId должен указывать на choice с grade success",
                code: "qte_precision_choice_correct_choice_not_success",
                section: "QTE",
                expected: "choice.grade = success",
                actual: correctGrade));
        }
    }

    private static void ValidatePrecisionChoiceTimeout(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues)
    {
        var hasTimeout = TryReadRequiredPrecisionChoiceInteger(
            config,
            configContext,
            issues,
            "timeoutMs",
            "qte_precision_choice_timeout_missing",
            "qte_precision_choice_timeout_invalid",
            out var timeoutMs);
        if (hasTimeout &&
            (timeoutMs < QteSceneService.PrecisionChoiceMinTimeoutMs ||
             timeoutMs > QteSceneService.PrecisionChoiceMaxTimeoutMs))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.timeoutMs",
                IssueSeverity.Error,
                "PrecisionChoice timeoutMs должен быть в игровом диапазоне",
                code: "qte_precision_choice_timeout_out_of_range",
                section: "QTE",
                expected: $"{QteSceneService.PrecisionChoiceMinTimeoutMs}..{QteSceneService.PrecisionChoiceMaxTimeoutMs}",
                actual: timeoutMs.ToString(CultureInfo.InvariantCulture),
                repairHint: "Используй короткое, но читаемое окно выбора под таймером."));
        }
    }

    private static void ValidatePrecisionChoiceTimeoutGrade(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues)
    {
        if (!config.TryGetProperty("timeoutGrade", out var timeoutGrade) ||
            timeoutGrade.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (timeoutGrade.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(timeoutGrade.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.timeoutGrade",
                IssueSeverity.Error,
                "PrecisionChoice timeoutGrade должен быть fail или partial",
                code: "qte_precision_choice_timeout_grade_invalid",
                section: "QTE",
                expected: "fail | partial",
                actual: timeoutGrade.ValueKind.ToString()));
            return;
        }

        var grade = timeoutGrade.GetString()!.Trim();
        if (!string.Equals(grade, "fail", StringComparison.Ordinal) &&
            !string.Equals(grade, "partial", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.timeoutGrade",
                IssueSeverity.Error,
                "PrecisionChoice timeoutGrade не может быть success и должен быть fail или partial",
                code: "qte_precision_choice_timeout_grade_invalid",
                section: "QTE",
                expected: "fail | partial",
                actual: grade));
        }
    }

    private static void ValidatePrecisionChoiceDecoyHints(
        JsonElement config,
        string configContext,
        IReadOnlyDictionary<string, string> choiceGradesById,
        List<ValidationIssue> issues)
    {
        if (!config.TryGetProperty("decoyHints", out var decoyHints) ||
            decoyHints.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (decoyHints.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.decoyHints",
                IssueSeverity.Error,
                "PrecisionChoice decoyHints должен быть array",
                code: "qte_precision_choice_decoy_hints_invalid",
                section: "QTE",
                expected: "array of { choiceId, hint } objects",
                actual: decoyHints.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var decoyHint in decoyHints.EnumerateArray())
        {
            var hintContext = $"{configContext}.decoyHints[{index++}]";
            if (decoyHint.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    hintContext,
                    IssueSeverity.Error,
                    "PrecisionChoice decoyHint должен быть object",
                    code: "qte_precision_choice_decoy_hint_invalid",
                    section: "QTE",
                    expected: "{ choiceId, hint } object",
                    actual: decoyHint.ValueKind.ToString()));
                continue;
            }

            var choiceId = ReadRequiredPrecisionChoiceString(
                decoyHint,
                hintContext,
                issues,
                "choiceId",
                "qte_precision_choice_decoy_hint_invalid",
                "PrecisionChoice decoyHint требует choiceId");
            ReadRequiredPrecisionChoiceString(
                decoyHint,
                hintContext,
                issues,
                "hint",
                "qte_precision_choice_decoy_hint_invalid",
                "PrecisionChoice decoyHint требует непустой hint");

            if (string.IsNullOrWhiteSpace(choiceId))
                continue;

            if (!choiceGradesById.TryGetValue(choiceId, out var choiceGrade))
            {
                issues.Add(new ValidationIssue(
                    $"{hintContext}.choiceId",
                    IssueSeverity.Error,
                    "PrecisionChoice decoyHint choiceId должен ссылаться на существующий choice",
                    code: "qte_precision_choice_decoy_hint_unknown_choice",
                    section: "QTE",
                    expected: "configured non-success choice id",
                    actual: choiceId));
                continue;
            }

            if (string.Equals(choiceGrade, "success", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"{hintContext}.choiceId",
                    IssueSeverity.Error,
                    "PrecisionChoice decoyHint не должен ссылаться на success choice",
                    code: "qte_precision_choice_decoy_hint_success_choice",
                    section: "QTE",
                    expected: "partial or fail choice id",
                    actual: choiceId));
            }
        }
    }

    private static string? ReadRequiredPrecisionChoiceString(
        JsonElement root,
        string context,
        List<ValidationIssue> issues,
        string propName,
        string code,
        string message)
    {
        if (!root.TryGetProperty(propName, out var node) ||
            node.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(node.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propName}",
                IssueSeverity.Error,
                message,
                code: code,
                section: "QTE",
                expected: "non-empty string",
                actual: root.TryGetProperty(propName, out var existingNode) ? existingNode.ValueKind.ToString() : "missing"));
            return null;
        }

        return node.GetString()!.Trim();
    }

    private static bool TryReadRequiredPrecisionChoiceInteger(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues,
        string propName,
        string missingCode,
        string invalidCode,
        out int value)
    {
        value = 0;
        if (!config.TryGetProperty(propName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.{propName}",
                IssueSeverity.Error,
                $"PrecisionChoice требует {propName}",
                code: missingCode,
                section: "QTE",
                expected: "integer",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out value))
            return true;

        issues.Add(new ValidationIssue(
            $"{configContext}.{propName}",
            IssueSeverity.Error,
            $"PrecisionChoice {propName} должен быть целым числом",
            code: invalidCode,
            section: "QTE",
            expected: "integer",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private static void ValidatePatternMemoryAlphabet(JsonElement config, string configContext, List<ValidationIssue> issues)
    {
        if (!config.TryGetProperty("alphabet", out var alphabet))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.alphabet",
                IssueSeverity.Error,
                "PatternMemory требует alphabet array",
                code: "qte_pattern_memory_alphabet_missing",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: "missing"));
            return;
        }

        if (alphabet.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.alphabet",
                IssueSeverity.Error,
                "PatternMemory alphabet должен быть массивом canonical QTE key tokens",
                code: "qte_pattern_memory_alphabet_invalid",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: alphabet.ValueKind.ToString()));
            return;
        }

        if (alphabet.GetArrayLength() == 0)
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.alphabet",
                IssueSeverity.Error,
                "PatternMemory требует хотя бы одну QTE клавишу в alphabet",
                code: "qte_pattern_memory_alphabet_empty",
                section: "QTE",
                expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                actual: "empty array"));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var key in alphabet.EnumerateArray())
        {
            var keyContext = $"{configContext}.alphabet[{index++}]";
            if (key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString()))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "PatternMemory alphabet token должен быть непустой строкой",
                    code: "qte_pattern_memory_alphabet_token_invalid",
                    section: "QTE",
                    expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                    actual: key.ValueKind.ToString()));
                continue;
            }

            var token = key.GetString()!.Trim();
            if (!string.Equals(token, token.ToLowerInvariant(), StringComparison.Ordinal) ||
                !QteKeyInput.IsSupportedToken(token))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "PatternMemory alphabet token должен быть canonical supported QTE key",
                    code: "qte_pattern_memory_alphabet_token_invalid",
                    section: "QTE",
                    expected: string.Join(" | ", QteKeyInput.SupportedTokens),
                    actual: token));
                continue;
            }

            if (!seen.Add(token))
            {
                issues.Add(new ValidationIssue(
                    keyContext,
                    IssueSeverity.Error,
                    "PatternMemory alphabet не должен содержать повторяющиеся клавиши",
                    code: "qte_pattern_memory_alphabet_duplicate",
                    section: "QTE",
                    expected: "unique QTE key tokens",
                    actual: token));
            }
        }
    }

    private static bool TryReadRequiredPatternMemoryInteger(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues,
        string propName,
        string missingCode,
        string invalidCode,
        out int value)
    {
        value = 0;
        if (!config.TryGetProperty(propName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.{propName}",
                IssueSeverity.Error,
                $"PatternMemory требует {propName}",
                code: missingCode,
                section: "QTE",
                expected: "integer",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out value))
            return true;

        issues.Add(new ValidationIssue(
            $"{configContext}.{propName}",
            IssueSeverity.Error,
            $"PatternMemory {propName} должен быть целым числом",
            code: invalidCode,
            section: "QTE",
            expected: "integer",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private static bool TryReadRequiredRhythmPulseInteger(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues,
        string propName,
        string missingCode,
        string invalidCode,
        out int value)
    {
        value = 0;
        if (!config.TryGetProperty(propName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.{propName}",
                IssueSeverity.Error,
                $"RhythmPulse требует {propName}",
                code: missingCode,
                section: "QTE",
                expected: "integer",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out value))
            return true;

        issues.Add(new ValidationIssue(
            $"{configContext}.{propName}",
            IssueSeverity.Error,
            $"RhythmPulse {propName} должен быть целым числом",
            code: invalidCode,
            section: "QTE",
            expected: "integer",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private static bool TryReadRequiredMashInputInteger(
        JsonElement config,
        string configContext,
        List<ValidationIssue> issues,
        string propName,
        string missingCode,
        string invalidCode,
        out int value)
    {
        value = 0;
        if (!config.TryGetProperty(propName, out var node))
        {
            issues.Add(new ValidationIssue(
                $"{configContext}.{propName}",
                IssueSeverity.Error,
                $"MashInput требует {propName}",
                code: missingCode,
                section: "QTE",
                expected: "integer",
                actual: "missing"));
            return false;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out value))
            return true;

        issues.Add(new ValidationIssue(
            $"{configContext}.{propName}",
            IssueSeverity.Error,
            $"MashInput {propName} должен быть целым числом",
            code: invalidCode,
            section: "QTE",
            expected: "integer",
            actual: node.ValueKind.ToString()));
        return false;
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
        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        if (!TryReadGuardianPowerJournalEntriesForCurrentSemanticProof(postJournalJson, out var rawPostEntries))
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют readable canonical current abode_power_journal proof.",
                code: "guardian_resonance_invalid_current_journal",
                section: "LifeEvaluation",
                expected: "readable current abode_power_journal with canonical resonance entries",
                actual: "current journal unreadable, malformed or semantically invalid",
                repairHint: "Для resonance materialize current abode_power_journal.json как canonical guardian power journal. Любой unreadable или semantically invalid current journal не считается proof surface."));
            return;
        }

        var semanticCurrentResonanceEntries = rawPostEntries
            .Where(entry => string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (semanticCurrentResonanceEntries.Count == 0)
            return;

        var currentJournalProof = ReadStrictGuardianPowerJournalEntriesForCurrentProof(postJournalJson, "resonance");
        if (currentJournalProof.Status == GuardianPowerJournalCurrentProofStatus.InvalidCurrentJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют readable canonical current abode_power_journal proof.",
                code: "guardian_resonance_invalid_current_journal",
                section: "LifeEvaluation",
                expected: "readable current abode_power_journal with canonical resonance entries",
                actual: currentJournalProof.FailureDescription ?? "current journal unreadable, malformed or semantically invalid",
                repairHint: "Для resonance materialize current abode_power_journal.json как canonical guardian power journal. Любой unreadable или semantically invalid current journal не считается proof surface."));
            return;
        }

        if (currentJournalProof.Status == GuardianPowerJournalCurrentProofStatus.InvalidCurrentGuardianAuthority)
        {
            var guardianPolicyContext = ResolveGuardianPolicyContextSync();
            if (!HasResolvedStrictPreTurnGuardianAuthority(guardianPolicyContext))
            {
                if (guardianPolicyContext.StrictPreTurnGuardianAuthorityStatus == StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotTracker ||
                    guardianPolicyContext.StrictPreTurnGuardianAuthorityStatus == StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotTracker)
                {
                    issues.Add(new ValidationIssue(
                        GuardianProjectState.TrackerPath,
                        IssueSeverity.Error,
                        "Новые resonance power events требуют canonical validated snapshot guardian project tracker baseline для pre-turn proof knowledge.",
                        code: "guardian_resonance_invalid_validated_snapshot_tracker",
                        section: "LifeEvaluation",
                        expected: $"canonical validated snapshot {GuardianProjectState.TrackerPath} for resonance proof knowledge",
                        actual: currentJournalProof.FailureDescription ?? DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext),
                        repairHint: $"Для resonance сохраняй в validated pending turn snapshot canonical {GuardianProjectState.TrackerPath}. Proof knowledge не может строиться из partial или invalid tracker snapshot."));
                    return;
                }

                if (guardianPolicyContext.StrictPreTurnGuardianAuthorityStatus == StrictPreTurnGuardianAuthorityStatus.MissingValidatedSnapshotJournal ||
                    guardianPolicyContext.StrictPreTurnGuardianAuthorityStatus == StrictPreTurnGuardianAuthorityStatus.InvalidValidatedSnapshotJournal)
                {
                    issues.Add(new ValidationIssue(
                        GuardianPowerEventState.JournalPath,
                        IssueSeverity.Error,
                        "Новые resonance power events требуют canonical validated snapshot abode_power_journal baseline для pre-turn proof knowledge.",
                        code: "guardian_resonance_invalid_validated_snapshot_journal",
                        section: "LifeEvaluation",
                        expected: $"canonical validated snapshot {GuardianPowerEventState.JournalPath} for resonance proof knowledge",
                        actual: currentJournalProof.FailureDescription ?? DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext),
                        repairHint: $"Для resonance сохраняй в validated pending turn snapshot canonical {GuardianPowerEventState.JournalPath}. Proof knowledge не может строиться из partial или invalid journal snapshot."));
                    return;
                }

                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json",
                    IssueSeverity.Error,
                    "Новые resonance power events требуют canonical validated snapshot guardians baseline для pre-turn proof knowledge.",
                    code: "guardian_resonance_invalid_validated_snapshot_guardians",
                    section: "LifeEvaluation",
                    expected: "canonical validated snapshot guardians.json for resonance proof knowledge",
                    actual: currentJournalProof.FailureDescription ?? DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext),
                    repairHint: "Для resonance сохраняй в validated pending turn snapshot canonical game_state/meta/guardians.json. Proof knowledge не может строиться из partial или invalid guardian snapshot."));
                return;
            }

            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Новые resonance power events требуют readable current guardian authority и не используют pre-turn guardian baseline как fallback authority source.",
                code: "guardian_resonance_invalid_current_guardian_authority",
                section: "LifeEvaluation",
                expected: "readable current guardian authority root",
                actual: currentJournalProof.FailureDescription ?? "current guardian authority unavailable",
                repairHint: "Исправь current game_state/meta/guardians.json и validated guardian baseline так, чтобы kernel построил strict current guardian authority до resonance proof."));
            return;
        }

        if (currentJournalProof.Status == GuardianPowerJournalCurrentProofStatus.InvalidCurrentTrackerAuthority)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют readable current guardian project tracker authority и не используют current journal как fallback authority source.",
                code: "guardian_resonance_invalid_current_tracker_authority",
                section: "LifeEvaluation",
                expected: $"readable current authority root for {GuardianProjectState.TrackerPath}",
                actual: currentJournalProof.FailureDescription ?? "current tracker authority unavailable",
                repairHint: $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил strict current tracker authority до resonance proof."));
            return;
        }

        if (currentJournalProof.Entries == null)
            return;

        var currentResonanceEntries = currentJournalProof.Entries
            .Where(entry =>
                string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currentResonanceCountsByLifeKey = currentResonanceEntries
            .GroupBy(entry => BuildGuardianResonanceLifeScopeKey(entry), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var currentLifeEntries in currentResonanceCountsByLifeKey.Values)
        {
            if (currentLifeEntries.Count <= 1)
                continue;

            var sampleEntry = currentLifeEntries[0];
            var guardianId = GetFirstNonEmptyString(sampleEntry, "guardianId") ?? string.Empty;
            var lifeId = GetGuardianPowerEventAuditStringValue(sampleEntry, "lifeId");
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "За одну завершённую жизнь допустим максимум один resonance power event на одного Хранителя",
                code: "guardian_resonance_duplicate_for_same_life",
                section: "LifeEvaluation",
                expected: "at most one resonance event per guardianId + lifeId per completed life",
                actual: $"{guardianId} / {lifeId}: {currentLifeEntries.Count} resonance events",
                repairHint: "Не дублируй resonance для одного и того же Хранителя в рамках одной оценки жизни."));
        }

        var snapshotContext = await ResolveGuardianValidatedSnapshotContextAsync();
        if (snapshotContext.SnapshotStatus != ValidatedPendingTurnSnapshotStatus.Usable || snapshotContext.Manifest == null)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют current validated pending turn snapshot life-evaluation context.",
                code: "guardian_resonance_invalid_validated_snapshot_context",
                section: "LifeEvaluation",
                expected: "current validated life-evaluation snapshot manifest",
                actual: DescribeValidatedPendingTurnSnapshotStatus(snapshotContext.SnapshotStatus),
                repairHint: "Не используй stale или отсутствующий pending turn snapshot для resonance. Life Evaluation turn должен сохранять current validated snapshot manifest и journal snapshot."));
            return;
        }

        var preJournalJson = await ReadValidatedPendingTurnSnapshotFileAsync(snapshotContext.Manifest, GuardianPowerEventState.JournalPath);
        var preTurnJournalKnowledgeResult = await ReadValidatedPreTurnGuardianPowerJournalProofKnowledgeAsync(
            CreateGuardianPowerEventProofScopeForReasonType("resonance"));
        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotGuardians)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Новые resonance power events требуют canonical validated snapshot guardians baseline для pre-turn proof knowledge.",
                code: "guardian_resonance_invalid_validated_snapshot_guardians",
                section: "LifeEvaluation",
                expected: "canonical validated snapshot guardians.json for resonance proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot guardians baseline invalid",
                repairHint: "Для resonance сохраняй в validated pending turn snapshot canonical game_state/meta/guardians.json. Proof knowledge не может строиться из partial или invalid guardian snapshot."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют canonical validated snapshot guardian project tracker baseline для pre-turn proof knowledge.",
                code: "guardian_resonance_invalid_validated_snapshot_tracker",
                section: "LifeEvaluation",
                expected: $"canonical validated snapshot {GuardianProjectState.TrackerPath} for resonance proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot tracker baseline invalid",
                repairHint: $"Для resonance сохраняй в validated pending turn snapshot canonical {GuardianProjectState.TrackerPath}. Proof knowledge не может строиться из partial или invalid tracker snapshot."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют canonical validated snapshot abode_power_journal baseline для pre-turn proof knowledge.",
                code: "guardian_resonance_invalid_validated_snapshot_journal",
                section: "LifeEvaluation",
                expected: $"canonical validated snapshot {GuardianPowerEventState.JournalPath} for resonance proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot journal baseline invalid",
                repairHint: $"Для resonance сохраняй в validated pending turn snapshot canonical {GuardianPowerEventState.JournalPath}. Proof knowledge не может строиться из missing, stale или invalid journal baseline."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Knowledge == null ||
            !TryReadStrictGuardianPowerJournalEntriesForValidatedBaselineProof(
                preJournalJson,
                preTurnJournalKnowledgeResult.Knowledge,
                "resonance",
                out var preEntries))
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "Новые resonance power events требуют readable validated pre-turn abode_power_journal baseline.",
                code: "guardian_resonance_invalid_validated_snapshot_journal",
                section: "LifeEvaluation",
                expected: "readable validated pre-turn abode_power_journal snapshot",
                actual: "validated snapshot journal missing, unreadable or malformed",
                repairHint: "Для resonance сохраняй в validated pending turn snapshot корректный abode_power_journal baseline; без читаемого baseline нельзя доказывать new resonance events."));
            return;
        }

        var postEntries = currentJournalProof.Entries;
        if (!TryValidateGuardianPowerJournalAppendOnlyIdentity(preEntries, postEntries, out var appendOnlyFailureDescription))
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "guardian_resonance требует append-only current abode_power_journal без переписывания pre-turn baseline entries",
                code: "guardian_resonance_invalid_current_journal",
                section: "LifeEvaluation",
                expected: "append-only current journal preserving validated pre-turn entries",
                actual: appendOnlyFailureDescription,
                repairHint: "Не переписывай pre-turn abode_power_journal entries и не переиспользуй их identity для новых resonance events."));
            return;
        }

        var knownEventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in preEntries)
        {
            var eventId = GetStringValue(entry, "eventId");
            if (!string.IsNullOrWhiteSpace(eventId))
                knownEventIds.Add(eventId);
        }

        var newResonanceEntries = currentResonanceEntries
            .Where(entry =>
            {
                var eventId = GetStringValue(entry, "eventId");
                return !string.IsNullOrWhiteSpace(eventId) &&
                       !knownEventIds.Contains(eventId);
            })
            .ToList();
        if (newResonanceEntries.Count == 0)
            return;

        if (!LifeEvaluationRewardAnalyzer.IsLifeEvaluationSourceLabel(snapshotContext.Manifest.SourceLabel))
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
    }

    private static string BuildGuardianResonanceLifeScopeKey(JsonElement entry)
        => TryBuildGuardianResonanceLifeScopeKey(entry, out var key) ? key : string.Empty;

    private static bool TryBuildGuardianResonanceLifeScopeKey(JsonElement entry, out string key)
    {
        key = string.Empty;
        var guardianId = GetFirstNonEmptyString(entry, "guardianId");
        var lifeId = GetGuardianPowerEventAuditStringValue(entry, "lifeId");
        if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(lifeId))
            return false;

        key = $"{guardianId}::{lifeId}";
        return true;
    }

    private static string GetGuardianPowerEventAuditStringValue(JsonElement entry, string propName)
    {
        if (entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("audit", out var audit) ||
            audit.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        return GetStringValue(audit, propName);
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
        ValidateOptionalString(item, itemContext, issues, "mechanicalSummaryAuthority");
        ValidateOptionalString(item, itemContext, issues, "mechanicalSummaryUnresolvedReason");
        ValidateInventoryMechanicalSummaryAuthority(item, itemContext, issues);
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
                case "mechanicalSummaryAuthority":
                case "mechanicalSummaryUnresolvedReason":
                    if (prop.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.Value.GetString()))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.{prop.Name}",
                            IssueSeverity.Error,
                            $"{prop.Name} должен быть непустой строкой",
                            code: "inventory_mechanical_summary_authority_invalid_string",
                            section: section,
                            repairHint: "Для narrative-only или unresolved item summary передай непустую player-facing строку в documented authority fields."));
                    }
                    break;
                case "disassembleTo":
                    if (prop.Value.ValueKind != JsonValueKind.Null)
                        ValidateItemDisassemblyArray(prop.Value, $"{itemContext}.disassembleTo", issues);
                    break;
                case "fateCards":
                case "ownerBondLevelCurrent":
                    break;
                case "effects":
                    ValidateInventorySummaryArray(prop.Value, $"{itemContext}.{prop.Name}", issues, section);
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

        ValidateInventoryMechanicalSummaryAuthority(item, itemContext, issues, section);
    }

    private void ValidateInventoryMechanicalSummaryAuthority(JsonElement item, string itemContext, List<ValidationIssue> issues, string section = "Inventory")
    {
        if (item.ValueKind != JsonValueKind.Object)
            return;

        var summaries = CollectInventorySummaryCandidates(item).ToList();
        if (summaries.Count == 0)
            return;

        ValidateInventoryMechanicalSummaryAuthorityValue(item, itemContext, issues, section);

        var explicitAuthority = GetFirstNonEmptyString(item, "mechanicalSummaryAuthority");
        if (IsInventoryNarrativeOnlySummaryAuthority(explicitAuthority))
            return;

        if (IsInventoryUnresolvedSummaryAuthority(explicitAuthority))
        {
            if (HasInventoryMechanicalSummaryUnresolvedReason(item))
                return;

            var itemIdentity = GetInventoryItemIssueIdentity(item);
            issues.Add(new ValidationIssue(
                $"{itemContext}.item:{itemIdentity}.mechanicalSummaryUnresolvedReason",
                IssueSeverity.Error,
                "Unresolved inventory mechanics summary должен иметь player-facing reason",
                code: "inventory_mechanical_summary_unresolved_missing_reason",
                section: section,
                expected: "mechanicalSummaryUnresolvedReason, unresolvedMechanicsReason, unidentifiedMechanicsReason, sealedReason, unreadableReason, or lockedReason",
                actual: GetInventoryItemIssueIdentity(item),
                repairHint: "Если механика предмета ещё неизвестна или запечатана, добавь player-facing причину вместо implied applied bonus."));
            return;
        }

        var hasAnyMeaningfulStructuredAuthority = HasAnyMeaningfulInventoryStructuredSummaryAuthority(item);
        foreach (var summary in summaries)
        {
            var itemIdentity = GetInventoryItemIssueIdentity(item);
            var summaryContext = $"{itemContext}.item:{itemIdentity}.{summary.PropertyName}[{summary.Index}]";
            if (LooksLikeMechanicalInventorySummary(summary.Text))
            {
                if (HasInventoryStructuredSummaryAuthorityForSummary(item, summary.Text))
                    continue;

                issues.Add(new ValidationIssue(
                    summaryContext,
                    IssueSeverity.Error,
                    "Inventory bonus/effect summary выглядит механическим, но не имеет matching structured authority",
                    code: "inventory_mechanical_summary_missing_structured_authority",
                    section: section,
                    expected: "matching structuredBonuses, combatEffect, customProperties, or mechanicalSummaryAuthority=Unresolved with player-facing reason",
                    actual: $"{GetInventoryItemIssueIdentity(item)}: «{summary.Text}»",
                    repairHint: "Не оставляй mechanical-looking bonuses/effects как единственный источник правды: добавь matching canonical structuredBonuses/combatEffect/customProperties или явно пометь механику unresolved с reason."));
            }
            else if (!hasAnyMeaningfulStructuredAuthority)
            {
                issues.Add(new ValidationIssue(
                    summaryContext,
                    IssueSeverity.Error,
                    "Narrative inventory bonus/effect summary должен быть явно classified as narrative-only",
                    code: "inventory_narrative_summary_missing_classification",
                    section: section,
                    expected: "mechanicalSummaryAuthority = NarrativeOnly, or structured authority if the summary has mechanics",
                    actual: $"{GetInventoryItemIssueIdentity(item)}: «{summary.Text}»",
                    repairHint: "Если строка является только flavor/lore, добавь mechanicalSummaryAuthority=\"NarrativeOnly\". Если она влияет на механику, добавь structured authority."));
            }
        }
    }

    private void ValidateInventoryMechanicalSummaryAuthorityValue(JsonElement item, string itemContext, List<ValidationIssue> issues, string section)
    {
        if (!item.TryGetProperty("mechanicalSummaryAuthority", out var authority))
            return;

        if (authority.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(authority.GetString()))
            return;

        var value = authority.GetString();
        if (IsInventoryNarrativeOnlySummaryAuthority(value) ||
            IsInventoryUnresolvedSummaryAuthority(value))
            return;

        issues.Add(new ValidationIssue(
            $"{itemContext}.mechanicalSummaryAuthority",
            IssueSeverity.Error,
            "mechanicalSummaryAuthority использует unsupported value",
            code: "inventory_mechanical_summary_authority_invalid_value",
            section: section,
            expected: "NarrativeOnly | FlavorOnly | Unresolved | Unknown | Unidentified | Sealed",
            actual: value,
            repairHint: "Используй NarrativeOnly для flavor/lore text или Unresolved/Unknown/Unidentified/Sealed вместе с player-facing reason."));
    }

    private static IEnumerable<InventorySummaryCandidate> CollectInventorySummaryCandidates(JsonElement item)
    {
        foreach (var summary in CollectInventorySummaryCandidates(item, "bonuses", includeObjectSummaries: false))
            yield return summary;

        foreach (var summary in CollectInventorySummaryCandidates(item, "effects", includeObjectSummaries: true))
            yield return summary;
    }

    private static IEnumerable<InventorySummaryCandidate> CollectInventorySummaryCandidates(
        JsonElement item,
        string propertyName,
        bool includeObjectSummaries)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            yield break;

        var index = 0;
        foreach (var entry in value.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var text = entry.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    yield return new InventorySummaryCandidate(propertyName, index, text.Trim());
            }
            else if (includeObjectSummaries && entry.ValueKind == JsonValueKind.Object)
            {
                var text = GetFirstNonEmptyString(entry, "effectDescription", "description", "name", "effect");
                if (!string.IsNullOrWhiteSpace(text))
                    yield return new InventorySummaryCandidate(propertyName, index, text.Trim());
            }

            index++;
        }
    }

    private void ValidateInventorySummaryArray(JsonElement value, string context, List<ValidationIssue> issues, string section)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Inventory effects должен быть массивом строк или summary objects",
                code: "inventory_effects_invalid_array",
                section: section,
                repairHint: "Передай effects как массив user-facing strings или objects с name/effect/description/effectDescription."));
            return;
        }

        var index = 0;
        foreach (var entry in value.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                if (string.IsNullOrWhiteSpace(entry.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}[{index}]",
                        IssueSeverity.Error,
                        "Inventory effects string должен быть непустым",
                        code: "inventory_effects_empty_string",
                        section: section));
                }
            }
            else if (entry.ValueKind == JsonValueKind.Object)
            {
                if (!HasAnyNonEmptyString(entry, "effectDescription", "description", "name", "effect"))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}[{index}]",
                        IssueSeverity.Error,
                        "Inventory effects object должен иметь user-facing summary",
                        code: "inventory_effects_object_missing_summary",
                        section: section,
                        expected: "effectDescription, description, name, or effect",
                        repairHint: "Добавь player-facing summary text к effects object."));
                }
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Inventory effects element должен быть строкой или объектом",
                    code: "inventory_effects_invalid_entry",
                    section: section));
            }

            index++;
        }
    }

    private static bool HasAnyMeaningfulInventoryStructuredSummaryAuthority(JsonElement item)
    {
        return EnumerateInventoryStructuredSummaryAuthorityObjects(item)
            .Any(HasMeaningfulInventoryStructuredSummaryAuthorityObject);
    }

    private static bool HasInventoryStructuredSummaryAuthorityForSummary(JsonElement item, string summary)
    {
        return EnumerateInventoryStructuredSummaryAuthorityObjects(item)
            .Any(authority => InventoryStructuredSummaryAuthorityMatches(summary, authority));
    }

    private static IEnumerable<InventoryAuthorityUnit> EnumerateInventoryStructuredSummaryAuthorityObjects(JsonElement item)
    {
        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses) &&
            structuredBonuses.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in structuredBonuses.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return new InventoryAuthorityUnit(entry, AllowImplicitValueType: false);
            }
        }

        if (item.TryGetProperty("customProperties", out var customProperties) &&
            customProperties.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in customProperties.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return new InventoryAuthorityUnit(entry, AllowImplicitValueType: false);
            }
        }

        if (!item.TryGetProperty("combatEffect", out var combatEffect))
            yield break;

        if (combatEffect.ValueKind == JsonValueKind.Object)
        {
            yield return new InventoryAuthorityUnit(combatEffect, AllowImplicitValueType: true);
        }
        else if (combatEffect.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in combatEffect.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return new InventoryAuthorityUnit(entry, AllowImplicitValueType: true);
            }
        }
    }

    private static bool InventoryStructuredSummaryAuthorityMatches(string summary, InventoryAuthorityUnit authority)
    {
        return EnumerateInventoryAuthorityUnitObjects(authority.Element, authority.AllowImplicitValueType)
            .Any(unit => InventoryAuthorityMetadataMatchesSummary(summary, unit));
    }

    private static bool HasMeaningfulInventoryStructuredSummaryAuthorityObject(InventoryAuthorityUnit authority)
    {
        return EnumerateInventoryAuthorityUnitObjects(authority.Element, authority.AllowImplicitValueType)
            .Any(HasMeaningfulLocalInventoryStructuredSummaryAuthorityObject);
    }

    private static IEnumerable<InventoryAuthorityUnit> EnumerateInventoryAuthorityUnitObjects(JsonElement root, bool allowImplicitValueType)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return new InventoryAuthorityUnit(root, allowImplicitValueType);
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object ||
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nested in EnumerateInventoryAuthorityUnitObjects(property.Value, allowImplicitValueType))
                        yield return nested;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object ||
                    entry.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nested in EnumerateInventoryAuthorityUnitObjects(entry, allowImplicitValueType))
                        yield return nested;
                }
            }
        }
    }

    private static bool HasMeaningfulLocalInventoryStructuredSummaryAuthorityObject(InventoryAuthorityUnit authority)
    {
        return EnumerateLocalInventoryAuthorityTargetTexts(authority.Element).Any() &&
               (EnumerateLocalInventoryAuthorityValueCandidates(authority.Element, authority.AllowImplicitValueType).Any() ||
                HasLocalInventoryNonNumericAuthorityValue(authority.Element));
    }

    private static bool InventoryAuthorityMetadataMatchesSummary(string summary, InventoryAuthorityUnit authority)
    {
        if (!HasMeaningfulLocalInventoryStructuredSummaryAuthorityObject(authority))
            return false;

        var hasTargetMatch = EnumerateLocalInventoryAuthorityTargetTexts(authority.Element)
            .Any(target => InventoryAuthorityTargetMatchesSummary(summary, target));
        if (!hasTargetMatch)
            return false;

        if (!TryExtractInventorySummaryValue(summary, out var summaryValue, out var summaryIsPercent))
            return true;

        return EnumerateLocalInventoryAuthorityValueCandidates(authority.Element, authority.AllowImplicitValueType)
            .Any(candidate => InventoryAuthorityValueMatchesSummary(summaryValue, summaryIsPercent, candidate));
    }

    private static bool InventoryAuthorityTargetMatchesSummary(string summary, string target)
    {
        var normalizedSummary = NormalizeInventoryAuthorityText(summary);
        var normalizedTarget = NormalizeInventoryAuthorityText(target);
        if (normalizedSummary.Length == 0 || normalizedTarget.Length == 0)
            return false;

        if (normalizedTarget.Length >= 3 && normalizedSummary.Contains(normalizedTarget, StringComparison.Ordinal))
            return true;

        foreach (var aliases in InventoryMechanicalAuthorityTargetAliases)
        {
            var summaryMatchesAlias = aliases
                .Select(NormalizeInventoryAuthorityText)
                .Any(alias => alias.Length >= 3 && normalizedSummary.Contains(alias, StringComparison.Ordinal));
            if (!summaryMatchesAlias)
                continue;

            var targetMatchesAlias = aliases
                .Select(NormalizeInventoryAuthorityText)
                .Any(alias => alias.Length >= 3 && normalizedTarget.Contains(alias, StringComparison.Ordinal));
            if (targetMatchesAlias)
                return true;
        }

        return false;
    }

    private static bool TryExtractInventorySummaryValue(string summary, out decimal value, out bool isPercent)
    {
        foreach (Match match in InventoryMechanicalSummaryValueRegex.Matches(summary))
        {
            var numberText = match.Groups["number"].Value.Replace(',', '.');
            if (!decimal.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                continue;

            if (match.Groups["sign"].Value == "-")
                value = -value;

            isPercent = match.Groups["percent"].Value == "%";
            return true;
        }

        value = 0;
        isPercent = false;
        return false;
    }

    private static bool InventoryAuthorityValueMatchesSummary(
        decimal summaryValue,
        bool summaryIsPercent,
        InventoryAuthorityValueCandidate candidate)
    {
        if (Math.Abs(candidate.Value - summaryValue) > 0.0001m)
            return false;

        return summaryIsPercent == candidate.IsPercent;
    }

    private static IEnumerable<string> EnumerateLocalInventoryAuthorityTargetTexts(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String &&
                IsInventoryAuthorityTargetProperty(property.Name) &&
                !string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                yield return property.Value.GetString()!;
            }
        }
    }

    private static bool IsInventoryAuthorityTargetProperty(string propertyName)
    {
        return StringEqualsAny(
            propertyName,
            "target",
            "targetType",
            "targetTypeDisplayName",
            "targetStateName",
            "stat",
            "characteristic",
            "skill",
            "attribute",
            "resource",
            "resourceType",
            "effectType");
    }

    private static IEnumerable<InventoryAuthorityValueCandidate> EnumerateLocalInventoryAuthorityValueCandidates(
        JsonElement root,
        bool allowImplicitValueType)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (!TryGetInventoryAuthorityValueType(root, allowImplicitValueType, out var localValueTypeIsPercent))
            yield break;

        foreach (var property in root.EnumerateObject())
        {
            if (IsInventoryAuthorityValueProperty(property.Name) &&
                TryReadInventoryAuthorityValue(property.Value, localValueTypeIsPercent, out var candidate))
            {
                yield return candidate;
            }
        }
    }

    private static bool IsInventoryAuthorityValueProperty(string propertyName)
    {
        return StringEqualsAny(
            propertyName,
            "value",
            "changeValue",
            "bonus",
            "modifier",
            "amount",
            "percent",
            "percentage");
    }

    private static bool TryReadInventoryAuthorityValue(
        JsonElement valueElement,
        bool? localValueTypeIsPercent,
        out InventoryAuthorityValueCandidate candidate)
    {
        switch (valueElement.ValueKind)
        {
            case JsonValueKind.Number:
                if (localValueTypeIsPercent.HasValue &&
                    valueElement.TryGetDecimal(out var numericValue))
                {
                    candidate = new InventoryAuthorityValueCandidate(numericValue, localValueTypeIsPercent.Value);
                    return true;
                }
                break;

            case JsonValueKind.String:
                var valueText = valueElement.GetString() ?? string.Empty;
                foreach (Match match in InventoryMechanicalSummaryValueRegex.Matches(valueText))
                {
                    var numberText = match.Groups["number"].Value.Replace(',', '.');
                    if (!decimal.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out var stringValue))
                        continue;

                    if (match.Groups["sign"].Value == "-")
                        stringValue = -stringValue;

                    var stringValueIsPercent = match.Groups["percent"].Value == "%";
                    if (localValueTypeIsPercent.HasValue)
                    {
                        if (stringValueIsPercent && !localValueTypeIsPercent.Value)
                            continue;

                        candidate = new InventoryAuthorityValueCandidate(stringValue, localValueTypeIsPercent.Value);
                        return true;
                    }

                    if (stringValueIsPercent)
                    {
                        candidate = new InventoryAuthorityValueCandidate(stringValue, true);
                        return true;
                    }
                }
                break;
        }

        candidate = default;
        return false;
    }

    private static bool TryGetInventoryAuthorityValueType(
        JsonElement root,
        bool allowImplicitValueType,
        out bool? isPercent)
    {
        isPercent = null;
        var valueType = GetFirstNonEmptyString(root, "valueType", "modifierType");
        if (string.IsNullOrWhiteSpace(valueType))
            return allowImplicitValueType && HasAnyNonEmptyString(root, "effectType", "targetType", "targetTypeDisplayName");

        if (StringEqualsAny(valueType, "Percentage", "Percent", "%", "Процент", "Процентный"))
        {
            isPercent = true;
            return true;
        }

        if (StringEqualsAny(valueType, "Flat", "Fixed", "Фикс", "Фиксированный"))
        {
            isPercent = false;
            return true;
        }

        return false;
    }

    private static bool HasLocalInventoryNonNumericAuthorityValue(JsonElement root)
    {
        if (!HasInventoryAuthorityNonNumericValueType(root))
            return false;

        foreach (var property in root.EnumerateObject())
        {
            if (IsInventoryAuthorityValueProperty(property.Name) &&
                IsMeaningfulNonNumericInventoryAuthorityValue(property.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasInventoryAuthorityNonNumericValueType(JsonElement root)
    {
        var valueType = GetFirstNonEmptyString(root, "valueType", "modifierType");
        return StringEqualsAny(
            valueType,
            "String",
            "Text",
            "Boolean",
            "Bool",
            "Строка",
            "Текст",
            "Булевый",
            "Логический");
    }

    private static bool IsMeaningfulNonNumericInventoryAuthorityValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.True or JsonValueKind.False => true,
            _ => false
        };
    }

    private static string NormalizeInventoryAuthorityText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private readonly record struct InventoryAuthorityValueCandidate(decimal Value, bool IsPercent);

    private readonly record struct InventoryAuthorityUnit(JsonElement Element, bool AllowImplicitValueType);

    private static bool HasInventoryMechanicalSummaryUnresolvedReason(JsonElement item)
    {
        return HasAnyNonEmptyString(
            item,
            "mechanicalSummaryUnresolvedReason",
            "unresolvedMechanicsReason",
            "unidentifiedMechanicsReason",
            "unknownMechanicsReason",
            "sealedReason",
            "unreadableReason",
            "lockedReason");
    }

    private static bool IsInventoryNarrativeOnlySummaryAuthority(string? value)
    {
        return StringEqualsAny(value, "NarrativeOnly", "FlavorOnly", "Narrative", "Flavor", "narrative-only", "flavor-only");
    }

    private static bool IsInventoryUnresolvedSummaryAuthority(string? value)
    {
        return StringEqualsAny(value, "Unresolved", "Unknown", "Unidentified", "Sealed");
    }

    private static bool LooksLikeMechanicalInventorySummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (InventoryMechanicalSummaryNumericRegex.IsMatch(text))
            return true;

        var normalized = text.ToLowerInvariant();
        return InventoryMechanicalSummaryTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static bool StringEqualsAny(string? value, params string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return candidates.Any(candidate => string.Equals(value.Trim(), candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetInventoryItemIssueIdentity(JsonElement item)
    {
        return GetFirstNonEmptyString(item, "itemId", "existedId", "id", "name") ?? "unknown inventory item";
    }

    private readonly record struct InventorySummaryCandidate(string PropertyName, int Index, string Text);

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

