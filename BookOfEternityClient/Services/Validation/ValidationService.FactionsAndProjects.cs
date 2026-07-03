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
    private void ValidateFactionControlArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} обязателен и должен быть массивом или null",
                code: "location_missing_faction_control_array",
                section: "Location",
                expected: $"{propName} array or null",
                actual: "missing property",
                repairHint: $"Добавь в location object поле {propName}. Используй null, если контролирующих фракций нет, или массив canonical factionControl entries."));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом или null",
                code: "location_invalid_faction_control_array_shape",
                section: "Location",
                expected: $"{propName} array or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Передавай {propName} как null или массив factionControl entries, а не как {value.ValueKind}."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;
            RequireString(item, itemContext, issues, "factionId");
            RequireString(item, itemContext, issues, "factionName");
            var controlType = RequireString(item, itemContext, issues, "controlType");
            if (!string.IsNullOrWhiteSpace(controlType) && !AllowedLocationControlTypes.Contains(controlType))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.controlType",
                    IssueSeverity.Error,
                    "factionControl.controlType должен быть одним из canonical enum значений",
                    code: "location_faction_control_invalid_type",
                    section: "Location",
                    expected: string.Join(" | ", AllowedLocationControlTypes),
                    actual: controlType,
                    repairHint: "Используй в factionControl.controlType только Military, Economic, Social или Covert по Block 20.2.A."));
            }

            ValidateIntegerField(item, itemContext, issues, "controlLevel");
            if (TryReadInt(item, "controlLevel", out var controlLevel) &&
                (controlLevel < 0 || controlLevel > 100))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.controlLevel",
                    IssueSeverity.Error,
                    "factionControl.controlLevel должен быть в диапазоне 0..100",
                    code: "location_faction_control_level_out_of_bounds",
                    section: "Location",
                    expected: "0..100",
                    actual: controlLevel.ToString(),
                    repairHint: "Сохраняй factionControl.controlLevel как integer от 0 до 100 по Block 20.2.A."));
            }
        }
    }


    private static bool IsLikelyFullFactionObject(JsonElement item)
    {
        return item.TryGetProperty("powerProfile", out _) ||
               item.TryGetProperty("resources", out _) ||
               item.TryGetProperty("ranks", out _) ||
               item.TryGetProperty("relations", out _) ||
               item.TryGetProperty("isPlayerFaction", out _) ||
               item.TryGetProperty("isPlayerMember", out _);
    }


    private void ValidateFullFactionObject(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string>? knownPermanentFactionIds, HashSet<string>? knownCanonicalFactionNames,
        HashSet<string>? sameTurnFactionInitialIds)
    {
        var factionId = GetFirstNonEmptyString(item, "factionId");
        var initialId = GetFirstNonEmptyString(item, "initialId");
        var factionName = GetFirstNonEmptyString(item, "name", "factionName");
        var hasFactionId = !string.IsNullOrWhiteSpace(factionId);
        var hasInitialId = !string.IsNullOrWhiteSpace(initialId);

        if (!hasFactionId && !hasInitialId)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.factionId",
                IssueSeverity.Error,
                "Faction Data Object должен содержать permanent factionId для existing faction или initialId для новой same-turn faction",
                code: "faction_full_object_missing_identity",
                section: "Factions",
                repairHint: "Для existing faction передай factionId. Для новой same-turn faction используй factionId=null и непустой initialId."));
        }

        if (hasFactionId && hasInitialId)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.initialId",
                IssueSeverity.Error,
                "initialId допустим только для новой same-turn faction и не должен присутствовать у existing faction object",
                code: "faction_full_object_conflicting_identity",
                section: "Factions",
                repairHint: "У existing faction оставь только permanent factionId. initialId используй только при создании новой фракции в том же ходу."));
        }

        if (!hasFactionId && hasInitialId)
        {
            if (!TempFactionInitialIdRegex.IsMatch(initialId!))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.initialId",
                    IssueSeverity.Error,
                    "initialId новой фракции должен использовать canonical temp-faction-* формат",
                    code: "faction_full_object_initial_id_invalid_format",
                    section: "Factions",
                    expected: "temp-faction-[description]",
                    actual: initialId,
                    repairHint: "Для genuinely new same-turn faction используй initialId в формате temp-faction-[short-description]."));
            }

            if (!item.TryGetProperty("factionId", out var explicitFactionIdNode) ||
                explicitFactionIdNode.ValueKind != JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.factionId",
                    IssueSeverity.Error,
                    "Новая same-turn faction в factionDataChanges должна явно передавать factionId = null вместе с initialId",
                    code: "faction_full_object_requires_explicit_null_faction_id",
                    section: "Factions",
                    expected: "factionId = null for genuinely new same-turn faction",
                    actual: item.TryGetProperty("factionId", out var actualFactionIdNode)
                        ? actualFactionIdNode.ValueKind.ToString()
                        : "missing",
                    repairHint: "Для новой same-turn faction передай factionId: null и непустой initialId. Для existing faction используй permanent factionId."));
            }

            if (!item.TryGetProperty("isNewFaction", out var isNewFactionNode) ||
                isNewFactionNode.ValueKind != JsonValueKind.True)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.isNewFaction",
                    IssueSeverity.Error,
                    "Новая same-turn faction в factionDataChanges должна явно помечаться isNewFaction = true",
                    code: "faction_full_object_requires_explicit_create_flag",
                    section: "Factions",
                    expected: "isNewFaction = true for genuinely new same-turn faction",
                    actual: item.TryGetProperty("isNewFaction", out var actualCreateNode)
                        ? actualCreateNode.ValueKind.ToString()
                        : "missing",
                    repairHint: "Для действительно новой фракции передай isNewFaction: true. Existing faction всегда обновляй через permanent factionId без initialId."));
            }

            if (!HasAnyNonEmptyString(item, "image_prompt"))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.image_prompt",
                    IssueSeverity.Error,
                    "Новая same-turn faction в factionDataChanges должна содержать image_prompt",
                    code: "faction_full_object_new_requires_image_prompt",
                    section: "Factions",
                    expected: "non-empty image_prompt for newly created faction",
                    actual: "missing",
                    repairHint: "Для новой фракции передай английский image_prompt, описывающий символ, знамя или штаб-квартиру фракции."));
            }
            else
            {
                var imagePrompt = GetFirstNonEmptyString(item, "image_prompt") ?? string.Empty;
                if (!LooksLikeEnglishImagePrompt(imagePrompt))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.image_prompt",
                        IssueSeverity.Error,
                        "image_prompt новой фракции должен быть English-only и не длиннее 150 символов",
                        code: "faction_full_object_image_prompt_invalid",
                        section: "Factions",
                        expected: "English prompt, <= 150 chars",
                        actual: imagePrompt.Length > 150 ? $">150 chars ({imagePrompt.Length})" : imagePrompt,
                        repairHint: "Используй для новой фракции краткий English-only image_prompt без кириллицы и длиннее 150 символов."));
                }
            }
        }

        if (hasFactionId &&
            knownPermanentFactionIds != null &&
            !knownPermanentFactionIds.Contains(factionId!))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.factionId",
                IssueSeverity.Error,
                $"Full faction object ссылается на неизвестный factionId '{factionId}'",
                code: "faction_full_object_unknown_faction_id",
                section: "Factions",
                expected: "existing permanent factionId from faction_core.json",
                actual: factionId,
                repairHint: "Для existing faction используй существующий factionId. Новую same-turn faction создавай через factionId=null и непустой initialId."));
        }

        if (!hasFactionId &&
            hasInitialId &&
            knownPermanentFactionIds != null &&
            knownPermanentFactionIds.Contains(initialId!))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.initialId",
                IssueSeverity.Error,
                $"initialId '{initialId}' конфликтует с уже существующим permanent factionId",
                code: "faction_full_object_initial_id_conflicts_with_existing_faction_id",
                section: "Factions",
                expected: "new temporary initialId that does not collide with existing permanent factionId",
                actual: initialId,
                repairHint: "Для existing faction используй permanent factionId. Для genuinely new same-turn faction выбери новый temporary initialId, который не совпадает с существующими factionId."));
        }

        if (!hasFactionId &&
            hasInitialId &&
            knownCanonicalFactionNames != null &&
            (sameTurnFactionInitialIds == null || !sameTurnFactionInitialIds.Contains(initialId!)) &&
            !string.IsNullOrWhiteSpace(factionName) &&
            knownCanonicalFactionNames.Contains(factionName))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.initialId",
                IssueSeverity.Error,
                $"Full faction object использует initialId для уже существующей фракции '{factionName}'",
                code: "faction_full_object_existing_requires_faction_id",
                section: "Factions",
                expected: "permanent factionId for existing faction",
                actual: $"initialId={initialId}",
                repairHint: "Если фракция уже существует в canonical faction_core.json, передай её permanent factionId. initialId оставляй только для genuinely new same-turn faction."));
        }

        RequireString(item, itemContext, issues, "name");
        RequireString(item, itemContext, issues, "description");
        ValidateOptionalString(item, itemContext, issues, "factionColor");
        RequireObjectProperty(item, itemContext, issues, "powerProfile");
        RequireObjectProperty(item, itemContext, issues, "resources");
        RequireObjectProperty(item, itemContext, issues, "ranks");
        RequireBooleanField(item, itemContext, issues, "isPlayerFaction");
        RequireBooleanField(item, itemContext, issues, "isPlayerMember");
        RequireNumberOrString(item, itemContext, issues, "level");
        RequireNumberOrString(item, itemContext, issues, "experience");
        RequireNumberOrString(item, itemContext, issues, "experienceForNextLevel");
        RequireString(item, itemContext, issues, "developmentArchetype");
        RequireNumberOrString(item, itemContext, issues, "reputation");
        ValidateOptionalString(item, itemContext, issues, "playerRank");
        ValidateOptionalString(item, itemContext, issues, "playerBranch");
        ValidateOptionalString(item, itemContext, issues, "playerStrategyDirective");
        ValidateOptionalString(item, itemContext, issues, "reputationDescription");
        ValidateOptionalString(item, itemContext, issues, "image_prompt");

        if (item.TryGetProperty("powerProfile", out var powerProfile) &&
            RequireObject(powerProfile, $"{itemContext}.powerProfile", issues))
        {
            foreach (var scale in new[] { "military", "economic", "social", "covert", "logistics", "stability", "arcane_tech", "exploration" })
            {
                if (!powerProfile.TryGetProperty(scale, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.powerProfile.{scale}",
                        IssueSeverity.Error,
                        "powerProfile scale обязателен в полном faction object",
                        code: "faction_full_object_missing_power_profile_scale",
                        section: "Factions",
                        expected: $"powerProfile.{scale}",
                        actual: "missing property",
                        repairHint: $"Для полного faction object добавь integer/number поле powerProfile.{scale}."));
                }
                else
                {
                    ValidateNonNegativeNumberField(powerProfile, $"{itemContext}.powerProfile", issues, scale);
                }
            }
        }

        if (item.TryGetProperty("resources", out var resources) &&
            RequireObject(resources, $"{itemContext}.resources", issues))
        {
            if (!resources.TryGetProperty("metaResources", out var metaResources))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.resources.metaResources",
                    IssueSeverity.Error,
                    "resources.metaResources обязателен в полном faction-core object",
                    code: "faction_full_object_missing_meta_resources",
                    section: "Factions",
                    expected: "resources.metaResources array",
                    actual: "missing property",
                    repairHint: "Для полного faction-core object добавь resources.metaResources как canonical array meta resource entries."));
            }
            else
            {
                ValidateFactionSidecarResourceArray(metaResources, $"{itemContext}.resources.metaResources", issues, requireUpkeep: true);
            }

            if (!resources.TryGetProperty("strategicGoods", out var strategicGoods))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.resources.strategicGoods",
                    IssueSeverity.Error,
                    "resources.strategicGoods обязателен в полном faction-core object",
                    code: "faction_full_object_missing_strategic_goods",
                    section: "Factions",
                    expected: "resources.strategicGoods array",
                    actual: "missing property",
                    repairHint: "Для полного faction-core object добавь resources.strategicGoods как canonical array strategic goods entries."));
            }
            else
            {
                ValidateFactionSidecarResourceArray(strategicGoods, $"{itemContext}.resources.strategicGoods", issues, requireUpkeep: false);
            }
        }

        var rankNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (item.TryGetProperty("ranks", out var ranks) &&
            RequireObject(ranks, $"{itemContext}.ranks", issues))
        {
            if (!TryGetArray(ranks, "branches", $"{itemContext}.ranks.branches", issues, out var branches))
                return;

            var branchIndex = 0;
            foreach (var branch in branches.EnumerateArray())
            {
                var branchContext = $"{itemContext}.ranks.branches[{branchIndex++}]";
                if (!RequireObject(branch, branchContext, issues))
                    continue;

                RequireString(branch, branchContext, issues, "branchId");
                RequireString(branch, branchContext, issues, "displayName");
                RequireBooleanField(branch, branchContext, issues, "isCoreBranch");
                if (!TryGetArray(branch, "ranks", $"{branchContext}.ranks", issues, out var branchRanks))
                    continue;

                var rankIndex = 0;
                foreach (var rank in branchRanks.EnumerateArray())
                {
                    var rankContext = $"{branchContext}.ranks[{rankIndex++}]";
                    if (!RequireObject(rank, rankContext, issues))
                        continue;

                    var rankNameMale = RequireString(rank, rankContext, issues, "rankNameMale");
                    var rankNameFemale = RequireString(rank, rankContext, issues, "rankNameFemale");
                    RequireNumberOrString(rank, rankContext, issues, "requiredReputation");
                    RequireString(rank, rankContext, issues, "unlockCondition");
                    RequireBooleanField(rank, rankContext, issues, "isJunctionPoint");
                    if (rank.TryGetProperty("availableBranches", out var availableBranches))
                        RequireArrayOfStrings(availableBranches, $"{rankContext}.availableBranches", issues);
                    if (rank.TryGetProperty("benefits", out var benefits) && benefits.ValueKind != JsonValueKind.String)
                        RequireArrayOfStrings(benefits, $"{rankContext}.benefits", issues);

                    if (!string.IsNullOrWhiteSpace(rankNameMale))
                        rankNames.Add(rankNameMale);
                    if (!string.IsNullOrWhiteSpace(rankNameFemale))
                        rankNames.Add(rankNameFemale);
                }
            }
        }

        var playerRank = GetFirstNonEmptyString(item, "playerRank");
        if (!string.IsNullOrWhiteSpace(playerRank) && rankNames.Count > 0 && !rankNames.Contains(playerRank))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerRank",
                IssueSeverity.Error,
                "playerRank должен совпадать с одним из rankNameMale/rankNameFemale в ranks.branches[].ranks[]"));
        }

        if (item.TryGetProperty("relations", out var relations))
        {
            if (relations.ValueKind == JsonValueKind.Array)
            {
                var relationIndex = 0;
                foreach (var relation in relations.EnumerateArray())
                {
                    var relationContext = $"{itemContext}.relations[{relationIndex++}]";
                    if (!RequireObject(relation, relationContext, issues))
                        continue;
                    RequireString(relation, relationContext, issues, "targetFactionId");
                    RequireString(relation, relationContext, issues, "status");
                    RequireString(relation, relationContext, issues, "description");
                }
            }
            else if (relations.ValueKind != JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.relations",
                    IssueSeverity.Error,
                    "relations должен быть массивом или null",
                    code: "faction_relations_invalid_array_shape",
                    section: "Factions",
                    expected: "relations array or null",
                    actual: relations.ValueKind.ToString(),
                    repairHint: "Передавай relations как null или массив canonical faction relation objects."));
            }
        }

        ValidateFactionExtendedFields(item, itemContext, issues);
        if (TryDescribeIncompleteFullFactionExtensionArray(item, out var extensionField, out var extensionActual))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.{extensionField}",
                IssueSeverity.Error,
                extensionField.Equals("customStates", StringComparison.OrdinalIgnoreCase)
                    ? "Full faction-core object must not use skeletal customStates"
                    : "Full faction-core object must not use partial optional extension data",
                code: extensionField.Equals("customStates", StringComparison.OrdinalIgnoreCase)
                    ? "faction_full_object_skeletal_custom_states"
                    : "faction_full_object_partial_optional_extension_data",
                section: "Factions",
                expected: "Canonical complete objects inside optional extension arrays of the full faction object",
                actual: extensionActual,
                repairHint: "Если полный faction-core object включает optional arrays вроде customStates/relations/projects/territories/structuredBonuses, каждый entry уже должен быть canonical complete data, а не skeletal delta."));

            issues.Add(new ValidationIssue(
                $"{itemContext}.{extensionField}",
                IssueSeverity.Error,
                itemContext.Contains(".factionDataChanges[", StringComparison.OrdinalIgnoreCase)
                    ? "Optional extension arrays inside factionDataChanges must already be canonical complete data"
                    : "Optional extension arrays inside full faction objects must already be canonical complete data",
                code: "faction_full_object_incomplete_optional_extension_array",
                section: "Factions",
                expected: "Canonical complete data in optional extension arrays",
                actual: extensionActual,
                repairHint: "Для full-object authoring не отправляй skeletal fragments в optional arrays. Передавай полностью заполненные canonical entries или используй отдельные sidecar/command surfaces для частичных изменений."));
        }
        if (item.TryGetProperty("customStates", out var customStates) &&
            customStates.ValueKind != JsonValueKind.Null)
        {
            ValidateArrayItems(customStates, $"{itemContext}.customStates", issues, ValidateCanonicalFactionCustomStateObject);
        }
        ValidateFactionProjectArray(item, itemContext, issues, "activeProjects", completed: false);
        ValidateFactionProjectArray(item, itemContext, issues, "completedProjects", completed: true);
    }


    private void ValidateFactionProjectArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName, bool completed)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;
        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом или null",
                code: "faction_project_array_invalid_shape",
                section: "Factions",
                expected: $"{propName} array or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Передавай {propName} как null или массив canonical faction project objects."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;
            ValidateFactionFullProjectObject(item, itemContext, issues, completed);
        }
    }


    private static bool TryDescribeIncompleteFullFactionExtensionArray(JsonElement item, out string fieldName, out string actual)
    {
        if (item.TryGetProperty("customStates", out var customStates) &&
            ArrayContainsIncompleteObjects(customStates, IsIncompleteFactionCustomState))
        {
            fieldName = "customStates";
            actual = "customStates contains skeletal entries without canonical currentValue/minValue/maxValue/description/progressionRule/thresholds";
            return true;
        }

        if (item.TryGetProperty("relations", out var relations) &&
            ArrayContainsIncompleteObjects(relations, relation =>
                !HasRequiredNonEmptyStrings(relation, "targetFactionId", "status", "description")))
        {
            fieldName = "relations";
            actual = "relations contains partial entries without targetFactionId/status/description";
            return true;
        }

        if (item.TryGetProperty("controlledTerritories", out var territories) &&
            ArrayContainsIncompleteObjects(territories, territory =>
                !HasRequiredNonEmptyStrings(territory, "locationId", "locationName")))
        {
            fieldName = "controlledTerritories";
            actual = "controlledTerritories contains partial entries without locationId/locationName";
            return true;
        }

        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses) &&
            ArrayContainsIncompleteObjects(structuredBonuses, bonus =>
                !HasRequiredNonEmptyStrings(bonus, "description", "bonusType", "target", "valueType", "application") ||
                !HasRequiredProperties(bonus, "value")))
        {
            fieldName = "structuredBonuses";
            actual = "structuredBonuses contains partial bonus objects";
            return true;
        }

        if (item.TryGetProperty("activeProjects", out var activeProjects) &&
            ArrayContainsIncompleteObjects(activeProjects, project =>
                !HasRequiredNonEmptyStrings(project, "projectId", "projectName", "activeState")))
        {
            fieldName = "activeProjects";
            actual = "activeProjects contains partial project entries";
            return true;
        }

        if (item.TryGetProperty("completedProjects", out var completedProjects) &&
            ArrayContainsIncompleteObjects(completedProjects, project =>
                !HasRequiredNonEmptyStrings(project, "projectId", "projectName", "finalState") ||
                !HasRequiredProperties(project, "completionTurn")))
        {
            fieldName = "completedProjects";
            actual = "completedProjects contains partial project entries";
            return true;
        }

        fieldName = string.Empty;
        actual = string.Empty;
        return false;
    }


    private static bool ArrayContainsIncompleteObjects(JsonElement value, Func<JsonElement, bool> isIncomplete)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in value.EnumerateArray())
        {
            if (isIncomplete(item))
                return true;
        }

        return false;
    }


    private static bool IsIncompleteFactionCustomState(JsonElement item)
    {
        return item.ValueKind != JsonValueKind.Object ||
               !HasAnyNonEmptyString(item, "stateName", "stateKey", "key", "name", "title") ||
               !HasRequiredProperties(item, "currentValue", "minValue", "maxValue", "description", "progressionRule", "thresholds");
    }


    private static bool HasRequiredProperties(JsonElement item, params string[] propertyNames)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var propertyName in propertyNames)
        {
            if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
                return false;
        }

        return true;
    }


    private static bool HasRequiredNonEmptyStrings(JsonElement item, params string[] propertyNames)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var propertyName in propertyNames)
        {
            if (!HasNonEmptyString(item, propertyName))
                return false;
        }

        return true;
    }


    private void RequireBooleanField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное boolean поле: {propName}",
                code: "missing_required_boolean_field",
                expected: "boolean",
                actual: "missing",
                repairHint: $"Добавь обязательное boolean поле {propName} со значением true или false по canonical contract."));
            return;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть boolean",
                code: "invalid_boolean_field",
                expected: "boolean",
                actual: value.ValueKind.ToString(),
                repairHint: $"Сохраняй {propName} как boolean true/false по canonical contract."));
        }
    }


    private void ValidateLooseStringOrObjectArray(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть массивом строк или объектов",
                code: "expected_string_or_object_array",
                expected: "JSON array of strings or objects",
                actual: value.ValueKind.ToString(),
                repairHint: "Сохрани поле как массив, где каждый элемент является строкой или JSON object."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind is not (JsonValueKind.String or JsonValueKind.Object))
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Элемент должен быть строкой или объектом",
                    code: "invalid_string_or_object_array_item",
                    expected: "string or object",
                    actual: item.ValueKind.ToString(),
                    repairHint: "Исправь элемент массива до непустой строки или JSON object по canonical contract."));
            }
            index++;
        }
    }


    private void ValidateFactionArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var canonicalFactionCoreFactions =
            string.Equals(contextPrefix, "game_state/factions/faction_core.json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(propName, "factions", StringComparison.OrdinalIgnoreCase);
        var sameTurnFactionInitialIds = CollectSameTurnFactionInitialIds(root);
        var knownPermanentFactionIds = CollectKnownPermanentFactionIds(root);
        var knownCanonicalFactionNames = CollectKnownPermanentFactionNames(root);
        var factionSubEntityStateIndex = CollectKnownFactionSubEntityStateIndex();
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;

            if (!HasAnyNonEmptyString(item, "factionId", "initialFactionId", "factionName", "name"))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Фракционный объект должен содержать factionId/initialFactionId/factionName/name",
                    code: "faction_missing_identity",
                    section: "Factions",
                    expected: "factionId or initialFactionId or factionName or name",
                    repairHint: "Передавай у фракционного объекта permanent factionId, same-turn initialFactionId или canonical factionName/name для идентификации и cross-reference."));
            }

            if (string.Equals(propName, "factions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propName, "factionDataChanges", StringComparison.OrdinalIgnoreCase) ||
                IsLikelyFullFactionObject(item))
            {
                if (canonicalFactionCoreFactions && string.IsNullOrWhiteSpace(GetFirstNonEmptyString(item, "factionId")))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.factionId",
                        IssueSeverity.Error,
                        "Каноническая faction_core.json.factions[] запись должна содержать permanent factionId; temporary initialId допустим только в same-turn GM delta, а не в сохранённом состоянии",
                        code: "canonical_faction_core_requires_permanent_faction_id",
                        section: "Factions",
                        expected: "non-empty permanent factionId in game_state/factions/faction_core.json.factions[]",
                        actual: item.TryGetProperty("factionId", out var factionIdNode)
                            ? factionIdNode.ValueKind.ToString()
                            : "missing",
                        repairHint: "Материализуй новую фракцию в canonical state с постоянным factionId вроде faction_<stable_slug>. Удали initialId/isNewFaction из сохранённой canonical записи или оставь их только в same-turn factionDataChanges."));
                }

                var fullObjectKnownPermanentFactionIds = canonicalFactionCoreFactions
                    ? null
                    : knownPermanentFactionIds;
                ValidateFullFactionObject(item, itemContext, issues, fullObjectKnownPermanentFactionIds, knownCanonicalFactionNames, sameTurnFactionInitialIds);
                continue;
            }

            switch (propName)
            {
                case "factionRankChanges":
                    ValidateFactionRankChangeCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
                    break;
                case "factionBonusChanges":
                    ValidateFactionBonusChangeCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds, factionSubEntityStateIndex);
                    break;
                case "factionResourceChanges":
                    ValidateFactionResourceChangeCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
                    break;
                case "factionProjectUpdates":
                    ValidateFactionProjectUpdateCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds, factionSubEntityStateIndex);
                    break;
                case "completeFactionProjects":
                    ValidateFactionProjectCompletionCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds, factionSubEntityStateIndex);
                    break;
                case "factionCustomStateChanges":
                    ValidateFactionCustomStateChangeCommand(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds, factionSubEntityStateIndex);
                    break;
            }
        }
    }


    private void ValidateFactionFullArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            if (!HasAnyNonEmptyString(item, "factionId", "factionName", "name"))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Фракционный объект должен содержать factionId/factionName/name",
                    code: "faction_full_array_missing_identity",
                    section: "Factions",
                    expected: "factionId or factionName or name",
                    actual: "missing identity fields",
                    repairHint: "Для canonical full faction object передай permanent factionId и/или canonical factionName/name, чтобы клиент мог однозначно разрешить целевую фракцию."));
            }

            RequireString(item, itemContext, issues, "name");
            RequireString(item, itemContext, issues, "description");
            ValidateOptionalString(item, itemContext, issues, "image_prompt");
            ValidateOptionalString(item, itemContext, issues, "factionColor");
            RequireNumberOrString(item, itemContext, issues, "level");
            RequireNumberOrString(item, itemContext, issues, "experience");
            RequireNumberOrString(item, itemContext, issues, "experienceForNextLevel");
            RequireString(item, itemContext, issues, "developmentArchetype");

            if (!item.TryGetProperty("isPlayerFaction", out var isPlayerFaction) ||
                isPlayerFaction.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.isPlayerFaction",
                    IssueSeverity.Error,
                    "Фракция должна содержать boolean isPlayerFaction"));
            }

            RequireBooleanField(item, itemContext, issues, "isPlayerMember");

            ValidateOptionalString(item, itemContext, issues, "playerRank");
            ValidateOptionalString(item, itemContext, issues, "playerBranch");
            ValidateOptionalString(item, itemContext, issues, "playerStrategyDirective");
            ValidateOptionalString(item, itemContext, issues, "reputationDescription");
            RequireNumberOrString(item, itemContext, issues, "reputation");

            if (item.TryGetProperty("powerProfile", out var powerProfile) &&
                RequireObject(powerProfile, $"{itemContext}.powerProfile", issues))
            {
                foreach (var scale in new[] { "military", "economic", "social", "covert", "logistics", "stability", "arcane_tech", "exploration" })
                    ValidateNonNegativeNumberField(powerProfile, $"{itemContext}.powerProfile", issues, scale);
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.powerProfile",
                    IssueSeverity.Error,
                    "Фракция должна содержать powerProfile"));
            }

            if (!item.TryGetProperty("resources", out var resources) ||
                !RequireObject(resources, $"{itemContext}.resources", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.resources",
                    IssueSeverity.Error,
                    "Фракция должна содержать resources"));
            }

            if (!item.TryGetProperty("ranks", out var ranks) ||
                !RequireObject(ranks, $"{itemContext}.ranks", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.ranks",
                    IssueSeverity.Error,
                    "Фракция должна содержать ranks"));
            }
            else if (ranks.TryGetProperty("branches", out var branches))
            {
                RequireArrayOfObjects(branches, $"{itemContext}.ranks.branches", issues);
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.ranks.branches",
                    IssueSeverity.Error,
                    "ranks должен содержать массив branches"));
            }

            ValidateFactionExtendedFields(item, itemContext, issues);

            if (item.TryGetProperty("relations", out var relations))
                RequireArrayOfObjects(relations, $"{itemContext}.relations", issues);
            if (item.TryGetProperty("customStates", out var customStates))
                RequireArrayOfObjects(customStates, $"{itemContext}.customStates", issues);
            if (item.TryGetProperty("activeProjects", out var activeProjects))
                RequireArrayOfObjects(activeProjects, $"{itemContext}.activeProjects", issues);
            if (item.TryGetProperty("completedProjects", out var completedProjects))
                RequireArrayOfObjects(completedProjects, $"{itemContext}.completedProjects", issues);
        }
    }


    private void ValidateFactionExtendedFields(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (item.TryGetProperty("customArchetypePriorities", out var priorities) &&
            RequireObject(priorities, $"{itemContext}.customArchetypePriorities", issues))
        {
            RequireString(priorities, $"{itemContext}.customArchetypePriorities", issues, "primary");
            RequireString(priorities, $"{itemContext}.customArchetypePriorities", issues, "secondary");
            RequireString(priorities, $"{itemContext}.customArchetypePriorities", issues, "tertiary");
        }

        if (item.TryGetProperty("controlledTerritories", out var territories))
        {
            if (territories.ValueKind == JsonValueKind.Null)
            {
                // allowed
            }
            else if (territories.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var territory in territories.EnumerateArray())
                {
                    var territoryContext = $"{itemContext}.controlledTerritories[{index++}]";
                    if (!RequireObject(territory, territoryContext, issues))
                        continue;
                    RequireString(territory, territoryContext, issues, "locationId");
                    RequireString(territory, territoryContext, issues, "locationName");
                }
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.controlledTerritories",
                    IssueSeverity.Error,
                    "controlledTerritories должен быть массивом или null",
                    code: "faction_controlled_territories_invalid_array_shape",
                    section: "Factions",
                    expected: "controlledTerritories array or null",
                    actual: territories.ValueKind.ToString(),
                    repairHint: "Передавай controlledTerritories как null или массив canonical territory entries с locationId/locationName."));
            }
        }

        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses) &&
            structuredBonuses.ValueKind != JsonValueKind.Null)
        {
            ValidateArrayItems(structuredBonuses, $"{itemContext}.structuredBonuses", issues, ValidateFactionStructuredBonusObject);
        }

        if (item.TryGetProperty("scribeChronicle", out var scribeChronicle) &&
            scribeChronicle.ValueKind != JsonValueKind.Null)
        {
            RequireArrayOfStrings(scribeChronicle, $"{itemContext}.scribeChronicle", issues);
        }

        if (item.TryGetProperty("resources", out var resources) &&
            RequireObject(resources, $"{itemContext}.resources", issues))
        {
            ValidateFactionResourceArray(resources, $"{itemContext}.resources", issues, "metaResources", true);
            ValidateFactionResourceArray(resources, $"{itemContext}.resources", issues, "strategicGoods", false);
        }
    }


    private void ValidateFactionStructuredBonusObject(JsonElement bonus, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(bonus, context, issues))
            return;

        RequireString(bonus, context, issues, "description");
        RequireString(bonus, context, issues, "bonusType");
        RequireString(bonus, context, issues, "target");
        RequireString(bonus, context, issues, "valueType");
        RequireNumberOrString(bonus, context, issues, "value");
        RequireString(bonus, context, issues, "application");
        ValidateOptionalString(bonus, context, issues, "condition");
    }


    private void ValidateFactionFullProjectObject(JsonElement item, string itemContext, List<ValidationIssue> issues, bool completed)
    {
        RequireString(item, itemContext, issues, "projectId");
        RequireString(item, itemContext, issues, "projectName");

        if (completed)
        {
            ValidateNonNegativeIntegerField(item, itemContext, issues, "completionTurn", "FactionProjects");
            var finalState = RequireString(item, itemContext, issues, "finalState");
            if (!string.IsNullOrWhiteSpace(finalState) &&
                !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(finalState, "Abandoned", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.finalState",
                    IssueSeverity.Error,
                    "completedProjects[].finalState должен быть Completed или Abandoned",
                    code: "faction_full_project_invalid_final_state",
                    section: "FactionProjects",
                    expected: "Completed or Abandoned",
                    actual: finalState,
                    repairHint: "Для завершённого проекта в completedProjects используй finalState = Completed или Abandoned."));
            }
            return;
        }

        var activeState = RequireString(item, itemContext, issues, "activeState");
        if (!string.IsNullOrWhiteSpace(activeState) &&
            (string.Equals(activeState, "Completed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(activeState, "Abandoned", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.activeState",
                IssueSeverity.Error,
                "activeProjects[] не должен хранить terminal state Completed/Abandoned",
                code: "faction_full_project_terminal_active_state_forbidden",
                section: "FactionProjects",
                expected: "Non-terminal activeState inside activeProjects[]",
                actual: activeState,
                repairHint: "Для завершённого или abandoned проекта перенеси запись в completedProjects[] с finalState, а не в activeProjects[]."));
        }
        RequireString(item, itemContext, issues, "description");
        ValidateFactionProjectCostArray(item, itemContext, issues, "totalResourceCost", "totalAmount");
        ValidateFactionProjectCostArray(item, itemContext, issues, "resourcesSpent", "amountSpent");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "totalTimeCostMinutes", "FactionProjects");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "timeSpentMinutes", "FactionProjects");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "totalSteps", "FactionProjects");
        ValidateNonNegativeIntegerField(item, itemContext, issues, "currentStep", "FactionProjects");
    }


    private void ValidateFactionProjectCostArray(JsonElement item, string itemContext, List<ValidationIssue> issues, string propName, string amountField)
    {
        if (!TryGetArray(item, propName, $"{itemContext}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var cost in arr.EnumerateArray())
        {
            var costContext = $"{itemContext}.{propName}[{index++}]";
            if (!RequireObject(cost, costContext, issues))
                continue;

            RequireString(cost, costContext, issues, "resourceName");
            RequireNumberOrString(cost, costContext, issues, amountField);
        }
    }


    private static bool HasAnyFactionProjectNonTerminalChanges(JsonElement projectUpdate)
    {
        foreach (var propName in new[]
                 {
                     "projectName", "name", "description", "activeState", "totalResourceCost", "resourcesSpent",
                     "totalTimeCostMinutes", "timeSpentMinutes", "totalSteps", "currentStep"
                 })
        {
            if (projectUpdate.TryGetProperty(propName, out _))
                return true;
        }

        return false;
    }


    private bool ValidateFactionAddOrUpdateIdField(
        JsonElement item,
        string context,
        List<ValidationIssue> issues,
        string propName,
        string code,
        string section,
        string repairHint)
    {
        if (!item.TryGetProperty(propName, out var idValue))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть явно указан как непустая строка для update или null для add",
                code: code,
                section: section,
                expected: $"{propName}=null for add or non-empty string for update",
                actual: "missing",
                repairHint: repairHint));
            return false;
        }

        if (idValue.ValueKind == JsonValueKind.Null)
            return true;

        if (idValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idValue.GetString()))
            return true;

        issues.Add(new ValidationIssue(
            $"{context}.{propName}",
            IssueSeverity.Error,
            $"{propName} должен быть null для add или непустой строкой для update",
            code: code,
            section: section,
            expected: $"{propName}=null for add or non-empty string for update",
            actual: idValue.ValueKind == JsonValueKind.String ? "empty string" : idValue.ValueKind.ToString(),
            repairHint: repairHint));
        return false;
    }


    private void ValidateFactionCommandTarget(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string>? allowedInitialFactionIds = null, HashSet<string>? knownPermanentFactionIds = null)
    {
        var factionId = GetFirstNonEmptyString(item, "factionId");
        var initialFactionId = GetFirstNonEmptyString(item, "initialFactionId");
        var hasFactionId = !string.IsNullOrWhiteSpace(factionId);
        var hasInitialFactionId = !string.IsNullOrWhiteSpace(initialFactionId);

        if (!hasFactionId && !hasInitialFactionId)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "Faction command должен содержать factionId для existing faction или initialFactionId для новой same-turn faction",
                code: "faction_command_missing_identity",
                section: "Factions",
                expected: "factionId for existing faction or initialFactionId for new same-turn faction",
                repairHint: "Для existing faction передавай permanent factionId. Для новой same-turn faction используй initialFactionId, который совпадает с factionDataChanges.initialId."));
        }

        if (hasFactionId && hasInitialFactionId)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "Faction command не должен одновременно использовать factionId и initialFactionId",
                code: "faction_command_conflicting_identity",
                section: "Factions",
                repairHint: "Для existing faction используй permanent factionId; для новой same-turn faction используй только initialFactionId."));
        }

        if (!hasFactionId && hasInitialFactionId &&
            (allowedInitialFactionIds == null || !allowedInitialFactionIds.Contains(initialFactionId!)))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.initialFactionId",
                IssueSeverity.Error,
                "initialFactionId допустим только для новой фракции, создаваемой в том же accepted turn через factionDataChanges.initialId",
                code: "faction_command_invalid_initial_target",
                section: "Factions",
                expected: "initialFactionId that matches a new same-turn factionDataChanges.initialId",
                actual: initialFactionId,
                repairHint: "Для уже существующей фракции используй permanent factionId. initialFactionId оставляй только для same-turn linking новой фракции."));
        }

        if (hasFactionId &&
            knownPermanentFactionIds != null &&
            knownPermanentFactionIds.Count > 0 &&
            !knownPermanentFactionIds.Contains(factionId!))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.factionId",
                IssueSeverity.Error,
                $"Faction command ссылается на неизвестный factionId '{factionId}'",
                code: "faction_command_unknown_faction_id",
                section: "Factions",
                expected: "existing permanent factionId from faction_core.json",
                actual: factionId,
                repairHint: "Для existing faction используй существующий permanent factionId из faction_core.json. Для новой фракции в этом же ходу создай её через factionDataChanges и ссылайся на неё только через initialFactionId для same-turn linking."));
        }

        ValidateOptionalString(item, itemContext, issues, "factionId");
        ValidateOptionalString(item, itemContext, issues, "initialFactionId");
        ValidateOptionalString(item, itemContext, issues, "factionName");
        ValidateOptionalString(item, itemContext, issues, "name");
    }


    private HashSet<string> CollectSameTurnFactionInitialIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void CollectFromRoot(JsonElement candidateRoot, HashSet<string> target)
        {
            foreach (var propName in new[] { "factionDataChanges", "factions" })
            {
                if (!candidateRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var initialId = GetFirstNonEmptyString(item, "initialId");
                    var factionId = GetFirstNonEmptyString(item, "factionId");
                    if (!string.IsNullOrWhiteSpace(initialId) && string.IsNullOrWhiteSpace(factionId))
                        target.Add(initialId);
                }
            }
        }

        var currentFactionCoreJson = ReadCurrentTrackedFileSync("game_state/factions/faction_core.json");
        if (!string.IsNullOrWhiteSpace(currentFactionCoreJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(currentFactionCoreJson);
                CollectFromRoot(doc.RootElement, ids);
            }
            catch
            {
                // ignored
            }
        }

        CollectFromRoot(root, ids);

        return ids;
    }


    private HashSet<string> CollectKnownPermanentFactionIds(JsonElement root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void CollectFromRoot(JsonElement candidateRoot, HashSet<string> target)
        {
            foreach (var propName in new[] { "factionDataChanges", "factions" })
            {
                if (!candidateRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var factionId = GetFirstNonEmptyString(item, "factionId");
                    if (!string.IsNullOrWhiteSpace(factionId))
                        target.Add(factionId);
                }
            }
        }

        var preTurnFactionCoreJson = ReadPreTurnTrackedFileSync("game_state/factions/faction_core.json");
        if (!string.IsNullOrWhiteSpace(preTurnFactionCoreJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnFactionCoreJson);
                CollectFromRoot(doc.RootElement, ids);
            }
            catch
            {
                // ignored
            }
        }

        return ids;
    }


    private HashSet<string> CollectKnownPermanentFactionNames(JsonElement root)
    {
        if (_knownCanonicalFactionNamesCache != null)
            return _knownCanonicalFactionNamesCache;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void CollectFromRoot(JsonElement candidateRoot, HashSet<string> target)
        {
            foreach (var propName in new[] { "factionDataChanges", "factions" })
            {
                if (!candidateRoot.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var factionId = GetFirstNonEmptyString(item, "factionId");
                    var name = GetFirstNonEmptyString(item, "name", "factionName");
                    if (!string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(name))
                        target.Add(name);
                }
            }
        }

        var preTurnFactionCoreJson = ReadPreTurnTrackedFileSync("game_state/factions/faction_core.json");
        if (!string.IsNullOrWhiteSpace(preTurnFactionCoreJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnFactionCoreJson);
                CollectFromRoot(doc.RootElement, names);
            }
            catch
            {
                // ignored
            }
        }

        _knownCanonicalFactionNamesCache = names;
        return names;
    }


    private FactionSubEntityStateIndex CollectKnownFactionSubEntityStateIndex()
    {
        var index = new FactionSubEntityStateIndex();

        void CollectFromRoot(JsonElement candidateRoot)
        {
            if (candidateRoot.ValueKind != JsonValueKind.Object)
                return;

            RegisterFactionSubEntities(index, candidateRoot);
            foreach (var prop in candidateRoot.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in prop.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                        RegisterFactionSubEntities(index, item);
                }
            }
        }

        foreach (var trackedPath in new[]
                 {
                     "game_state/factions/faction_core.json",
                     "game_state/factions/faction_structure.json",
                     "game_state/factions/faction_projects.json",
                     "game_state/factions/faction_custom.json"
                 })
        {
            var preTurnJson = ReadPreTurnTrackedFileSync(trackedPath);
            if (!string.IsNullOrWhiteSpace(preTurnJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(preTurnJson);
                    CollectFromRoot(doc.RootElement);
                }
                catch
                {
                    // ignored
                }
            }

        }

        return index;
    }


    private static void RegisterFactionSubEntities(FactionSubEntityStateIndex index, JsonElement item)
    {
        var factionKey = GetFirstNonEmptyString(item, "factionId", "initialFactionId", "initialId");
        if (string.IsNullOrWhiteSpace(factionKey))
            return;

        if (item.TryGetProperty("structuredBonuses", out var structuredBonuses) && structuredBonuses.ValueKind == JsonValueKind.Array)
        {
            foreach (var bonus in structuredBonuses.EnumerateArray())
            {
                var bonusId = GetFirstNonEmptyString(bonus, "bonusId");
                if (!string.IsNullOrWhiteSpace(bonusId))
                    AddDictionarySetValue(index.BonusIdsByFactionKey, factionKey, bonusId);
            }
        }

        foreach (var propName in new[] { "activeProjects", "completedProjects" })
        {
            if (!item.TryGetProperty(propName, out var projects) || projects.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var project in projects.EnumerateArray())
            {
                var projectId = GetFirstNonEmptyString(project, "projectId");
                if (!string.IsNullOrWhiteSpace(projectId))
                    AddDictionarySetValue(index.ProjectIdsByFactionKey, factionKey, projectId);
            }
        }

        if (item.TryGetProperty("customStates", out var customStates) && customStates.ValueKind == JsonValueKind.Array)
        {
            foreach (var state in customStates.EnumerateArray())
            {
                var stateId = GetFirstNonEmptyString(state, "stateId");
                if (!string.IsNullOrWhiteSpace(stateId))
                    AddDictionarySetValue(index.CustomStateIdsByFactionKey, factionKey, stateId);
            }
        }
    }


    private static string? ResolveFactionCommandKey(JsonElement item)
        => GetFirstNonEmptyString(item, "factionId", "initialFactionId");


    private static HashSet<string> GetFactionSubEntitySet(Dictionary<string, HashSet<string>> dictionary, string factionKey)
    {
        if (!dictionary.TryGetValue(factionKey, out var ids))
        {
            ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dictionary[factionKey] = ids;
        }

        return ids;
    }


    private void ValidateFactionBonusChangeCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds, FactionSubEntityStateIndex factionSubEntityStateIndex)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
        var factionKey = ResolveFactionCommandKey(item);

        var hasAddOrUpdate = item.TryGetProperty("bonusesToAddOrUpdate", out var bonusesToAddOrUpdate);
        var hasRemove = item.TryGetProperty("bonusesToRemove", out var bonusesToRemove);
        if (!hasAddOrUpdate && !hasRemove)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "factionBonusChanges item должен содержать bonusesToAddOrUpdate и/или bonusesToRemove",
                code: "faction_bonus_change_missing_operations",
                section: "FactionBonuses",
                expected: "bonusesToAddOrUpdate and/or bonusesToRemove",
                actual: "no bonus operations",
                repairHint: "Добавь в factionBonusChanges хотя бы одну ветку: bonusesToAddOrUpdate для создания/обновления бонусов и/или bonusesToRemove для удаления существующих bonusId."));
            return;
        }

        if (hasAddOrUpdate)
        {
            RequireArrayOfObjects(bonusesToAddOrUpdate, $"{itemContext}.bonusesToAddOrUpdate", issues);
            if (bonusesToAddOrUpdate.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var bonus in bonusesToAddOrUpdate.EnumerateArray())
                {
                    var bonusContext = $"{itemContext}.bonusesToAddOrUpdate[{index++}]";
                    if (!RequireObject(bonus, bonusContext, issues))
                        continue;

                    if (!ValidateFactionAddOrUpdateIdField(
                            bonus,
                            bonusContext,
                            issues,
                            "bonusId",
                            code: "faction_bonus_add_or_update_invalid_bonus_id",
                            section: "FactionBonuses",
                            repairHint: "Для нового бонуса передай bonusId = null. Для обновления существующего бонуса передай непустой bonusId строкой."))
                    {
                        continue;
                    }

                    var bonusId = GetFirstNonEmptyString(bonus, "bonusId");
                    if (!string.IsNullOrWhiteSpace(factionKey) && !string.IsNullOrWhiteSpace(bonusId))
                    {
                        var knownBonusIds = GetFactionSubEntitySet(factionSubEntityStateIndex.BonusIdsByFactionKey, factionKey);
                        if (!knownBonusIds.Contains(bonusId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{bonusContext}.bonusId",
                                IssueSeverity.Error,
                                "factionBonusChanges пытается обновить bonusId, которого нет в canonical state этой фракции",
                                code: "faction_bonus_update_unknown_bonus_id",
                                section: "FactionBonuses",
                                expected: "existing bonusId of the targeted faction or null for a brand-new bonus",
                                actual: bonusId,
                                repairHint: "Для обновления используй реальный bonusId из structuredBonuses target faction. Для нового бонуса передай bonusId = null и дай системе создать permanent identity позже."));
                        }
                        else
                        {
                            knownBonusIds.Add(bonusId);
                        }
                    }

                    ValidateFactionStructuredBonusObject(bonus, bonusContext, issues);
                }
            }
        }

        if (hasRemove)
        {
            RequireArrayOfStrings(bonusesToRemove, $"{itemContext}.bonusesToRemove", issues);
            if (!string.IsNullOrWhiteSpace(factionKey) && bonusesToRemove.ValueKind == JsonValueKind.Array)
            {
                var knownBonusIds = GetFactionSubEntitySet(factionSubEntityStateIndex.BonusIdsByFactionKey, factionKey);
                var removeIndex = 0;
                foreach (var bonusIdNode in bonusesToRemove.EnumerateArray())
                {
                    var removeContext = $"{itemContext}.bonusesToRemove[{removeIndex++}]";
                    if (bonusIdNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(bonusIdNode.GetString()))
                        continue;

                    var bonusId = bonusIdNode.GetString()!;
                    if (!knownBonusIds.Contains(bonusId))
                    {
                        issues.Add(new ValidationIssue(
                            removeContext,
                            IssueSeverity.Error,
                            "factionBonusChanges пытается удалить bonusId, которого нет у target faction",
                            code: "faction_bonus_remove_unknown_bonus_id",
                            section: "FactionBonuses",
                            expected: "existing bonusId from target faction structuredBonuses",
                            actual: bonusId,
                            repairHint: "Удаляй только тот bonusId, который реально существует в structuredBonuses выбранной фракции."));
                        continue;
                    }

                    knownBonusIds.Remove(bonusId);
                }
            }
        }
    }


    private void ValidateFactionResourceChangeCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
        if (!TryGetArray(item, "resourceChanges", $"{itemContext}.resourceChanges", issues, out var resourceChanges))
            return;

        var index = 0;
        foreach (var resourceChange in resourceChanges.EnumerateArray())
        {
            var changeContext = $"{itemContext}.resourceChanges[{index++}]";
            if (!RequireObject(resourceChange, changeContext, issues))
                continue;

            RequireString(resourceChange, changeContext, issues, "resourceName");
            ValidateIntegerField(resourceChange, changeContext, issues, "changeAmount");
        }
    }


    private void ValidateFactionProjectUpdateCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds, FactionSubEntityStateIndex factionSubEntityStateIndex)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
        var factionKey = ResolveFactionCommandKey(item);
        if (!item.TryGetProperty("projectUpdate", out var projectUpdate) ||
            !RequireObject(projectUpdate, $"{itemContext}.projectUpdate", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.projectUpdate",
                IssueSeverity.Error,
                "factionProjectUpdates item должен содержать projectUpdate",
                code: "faction_project_update_missing_payload",
                section: "FactionProjects",
                expected: "projectUpdate object",
                actual: "missing or non-object",
                repairHint: "Добавь в factionProjectUpdates объект projectUpdate с projectId и хотя бы одним non-terminal tracker change."));
            return;
        }

        var projectId = RequireString(projectUpdate, $"{itemContext}.projectUpdate", issues, "projectId");
        if (!string.IsNullOrWhiteSpace(factionKey) && !string.IsNullOrWhiteSpace(projectId))
        {
            var knownProjectIds = GetFactionSubEntitySet(factionSubEntityStateIndex.ProjectIdsByFactionKey, factionKey);
            if (!knownProjectIds.Contains(projectId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.projectUpdate.projectId",
                    IssueSeverity.Error,
                    "factionProjectUpdates ссылается на projectId, которого нет в canonical project tracker этой фракции",
                    code: "faction_project_update_unknown_project_id",
                    section: "FactionProjects",
                    expected: "existing projectId from activeProjects/completedProjects of the target faction",
                    actual: projectId,
                    repairHint: "Обновляй только существующий projectId target faction. Новый проект создай в canonical faction state, а partial progress меняй через factionProjectUpdates только после появления projectId."));
            }
        }
        if (!HasAnyFactionProjectNonTerminalChanges(projectUpdate))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.projectUpdate",
                IssueSeverity.Error,
                "factionProjectUpdates.projectUpdate должен содержать хотя бы одно non-terminal tracker change",
                code: "faction_project_update_missing_changes",
                section: "FactionProjects",
                repairHint: "Передай вместе с projectId хотя бы одно допустимое non-terminal изменение: projectName/description, activeState, totalResourceCost/resourcesSpent, totalTimeCostMinutes/timeSpentMinutes или totalSteps/currentStep."));
        }
        if (projectUpdate.TryGetProperty("projectName", out _))
            RequireString(projectUpdate, $"{itemContext}.projectUpdate", issues, "projectName");
        if (projectUpdate.TryGetProperty("name", out _))
            RequireString(projectUpdate, $"{itemContext}.projectUpdate", issues, "name");
        if (projectUpdate.TryGetProperty("description", out _))
            RequireString(projectUpdate, $"{itemContext}.projectUpdate", issues, "description");
        if (projectUpdate.TryGetProperty("totalResourceCost", out _))
            ValidateFactionProjectCostArray(projectUpdate, $"{itemContext}.projectUpdate", issues, "totalResourceCost", "totalAmount");
        if (projectUpdate.TryGetProperty("timeSpentMinutes", out _))
            ValidateNonNegativeIntegerField(projectUpdate, $"{itemContext}.projectUpdate", issues, "timeSpentMinutes", "FactionProjects");
        if (projectUpdate.TryGetProperty("totalTimeCostMinutes", out _))
            ValidateNonNegativeIntegerField(projectUpdate, $"{itemContext}.projectUpdate", issues, "totalTimeCostMinutes", "FactionProjects");
        if (projectUpdate.TryGetProperty("totalSteps", out _))
            ValidateNonNegativeIntegerField(projectUpdate, $"{itemContext}.projectUpdate", issues, "totalSteps", "FactionProjects");
        if (projectUpdate.TryGetProperty("currentStep", out _))
            ValidateNonNegativeIntegerField(projectUpdate, $"{itemContext}.projectUpdate", issues, "currentStep", "FactionProjects");
        if (projectUpdate.TryGetProperty("activeState", out _))
        {
            var activeState = RequireString(projectUpdate, $"{itemContext}.projectUpdate", issues, "activeState");
            if (string.Equals(activeState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activeState, "Abandoned", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.projectUpdate.activeState",
                    IssueSeverity.Error,
                    "factionProjectUpdates не должен завершать проект через Completed/Abandoned",
                    code: "faction_project_update_terminal_state_forbidden",
                    section: "FactionProjects",
                    expected: "Non-terminal activeState or completeFactionProjects command",
                    actual: activeState,
                    repairHint: "Для завершения проекта используй completeFactionProjects, а factionProjectUpdates оставь только для progress/partial changes."));
            }
        }

        if (projectUpdate.TryGetProperty("resourcesSpent", out var resourcesSpent))
        {
            RequireArrayOfObjects(resourcesSpent, $"{itemContext}.projectUpdate.resourcesSpent", issues);
            if (resourcesSpent.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var spent in resourcesSpent.EnumerateArray())
                {
                    var spentContext = $"{itemContext}.projectUpdate.resourcesSpent[{index++}]";
                    if (!RequireObject(spent, spentContext, issues))
                        continue;

                    RequireString(spent, spentContext, issues, "resourceName");
                    ValidateNonNegativeNumberField(spent, spentContext, issues, "amountSpent");
                }
            }
        }
    }


    private void ValidateFactionProjectCompletionCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds, FactionSubEntityStateIndex factionSubEntityStateIndex)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
        var factionKey = ResolveFactionCommandKey(item);
        var projectId = RequireString(item, itemContext, issues, "projectId");
        RequireString(item, itemContext, issues, "projectName");
        var finalState = RequireString(item, itemContext, issues, "finalState");
        if (!string.IsNullOrWhiteSpace(factionKey) && !string.IsNullOrWhiteSpace(projectId))
        {
            var knownProjectIds = GetFactionSubEntitySet(factionSubEntityStateIndex.ProjectIdsByFactionKey, factionKey);
            if (!knownProjectIds.Contains(projectId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.projectId",
                    IssueSeverity.Error,
                    "completeFactionProjects ссылается на projectId, которого нет у target faction",
                    code: "faction_project_completion_unknown_project_id",
                    section: "FactionProjects",
                    expected: "existing projectId from target faction project tracker",
                    actual: projectId,
                    repairHint: "Завершай только тот projectId, который реально существует у выбранной фракции. Если проект новый, сначала создай его в canonical faction state."));
            }
        }
        if (!string.IsNullOrWhiteSpace(finalState) &&
            !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(finalState, "Abandoned", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.finalState",
                IssueSeverity.Error,
                "completeFactionProjects.finalState должен быть Completed или Abandoned",
                code: "faction_project_completion_invalid_final_state",
                section: "FactionProjects",
                expected: "Completed or Abandoned",
                actual: finalState,
                repairHint: "Для completeFactionProjects используй только finalState = Completed или Abandoned."));
        }
    }


    private void ValidateFactionCustomStateChangeCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds, FactionSubEntityStateIndex factionSubEntityStateIndex)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);
        var factionKey = ResolveFactionCommandKey(item);

        var hasAddOrUpdate = item.TryGetProperty("statesToAddOrUpdate", out var statesToAddOrUpdate);
        var hasRemove = item.TryGetProperty("statesToRemove", out var statesToRemove);
        if (!hasAddOrUpdate && !hasRemove)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "factionCustomStateChanges item должен содержать statesToAddOrUpdate и/или statesToRemove",
                code: "faction_custom_state_change_missing_operations",
                section: "Factions",
                expected: "statesToAddOrUpdate and/or statesToRemove",
                actual: "no custom state operations",
                repairHint: "Добавь в factionCustomStateChanges хотя бы одну ветку: statesToAddOrUpdate для создания/обновления custom state entries и/или statesToRemove для удаления существующих stateId."));
            return;
        }

        if (hasAddOrUpdate)
        {
            RequireArrayOfObjects(statesToAddOrUpdate, $"{itemContext}.statesToAddOrUpdate", issues);
            if (statesToAddOrUpdate.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var state in statesToAddOrUpdate.EnumerateArray())
                {
                    var stateContext = $"{itemContext}.statesToAddOrUpdate[{index++}]";
                    if (!RequireObject(state, stateContext, issues))
                        continue;

                    if (!ValidateFactionAddOrUpdateIdField(
                            state,
                            stateContext,
                            issues,
                            "stateId",
                            code: "faction_custom_state_change_invalid_state_id",
                            section: "Factions",
                            repairHint: "Для нового custom state передай stateId = null. Для обновления существующего state передай непустой stateId строкой вместе с полным Custom State Object."))
                    {
                        continue;
                    }

                    var stateId = GetFirstNonEmptyString(state, "stateId");
                    if (!string.IsNullOrWhiteSpace(factionKey) && !string.IsNullOrWhiteSpace(stateId))
                    {
                        var knownStateIds = GetFactionSubEntitySet(factionSubEntityStateIndex.CustomStateIdsByFactionKey, factionKey);
                        if (!knownStateIds.Contains(stateId))
                        {
                            issues.Add(new ValidationIssue(
                                $"{stateContext}.stateId",
                                IssueSeverity.Error,
                                "factionCustomStateChanges пытается обновить stateId, которого нет у target faction",
                                code: "faction_custom_state_update_unknown_state_id",
                                section: "Factions",
                                expected: "existing stateId from target faction customStates or null for a brand-new state",
                                actual: stateId,
                                repairHint: "Для обновления используй реальный stateId из customStates target faction. Для нового состояния передай stateId = null."));
                        }
                        else
                        {
                            knownStateIds.Add(stateId);
                        }
                    }

                    ValidateCanonicalFactionCustomStateObject(state, stateContext, issues);
                }
            }
        }

        if (hasRemove)
        {
            RequireArrayOfStrings(statesToRemove, $"{itemContext}.statesToRemove", issues);
            if (!string.IsNullOrWhiteSpace(factionKey) && statesToRemove.ValueKind == JsonValueKind.Array)
            {
                var knownStateIds = GetFactionSubEntitySet(factionSubEntityStateIndex.CustomStateIdsByFactionKey, factionKey);
                var removeIndex = 0;
                foreach (var stateIdNode in statesToRemove.EnumerateArray())
                {
                    var removeContext = $"{itemContext}.statesToRemove[{removeIndex++}]";
                    if (stateIdNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(stateIdNode.GetString()))
                        continue;

                    var stateId = stateIdNode.GetString()!;
                    if (!knownStateIds.Contains(stateId))
                    {
                        issues.Add(new ValidationIssue(
                            removeContext,
                            IssueSeverity.Error,
                            "factionCustomStateChanges пытается удалить stateId, которого нет у target faction",
                            code: "faction_custom_state_remove_unknown_state_id",
                            section: "Factions",
                            expected: "existing stateId from target faction customStates",
                            actual: stateId,
                            repairHint: "Удаляй только тот stateId, который реально существует в customStates выбранной фракции."));
                        continue;
                    }

                    knownStateIds.Remove(stateId);
                }
            }
        }
    }


    private void ValidateFactionRankChangeCommand(JsonElement item, string itemContext, List<ValidationIssue> issues,
        HashSet<string> sameTurnFactionInitialIds, HashSet<string> knownPermanentFactionIds)
    {
        ValidateFactionCommandTarget(item, itemContext, issues, sameTurnFactionInitialIds, knownPermanentFactionIds);

        var hasAnyOperation = false;
        if (item.TryGetProperty("branchesToAdd", out var branchesToAdd))
        {
            hasAnyOperation = true;
            RequireArrayOfObjects(branchesToAdd, $"{itemContext}.branchesToAdd", issues);
            if (branchesToAdd.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var branch in branchesToAdd.EnumerateArray())
                {
                    var branchContext = $"{itemContext}.branchesToAdd[{index++}]";
                    if (!RequireObject(branch, branchContext, issues))
                        continue;

                    RequireString(branch, branchContext, issues, "branchId");
                    RequireString(branch, branchContext, issues, "displayName");
                    RequireBooleanField(branch, branchContext, issues, "isCoreBranch");
                    if (!TryGetArray(branch, "ranks", $"{branchContext}.ranks", issues, out var ranks))
                        continue;

                    var rankIndex = 0;
                    foreach (var rank in ranks.EnumerateArray())
                        ValidateFactionRankObject(rank, $"{branchContext}.ranks[{rankIndex++}]", issues);
                }
            }
        }

        if (item.TryGetProperty("branchesToUpdate", out var branchesToUpdate))
        {
            hasAnyOperation = true;
            RequireArrayOfObjects(branchesToUpdate, $"{itemContext}.branchesToUpdate", issues);
            if (branchesToUpdate.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var branch in branchesToUpdate.EnumerateArray())
                {
                    var branchContext = $"{itemContext}.branchesToUpdate[{index++}]";
                    if (!RequireObject(branch, branchContext, issues))
                        continue;

                    RequireString(branch, branchContext, issues, "branchId");
                    ValidateOptionalString(branch, branchContext, issues, "newDisplayName");
                }
            }
        }

        if (item.TryGetProperty("branchesToRemove", out var branchesToRemove))
        {
            hasAnyOperation = true;
            RequireArrayOfStrings(branchesToRemove, $"{itemContext}.branchesToRemove", issues);
            if (branchesToRemove.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var branchIdNode in branchesToRemove.EnumerateArray())
                {
                    var branchId = branchIdNode.ValueKind == JsonValueKind.String ? branchIdNode.GetString() ?? string.Empty : string.Empty;
                    if (string.Equals(branchId, "core", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.branchesToRemove[{index}]",
                            IssueSeverity.Error,
                            "Нельзя удалять core branch через factionRankChanges",
                            code: "faction_rank_core_branch_remove_forbidden",
                            section: "FactionRanks",
                            expected: "Any branch except core",
                            actual: branchId,
                            repairHint: "Не добавляй core в branchesToRemove; core branch является неустранимой частью иерархии."));
                    }
                    index++;
                }
            }
        }

        if (item.TryGetProperty("ranksToAdd", out var ranksToAdd))
        {
            hasAnyOperation = true;
            RequireArrayOfObjects(ranksToAdd, $"{itemContext}.ranksToAdd", issues);
            if (ranksToAdd.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var rankAdd in ranksToAdd.EnumerateArray())
                {
                    var rankAddContext = $"{itemContext}.ranksToAdd[{index++}]";
                    if (!RequireObject(rankAdd, rankAddContext, issues))
                        continue;

                    RequireString(rankAdd, rankAddContext, issues, "targetBranchId");
                    if (rankAdd.TryGetProperty("rank", out var rank))
                        ValidateFactionRankObject(rank, $"{rankAddContext}.rank", issues);
                    else
                        issues.Add(new ValidationIssue(
                            $"{rankAddContext}.rank",
                            IssueSeverity.Error,
                            "ranksToAdd item должен содержать полный rank object"));
                }
            }
        }

        if (item.TryGetProperty("ranksToUpdate", out var ranksToUpdate))
        {
            hasAnyOperation = true;
            RequireArrayOfObjects(ranksToUpdate, $"{itemContext}.ranksToUpdate", issues);
            if (ranksToUpdate.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var rankUpdate in ranksToUpdate.EnumerateArray())
                {
                    var rankUpdateContext = $"{itemContext}.ranksToUpdate[{index++}]";
                    if (!RequireObject(rankUpdate, rankUpdateContext, issues))
                        continue;

                    RequireString(rankUpdate, rankUpdateContext, issues, "targetBranchId");
                    RequireString(rankUpdate, rankUpdateContext, issues, "rankIdentifier");
                    if (!rankUpdate.TryGetProperty("update", out var update) ||
                        !RequireObject(update, $"{rankUpdateContext}.update", issues))
                    {
                        issues.Add(new ValidationIssue(
                            $"{rankUpdateContext}.update",
                            IssueSeverity.Error,
                            "ranksToUpdate item должен содержать update object"));
                        continue;
                    }

                    if (!HasAnyNonEmptyString(update, "newRankNameMale", "newRankNameFemale", "newUnlockCondition") &&
                        !update.TryGetProperty("newRequiredReputation", out _) &&
                        !update.TryGetProperty("newBenefits", out _) &&
                        !update.TryGetProperty("newIsJunctionPoint", out _) &&
                        !update.TryGetProperty("newAvailableBranches", out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{rankUpdateContext}.update",
                            IssueSeverity.Error,
                            "ranksToUpdate.update должен содержать хотя бы одно изменяемое поле"));
                    }

                    ValidateOptionalString(update, $"{rankUpdateContext}.update", issues, "newRankNameMale");
                    ValidateOptionalString(update, $"{rankUpdateContext}.update", issues, "newRankNameFemale");
                    if (update.TryGetProperty("newRequiredReputation", out _))
                        ValidateNumberField(update, $"{rankUpdateContext}.update", issues, "newRequiredReputation");
                    ValidateOptionalString(update, $"{rankUpdateContext}.update", issues, "newUnlockCondition");
                    if (update.TryGetProperty("newBenefits", out var newBenefits))
                        RequireArrayOfStrings(newBenefits, $"{rankUpdateContext}.update.newBenefits", issues);
                    if (update.TryGetProperty("newIsJunctionPoint", out var newIsJunctionPoint) &&
                        newIsJunctionPoint.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        issues.Add(new ValidationIssue(
                            $"{rankUpdateContext}.update.newIsJunctionPoint",
                            IssueSeverity.Error,
                            "newIsJunctionPoint должен быть boolean"));
                    }
                    if (update.TryGetProperty("newAvailableBranches", out var newAvailableBranches))
                        RequireArrayOfStrings(newAvailableBranches, $"{rankUpdateContext}.update.newAvailableBranches", issues);
                }
            }
        }

        if (item.TryGetProperty("ranksToRemove", out var ranksToRemove))
        {
            hasAnyOperation = true;
            RequireArrayOfObjects(ranksToRemove, $"{itemContext}.ranksToRemove", issues);
            if (ranksToRemove.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var rankRemove in ranksToRemove.EnumerateArray())
                {
                    var rankRemoveContext = $"{itemContext}.ranksToRemove[{index++}]";
                    if (!RequireObject(rankRemove, rankRemoveContext, issues))
                        continue;

                    RequireString(rankRemove, rankRemoveContext, issues, "targetBranchId");
                    RequireString(rankRemove, rankRemoveContext, issues, "rankIdentifier");
                }
            }
        }

        if (!hasAnyOperation)
        {
            issues.Add(new ValidationIssue(
                itemContext,
                IssueSeverity.Error,
                "factionRankChanges item должен содержать хотя бы одну rank/branch операцию",
                code: "faction_rank_change_missing_operations",
                section: "FactionRanks",
                expected: "branchesToAdd | branchesToUpdate | branchesToRemove | ranksToAdd | ranksToUpdate | ranksToRemove",
                actual: "no rank or branch operations",
                repairHint: "Добавь в factionRankChanges хотя бы одну rank/branch операцию. Не отправляй пустой командный объект без изменений и без target payload."));
        }
    }


    private void ValidateFactionRankObject(JsonElement rank, string rankContext, List<ValidationIssue> issues)
    {
        if (!RequireObject(rank, rankContext, issues))
            return;

        RequireString(rank, rankContext, issues, "rankNameMale");
        RequireString(rank, rankContext, issues, "rankNameFemale");
        RequireNumberOrString(rank, rankContext, issues, "requiredReputation");
        RequireString(rank, rankContext, issues, "unlockCondition");
        RequireBooleanField(rank, rankContext, issues, "isJunctionPoint");
        if (rank.TryGetProperty("benefits", out var benefits) && benefits.ValueKind != JsonValueKind.String)
            RequireArrayOfStrings(benefits, $"{rankContext}.benefits", issues);
        if (rank.TryGetProperty("availableBranches", out var availableBranches))
            RequireArrayOfStrings(availableBranches, $"{rankContext}.availableBranches", issues);
    }


    private void ValidateFactionSidecarResourceArray(JsonElement arr, string context, List<ValidationIssue> issues, bool requireUpkeep)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingResourceFields = new List<string>();
            if (!HasNonEmptyString(item, "resourceName"))
                missingResourceFields.Add("resourceName");
            if (!item.TryGetProperty("currentStockpile", out _))
                missingResourceFields.Add("currentStockpile");
            if (!item.TryGetProperty("incomePerCycle", out _))
                missingResourceFields.Add("incomePerCycle");
            if (requireUpkeep && !item.TryGetProperty("upkeepPerCycle", out _))
                missingResourceFields.Add("upkeepPerCycle");

            if (missingResourceFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Canonical faction resource entry не содержит обязательные поля",
                    code: "canonical_faction_resource_entry_missing_required_fields",
                    section: "Factions",
                    expected: requireUpkeep
                        ? "resourceName, currentStockpile, incomePerCycle, upkeepPerCycle"
                        : "resourceName, currentStockpile, incomePerCycle",
                    actual: string.Join(", ", missingResourceFields),
                    repairHint: "Для canonical faction_resources entries сохраняй полный resource object с обязательными числовыми полями, а не partial delta."));
                continue;
            }

            RequireString(item, itemContext, issues, "resourceName");
            RequireNonNegativeNumberField(item, itemContext, issues, "currentStockpile");
            RequireNonNegativeNumberField(item, itemContext, issues, "incomePerCycle");
            if (requireUpkeep)
                RequireNonNegativeNumberField(item, itemContext, issues, "upkeepPerCycle");
        }
    }


    private void ValidateCanonicalFactionCustomStateObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(item, context, issues))
            return;

        var hasIdentity = HasAnyNonEmptyString(item, "stateName", "stateKey", "key", "name", "title");
        var missingCoreFields = new List<string>();
        if (!hasIdentity)
            missingCoreFields.Add("stateName/stateKey/key/name/title");
        if (!item.TryGetProperty("currentValue", out _))
            missingCoreFields.Add("currentValue");
        if (!item.TryGetProperty("minValue", out _))
            missingCoreFields.Add("minValue");
        if (!item.TryGetProperty("maxValue", out _))
            missingCoreFields.Add("maxValue");
        if (!HasNonEmptyString(item, "description"))
            missingCoreFields.Add("description");
        if (!item.TryGetProperty("progressionRule", out _))
            missingCoreFields.Add("progressionRule");
        if (!item.TryGetProperty("thresholds", out _))
            missingCoreFields.Add("thresholds");

        if (missingCoreFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Canonical faction custom state не содержит обязательные корневые поля",
                code: "canonical_faction_custom_state_missing_required_fields",
                section: "Factions",
                expected: "identity, currentValue, minValue, maxValue, description, progressionRule, thresholds",
                actual: string.Join(", ", missingCoreFields),
                repairHint: "Для canonical faction_custom entries сохраняй полный Custom State Object, а не identity-only stub или partial delta."));
            return;
        }

        RequireNumberOrString(item, context, issues, "currentValue");
        RequireNumberOrString(item, context, issues, "minValue");
        RequireNumberOrString(item, context, issues, "maxValue");
        RequireString(item, context, issues, "description");

        if (!item.TryGetProperty("progressionRule", out var progressionRule) ||
            !RequireObject(progressionRule, $"{context}.progressionRule", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.progressionRule",
                IssueSeverity.Error,
                "Canonical faction custom state должен содержать progressionRule object",
                code: "canonical_faction_custom_state_missing_progression_rule",
                section: "Factions",
                expected: "progressionRule object with changePerTurn and description",
                actual: item.TryGetProperty("progressionRule", out var actualProgressionRule) ? actualProgressionRule.ValueKind.ToString() : "missing",
                repairHint: "Для canonical faction custom state сохраняй progressionRule object с changePerTurn и description, а не partial stub без правила прогрессии."));
        }
        else
        {
            RequireNumberOrString(progressionRule, $"{context}.progressionRule", issues, "changePerTurn");
            RequireString(progressionRule, $"{context}.progressionRule", issues, "description");
        }

        if (!TryGetArray(item, "thresholds", $"{context}.thresholds", issues, out var thresholds))
            return;

        var thresholdIndex = 0;
        foreach (var threshold in thresholds.EnumerateArray())
        {
            var thresholdContext = $"{context}.thresholds[{thresholdIndex++}]";
            if (!RequireObject(threshold, thresholdContext, issues))
                continue;

            RequireString(threshold, thresholdContext, issues, "levelName");
            RequireString(threshold, thresholdContext, issues, "triggerCondition");
            RequireNumberOrString(threshold, thresholdContext, issues, "triggerValue");

            if (!TryGetArray(threshold, "associatedEffects", $"{thresholdContext}.associatedEffects", issues, out var associatedEffects))
                continue;

            ValidateArrayItems(associatedEffects, $"{thresholdContext}.associatedEffects", issues, ValidateEffectObject);
        }
    }


    private void ValidateCanonicalFactionProjectsArray(JsonElement arr, string context, List<ValidationIssue> issues, bool completed)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateCanonicalFactionIdentity(item, itemContext, issues);
            var missingProjectFields = new List<string>();
            if (!HasNonEmptyString(item, "projectId"))
                missingProjectFields.Add("projectId");
            if (!HasAnyNonEmptyString(item, "projectName", "name"))
                missingProjectFields.Add("projectName/name");
            if (completed)
            {
                if (!HasNonEmptyString(item, "finalState"))
                    missingProjectFields.Add("finalState");
                if (!item.TryGetProperty("completionTurn", out _))
                    missingProjectFields.Add("completionTurn");
            }
            else
            {
                if (!HasNonEmptyString(item, "activeState"))
                    missingProjectFields.Add("activeState");
                if (!HasNonEmptyString(item, "description"))
                    missingProjectFields.Add("description");
                if (!item.TryGetProperty("totalResourceCost", out _))
                    missingProjectFields.Add("totalResourceCost");
                if (!item.TryGetProperty("resourcesSpent", out _))
                    missingProjectFields.Add("resourcesSpent");
                if (!item.TryGetProperty("totalTimeCostMinutes", out _))
                    missingProjectFields.Add("totalTimeCostMinutes");
                if (!item.TryGetProperty("timeSpentMinutes", out _))
                    missingProjectFields.Add("timeSpentMinutes");
                if (!item.TryGetProperty("totalSteps", out _))
                    missingProjectFields.Add("totalSteps");
                if (!item.TryGetProperty("currentStep", out _))
                    missingProjectFields.Add("currentStep");
            }

            if (missingProjectFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Canonical faction project entry не содержит обязательные корневые поля",
                    code: "canonical_faction_project_missing_required_fields",
                    section: "FactionProjects",
                    expected: completed
                        ? "projectId, projectName/name, finalState, completionTurn"
                        : "projectId, projectName/name, activeState, description, totalResourceCost, resourcesSpent, totalTimeCostMinutes, timeSpentMinutes, totalSteps, currentStep",
                    actual: string.Join(", ", missingProjectFields),
                    repairHint: "Для canonical faction_projects сохраняй полный project object с обязательными root fields, а не partial fragment."));
                continue;
            }

            RequireString(item, itemContext, issues, "projectId");
            if (!HasAnyNonEmptyString(item, "projectName", "name"))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Faction project должен содержать projectName или name"));
            }

            if (completed)
            {
                var finalState = RequireString(item, itemContext, issues, "finalState");
                ValidateNonNegativeIntegerField(item, itemContext, issues, "completionTurn", "FactionProjects");
                if (!string.IsNullOrWhiteSpace(finalState) &&
                    !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(finalState, "Abandoned", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.finalState",
                        IssueSeverity.Error,
                        "completedProjects[].finalState должен быть Completed или Abandoned",
                        code: "canonical_faction_project_invalid_final_state",
                        section: "FactionProjects",
                        expected: "Completed or Abandoned",
                        actual: finalState,
                        repairHint: "Для записи в completedProjects используй finalState = Completed или Abandoned и обязательный completionTurn."));
                }
            }
            else
            {
                var activeState = RequireString(item, itemContext, issues, "activeState");
                ValidateFactionFullProjectObject(item, itemContext, issues, completed: false);
                if (!string.IsNullOrWhiteSpace(activeState) &&
                    (string.Equals(activeState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(activeState, "Abandoned", StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.activeState",
                        IssueSeverity.Error,
                        "activeProjects[] не должен хранить terminal state Completed/Abandoned",
                        code: "canonical_faction_project_terminal_active_state_forbidden",
                        section: "FactionProjects",
                        expected: "Non-terminal activeState inside activeProjects[]",
                        actual: activeState,
                        repairHint: "Для завершённого или abandoned проекта используй completedProjects[] с finalState и completionTurn."));
                }
            }
        }
    }


    private void ValidateFactionResourceArray(JsonElement resources, string contextPrefix, List<ValidationIssue> issues, string propName, bool requireUpkeep)
    {
        if (!resources.TryGetProperty(propName, out var arr) || arr.ValueKind == JsonValueKind.Null)
            return;

        RequireArrayOfObjects(arr, $"{contextPrefix}.{propName}", issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "resourceName");
            RequireNonNegativeNumberField(item, itemContext, issues, "currentStockpile");
            RequireNonNegativeNumberField(item, itemContext, issues, "incomePerCycle");
            if (requireUpkeep)
                RequireNonNegativeNumberField(item, itemContext, issues, "upkeepPerCycle");
        }
    }


    private void ValidateFactionChronicles(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.TryGetProperty("entries", out var entries))
        {
            ValidateLooseStringOrObjectArray(entries, $"{contextPrefix}.entries", issues);
            return;
        }

        if (!TryGetArray(root, "factionChronicleUpdates", $"{contextPrefix}.factionChronicleUpdates", issues, out var arr))
            return;

        var knownPermanentFactionIds = CollectKnownPermanentFactionIds(root);
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.factionChronicleUpdates[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;

            var factionId = GetFirstNonEmptyString(item, "factionId");
            if (string.IsNullOrWhiteSpace(factionId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.factionId",
                    IssueSeverity.Error,
                    "factionChronicleUpdates требует factionId для канонической привязки записи к фракции",
                    code: "faction_chronicle_missing_faction_id",
                    section: "FactionChronicle",
                        expected: "non-empty factionId",
                        actual: "missing",
                        repairHint: "Для factionChronicleUpdates передавай factionId + entryToAppend. Одного factionName/name недостаточно: запись должна оставаться связанной с canonical faction state."));
            }
            else if (knownPermanentFactionIds.Count > 0 && !knownPermanentFactionIds.Contains(factionId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.factionId",
                    IssueSeverity.Error,
                    $"factionChronicleUpdates ссылается на factionId '{factionId}', которого нет в canonical faction state",
                    code: "faction_chronicle_unknown_faction_id",
                    section: "FactionChronicle",
                    expected: "existing permanent factionId from faction_core.json",
                    actual: factionId,
                    repairHint: "Для factionChronicleUpdates используй существующий permanent factionId из canonical faction_core state. Новую фракцию сначала создай/сохрани через faction_core, затем привязывай к ней chronicle entry."));
            }

            ValidateOptionalString(item, itemContext, issues, "factionName");
            var entryToAppend = RequireString(item, itemContext, issues, "entryToAppend");
            if (!string.IsNullOrWhiteSpace(entryToAppend) &&
                !CharacterChronicleEntryPrefixRegex.IsMatch(entryToAppend) &&
                !LegacyTurnPrefixedEntryRegex.IsMatch(entryToAppend))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.entryToAppend",
                    IssueSeverity.Error,
                    "factionChronicleUpdates.entryToAppend должен начинаться с допустимого turn prefix для хроники фракции",
                    code: "faction_chronicle_entry_prefix_invalid",
                    section: "FactionChronicle",
                    expected: "#[turn_number] - ... or #[turn_number]. ...",
                    actual: entryToAppend,
                    repairHint: "Для factionChronicleUpdates используй один из допустимых префиксов turn anchor: '#[turn_number] - ...' или более короткий '#[turn_number]. ...' по faction chronicle rules."));
            }
        }
    }
}
