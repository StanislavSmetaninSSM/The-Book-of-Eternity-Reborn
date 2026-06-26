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
    private Dictionary<string, GuardianSequentialState> CollectKnownGuardianSequentialStatesForCommandValidation(
        GuardianPolicyContext? guardianPolicyContext = null)
    {
        guardianPolicyContext ??= _guardianPolicyContextInProgress ?? ResolveGuardianPolicyContextSync();
        var states = new Dictionary<string, GuardianSequentialState>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetGuardianPreTurnBaselineRootForCommandAuthorization(guardianPolicyContext, out var baselineRoot))
            return states;
        if (baselineRoot.ValueKind != JsonValueKind.Object ||
            !baselineRoot.TryGetProperty("guardians", out var guardians) ||
            guardians.ValueKind != JsonValueKind.Array)
        {
            return states;
        }

        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (!string.IsNullOrWhiteSpace(guardianId))
                states[guardianId] = ParseGuardianSequentialState(guardian);
        }

        return states;
    }


    private static GuardianSequentialState CloneGuardianSequentialState(GuardianSequentialState source)
    {
        var clone = new GuardianSequentialState
        {
            CurrentReputation = source.CurrentReputation,
            CurrentAbodePower = source.CurrentAbodePower,
            FounderExtraGachaCharges = source.FounderExtraGachaCharges,
            ChargesUsedThisReturn = source.ChargesUsedThisReturn
        };
        clone.AvailableQuestIds.UnionWith(source.AvailableQuestIds);
        clone.ActiveQuestIds.UnionWith(source.ActiveQuestIds);
        foreach (var (questId, difficulty) in source.QuestDifficultyById)
            clone.QuestDifficultyById[questId] = difficulty;
        foreach (var (questId, status) in source.ActiveQuestStatusById)
            clone.ActiveQuestStatusById[questId] = status;

        return clone;
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
        var activeQuestStatusById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guardian.TryGetProperty("questManagement", out var questManagement) &&
            questManagement.ValueKind == JsonValueKind.Object)
        {
            CollectQuestIdsFromGuardianQuestArray(questManagement, "availableQuests", availableQuestIds, questDifficultyById);
            CollectQuestIdsFromGuardianQuestArray(questManagement, "activeQuests", activeQuestIds, questDifficultyById, activeQuestStatusById);
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
            FounderExtraGachaCharges = PlayerGuardianFoundationState.GetFounderExtraGachaCharges(guardian),
            ChargesUsedThisReturn = chargesUsedThisReturn
        };
        state.AvailableQuestIds.UnionWith(availableQuestIds);
        state.ActiveQuestIds.UnionWith(activeQuestIds);
        foreach (var (questId, difficulty) in questDifficultyById)
            state.QuestDifficultyById[questId] = difficulty;
        foreach (var (questId, status) in activeQuestStatusById)
            state.ActiveQuestStatusById[questId] = status;
        return state;
    }


    private static void CollectQuestIdsFromGuardianQuestArray(
        JsonElement questManagement,
        string propName,
        HashSet<string> target,
        Dictionary<string, string>? difficultyById = null,
        Dictionary<string, string>? statusById = null)
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
                var status = GetFirstNonEmptyString(quest, "status");
                if (statusById != null && !string.IsNullOrWhiteSpace(status))
                    statusById[questId] = status;
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
            if (!TryResolveGuardianProjectTrackerValidationRoot(
                    $"{auditContext}.relicForgingBonusSteps",
                    "gachaBonusAudit.relicForgingBonusSteps требует readable current guardian project tracker authority и не использует forge bonus fallback без canonical tracker provenance.",
                    "guardian_process_gacha_bonus_audit_missing_current_tracker_authority",
                    "UpdateGuardians.processGacha",
                    $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед audit relic forging bonus steps.",
                    issues,
                    out var trackerRoot))
            {
                return;
            }

            var trackerMatch = ResolveAvailableForgeGachaBonusStepsFromTrackerJson(
                trackerRoot.GetRawText(),
                guardianId,
                sourceProjectId ?? string.Empty);
            var availableForgeSteps = trackerMatch.HasMatch
                ? trackerMatch.AvailableSteps
                : 0;
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
        var hasProjectCommandFields =
            root.TryGetProperty("startGuardianProjects", out _) ||
            root.TryGetProperty("guardianProjectUpdates", out _) ||
            root.TryGetProperty("completeGuardianProjects", out _);
        var hasDirectTrackerState =
            root.TryGetProperty("activeProjects", out _) ||
            root.TryGetProperty("completedProjects", out _) ||
            root.TryGetProperty("temporaryProjectModifiers", out _);
        var hasAuthoritativeDirectTrackerState = GuardianProjectTrackerHasAuthorityData(root);
        var requiresProjectAuthority = hasProjectCommandFields || hasAuthoritativeDirectTrackerState;
        if (!isTrackerFile && !hasProjectCommandFields)
            return;

        if (requiresProjectAuthority &&
            !TryRequireGuardianPreTurnBaseline(
                contextPrefix,
                "Guardian project/gacha validation требует readable validated pre-turn guardians baseline и не использует current guardians[] как fallback authority.",
                "guardian_project_missing_validated_preturn_guardians_snapshot",
                "GuardianProjects",
                "Сохраняй validated snapshot copy game_state/meta/guardians.json для guardian project/gacha turns. Без этого project politics и guardian command sequencing не должны выводиться из current guardians[].",
                issues))
        {
            return;
        }

        if (requiresProjectAuthority &&
            !TryRequireGuardianProjectTrackerPreTurnBaseline(
                contextPrefix,
                "Guardian project/gacha validation требует readable validated pre-turn project tracker baseline и не использует current tracker state как fallback authority.",
                "guardian_project_missing_validated_preturn_tracker_snapshot",
                "Guardian project/gacha validation требует semantically valid validated pre-turn project tracker baseline и не использует broken tracker snapshot как fallback authority.",
                "guardian_project_invalid_validated_preturn_tracker_snapshot",
                "GuardianProjects",
                $"Сохраняй validated snapshot copy {GuardianProjectState.TrackerPath} для guardian project/gacha turns. Без этого project politics, active project identity и derived tracker state не должны выводиться из current tracker.",
                issues))
        {
            return;
        }

        var guardianIdentityState = ReadGuardianProjectIdentityValidationState();
        var relationshipScores = guardianIdentityState.RelationshipScores;
        var knownGuardianIds = guardianIdentityState.KnownGuardianIds;

        if (root.TryGetProperty("activeProjects", out var activeProjects))
            ValidateGuardianProjectEntryArray(activeProjects, $"{contextPrefix}.activeProjects", issues, completed: false, relationshipScores, knownGuardianIds);
        if (root.TryGetProperty("completedProjects", out var completedProjects))
            ValidateGuardianProjectEntryArray(completedProjects, $"{contextPrefix}.completedProjects", issues, completed: true, relationshipScores, knownGuardianIds);
        ValidateGuardianProjectIdentityCollisions(root, contextPrefix, issues);
        if (root.TryGetProperty("temporaryProjectModifiers", out var temporaryModifiers))
            ValidateGuardianProjectTemporaryModifiers(temporaryModifiers, $"{contextPrefix}.temporaryProjectModifiers", issues);

        if (isTrackerFile && requiresProjectAuthority)
            ValidateGuardianProjectMaterializedStateAgainstAuthority(root, contextPrefix, issues);

        var knownProjects = ReadKnownGuardianProjectKeysForValidation();
        var knownCompletedProjects = ReadKnownCompletedGuardianProjectKeysForValidation();
        var knownProjectDetails = ReadKnownGuardianProjectsForValidation();
        var knownActiveProjectIdsByGuardian = ReadKnownActiveGuardianProjectIdsByGuardianForValidation();
        var startedThisTurn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startedProjectDetails = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("startGuardianProjects", out var startCommands))
            ValidateGuardianProjectStartCommands(startCommands, $"{contextPrefix}.startGuardianProjects", issues, knownProjects, knownCompletedProjects, knownActiveProjectIdsByGuardian, startedThisTurn, startedProjectDetails, relationshipScores, knownGuardianIds);
        if (root.TryGetProperty("guardianProjectUpdates", out var updateCommands))
            ValidateGuardianProjectUpdateCommands(updateCommands, $"{contextPrefix}.guardianProjectUpdates", issues, knownProjects, startedThisTurn, knownGuardianIds);
        if (root.TryGetProperty("completeGuardianProjects", out var completeCommands))
            ValidateGuardianProjectCompletionCommands(completeCommands, $"{contextPrefix}.completeGuardianProjects", issues, knownProjects, knownProjectDetails, startedProjectDetails, startedThisTurn, relationshipScores, knownGuardianIds);
    }

    private static bool GuardianProjectTrackerHasAuthorityData(JsonElement root)
    {
        return HasNonEmptyArray(root, "activeProjects") ||
               HasNonEmptyArray(root, "completedProjects") ||
               HasNonEmptyArray(root, "temporaryProjectModifiers");

        static bool HasNonEmptyArray(JsonElement owner, string propertyName)
        {
            return owner.TryGetProperty(propertyName, out var array) &&
                   array.ValueKind == JsonValueKind.Array &&
                   array.GetArrayLength() > 0;
        }
    }

    private bool TryRequireGuardianPreTurnBaseline(
        string path,
        string message,
        string code,
        string section,
        string repairHint,
        List<ValidationIssue> issues)
    {
        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        if (HasResolvedGenericSharedStrictPreTurnGuardianAuthority(guardianPolicyContext))
            return true;

        if (TryGetGuardianBaselineFailureKind(guardianPolicyContext, out var failureKind) &&
            IsIdleStateWithoutActiveTurn(failureKind))
            return true;

        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: "current validated pending turn snapshot with readable game_state/meta/guardians.json",
            actual: DescribeGuardianPreTurnBaselineFailure(guardianPolicyContext),
            repairHint: repairHint));
        return false;
    }

    private bool TryRequireGuardianProjectTrackerPreTurnBaseline(
        string path,
        string missingMessage,
        string missingCode,
        string invalidMessage,
        string invalidCode,
        string section,
        string repairHint,
        List<ValidationIssue> issues)
    {
        if (TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(out _, out _, out var trackerFailureDescription))
            return true;

        var (message, code) = ResolveGuardianProjectTrackerPreTurnBaselineIssueSurface(
            missingMessage,
            missingCode,
            invalidMessage,
            invalidCode,
            trackerFailureDescription);

        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: $"validated shared pre-turn {GuardianProjectState.TrackerPath} authority root",
            actual: trackerFailureDescription,
            repairHint: repairHint));
        return false;
    }

    private bool TryRequireGuardianProjectCurrentAuthority(
        string path,
        string baselineMissingMessage,
        string baselineMissingCode,
        string baselineInvalidMessage,
        string baselineInvalidCode,
        string currentAuthorityMessage,
        string currentAuthorityCode,
        string section,
        string baselineRepairHint,
        string currentAuthorityRepairHint,
        List<ValidationIssue> issues)
    {
        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(
                out _,
                out _,
                out var preTurnTrackerAuthorityFailureDescription))
        {
            var (baselineMessage, baselineCode) = ResolveGuardianProjectTrackerPreTurnBaselineIssueSurface(
                baselineMissingMessage,
                baselineMissingCode,
                baselineInvalidMessage,
                baselineInvalidCode,
                preTurnTrackerAuthorityFailureDescription);

            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                baselineMessage,
                code: baselineCode,
                section: section,
                expected: $"validated shared pre-turn {GuardianProjectState.TrackerPath} authority root",
                actual: preTurnTrackerAuthorityFailureDescription,
                repairHint: baselineRepairHint));
            return false;
        }

        if (TryResolveStrictGuardianProjectTrackerAuthorityRootForValidation(
                out _,
                out var strictTrackerAuthorityFailureDescription))
            return true;

        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            currentAuthorityMessage,
            code: currentAuthorityCode,
            section: section,
            expected: $"strict guardian-backed current {GuardianProjectState.TrackerPath} authority root",
            actual: strictTrackerAuthorityFailureDescription,
            repairHint: currentAuthorityRepairHint));
        return false;
    }

    private static (string Message, string Code) ResolveGuardianProjectTrackerPreTurnBaselineIssueSurface(
        string missingMessage,
        string missingCode,
        string invalidMessage,
        string invalidCode,
        string failureDescription)
    {
        return failureDescription.Contains("semantically invalid", StringComparison.OrdinalIgnoreCase)
            ? (invalidMessage, invalidCode)
            : (missingMessage, missingCode);
    }

    private bool TryResolveGuardianProjectTrackerValidationRoot(
        string path,
        string message,
        string code,
        string section,
        string repairHint,
        List<ValidationIssue> issues,
        out JsonElement trackerRoot)
        => TryResolveGuardianProjectTrackerValidationRoot(
            path,
            message,
            code,
            section,
            repairHint,
            issues,
            out trackerRoot,
            out _);

    private bool TryResolveGuardianProjectTrackerValidationRoot(
        string path,
        string message,
        string code,
        string section,
        string repairHint,
        List<ValidationIssue> issues,
        out JsonElement trackerRoot,
        out GuardianProjectTrackerPolicyContext trackerContext)
    {
        if (TryResolveGuardianProjectTrackerValidationRootSync(out trackerRoot, out trackerContext, out var trackerFailureDescription))
            return true;

        trackerContext = null!;
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: section,
            expected: $"shared-strict guardian-backed current {GuardianProjectState.TrackerPath} authority root",
            actual: trackerFailureDescription,
            repairHint: repairHint));
        return false;
    }

    private static string DescribeGuardianProjectTrackerAuthorityFailure(GuardianProjectTrackerPolicyContext trackerContext)
    {
        if (!HasUsableValidatedPreTurnGuardianProjectTrackerBaseline(trackerContext))
            return DescribeGuardianTrackedSnapshotFileStatus(trackerContext.PreTurnTrackerSnapshot.FileStatus);

        return trackerContext.CurrentStateFailureKind switch
        {
            GuardianCurrentStateFailureKind.MissingCurrentState => $"current {GuardianProjectState.TrackerPath} is missing",
            GuardianCurrentStateFailureKind.UnreadableCurrentState => $"current {GuardianProjectState.TrackerPath} is unreadable or malformed",
            GuardianCurrentStateFailureKind.SemanticallyInvalidCurrentState => trackerContext.CurrentStateFailureDescription ?? $"current {GuardianProjectState.TrackerPath} is semantically invalid",
            _ => $"current {GuardianProjectState.TrackerPath} authority root is unavailable"
        };
    }

    private void ValidateGuardianProjectMaterializedStateAgainstAuthority(
        JsonElement currentTrackerRoot,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!TryResolveGuardianProjectTrackerValidationRoot(
                contextPrefix,
                "Current guardian project tracker materialized state requires readable current tracker authority and cannot be compared against a missing authority root.",
                "guardian_project_materialized_state_missing_current_tracker_authority",
                "GuardianProjects",
                $"Исправь current {GuardianProjectState.TrackerPath} так, чтобы validator мог построить shared-strict current tracker authority root перед сравнением materialized tracker arrays.",
                issues,
                out var trackerAuthorityRoot))
        {
            return;
        }

        ValidateGuardianProjectMaterializedArrayAgainstAuthority(
            currentTrackerRoot,
            trackerAuthorityRoot,
            contextPrefix,
            "activeProjects",
            BuildGuardianProjectMaterializedEntryMap,
            issues);
        ValidateGuardianProjectMaterializedArrayAgainstAuthority(
            currentTrackerRoot,
            trackerAuthorityRoot,
            contextPrefix,
            "completedProjects",
            BuildGuardianProjectMaterializedEntryMap,
            issues);
        ValidateGuardianProjectMaterializedArrayAgainstAuthority(
            currentTrackerRoot,
            trackerAuthorityRoot,
            contextPrefix,
            "temporaryProjectModifiers",
            BuildGuardianProjectMaterializedModifierMap,
            issues);
    }

    private void ValidateGuardianProjectMaterializedArrayAgainstAuthority(
        JsonElement currentTrackerRoot,
        JsonElement authorityRoot,
        string contextPrefix,
        string propertyName,
        Func<JsonElement, Dictionary<string, JsonElement>?> mapBuilder,
        List<ValidationIssue> issues)
    {
        if (!currentTrackerRoot.TryGetProperty(propertyName, out var currentArray) ||
            currentArray.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var currentEntries = mapBuilder(currentArray);
        if (currentEntries == null)
            return;

        Dictionary<string, JsonElement> authorityEntries;
        if (authorityRoot.ValueKind == JsonValueKind.Object &&
            authorityRoot.TryGetProperty(propertyName, out var authorityArray) &&
            authorityArray.ValueKind == JsonValueKind.Array)
        {
            authorityEntries = mapBuilder(authorityArray) ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
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
            $"{contextPrefix}.{propertyName}",
            IssueSeverity.Error,
            $"Current {propertyName} must match kernel-authoritative guardian project tracker state reconstructed from validated pre-turn baseline and same-turn project commands.",
            code: "guardian_project_materialized_state_outside_authority",
            section: "GuardianProjects",
            expected: $"kernel-authoritative {propertyName} only",
            actual: $"materialized current {propertyName} diverges from kernel authority view",
            repairHint: $"Rewrite {propertyName} to match the tracker state reconstructed from validated pre-turn baseline plus authorized same-turn guardian project commands. Current materialized tracker state is not an authority source by itself."));
    }

    private static Dictionary<string, JsonElement>? BuildGuardianProjectMaterializedEntryMap(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in array.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(entry, "guardianId");
            var projectId = entry.TryGetProperty("project", out var project)
                ? GetFirstNonEmptyString(project, "projectId")
                : null;
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var key = GuardianProjectState.BuildKey(guardianId, projectId);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (result.ContainsKey(key))
                return null;

            result[key] = entry;
        }

        return result;
    }

    private static Dictionary<string, JsonElement>? BuildGuardianProjectMaterializedModifierMap(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return null;

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in array.EnumerateArray())
        {
            var key = BuildGuardianProjectModifierKey(entry);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (result.ContainsKey(key))
                return null;

            result[key] = entry;
        }

        return result;
    }

    private static string BuildGuardianProjectModifierKey(JsonElement entry)
    {
        var guardianId = GetFirstNonEmptyString(entry, "guardianId");
        var modifierId = GetFirstNonEmptyString(entry, "modifierId");
        if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(modifierId))
            return string.Empty;

        return $"{guardianId}::{modifierId}";
    }

    private sealed record GuardianProjectValidationSnapshot(
        string? ProjectType,
        string? TargetGuardianId,
        string? BetrayalReason);

    private enum GuardianPowerJournalRepairStatus
    {
        Unchanged,
        Canonicalized,
        Irreparable
    }

    private sealed record GuardianPowerJournalRepairValidationResult(
        JsonElement EffectiveEntry,
        GuardianPowerJournalRepairStatus Status);

    private sealed record PoliticalGuardianPowerEventProjectSnapshot(
        string ProjectGuardianId,
        string ProjectId,
        string? ProjectName,
        string? ProjectType,
        string? ProjectTier,
        string? FinalState,
        string? TargetGuardianId);

    private sealed record GuardianIdentityValidationState(
        HashSet<string> KnownGuardianIds,
        HashSet<string> KnownGuardianNames,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> RelationshipScores);

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ReadGuardianRelationshipScoresForValidation()
        => ReadGuardianIdentityValidationState().RelationshipScores;

    private GuardianIdentityValidationState ReadGuardianProjectIdentityValidationState()
    {
        var knownGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownGuardianNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relationshipScores = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        var hasValidatedPreTurnBaseline =
            TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var preTurnAuthorityRoot);
        if (!hasValidatedPreTurnBaseline && !guardianPolicyContext.HasStrictCurrentAuthorityRoot)
            return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
        if (hasValidatedPreTurnBaseline)
        {
            MergeGuardianIdentityValidationStateFromStoredGuardians(
                preTurnAuthorityRoot,
                knownGuardianIds,
                relationshipScores);
        }

        try
        {
            var guardianIdsWithCurrentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (guardianPolicyContext.HasStrictCurrentAuthorityRoot)
            {
                MergeGuardianIdentityValidationStateFromStoredGuardians(
                    guardianPolicyContext.StrictCurrentAuthorityRoot,
                    knownGuardianIds,
                    relationshipScores);
                CollectGuardianIdentityNamesFromStoredGuardians(
                    guardianPolicyContext.StrictCurrentAuthorityRoot,
                    knownGuardianIds,
                    knownGuardianNames,
                    guardianIdsWithCurrentNames,
                    onlyAuthorizedGuardians: false);
            }

            if (hasValidatedPreTurnBaseline)
            {
                CollectGuardianIdentityNamesFromStoredGuardians(
                    preTurnAuthorityRoot,
                    knownGuardianIds,
                    knownGuardianNames,
                    guardianIdsWithCurrentNames,
                    onlyAuthorizedGuardians: false,
                    skipGuardianIds: guardianIdsWithCurrentNames);
            }
        }
        catch
        {
            return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
        }

        return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
    }

    private GuardianIdentityValidationState ReadGuardianIdentityValidationState()
    {
        var knownGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownGuardianNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relationshipScores = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var guardianPolicyContext = ResolveGuardianPolicyContextSync();
        var hasValidatedPreTurnBaseline =
            TryGetGenericSharedStrictPreTurnGuardianAuthorityRoot(guardianPolicyContext, out var preTurnAuthorityRoot);
        if (!hasValidatedPreTurnBaseline && !guardianPolicyContext.HasStrictCurrentAuthorityRoot)
            return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
        if (hasValidatedPreTurnBaseline)
        {
            MergeGuardianIdentityValidationStateFromStoredGuardians(
                preTurnAuthorityRoot,
                knownGuardianIds,
                relationshipScores);
        }

        try
        {
            var guardianIdsWithCurrentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (guardianPolicyContext.HasStrictCurrentAuthorityRoot)
            {
                MergeGuardianIdentityValidationStateFromStoredGuardians(
                    guardianPolicyContext.StrictCurrentAuthorityRoot,
                    knownGuardianIds,
                    relationshipScores);
                CollectGuardianIdentityNamesFromStoredGuardians(
                    guardianPolicyContext.StrictCurrentAuthorityRoot,
                    knownGuardianIds,
                    knownGuardianNames,
                    guardianIdsWithCurrentNames,
                    onlyAuthorizedGuardians: false);
            }

            if (hasValidatedPreTurnBaseline)
            {
                CollectGuardianIdentityNamesFromStoredGuardians(
                    preTurnAuthorityRoot,
                    knownGuardianIds,
                    knownGuardianNames,
                    guardianIdsWithCurrentNames,
                    onlyAuthorizedGuardians: false,
                    skipGuardianIds: guardianIdsWithCurrentNames);
            }
        }
        catch
        {
            return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
        }

        return new GuardianIdentityValidationState(knownGuardianIds, knownGuardianNames, relationshipScores);
    }

    private HashSet<string> ReadKnownGuardianIdsForProjectValidation()
        => ReadGuardianIdentityValidationState().KnownGuardianIds;

    private static bool MergeGuardianIdentityValidationStateFromStoredGuardians(
        JsonElement root,
        HashSet<string> knownGuardianIds,
        IDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        bool onlyAuthorizedGuardians = false)
    {
        if (!root.TryGetProperty("guardians", out var guardians) || guardians.ValueKind != JsonValueKind.Array)
            return false;

        var mergedAny = false;
        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            if (onlyAuthorizedGuardians && !knownGuardianIds.Contains(guardianId))
                continue;

            MergeGuardianIdentityValidationState(guardian, knownGuardianIds, relationshipScores);
            mergedAny = true;
        }

        return mergedAny;
    }

    private static void MergeGuardianIdentityValidationState(
        JsonElement guardian,
        HashSet<string> knownGuardianIds,
        IDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores)
    {
        if (guardian.ValueKind != JsonValueKind.Object)
            return;

        var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
        if (string.IsNullOrWhiteSpace(guardianId))
            return;

        knownGuardianIds.Add(guardianId);
        var scoresByTarget = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (guardian.TryGetProperty("guardianRelationships", out var relationships) && relationships.ValueKind == JsonValueKind.Array)
        {
            foreach (var relationship in relationships.EnumerateArray())
            {
                if (relationship.ValueKind != JsonValueKind.Object)
                    continue;

                var targetGuardianId = GetFirstNonEmptyString(relationship, "targetGuardianId");
                if (string.IsNullOrWhiteSpace(targetGuardianId) ||
                    string.Equals(targetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scoresByTarget[targetGuardianId] = ResolveGuardianRelationshipScoreForValidation(relationship);
            }
        }

        relationshipScores[guardianId] = scoresByTarget;
    }

    private static void RegisterGuardianIdentityNames(
        JsonElement guardian,
        HashSet<string> knownGuardianNames)
    {
        if (guardian.ValueKind != JsonValueKind.Object)
            return;

        var guardianName = GuardianManifestation.GetDisplayName(guardian);
        var canonicalName = GuardianManifestation.GetCanonicalName(guardian);
        if (!string.IsNullOrWhiteSpace(guardianName))
            knownGuardianNames.Add(guardianName);
        if (!string.IsNullOrWhiteSpace(canonicalName))
            knownGuardianNames.Add(canonicalName);
    }

    private static void CollectGuardianIdentityNamesFromStoredGuardians(
        JsonElement root,
        HashSet<string> knownGuardianIds,
        HashSet<string> knownGuardianNames,
        HashSet<string> coveredGuardianIds,
        bool onlyAuthorizedGuardians,
        HashSet<string>? skipGuardianIds = null)
    {
        if (!root.TryGetProperty("guardians", out var guardians) || guardians.ValueKind != JsonValueKind.Array)
            return;

        foreach (var guardian in guardians.EnumerateArray())
        {
            var guardianId = GetFirstNonEmptyString(guardian, "guardianId", "id");
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            if (onlyAuthorizedGuardians && !knownGuardianIds.Contains(guardianId))
                continue;

            if (skipGuardianIds != null && skipGuardianIds.Contains(guardianId))
                continue;

            RegisterGuardianIdentityNames(guardian, knownGuardianNames);
            coveredGuardianIds.Add(guardianId);
        }
    }

    private static int ResolveGuardianRelationshipScoreForValidation(JsonElement relationship)
    {
        if (relationship.TryGetProperty("attitudeScore", out var scoreNode) &&
            scoreNode.ValueKind == JsonValueKind.Number &&
            scoreNode.TryGetInt32(out var attitudeScore))
        {
            return Math.Clamp(attitudeScore, GuardianRelationshipRules.MinAttitudeScore, GuardianRelationshipRules.MaxAttitudeScore);
        }

        var tier = GetFirstNonEmptyString(relationship, "attitudeTier", "attitude");
        return Math.Clamp(
            GuardianRelationshipRules.ResolveLegacyScore(tier),
            GuardianRelationshipRules.MinAttitudeScore,
            GuardianRelationshipRules.MaxAttitudeScore);
    }

    private static bool RequiresGuardianPoliticalBetrayalReason(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        string? sourceGuardianId,
        string? targetGuardianId)
    {
        if (string.IsNullOrWhiteSpace(sourceGuardianId) || string.IsNullOrWhiteSpace(targetGuardianId))
            return false;

        return relationshipScores.TryGetValue(sourceGuardianId, out var scoresByTarget) &&
               scoresByTarget.TryGetValue(targetGuardianId, out var score) &&
               GuardianRelationshipRules.RequiresBetrayalReason(score);
    }

    private static bool IsGuardianPoliticalProjectType(string? projectType) =>
        string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase);


    private void ValidateGuardianPowerEventData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("guardianPowerEvents", out var events))
            return;

        var context = $"{contextPrefix}.guardianPowerEvents";
        RequireArrayOfObjects(events, context, issues);
        if (events.ValueKind != JsonValueKind.Array)
            return;
        if (!HasNonEmptyGuardianPowerEventArray(events))
            return;

        if (!TryRequireGuardianPreTurnBaseline(
                context,
                "Guardian power event validation требует readable validated pre-turn guardians baseline и не использует current guardian/project state как fallback authority.",
                "guardian_power_event_missing_validated_preturn_guardians_snapshot",
                "AbodePower",
                "Сохраняй validated snapshot copy game_state/meta/guardians.json для turns с guardianPowerEvents. Без этого validator не должен резолвить guardianId и source project из current-only state.",
                issues))
        {
            return;
        }

        var knownGuardianIds = ReadKnownGuardianIdsForProjectValidation();
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects =
            new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (GuardianPowerEventArrayRequiresProjectTrackerAuthority(events))
        {
            if (!TryRequireGuardianProjectCurrentAuthority(
                    context,
                    "Guardian power event validation требует readable validated pre-turn project tracker baseline и не использует current guardian/project state как fallback authority.",
                    "guardian_power_event_missing_validated_preturn_tracker_snapshot",
                    "Guardian power event validation требует semantically valid validated pre-turn project tracker baseline и не использует broken validated tracker snapshot как authority.",
                    "guardian_power_event_invalid_validated_preturn_tracker_snapshot",
                    "Guardian power event validation требует readable current guardian project tracker authority и не использует projected pre-turn tracker state как fallback.",
                    "guardian_power_event_missing_current_tracker_authority",
                    "AbodePower",
                    $"Сохраняй validated snapshot copy {GuardianProjectState.TrackerPath} для turns с guardianPowerEvents. Без этого validator не должен резолвить source project из current-only tracker state.",
                    $"Исправь current {GuardianProjectState.TrackerPath} так, чтобы validator мог построить strict current authority root. guardianPowerEvents не должны валидироваться по stale projected tracker state.",
                    issues))
            {
                return;
            }

            knownPoliticalProjects = ReadKnownPoliticalGuardianPowerEventProjectsForValidation();
        }

        var preTurnJournalIdentityResolution = ResolveValidatedPreTurnGuardianPowerJournalIdentityState();
        if (preTurnJournalIdentityResolution.Status != GuardianPowerJournalIdentityBaselineStatus.Resolved ||
            preTurnJournalIdentityResolution.IdentityState == null)
        {
            var issueCode = preTurnJournalIdentityResolution.Status == GuardianPowerJournalIdentityBaselineStatus.MissingValidatedSnapshotJournal
                ? "guardian_power_event_missing_validated_preturn_journal_snapshot"
                : "guardian_power_event_invalid_validated_preturn_journal_snapshot";
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Guardian power event identity validation требует readable validated pre-turn abode_power_journal baseline и не использует broken snapshot journal как permissive fallback.",
                code: issueCode,
                section: "AbodePower",
                expected: $"current validated pending turn snapshot with readable {GuardianPowerEventState.JournalPath}",
                actual: preTurnJournalIdentityResolution.FailureDescription,
                repairHint: $"Сохраняй validated snapshot copy {GuardianPowerEventState.JournalPath} для turns с guardianPowerEvents. Без baseline journal identity validator не должен резолвить append-only eventId и same-life resonance uniqueness."));
            return;
        }

        ValidateGuardianPowerEventArrayAgainstKnownContext(
            events,
            context,
            issues,
            knownGuardianIds,
            knownPoliticalProjects,
            preTurnJournalIdentityResolution.IdentityState);
    }

    private void ValidateGuardianPowerEventArrayAgainstKnownContext(
        JsonElement events,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        GuardianPowerJournalIdentityState? preTurnJournalIdentityState)
    {
        RequireArrayOfObjects(events, context, issues);
        if (events.ValueKind != JsonValueKind.Array)
            return;

        ValidateGuardianPowerEventEntriesAgainstKnownContext(
            CollectGuardianPowerEventEntriesForProof(events, context, proofRelevantReasonType: null),
            issues,
            knownGuardianIds,
            knownPoliticalProjects,
            preTurnJournalIdentityState);
    }

    private void ValidateGuardianPowerEventEntriesAgainstKnownContext(
        IReadOnlyList<(JsonElement Entry, string Context)> events,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        GuardianPowerJournalIdentityState? preTurnJournalIdentityState)
    {
        var effectiveEventsForIdentityValidation = new List<(JsonElement Entry, string Context)>();
        foreach (var (item, itemContext) in events)
        {
            if (!RequireObject(item, itemContext, issues))
                continue;

            effectiveEventsForIdentityValidation.Add((item, itemContext));

            RequireString(item, itemContext, issues, "eventId");
            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            ValidateKnownGuardianId($"{itemContext}.guardianId", guardianId, issues, knownGuardianIds);
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

            var sourceSurface = RequireString(item, itemContext, issues, "sourceSurface");
            var sourceId = RequireString(item, itemContext, issues, "sourceId");
            RequireString(item, itemContext, issues, "title");
            RequireString(item, itemContext, issues, "summary");
            ValidateOptionalNullableStringField(item, itemContext, issues, "relatedGuardianId");
            var relatedGuardianId = GetFirstNonEmptyString(item, "relatedGuardianId");
            ValidateOptionalKnownRelatedGuardianId(
                $"{itemContext}.relatedGuardianId",
                guardianId,
                relatedGuardianId,
                issues,
                knownGuardianIds);
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
                ValidateGuardianPowerEventAudit(
                    guardianId,
                    relatedGuardianId,
                    sourceSurface,
                    sourceId,
                    reasonType,
                    TryReadInt(item, "delta", out var powerEventDelta) ? powerEventDelta : null,
                    audit,
                    itemContext,
                    $"{itemContext}.audit",
                    issues,
                    knownPoliticalProjects);
                ValidateCompletionSourcedRivalStrikeEventContract(
                    item,
                    itemContext,
                    guardianId,
                    relatedGuardianId,
                    sourceSurface,
                    sourceId,
                    reasonType,
                    audit,
                    $"{itemContext}.audit",
                    issues,
                    knownPoliticalProjects,
                    journalSurface: false);
                ValidateUpdateSourcedRivalStrikeEventContract(
                    item,
                    itemContext,
                    sourceSurface,
                    reasonType,
                    issues);
            }
        }

        ValidateGuardianPowerEventIdentityContract(
            effectiveEventsForIdentityValidation,
            preTurnJournalIdentityState,
            issues);
    }

    private static bool TryCollectGuardianPowerEventEntriesForProof(
        JsonElement entries,
        string context,
        GuardianPowerEventProofScope? proofScope,
        out List<(JsonElement Entry, string Context)> result,
        out string failureDescription)
    {
        result = new List<(JsonElement Entry, string Context)>();
        failureDescription = "guardianPowerEvents proof surface unreadable";
        if (entries.ValueKind != JsonValueKind.Array)
            return false;

        if (proofScope == null)
        {
            result = CollectGuardianPowerEventEntriesForProof(entries, context, proofRelevantReasonType: null);
            failureDescription = string.Empty;
            return true;
        }

        var scope = proofScope.Value;
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{context}[{index++}]";
            if (!TryClassifyGuardianPowerEventProofRelevance(
                    entry,
                    entryContext,
                    scope,
                    out var isRelevant,
                    out failureDescription))
            {
                return false;
            }

            if (isRelevant)
                result.Add((entry, entryContext));
        }

        failureDescription = string.Empty;
        return true;
    }

    private static bool TryClassifyGuardianPowerEventProofRelevance(
        JsonElement entry,
        string entryContext,
        GuardianPowerEventProofScope proofScope,
        out bool isRelevant,
        out string failureDescription)
    {
        isRelevant = false;
        failureDescription = string.Empty;
        if (entry.ValueKind != JsonValueKind.Object)
        {
            failureDescription = $"{entryContext} must be an object to classify strict guardian power-event proof relevance";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(proofScope.GuardianId))
        {
            var guardianId = GetFirstNonEmptyString(entry, "guardianId");
            if (string.IsNullOrWhiteSpace(guardianId))
            {
                failureDescription =
                    $"{entryContext}.guardianId missing or empty; strict snapshot proof cannot determine whether this raw power event belongs to guardian-scoped proof";
                return false;
            }

            if (!string.Equals(guardianId, proofScope.GuardianId, StringComparison.OrdinalIgnoreCase))
            {
                isRelevant = false;
                return true;
            }
        }

        var reasonType = GetFirstNonEmptyString(entry, "reasonType");
        if (string.IsNullOrWhiteSpace(reasonType) || !GuardianPowerEventState.IsValidReasonType(reasonType))
        {
            failureDescription =
                $"{entryContext}.reasonType missing or unsupported; strict snapshot proof cannot classify this raw power event as relevant or irrelevant";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(proofScope.ReasonType) &&
            !string.Equals(reasonType, proofScope.ReasonType, StringComparison.OrdinalIgnoreCase))
        {
            isRelevant = false;
            return true;
        }

        if (string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(proofScope.OfferingType))
        {
            return TryClassifyGuardianOfferingPowerEventProofRelevance(
                entry,
                entryContext,
                proofScope,
                out isRelevant,
                out failureDescription);
        }

        isRelevant = true;
        return true;
    }

    private static bool TryClassifyGuardianOfferingPowerEventProofRelevance(
        JsonElement entry,
        string entryContext,
        GuardianPowerEventProofScope proofScope,
        out bool isRelevant,
        out string failureDescription)
    {
        isRelevant = false;
        failureDescription = string.Empty;
        if (!entry.TryGetProperty("audit", out var audit) || audit.ValueKind != JsonValueKind.Object)
        {
            failureDescription =
                $"{entryContext}.audit missing or invalid; strict snapshot offering proof cannot determine whether this raw event belongs to the validated request";
            return false;
        }

        if (!TryReadRequiredStrictString(audit, "offeringType", out var offeringType))
        {
            failureDescription =
                $"{entryContext}.audit.offeringType missing or invalid; strict snapshot offering proof cannot classify raw offering relevance";
            return false;
        }

        if (!string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            failureDescription =
                $"{entryContext}.audit.offeringType uses an unsupported value; strict snapshot offering proof cannot classify raw offering relevance";
            return false;
        }

        if (!string.Equals(offeringType, proofScope.OfferingType, StringComparison.OrdinalIgnoreCase))
        {
            isRelevant = false;
            return true;
        }

        if (!TryReadRequiredStrictString(audit, "returnCycleId", out var returnCycleId))
        {
            failureDescription =
                $"{entryContext}.audit.returnCycleId missing or invalid; strict snapshot offering proof cannot classify raw offering relevance";
            return false;
        }

        if (!string.Equals(returnCycleId, proofScope.ReturnCycleId, StringComparison.OrdinalIgnoreCase))
        {
            isRelevant = false;
            return true;
        }

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadStrictPositiveInt32(audit, "inkFeathersOffered", out var inkFeathersOffered))
            {
                failureDescription =
                    $"{entryContext}.audit.inkFeathersOffered missing or invalid; strict snapshot offering proof cannot classify raw offering relevance";
                return false;
            }

            isRelevant = proofScope.InkFeathersOffered.HasValue && inkFeathersOffered == proofScope.InkFeathersOffered.Value;
            return true;
        }

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadRequiredStrictString(audit, "relicId", out var relicId))
            {
                failureDescription =
                    $"{entryContext}.audit.relicId missing or invalid; strict snapshot offering proof cannot classify raw offering relevance";
                return false;
            }

            isRelevant = !string.IsNullOrWhiteSpace(proofScope.RelicId) &&
                         string.Equals(relicId, proofScope.RelicId, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (!TryReadRequiredStrictString(audit, "archiveId", out var archiveId))
        {
            failureDescription =
                $"{entryContext}.audit.archiveId missing or invalid; strict snapshot offering proof cannot classify raw offering relevance";
            return false;
        }

        isRelevant = !string.IsNullOrWhiteSpace(proofScope.ArchiveId) &&
                     string.Equals(archiveId, proofScope.ArchiveId, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static List<(JsonElement Entry, string Context)> CollectGuardianPowerEventEntriesForProof(
        JsonElement entries,
        string context,
        string? proofRelevantReasonType)
    {
        var result = new List<(JsonElement Entry, string Context)>();
        if (entries.ValueKind != JsonValueKind.Array)
            return result;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{context}[{index++}]";
            if (string.IsNullOrWhiteSpace(proofRelevantReasonType) ||
                string.Equals(
                    GetFirstNonEmptyString(entry, "reasonType"),
                    proofRelevantReasonType,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add((entry, entryContext));
            }
        }

        return result;
    }

    private static void ValidateGuardianPowerEventIdentityContract(
        IReadOnlyList<(JsonElement Entry, string Context)> events,
        GuardianPowerJournalIdentityState? preTurnJournalIdentityState,
        List<ValidationIssue> issues)
    {
        var seenEventIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenResonanceLifeScopeKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entry, context) in events)
        {
            var eventId = GetFirstNonEmptyString(entry, "eventId");
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                if (seenEventIds.TryGetValue(eventId, out var firstContext))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.eventId",
                        IssueSeverity.Error,
                        "guardianPowerEvents.eventId должен быть уникальным в рамках raw guardianPowerEvents array",
                        code: "guardian_power_event_duplicate_raw_event_id",
                        section: "AbodePower",
                        expected: "unique raw eventId",
                        actual: $"{eventId} (already used at {firstContext}.eventId)",
                        repairHint: "Не дублируй eventId в raw guardianPowerEvents. Каждое raw power event должно иметь собственный append-only identity key до materialization в journal."));
                }
                else
                {
                    seenEventIds[eventId] = context;
                }

                if (preTurnJournalIdentityState != null && preTurnJournalIdentityState.EventIds.Contains(eventId))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.eventId",
                        IssueSeverity.Error,
                        "guardianPowerEvents.eventId не должен переиспользовать validated pre-turn abode_power_journal eventId",
                        code: "guardian_power_event_raw_event_id_conflicts_with_validated_preturn_journal",
                        section: "AbodePower",
                        expected: "new raw eventId not present in validated pre-turn journal baseline",
                        actual: $"{eventId} already exists in validated pre-turn {GuardianPowerEventState.JournalPath}",
                        repairHint: "Используй новый append-only eventId для raw guardianPowerEvents. Validated pre-turn journal eventId нельзя переиспользовать для нового power event."));
                }
            }

            if (!string.Equals(GetFirstNonEmptyString(entry, "reasonType"), "resonance", StringComparison.OrdinalIgnoreCase) ||
                !TryBuildGuardianResonanceLifeScopeKey(entry, out var resonanceLifeScopeKey))
            {
                continue;
            }

            if (seenResonanceLifeScopeKeys.TryGetValue(resonanceLifeScopeKey, out var firstLifeScopeContext))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.audit.lifeId",
                    IssueSeverity.Error,
                    "guardianPowerEvents.reasonType=resonance допускает максимум одно raw событие на одного Хранителя в рамках одной завершённой жизни",
                    code: "guardian_power_event_duplicate_raw_resonance_for_same_life",
                    section: "LifeEvaluation",
                    expected: "at most one raw resonance event per guardianId + lifeId",
                    actual: $"{resonanceLifeScopeKey} (already used at {firstLifeScopeContext}.audit.lifeId)",
                    repairHint: "Не дублируй raw resonance события для одного и того же guardianId + lifeId. Повторный resonance для той же жизни не должен попадать в authority input."));
            }
            else
            {
                seenResonanceLifeScopeKeys[resonanceLifeScopeKey] = context;
            }

            if (preTurnJournalIdentityState != null &&
                preTurnJournalIdentityState.ResonanceLifeScopeKeys.Contains(resonanceLifeScopeKey))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.audit.lifeId",
                    IssueSeverity.Error,
                    "guardianPowerEvents.reasonType=resonance не должен дублировать validated pre-turn resonance for the same life",
                    code: "guardian_power_event_raw_resonance_conflicts_with_validated_preturn_journal",
                    section: "LifeEvaluation",
                    expected: "no validated pre-turn journal resonance for the same guardianId + lifeId",
                    actual: $"{resonanceLifeScopeKey} already exists in validated pre-turn {GuardianPowerEventState.JournalPath}",
                    repairHint: "Не добавляй raw resonance power event для guardianId + lifeId, который уже присутствовал в validated pre-turn abode_power_journal. Same-life duplicate должен отбраковываться до authority build."));
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

        if (!TryRequireGuardianPreTurnBaseline(
                contextPrefix,
                "Abode power journal validation требует readable validated pre-turn guardians baseline и не использует current guardian/project state как fallback authority.",
                "guardian_power_event_missing_validated_preturn_guardians_snapshot",
                "AbodePower",
                "Сохраняй validated snapshot copy game_state/meta/guardians.json для turns с abode_power_journal. Без этого validator не должен резолвить guardianId и source project из current-only state.",
                issues))
        {
            return;
        }

        var knownGuardianIds = ReadKnownGuardianIdsForProjectValidation();
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects =
            new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (GuardianPowerEventArrayRequiresProjectTrackerAuthority(entries))
        {
            if (!TryRequireGuardianProjectCurrentAuthority(
                    contextPrefix,
                    "Abode power journal validation требует readable validated pre-turn project tracker baseline и не использует current guardian/project state как fallback authority.",
                    "guardian_power_event_missing_validated_preturn_tracker_snapshot",
                    "Abode power journal validation требует semantically valid validated pre-turn project tracker baseline и не использует broken validated tracker snapshot как authority.",
                    "guardian_power_event_invalid_validated_preturn_tracker_snapshot",
                    "Abode power journal validation требует readable current guardian project tracker authority и не использует projected pre-turn tracker state как fallback.",
                    "guardian_power_event_missing_current_tracker_authority",
                    "AbodePower",
                    $"Сохраняй validated snapshot copy {GuardianProjectState.TrackerPath} для turns с abode_power_journal. Без этого validator не должен резолвить source project из current-only tracker state.",
                    $"Исправь current {GuardianProjectState.TrackerPath} так, чтобы validator мог построить strict current authority root. abode_power_journal не должен валидироваться по stale projected tracker state.",
                    issues))
            {
                return;
            }

            knownPoliticalProjects = ReadKnownPoliticalGuardianPowerEventProjectsForValidation();
        }

        var effectiveEntriesForIdentityValidation = new List<(JsonElement Entry, string Context)>();
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            var repairResult = RepairGuardianPowerJournalEntryForValidation(entry, knownPoliticalProjects);
            var effectiveEntry = repairResult.EffectiveEntry;
            if (repairResult.Status == GuardianPowerJournalRepairStatus.Canonicalized)
            {
                issues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "abode_power_journal entry расходится с canonical repaired form и должен быть переписан перед прохождением strict validation",
                    code: "guardian_power_event_requires_canonical_repair",
                    section: "AbodePower",
                    repairHint: "Запусти canonical journal repair/normalizer, чтобы raw journal entry хранил ту же attacker/project identity, которую использует validator."));
            }

            ValidateGuardianPowerJournalEntryContract(
                effectiveEntry,
                entryContext,
                issues,
                knownGuardianIds,
                knownPoliticalProjects);
            effectiveEntriesForIdentityValidation.Add((effectiveEntry, entryContext));
        }

        ValidateGuardianPowerJournalIdentityContract(effectiveEntriesForIdentityValidation, issues);
    }

    private static void ValidateGuardianPowerJournalIdentityContract(
        IReadOnlyList<(JsonElement Entry, string Context)> entries,
        List<ValidationIssue> issues)
    {
        var seenEntryIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenEventIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (entry, context) in entries)
        {
            var entryId = GetFirstNonEmptyString(entry, "entryId");
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                if (seenEntryIds.TryGetValue(entryId, out var firstContext))
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.entryId",
                        IssueSeverity.Error,
                        "abode_power_journal.entryId должен быть уникальным в рамках всего журнала",
                        code: "guardian_power_event_duplicate_entry_id",
                        section: "AbodePower",
                        expected: "unique entryId",
                        actual: $"{entryId} (already used at {firstContext}.entryId)",
                        repairHint: "Не дублируй entryId в abode_power_journal и не переиспользуй identity уже существующей записи."));
                }
                else
                {
                    seenEntryIds[entryId] = context;
                }
            }

            var eventId = GetFirstNonEmptyString(entry, "eventId");
            if (string.IsNullOrWhiteSpace(eventId))
                continue;

            if (seenEventIds.TryGetValue(eventId, out var firstEventContext))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.eventId",
                    IssueSeverity.Error,
                    "abode_power_journal.eventId должен быть уникальным в рамках всего журнала",
                    code: "guardian_power_event_duplicate_event_id",
                    section: "AbodePower",
                    expected: "unique eventId",
                    actual: $"{eventId} (already used at {firstEventContext}.eventId)",
                    repairHint: "Не переиспользуй eventId для новых journal entries. Каждое событие силы Обители должно иметь собственный append-only eventId."));
            }
            else
            {
                seenEventIds[eventId] = context;
            }
        }
    }

    private void ValidateGuardianPowerJournalEntryContract(
        JsonElement effectiveEntry,
        string entryContext,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        RequireString(effectiveEntry, entryContext, issues, "entryId");
        RequireString(effectiveEntry, entryContext, issues, "eventId");
        ValidateNonNegativeIntegerField(effectiveEntry, entryContext, issues, "turn", "AbodePower");
        var guardianId = RequireString(effectiveEntry, entryContext, issues, "guardianId");
        ValidateKnownGuardianId($"{entryContext}.guardianId", guardianId, issues, knownGuardianIds);
        RequireString(effectiveEntry, entryContext, issues, "guardianName");
        ValidateIntegerField(effectiveEntry, entryContext, issues, "delta");
        var reasonType = RequireString(effectiveEntry, entryContext, issues, "reasonType");
        if (!string.IsNullOrWhiteSpace(reasonType) && !GuardianPowerEventState.IsValidReasonType(reasonType))
        {
            issues.Add(new ValidationIssue(
                $"{entryContext}.reasonType",
                IssueSeverity.Error,
                "abode_power_journal.reasonType использует неподдерживаемый тип события силы Обители",
                code: "guardian_power_event_invalid_reason_type",
                section: "AbodePower",
                expected: string.Join(" | ", GuardianPowerEventState.AllowedReasonTypes),
                actual: reasonType,
                repairHint: "Используй только reasonType из canonical Abode Power contract."));
        }

        var sourceSurface = RequireString(effectiveEntry, entryContext, issues, "sourceSurface");
        var sourceId = RequireString(effectiveEntry, entryContext, issues, "sourceId");
        RequireString(effectiveEntry, entryContext, issues, "title");
        RequireString(effectiveEntry, entryContext, issues, "summary");
        var visibility = RequireString(effectiveEntry, entryContext, issues, "visibility");
        if (!string.IsNullOrWhiteSpace(visibility) && !GuardianPowerEventState.IsValidVisibility(visibility))
        {
            issues.Add(new ValidationIssue(
                $"{entryContext}.visibility",
                IssueSeverity.Error,
                "abode_power_journal.visibility использует неподдерживаемое значение",
                code: "guardian_power_event_invalid_visibility",
                section: "AbodePower",
                expected: string.Join(" | ", GuardianPowerEventState.AllowedVisibility),
                actual: visibility,
                repairHint: "Используй visibility=player_known или hidden."));
        }

        ValidateOptionalNullableStringField(effectiveEntry, entryContext, issues, "relatedGuardianId");
        var relatedGuardianId = GetFirstNonEmptyString(effectiveEntry, "relatedGuardianId");
        ValidateOptionalKnownRelatedGuardianId(
            $"{entryContext}.relatedGuardianId",
            guardianId,
            relatedGuardianId,
            issues,
            knownGuardianIds);
        var appliedAt = RequireString(effectiveEntry, entryContext, issues, "appliedAt");
        if (!string.IsNullOrWhiteSpace(appliedAt) && !DateTimeOffset.TryParse(appliedAt, out _))
        {
            issues.Add(new ValidationIssue(
                $"{entryContext}.appliedAt",
                IssueSeverity.Error,
                "abode_power_journal.appliedAt должен быть ISO 8601 timestamp",
                code: "guardian_power_event_invalid_applied_at",
                section: "AbodePower",
                repairHint: "Сохраняй appliedAt как ISO 8601 timestamp."));
        }

        if (!effectiveEntry.TryGetProperty("audit", out var audit) || !RequireObject(audit, $"{entryContext}.audit", issues))
        {
            issues.Add(new ValidationIssue(
                $"{entryContext}.audit",
                IssueSeverity.Error,
                "abode_power_journal entry обязан содержать audit object",
                code: "guardian_power_event_missing_audit",
                section: "AbodePower",
                repairHint: "Каждая запись canonical abode power history должна хранить machine-readable audit object."));
            return;
        }

        ValidateGuardianPowerEventAudit(
            guardianId,
            relatedGuardianId,
            sourceSurface,
            sourceId,
            reasonType,
            TryReadInt(effectiveEntry, "delta", out var journalDelta) ? journalDelta : null,
            audit,
            entryContext,
            $"{entryContext}.audit",
            issues,
            knownPoliticalProjects);
        ValidateCompletionSourcedRivalStrikeEventContract(
            effectiveEntry,
            entryContext,
            guardianId,
            relatedGuardianId,
            sourceSurface,
            sourceId,
            reasonType,
            audit,
            $"{entryContext}.audit",
            issues,
            knownPoliticalProjects,
            journalSurface: true);
        ValidateUpdateSourcedRivalStrikeEventContract(
            effectiveEntry,
            entryContext,
            sourceSurface,
            reasonType,
            issues);
    }

    private static GuardianPowerJournalRepairValidationResult RepairGuardianPowerJournalEntryForValidation(
        JsonElement entry,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        var reasonType = GetFirstNonEmptyString(entry, "reasonType");
        if (!IsPoliticalGuardianPowerEventReasonType(reasonType))
            return new GuardianPowerJournalRepairValidationResult(entry, GuardianPowerJournalRepairStatus.Unchanged);

        JsonObject? clone;
        try
        {
            clone = JsonNode.Parse(entry.GetRawText()) as JsonObject;
        }
        catch
        {
            return new GuardianPowerJournalRepairValidationResult(entry, GuardianPowerJournalRepairStatus.Unchanged);
        }

        if (clone == null)
            return new GuardianPowerJournalRepairValidationResult(entry, GuardianPowerJournalRepairStatus.Unchanged);

        var status = CanonicalizePoliticalGuardianPowerJournalEntry(clone, knownPoliticalProjects);
        return new GuardianPowerJournalRepairValidationResult(ToJsonElement(clone), status);
    }


    private void ValidateGuardianPowerEventAudit(
        string? guardianId,
        string? relatedGuardianId,
        string? sourceSurface,
        string? sourceId,
        string reasonType,
        int? eventDelta,
        JsonElement audit,
        string eventContext,
        string auditContext,
        List<ValidationIssue> issues,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        bool authorityIndependentOnly = false)
    {
        if (string.IsNullOrWhiteSpace(reasonType))
            return;

        if (string.Equals(reasonType, "resonance", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonPoliticalGuardianPowerEventSourceSurfaceAlignment(
                sourceSurface,
                reasonType,
                $"{eventContext}.sourceSurface",
                issues);
            RequireString(audit, auditContext, issues, "lifeId");
            RequireIntegerAuditField(audit, auditContext, issues, "domainAlignment");
            RequireIntegerAuditField(audit, auditContext, issues, "worldScale");
            RequireIntegerAuditField(audit, auditContext, issues, "permanence");
            RequireIntegerAuditField(audit, auditContext, issues, "sacrifice");
            RequireIntegerAuditField(audit, auditContext, issues, "publicImpact");
            RequireIntegerAuditField(audit, auditContext, issues, "resonanceScore");
            RequireString(audit, auditContext, issues, "classification");
            RequirePositiveIntegerAuditField(audit, auditContext, issues, "finalDelta");

            if (eventDelta.HasValue && eventDelta.Value <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{eventContext}.delta",
                    IssueSeverity.Error,
                    "resonance power-event должен нести положительный delta",
                    code: "guardian_power_event_resonance_delta_sign_mismatch",
                    section: "AbodePower",
                    expected: "positive integer delta",
                    actual: eventDelta.Value.ToString(),
                    repairHint: "Для resonance сохраняй положительный delta, равный итоговому resonance gain на dedicated Life Evaluation turn."));
            }

            if (eventDelta.HasValue &&
                TryReadInt(audit, "finalDelta", out var resonanceFinalDelta) &&
                eventDelta.Value != resonanceFinalDelta)
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.finalDelta",
                    IssueSeverity.Error,
                    "resonance power-event должен держать audit.finalDelta, равный top-level delta",
                    code: "guardian_power_event_resonance_delta_final_delta_mismatch",
                    section: "AbodePower",
                    expected: eventDelta.Value.ToString(),
                    actual: resonanceFinalDelta.ToString(),
                    repairHint: "Синхронизируй resonance audit.finalDelta с итоговым top-level delta."));            
            }

            return;
        }

        if (string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonPoliticalGuardianPowerEventSourceSurfaceAlignment(
                sourceSurface,
                reasonType,
                $"{eventContext}.sourceSurface",
                issues);
            var offeringType = RequireString(audit, auditContext, issues, "offeringType");
            RequireString(audit, auditContext, issues, "returnCycleId");
            RequirePositiveIntegerAuditField(audit, auditContext, issues, "baseDelta");
            RequirePositiveIntegerAuditField(audit, auditContext, issues, "finalDelta");

            if (eventDelta.HasValue && eventDelta.Value <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{eventContext}.delta",
                    IssueSeverity.Error,
                    "offering power-event должен нести положительный delta",
                    code: "guardian_power_event_offering_delta_sign_mismatch",
                    section: "AbodePower",
                    expected: "positive integer delta",
                    actual: eventDelta.Value.ToString(),
                    repairHint: "Для offering сохраняй положительный delta, равный gain силы Обители."));
            }

            if (eventDelta.HasValue &&
                TryReadInt(audit, "finalDelta", out var finalDelta) &&
                eventDelta.Value != finalDelta)
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.finalDelta",
                    IssueSeverity.Error,
                    "offering power-event должен держать audit.finalDelta, равный top-level delta",
                    code: "guardian_power_event_offering_delta_final_delta_mismatch",
                    section: "AbodePower",
                    expected: eventDelta.Value.ToString(),
                    actual: finalDelta.ToString(),
                    repairHint: "Синхронизируй audit.finalDelta с итоговым top-level delta offering power event."));
            }

            if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            {
                RequirePositiveIntegerAuditField(audit, auditContext, issues, "inkFeathersOffered");
                RequireNonNegativeIntegerAuditField(audit, auditContext, issues, "capRemainingBefore");
            }
            else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
            {
                RequireString(audit, auditContext, issues, "relicId");
                RequireString(audit, auditContext, issues, "relicName");
                var relicRarity = RequireString(audit, auditContext, issues, "relicRarity");
                if (!string.IsNullOrWhiteSpace(relicRarity) &&
                    !GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(relicRarity))
                {
                    issues.Add(new ValidationIssue(
                        $"{auditContext}.relicRarity",
                        IssueSeverity.Error,
                        "offering audit relicRarity должна быть canonical rarity tier с поддерживаемым power gain",
                        code: "guardian_power_event_offering_relic_invalid_rarity",
                        section: "AbodePower",
                        expected: GuardianAbodeOfferingState.DescribeCanonicalSoulRelicRarities(),
                        actual: relicRarity,
                        repairHint: "Сохраняй для soul_relic canonical relicRarity, который реально даёт power gain по Abode Offering rules."));
                }
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
            else if (!string.IsNullOrWhiteSpace(offeringType))
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.offeringType",
                    IssueSeverity.Error,
                    "offering audit должен использовать только whitelisted offeringType",
                    code: "guardian_power_event_offering_invalid_type",
                    section: "AbodePower",
                    expected: $"{GuardianAbodeOfferingState.OfferingTypeInkFeathers} | {GuardianAbodeOfferingState.OfferingTypeSoulRelic} | {GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment} | {GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord}",
                    actual: offeringType,
                    repairHint: "Сохраняй в offering audit только один из canonical offeringType, который поддерживает runtime contract и accepted-turn proof."));
            }

            ValidateOfferingPowerEventDeterministicGain(
                audit,
                auditContext,
                eventContext,
                offeringType,
                eventDelta,
                issues);

            return;
        }

        if (IsPoliticalGuardianPowerEventReasonType(reasonType))
        {
            var completionSurface = string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase);
            ValidatePoliticalGuardianPowerEventAuditIdentity(
                guardianId,
                relatedGuardianId,
                sourceSurface,
                sourceId,
                reasonType,
                audit,
                auditContext,
                issues,
                requireFinalState: completionSurface,
                authorityIndependentOnly,
                knownPoliticalProjects);

            if (authorityIndependentOnly)
                return;

            if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase))
            {
                if (completionSurface)
                    ValidateOffensiveImpactAuditFields(audit, auditContext, issues, commandSurface: false);
                else if (string.Equals(sourceSurface, "guardianProjectUpdates", StringComparison.OrdinalIgnoreCase))
                    ValidateGuardianProjectSabotageAudit(audit, auditContext, issues);
            }
            else if (string.Equals(reasonType, "project_assist", StringComparison.OrdinalIgnoreCase) ||
                     (string.Equals(reasonType, "rival_defense", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(sourceSurface, "guardianProjectUpdates", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateGuardianProjectAssistAudit(audit, auditContext, issues);
            }
        }
    }

    private void ValidateCompletionSourcedRivalStrikeEventContract(
        JsonElement eventRoot,
        string eventContext,
        string? guardianId,
        string? relatedGuardianId,
        string? sourceSurface,
        string? sourceId,
        string reasonType,
        JsonElement audit,
        string auditContext,
        List<ValidationIssue> issues,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        bool journalSurface,
        bool authorityIndependentOnly = false)
    {
        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(relatedGuardianId))
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.relatedGuardianId",
                IssueSeverity.Error,
                "completion-sourced rival_strike обязан указывать source guardian в relatedGuardianId",
                code: "guardian_power_event_rival_strike_missing_related_guardian_id",
                section: "AbodePower",
                expected: "existing source guardian id",
                repairHint: "Для target-side rival_strike держи relatedGuardianId равным attacking guardian."));
        }

        var hasDelta = TryReadInt(eventRoot, "delta", out var deltaValue);
        if (!hasDelta || deltaValue >= 0)
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.delta",
                IssueSeverity.Error,
                "completion-sourced rival_strike должен нести отрицательный delta для цели",
                code: "guardian_power_event_rival_strike_delta_sign_mismatch",
                section: "AbodePower",
                expected: "negative integer delta",
                actual: hasDelta ? deltaValue.ToString() : "missing_or_invalid",
                repairHint: "Для target-side rival_strike записывай отрицательный delta, равный потере силы цели."));
        }

        var hasTargetLoss = TryReadInt(audit, "targetLoss", out var targetLoss);
        if (!hasTargetLoss || targetLoss <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.targetLoss",
                IssueSeverity.Error,
                "completion-sourced rival_strike должен нести положительный targetLoss",
                code: "guardian_power_event_rival_strike_target_loss_invalid",
                section: "AbodePower",
                expected: "positive integer targetLoss",
                actual: hasTargetLoss ? targetLoss.ToString() : "missing_or_invalid",
                repairHint: "Сохраняй rival_strike только для реального hostile loss и передавай targetLoss > 0."));
        }

        if (hasDelta && hasTargetLoss && deltaValue < 0 && targetLoss > 0)
        {
            var appliedLoss = -deltaValue;
            var mismatch = journalSurface
                ? appliedLoss > targetLoss
                : appliedLoss != targetLoss;
            if (mismatch)
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.targetLoss",
                    IssueSeverity.Error,
                    journalSurface
                        ? "completion-sourced rival_strike не может применять потери больше, чем разрешает audit.targetLoss"
                        : "raw completion-sourced rival_strike должен нести delta, точно равный hostile targetLoss до clamp",
                    code: "guardian_power_event_rival_strike_delta_target_loss_mismatch",
                    section: "AbodePower",
                    expected: journalSurface ? $">= {appliedLoss}" : appliedLoss.ToString(),
                    actual: targetLoss.ToString(),
                    repairHint: journalSurface
                        ? "Для completion-sourced rival_strike храни applied delta после clamp так, чтобы abs(delta) не превышал targetLoss."
                        : "На raw guardianPowerEvents completion-sourced rival_strike должен записывать pre-clamp delta, равный targetLoss по модулю."));
            }
        }

        if (authorityIndependentOnly)
            return;

        var snapshot = ResolvePoliticalGuardianPowerEventSnapshot(
            guardianId,
            relatedGuardianId,
            sourceSurface,
            sourceId,
            reasonType,
            GetFirstNonEmptyString(audit, "projectGuardianId"),
            GetFirstNonEmptyString(audit, "projectId"),
            knownPoliticalProjects);
        if (snapshot == null)
        {
            return;
        }

        if (!string.Equals(snapshot.ProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.projectType",
                IssueSeverity.Error,
                "completion-sourced rival_strike может ссылаться только на offensive_intrigue source project",
                code: "guardian_power_event_rival_strike_project_type_mismatch",
                section: "AbodePower",
                expected: "offensive_intrigue",
                actual: snapshot.ProjectType ?? string.Empty,
                repairHint: "Держи completion-sourced rival_strike привязанным к завершённому offensive_intrigue."));
        }

        if (!string.IsNullOrWhiteSpace(guardianId) &&
            !string.IsNullOrWhiteSpace(snapshot.TargetGuardianId) &&
            !string.Equals(snapshot.TargetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.guardianId",
                IssueSeverity.Error,
                "completion-sourced rival_strike должен указывать в guardianId ровно ту цель, которая сохранена в source offensive_intrigue",
                code: "guardian_power_event_rival_strike_target_guardian_mismatch",
                section: "AbodePower",
                expected: snapshot.TargetGuardianId,
                actual: guardianId,
                repairHint: "Для target-side rival_strike сохраняй guardianId равным targetGuardianId исходного offensive_intrigue."));
        }
    }

    private void ValidateUpdateSourcedRivalStrikeEventContract(
        JsonElement eventRoot,
        string eventContext,
        string? sourceSurface,
        string reasonType,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "guardianProjectUpdates", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hasDelta = TryReadInt(eventRoot, "delta", out var deltaValue);
        if (!hasDelta || deltaValue >= 0)
        {
            issues.Add(new ValidationIssue(
                $"{eventContext}.delta",
                IssueSeverity.Error,
                "update-sourced rival_strike должен нести отрицательный sabotage delta",
                code: "guardian_power_event_update_rival_strike_delta_sign_mismatch",
                section: "AbodePower",
                expected: "negative integer delta",
                actual: hasDelta ? deltaValue.ToString() : "missing_or_invalid",
                repairHint: "Для sabotage-style rival_strike сохраняй отрицательный delta, соответствующий hostile power loss."));
        }
    }

    private static bool IsPoliticalGuardianPowerEventReasonType(string? reasonType) =>
        string.Equals(reasonType, "project_completion", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "project_failure", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "project_assist", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "rival_defense", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase);

    private static bool GuardianPowerEventArrayRequiresProjectTrackerAuthority(JsonElement entries)
    {
        if (entries.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var entry in entries.EnumerateArray())
        {
            if (IsPoliticalGuardianPowerEventReasonType(GetFirstNonEmptyString(entry, "reasonType")))
                return true;
        }

        return false;
    }

    private static bool GuardianPowerEventEntriesRequireProjectTrackerAuthority(
        IEnumerable<(JsonElement Entry, string Context)> entries)
    {
        foreach (var (entry, _) in entries)
        {
            if (IsPoliticalGuardianPowerEventReasonType(GetFirstNonEmptyString(entry, "reasonType")))
                return true;
        }

        return false;
    }

    private void ValidatePoliticalGuardianPowerEventAuditIdentity(
        string? guardianId,
        string? relatedGuardianId,
        string? sourceSurface,
        string? sourceId,
        string reasonType,
        JsonElement audit,
        string auditContext,
        List<ValidationIssue> issues,
        bool requireFinalState,
        bool authorityIndependentOnly,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        ValidatePoliticalGuardianPowerEventSourceSurfaceAlignment(sourceSurface, reasonType, auditContext, issues);

        var authoredProjectGuardianId = GetFirstNonEmptyString(audit, "projectGuardianId");
        var authoredProjectId = GetFirstNonEmptyString(audit, "projectId");
        var authoredProjectName = GetFirstNonEmptyString(audit, "projectName");
        var authoredProjectType = GetFirstNonEmptyString(audit, "projectType");
        var authoredProjectTier = GetFirstNonEmptyString(audit, "projectTier");
        var authoredFinalState = GetFirstNonEmptyString(audit, "finalState");
        var derivedProjectGuardianId = ResolvePoliticalGuardianPowerEventProjectGuardianId(
            guardianId,
            relatedGuardianId,
            sourceSurface,
            reasonType);
        var effectiveProjectGuardianId = !string.IsNullOrWhiteSpace(authoredProjectGuardianId)
            ? authoredProjectGuardianId
            : derivedProjectGuardianId;
        if (!string.IsNullOrWhiteSpace(authoredProjectGuardianId) &&
            !string.IsNullOrWhiteSpace(derivedProjectGuardianId) &&
            !string.Equals(authoredProjectGuardianId, derivedProjectGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.projectGuardianId",
                IssueSeverity.Error,
                "political power-event audit.projectGuardianId расходится с ожидаемым owner guardian для этого surface/reasonType",
                code: "guardian_power_event_project_guardian_id_mismatch",
                section: "AbodePower",
                expected: derivedProjectGuardianId,
                actual: authoredProjectGuardianId,
                repairHint: "Держи audit.projectGuardianId согласованным с source guardian проекта."));
        }

        var effectiveProjectId = !string.IsNullOrWhiteSpace(authoredProjectId)
            ? authoredProjectId
            : sourceId;
        PoliticalGuardianPowerEventProjectSnapshot? snapshot = null;
        if (!authorityIndependentOnly)
        {
            snapshot = ResolvePoliticalGuardianPowerEventSnapshot(
                guardianId,
                relatedGuardianId,
                sourceSurface,
                sourceId,
                reasonType,
                authoredProjectGuardianId,
                effectiveProjectId,
                knownPoliticalProjects);
        }

        var effectiveProjectName = !string.IsNullOrWhiteSpace(authoredProjectName)
            ? authoredProjectName
            : snapshot?.ProjectName;
        var effectiveProjectType = !string.IsNullOrWhiteSpace(authoredProjectType)
            ? authoredProjectType
            : snapshot?.ProjectType;
        var effectiveProjectTier = !string.IsNullOrWhiteSpace(authoredProjectTier)
            ? authoredProjectTier
            : snapshot?.ProjectTier;
        var effectiveFinalState = !string.IsNullOrWhiteSpace(authoredFinalState)
            ? authoredFinalState
            : snapshot?.FinalState;

        RequirePoliticalGuardianPowerEventAuditString(effectiveProjectGuardianId, $"{auditContext}.projectGuardianId", "projectGuardianId", issues);
        RequirePoliticalGuardianPowerEventAuditString(effectiveProjectId, $"{auditContext}.projectId", "projectId", issues);
        RequirePoliticalGuardianPowerEventAuditString(effectiveProjectName, $"{auditContext}.projectName", "projectName", issues);
        RequirePoliticalGuardianPowerEventAuditString(effectiveProjectType, $"{auditContext}.projectType", "projectType", issues);
        RequirePoliticalGuardianPowerEventAuditString(effectiveProjectTier, $"{auditContext}.projectTier", "projectTier", issues);
        if (!string.IsNullOrWhiteSpace(sourceId) &&
            !string.IsNullOrWhiteSpace(effectiveProjectId) &&
            !string.Equals(sourceId, effectiveProjectId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.projectId",
                IssueSeverity.Error,
                "political power-event audit.projectId must match sourceId for project-sourced events",
                code: "guardian_power_event_project_source_id_mismatch",
                section: "AbodePower",
                expected: sourceId,
                actual: effectiveProjectId,
                repairHint: "Keep audit.projectId aligned with the top-level sourceId for project-sourced power events."));
        }

        if (!authorityIndependentOnly &&
            !string.IsNullOrWhiteSpace(effectiveProjectGuardianId) &&
            !string.IsNullOrWhiteSpace(effectiveProjectId) &&
            snapshot == null)
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.projectId",
                IssueSeverity.Error,
                "political power-event ссылается на project source, которого нет в canonical guardian project state",
                code: "guardian_power_event_unknown_source_project",
                section: "AbodePower",
                expected: "existing canonical guardianId + projectId pair",
                actual: $"{effectiveProjectGuardianId}:{effectiveProjectId}",
                repairHint: "Ссылайся только на реально существующий project source и держи audit.projectGuardianId/projectId согласованными с tracker state."));
        }

        if (!authorityIndependentOnly)
        {
            ValidatePoliticalGuardianPowerEventProjectMetadataAgainstSnapshot(
                snapshot,
                authoredProjectGuardianId,
                authoredProjectName,
                authoredProjectType,
                authoredProjectTier,
                authoredFinalState,
                auditContext,
                issues);
        }

        if (requireFinalState)
        {
            RequirePoliticalGuardianPowerEventAuditString(effectiveFinalState, $"{auditContext}.finalState", "finalState", issues);
            if (!string.IsNullOrWhiteSpace(effectiveFinalState) && !GuardianProjectState.IsValidFinalState(effectiveFinalState))
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.finalState",
                    IssueSeverity.Error,
                    "political power-event audit.finalState must use a supported guardian project terminal state",
                    code: "guardian_power_event_invalid_project_final_state",
                    section: "AbodePower",
                    expected: string.Join(" | ", GuardianProjectState.AllowedFinalStates),
                    actual: effectiveFinalState,
                    repairHint: "Use one of the canonical guardian project terminal states in political power-event audit.finalState."));
            }

            if (!string.IsNullOrWhiteSpace(effectiveFinalState))
                ValidatePoliticalGuardianPowerEventFinalStateAlignment(sourceSurface, reasonType, effectiveFinalState, auditContext, issues);
        }
    }

    private void ValidatePoliticalGuardianPowerEventSourceSurfaceAlignment(
        string? sourceSurface,
        string reasonType,
        string auditContext,
        List<ValidationIssue> issues)
    {
        var isCompletionSurface = string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase);
        var isUpdateSurface = string.Equals(sourceSurface, "guardianProjectUpdates", StringComparison.OrdinalIgnoreCase);
        var isValid = string.Equals(reasonType, "project_completion", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(reasonType, "project_failure", StringComparison.OrdinalIgnoreCase)
            ? isCompletionSurface
            : string.Equals(reasonType, "project_assist", StringComparison.OrdinalIgnoreCase)
                ? isUpdateSurface
                : isCompletionSurface || isUpdateSurface;
        if (isValid)
            return;

        var expectedSurface = string.Equals(reasonType, "project_completion", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(reasonType, "project_failure", StringComparison.OrdinalIgnoreCase)
            ? "completeGuardianProjects"
            : string.Equals(reasonType, "project_assist", StringComparison.OrdinalIgnoreCase)
                ? "guardianProjectUpdates"
                : "guardianProjectUpdates | completeGuardianProjects";
        issues.Add(new ValidationIssue(
            $"{auditContext}.projectId",
            IssueSeverity.Error,
            "political power-event reasonType не согласован с sourceSurface runtime contract",
            code: "guardian_power_event_reason_type_source_surface_mismatch",
            section: "AbodePower",
            expected: expectedSurface,
            actual: sourceSurface ?? string.Empty,
            repairHint: "Сохраняй political power events только на тех sourceSurface, которые реально генерирует guardian project runtime."));
    }

    private void ValidateNonPoliticalGuardianPowerEventSourceSurfaceAlignment(
        string? sourceSurface,
        string reasonType,
        string sourceSurfaceContext,
        List<ValidationIssue> issues)
    {
        if (!TryGetExpectedNonPoliticalGuardianPowerEventSourceSurface(reasonType, out var expectedSurface) ||
            string.Equals(sourceSurface, expectedSurface, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            sourceSurfaceContext,
            IssueSeverity.Error,
            "guardian power-event reasonType не согласован с sourceSurface runtime contract",
            code: "guardian_power_event_reason_type_source_surface_mismatch",
            section: "AbodePower",
            expected: expectedSurface,
            actual: sourceSurface ?? string.Empty,
            repairHint: "Держи non-political guardian power events только на тех sourceSurface, которые реально генерирует соответствующий runtime flow."));
    }

    private static bool TryGetExpectedNonPoliticalGuardianPowerEventSourceSurface(string reasonType, out string expectedSurface)
    {
        if (string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase))
        {
            expectedSurface = "guardianAbodeOffering";
            return true;
        }

        if (string.Equals(reasonType, "resonance", StringComparison.OrdinalIgnoreCase))
        {
            expectedSurface = "life_evaluation";
            return true;
        }

        expectedSurface = string.Empty;
        return false;
    }

    private void ValidateOfferingPowerEventDeterministicGain(
        JsonElement audit,
        string auditContext,
        string eventContext,
        string? offeringType,
        int? eventDelta,
        List<ValidationIssue> issues)
    {
        if (!eventDelta.HasValue || string.IsNullOrWhiteSpace(offeringType))
            return;

        if (!TryResolveExpectedOfferingPowerDelta(audit, auditContext, offeringType, issues, out var expectedDelta))
            return;

        if (eventDelta.Value == expectedDelta)
            return;

        issues.Add(new ValidationIssue(
            $"{eventContext}.delta",
            IssueSeverity.Error,
            "offering power-event должен использовать delta, детерминированно рассчитанный по canonical offering rules",
            code: "guardian_power_event_offering_delta_formula_mismatch",
            section: "AbodePower",
            expected: expectedDelta.ToString(),
            actual: eventDelta.Value.ToString(),
            repairHint: "Не подбирай offering delta вручную; рассчитывай его из offeringType и canonical offering audit payload."));
    }

    private bool TryResolveExpectedOfferingPowerDelta(
        JsonElement audit,
        string auditContext,
        string offeringType,
        List<ValidationIssue> issues,
        out int expectedDelta)
    {
        expectedDelta = 0;

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadInt(audit, "inkFeathersOffered", out var inkFeathersOffered))
                return false;

            if (inkFeathersOffered <= 0 || inkFeathersOffered % 50 != 0 || inkFeathersOffered > 150)
            {
                issues.Add(new ValidationIssue(
                    $"{auditContext}.inkFeathersOffered",
                    IssueSeverity.Error,
                    "offering audit inkFeathersOffered должен использовать supported offering amounts",
                    code: "guardian_power_event_offering_invalid_amount",
                    section: "AbodePower",
                    expected: "50 | 100 | 150",
                    actual: inkFeathersOffered.ToString(),
                    repairHint: "Для ink_feathers используй только canonical offering amounts: 50, 100 или 150."));
                return false;
            }

            expectedDelta = GuardianAbodeOfferingState.ResolvePowerGainForInkFeatherOffering(inkFeathersOffered);
            return expectedDelta > 0;
        }

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            var relicRarity = GetFirstNonEmptyString(audit, "relicRarity");
            if (string.IsNullOrWhiteSpace(relicRarity))
                return false;

            expectedDelta = GuardianAbodeOfferingState.ResolvePowerGainForSoulRelicOffering(relicRarity);
            return expectedDelta > 0;
        }

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            var archiveRarity = GetFirstNonEmptyString(audit, "archiveRarity");
            if (string.IsNullOrWhiteSpace(archiveRarity))
                return false;

            expectedDelta = AbodePowerRules.ResolvePowerGainForArchiveRarity(archiveRarity);
            return expectedDelta > 0;
        }

        return false;
    }

    private static void RequireIntegerAuditField(
        JsonElement audit,
        string auditContext,
        List<ValidationIssue> issues,
        string fieldName)
    {
        if (!audit.TryGetProperty(fieldName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.{fieldName}",
                IssueSeverity.Error,
                "guardian power-event audit обязан содержать обязательное integer поле",
                code: "guardian_power_event_missing_audit_field",
                section: "AbodePower"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            return;

        issues.Add(new ValidationIssue(
            $"{auditContext}.{fieldName}",
            IssueSeverity.Error,
            "guardian power-event audit поле должно быть integer",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static void RequirePositiveIntegerAuditField(
        JsonElement audit,
        string auditContext,
        List<ValidationIssue> issues,
        string fieldName)
    {
        if (!audit.TryGetProperty(fieldName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.{fieldName}",
                IssueSeverity.Error,
                "guardian power-event audit обязан содержать обязательное positive integer поле",
                code: "guardian_power_event_missing_audit_field",
                section: "AbodePower"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var intValue) &&
            intValue > 0)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{auditContext}.{fieldName}",
            IssueSeverity.Error,
            "guardian power-event audit поле должно быть положительным integer",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static void RequireNonNegativeIntegerAuditField(
        JsonElement audit,
        string auditContext,
        List<ValidationIssue> issues,
        string fieldName)
    {
        if (!audit.TryGetProperty(fieldName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.{fieldName}",
                IssueSeverity.Error,
                "guardian power-event audit обязан содержать обязательное non-negative integer поле",
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
            $"{auditContext}.{fieldName}",
            IssueSeverity.Error,
            "guardian power-event audit поле должно быть неотрицательным integer",
            code: "guardian_power_event_invalid_audit_field",
            section: "AbodePower"));
    }

    private static void RequirePoliticalGuardianPowerEventAuditString(
        string? value,
        string context,
        string fieldName,
        List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            $"political power-event audit должен содержать {fieldName}",
            code: $"guardian_power_event_missing_{fieldName}",
            section: "AbodePower",
            repairHint: "Сохраняй political power-event audit как decision-complete machine-readable project snapshot."));
    }

    private void ValidatePoliticalGuardianPowerEventProjectMetadataAgainstSnapshot(
        PoliticalGuardianPowerEventProjectSnapshot? snapshot,
        string? authoredProjectGuardianId,
        string? authoredProjectName,
        string? authoredProjectType,
        string? authoredProjectTier,
        string? authoredFinalState,
        string auditContext,
        List<ValidationIssue> issues)
    {
        if (snapshot == null)
            return;

        ValidatePoliticalGuardianPowerEventSnapshotField(snapshot.ProjectGuardianId, authoredProjectGuardianId, $"{auditContext}.projectGuardianId", "projectGuardianId", issues);
        ValidatePoliticalGuardianPowerEventSnapshotField(snapshot.ProjectName, authoredProjectName, $"{auditContext}.projectName", "projectName", issues);
        ValidatePoliticalGuardianPowerEventSnapshotField(snapshot.ProjectType, authoredProjectType, $"{auditContext}.projectType", "projectType", issues);
        ValidatePoliticalGuardianPowerEventSnapshotField(snapshot.ProjectTier, authoredProjectTier, $"{auditContext}.projectTier", "projectTier", issues);
        if (!string.IsNullOrWhiteSpace(snapshot.FinalState))
            ValidatePoliticalGuardianPowerEventSnapshotField(snapshot.FinalState, authoredFinalState, $"{auditContext}.finalState", "finalState", issues);
    }

    private static string ResolvePoliticalGuardianPowerEventProjectGuardianId(
        string? guardianId,
        string? relatedGuardianId,
        string? sourceSurface,
        string reasonType)
    {
        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            return relatedGuardianId ?? string.Empty;
        }

        return guardianId ?? string.Empty;
    }

    private static string BuildPoliticalGuardianPowerEventProjectKey(string? projectGuardianId, string? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectGuardianId) || string.IsNullOrWhiteSpace(projectId))
            return string.Empty;

        return GuardianProjectState.BuildKey(projectGuardianId, projectId);
    }

    private static GuardianPowerJournalRepairStatus CanonicalizePoliticalGuardianPowerJournalEntry(
        JsonObject entry,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        var reasonType = GetNodeString(entry["reasonType"]);
        if (!IsPoliticalGuardianPowerEventReasonType(reasonType))
            return GuardianPowerJournalRepairStatus.Unchanged;

        var sourceSurface = GetNodeString(entry["sourceSurface"]);
        var sourceId = GetNodeString(entry["sourceId"]);
        var audit = entry["audit"] as JsonObject;
        var changed = false;
        var createdAudit = false;
        if (audit == null)
        {
            audit = new JsonObject();
            createdAudit = true;
        }

        var snapshot = ResolvePoliticalGuardianPowerEventSnapshotForJournalRepair(
            entry,
            audit,
            reasonType ?? string.Empty,
            sourceSurface,
            sourceId,
            knownPoliticalProjects);
        if (snapshot == null)
            return GuardianPowerJournalRepairStatus.Unchanged;

        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(snapshot.TargetGuardianId))
        {
            var targetGuardianId = GetNodeString(entry["guardianId"]);
            if (!string.Equals(snapshot.TargetGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
                return GuardianPowerJournalRepairStatus.Irreparable;
        }

        if (createdAudit)
        {
            entry["audit"] = audit;
            changed = true;
        }
        changed = SetCanonicalPoliticalJournalValue(audit, "projectGuardianId", snapshot.ProjectGuardianId) || changed;
        changed = SetCanonicalPoliticalJournalValue(audit, "projectId", snapshot.ProjectId) || changed;
        changed = SetCanonicalPoliticalJournalValue(audit, "projectName", snapshot.ProjectName) || changed;
        changed = SetCanonicalPoliticalJournalValue(audit, "projectType", snapshot.ProjectType) || changed;
        changed = SetCanonicalPoliticalJournalValue(audit, "projectTier", snapshot.ProjectTier) || changed;
        if (!string.IsNullOrWhiteSpace(snapshot.FinalState))
            changed = SetCanonicalPoliticalJournalValue(audit, "finalState", snapshot.FinalState) || changed;

        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            changed = SetCanonicalPoliticalJournalValue(entry, "relatedGuardianId", snapshot.ProjectGuardianId) || changed;
        }

        return changed
            ? GuardianPowerJournalRepairStatus.Canonicalized
            : GuardianPowerJournalRepairStatus.Unchanged;
    }

    private static PoliticalGuardianPowerEventProjectSnapshot? ResolvePoliticalGuardianPowerEventSnapshotForJournalRepair(
        JsonObject entry,
        JsonObject audit,
        string reasonType,
        string? sourceSurface,
        string? sourceId,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        var projectId = GetNodeString(audit["projectId"]);
        if (string.IsNullOrWhiteSpace(projectId))
            projectId = sourceId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        return ResolvePoliticalGuardianPowerEventSnapshot(
            GetNodeString(entry["guardianId"]),
            GetNodeString(entry["relatedGuardianId"]),
            sourceSurface,
            sourceId,
            reasonType,
            GetNodeString(audit["projectGuardianId"]),
            projectId,
            knownPoliticalProjects);
    }

    private static PoliticalGuardianPowerEventProjectSnapshot? ResolvePoliticalGuardianPowerEventSnapshot(
        string? guardianId,
        string? relatedGuardianId,
        string? sourceSurface,
        string? sourceId,
        string reasonType,
        string? authoredProjectGuardianId,
        string? authoredProjectId,
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects)
    {
        var effectiveProjectId = authoredProjectId;
        if (string.IsNullOrWhiteSpace(effectiveProjectId))
            effectiveProjectId = sourceId;
        if (string.IsNullOrWhiteSpace(effectiveProjectId))
            return null;

        var projectGuardianId = authoredProjectGuardianId;
        if (string.IsNullOrWhiteSpace(projectGuardianId))
        {
            projectGuardianId = ResolvePoliticalGuardianPowerEventProjectGuardianId(
                guardianId,
                relatedGuardianId,
                sourceSurface,
                reasonType);
        }

        var lookupKey = BuildPoliticalGuardianPowerEventProjectKey(projectGuardianId, effectiveProjectId);
        if (!string.IsNullOrWhiteSpace(lookupKey) &&
            knownPoliticalProjects.TryGetValue(lookupKey, out var ownerBoundSnapshot))
        {
            if (IsEligiblePoliticalGuardianPowerEventSnapshot(ownerBoundSnapshot, sourceSurface, reasonType))
                return ownerBoundSnapshot;
        }

        var targetAwareSnapshot = TryResolveCompletionSourcedRivalStrikeProjectByProjectIdAndTarget(
            knownPoliticalProjects,
            effectiveProjectId,
            guardianId,
            sourceSurface,
            reasonType);
        if (targetAwareSnapshot != null)
            return targetAwareSnapshot;

        var uniqueSnapshot = TryResolveUniquePoliticalGuardianPowerEventProjectById(knownPoliticalProjects, effectiveProjectId);
        return IsEligiblePoliticalGuardianPowerEventSnapshot(uniqueSnapshot, sourceSurface, reasonType)
            ? uniqueSnapshot
            : null;
    }

    private static PoliticalGuardianPowerEventProjectSnapshot? TryResolveCompletionSourcedRivalStrikeProjectByProjectIdAndTarget(
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        string projectId,
        string? targetGuardianId,
        string? sourceSurface,
        string reasonType)
    {
        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(targetGuardianId))
        {
            return null;
        }

        PoliticalGuardianPowerEventProjectSnapshot? match = null;
        foreach (var snapshot in knownPoliticalProjects.Values)
        {
            if (!string.Equals(snapshot.ProjectId, projectId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.ProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.FinalState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(snapshot.TargetGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match != null)
                return null;

            match = snapshot;
        }

        return match;
    }

    private static bool IsEligiblePoliticalGuardianPowerEventSnapshot(
        PoliticalGuardianPowerEventProjectSnapshot? snapshot,
        string? sourceSurface,
        string reasonType)
    {
        if (snapshot == null)
            return false;

        if (!string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(snapshot.ProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(snapshot.FinalState, "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static PoliticalGuardianPowerEventProjectSnapshot? TryResolveUniquePoliticalGuardianPowerEventProjectById(
        IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> knownPoliticalProjects,
        string projectId)
    {
        PoliticalGuardianPowerEventProjectSnapshot? match = null;
        foreach (var snapshot in knownPoliticalProjects.Values)
        {
            if (!string.Equals(snapshot.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match != null)
                return null;

            match = snapshot;
        }

        return match;
    }

    private static bool SetCanonicalPoliticalJournalValue(JsonObject target, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var current = GetNodeString(target[propertyName]);
        if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase))
            return false;

        target[propertyName] = value;
        return true;
    }

    private static JsonElement ToJsonElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static void ValidatePoliticalGuardianPowerEventSnapshotField(
        string? expectedValue,
        string? authoredValue,
        string context,
        string fieldName,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(expectedValue) || string.IsNullOrWhiteSpace(authoredValue))
            return;
        if (string.Equals(expectedValue, authoredValue, StringComparison.OrdinalIgnoreCase))
            return;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            $"political power-event audit.{fieldName} расходится с canonical metadata referenced project",
            code: $"guardian_power_event_{fieldName}_mismatch",
            section: "AbodePower",
            expected: expectedValue,
            actual: authoredValue,
            repairHint: "Синхронизируй political power-event audit с canonical tracker metadata проекта."));
    }

    private void ValidatePoliticalGuardianPowerEventFinalStateAlignment(
        string? sourceSurface,
        string reasonType,
        string finalState,
        string auditContext,
        List<ValidationIssue> issues)
    {
        if (string.Equals(reasonType, "project_completion", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.finalState",
                IssueSeverity.Error,
                "project_completion power events must carry finalState=Completed",
                code: "guardian_power_event_project_completion_final_state_mismatch",
                section: "AbodePower",
                expected: "Completed",
                actual: finalState,
                repairHint: "Keep project_completion audit.finalState aligned with the completed project outcome."));
            return;
        }

        if (string.Equals(reasonType, "project_failure", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.finalState",
                IssueSeverity.Error,
                "project_failure power events must not carry finalState=Completed",
                code: "guardian_power_event_project_failure_final_state_mismatch",
                section: "AbodePower",
                expected: "Abandoned | Sabotaged | Collapsed",
                actual: finalState,
                repairHint: "Keep project_failure audit.finalState aligned with a failed terminal project state."));
            return;
        }

        if (string.Equals(reasonType, "rival_strike", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sourceSurface, "completeGuardianProjects", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{auditContext}.finalState",
                IssueSeverity.Error,
                "completion-sourced rival_strike must carry finalState=Completed",
                code: "guardian_power_event_rival_strike_final_state_mismatch",
                section: "AbodePower",
                expected: "Completed",
                actual: finalState,
                repairHint: "Keep completion-sourced rival_strike audit.finalState aligned with successful offensive completion."));
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


    private void ValidateGuardianProjectEntryArray(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        bool completed,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        HashSet<string> knownGuardianIds)
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
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                AddUnknownGuardianProjectIssue($"{entryContext}.guardianId", guardianId, issues);
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

            ValidateGuardianFullProjectObject(project, $"{entryContext}.project", issues, completed, guardianId, relationshipScores, knownGuardianIds);
        }
    }

    private void ValidateGuardianProjectIdentityCollisions(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var seenKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ValidateGuardianProjectIdentityCollisionsInArray(root, "activeProjects", $"{contextPrefix}.activeProjects", seenKeys, issues);
        ValidateGuardianProjectIdentityCollisionsInArray(root, "completedProjects", $"{contextPrefix}.completedProjects", seenKeys, issues);
    }

    private void ValidateGuardianProjectIdentityCollisionsInArray(
        JsonElement root,
        string propertyName,
        string context,
        IDictionary<string, string> seenKeys,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var entries) || entries.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{context}[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("guardianId", out var guardianIdNode) ||
                guardianIdNode.ValueKind != JsonValueKind.String ||
                !entry.TryGetProperty("project", out var project) ||
                project.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var guardianId = guardianIdNode.GetString();
            var projectId = GetFirstNonEmptyString(project, "projectId");
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var key = GuardianProjectState.BuildKey(guardianId, projectId);
            if (!seenKeys.TryAdd(key, $"{entryContext}.project.projectId"))
            {
                issues.Add(new ValidationIssue(
                    $"{entryContext}.project.projectId",
                    IssueSeverity.Error,
                    "guardian project tracker не может содержать один и тот же guardianId + projectId больше одного раза в active/completed history",
                    code: "guardian_project_duplicate_project_key",
                    section: "GuardianProjects",
                    expected: "historically unique guardianId + projectId key",
                    actual: key,
                    repairHint: "Удали collision между activeProjects/completedProjects и оставь для каждого guardianId + projectId ровно одну canonical project запись."));
            }
        }
    }


    private void ValidateGuardianProjectTemporaryModifiers(JsonElement value, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var seenModifierKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            var modifierId = RequireString(item, itemContext, issues, "modifierId");
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

            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(modifierId))
                continue;

            var key = $"{guardianId}::{modifierId}";
            if (seenModifierKeys.Add(key))
                continue;

            issues.Add(new ValidationIssue(
                $"{itemContext}.modifierId",
                IssueSeverity.Error,
                "temporaryProjectModifiers не может содержать один и тот же guardianId + modifierId больше одного раза",
                code: "guardian_project_duplicate_modifier_key",
                section: "GuardianProjects",
                expected: "historically unique guardianId + modifierId key",
                actual: key,
                repairHint: "Оставь для temporaryProjectModifiers только одну canonical запись на каждую пару guardianId + modifierId."));
        }
    }


    private void ValidateGuardianProjectStartCommands(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownProjects,
        HashSet<string> knownCompletedProjects,
        IReadOnlyDictionary<string, string> knownActiveProjectIdsByGuardian,
        HashSet<string> startedThisTurn,
        IDictionary<string, GuardianProjectValidationSnapshot> startedProjectDetails,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        HashSet<string> knownGuardianIds)
    {
        RequireArrayOfObjects(value, context, issues);
        if (value.ValueKind != JsonValueKind.Array)
            return;

        var duplicateGuardianIds = value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => GetFirstNonEmptyString(item, "guardianId"))
            .Where(guardianId => !string.IsNullOrWhiteSpace(guardianId))
            .GroupBy(guardianId => guardianId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var guardianId = RequireString(item, itemContext, issues, "guardianId");
            if (!item.TryGetProperty("project", out var project) || !RequireObject(project, $"{itemContext}.project", issues))
                continue;

            var issueCountBeforeProjectValidation = issues.Count;
            ValidateGuardianFullProjectObject(project, $"{itemContext}.project", issues, completed: false, guardianId, relationshipScores, knownGuardianIds);
            var projectId = GetFirstNonEmptyString(project, "projectId");
            var hasProjectValidationErrors = issues.Count > issueCountBeforeProjectValidation &&
                issues.Skip(issueCountBeforeProjectValidation).Any(issue => issue.Severity == IssueSeverity.Error);
            var canUseForFallback = !string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(projectId);
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
            {
                AddUnknownGuardianProjectIssue($"{itemContext}.guardianId", guardianId, issues);
                canUseForFallback = false;
            }
            if (!string.IsNullOrWhiteSpace(guardianId) && duplicateGuardianIds.Contains(guardianId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.guardianId",
                    IssueSeverity.Error,
                    "startGuardianProjects не должен запускать больше одного проекта для одного guardianId в одном ходу",
                    code: "guardian_project_start_duplicate_guardian",
                    section: "GuardianProjects",
                    repairHint: "Если Хранителю нужен новый проект, запускай только один active project per guardian в v1."));
                canUseForFallback = false;
            }

            if (!string.IsNullOrWhiteSpace(guardianId) && !string.IsNullOrWhiteSpace(projectId))
            {
                var key = GuardianProjectState.BuildKey(guardianId, projectId!);
                if (knownProjects.Contains(key))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.project.projectId",
                        IssueSeverity.Error,
                        "startGuardianProjects не может повторно использовать projectId уже существующего canonical active project",
                        code: "guardian_project_start_duplicate_existing_project_id",
                        section: "GuardianProjects",
                        expected: "new projectId for the target guardian",
                        actual: projectId,
                        repairHint: "Для нового проекта используй новый projectId, а для существующего active project работай через guardianProjectUpdates или completeGuardianProjects."));
                    canUseForFallback = false;
                }
                else if (knownCompletedProjects.Contains(key))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.project.projectId",
                        IssueSeverity.Error,
                        "startGuardianProjects не может повторно использовать projectId уже завершённого проекта этого guardian",
                        code: "guardian_project_start_duplicate_completed_project_id",
                        section: "GuardianProjects",
                        expected: "new historically unique projectId for the target guardian",
                        actual: projectId,
                        repairHint: "Не переиспользуй projectId завершённых guardian projects. Для нового проекта всегда создавай новый projectId."));
                    canUseForFallback = false;
                }

                if (!string.IsNullOrWhiteSpace(guardianId) &&
                    knownGuardianIds.Contains(guardianId) &&
                    knownActiveProjectIdsByGuardian.TryGetValue(guardianId, out var existingProjectId) &&
                    !string.IsNullOrWhiteSpace(existingProjectId) &&
                    !string.Equals(existingProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.guardianId",
                        IssueSeverity.Error,
                        "startGuardianProjects не может заменять уже существующий canonical active project этого guardian",
                        code: "guardian_project_start_guardian_already_has_active_project",
                        section: "GuardianProjects",
                        expected: existingProjectId,
                        actual: projectId,
                        repairHint: "Сначала заверши или обнови уже существующий active project Хранителя, а не запускай новый поверх него."));
                    canUseForFallback = false;
                }

                if (hasProjectValidationErrors)
                    canUseForFallback = false;

                if (canUseForFallback)
                {
                    startedThisTurn.Add(key);
                    startedProjectDetails[key] = new GuardianProjectValidationSnapshot(
                        GetFirstNonEmptyString(project, "projectType"),
                        GetFirstNonEmptyString(project, "targetGuardianId"),
                        GetFirstNonEmptyString(project, "betrayalReason"));
                }
            }
        }
    }


    private void ValidateGuardianProjectUpdateCommands(
        JsonElement value,
        string context,
        List<ValidationIssue> issues,
        HashSet<string> knownProjects,
        HashSet<string> startedThisTurn,
        HashSet<string> knownGuardianIds)
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
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                AddUnknownGuardianProjectIssue($"{itemContext}.guardianId", guardianId, issues);
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
        IReadOnlyDictionary<string, GuardianProjectValidationSnapshot> knownProjectDetails,
        IReadOnlyDictionary<string, GuardianProjectValidationSnapshot> startedProjectDetails,
        HashSet<string> startedThisTurn,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        HashSet<string> knownGuardianIds)
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
            if (!string.IsNullOrWhiteSpace(guardianId) && !knownGuardianIds.Contains(guardianId))
                AddUnknownGuardianProjectIssue($"{itemContext}.guardianId", guardianId, issues);
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
            var betrayalReason = GetFirstNonEmptyString(item, "betrayalReason");
            ValidateOptionalNullableStringField(item, itemContext, issues, "targetGuardianId");
            ValidateOptionalNullableStringField(item, itemContext, issues, "betrayalReason");
            startedProjectDetails.TryGetValue(key, out var startedProject);
            knownProjectDetails.TryGetValue(key, out var preTurnKnownProject);
            var knownProject = startedProject ?? preTurnKnownProject;
            var effectiveProjectType = knownProject?.ProjectType;
            var storedTargetGuardianId = knownProject?.TargetGuardianId;
            var hasPoliticalTargetMismatch =
                IsGuardianPoliticalProjectType(effectiveProjectType) &&
                !string.IsNullOrWhiteSpace(targetGuardianId) &&
                !string.IsNullOrWhiteSpace(storedTargetGuardianId) &&
                !string.Equals(targetGuardianId, storedTargetGuardianId, StringComparison.OrdinalIgnoreCase);
            var effectiveTargetGuardianId = hasPoliticalTargetMismatch && !string.IsNullOrWhiteSpace(storedTargetGuardianId)
                ? storedTargetGuardianId
                : !string.IsNullOrWhiteSpace(targetGuardianId)
                    ? targetGuardianId
                    : storedTargetGuardianId;
            var effectiveBetrayalReason = !string.IsNullOrWhiteSpace(betrayalReason)
                ? betrayalReason
                : knownProject?.BetrayalReason;
            if (hasPoliticalTargetMismatch)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.targetGuardianId",
                    IssueSeverity.Error,
                    "completeGuardianProjects не может менять targetGuardianId уже запущенного политического проекта",
                    code: "guardian_project_completion_target_mismatch",
                    section: "GuardianProjects",
                    expected: storedTargetGuardianId,
                    actual: targetGuardianId,
                    repairHint: "Либо заверши проект с его исходной целью, либо не передавай targetGuardianId повторно в completion command."));
            }
            if (IsGuardianPoliticalProjectType(effectiveProjectType))
                AddGuardianPoliticalTargetIdentityIssues(itemContext, issues, guardianId, effectiveTargetGuardianId, knownGuardianIds);
            if (string.Equals(effectiveProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
                RequiresGuardianPoliticalBetrayalReason(relationshipScores, guardianId, effectiveTargetGuardianId) &&
                string.IsNullOrWhiteSpace(effectiveBetrayalReason))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.betrayalReason",
                    IssueSeverity.Error,
                    "completed offensive_intrigue против ally/trusted target требует explicit betrayal reason на active project или completion command",
                    code: "guardian_project_missing_betrayal_reason",
                    section: "GuardianProjects",
                    repairHint: "Сохрани betrayalReason либо в active project, либо прямо в completeGuardianProjects command перед завершением offensive_intrigue против ally/trusted target."));
            }

            AddGuardianPoliticalTargetPreferenceIssues(
                itemContext,
                issues,
                guardianId,
                effectiveProjectType,
                effectiveTargetGuardianId,
                relationshipScores);
            var isCompletedOffensiveIntrigue =
                string.Equals(effectiveProjectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase);
            if (item.TryGetProperty("offensiveImpactAudit", out var offensiveImpactAudit) &&
                offensiveImpactAudit.ValueKind != JsonValueKind.Null)
            {
                RequireObject(offensiveImpactAudit, $"{itemContext}.offensiveImpactAudit", issues);
                if (!isCompletedOffensiveIntrigue)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.offensiveImpactAudit",
                        IssueSeverity.Error,
                        "offensiveImpactAudit допустим только для Completed offensive_intrigue",
                        code: "guardian_project_completion_unexpected_offensive_audit",
                        section: "GuardianProjects",
                        expected: "effectiveProjectType = offensive_intrigue and finalState = Completed",
                        actual: string.IsNullOrWhiteSpace(effectiveProjectType)
                            ? $"<unresolved>; finalState={finalState ?? "<null>"}"
                            : $"{effectiveProjectType}; finalState={finalState ?? "<null>"}",
                        repairHint: "Передавай offensiveImpactAudit только при завершении Completed offensive_intrigue."));
                }
                if (string.IsNullOrWhiteSpace(effectiveTargetGuardianId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.targetGuardianId",
                        IssueSeverity.Error,
                        "offensiveImpactAudit требует разрешимый targetGuardianId из completion command или active project",
                        code: "guardian_project_completion_offensive_audit_missing_target",
                        section: "GuardianProjects",
                        repairHint: "Если completion несёт offensiveImpactAudit, передай targetGuardianId прямо в команде или сохрани его в активном offensive_intrigue до завершения."));
                }
                if (offensiveImpactAudit.ValueKind == JsonValueKind.Object)
                {
                    var offensiveAuditContext = $"{itemContext}.offensiveImpactAudit";
                    ValidateOffensiveImpactAuditFields(offensiveImpactAudit, offensiveAuditContext, issues, commandSurface: true);
                }
            }
            if (item.TryGetProperty("pressureAudit", out var pressureAudit))
                RequireObject(pressureAudit, $"{itemContext}.pressureAudit", issues);
            if (item.TryGetProperty("stabilityAudit", out var stabilityAudit))
                RequireObject(stabilityAudit, $"{itemContext}.stabilityAudit", issues);
            if (item.TryGetProperty("workAudit", out var workAudit))
                RequireObject(workAudit, $"{itemContext}.workAudit", issues);
        }
    }


    private void ValidateGuardianFullProjectObject(
        JsonElement project,
        string context,
        List<ValidationIssue> issues,
        bool completed,
        string? sourceGuardianId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores,
        HashSet<string> knownGuardianIds)
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

        ValidateOptionalNullableStringField(project, context, issues, "betrayalReason");
        var targetGuardianId = GetFirstNonEmptyString(project, "targetGuardianId");
        var betrayalReason = GetFirstNonEmptyString(project, "betrayalReason");
        if (IsGuardianPoliticalProjectType(projectType))
            AddGuardianPoliticalTargetIdentityIssues(context, issues, sourceGuardianId, targetGuardianId, knownGuardianIds);
        if (string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
            RequiresGuardianPoliticalBetrayalReason(relationshipScores, sourceGuardianId, targetGuardianId) &&
            string.IsNullOrWhiteSpace(betrayalReason))
        {
            issues.Add(new ValidationIssue(
                $"{context}.betrayalReason",
                IssueSeverity.Error,
                "offensive_intrigue против ally/trusted target требует explicit betrayal reason",
                code: "guardian_project_missing_betrayal_reason",
                section: "GuardianProjects",
                repairHint: "Если Хранитель атакует ally/trusted target, добавь betrayalReason с явной причиной разрыва или предательства."));
        }

        AddGuardianPoliticalTargetPreferenceIssues(
            context,
            issues,
            sourceGuardianId,
            projectType,
            targetGuardianId,
            relationshipScores);

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

    private void AddGuardianPoliticalTargetPreferenceIssues(
        string context,
        List<ValidationIssue> issues,
        string? sourceGuardianId,
        string? projectType,
        string? targetGuardianId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> relationshipScores)
    {
        if (!string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(sourceGuardianId) ||
            string.IsNullOrWhiteSpace(targetGuardianId))
        {
            return;
        }

        if (!relationshipScores.TryGetValue(sourceGuardianId, out var scoresByTarget) ||
            !scoresByTarget.TryGetValue(targetGuardianId, out var score))
        {
            return;
        }

        if (GuardianRelationshipRules.IsWeakPoliticalTarget(score))
        {
            var tier = GuardianRelationshipRules.ResolveAttitudeTier(score);
            issues.Add(new ValidationIssue(
                $"{context}.targetGuardianId",
                IssueSeverity.Warning,
                "offensive_intrigue против neutral target остаётся допустимым, но считается слабо мотивированным политическим давлением",
                code: "guardian_project_neutral_target_low_motivation",
                section: "GuardianProjects",
                expected: "preferred hostile target tier: rival | enemy",
                actual: tier,
                repairHint: "Для preferred hostile politics выбирай rival/enemy target; neutral target допускается, но должен быть осознанным исключением."));
        }
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

    private void ValidateOffensiveImpactAuditRelationshipMetadata(JsonElement offensiveAudit, string context, List<ValidationIssue> issues)
    {
        var hasTargetAttitudeScore =
            offensiveAudit.TryGetProperty("targetAttitudeScore", out var targetAttitudeScoreNode) &&
            targetAttitudeScoreNode.ValueKind != JsonValueKind.Null;
        var hasTargetAttitudeTier =
            offensiveAudit.TryGetProperty("targetAttitudeTier", out var targetAttitudeTierNode) &&
            targetAttitudeTierNode.ValueKind != JsonValueKind.Null;
        var hasHostilityWeight =
            offensiveAudit.TryGetProperty("hostilityWeight", out var hostilityWeightNode) &&
            hostilityWeightNode.ValueKind != JsonValueKind.Null;
        var hasPreferredHostileTarget =
            offensiveAudit.TryGetProperty("preferredHostileTarget", out var preferredHostileTargetNode) &&
            preferredHostileTargetNode.ValueKind != JsonValueKind.Null;
        var hasAnyRelationshipMetadata =
            hasTargetAttitudeScore || hasTargetAttitudeTier || hasHostilityWeight || hasPreferredHostileTarget;
        if (!hasAnyRelationshipMetadata)
            return;

        if (!(hasTargetAttitudeScore && hasTargetAttitudeTier && hasHostilityWeight && hasPreferredHostileTarget))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "offensiveImpactAudit relation-derived metadata must be complete when any political relationship field is present",
                code: "guardian_project_offensive_incomplete_relationship_metadata",
                section: "GuardianProjects",
                repairHint: "Если сохраняешь political relation metadata, сохраняй targetAttitudeScore, targetAttitudeTier, hostilityWeight и preferredHostileTarget целиком и согласованно."));
            return;
        }

        if (!TryReadInt(offensiveAudit, "targetAttitudeScore", out var targetAttitudeScore))
            return;

        if (targetAttitudeScore < GuardianRelationshipRules.MinAttitudeScore ||
            targetAttitudeScore > GuardianRelationshipRules.MaxAttitudeScore)
        {
            issues.Add(new ValidationIssue(
                $"{context}.targetAttitudeScore",
                IssueSeverity.Error,
                "offensiveImpactAudit.targetAttitudeScore должен быть в canonical guardian relationship range",
                code: "guardian_project_offensive_target_attitude_score_out_of_bounds",
                section: "GuardianProjects",
                expected: $"{GuardianRelationshipRules.MinAttitudeScore}..{GuardianRelationshipRules.MaxAttitudeScore}",
                actual: targetAttitudeScore.ToString(),
                repairHint: "Используй canonical guardian relationship score range -100..100."));
            return;
        }

        var expectedTier = GuardianRelationshipRules.ResolveAttitudeTier(targetAttitudeScore);
        var actualTier = GetFirstNonEmptyString(offensiveAudit, "targetAttitudeTier");
        if (!string.IsNullOrWhiteSpace(actualTier) &&
            !string.Equals(actualTier, expectedTier, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.targetAttitudeTier",
                IssueSeverity.Error,
                "offensiveImpactAudit.targetAttitudeTier не совпадает с targetAttitudeScore",
                code: "guardian_project_offensive_target_attitude_tier_mismatch",
                section: "GuardianProjects",
                expected: expectedTier,
                actual: actualTier,
                repairHint: "Согласуй targetAttitudeTier с ResolveAttitudeTier(targetAttitudeScore)."));
        }

        if (TryReadInt(offensiveAudit, "hostilityWeight", out var hostilityWeight))
        {
            var expectedHostilityWeight = GuardianRelationshipRules.ResolvePoliticalTargetWeight(targetAttitudeScore);
            if (hostilityWeight != expectedHostilityWeight)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.hostilityWeight",
                    IssueSeverity.Error,
                    "offensiveImpactAudit.hostilityWeight не совпадает с targetAttitudeScore",
                    code: "guardian_project_offensive_hostility_weight_mismatch",
                    section: "GuardianProjects",
                    expected: expectedHostilityWeight.ToString(),
                    actual: hostilityWeight.ToString(),
                    repairHint: "Согласуй hostilityWeight с ResolvePoliticalTargetWeight(targetAttitudeScore)."));
            }
        }

        if (TryParseBooleanLiteral(preferredHostileTargetNode, out var preferredHostileTarget))
        {
            var expectedPreferredHostileTarget = GuardianRelationshipRules.ResolvePoliticalTargetWeight(targetAttitudeScore) > 0;
            if (preferredHostileTarget != expectedPreferredHostileTarget)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.preferredHostileTarget",
                    IssueSeverity.Error,
                    "offensiveImpactAudit.preferredHostileTarget не совпадает с targetAttitudeScore и hostilityWeight",
                    code: "guardian_project_offensive_preferred_hostile_target_mismatch",
                    section: "GuardianProjects",
                    expected: expectedPreferredHostileTarget.ToString(),
                    actual: preferredHostileTarget.ToString(),
                    repairHint: "Согласуй preferredHostileTarget с ResolvePoliticalTargetWeight(targetAttitudeScore) > 0."));
            }
        }
    }

    private void ValidateOffensiveImpactAuditFields(JsonElement offensiveAudit, string context, List<ValidationIssue> issues, bool commandSurface)
    {
        if (!commandSurface)
        {
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "attackerCurrentPower", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "targetCurrentPower", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "baseLoss", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "attackerBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "baseTargetShield", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "fortificationBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "counterOperationBonus", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "targetShield", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "pressureDelta", "GuardianProjects");
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "stabilityDamage", "GuardianProjects");
        }

        ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "targetLoss", "GuardianProjects");
        ValidateOffensivePlayerDefenseBonusField(offensiveAudit, context, issues);
        if (offensiveAudit.TryGetProperty("targetAttitudeScore", out _))
            ValidateIntegerField(offensiveAudit, context, issues, "targetAttitudeScore");
        if (offensiveAudit.TryGetProperty("targetAttitudeTier", out var targetAttitudeTier) && targetAttitudeTier.ValueKind != JsonValueKind.Null)
        {
            RequireString(offensiveAudit, context, issues, "targetAttitudeTier");
            var tier = GetFirstNonEmptyString(offensiveAudit, "targetAttitudeTier");
            if (!string.IsNullOrWhiteSpace(tier) && !GuardianRelationshipRules.IsValidAttitudeTier(tier))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.targetAttitudeTier",
                    IssueSeverity.Error,
                    "offensiveImpactAudit.targetAttitudeTier должен быть canonical guardian relationship tier",
                    code: "guardian_project_offensive_invalid_target_attitude_tier",
                    section: "GuardianProjects",
                    expected: "trusted | ally | neutral | competitive | rival | enemy",
                    actual: tier));
            }
        }
        if (offensiveAudit.TryGetProperty("hostilityWeight", out _))
            ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "hostilityWeight", "GuardianProjects");
        if (offensiveAudit.TryGetProperty("preferredHostileTarget", out var preferredHostileTarget) && preferredHostileTarget.ValueKind != JsonValueKind.Null)
            RequireBooleanField(offensiveAudit, context, issues, "preferredHostileTarget");
        ValidateOffensiveImpactAuditRelationshipMetadata(offensiveAudit, context, issues);
    }

    private void AddUnknownGuardianProjectIssue(string path, string guardianId, List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            $"guardian project ссылается на неизвестного guardian '{guardianId}'",
            code: "guardian_project_unknown_guardian_id",
            section: "GuardianProjects",
            expected: "existing guardianId from game_state/meta/guardians.json",
            actual: guardianId,
            repairHint: "Используй в guardian projects только существующий guardianId из текущего canonical guardians state."));
    }

    private void AddGuardianPoliticalTargetIdentityIssues(
        string context,
        List<ValidationIssue> issues,
        string? sourceGuardianId,
        string? targetGuardianId,
        HashSet<string> knownGuardianIds)
    {
        if (string.IsNullOrWhiteSpace(targetGuardianId))
            return;

        if (!string.IsNullOrWhiteSpace(sourceGuardianId) &&
            string.Equals(sourceGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.targetGuardianId",
                IssueSeverity.Error,
                "Политический guardian project не может ссылаться на самого Хранителя как на targetGuardianId",
                code: "guardian_project_self_target_guardian",
                section: "GuardianProjects",
                expected: "different existing guardianId",
                actual: targetGuardianId,
                repairHint: "Для offensive_intrigue и counter_rival_operation выбирай другого существующего Хранителя."));
            return;
        }

        if (knownGuardianIds.Contains(targetGuardianId))
            return;

        issues.Add(new ValidationIssue(
            $"{context}.targetGuardianId",
            IssueSeverity.Error,
            $"Политический guardian project ссылается на неизвестного target guardian '{targetGuardianId}'",
            code: "guardian_project_unknown_target_guardian_id",
            section: "GuardianProjects",
            expected: "existing guardianId from game_state/meta/guardians.json",
            actual: targetGuardianId,
            repairHint: "Для offensive_intrigue и counter_rival_operation используй targetGuardianId существующего Хранителя."));
    }

    private void ValidateOffensivePlayerDefenseBonusField(JsonElement offensiveAudit, string context, List<ValidationIssue> issues)
    {
        if (!offensiveAudit.TryGetProperty("playerDefenseBonus", out var playerDefenseBonus) ||
            playerDefenseBonus.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        ValidateNonNegativeIntegerField(offensiveAudit, context, issues, "playerDefenseBonus", "GuardianProjects");
        if (TryReadInt(offensiveAudit, "playerDefenseBonus", out var parsedValue) && (parsedValue < 0 || parsedValue > 2))
        {
            issues.Add(new ValidationIssue(
                $"{context}.playerDefenseBonus",
                IssueSeverity.Error,
                "offensiveImpactAudit.playerDefenseBonus должен быть в диапазоне 0..2",
                code: "guardian_project_offensive_player_defense_bonus_out_of_bounds",
                section: "GuardianProjects",
                expected: "0..2",
                actual: parsedValue.ToString(),
                repairHint: "Используй только 0, 1 или 2 для playerDefenseBonus по canonical offensive contract."));
        }
    }

    private static bool TryParseBooleanLiteral(JsonElement value, out bool parsed)
    {
        parsed = false;
        return value.ValueKind switch
        {
            JsonValueKind.True => (parsed = true) || true,
            JsonValueKind.False => true,
            _ => false
        };
    }


    private void ValidateGuardianProjectOutcomeAudit(JsonElement project, string context, List<ValidationIssue> issues)
    {
        var projectType = GetFirstNonEmptyString(project, "projectType");
        var finalState = GetFirstNonEmptyString(project, "finalState");
        var requiresOffensiveAudit =
            string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase);
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
            (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
             string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase));

        if (requiresOffensiveAudit)
        {
            if (!project.TryGetProperty("offensiveImpactAudit", out var offensiveAudit) ||
                !RequireObject(offensiveAudit, $"{context}.offensiveImpactAudit", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.offensiveImpactAudit",
                    IssueSeverity.Error,
                    "Completed offensive_intrigue должен содержать offensiveImpactAudit",
                    code: "guardian_project_missing_offensive_impact_audit",
                    section: "GuardianProjects",
                    repairHint: "Сохраняй offensiveImpactAudit у completed offensive_intrigue, чтобы политический удар и target pressure были детерминированы."));
                return;
            }

            var offensiveAuditContext = $"{context}.offensiveImpactAudit";
            ValidateOffensiveImpactAuditFields(offensiveAudit, offensiveAuditContext, issues, commandSurface: false);
            return;
        }

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

        if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "pressureRelief", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "stabilityRelief", "GuardianProjects");
            ValidateNonNegativeIntegerField(audit, auditContext, issues, "abodePowerGain", "GuardianProjects");
            if (audit.TryGetProperty("coalitionSupportBonus", out _))
                ValidateNonNegativeIntegerField(audit, auditContext, issues, "coalitionSupportBonus", "GuardianProjects");
            if (audit.TryGetProperty("coalitionEligible", out var coalitionEligible) && coalitionEligible.ValueKind != JsonValueKind.Null)
                RequireBooleanField(audit, auditContext, issues, "coalitionEligible");
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


    private bool TryResolveGuardianDerivedStateForValidation(
        JsonElement guardian,
        string path,
        string message,
        string code,
        string section,
        string repairHint,
        List<ValidationIssue> issues,
        out GuardianProjectState.ResolvedGuardianDerivedState derivedState)
    {
        derivedState = GuardianProjectState.ResolveGuardianDerivedState(guardian);
        if (!TryResolveGuardianProjectTrackerValidationRoot(
                path,
                message,
                code,
                section,
                repairHint,
                issues,
                out var trackerRoot))
        {
            return false;
        }

        derivedState = GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot);
        return true;
    }


    private HashSet<string> ReadKnownGuardianProjectKeysForValidation()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(out var trackerRoot, out _))
            return result;

        MergeKnownGuardianProjectKeysForValidation(result, trackerRoot.GetRawText());

        return result;
    }

    private HashSet<string> ReadKnownCompletedGuardianProjectKeysForValidation()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(out var trackerRoot, out _))
            return result;

        MergeKnownCompletedGuardianProjectKeysForValidation(result, trackerRoot.GetRawText());

        return result;
    }

    private IReadOnlyDictionary<string, GuardianProjectValidationSnapshot> ReadKnownGuardianProjectsForValidation()
    {
        var result = new Dictionary<string, GuardianProjectValidationSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(out var trackerRoot, out _))
            return result;

        MergeKnownGuardianProjectsForValidation(result, trackerRoot.GetRawText());

        return result;
    }

    private IReadOnlyDictionary<string, PoliticalGuardianPowerEventProjectSnapshot> ReadKnownPoliticalGuardianPowerEventProjectsForValidation()
    {
        return TryReadKnownPoliticalGuardianPowerEventProjectsFromStrictTrackerAuthority(
                out var projects,
                out _)
            ? projects
            : new Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot>(StringComparer.OrdinalIgnoreCase);
    }

    private static void MergeKnownGuardianProjectKeysForValidation(HashSet<string> result, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("activeProjects", out var activeProjects) ||
                activeProjects.ValueKind != JsonValueKind.Array)
            {
                return;
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
    }

    private static void MergeKnownGuardianProjectsForValidation(
        Dictionary<string, GuardianProjectValidationSnapshot> result,
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("activeProjects", out var activeProjects) ||
                activeProjects.ValueKind != JsonValueKind.Array)
            {
                return;
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
                if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                    continue;

                result[GuardianProjectState.BuildKey(guardianId!, projectId!)] = new GuardianProjectValidationSnapshot(
                    GetFirstNonEmptyString(project, "projectType"),
                    GetFirstNonEmptyString(project, "targetGuardianId"),
                    GetFirstNonEmptyString(project, "betrayalReason"));
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void MergeKnownCompletedGuardianProjectKeysForValidation(HashSet<string> result, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("completedProjects", out var completedProjects) ||
                completedProjects.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var entry in completedProjects.EnumerateArray())
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
    }

    private static void MergeKnownPoliticalGuardianPowerEventProjectsForValidation(
        Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot> result,
        HashSet<string> ambiguousKeys,
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            MergeKnownPoliticalGuardianPowerEventProjectEntries(result, ambiguousKeys, doc.RootElement, "activeProjects", completed: false);
            MergeKnownPoliticalGuardianPowerEventProjectEntries(result, ambiguousKeys, doc.RootElement, "completedProjects", completed: true);
        }
        catch
        {
            // ignored
        }
    }

    private static void MergeKnownPoliticalGuardianPowerEventProjectEntries(
        Dictionary<string, PoliticalGuardianPowerEventProjectSnapshot> result,
        HashSet<string> ambiguousKeys,
        JsonElement root,
        string propertyName,
        bool completed)
    {
        if (!root.TryGetProperty(propertyName, out var entries) || entries.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("guardianId", out var guardianIdNode) || guardianIdNode.ValueKind != JsonValueKind.String)
                continue;
            if (!entry.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object)
                continue;

            var projectGuardianId = guardianIdNode.GetString();
            var projectId = GetFirstNonEmptyString(project, "projectId");
            if (string.IsNullOrWhiteSpace(projectGuardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var key = GuardianProjectState.BuildKey(projectGuardianId!, projectId);
            if (ambiguousKeys.Contains(key))
                continue;

            var snapshot = new PoliticalGuardianPowerEventProjectSnapshot(
                projectGuardianId!,
                projectId,
                GetFirstNonEmptyString(project, "projectName"),
                GetFirstNonEmptyString(project, "projectType"),
                GetFirstNonEmptyString(project, "projectTier"),
                completed ? GetFirstNonEmptyString(project, "finalState") : null,
                GetFirstNonEmptyString(project, "targetGuardianId"));

            if (result.TryGetValue(key, out var existingSnapshot))
            {
                if (!Equals(existingSnapshot, snapshot))
                {
                    result.Remove(key);
                    ambiguousKeys.Add(key);
                }

                continue;
            }

            result[key] = snapshot;
        }
    }

    private IReadOnlyDictionary<string, string> ReadKnownActiveGuardianProjectIdsByGuardianForValidation()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!TryResolveSharedGuardianProjectTrackerPreTurnAuthorityRootSync(out var trackerRoot, out _))
        {
            return result;
        }

        MergeKnownActiveGuardianProjectIdsByGuardian(result, trackerRoot.GetRawText());

        return result;
    }

    private static void MergeKnownActiveGuardianProjectIdsByGuardian(Dictionary<string, string> result, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("activeProjects", out var activeProjects) ||
                activeProjects.ValueKind != JsonValueKind.Array)
            {
                return;
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
                    result[guardianId!] = projectId!;
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ValidateKnownGuardianId(
        string context,
        string? guardianId,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds)
    {
        if (string.IsNullOrWhiteSpace(guardianId) || knownGuardianIds.Contains(guardianId))
            return;

        AddUnknownGuardianProjectIssue(context, guardianId, issues);
    }

    private void ValidateOptionalKnownRelatedGuardianId(
        string context,
        string? guardianId,
        string? relatedGuardianId,
        List<ValidationIssue> issues,
        HashSet<string> knownGuardianIds)
    {
        if (string.IsNullOrWhiteSpace(relatedGuardianId))
            return;

        if (!string.IsNullOrWhiteSpace(guardianId) &&
            string.Equals(guardianId, relatedGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "guardian power event не может ссылаться на того же Хранителя в relatedGuardianId",
                code: "guardian_power_event_related_guardian_self_reference",
                section: "AbodePower",
                expected: "another existing guardian id",
                actual: relatedGuardianId,
                repairHint: "Используй relatedGuardianId другого Хранителя или не передавай это поле."));
            return;
        }

        if (knownGuardianIds.Contains(relatedGuardianId))
            return;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            $"guardian power event ссылается на неизвестного related guardian '{relatedGuardianId}'",
            code: "guardian_power_event_unknown_related_guardian_id",
            section: "AbodePower",
            actual: relatedGuardianId,
            repairHint: "Используй relatedGuardianId существующего Хранителя или не передавай это поле."));
    }


    private static ForgeGachaBonusLookupResult ResolveAvailableForgeGachaBonusStepsFromTrackerJson(
        string? json,
        string guardianId,
        string sourceProjectId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ForgeGachaBonusLookupResult.NoMatch;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return ForgeGachaBonusLookupResult.NoMatch;

            if (!string.IsNullOrWhiteSpace(sourceProjectId))
            {
                if (TryResolveForgeGachaBonusStepsByProjectId(
                        doc.RootElement,
                        guardianId,
                        sourceProjectId,
                        out var availableSteps))
                {
                    return new ForgeGachaBonusLookupResult(true, availableSteps);
                }
            }

            if (!doc.RootElement.TryGetProperty("completedProjects", out var completedProjects) ||
                completedProjects.ValueKind != JsonValueKind.Array)
            {
                return ForgeGachaBonusLookupResult.NoMatch;
            }

            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!TryResolveForgeGachaBonusStepsFromCompletedEntry(entry, guardianId, sourceProjectId, out var availableSteps))
                    continue;

                return new ForgeGachaBonusLookupResult(true, availableSteps);
            }
        }
        catch
        {
            // ignored
        }

        return ForgeGachaBonusLookupResult.NoMatch;
    }

    private static bool TryResolveForgeGachaBonusStepsByProjectId(
        JsonElement trackerRoot,
        string guardianId,
        string sourceProjectId,
        out int availableSteps)
    {
        if (trackerRoot.TryGetProperty("completedProjects", out var completedProjects) &&
            completedProjects.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in completedProjects.EnumerateArray())
            {
                if (!TryMatchForgeProjectIdentity(entry, guardianId, sourceProjectId))
                    continue;

                availableSteps = TryIsCompletedRelicForgingProject(entry)
                    ? ResolveForgeGachaBonusStepsFromProjectEntry(entry)
                    : 0;
                return true;
            }
        }

        if (trackerRoot.TryGetProperty("activeProjects", out var activeProjects) &&
            activeProjects.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in activeProjects.EnumerateArray())
            {
                if (!TryMatchForgeProjectIdentity(entry, guardianId, sourceProjectId))
                    continue;

                availableSteps = 0;
                return true;
            }
        }

        availableSteps = 0;
        return false;
    }

    private static bool TryResolveForgeGachaBonusStepsFromCompletedEntry(
        JsonElement entry,
        string guardianId,
        string sourceProjectId,
        out int availableSteps)
    {
        if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
            !entry.TryGetProperty("project", out var project) ||
            project.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetFirstNonEmptyString(project, "projectType"), "relic_forging", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetFirstNonEmptyString(project, "finalState"), "Completed", StringComparison.OrdinalIgnoreCase))
        {
            availableSteps = 0;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sourceProjectId) &&
            !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase))
        {
            availableSteps = 0;
            return false;
        }

        availableSteps = ResolveForgeGachaBonusStepsFromProjectEntry(entry);
        return true;
    }

    private static bool TryMatchForgeProjectIdentity(
        JsonElement entry,
        string guardianId,
        string sourceProjectId)
    {
        if (!string.Equals(GetFirstNonEmptyString(entry, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
            !entry.TryGetProperty("project", out var project) ||
            project.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetFirstNonEmptyString(project, "projectId"), sourceProjectId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool TryIsCompletedRelicForgingProject(JsonElement entry)
    {
        return entry.TryGetProperty("project", out var project) &&
               project.ValueKind == JsonValueKind.Object &&
               string.Equals(GetFirstNonEmptyString(project, "projectType"), "relic_forging", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetFirstNonEmptyString(project, "finalState"), "Completed", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveForgeGachaBonusStepsFromProjectEntry(JsonElement entry)
    {
        if (!entry.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object)
            return 0;

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

    private readonly record struct ForgeGachaBonusLookupResult(bool HasMatch, int AvailableSteps)
    {
        public static ForgeGachaBonusLookupResult NoMatch => new(false, 0);
    }

    private readonly record struct LoreResearchVisibleClueBudgetLookupResult(bool HasProject, bool IsCurrentLifeApplicable, int GrantedBudget)
    {
        public static LoreResearchVisibleClueBudgetLookupResult NoMatch => new(false, false, 0);
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


    private static LoreResearchVisibleClueBudgetLookupResult ReadGrantedLoreResearchVisibleClueBudget(
        JsonElement? trackerRoot,
        string guardianId,
        string sourceProjectId,
        int currentIncarnation)
    {
        if (trackerRoot == null || trackerRoot.Value.ValueKind != JsonValueKind.Object ||
            !trackerRoot.Value.TryGetProperty("completedProjects", out var completedProjects) ||
            completedProjects.ValueKind != JsonValueKind.Array)
        {
            return LoreResearchVisibleClueBudgetLookupResult.NoMatch;
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

            if (TryParseJsonObject(project) is not JsonObject projectRoot)
                return new LoreResearchVisibleClueBudgetLookupResult(true, false, 0);

            var grantedBudget = GuardianProjectState.GetGrantedVisibleRivalClueBudgetForCurrentLife(projectRoot, currentIncarnation);
            var isCurrentLifeApplicable =
                projectRoot["effectState"] is JsonObject effectState &&
                GetNodeInt(effectState["targetIncarnation"]) == currentIncarnation;
            return new LoreResearchVisibleClueBudgetLookupResult(true, isCurrentLifeApplicable, grantedBudget);
        }

        return LoreResearchVisibleClueBudgetLookupResult.NoMatch;
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
            var expectedCharges = GetExpectedGuardianGachaCharges(guardian, parsedReputation);
            if (chargesPerReturn != expectedCharges)
            {
                issues.Add(new ValidationIssue(
                    $"{gachaContext}.chargesPerReturn",
                    IssueSeverity.Error,
                    "Guardian gachaSystem.chargesPerReturn должен совпадать с reputation tier + abode power bonus + optional founder bonus",
                    code: "guardian_gacha_charges_tier_mismatch",
                    section: "Guardians",
                    expected: expectedCharges.ToString(),
                    actual: chargesPerReturn.ToString(),
                    repairHint: "Синхронизируй chargesPerReturn с guardian reputation tier, bonusGachaCharges от текущей силы Обители и founder-origin bonus, если guardian основан из вознесённой души."));
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


    private static int GetExpectedGuardianGachaCharges(JsonElement guardian, int currentReputation)
        => GuardianGachaChargeRules.GetChargesPerReturnForReputation(currentReputation, AbodePowerRules.GetCurrentPower(guardian)) +
           PlayerGuardianFoundationState.GetFounderExtraGachaCharges(guardian);


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
