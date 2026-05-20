using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private sealed record SarefUnlockReference(
        string Id,
        string? SourceGuardianId,
        int SourceQuestOrdinal,
        string Context);

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

        var guardianQuestlines = ValidateSarefGuardianQuestlines(root, contextPrefix, issues);
        ValidateSarefLatentTraces(root, contextPrefix, issues);
        ValidateSarefRevelations(root, contextPrefix, issues, out var revealedCategories, out var revelationCount, out var questFourRevelations);
        ValidateSarefAdvantages(root, contextPrefix, issues, out var advantageCount, out var questFourAdvantages);
        ValidateSarefQuestFourUnlockLinks(guardianQuestlines, questFourRevelations, questFourAdvantages, contextPrefix, issues);
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

    private static Dictionary<string, Dictionary<int, string>> ValidateSarefGuardianQuestlines(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetRequiredSarefArray(root, contextPrefix, "guardianQuestlines", issues, out var questlines))
            return result;

        var guardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in questlines.EnumerateArray())
        {
            var context = $"{contextPrefix}.guardianQuestlines[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            var guardianId = RequireSarefString(item, context, "guardianId", "saref_main_story_missing_guardian_questline_guardian_id", issues);
            if (!string.IsNullOrWhiteSpace(guardianId) && !guardianIds.Add(guardianId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат guardianQuestlines[].guardianId.",
                    "saref_main_story_duplicate_guardian_questline",
                    "unique guardianId",
                    guardianId);
            }

            ValidateSarefTurnFields(item, context, issues);
            var states = ValidateSarefQuestStates(item, context, issues);
            if (!string.IsNullOrWhiteSpace(guardianId))
                result[guardianId] = states;
        }

        return result;
    }

    private static Dictionary<int, string> ValidateSarefQuestStates(
        JsonElement questline,
        string context,
        List<ValidationIssue> issues)
    {
        var states = new Dictionary<int, string>();
        if (!questline.TryGetProperty("questStates", out var questStates))
        {
            AddSarefIssue(
                issues,
                $"{context}.questStates",
                "guardianQuestlines[] должен хранить questStates[] с questOrdinal 1..4.",
                "saref_main_story_missing_quest_states",
                "questStates[]",
                "missing");
            return states;
        }

        if (questStates.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{context}.questStates",
                "guardianQuestlines[].questStates должен быть массивом.",
                "saref_main_story_array_not_array",
                "array",
                questStates.ValueKind.ToString());
            return states;
        }

        var index = 0;
        foreach (var item in questStates.EnumerateArray())
        {
            var itemContext = $"{context}.questStates[{index++}]";
            if (!ValidateSarefArrayObject(item, itemContext, issues))
                continue;

            if (ContainsForbiddenSarefPhysicalEvidenceField(item))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Квесты Сарефа не могут переносить физические mortal предметы; используй memory/image/echo/proof.",
                    "saref_main_story_physical_mortal_item_evidence",
                    "memoryImprint/itemEcho/lifeEventEvidence/locationWitness/knowledgeTrace/soulResonance without physical transfer fields",
                    "physical mortal item field");
            }

            var questOrdinal = RequireSarefQuestOrdinal(item, itemContext, issues);
            var status = RequireSarefString(item, itemContext, "status", "saref_main_story_missing_quest_status", issues);
            if (!string.IsNullOrWhiteSpace(status) && !SarefMainStoryState.QuestProgressStates.Contains(status))
            {
                AddSarefIssue(
                    issues,
                    $"{itemContext}.status",
                    "Saref guardian quest status не поддерживается.",
                    "saref_main_story_invalid_quest_status",
                    string.Join("/", SarefMainStoryState.QuestProgressStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    status);
            }

            ValidateSarefTurnFields(item, itemContext, issues);
            if (questOrdinal is < 1 or > 4 || string.IsNullOrWhiteSpace(status) ||
                !SarefMainStoryState.QuestProgressStates.Contains(status))
            {
                continue;
            }

            if (states.ContainsKey(questOrdinal))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Дубликат questStates[].questOrdinal внутри guardianQuestline.",
                    "saref_main_story_duplicate_quest_ordinal",
                    "unique questOrdinal 1..4",
                    questOrdinal.ToString());
                continue;
            }

            states[questOrdinal] = status;
        }

        foreach (var (ordinal, status) in states.OrderBy(entry => entry.Key))
        {
            if (!string.Equals(status, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
                continue;

            for (var requiredOrdinal = 1; requiredOrdinal < ordinal; requiredOrdinal++)
            {
                if (states.TryGetValue(requiredOrdinal, out var priorStatus) &&
                    string.Equals(priorStatus, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddSarefIssue(
                    issues,
                    $"{context}.questStates",
                    "Официальное восстановление памяти Сарефа должно закрываться по порядку 1 -> 2 -> 3 -> 4.",
                    "saref_main_story_questline_out_of_order",
                    $"quest {requiredOrdinal} completed before quest {ordinal}",
                    $"quest {ordinal}=completed");
                break;
            }
        }

        return states;
    }

    private static void ValidateSarefLatentTraces(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetRequiredSarefArray(root, contextPrefix, "latentTraces", issues, out var traces))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in traces.EnumerateArray())
        {
            var context = $"{contextPrefix}.latentTraces[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            if (ContainsForbiddenSarefPhysicalEvidenceField(item))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Latent trace Сарефа не может хранить физический mortal предмет.",
                    "saref_main_story_physical_mortal_item_evidence",
                    "non-physical memory/image/echo/proof",
                    "physical mortal item field");
            }

            var traceId = RequireSarefString(item, context, "traceId", "saref_main_story_missing_trace_id", issues);
            if (!string.IsNullOrWhiteSpace(traceId) && !ids.Add(traceId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат latentTraces[].traceId.",
                    "saref_main_story_duplicate_latent_trace",
                    "unique traceId",
                    traceId);
            }

            if (item.TryGetProperty("questOrdinal", out _))
                RequireSarefQuestOrdinal(item, context, issues);

            if (item.TryGetProperty("status", out var statusNode) && TryGetSarefString(statusNode, out var status) &&
                !SarefMainStoryState.LatentTraceStates.Contains(status))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.status",
                    "latentTraces[].status может быть только latent или recognized.",
                    "saref_main_story_invalid_latent_trace_status",
                    string.Join("/", SarefMainStoryState.LatentTraceStates),
                    status);
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static void ValidateSarefQuestFourUnlockLinks(
        IReadOnlyDictionary<string, Dictionary<int, string>> guardianQuestlines,
        IReadOnlyList<SarefUnlockReference> questFourRevelations,
        IReadOnlyList<SarefUnlockReference> questFourAdvantages,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        var revelationGuardians = questFourRevelations
            .Where(reference => !string.IsNullOrWhiteSpace(reference.SourceGuardianId))
            .Select(reference => reference.SourceGuardianId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var advantageGuardians = questFourAdvantages
            .Where(reference => !string.IsNullOrWhiteSpace(reference.SourceGuardianId))
            .Select(reference => reference.SourceGuardianId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in questFourRevelations)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(guardianQuestlines, reference.SourceGuardianId))
            {
                AddSarefIssue(
                    issues,
                    reference.Context,
                    "sarefRevelation из 4-го квеста требует завершенные квесты 1-4 этого Хранителя.",
                    "saref_main_story_revelation_without_questline_completion",
                    "guardianQuestlines[].questStates 1..4 completed for sourceGuardianId",
                    reference.SourceGuardianId ?? "missing");
            }
        }

        foreach (var reference in questFourAdvantages)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(guardianQuestlines, reference.SourceGuardianId))
            {
                AddSarefIssue(
                    issues,
                    reference.Context,
                    "sarefAdvantage из 4-го квеста требует завершенные квесты 1-4 этого Хранителя.",
                    "saref_main_story_advantage_without_questline_completion",
                    "guardianQuestlines[].questStates 1..4 completed for sourceGuardianId",
                    reference.SourceGuardianId ?? "missing");
            }
        }

        foreach (var (guardianId, states) in guardianQuestlines)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(states))
                continue;

            if (!revelationGuardians.Contains(guardianId))
            {
                AddSarefIssue(
                    issues,
                    $"{contextPrefix}.sarefRevelations",
                    "Завершенный 4-й квест Хранителя должен открыть canonical sarefRevelation.",
                    "saref_main_story_completed_quest_four_missing_revelation",
                    $"sarefRevelations[] with sourceGuardianId={guardianId} and sourceQuestOrdinal=4",
                    "missing");
            }

            if (!advantageGuardians.Contains(guardianId))
            {
                AddSarefIssue(
                    issues,
                    $"{contextPrefix}.sarefAdvantages",
                    "Завершенный 4-й квест Хранителя должен открыть canonical sarefAdvantage.",
                    "saref_main_story_completed_quest_four_missing_advantage",
                    $"sarefAdvantages[] with sourceGuardianId={guardianId} and sourceQuestOrdinal=4",
                    "missing");
            }
        }
    }

    private static bool HasCompletedSarefQuestlineThroughQuestFour(
        IReadOnlyDictionary<string, Dictionary<int, string>> questlines,
        string? guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId) || !questlines.TryGetValue(guardianId, out var states))
            return false;

        return HasCompletedSarefQuestlineThroughQuestFour(states);
    }

    private static bool HasCompletedSarefQuestlineThroughQuestFour(IReadOnlyDictionary<int, string> states)
    {
        for (var ordinal = 1; ordinal <= 4; ordinal++)
        {
            if (!states.TryGetValue(ordinal, out var status) ||
                !string.Equals(status, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
        out int revelationCount,
        out List<SarefUnlockReference> questFourRevelations)
    {
        revealedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        revelationCount = 0;
        questFourRevelations = new List<SarefUnlockReference>();
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

            var sourceGuardianId = RequireSarefString(item, context, "sourceGuardianId", "saref_main_story_missing_revelation_source", issues);
            var sourceQuestOrdinal = ResolveSarefSourceQuestOrdinal(item, context, issues);
            if (!string.IsNullOrWhiteSpace(sourceGuardianId))
            {
                if (sourceQuestOrdinal == 4)
                {
                    questFourRevelations.Add(new SarefUnlockReference(revelationId ?? string.Empty, sourceGuardianId, sourceQuestOrdinal, context));
                }
                else
                {
                    AddSarefIssue(
                        issues,
                        context,
                        "Canonical sarefRevelation от Хранителя может открываться только 4-м квестом.",
                        "saref_main_story_revelation_not_from_quest_four",
                        "sourceQuestOrdinal=4 or sourceQuestId ending with q4",
                        sourceQuestOrdinal <= 0 ? "missing" : sourceQuestOrdinal.ToString());
                }
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static void ValidateSarefAdvantages(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        out int advantageCount,
        out List<SarefUnlockReference> questFourAdvantages)
    {
        advantageCount = 0;
        questFourAdvantages = new List<SarefUnlockReference>();
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

            var sourceGuardianId = GetSarefOptionalString(item, "sourceGuardianId");
            var sourceQuestOrdinal = ResolveSarefSourceQuestOrdinal(item, context, issues);
            if (!string.IsNullOrWhiteSpace(sourceGuardianId))
            {
                if (sourceQuestOrdinal == 4)
                {
                    questFourAdvantages.Add(new SarefUnlockReference(advantageId ?? string.Empty, sourceGuardianId, sourceQuestOrdinal, context));
                }
                else
                {
                    AddSarefIssue(
                        issues,
                        context,
                        "Canonical sarefAdvantage от Хранителя может открываться только 4-м квестом.",
                        "saref_main_story_advantage_not_from_quest_four",
                        "sourceQuestOrdinal=4 or sourceQuestId ending with q4",
                        sourceQuestOrdinal <= 0 ? "missing" : sourceQuestOrdinal.ToString());
                }
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

    private static int RequireSarefQuestOrdinal(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("questOrdinal", out var node))
        {
            AddSarefIssue(
                issues,
                $"{context}.questOrdinal",
                "questOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_missing_quest_ordinal",
                "integer 1..4",
                "missing");
            return 0;
        }

        if (node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out var ordinal) || ordinal is < 1 or > 4)
        {
            AddSarefIssue(
                issues,
                $"{context}.questOrdinal",
                "questOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_invalid_quest_ordinal",
                "integer 1..4",
                node.ValueKind == JsonValueKind.Number ? node.ToString() : node.ValueKind.ToString());
            return 0;
        }

        return ordinal;
    }

    private static int ResolveSarefSourceQuestOrdinal(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (item.TryGetProperty("sourceQuestOrdinal", out var ordinalNode))
        {
            if (ordinalNode.ValueKind == JsonValueKind.Number &&
                ordinalNode.TryGetInt32(out var ordinal) &&
                ordinal is >= 1 and <= 4)
            {
                return ordinal;
            }

            AddSarefIssue(
                issues,
                $"{context}.sourceQuestOrdinal",
                "sourceQuestOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_invalid_source_quest_ordinal",
                "integer 1..4",
                ordinalNode.ValueKind == JsonValueKind.Number ? ordinalNode.ToString() : ordinalNode.ValueKind.ToString());
            return 0;
        }

        var sourceQuestId = GetSarefOptionalString(item, "sourceQuestId");
        if (string.IsNullOrWhiteSpace(sourceQuestId))
            return 0;

        var trimmed = sourceQuestId.Trim();
        for (var ordinal = 1; ordinal <= 4; ordinal++)
        {
            if (trimmed.EndsWith($"q{ordinal}", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith($"quest_{ordinal}", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith($"quest{ordinal}", StringComparison.OrdinalIgnoreCase))
            {
                return ordinal;
            }
        }

        return 0;
    }

    private static bool ContainsForbiddenSarefPhysicalEvidenceField(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (GuardianProjectState.IsForbiddenQuestPhysicalEvidenceField(property.Name) ||
                    ContainsForbiddenSarefPhysicalEvidenceField(property.Value))
                {
                    return true;
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                if (ContainsForbiddenSarefPhysicalEvidenceField(item))
                    return true;
            }
        }

        return false;
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

    private static string? GetSarefOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || !TryGetSarefString(node, out var value))
            return null;

        return value;
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
