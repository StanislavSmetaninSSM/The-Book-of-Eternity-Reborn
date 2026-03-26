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
    private Dictionary<string, GuardianSequentialState> CollectKnownGuardianSequentialStatesForCommandValidation()
    {
        var states = new Dictionary<string, GuardianSequentialState>(StringComparer.OrdinalIgnoreCase);
        var preTurnGuardiansJson = ReadPreTurnTrackedFileSync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(preTurnGuardiansJson))
            return states;

        try
        {
            using var doc = JsonDocument.Parse(preTurnGuardiansJson);
            CollectGuardianSequentialStatesFromRoot(doc.RootElement, states);
        }
        catch
        {
            // ignored: higher-level JSON integrity validation already covers malformed pre-turn state
        }

        return states;
    }


    private static void CollectGuardianSequentialStatesFromRoot(JsonElement root, Dictionary<string, GuardianSequentialState> states)
    {
        if (root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var guardian in guardians.EnumerateArray())
                MergeGuardianSequentialState(states, guardian);
        }

        if (root.TryGetProperty("activeGuardian", out var activeGuardian) && activeGuardian.ValueKind == JsonValueKind.Object)
            MergeGuardianSequentialState(states, activeGuardian);
    }


    private static void MergeGuardianSequentialState(Dictionary<string, GuardianSequentialState> states, JsonElement guardian)
    {
        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(guardianId))
            return;

        states[guardianId] = ParseGuardianSequentialState(guardian);
    }


    private static GuardianSequentialState ParseGuardianSequentialState(JsonElement guardian)
    {
        int? currentReputation = null;
        if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
            relationshipData.ValueKind == JsonValueKind.Object &&
            relationshipData.TryGetProperty("currentReputation", out var currentReputationNode) &&
            currentReputationNode.ValueKind == JsonValueKind.Number &&
            currentReputationNode.TryGetInt32(out var parsedReputation))
        {
            currentReputation = parsedReputation;
        }

        var availableQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var questDifficultyById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guardian.TryGetProperty("questManagement", out var questManagement) &&
            questManagement.ValueKind == JsonValueKind.Object)
        {
            CollectQuestIdsFromGuardianQuestArray(questManagement, "availableQuests", availableQuestIds, questDifficultyById);
            CollectQuestIdsFromGuardianQuestArray(questManagement, "activeQuests", activeQuestIds, questDifficultyById);
        }

        var chargesUsedThisReturn = 0;
        if (guardian.TryGetProperty("gachaSystem", out var gachaSystem) &&
            gachaSystem.ValueKind == JsonValueKind.Object &&
            gachaSystem.TryGetProperty("chargesUsedThisReturn", out var chargesUsedNode) &&
            chargesUsedNode.ValueKind == JsonValueKind.Number &&
            chargesUsedNode.TryGetInt32(out var parsedChargesUsed))
        {
            chargesUsedThisReturn = Math.Max(0, parsedChargesUsed);
        }

        var state = new GuardianSequentialState
        {
            CurrentReputation = currentReputation,
            CurrentAbodePower = AbodePowerRules.GetCurrentPower(guardian),
            ChargesUsedThisReturn = chargesUsedThisReturn
        };
        state.AvailableQuestIds.UnionWith(availableQuestIds);
        state.ActiveQuestIds.UnionWith(activeQuestIds);
        foreach (var (questId, difficulty) in questDifficultyById)
            state.QuestDifficultyById[questId] = difficulty;
        return state;
    }


    private static void CollectQuestIdsFromGuardianQuestArray(
        JsonElement questManagement,
        string propName,
        HashSet<string> target,
        Dictionary<string, string>? difficultyById = null)
    {
        if (!questManagement.TryGetProperty(propName, out var questArray) || questArray.ValueKind != JsonValueKind.Array)
            return;

        foreach (var quest in questArray.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object)
                continue;

            var questId = GetFirstNonEmptyString(quest, "questId");
            if (!string.IsNullOrWhiteSpace(questId))
            {
                target.Add(questId);
                if (difficultyById != null)
                    difficultyById[questId] = NormalizeGuardianQuestDifficulty(GetFirstNonEmptyString(quest, "difficulty"));
            }
        }
    }


    private static string NormalizeGuardianQuestDifficulty(string? difficulty) =>
        AbodePowerRules.NormalizeGuardianQuestDifficulty(difficulty);


    private static void ValidateGuardianAvailableQuestDifficultyCeiling(
        JsonElement availableQuests,
        string questArrayContext,
        string difficultyCeiling,
        List<ValidationIssue> issues)
    {
        if (availableQuests.ValueKind != JsonValueKind.Array)
            return;

        var allowedCeiling = NormalizeGuardianQuestDifficulty(difficultyCeiling);
        var allowedCeilingRank = AbodePowerRules.GetGuardianQuestDifficultyRank(allowedCeiling);
        var questIndex = 0;
        foreach (var quest in availableQuests.EnumerateArray())
        {
            var questContext = $"{questArrayContext}[{questIndex++}]";
            if (!RequireObject(quest, questContext, issues))
                continue;

            var difficulty = GetFirstNonEmptyString(quest, "difficulty");
            if (string.IsNullOrWhiteSpace(difficulty))
                continue;

            var normalizedDifficulty = NormalizeGuardianQuestDifficulty(difficulty);
            var difficultyRank = AbodePowerRules.GetGuardianQuestDifficultyRank(normalizedDifficulty);
            if (difficultyRank <= allowedCeilingRank)
                continue;

            issues.Add(new ValidationIssue(
                $"{questContext}.difficulty",
                IssueSeverity.Error,
                "Guardian availableQuests превышает потолок сложности, разрешённый текущей силой Обители",
                code: "guardian_available_quest_difficulty_ceiling_exceeded",
                section: "Guardians",
                expected: $"difficulty at or below {allowedCeiling}",
                actual: normalizedDifficulty,
                repairHint: "Для availableQuests держи difficulty не выше shared guardian quest difficulty ceiling от текущей силы Обители. Более сильные квесты могут оставаться только уже активными или историческими."));
        }
    }


    private void ValidateGuardianQuestPowerAudit(
        JsonElement item,
        string itemContext,
        string guardianId,
        string questId,
        string outcome,
        GuardianSequentialState? guardianState,
        List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("questPowerAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit",
                IssueSeverity.Error,
                "completeQuest обязан содержать questPowerAudit для детерминированного изменения Силы Обители",
                code: "guardian_complete_quest_missing_power_audit",
                section: "UpdateGuardians.completeQuest",
                expected: "questPowerAudit object",
                actual: item.TryGetProperty("questPowerAudit", out var actualAudit) ? actualAudit.ValueKind.ToString() : "missing",
                repairHint: "Добавь questPowerAudit с questDifficultyTier, outcome, baseDelta, bonusDelta и finalDelta. Не меняй Силу Обители guardian quest-ом без audit trail."));
            return;
        }

        var difficultyTier = NormalizeGuardianQuestDifficulty(RequireString(audit, $"{itemContext}.questPowerAudit", issues, "questDifficultyTier"));
        var auditOutcome = RequireString(audit, $"{itemContext}.questPowerAudit", issues, "outcome");
        ValidateIntegerField(audit, $"{itemContext}.questPowerAudit", issues, "baseDelta");
        ValidateIntegerField(audit, $"{itemContext}.questPowerAudit", issues, "bonusDelta");
        ValidateIntegerField(audit, $"{itemContext}.questPowerAudit", issues, "finalDelta");

        var supportsCurrentProject = GetBoolean(audit, "supportsCurrentProject", false);
        var defendsAgainstRivalPressure = GetBoolean(audit, "defendsAgainstRivalPressure", false);
        var baseDelta = GetIntOrDefault(audit, "baseDelta");
        var bonusDelta = GetIntOrDefault(audit, "bonusDelta");
        var finalDelta = GetIntOrDefault(audit, "finalDelta");

        if (!string.IsNullOrWhiteSpace(auditOutcome) &&
            !string.Equals(auditOutcome, outcome, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit.outcome",
                IssueSeverity.Error,
                "questPowerAudit.outcome должен совпадать с completeQuest.outcome",
                code: "guardian_complete_quest_power_audit_outcome_mismatch",
                section: "UpdateGuardians.completeQuest",
                expected: outcome,
                actual: auditOutcome,
                repairHint: "Синхронизируй audit с фактическим outcome completeQuest."));
        }

        if (guardianState != null &&
            !string.IsNullOrWhiteSpace(questId) &&
            guardianState.QuestDifficultyById.TryGetValue(questId, out var canonicalDifficulty) &&
            !string.Equals(canonicalDifficulty, difficultyTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit.questDifficultyTier",
                IssueSeverity.Error,
                "questPowerAudit.questDifficultyTier должен совпадать с canonical difficulty этого guardian quest",
                code: "guardian_complete_quest_power_audit_difficulty_mismatch",
                section: "UpdateGuardians.completeQuest",
                expected: canonicalDifficulty,
                actual: difficultyTier,
                repairHint: "Используй difficulty из canonical questManagement этого Хранителя, а не произвольную сложность."));
        }

        var expectedBase = AbodePowerRules.ResolveGuardianQuestBasePowerDelta(difficultyTier, outcome);
        var expectedBonus = AbodePowerRules.ResolveGuardianQuestBonusPowerDelta(expectedBase, supportsCurrentProject, defendsAgainstRivalPressure);
        var expectedFinal = expectedBase + expectedBonus;
        if (baseDelta != expectedBase)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit.baseDelta",
                IssueSeverity.Error,
                "questPowerAudit.baseDelta не совпадает с формулой сложности и outcome",
                code: "guardian_complete_quest_power_audit_base_mismatch",
                section: "UpdateGuardians.completeQuest",
                expected: expectedBase.ToString(),
                actual: baseDelta.ToString(),
                repairHint: "Следуй формуле силы Обители для guardian quest difficulty/outcome."));
        }

        if (bonusDelta != expectedBonus)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit.bonusDelta",
                IssueSeverity.Error,
                "questPowerAudit.bonusDelta не совпадает с формулой supportsCurrentProject/defendsAgainstRivalPressure",
                code: "guardian_complete_quest_power_audit_bonus_mismatch",
                section: "UpdateGuardians.completeQuest",
                expected: expectedBonus.ToString(),
                actual: bonusDelta.ToString(),
                repairHint: "Бонус к силе Обители для guardian quest должен считаться детерминированно."));
        }

        if (finalDelta != expectedFinal)
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.questPowerAudit.finalDelta",
                IssueSeverity.Error,
                "questPowerAudit.finalDelta должен быть равен baseDelta + bonusDelta",
                code: "guardian_complete_quest_power_audit_final_mismatch",
                section: "UpdateGuardians.completeQuest",
                expected: expectedFinal.ToString(),
                actual: finalDelta.ToString(),
                repairHint: "Не подбирай finalDelta вручную; используй детерминированную формулу guardian quest power audit."));
        }
    }


    private void ValidateGuardianGachaBonusAudit(
        JsonElement item,
        string itemContext,
        string guardianId,
        string? baseRarity,
        string? finalRarity,
        int currentPower,
        List<ValidationIssue> issues)
    {
        var baseRank = GetRarityRank(baseRarity);
        var finalRank = GetRarityRank(finalRarity);
        var finalUpgradeSteps = Math.Max(0, finalRank - baseRank);
        var hasAudit = item.TryGetProperty("gachaBonusAudit", out var audit) && audit.ValueKind == JsonValueKind.Object;

        if (!hasAudit)
        {
            if (finalUpgradeSteps > 0)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.gachaBonusAudit",
                    IssueSeverity.Error,
                    "processGacha с повышением редкости обязан содержать gachaBonusAudit",
                    code: "guardian_process_gacha_missing_bonus_audit",
                    section: "UpdateGuardians.processGacha",
                    repairHint: "Добавь gachaBonusAudit с baseRarity, abodePowerBonusSteps, relicForgingBonusSteps, finalRarity и optional sourceProjectId."));
            }
            return;
        }

        var auditContext = $"{itemContext}.gachaBonusAudit";
        var auditBaseRarity = RequireString(audit, auditContext, issues, "baseRarity");
        var auditFinalRarity = RequireString(audit, auditContext, issues, "finalRarity");
        ValidateNonNegativeIntegerField(audit, auditContext, issues, "abodePowerBonusSteps", "UpdateGuardians.processGacha");
        ValidateNonNegativeIntegerField(audit, auditContext, issues, "relicForgingBonusSteps", "UpdateGuardians.processGacha");
        ValidateOptionalString(audit, auditContext, issues, "sourceProjectId");

        var abodePowerBonusSteps = GetIntOrDefault(audit, "abodePowerBonusSteps");
        var relicForgingBonusSteps = GetIntOrDefault(audit, "relicForgingBonusSteps");
        var sourceProjectId = GetFirstNonEmptyString(audit, "sourceProjectId");
        var maxAbodeSteps = AbodePowerRules.GetGuardianRarityCeilingBonusSteps(currentPower);

        if (!string.IsNullOrWhiteSpace(baseRarity) &&
            !string.Equals(baseRarity, auditBaseRarity, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.baseRarity",
                IssueSeverity.Error,
                "gachaBonusAudit.baseRarity должен совпадать с client-computed gacha base rarity",
                code: "guardian_process_gacha_bonus_audit_base_rarity_mismatch",
                section: "UpdateGuardians.processGacha",
                expected: baseRarity,
                actual: auditBaseRarity));
        }

        if (!string.IsNullOrWhiteSpace(finalRarity) &&
            !string.Equals(finalRarity, auditFinalRarity, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.finalRarity",
                IssueSeverity.Error,
                "gachaBonusAudit.finalRarity должен совпадать с result.rarity",
                code: "guardian_process_gacha_bonus_audit_final_rarity_mismatch",
                section: "UpdateGuardians.processGacha",
                expected: finalRarity,
                actual: auditFinalRarity));
        }

        if (abodePowerBonusSteps > maxAbodeSteps)
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.abodePowerBonusSteps",
                IssueSeverity.Error,
                "gachaBonusAudit.abodePowerBonusSteps превышает derived ceiling от current abode power",
                code: "guardian_process_gacha_bonus_audit_abode_steps_exceeded",
                section: "UpdateGuardians.processGacha",
                expected: $"0..{maxAbodeSteps}",
                actual: abodePowerBonusSteps.ToString()));
        }

        if (relicForgingBonusSteps > 0 && string.IsNullOrWhiteSpace(sourceProjectId))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.sourceProjectId",
                IssueSeverity.Error,
                "При relicForgingBonusSteps > 0 нужно указать sourceProjectId",
                code: "guardian_process_gacha_bonus_audit_missing_source_project",
                section: "UpdateGuardians.processGacha",
                repairHint: "Если гача усилена completed relic_forging, укажи projectId этого проекта в sourceProjectId."));
        }

        if (relicForgingBonusSteps == 0 && !string.IsNullOrWhiteSpace(sourceProjectId))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.sourceProjectId",
                IssueSeverity.Error,
                "sourceProjectId допустим только когда реально использован relic forging bonus",
                code: "guardian_process_gacha_bonus_audit_unexpected_source_project",
                section: "UpdateGuardians.processGacha"));
        }

        if (relicForgingBonusSteps > 0 && !string.IsNullOrWhiteSpace(guardianId))
        {
            var availableForgeSteps = GetAvailableForgeGachaBonusStepsSync(guardianId, sourceProjectId ?? string.Empty);
            if (relicForgingBonusSteps > availableForgeSteps)
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.relicForgingBonusSteps",
                    IssueSeverity.Error,
                    "gachaBonusAudit.relicForgingBonusSteps превышает доступный forge bonus use",
                    code: "guardian_process_gacha_bonus_audit_forge_steps_exceeded",
                    section: "UpdateGuardians.processGacha",
                    expected: $"0..{availableForgeSteps}",
                    actual: relicForgingBonusSteps.ToString()));
            }
        }

        if (abodePowerBonusSteps + relicForgingBonusSteps != finalUpgradeSteps)
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}",
                IssueSeverity.Error,
                "gachaBonusAudit должен точно объяснять весь прирост редкости",
                code: "guardian_process_gacha_bonus_audit_upgrade_mismatch",
                section: "UpdateGuardians.processGacha",
                expected: $"abodePowerBonusSteps + relicForgingBonusSteps = {finalUpgradeSteps}",
                actual: $"{abodePowerBonusSteps} + {relicForgingBonusSteps}"));
        }
    }


    private void ValidateGuardianCurrentProjectObject(JsonElement currentProject, string projectContext, List<ValidationIssue> issues)
    {
        RequireString(currentProject, projectContext, issues, "projectId");
        if (!HasAnyNonEmptyString(currentProject, "projectName", "name"))
        {
            issues.Add(new ValidationIssue(
                projectContext,
                IssueSeverity.Error,
                "currentProject должен содержать projectName или name"));
        }

        RequireString(currentProject, projectContext, issues, "description");
        ValidateNonNegativeIntegerField(currentProject, projectContext, issues, "progressPercent", "UpdateGuardians.updateProject");
        if (currentProject.TryGetProperty("progressPercent", out var progressPercent) &&
            progressPercent.ValueKind == JsonValueKind.Number &&
            progressPercent.TryGetInt32(out var parsedProgressPercent) &&
            (parsedProgressPercent < 0 || parsedProgressPercent > 100))
        {
            issues.Add(new ValidationIssue(
                $"{projectContext}.progressPercent",
                IssueSeverity.Error,
                "progressPercent должен быть в диапазоне 0..100"));
        }

        if (!currentProject.TryGetProperty("estimatedTurnsLeft", out _) &&
            !currentProject.TryGetProperty("estimatedCompletionTurn", out _))
        {
            issues.Add(new ValidationIssue(
                projectContext,
                IssueSeverity.Error,
                "currentProject должен содержать estimatedTurnsLeft или estimatedCompletionTurn"));
        }

        if (currentProject.TryGetProperty("estimatedTurnsLeft", out _))
            ValidateNonNegativeIntegerField(currentProject, projectContext, issues, "estimatedTurnsLeft", "UpdateGuardians.updateProject");
        if (currentProject.TryGetProperty("estimatedCompletionTurn", out _))
            ValidateNonNegativeIntegerField(currentProject, projectContext, issues, "estimatedCompletionTurn", "UpdateGuardians.updateProject");

        if (!currentProject.TryGetProperty("playerCanAssist", out var playerCanAssist) ||
            playerCanAssist.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add(new ValidationIssue(
                $"{projectContext}.playerCanAssist",
                IssueSeverity.Error,
                "currentProject должен содержать boolean playerCanAssist"));
        }

        ValidateOptionalString(currentProject, projectContext, issues, "assistDescription");
    }


    private void ValidateGuardianLoreFragmentObject(JsonElement loreFragment, string fragmentContext, List<ValidationIssue> issues, bool allowNullableContent)
    {
        RequireString(loreFragment, fragmentContext, issues, "fragmentId");
        var category = RequireString(loreFragment, fragmentContext, issues, "category");
        RequireString(loreFragment, fragmentContext, issues, "title");
        if (!allowNullableContent)
        {
            RequireString(loreFragment, fragmentContext, issues, "content");
        }
        else if (loreFragment.TryGetProperty("content", out var content) &&
                 content.ValueKind != JsonValueKind.Null &&
                 (content.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(content.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{fragmentContext}.content",
                IssueSeverity.Error,
                "Stored guardian lore fragment content должен быть непустой строкой или null для ещё не раскрытого fragment",
                code: "guardian_lore_fragment_invalid_content",
                section: "Guardians",
                repairHint: "Для locked pre-planned lore fragment оставляй content = null; для unlocked fragment передавай непустой content."));
        }
        ValidateNonNegativeIntegerField(loreFragment, fragmentContext, issues, "requiredReputation", "UpdateGuardians.unlockLore");

        if (!string.IsNullOrWhiteSpace(category) && !AllowedGuardianLoreFragmentCategories.Contains(category))
        {
            issues.Add(new ValidationIssue(
                $"{fragmentContext}.category",
                IssueSeverity.Error,
                "Guardian loreFragment.category должен быть одним из canonical enum значений",
                code: "guardian_lore_fragment_invalid_category",
                section: "UpdateGuardians.unlockLore",
                expected: string.Join(" | ", AllowedGuardianLoreFragmentCategories),
                actual: category,
                repairHint: "Используй category только из guardian lore fragment contract."));
        }

        if (TryReadInt(loreFragment, "requiredReputation", out var requiredReputation) &&
            !AllowedGuardianLoreFragmentReputationThresholds.Contains(requiredReputation))
        {
            issues.Add(new ValidationIssue(
                $"{fragmentContext}.requiredReputation",
                IssueSeverity.Error,
                "Guardian loreFragment.requiredReputation должен совпадать с canonical reputation unlock threshold",
                code: "guardian_lore_fragment_invalid_required_reputation",
                section: "UpdateGuardians.unlockLore",
                expected: "0 | 50 | 130 | 230",
                actual: requiredReputation.ToString(),
                repairHint: "Используй только reputation thresholds 0, 50, 130 или 230 для unlockLore fragments."));
        }
    }


    private void ValidateGuardianMoodObject(JsonElement mood, string moodContext, List<ValidationIssue> issues)
    {
        var current = RequireString(mood, moodContext, issues, "current");
        ValidatePositiveIntegerField(mood, moodContext, issues, "intensity");
        RequireString(mood, moodContext, issues, "reason");
        if (!mood.TryGetProperty("since", out _))
        {
            issues.Add(new ValidationIssue(
                $"{moodContext}.since",
                IssueSeverity.Error,
                "Guardian mood object должен содержать since",
                code: "guardian_mood_missing_since",
                section: "UpdateGuardians.setMood",
                expected: "non-negative since marker",
                actual: "missing",
                repairHint: "Добавь mood.since в canonical guardian mood object, как это описано в guardian inner-life contract."));
        }
        else
        {
            ValidateNonNegativeIntegerField(mood, moodContext, issues, "since", "UpdateGuardians.setMood");
        }

        if (!string.IsNullOrWhiteSpace(current) && !AllowedGuardianMoodStates.Contains(current))
        {
            issues.Add(new ValidationIssue(
                $"{moodContext}.current",
                IssueSeverity.Error,
                "Guardian mood.current должен быть одним из canonical enum значений",
                code: "guardian_mood_invalid_current",
                section: "UpdateGuardians.setMood",
                expected: string.Join(" | ", AllowedGuardianMoodStates),
                actual: current,
                repairHint: "Используй mood.current только из guardian mood contract."));
        }

        if (TryReadInt(mood, "intensity", out var intensity) && (intensity < 1 || intensity > 100))
        {
            issues.Add(new ValidationIssue(
                $"{moodContext}.intensity",
                IssueSeverity.Error,
                "Guardian mood.intensity должен быть в диапазоне 1..100",
                code: "guardian_mood_intensity_out_of_bounds",
                section: "UpdateGuardians.setMood",
                expected: "1..100",
                actual: intensity.ToString(),
                repairHint: "Используй canonical 1-100 intensity scale для guardian mood."));
        }
    }


    private void ValidateGuardianProjectStateData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var isTrackerFile = contextPrefix.EndsWith(GuardianProjectState.TrackerPath, StringComparison.OrdinalIgnoreCase);
        var hasGuardianProjectFields =
            root.TryGetProperty("startGuardianProjects", out _) ||
            root.TryGetProperty("guardianProjectUpdates", out _) ||
            root.TryGetProperty("completeGuardianProjects", out _) ||
            root.TryGetProperty("temporaryProjectModifiers", out _);
        if (!isTrackerFile && !hasGuardianProjectFields)
            return;

        if (root.TryGetProperty("activeProjects", out var activeProjects))
            ValidateGuardianProjectEntryArray(activeProjects, $"{contextPrefix}.activeProjects", issues, completed: false);
        if (root.TryGetProperty("completedProjects", out var completedProjects))
            ValidateGuardianProjectEntryArray(completedProjects, $"{contextPrefix}.completedProjects", issues, completed: true);
        if (root.TryGetProperty("temporaryProjectModifiers", out var temporaryModifiers))
            ValidateGuardianProjectTemporaryModifiers(temporaryModifiers, $"{contextPrefix}.temporaryProjectModifiers", issues);

        var knownProjects = ReadKnownGuardianProjectKeysForValidation();
        var startedThisTurn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("startGuardianProjects", out var startCommands))
            ValidateGuardianProjectStartCommands(startCommands, $"{contextPrefix}.startGuardianProjects", issues, startedThisTurn);
        if (root.TryGetProperty("guardianProjectUpdates", out var updateCommands))
            ValidateGuardianProjectUpdateCommands(updateCommands, $"{contextPrefix}.guardianProjectUpdates", issues, knownProjects, startedThisTurn);
        if (root.TryGetProperty("completeGuardianProjects", out var completeCommands))
            ValidateGuardianProjectCompletionCommands(completeCommands, $"{contextPrefix}.completeGuardianProjects", issues, knownProjects, startedThisTurn);
    }


    private void ValidateGuardianPowerEventData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("guardianPowerEvents", out var events))
            return;

        var context = $"{contextPrefix}.guardianPowerEvents";
        RequireArrayOfObjects(events, context, issues);
        if (events.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in events.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "eventId");
            RequireString(item, itemContext, issues, "guardianId");
            ValidateIntegerField(item, itemContext, issues, "delta");
            var reasonType = RequireString(item, itemContext, issues, "reasonType");
            if (!string.IsNullOrWhiteSpace(reasonType) && !GuardianPowerEventState.IsValidReasonType(reasonType))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.reasonType",
                    IssueSeverity.Error,
                    "guardianPowerEvents.reasonType использует неподдерживаемый тип события силы Обители",
                    code: "guardian_power_event_invalid_reason_type",
                    section: "AbodePower",
                    expected: string.Join(" | ", GuardianPowerEventState.AllowedReasonTypes),
                    actual: reasonType,
                    repairHint: "Используй только reasonType из canonical Abode Power contract."));
            }
            else if (string.Equals(reasonType, "guardian_quest", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.reasonType",
                    IssueSeverity.Error,
                    "guardian quest power change должен идти через UpdateGuardians.completeQuest.questPowerAudit, а не через raw guardianPowerEvents",
                    code: "guardian_power_event_guardian_quest_wrong_surface",
                    section: "AbodePower",
                    expected: "completeQuest with questPowerAudit",
                    actual: "guardianPowerEvents.reasonType = guardian_quest",
                    repairHint: "Не отправляй raw guardianPowerEvents для guardian quest. Заверши квест через UpdateGuardians.completeQuest и передай questPowerAudit."));
            }

            RequireString(item, itemContext, issues, "sourceSurface");
            RequireString(item, itemContext, issues, "sourceId");
            RequireString(item, itemContext, issues, "title");
            RequireString(item, itemContext, issues, "summary");
            ValidateOptionalNullableStringField(item, itemContext, issues, "relatedGuardianId");
            var visibility = GetFirstNonEmptyString(item, "visibility");
            if (!string.IsNullOrWhiteSpace(visibility) && !GuardianPowerEventState.IsValidVisibility(visibility))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.visibility",
                    IssueSeverity.Error,
                    "guardianPowerEvents.visibility использует неподдерживаемое значение",
                    code: "guardian_power_event_invalid_visibility",
                    section: "AbodePower",
                    expected: string.Join(" | ", GuardianPowerEventState.AllowedVisibility),
                    actual: visibility,
                    repairHint: "Используй visibility=player_known или hidden."));
            }

            if (item.TryGetProperty("appliedAt", out var appliedAt) && appliedAt.ValueKind != JsonValueKind.Null)
            {
                var appliedAtValue = RequireString(item, itemContext, issues, "appliedAt");
                if (!string.IsNullOrWhiteSpace(appliedAtValue) && !DateTimeOffset.TryParse(appliedAtValue, out _))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.appliedAt",
                        IssueSeverity.Error,
                        "guardianPowerEvents.appliedAt должен быть ISO 8601 timestamp",
                        code: "guardian_power_event_invalid_applied_at",
                        section: "AbodePower",
                        repairHint: "Сохраняй appliedAt как ISO 8601 timestamp или не передавай его, чтобы клиент проставил сам."));
                }
            }

            if (!item.TryGetProperty("audit", out var audit) || !RequireObject(audit, $"{itemContext}.audit", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.audit",
                    IssueSeverity.Error,
                    "guardianPowerEvents обязан содержать audit object",
                    code: "guardian_power_event_missing_audit",
                    section: "AbodePower",
                    repairHint: "Каждое изменение силы Обители должно приходить с machine-readable audit object."));
            }
            else
            {
                ValidateGuardianPowerEventAudit(reasonType, audit, $"{itemContext}.audit", issues);
            }
        }
    }


    private void ValidateGuardianPowerJournalData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!contextPrefix.EndsWith(GuardianPowerEventState.JournalPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (!root.TryGetProperty("entries", out var entries))
            return;

        RequireArrayOfObjects(entries, $"{contextPrefix}.entries", issues);
        if (entries.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            RequireString(entry, entryContext, issues, "entryId");
            RequireString(entry, entryContext, issues, "eventId");
            ValidateNonNegativeIntegerField(entry, entryContext, issues, "turn", "AbodePower");
            RequireString(entry, entryContext, issues, "guardianId");
            RequireString(entry, entryContext, issues, "guardianName");
            ValidateIntegerField(entry, entryContext, issues, "delta");
            RequireString(entry, entryContext, issues, "reasonType");
            RequireString(entry, entryContext, issues, "sourceSurface");
            RequireString(entry, entryContext, issues, "sourceId");
            RequireString(entry, entryContext, issues, "title");
            RequireString(entry, entryContext, issues, "summary");
            RequireString(entry, entryContext, issues, "visibility");
            ValidateOptionalNullableStringField(entry, entryContext, issues, "relatedGuardianId");
            RequireString(entry, entryContext, issues, "appliedAt");
            if (entry.TryGetProperty("audit", out var audit))
                RequireObject(audit, $"{entryContext}.audit", issues);
        }
    }


    private void ValidateGuardianPowerEventAudit(string reasonType, JsonElement audit, string auditContext, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(reasonType))
            return;

        if (string.Equals(reasonType, "resonance", StringComparison.OrdinalIgnoreCase))
        {
            ValidateIntegerField(audit, auditContext, issues, "domainAlignment");
            ValidateIntegerField(audit, auditContext, issues, "worldScale");
            ValidateIntegerField(audit, auditContext, issues, "permanence");
            ValidateIntegerField(audit, auditContext, issues, "sacrifice");
            ValidateIntegerField(audit, auditContext, issues, "publicImpact");
            ValidateIntegerField(audit, auditContext, issues, "resonanceScore");
            RequireString(audit, auditContext, issues, "classification");
            ValidateIntegerField(audit, auditContext, issues, "finalDelta");
            return;
        }

        if (string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase))
        {
            var offeringType = RequireString(audit, auditContext, issues, "offeringType");
            RequireString(audit, auditContext, issues, "returnCycleId");
            ValidateIntegerField(audit, auditContext, issues, "baseDelta");
            ValidateIntegerField(audit, auditContext, issues, "finalDelta");

            if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            {
                ValidatePositiveNumberField(audit, auditContext, issues, "inkFeathersOffered");
                ValidateNonNegativeIntegerField(audit, auditContext, issues, "capRemainingBefore", "AbodePower");
            }
            else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
            {
                RequireString(audit, auditContext, issues, "relicId");
                RequireString(audit, auditContext, issues, "relicName");
                RequireString(audit, auditContext, issues, "relicRarity");
            }
            else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
            {
                RequireString(audit, auditContext, issues, "archiveId");
                RequireString(audit, auditContext, issues, "archiveTitle");
                var archiveEntryType = RequireString(audit, auditContext, issues, "archiveEntryType");
                var archiveRarity = RequireString(audit, auditContext, issues, "archiveRarity");
                if (!string.IsNullOrWhiteSpace(archiveEntryType) &&
                    !AfterlifeArchiveState.OfferingTypeMatchesEntryType(offeringType, archiveEntryType))
                {
                    issues.Add(new ValidationIssue(
                        $"{auditContext}.archiveEntryType",
                        IssueSeverity.Error,
                        "offering audit archiveEntryType не соответствует offeringType",
                        code: "guardian_power_event_offering_archive_type_mismatch",
                        section: "AbodePower",
                        expected: string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase)
                            ? AfterlifeArchiveState.EntryTypeLoreFragment
                            : AfterlifeArchiveState.EntryTypeSecretRecord,
                        actual: archiveEntryType,
                        repairHint: "Синхронизируй offering audit: archive_lore_fragment -> lore_fragment, archive_secret_record -> secret_record."));
                }
                if (!string.IsNullOrWhiteSpace(archiveRarity) && GetRarityRank(archiveRarity) == 0)
                {
                    issues.Add(new ValidationIssue(
                        $"{auditContext}.archiveRarity",
                        IssueSeverity.Error,
                        "offering audit archiveRarity должна быть canonical rarity tier",
                        code: "guardian_power_event_offering_archive_invalid_rarity",
                        section: "AbodePower",
                        expected: "Common | Uncommon | Rare | Epic | Legendary | Unique",
                        actual: archiveRarity));
                }
            }
        }
    }


    private void ValidateGuardianProjectJournalData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!contextPrefix.EndsWith(GuardianProjectState.JournalPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (!root.TryGetProperty("entries", out var entries))
            return;

        RequireArrayOfObjects(entries, $"{contextPrefix}.entries", issues);
        if (entries.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            RequireString(entry, entryContext, issues, "entryId");
            ValidateNonNegativeIntegerField(entry, entryContext, issues, "turn", "GuardianProjects");
            RequireString(entry, entryContext, issues, "guardianId");
            RequireString(entry, entryContext, issues, "projectId");
            RequireString(entry, entryContext, issues, "eventType");
            RequireString(entry, entryContext, issues, "visibility");
            RequireString(entry, entryContext, issues, "title");
            RequireString(entry, entryContext, issues, "summary");
            if (entry.TryGetProperty("details", out var details))
                ValidateLooseStringOrObjectArray(details, $"{entryContext}.details", issues);
        }
    }


    private void ValidateGuardianProjectEntryArray(JsonElement value, string context, List<ValidationIssue> issues, bool completed)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var activeGuardians = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in value.EnumerateArray())
        {
            var entryContext = $"{context}[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var guardianId = RequireString(entry, entryContext, issues, "guardianId");
            if (!completed && !string.IsNullOrWhiteSpace(guardianId) && !activeGuardians.Add(guardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.guardianId",
                    IssueSeverity.Error,
                    "У одного Хранителя не может быть больше одного активного guardian project в v1",
                    code: "guardian_project_duplicate_active_guardian",
                    section: "GuardianProjects",
                    expected: "at most one active project per guardian",
                    actual: guardianId,
                    repairHint: "Оставляй у одного guardianId не более одной записи в activeProjects[]."));
            }

            if (!entry.TryGetProperty("project", out var project) || !RequireObject(project, $"{entryContext}.project", issues))
                continue;

            ValidateGuardianFullProjectObject(project, $"{entryContext}.project", issues, completed);
        }
    }


    private void ValidateGuardianProjectTemporaryModifiers(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "guardianId");
            RequireString(item, itemContext, issues, "modifierId");
            var modifierType = RequireString(item, itemContext, issues, "modifierType");
            if (!string.IsNullOrWhiteSpace(modifierType) &&
                !string.Equals(modifierType, "next_internal_project_starting_pressure", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.modifierType",
                    IssueSeverity.Error,
                    "temporaryProjectModifiers.modifierType использует неподдерживаемый тип",
                    code: "guardian_project_invalid_modifier_type",
                    section: "GuardianProjects",
                    expected: "next_internal_project_starting_pressure",
                    actual: modifierType,
                    repairHint: "В текущем этапе используй только next_internal_project_starting_pressure."));
            }
            ValidateIntegerField(item, itemContext, issues, "value");
            ValidateNonNegativeIntegerField(item, itemContext, issues, "remainingApplications", "GuardianProjects");
        }
    }


    private void ValidateGuardianProjectStartCommands(JsonElement value, string context, List<ValidationIssue> issues, HashSet<string> startedThisTurn)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var guardiansStarted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            if (!item.TryGetProperty("project", out var project) || !RequireObject(project, $"{itemContext}.project", issues))
                continue;

            ValidateGuardianFullProjectObject(project, $"{itemContext}.project", issues, completed: false);
            var projectId = GetFirstNonEmptyString(project, "projectId");
            if (!string.IsNullOrWhiteSpace(guardianId) && !guardiansStarted.Add(guardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.guardianId",
                    IssueSeverity.Error,
                    "startGuardianProjects не должен запускать больше одного проекта для одного guardianId в одном ходу",
                    code: "guardian_project_start_duplicate_guardian",
                    section: "GuardianProjects",
                    repairHint: "Если Хранителю нужен новый проект, запускай только один active project per guardian в v1."));
            }

            if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(projectId))
                startedThisTurn.Add(GuardianProjectState.BuildKey(guardianId, projectId));
        }
    }


    private void ValidateGuardianProjectUpdateCommands(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownProjects,
        HashSet<string> startedThisTurn)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            var projectId = RequireString(item, itemContext, issues, "projectId");
            var key = GuardianProjectState.BuildKey(guardianId, projectId);
            if (!string.IsNullOrWhiteSpace(guardianId) &&
                !string.IsNullOrWhiteSpace(projectId) &&
                !knownProjects.Contains(key) &&
                !startedThisTurn.Contains(key))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.projectId",
                    IssueSeverity.Error,
                    "guardianProjectUpdates ссылается на projectId, которого нет в canonical active tracker этого Хранителя",
                    code: "guardian_project_update_unknown_project_id",
                    section: "GuardianProjects",
                    expected: "existing active projectId for the target guardian",
                    actual: projectId,
                    repairHint: "Обновляй только существующий active projectId target guardian либо сначала создай его в startGuardianProjects."));
            }

            if (!HasAnyGuardianProjectNonTerminalChanges(item))
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "guardianProjectUpdates должен содержать хотя бы одно non-terminal tracker change",
                    code: "guardian_project_update_missing_changes",
                    section: "GuardianProjects",
                    repairHint: "Передай вместе с guardianId/projectId хотя бы одно изменение: activeState, workDone, currentStage, pressure, stability или audit objects."));
            }

            if (item.TryGetProperty("activeState", out _))
                RequireString(item, itemContext, issues, "activeState");
            if (item.TryGetProperty("workDone", out _))
                ValidateNonNegativeIntegerField(item, itemContext, issues, "workDone", "GuardianProjects");
            if (item.TryGetProperty("currentStage", out _))
                ValidateNonNegativeIntegerField(item, itemContext, issues, "currentStage", "GuardianProjects");
            if (item.TryGetProperty("pressure", out _))
                ValidateNonNegativeIntegerField(item, itemContext, issues, "pressure", "GuardianProjects");
            if (item.TryGetProperty("stability", out _))
                ValidateNonNegativeIntegerField(item, itemContext, issues, "stability", "GuardianProjects");
            if (item.TryGetProperty("pressureAudit", out var pressureAudit))
                RequireObject(pressureAudit, $"{itemContext}.pressureAudit", issues);
            if (item.TryGetProperty("stabilityAudit", out var stabilityAudit))
                RequireObject(stabilityAudit, $"{itemContext}.stabilityAudit", issues);
            if (item.TryGetProperty("workAudit", out var workAudit))
                RequireObject(workAudit, $"{itemContext}.workAudit", issues);
            if (item.TryGetProperty("assistAudit", out var assistAudit))
                ValidateGuardianProjectAssistAudit(assistAudit, $"{itemContext}.assistAudit", issues);
            if (item.TryGetProperty("sabotageAudit", out var sabotageAudit))
                ValidateGuardianProjectSabotageAudit(sabotageAudit, $"{itemContext}.sabotageAudit", issues);
        }
    }


    private void ValidateGuardianProjectCompletionCommands(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownProjects,
        HashSet<string> startedThisTurn)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            var projectId = RequireString(item, itemContext, issues, "projectId");
            var key = GuardianProjectState.BuildKey(guardianId, projectId);
            if (!string.IsNullOrWhiteSpace(guardianId) &&
                !string.IsNullOrWhiteSpace(projectId) &&
                !knownProjects.Contains(key) &&
                !startedThisTurn.Contains(key))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.projectId",
                    IssueSeverity.Error,
                    "completeGuardianProjects ссылается на projectId, которого нет у target guardian",
                    code: "guardian_project_completion_unknown_project_id",
                    section: "GuardianProjects",
                    expected: "existing active projectId for the target guardian",
                    actual: projectId,
                    repairHint: "Завершай только тот projectId, который реально существует у выбранного Хранителя."));
            }

            var finalState = RequireString(item, itemContext, issues, "finalState");
            if (!string.IsNullOrWhiteSpace(finalState) && !GuardianProjectState.IsValidFinalState(finalState))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.finalState",
                    IssueSeverity.Error,
                    "completeGuardianProjects.finalState использует неподдерживаемое terminal state",
                    code: "guardian_project_completion_invalid_final_state",
                    section: "GuardianProjects",
                    expected: string.Join(" | ", GuardianProjectState.AllowedFinalStates),
                    actual: finalState,
                    repairHint: "Используй только Completed, Abandoned, Sabotaged или Collapsed."));
            }

            RequireString(item, itemContext, issues, "outcome");
            ValidateIntegerField(item, itemContext, issues, "abodePowerDelta");
            var targetGuardianId = GetFirstNonEmptyString(item, "targetGuardianId");
            ValidateOptionalNullableStringField(item, itemContext, issues, "targetGuardianId");
            if (item.TryGetProperty("offensiveImpactAudit", out var offensiveImpactAudit) &&
                offensiveImpactAudit.ValueKind != JsonValueKind.Null)
            {
                RequireObject(offensiveImpactAudit, $"{itemContext}.offensiveImpactAudit", issues);
                if (string.IsNullOrWhiteSpace(targetGuardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.targetGuardianId",
                        IssueSeverity.Error,
                        "offensiveImpactAudit требует targetGuardianId",
                        code: "guardian_project_completion_offensive_audit_missing_target",
                        section: "GuardianProjects",
                        repairHint: "Если completion несёт offensiveImpactAudit, сохраняй непустой targetGuardianId."));
                }
                if (offensiveImpactAudit.ValueKind == JsonValueKind.Object)
                    ValidateNonNegativeIntegerField(offensiveImpactAudit, $"{itemContext}.offensiveImpactAudit", issues, "targetLoss", "GuardianProjects");
            }
            if (item.TryGetProperty("pressureAudit", out var pressureAudit))
                RequireObject(pressureAudit, $"{itemContext}.pressureAudit", issues);
            if (item.TryGetProperty("stabilityAudit", out var stabilityAudit))
                RequireObject(stabilityAudit, $"{itemContext}.stabilityAudit", issues);
            if (item.TryGetProperty("workAudit", out var workAudit))
                RequireObject(workAudit, $"{itemContext}.workAudit", issues);
        }
    }


    private void ValidateGuardianFullProjectObject(JsonElement project, string context, List<ValidationIssue> issues, bool completed)
    {
        RequireString(project, context, issues, "projectId");
        var projectType = RequireString(project, context, issues, "projectType");
        var projectTier = RequireString(project, context, issues, "projectTier");
        var projectMode = RequireString(project, context, issues, "projectMode");
        if (!string.IsNullOrWhiteSpace(projectTier) && !GuardianProjectState.IsValidProjectTier(projectTier))
        {
            issues.Add(new ValidationIssue(
                $"{context}.projectTier",
                IssueSeverity.Error,
                "guardian project использует неподдерживаемый projectTier",
                code: "guardian_project_invalid_tier",
                section: "GuardianProjects",
                expected: string.Join(" | ", GuardianProjectState.AllowedProjectTiers),
                actual: projectTier,
                repairHint: "Используй только minor, major или grand."));
        }
        if (!string.IsNullOrWhiteSpace(projectMode) && !GuardianProjectState.IsValidProjectMode(projectMode))
        {
            issues.Add(new ValidationIssue(
                $"{context}.projectMode",
                IssueSeverity.Error,
                "guardian project использует неподдерживаемый projectMode",
                code: "guardian_project_invalid_mode",
                section: "GuardianProjects",
                expected: string.Join(" | ", GuardianProjectState.AllowedProjectModes),
                actual: projectMode,
                repairHint: "Используй только internal, supportive или offensive."));
        }

        if (string.IsNullOrWhiteSpace(GetFirstNonEmptyString(project, "projectName", "name")))
        {
            issues.Add(new ValidationIssue(
                $"{context}.projectName",
                IssueSeverity.Error,
                "guardian project должен содержать projectName или name",
                code: "guardian_project_missing_name",
                section: "GuardianProjects",
                repairHint: "Сохраняй у guardian project непустое projectName или name."));
        }

        if ((string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase)) &&
            string.IsNullOrWhiteSpace(GetFirstNonEmptyString(project, "targetGuardianId")))
        {
            issues.Add(new ValidationIssue(
                $"{context}.targetGuardianId",
                IssueSeverity.Error,
                "Политический guardian project обязан ссылаться на targetGuardianId",
                code: "guardian_project_missing_target_guardian",
                section: "GuardianProjects",
                repairHint: "Для offensive_intrigue и counter_rival_operation сохраняй непустой targetGuardianId."));
        }

        if (completed)
        {
            ValidateNonNegativeIntegerField(project, context, issues, "completionTurn", "GuardianProjects");
            var finalState = RequireString(project, context, issues, "finalState");
            if (!string.IsNullOrWhiteSpace(finalState) && !GuardianProjectState.IsValidFinalState(finalState))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.finalState",
                    IssueSeverity.Error,
                    "completed guardian project использует неподдерживаемый finalState",
                    code: "guardian_project_invalid_completed_final_state",
                    section: "GuardianProjects",
                    expected: string.Join(" | ", GuardianProjectState.AllowedFinalStates),
                    actual: finalState,
                    repairHint: "Для terminal guardian projects используй только Completed, Abandoned, Sabotaged или Collapsed."));
            }

            ValidateOptionalString(project, context, issues, "outcome");
            if (project.TryGetProperty("abodePowerDelta", out _))
                ValidateIntegerField(project, context, issues, "abodePowerDelta");
            ValidateGuardianProjectOutcomeAudit(project, context, issues);
            ValidateGuardianProjectEffectState(project, context, issues);
            if (project.TryGetProperty("systemEffectSummary", out var systemEffectSummary) && systemEffectSummary.ValueKind != JsonValueKind.Null)
                ValidateLooseStringOrObjectArray(systemEffectSummary, $"{context}.systemEffectSummary", issues);
            return;
        }

        RequireString(project, context, issues, "activeState");
        ValidateNonNegativeIntegerField(project, context, issues, "totalWork", "GuardianProjects");
        ValidateNonNegativeIntegerField(project, context, issues, "workDone", "GuardianProjects");
        ValidateNonNegativeIntegerField(project, context, issues, "totalStages", "GuardianProjects");
        ValidateNonNegativeIntegerField(project, context, issues, "currentStage", "GuardianProjects");
        ValidateNonNegativeIntegerField(project, context, issues, "pressure", "GuardianProjects");
        ValidateNonNegativeIntegerField(project, context, issues, "stability", "GuardianProjects");
        ValidateOptionalString(project, context, issues, "description");
        ValidateNonNegativeIntegerField(project, context, issues, "startedTurn", "GuardianProjects");
        if (project.TryGetProperty("estimatedCompletionTurn", out _))
            ValidateNonNegativeIntegerField(project, context, issues, "estimatedCompletionTurn", "GuardianProjects");
        if (project.TryGetProperty("playerCanAssist", out var playerCanAssist) && playerCanAssist.ValueKind != JsonValueKind.Null)
            RequireBooleanField(project, context, issues, "playerCanAssist");
        ValidateOptionalString(project, context, issues, "assistDescription");
    }


    private static bool HasAnyGuardianProjectNonTerminalChanges(JsonElement item)
    {
        foreach (var propName in new[]
                 {
                     "activeState", "workDone", "currentStage", "pressure", "stability",
                     "pressureAudit", "stabilityAudit", "workAudit", "assistAudit", "sabotageAudit"
                 })
        {
            if (item.TryGetProperty(propName, out _))
                return true;
        }

        return false;
    }


    private void ValidateGuardianProjectOutcomeAudit(JsonElement project, string context, List<ValidationIssue> issues)
    {
        var projectType = GetFirstNonEmptyString(project, "projectType");
        var finalState = GetFirstNonEmptyString(project, "finalState");
        var requiresAudit =
            (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase));

        if (!requiresAudit)
            return;

        if (!project.TryGetProperty("projectOutcomeAudit", out var audit) || !RequireObject(audit, $"{context}.projectOutcomeAudit", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.projectOutcomeAudit",
                IssueSeverity.Error,
                "Recipe-driven completed guardian project должен содержать projectOutcomeAudit",
                code: "guardian_project_missing_project_outcome_audit",
                section: "GuardianProjects",
                repairHint: "Сохраняй projectOutcomeAudit у completed/sabotaged recipe-проектов, чтобы их системный effect был детерминированным."));
            return;
        }

        var auditContext = $"{context}.projectOutcomeAudit";
        if (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "safePressureBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "defenseRatingBonus", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "upgradedTradeSlots", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "elevatedTradeSlots", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "guardianRarityCeilingBonusSteps", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "bonusLoreUnlocks", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "questHookCount", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "guaranteedArchiveQuestCount", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "specialQuestLineUnlocks", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "visibleRivalClueBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "archiveWarningTierBonus", "GuardianProjects");
            if (!audit.TryGetProperty("unlockedLoreFragments", out var unlockedLoreFragments))
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.unlockedLoreFragments",
                    IssueSeverity.Error,
                    "Completed lore_research должен содержать unlockedLoreFragments",
                    code: "guardian_project_lore_research_missing_unlocked_fragments",
                    section: "GuardianProjects",
                    repairHint: "Передавай в projectOutcomeAudit полный массив unlockedLoreFragments с раскрытыми fragment objects."));
                return;
            }

            RequireArrayOfObjects(unlockedLoreFragments, $"{auditContext}.unlockedLoreFragments", issues);
            if (unlockedLoreFragments.ValueKind == JsonValueKind.Array)
            {
                var expectedUnlocks = TryReadInt(audit, "bonusLoreUnlocks", out var count) ? count : 0;
                if (unlockedLoreFragments.GetArrayLength() != expectedUnlocks)
                {
                    issues.Add(new ValidationIssue(
                        $"{auditContext}.unlockedLoreFragments",
                        IssueSeverity.Error,
                        "Число unlockedLoreFragments должно совпадать с bonusLoreUnlocks",
                        code: "guardian_project_lore_research_unlock_count_mismatch",
                        section: "GuardianProjects",
                        expected: expectedUnlocks.ToString(),
                        actual: unlockedLoreFragments.GetArrayLength().ToString(),
                        repairHint: "Сохраняй столько unlockedLoreFragments, сколько заявлено в bonusLoreUnlocks."));
                }

                var index = 0;
                foreach (var fragment in unlockedLoreFragments.EnumerateArray())
                {
                    if (fragment.ValueKind == JsonValueKind.Object)
                        ValidateGuardianLoreFragmentObject(fragment, $"{auditContext}.unlockedLoreFragments[{index}]", issues, allowNullableContent: false);
                    index++;
                }
            }
            return;
        }

        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "preparationBudgetPoints", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "preparationClaimPriorityBonus", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "hostilePriorityTokensGranted", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "attackerCurrentPower", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "targetCurrentPower", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "baseLoss", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "attackerBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "baseTargetShield", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "fortificationBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "counterOperationBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "playerDefenseBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "targetShield", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "targetLoss", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "pressureDelta", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "stabilityDamage", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "pressureRelief", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "stabilityRelief", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "abodePowerGain", "GuardianProjects");
        }
    }


    private void ValidateGuardianProjectAssistAudit(JsonElement audit, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(audit, context, issues))
            return;

        ValidateOptionalString(audit, context, issues, "auditKind");
        ValidateAxisScore(audit, context, issues, "DomainRelevance");
        ValidateAxisScore(audit, context, issues, "RiskOrCost");
        ValidateAxisScore(audit, context, issues, "ScarcityOrUniqueness");
        ValidateAxisScore(audit, context, issues, "DirectProjectImpact");
        ValidateNonNegativeIntegerField(audit, context, issues, "assistScore", "GuardianProjects");
        var classification = RequireString(audit, context, issues, "classification");

        var assistScore = GetIntOrDefault(audit, "assistScore");
        var expectedScore =
            GetAxisScore(audit, "DomainRelevance") +
            GetAxisScore(audit, "RiskOrCost") +
            GetAxisScore(audit, "ScarcityOrUniqueness") +
            GetAxisScore(audit, "DirectProjectImpact");
        if (assistScore != expectedScore)
        {
            issues.Add(new ValidationIssue(
                $"{context}.assistScore",
                IssueSeverity.Error,
                "assistAudit.assistScore должен быть суммой всех assist axes",
                code: "guardian_project_assist_score_mismatch",
                section: "GuardianProjects",
                expected: expectedScore.ToString(),
                actual: assistScore.ToString(),
                repairHint: "Посчитай assistScore как сумму DomainRelevance + RiskOrCost + ScarcityOrUniqueness + DirectProjectImpact."));
        }

        var expectedClassification = ResolveAssistClassification(expectedScore, GetFirstNonEmptyString(audit, "auditKind"));
        if (!string.Equals(classification, expectedClassification, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.classification",
                IssueSeverity.Error,
                "assistAudit.classification не совпадает с детерминированной шкалой assist score",
                code: "guardian_project_assist_classification_mismatch",
                section: "GuardianProjects",
                expected: expectedClassification,
                actual: classification,
                repairHint: "Используй одну из canonical classifications: not qualified as project assist, minor assist, meaningful assist, major breakthrough, minor defensive help, meaningful protection, major defensive breakthrough."));
        }
    }


    private void ValidateGuardianProjectSabotageAudit(JsonElement audit, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(audit, context, issues))
            return;

        ValidateAxisScore(audit, context, issues, "HostileReach");
        ValidateAxisScore(audit, context, issues, "ProjectExposure");
        ValidateAxisScore(audit, context, issues, "DamageIntent");
        ValidateAxisScore(audit, context, issues, "DamageAchieved");
        ValidateAxisScore(audit, context, issues, "PlayerComplicity");
        ValidateNonNegativeIntegerField(audit, context, issues, "sabotageSeverityScore", "GuardianProjects");
        var classification = RequireString(audit, context, issues, "classification");

        var severityScore = GetIntOrDefault(audit, "sabotageSeverityScore");
        var expectedScore =
            GetAxisScore(audit, "HostileReach") +
            GetAxisScore(audit, "ProjectExposure") +
            GetAxisScore(audit, "DamageIntent") +
            GetAxisScore(audit, "DamageAchieved") +
            GetAxisScore(audit, "PlayerComplicity");
        if (severityScore != expectedScore)
        {
            issues.Add(new ValidationIssue(
                $"{context}.sabotageSeverityScore",
                IssueSeverity.Error,
                "sabotageAudit.sabotageSeverityScore должен быть суммой sabotage axes",
                code: "guardian_project_sabotage_score_mismatch",
                section: "GuardianProjects",
                expected: expectedScore.ToString(),
                actual: severityScore.ToString(),
                repairHint: "Посчитай sabotageSeverityScore как сумму HostileReach + ProjectExposure + DamageIntent + DamageAchieved + PlayerComplicity."));
        }

        var expectedClassification = ResolveSabotageClassification(expectedScore);
        if (!string.Equals(classification, expectedClassification, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.classification",
                IssueSeverity.Error,
                "sabotageAudit.classification не совпадает с детерминированной шкалой sabotage severity",
                code: "guardian_project_sabotage_classification_mismatch",
                section: "GuardianProjects",
                expected: expectedClassification,
                actual: classification,
                repairHint: "Используй одну из canonical classifications: nuisance, minor interference, major sabotage, grand strike."));
        }
    }


    private void ValidateAxisScore(JsonElement audit, string context, List<ValidationIssue> issues, string fieldName)
    {
        ValidateNonNegativeIntegerField(audit, context, issues, fieldName, "GuardianProjects");
        var value = GetIntOrDefault(audit, fieldName);
        if (value < 0 || value > 2)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{fieldName}",
                IssueSeverity.Error,
                $"{fieldName} должен использовать canonical ось 0..2",
                code: "guardian_project_audit_axis_out_of_range",
                section: "GuardianProjects",
                expected: "0..2",
                actual: value.ToString(),
                repairHint: "Используй для scoring axes только значения 0, 1 или 2."));
        }
    }


    private static int GetAxisScore(JsonElement audit, string fieldName) => GetIntOrDefault(audit, fieldName);


    private static string ResolveAssistClassification(int score, string? auditKind)
    {
        var defense = string.Equals((auditKind ?? string.Empty).Trim(), "defense", StringComparison.OrdinalIgnoreCase);
        if (score <= 2)
            return "not qualified as project assist";
        if (score <= 4)
            return defense ? "minor defensive help" : "minor assist";
        if (score <= 6)
            return defense ? "meaningful protection" : "meaningful assist";
        return defense ? "major defensive breakthrough" : "major breakthrough";
    }


    private static string ResolveSabotageClassification(int score) =>
        score switch
        {
            <= 2 => "nuisance",
            <= 4 => "minor interference",
            <= 7 => "major sabotage",
            _ => "grand strike"
        };


    private void ValidateGuardianProjectEffectState(JsonElement project, string context, List<ValidationIssue> issues)
    {
        var projectType = GetFirstNonEmptyString(project, "projectType");
        var finalState = GetFirstNonEmptyString(project, "finalState");
        var requiresEffectState =
            (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase)));

        if (!requiresEffectState)
            return;

        if (!project.TryGetProperty("effectState", out var effectState) || !RequireObject(effectState, $"{context}.effectState", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.effectState",
                IssueSeverity.Error,
                "Completed recipe-driven guardian project должен содержать effectState для временного lifecycle",
                code: "guardian_project_missing_effect_state",
                section: "GuardianProjects",
                repairHint: "Сохраняй effectState у временных recipe-проектов, чтобы их бонусы не становились постоянными."));
            return;
        }

        var effectContext = $"{context}.effectState";
        if (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "safePressureBonusGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "defenseRatingBonusGranted", "GuardianProjects");
            return;
        }

        if (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "tradeRefreshUsesGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "tradeRefreshUsesSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "gachaUsesGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "gachaUsesSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "upgradedTradeSlotsGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "elevatedTradeSlotsGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "rarityCeilingBonusStepsGranted", "GuardianProjects");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "tradeRefreshUsesSpent", "tradeRefreshUsesGranted");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "gachaUsesSpent", "gachaUsesGranted");
            return;
        }

        ValidateNonNegativeIntegerField(effectState, effectContext, issues, "targetIncarnation", "GuardianProjects");
        if (string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "bonusLoreUnlocksApplied", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "questHookTokensGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "questHookTokensSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "guaranteedArchiveQuestGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "guaranteedArchiveQuestSpawned", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "guaranteedArchiveQuestConsumed", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "specialQuestLineTokensGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "specialQuestLineTokensSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "visibleRivalClueBudgetGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "visibleRivalClueBudgetSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "archiveWarningTierBonusGranted", "GuardianProjects");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "questHookTokensSpent", "questHookTokensGranted");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "guaranteedArchiveQuestSpawned", "guaranteedArchiveQuestGranted");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "guaranteedArchiveQuestConsumed", "guaranteedArchiveQuestGranted");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "guaranteedArchiveQuestConsumed", "guaranteedArchiveQuestSpawned");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "specialQuestLineTokensSpent", "specialQuestLineTokensGranted");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "visibleRivalClueBudgetSpent", "visibleRivalClueBudgetGranted");
            return;
        }

        if (string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "preparationBudgetPointsGranted", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "preparationBudgetPointsSpent", "GuardianProjects");
            ValidateNonNegativeIntegerField(effectState, effectContext, issues, "preparationClaimPriorityBonusGranted", "GuardianProjects");
            RequireBooleanField(effectState, effectContext, issues, "consumedAtLifeStart");
            ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "preparationBudgetPointsSpent", "preparationBudgetPointsGranted");
            return;
        }

        ValidateNonNegativeIntegerField(effectState, effectContext, issues, "hostilePriorityTokensGranted", "GuardianProjects");
        ValidateNonNegativeIntegerField(effectState, effectContext, issues, "hostilePriorityTokensSpent", "GuardianProjects");
        RequireBooleanField(effectState, effectContext, issues, "consumedAtLifeStart");
        ValidateSpentDoesNotExceedGranted(effectState, effectContext, issues, "hostilePriorityTokensSpent", "hostilePriorityTokensGranted");
    }


    private void ValidateSpentDoesNotExceedGranted(
        JsonElement effectState,
        string effectContext,
        List<ValidationIssue> issues,
        string spentField,
        string grantedField)
    {
        var spent = GetIntOrDefault(effectState, spentField);
        var granted = GetIntOrDefault(effectState, grantedField);
        if (spent <= granted)
            return;

        issues.Add(new ValidationIssue(
            $"{effectContext}.{spentField}",
            IssueSeverity.Error,
            "effectState не может тратить больше ресурса, чем было granted",
            code: "guardian_project_effect_state_spent_exceeds_granted",
            section: "GuardianProjects",
            expected: $"<= {grantedField} ({granted})",
            actual: spent.ToString(),
            repairHint: "Синхронизируй effectState lifecycle: spent должен быть не больше granted и не может повторно расходоваться после исчерпания."));
    }


    private GuardianProjectState.ResolvedGuardianDerivedState ResolveGuardianDerivedStateForValidation(JsonElement guardian)
    {
        var trackerJson = ReadCurrentTrackedFileSync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerJson))
            return GuardianProjectState.ResolveGuardianDerivedState(guardian);

        try
        {
            using var trackerDoc = JsonDocument.Parse(trackerJson);
            return GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerDoc.RootElement);
        }
        catch
        {
            return GuardianProjectState.ResolveGuardianDerivedState(guardian);
        }
    }


    private HashSet<string> ReadKnownGuardianProjectKeysForValidation()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var json = ReadPreTurnTrackedFileSync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("activeProjects", out var activeProjects) ||
                activeProjects.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var entry in activeProjects.EnumerateArray())
            {
                if (!entry.TryGetProperty("guardianId", out var guardianIdNode) ||
                    guardianIdNode.ValueKind != JsonValueKind.String ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var guardianId = guardianIdNode.GetString();
                var projectId = GetFirstNonEmptyString(project, "projectId");
                if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(projectId))
                    result.Add(GuardianProjectState.BuildKey(guardianId!, projectId!));
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private int GetAvailableForgeGachaBonusStepsSync(string guardianId, string sourceProjectId)
    {
        var json = ReadPreTurnTrackedFileSync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("completedProjects", out var completedProjects) ||
                completedProjects.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                    !entry.TryGetProperty("project", out var project) ||
                    project.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetFirstNonEmptyString(project, "projectType"), "relic_forging", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetFirstNonEmptyString(project, "finalState"), "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceProjectId) &&
                    !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var granted = 0;
                var spent = 0;
                if (project.TryGetProperty("effectState", out var effectState) && effectState.ValueKind == JsonValueKind.Object)
                {
                    granted = GetIntOrDefault(effectState, "gachaUsesGranted");
                    spent = GetIntOrDefault(effectState, "gachaUsesSpent");
                }

                if (granted <= 0)
                    granted = 1;

                var remainingUses = Math.Max(0, granted - spent);
                if (remainingUses <= 0)
                    return 0;

                if (project.TryGetProperty("projectOutcomeAudit", out var audit) && audit.ValueKind == JsonValueKind.Object)
                    return GetIntOrDefault(audit, "guardianRarityCeilingBonusSteps");

                return GuardianProjectState.GetDefaultRelicForgingRarityBonusSteps(GetFirstNonEmptyString(project, "projectTier"));
            }
        }
        catch
        {
            // ignored
        }

        return 0;
    }


    private static int ReadAvailableLoreResearchQuestTokens(JsonElement? trackerRoot, string guardianId, string sourceProjectId, string questOrigin)
    {
        if (trackerRoot == null || trackerRoot.Value.ValueKind != JsonValueKind.Object ||
            !trackerRoot.Value.TryGetProperty("completedProjects", out var completedProjects) ||
            completedProjects.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetFirstNonEmptyString(project, "projectType"), "lore_research", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetFirstNonEmptyString(project, "finalState"), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!project.TryGetProperty("effectState", out var effectState) || effectState.ValueKind != JsonValueKind.Object)
                return 0;

            if (string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase))
                return Math.Max(0, GetIntOrDefault(effectState, "specialQuestLineTokensGranted") - GetIntOrDefault(effectState, "specialQuestLineTokensSpent"));

            if (string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
                return Math.Max(0, GetIntOrDefault(effectState, "guaranteedArchiveQuestGranted") - GetIntOrDefault(effectState, "guaranteedArchiveQuestConsumed"));

            return Math.Max(0, GetIntOrDefault(effectState, "questHookTokensGranted") - GetIntOrDefault(effectState, "questHookTokensSpent"));
        }

        return 0;
    }


    private static int ReadGrantedLoreResearchQuestTokens(JsonElement? trackerRoot, string guardianId, string sourceProjectId, string questOrigin)
    {
        if (trackerRoot == null || trackerRoot.Value.ValueKind != JsonValueKind.Object ||
            !trackerRoot.Value.TryGetProperty("completedProjects", out var completedProjects) ||
            completedProjects.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetFirstNonEmptyString(project, "projectType"), "lore_research", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!project.TryGetProperty("effectState", out var effectState) || effectState.ValueKind != JsonValueKind.Object)
                return 0;

            if (string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase))
                return GetIntOrDefault(effectState, "specialQuestLineTokensGranted");

            if (string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
                return GetIntOrDefault(effectState, "guaranteedArchiveQuestGranted");

            return GetIntOrDefault(effectState, "questHookTokensGranted");
        }

        return 0;
    }


    private static int ReadGrantedLoreResearchVisibleClueBudget(JsonElement? trackerRoot, string guardianId, string sourceProjectId)
    {
        if (trackerRoot == null || trackerRoot.Value.ValueKind != JsonValueKind.Object ||
            !trackerRoot.Value.TryGetProperty("completedProjects", out var completedProjects) ||
            completedProjects.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        foreach (var entry in completedProjects.EnumerateArray())
        {
            if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetFirstNonEmptyString(project, "projectType"), "lore_research", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (project.TryGetProperty("effectState", out var effectState) &&
                effectState.ValueKind == JsonValueKind.Object &&
                TryReadIntField(effectState, "visibleRivalClueBudgetGranted", out var granted))
            {
                return granted;
            }

            if (project.TryGetProperty("projectOutcomeAudit", out var audit) &&
                audit.ValueKind == JsonValueKind.Object &&
                TryReadIntField(audit, "visibleRivalClueBonus", out var auditGranted))
            {
                return auditGranted;
            }

            return 0;
        }

        return 0;
    }


    private void ValidateGuardianGachaState(JsonElement guardian, string guardianContext, List<ValidationIssue> issues)
    {
        if (!guardian.TryGetProperty("gachaSystem", out var gachaSystem) || gachaSystem.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{guardianContext}.gachaSystem",
                IssueSeverity.Error,
                "Canonical guardian state должен содержать gachaSystem",
                code: "guardian_state_missing_gacha_system",
                section: "Guardians",
                repairHint: "Сохраняй в guardian state gachaSystem с chargesPerReturn, chargesUsedThisReturn и gachaHistory."));
            return;
        }

        if (!RequireObject(gachaSystem, $"{guardianContext}.gachaSystem", issues))
            return;

        var gachaContext = $"{guardianContext}.gachaSystem";
        ValidateNonNegativeNumberField(gachaSystem, gachaContext, issues, "chargesPerReturn");
        ValidateNonNegativeNumberField(gachaSystem, gachaContext, issues, "chargesUsedThisReturn");

        if (!gachaSystem.TryGetProperty("gachaHistory", out var gachaHistory) || gachaHistory.ValueKind == JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{gachaContext}.gachaHistory",
                IssueSeverity.Error,
                "Canonical guardian gachaSystem должен содержать gachaHistory array",
                code: "guardian_gacha_history_missing",
                section: "Guardians",
                repairHint: "Сохраняй gachaHistory как canonical массив попыток с timestamp, costInFeathers, relicId и finalRarity."));
        }
        else
        {
            RequireArrayOfObjects(gachaHistory, $"{gachaContext}.gachaHistory", issues);
            if (gachaHistory.ValueKind == JsonValueKind.Array)
            {
                var historyIndex = 0;
                foreach (var entry in gachaHistory.EnumerateArray())
                {
                    var entryContext = $"{gachaContext}.gachaHistory[{historyIndex++}]";
                    if (!RequireObject(entry, entryContext, issues))
                        continue;

                    var timestamp = RequireString(entry, entryContext, issues, "timestamp");
                    ValidatePositiveNumberField(entry, entryContext, issues, "costInFeathers");
                    RequireString(entry, entryContext, issues, "relicId");
                    var finalRarity = RequireString(entry, entryContext, issues, "finalRarity");

                    if (!string.IsNullOrWhiteSpace(timestamp) && !DateTimeOffset.TryParse(timestamp, out _))
                    {
                        issues.Add(new ValidationIssue(
                            $"{entryContext}.timestamp",
                            IssueSeverity.Error,
                            "Guardian gachaHistory.timestamp должен быть ISO 8601 timestamp",
                            code: "guardian_gacha_history_invalid_timestamp",
                            section: "Guardians",
                            expected: "ISO 8601 timestamp",
                            actual: timestamp,
                            repairHint: "Сохраняй timestamp в gachaHistory как ISO 8601 строку."));
                    }

                    if (!string.IsNullOrWhiteSpace(finalRarity) && GetRarityRank(finalRarity) == 0)
                    {
                        issues.Add(new ValidationIssue(
                            $"{entryContext}.finalRarity",
                            IssueSeverity.Error,
                            "Guardian gachaHistory.finalRarity должен быть canonical rarity",
                            code: "guardian_gacha_history_invalid_final_rarity",
                            section: "Guardians",
                            expected: "common | uncommon | rare | epic | legendary",
                            actual: finalRarity,
                            repairHint: "Сохраняй finalRarity как одну из canonical rarity tiers."));
                    }
                }
            }
        }

        if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
            relationshipData.ValueKind == JsonValueKind.Object &&
            relationshipData.TryGetProperty("currentReputation", out var currentReputation) &&
            currentReputation.ValueKind == JsonValueKind.Number &&
            currentReputation.TryGetInt32(out var parsedReputation) &&
            gachaSystem.TryGetProperty("chargesPerReturn", out var chargesPerReturnNode) &&
            chargesPerReturnNode.ValueKind == JsonValueKind.Number &&
            chargesPerReturnNode.TryGetInt32(out var chargesPerReturn))
        {
            var expectedCharges = GetExpectedGuardianGachaCharges(parsedReputation, AbodePowerRules.GetCurrentPower(guardian));
            if (chargesPerReturn != expectedCharges)
            {
                issues.Add(new ValidationIssue(
                    $"{gachaContext}.chargesPerReturn",
                    IssueSeverity.Error,
                    "Guardian gachaSystem.chargesPerReturn должен совпадать с reputation tier + abode power bonus",
                    code: "guardian_gacha_charges_tier_mismatch",
                    section: "Guardians",
                    expected: expectedCharges.ToString(),
                    actual: chargesPerReturn.ToString(),
                    repairHint: "Синхронизируй chargesPerReturn с guardian reputation tier и bonusGachaCharges от текущей силы Обители."));
            }
        }

        if (gachaSystem.TryGetProperty("chargesPerReturn", out var chargesPerReturnNodeForUsage) &&
            gachaSystem.TryGetProperty("chargesUsedThisReturn", out var chargesUsedNode) &&
            chargesPerReturnNodeForUsage.ValueKind == JsonValueKind.Number &&
            chargesUsedNode.ValueKind == JsonValueKind.Number &&
            chargesPerReturnNodeForUsage.TryGetInt32(out var chargesPerReturnForUsage) &&
            chargesUsedNode.TryGetInt32(out var chargesUsedThisReturn) &&
            chargesUsedThisReturn > chargesPerReturnForUsage)
        {
            issues.Add(new ValidationIssue(
                $"{gachaContext}.chargesUsedThisReturn",
                IssueSeverity.Error,
                "chargesUsedThisReturn не может превышать chargesPerReturn"));
        }
    }


    private static int GetExpectedGuardianGachaCharges(int currentReputation, int currentPower)
        => GuardianGachaChargeRules.GetChargesPerReturnForReputation(currentReputation, currentPower);


    private void CompareGuardianGachaState(JsonElement activeGuardian, string activeGuardianContext,
        JsonElement guardianFromArray, string guardianArrayContext, List<ValidationIssue> issues)
    {
        if (!activeGuardian.TryGetProperty("gachaSystem", out var activeGachaSystem) || activeGachaSystem.ValueKind != JsonValueKind.Object)
            return;
        if (!guardianFromArray.TryGetProperty("gachaSystem", out var arrayGachaSystem) || arrayGachaSystem.ValueKind != JsonValueKind.Object)
            return;

        if (TryReadIntField(activeGachaSystem, "chargesPerReturn", out var activeChargesPerReturn) &&
            TryReadIntField(arrayGachaSystem, "chargesPerReturn", out var arrayChargesPerReturn) &&
            activeChargesPerReturn != arrayChargesPerReturn)
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.gachaSystem.chargesPerReturn",
                IssueSeverity.Error,
                $"activeGuardian расходится с {guardianArrayContext}.gachaSystem.chargesPerReturn"));
        }

        if (TryReadIntField(activeGachaSystem, "chargesUsedThisReturn", out var activeChargesUsed) &&
            TryReadIntField(arrayGachaSystem, "chargesUsedThisReturn", out var arrayChargesUsed) &&
            activeChargesUsed != arrayChargesUsed)
        {
            issues.Add(new ValidationIssue(
                $"{activeGuardianContext}.gachaSystem.chargesUsedThisReturn",
                IssueSeverity.Error,
                $"activeGuardian расходится с {guardianArrayContext}.gachaSystem.chargesUsedThisReturn"));
        }
    }
}
