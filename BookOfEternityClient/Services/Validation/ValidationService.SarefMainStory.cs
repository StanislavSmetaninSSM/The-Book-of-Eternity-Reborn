using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateSarefMainStoryStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                SarefMainStoryState.StatePath,
                IssueSeverity.Error,
                "main_story_saref_state.json должен быть JSON object.",
                code: "saref_main_story_invalid_root",
                section: "SarefMainStory",
                expected: "object with schemaVersion, revealStage, guardianQuestlines[], latentTraces[], sarefRevelations[], sarefAdvantages[]",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "SarefMainStory");

        var revealStage = RequireSarefString(root, contextPrefix, "revealStage", "saref_main_story_missing_reveal_stage", issues);
        if (!string.IsNullOrWhiteSpace(revealStage) && !SarefMainStoryState.RevealStages.Contains(revealStage))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.revealStage",
                "revealStage main story Сарефа не поддерживается.",
                "saref_main_story_invalid_reveal_stage",
                string.Join("/", SarefMainStoryState.RevealStages.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                revealStage);
        }

        ValidateSarefArray(root, contextPrefix, "guardianQuestlines", "guardianId", "saref_main_story_duplicate_guardian_questline", issues);
        ValidateSarefArray(root, contextPrefix, "latentTraces", "traceId", "saref_main_story_duplicate_latent_trace", issues);
        ValidateSarefRevelations(root, contextPrefix, issues, out var revealedCategories, out var revelationCount);
        ValidateSarefAdvantages(root, contextPrefix, issues, out var advantageCount);
        ValidateSarefArray(root, contextPrefix, "defeatOutcomes", "outcomeId", "saref_main_story_duplicate_defeat_outcome", issues);
        ValidateSarefArray(root, contextPrefix, "endings", "endingId", "saref_main_story_duplicate_ending", issues);
        var factionVisibility = ValidateSarefFactionLinks(root, contextPrefix, issues);
        ValidateSarefNullableStateObject(root, contextPrefix, "playerOathState", "state", SarefMainStoryState.PlayerOathStates, "saref_main_story_invalid_player_oath_state", issues);
        ValidateSarefNullableStateObject(root, contextPrefix, "sarefPersonalBond", "state", SarefMainStoryState.PersonalBondStates, "saref_main_story_invalid_personal_bond_state", issues);
        ValidateSarefNullableObject(root, contextPrefix, "wingsInfiltration", issues);
        ValidateSarefNullableObject(root, contextPrefix, "finalConfrontation", issues);
        ValidateSarefRevealStageInvariants(
            revealStage,
            revelationCount,
            advantageCount,
            revealedCategories,
            factionVisibility,
            contextPrefix,
            issues);
    }

    private static void ValidateSarefRevealStageInvariants(
        string? revealStage,
        int revelationCount,
        int advantageCount,
        HashSet<string> revealedCategories,
        string? factionVisibility,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(revealStage) || !SarefMainStoryState.RevealStages.Contains(revealStage))
            return;

        var noSpoilerStage =
            string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase);
        if (noSpoilerStage &&
            (revelationCount > 0 || advantageCount > 0 ||
             string.Equals(factionVisibility, "revealed", StringComparison.OrdinalIgnoreCase)))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.revealStage",
                "No-spoiler стадия Сарефа не может содержать раскрытые фрагменты, преимущества или раскрытую фракцию.",
                "saref_main_story_no_spoiler_stage_has_revealed_content",
                "unknown/shadow without sarefRevelations[], sarefAdvantages[], or factionLinks.visibility=revealed",
                revealStage);
        }

        if (StageAtLeast(revealStage, SarefMainStoryState.RevealStageNameRevealed) && revelationCount == 0)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.sarefRevelations",
                "Стадия name_revealed или выше требует хотя бы один canonical sarefRevelation.",
                "saref_main_story_revealed_stage_without_revelation",
                "sarefRevelations[] with sourceGuardianId/category/revealedAtTurn",
                "empty");
        }

        if (StageAtLeast(revealStage, SarefMainStoryState.RevealStageWingsRevealed) &&
            !HasWingsUnlockRoute(revealedCategories))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.sarefRevelations",
                "Нельзя перейти к Wings/final стадиям без достаточного маршрута раскрытия Крыльев Ангелов.",
                "saref_main_story_wings_stage_without_unlock_route",
                "all four mandatory categories, or 3 mandatory + 2 additional, or 2 mandatory + 4 additional",
                string.Join(", ", revealedCategories.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static bool StageAtLeast(string revealStage, string requiredStage) =>
        StageRank(revealStage) >= StageRank(requiredStage);

    private static int StageRank(string revealStage)
    {
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageNameRevealed, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageWingsRevealed, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageInfiltrationActive, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageConfrontationAvailable, StringComparison.OrdinalIgnoreCase))
            return 5;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageCompleted, StringComparison.OrdinalIgnoreCase))
            return 6;

        return 0;
    }

    private static bool HasWingsUnlockRoute(HashSet<string> categories)
    {
        var mandatoryCount = SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains);
        var additionalCount = categories.Count(category => !SarefMainStoryState.MandatoryWingsCategories.Contains(category));
        return mandatoryCount == 4 ||
               mandatoryCount >= 3 && additionalCount >= 2 ||
               mandatoryCount >= 2 && additionalCount >= 4;
    }

    private static void ValidateSarefRevelations(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        out HashSet<string> revealedCategories,
        out int revelationCount)
    {
        revealedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        revelationCount = 0;
        if (!TryGetRequiredSarefArray(root, contextPrefix, "sarefRevelations", issues, out var revelations))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in revelations.EnumerateArray())
        {
            var context = $"{contextPrefix}.sarefRevelations[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            revelationCount++;
            var revelationId = RequireSarefString(item, context, "revelationId", "saref_main_story_missing_revelation_id", issues);
            if (!string.IsNullOrWhiteSpace(revelationId) && !ids.Add(revelationId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат sarefRevelations[].revelationId.",
                    "saref_main_story_duplicate_revelation",
                    "unique revelationId",
                    revelationId);
            }

            var category = RequireSarefString(item, context, "category", "saref_main_story_missing_revelation_category", issues);
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!SarefMainStoryState.RevelationCategories.Contains(category))
                {
                    AddSarefIssue(
                        issues,
                        $"{context}.category",
                        "Категория sarefRevelation не поддерживается.",
                        "saref_main_story_invalid_revelation_category",
                        string.Join("/", SarefMainStoryState.RevelationCategories.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                        category);
                }
                else
                {
                    revealedCategories.Add(category);
                }
            }

            RequireSarefString(item, context, "sourceGuardianId", "saref_main_story_missing_revelation_source", issues);
            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static void ValidateSarefAdvantages(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        out int advantageCount)
    {
        advantageCount = 0;
        if (!TryGetRequiredSarefArray(root, contextPrefix, "sarefAdvantages", issues, out var advantages))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in advantages.EnumerateArray())
        {
            var context = $"{contextPrefix}.sarefAdvantages[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            advantageCount++;
            var advantageId = RequireSarefString(item, context, "advantageId", "saref_main_story_missing_advantage_id", issues);
            if (!string.IsNullOrWhiteSpace(advantageId) && !ids.Add(advantageId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат sarefAdvantages[].advantageId.",
                    "saref_main_story_duplicate_advantage",
                    "unique advantageId",
                    advantageId);
            }

            var state = RequireSarefString(item, context, "state", "saref_main_story_missing_advantage_state", issues);
            if (!string.IsNullOrWhiteSpace(state) && !SarefMainStoryState.AdvantageStates.Contains(state))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.state",
                    "Состояние преимущества против Сарефа не поддерживается.",
                    "saref_main_story_invalid_advantage_state",
                    string.Join("/", SarefMainStoryState.AdvantageStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    state);
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static void ValidateSarefArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        string idProperty,
        string duplicateCode,
        List<ValidationIssue> issues)
    {
        if (!TryGetRequiredSarefArray(root, contextPrefix, propertyName, issues, out var array))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var context = $"{contextPrefix}.{propertyName}[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            if (item.TryGetProperty(idProperty, out var idNode) &&
                TryGetSarefString(idNode, out var id) &&
                !string.IsNullOrWhiteSpace(id) &&
                !ids.Add(id))
            {
                AddSarefIssue(
                    issues,
                    context,
                    $"Дубликат {propertyName}[].{idProperty}.",
                    duplicateCode,
                    $"unique {idProperty}",
                    id);
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static string? ValidateSarefFactionLinks(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("factionLinks", out var factionLinks))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks",
                "main_story_saref_state.json должен содержать factionLinks object.",
                "saref_main_story_missing_required_property",
                "factionLinks object",
                "missing");
            return null;
        }

        if (factionLinks.ValueKind != JsonValueKind.Object)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks",
                "factionLinks должен быть object.",
                "saref_main_story_object_or_null_not_object",
                "object",
                factionLinks.ValueKind.ToString());
            return null;
        }

        if (!factionLinks.TryGetProperty("visibility", out var visibilityNode) ||
            !TryGetSarefString(visibilityNode, out var visibility))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks.visibility",
                "factionLinks.visibility должен быть непустой строкой.",
                "saref_main_story_missing_faction_visibility",
                string.Join("/", SarefMainStoryState.FactionVisibilityStates),
                "missing");
            return null;
        }

        if (!SarefMainStoryState.FactionVisibilityStates.Contains(visibility))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks.visibility",
                "factionLinks.visibility не поддерживается.",
                "saref_main_story_invalid_faction_visibility",
                string.Join("/", SarefMainStoryState.FactionVisibilityStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                visibility);
        }

        return visibility;
    }

    private static void ValidateSarefNullableStateObject(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        string stateProperty,
        HashSet<string> allowedStates,
        string invalidCode,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind == JsonValueKind.Null)
            return;

        string? state = null;
        if (node.ValueKind == JsonValueKind.String)
        {
            state = node.GetString();
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            state = RequireSarefString(node, $"{contextPrefix}.{propertyName}", stateProperty, $"saref_main_story_missing_{propertyName}", issues);
            ValidateSarefTurnFields(node, $"{contextPrefix}.{propertyName}", issues);
        }
        else
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть null, string или object.",
                "saref_main_story_object_or_null_not_object",
                "null/string/object",
                node.ValueKind.ToString());
        }

        if (!string.IsNullOrWhiteSpace(state) && !allowedStates.Contains(state))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}.{stateProperty}",
                $"{propertyName}.{stateProperty} не поддерживается.",
                invalidCode,
                string.Join("/", allowedStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                state);
        }
    }

    private static void ValidateSarefNullableObject(JsonElement root, string contextPrefix, string propertyName, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind == JsonValueKind.Null)
            return;

        if (node.ValueKind != JsonValueKind.Object)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть null или object.",
                "saref_main_story_object_or_null_not_object",
                "null/object",
                node.ValueKind.ToString());
            return;
        }

        ValidateSarefTurnFields(node, $"{contextPrefix}.{propertyName}", issues);
    }

    private static bool TryGetRequiredSarefArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        List<ValidationIssue> issues,
        out JsonElement array)
    {
        array = default;
        if (!root.TryGetProperty(propertyName, out var node))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"main_story_saref_state.json должен содержать {propertyName}[].",
                "saref_main_story_missing_required_property",
                $"{propertyName}[]",
                "missing");
            return false;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом.",
                "saref_main_story_array_not_array",
                "array",
                node.ValueKind.ToString());
            return false;
        }

        array = node;
        return true;
    }

    private static bool ValidateSarefArrayObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (item.ValueKind == JsonValueKind.Object)
            return true;

        AddSarefIssue(
            issues,
            context,
            "Элемент массива main_story_saref_state.json должен быть object.",
            "saref_main_story_entry_not_object",
            "object",
            item.ValueKind.ToString());
        return false;
    }

    private static void ValidateSarefTurnFields(JsonElement item, string context, List<ValidationIssue> issues)
    {
        foreach (var fieldName in new[]
                 {
                     "createdAtTurn", "updatedAtTurn", "recognizedAtTurn", "revealedAtTurn", "unlockedAtTurn",
                     "spentAtTurn", "disabledAtTurn", "suppressedAtTurn", "startedAtTurn", "completedAtTurn",
                     "resolvedAtTurn", "defeatedAtTurn", "endedAtTurn"
                 })
        {
            if (!item.TryGetProperty(fieldName, out var value))
                continue;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var turn))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.{fieldName}",
                    "Turn-поля main_story_saref_state.json должны быть целыми числами.",
                    "saref_main_story_invalid_turn",
                    "integer >= 0",
                    value.ValueKind.ToString());
            }
            else if (turn < 0)
            {
                AddSarefIssue(
                    issues,
                    $"{context}.{fieldName}",
                    "Turn-поля main_story_saref_state.json не могут быть отрицательными.",
                    "saref_main_story_negative_turn",
                    "integer >= 0",
                    turn.ToString());
            }
        }
    }

    private static string? RequireSarefString(
        JsonElement root,
        string context,
        string propertyName,
        string missingCode,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(propertyName, out var node) &&
            TryGetSarefString(node, out var value))
        {
            return value;
        }

        AddSarefIssue(
            issues,
            $"{context}.{propertyName}",
            $"{propertyName} должен быть непустой строкой.",
            missingCode,
            "non-empty string",
            root.TryGetProperty(propertyName, out var present) ? present.ValueKind.ToString() : "missing");
        return null;
    }

    private static bool TryGetSarefString(JsonElement node, out string value)
    {
        value = string.Empty;
        if (node.ValueKind != JsonValueKind.String)
            return false;

        value = node.GetString()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void AddSarefIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string? expected = null,
        string? actual = null)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "SarefMainStory",
            expected: expected,
            actual: actual,
            repairHint: "Исправь game_state/meta/main_story_saref_state.json по контракту Крыльев над Бездной; не повышай revealStage без canonical evidence."));
    }
}
