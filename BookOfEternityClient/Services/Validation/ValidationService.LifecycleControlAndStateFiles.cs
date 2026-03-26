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
    private async Task ValidateNpcStateFiles(List<ValidationIssue> issues)
    {
        await ValidateNpcFile("game_state/npcs/npc_core.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UpdateNPCs", "NPCsRenameData", "NPCsInScene"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_skills.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCActiveSkillChanges", "NPCPassiveSkillChanges",
                "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_inventory.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCInventoryAdds", "NPCInventoryUpdates", "NPCInventoryRemovals",
                "NPCEquipmentChanges", "NPCInventoryResourcesChanges"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_journals.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCJournals", "npcJournals"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_memory.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCUnlockedMemories"
            }, issues);
        await ValidateFlexibleStateFile("game_state/npcs/item_journals.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "itemJournalUpdates", "itemJournals", "entries"
            }, issues, ValidateItemJournalStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/npcs/item_journals.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "entries"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_relationships.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCRelationshipChanges", "interNPCRelationshipChanges", "NPCRelationshipLockUpdates"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_goals.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCGoalUpdates", "NPCQuestUpdates"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_activities.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCActivityUpdates", "completeNPCActivities"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_masks.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCMaskAdds", "NPCMaskUpdates", "NPCMaskRemovals", "NPCActiveMaskChange"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_fate_cards.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCFateCardUnlocks"
            }, issues);

        await ValidateNpcFile("game_state/npcs/npc_custom_states.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NPCCustomStateChanges"
            }, issues);
    }

    private async Task ValidateWorldQuestCombatFactionStateFiles(List<ValidationIssue> issues)
    {
        await ValidateFlexibleStateFile("game_state/world/current_location.json", null, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/world_map.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "worldMapUpdates", "newLocations", "newLinks", "locationUpdates",
                "storageUpdates", "storagesToRemove",
                "linkUpdates", "linksToRemove",
                "threatsToAdd", "threatsToUpdate", "threatsToRemove", "completeThreatActivities"
            }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/world_events.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "worldEventsLog" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile(RivalSoulArcService.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UpdateRivalSoulArcs", "arcs" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/world_flags.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "worldStateFlags", "removeWorldStateFlags" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/world_time.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "timeChange", "setWorldTime", "year", "monthName", "dayOfMonth", "timeOfDay", "currentTimeInMinutes"
            }, issues, ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/weather.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "weatherChange", "description", "tendency", "season", "temperature", "windSpeed", "wind", "visibility", "mechanicalEffects"
            }, issues, ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/world/progression.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "updateWorldProgressionTracker", "updateFactionProgressionTracker"
            }, issues, ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile(ProgressionScheduleService.ReportPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "progressionProcessingReport",
                "worldCyclesProcessed", "factionCyclesProcessed",
                "chaosSeaCyclesProcessed", "guardianProjectCyclesProcessed",
                "newLastWorldSimulationTimeInMinutes", "newLastFactionSimulationTimeInMinutes",
                "newLastChaosSeaSimulationOrdinal", "newLastGuardianProjectCycleOrdinal"
            }, issues, ValidateProgressionReportStateFile);
        await ValidateFlexibleStateFile(PendingInkActionsPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pendingActions"
            }, issues, ValidatePendingInkActionsStateFile);

        await ValidateFlexibleStateFile("game_state/quests/regular_quests.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UpdateQuests", "quests" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/quests/soul_quests.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UpdateSoulQuests", "quests" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/quests/quest_history.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "questHistory", "questRewards", "questChains", "questLog" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateStrictTopLevelObjectFileAsync("game_state/quests/quest_history.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "questHistory", "questRewards", "questChains" }, issues);
        await ValidateFlexibleStateFile("game_state/quests/plot_outline.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "plotOutline" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateStrictTopLevelObjectFileAsync("game_state/quests/plot_outline.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "plotOutline" }, issues);

        await ValidateFlexibleStateFile("game_state/combat/enemies.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "enemiesData" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/combat/allies.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alliesData" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/combat/combat_log.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "combat_log_markdown" }, issues,
            ValidateWorldQuestCombatFactionContract);

        await ValidateFlexibleStateFile("game_state/factions/faction_core.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionDataChanges", "factions" }, issues,
            ValidateWorldQuestCombatFactionContract);
        await ValidateFlexibleStateFile("game_state/factions/faction_structure.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionRankChanges", "factionBonusChanges", "entries" }, issues,
            ValidateFactionStructureStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/factions/faction_structure.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionRankChanges", "factionBonusChanges", "entries" }, issues);
        await ValidateFlexibleStateFile("game_state/factions/faction_resources.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionResourceChanges", "entries" }, issues,
            ValidateFactionResourcesStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/factions/faction_resources.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionResourceChanges", "entries" }, issues);
        await ValidateFlexibleStateFile("game_state/factions/faction_projects.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionProjectUpdates", "completeFactionProjects", "activeProjects", "completedProjects" }, issues,
            ValidateFactionProjectsStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/factions/faction_projects.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionProjectUpdates", "completeFactionProjects", "activeProjects", "completedProjects" }, issues);
        await ValidateFlexibleStateFile("game_state/factions/faction_custom.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionCustomStateChanges", "entries" }, issues,
            ValidateFactionCustomStateFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/factions/faction_custom.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionCustomStateChanges", "entries" }, issues);
        await ValidateFlexibleStateFile("game_state/factions/faction_chronicles.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "factionChronicleUpdates", "entries" }, issues,
            ValidateWorldQuestCombatFactionContract);
    }

    private async Task ValidateMetaMiscStateFiles(List<ValidationIssue> issues)
    {
        await ValidateFlexibleStateFile("game_state/meta/soul_state.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "metaStateUpdates",
                "afterlifeArchiveUpdates",
                "soulName", "previousSoulNames", "currentRealm", "currentIncarnation", "enlightenment", "soulProgression",
                "inkFeathers", "soulRelics", "afterlifeArchive", "livesHistory", "crossIncarnationData", "currentTier",
                "soulImprint", "pendingMemoryLegacy"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/meta/guardians.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UpdateGuardians", "guardianPowerEvents", "guardians", "activeGuardian", "chaosSeaNavigation", "pendingGuardianCreation"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile(GuardianPowerEventState.JournalPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entries" }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync(GuardianPowerEventState.JournalPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entries" }, issues);
        await ValidateFlexibleStateFile(GuardianProjectState.TrackerPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "startGuardianProjects", "guardianProjectUpdates", "completeGuardianProjects",
                "activeProjects", "completedProjects", "temporaryProjectModifiers"
            }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync(GuardianProjectState.TrackerPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "startGuardianProjects", "guardianProjectUpdates", "completeGuardianProjects",
                "activeProjects", "completedProjects", "temporaryProjectModifiers"
            }, issues);
        await ValidateFlexibleStateFile(GuardianProjectState.JournalPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entries" }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync(GuardianProjectState.JournalPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "entries" }, issues);
        await ValidateFlexibleStateFile("game_state/meta/player_behavior.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "playerBehaviorAssessment", "historyManipulationCoefficient"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/meta/character_chronicle.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "characterChronicleUpdates", "entries"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/meta/achievements.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "achievementUnlocks", "unlockedAchievements", "trackedProgress", "stats"
            }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync("game_state/meta/achievements.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "achievementUnlocks", "unlockedAchievements", "trackedProgress", "stats"
            }, issues);
        await ValidateFlexibleStateFile("lore/codex_entries.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "loreCodexUpdates", "entries", "totalEntries", "categories"
            }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync("lore/codex_entries.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "loreCodexUpdates", "entries", "totalEntries", "categories"
            }, issues);
        await ValidateFlexibleStateFile("game_state/misc/multipliers.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "multipliers"
            }, issues, ValidateMetaMiscContract);
        await ValidateStrictTopLevelObjectFileAsync("game_state/misc/multipliers.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "multipliers"
            }, issues);
        await ValidateFlexibleStateFile("game_state/misc/vehicles.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UpdateVehicles", "removeVehicles", "activeVehicleChange", "vehicles"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/misc/storage_access.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "grantStorageAccess", "revokeStorageAccess", "shareStorageAccess"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/misc/player_interactions.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "otherPlayersInteractions"
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/control/life_transitions.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reason", "summary", "_lastUpdated"
            }, issues, ValidateLifeTransitionControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/life_transitions.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reason", "summary", "_lastUpdated"
            }, issues);
        await ValidateFlexibleStateFile("game_state/control/incarnation_trigger.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "worldDescription", "characterDescription", "circumstances", "source", "guardianId", "severityBand", "reason", "provocationSummary", "_lastUpdated"
            }, issues, ValidateIncarnationTriggerControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/incarnation_trigger.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "worldDescription", "characterDescription", "circumstances", "source", "guardianId", "severityBand", "reason", "provocationSummary", "_lastUpdated"
            }, issues);
        await ValidateFlexibleStateFile("game_state/control/ascension.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AscensionTrigger", "playerChoice", "_lastUpdated"
            }, issues, ValidateAscensionControlFile);
        await ValidateStrictTopLevelObjectFileAsync("game_state/control/ascension.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AscensionTrigger", "playerChoice", "_lastUpdated"
            }, issues);
        // Client-owned world setup surfaces are validated via mutation checks instead of GM repair-loop shape errors.
        await ValidateLifecycleControlContextAsync(issues);
    }

    private void ValidateLifeTransitionControlFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "reason");
        RequireString(root, contextPrefix, issues, "summary");

        if (root.TryGetProperty("reason", out var reasonProp) &&
            reasonProp.ValueKind == JsonValueKind.String)
        {
            var reason = reasonProp.GetString() ?? "";
            if (!string.Equals(reason, "Death", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.reason",
                    IssueSeverity.Error,
                    "TriggerLifeEnd.reason должен быть Death или Voluntary",
                    code: "life_transition_invalid_reason",
                    section: "Lifecycle",
                    expected: "Death or Voluntary",
                    actual: reason,
                    repairHint: "Используй reason=Death для принудительного конца жизни или reason=Voluntary для добровольного завершения mortal life перед отдельной оценкой жизни."));
            }
        }
    }

    private void ValidateIncarnationTriggerControlFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "worldDescription");
        RequireString(root, contextPrefix, issues, "characterDescription");
        RequireString(root, contextPrefix, issues, "circumstances");

        var source = root.TryGetProperty("source", out var sourceProp) && sourceProp.ValueKind == JsonValueKind.String
            ? sourceProp.GetString() ?? ""
            : "";
        if (!string.Equals(source, IncarnationTriggerContract.GuardianForcedSource, StringComparison.OrdinalIgnoreCase))
            return;

        if (!root.TryGetProperty("guardianId", out var guardianIdProp) ||
            guardianIdProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(guardianIdProp.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.guardianId",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation обязан содержать guardianId",
                code: "forced_incarnation_missing_guardian_id",
                section: "Lifecycle",
                expected: "guardianId string",
                actual: !root.TryGetProperty("guardianId", out _) ? "missing" : guardianIdProp.ValueKind.ToString(),
                repairHint: "Для guardian-forced incarnation укажи guardianId текущего активного Хранителя."));
        }

        var severityBand = root.TryGetProperty("severityBand", out var severityBandProp) &&
                           severityBandProp.ValueKind == JsonValueKind.String
            ? severityBandProp.GetString() ?? ""
            : "";
        if (!IncarnationTriggerContract.IsValidSeverityBand(severityBand))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.severityBand",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation должен содержать severityBand = harsh или severe",
                code: "forced_incarnation_invalid_severity_band",
                section: "Lifecycle",
                expected: "harsh | severe",
                actual: string.IsNullOrWhiteSpace(severityBand) ? "missing" : severityBand,
                repairHint: "Используй severityBand=harsh для умеренно враждебного старта или severityBand=severe для крайней враждебности."));
        }

        if (!root.TryGetProperty("reason", out var reasonProp) ||
            reasonProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(reasonProp.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.reason",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation обязан содержать reason",
                code: "forced_incarnation_missing_reason",
                section: "Lifecycle",
                expected: "non-empty sanction reason",
                actual: !root.TryGetProperty("reason", out _) ? "missing" : reasonProp.ValueKind.ToString(),
                repairHint: "Кратко объясни, за что Хранитель навязывает это воплощение."));
        }

        if (!root.TryGetProperty("provocationSummary", out var provocationProp) ||
            provocationProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(provocationProp.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.provocationSummary",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation обязан содержать provocationSummary",
                code: "forced_incarnation_missing_provocation_summary",
                section: "Lifecycle",
                expected: "non-empty provocationSummary",
                actual: !root.TryGetProperty("provocationSummary", out _) ? "missing" : provocationProp.ValueKind.ToString(),
                repairHint: "Явно опиши, какая провокация или оскорбление игрока вызвали санкцию Хранителя."));
        }
    }

    private void ValidateAscensionControlFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var hasTrigger =
            root.TryGetProperty("AscensionTrigger", out var legacyTrigger) &&
            legacyTrigger.ValueKind == JsonValueKind.True;

        if (!hasTrigger)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "ascension.json должен содержать AscensionTrigger=true",
                code: "ascension_trigger_flag_missing",
                section: "Lifecycle",
                expected: "AscensionTrigger=true",
                actual: "No true trigger flag found",
                repairHint: "Явно укажи AscensionTrigger=true в ascension.json, как это описано в CLI lifecycle contract."));
        }

        var playerChoice = root.TryGetProperty("playerChoice", out var playerChoiceProp) &&
                           playerChoiceProp.ValueKind == JsonValueKind.String
            ? playerChoiceProp.GetString() ?? ""
            : "";
        if (!string.Equals(playerChoice, "Ascension", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.playerChoice",
                IssueSeverity.Error,
                "ascension.json должен содержать playerChoice = 'Ascension'",
                code: "ascension_invalid_player_choice",
                section: "Lifecycle",
                expected: "Ascension",
                actual: string.IsNullOrWhiteSpace(playerChoice) ? "missing or empty" : playerChoice,
                repairHint: "Для AscensionTrigger передай playerChoice='Ascension', чтобы lifecycle route был однозначен для клиента."));
        }
    }

    private void ValidateIncarnationWorldSetupStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var mode = RequireString(root, contextPrefix, issues, "mode");
        if (!string.IsNullOrWhiteSpace(mode) &&
            mode is not ("profile" or "manual" or "mixed"))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.mode",
                IssueSeverity.Error,
                "mode в incarnation_world_setup.json должен быть profile, manual или mixed",
                code: "world_setup_invalid_mode",
                section: "WorldSetup",
                expected: "profile | manual | mixed",
                actual: mode));
        }

        ValidateOptionalString(root, contextPrefix, issues, "profileId");
        ValidateOptionalString(root, contextPrefix, issues, "profileName");
        ValidateOptionalString(root, contextPrefix, issues, "characterDescription");
        ValidateOptionalString(root, contextPrefix, issues, "startingCircumstances");
        ValidateOptionalString(root, contextPrefix, issues, "lastUpdated");

        if (!root.TryGetProperty("worldDirectives", out var directives) || directives.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.worldDirectives",
                IssueSeverity.Error,
                "incarnation_world_setup.json обязан содержать object worldDirectives",
                code: "world_setup_missing_world_directives",
                section: "WorldSetup",
                expected: "worldDirectives object"));
            return;
        }

        ValidateWorldDirectivesObject(directives, $"{contextPrefix}.worldDirectives", issues);
    }

    private void ValidateWorldDirectivesStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateWorldDirectivesObject(root, contextPrefix, issues);
    }

    private void ValidateWorldDirectivesObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateOptionalString(root, contextPrefix, issues, "worldTitle");
        ValidateOptionalString(root, contextPrefix, issues, "genre");
        ValidateOptionalString(root, contextPrefix, issues, "era");
        ValidateOptionalString(root, contextPrefix, issues, "tone");
        ValidateOptionalString(root, contextPrefix, issues, "settingSummary");
        ValidateOptionalString(root, contextPrefix, issues, "detailedWorldDescription");
        ValidateOptionalString(root, contextPrefix, issues, "sourceProfileId");
        ValidateOptionalString(root, contextPrefix, issues, "sourceProfileName");
        ValidateOptionalString(root, contextPrefix, issues, "lastUpdated");

        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "hardRules");
        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "requiredElements");
        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "forbiddenElements");
        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "specialMechanics");
        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "continuityNotes");
        ValidateWorldDirectiveStringArray(root, contextPrefix, issues, "playerAmendments");
    }

    private void ValidateWorldDirectiveStringArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var prop))
            return;

        if (prop.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом строк",
                code: "world_directives_invalid_string_array",
                section: "WorldSetup",
                expected: "array of strings",
                actual: prop.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{propName}[{index}]",
                    IssueSeverity.Error,
                    $"{propName} должен содержать только непустые строки",
                    code: "world_directives_invalid_string_array_item",
                    section: "WorldSetup"));
            }
            index++;
        }
    }

    private async Task ValidateLifecycleControlContextAsync(List<ValidationIssue> issues)
    {
        await ValidateLifeTransitionContextAsync(issues);
        await ValidateIncarnationContextAsync(issues);
        await ValidateAscensionContextAsync(issues);
        await ValidateSystemGuardianAttractionContextAsync(issues);
    }

    private void ValidateSystemGuardianAttractionStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var mode = RequireString(root, contextPrefix, issues, "mode");
        if (!string.IsNullOrWhiteSpace(mode) &&
            !string.Equals(mode, "system_guardian_attraction", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.mode",
                IssueSeverity.Error,
                "system_guardian_attraction.json должен содержать mode = system_guardian_attraction",
                code: "system_guardian_attraction_invalid_mode",
                section: "SystemGuardianPresets",
                expected: "system_guardian_attraction",
                actual: mode));
        }

        RequireString(root, contextPrefix, issues, "targetPresetId");
        RequireString(root, contextPrefix, issues, "targetPresetDisplayName");
        RequireString(root, contextPrefix, issues, "targetPresetVersion");
        RequireString(root, contextPrefix, issues, "sourceLibrary");
        RequireString(root, contextPrefix, issues, "renderedPromptPackage");
        ValidateOptionalString(root, contextPrefix, issues, "targetSummary");
        ValidateOptionalString(root, contextPrefix, issues, "_lastUpdated");
    }

    private async Task ValidateSystemGuardianAttractionContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(SystemGuardianLibraryService.AttractionRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    SystemGuardianLibraryService.AttractionRequestPath,
                    IssueSeverity.Error,
                    "system_guardian_attraction.json должен быть JSON object",
                    code: "system_guardian_attraction_invalid_root",
                    section: "SystemGuardianPresets"));
                return;
            }

            ValidateSystemGuardianAttractionStateFile(doc.RootElement, SystemGuardianLibraryService.AttractionRequestPath, issues);

            var targetPresetId = GetFirstNonEmptyString(doc.RootElement, "targetPresetId");
            if (string.IsNullOrWhiteSpace(targetPresetId))
                return;

            var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
            if (string.IsNullOrWhiteSpace(guardiansJson))
                return;

            using var guardiansDoc = JsonDocument.Parse(guardiansJson);
            if (!guardiansDoc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.activeGuardian",
                    IssueSeverity.Error,
                    "После deterministic attraction к системному Хранителю должен существовать activeGuardian.",
                    code: "system_guardian_attraction_missing_active_guardian",
                    section: "SystemGuardianPresets",
                    expected: $"activeGuardian.sourcePreset.presetId = {targetPresetId}",
                    actual: "missing"));
                return;
            }

            var activePresetId = TryReadGuardianSourcePresetId(activeGuardian);
            if (!string.Equals(activePresetId, targetPresetId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.activeGuardian.sourcePreset.presetId",
                    IssueSeverity.Error,
                    "Deterministic attraction к системному Хранителю завершился другим preset или без sourcePreset metadata.",
                    code: "system_guardian_attraction_target_mismatch",
                    section: "SystemGuardianPresets",
                    expected: targetPresetId,
                    actual: string.IsNullOrWhiteSpace(activePresetId) ? "missing/empty" : activePresetId,
                    repairHint: "После system guardian attraction current activeGuardian должен ссылаться на requested presetId."));   
            }
        }
        catch
        {
            issues.Add(new ValidationIssue(
                SystemGuardianLibraryService.AttractionRequestPath,
                IssueSeverity.Error,
                "system_guardian_attraction.json не читается как валидный JSON",
                code: "system_guardian_attraction_invalid_json",
                section: "SystemGuardianPresets"));
        }
    }

    private void ValidatePendingAbodeOfferingStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "guardianId");
        RequireString(root, contextPrefix, issues, "guardianName");
        var offeringType = RequireString(root, contextPrefix, issues, "offeringType");
        if (!string.IsNullOrWhiteSpace(offeringType) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.offeringType",
                IssueSeverity.Error,
                "pending_abode_offering.json поддерживает только whitelisted offering types",
                code: "abode_offering_invalid_type",
                section: "GuardianOfferings",
                expected: $"{GuardianAbodeOfferingState.OfferingTypeInkFeathers} | {GuardianAbodeOfferingState.OfferingTypeSoulRelic} | {GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment} | {GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord}",
                actual: offeringType,
                repairHint: "Используй один из whitelisted offering types: ink_feathers, soul_relic, archive_lore_fragment или archive_secret_record."));
        }

        if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            ValidatePositiveNumberField(root, contextPrefix, issues, "inkFeathersOffered");
            var offered = TryReadIntField(root, "inkFeathersOffered", out var parsedOffered) ? parsedOffered : 0;
            if (offered > 0 && (offered % 50 != 0 || offered > 150))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.inkFeathersOffered",
                    IssueSeverity.Error,
                    "pending_abode_offering.json должен использовать сумму, кратную 50 и не выше 150",
                    code: "abode_offering_invalid_amount",
                    section: "GuardianOfferings",
                    expected: "50 | 100 | 150",
                    actual: offered.ToString(),
                    repairHint: "Поддерживаемые offering amounts: 50, 100 или 150 Чернильных Перьев."));
            }
        }
        else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            RequireString(root, contextPrefix, issues, "relicId");
            RequireString(root, contextPrefix, issues, "relicName");
            RequireString(root, contextPrefix, issues, "relicRarity");
        }
        else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            RequireString(root, contextPrefix, issues, "archiveId");
            RequireString(root, contextPrefix, issues, "archiveTitle");
            var archiveEntryType = RequireString(root, contextPrefix, issues, "archiveEntryType");
            var archiveRarity = RequireString(root, contextPrefix, issues, "archiveRarity");
            if (!string.IsNullOrWhiteSpace(archiveEntryType) &&
                !AfterlifeArchiveState.OfferingTypeMatchesEntryType(offeringType, archiveEntryType))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.archiveEntryType",
                    IssueSeverity.Error,
                    "pending_abode_offering archiveEntryType не соответствует выбранному offeringType",
                    code: "abode_offering_archive_type_mismatch",
                    section: "GuardianOfferings",
                    expected: string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase)
                        ? AfterlifeArchiveState.EntryTypeLoreFragment
                        : AfterlifeArchiveState.EntryTypeSecretRecord,
                    actual: archiveEntryType,
                    repairHint: "Для archive_lore_fragment используй archiveEntryType=lore_fragment; для archive_secret_record используй archiveEntryType=secret_record."));
            }
            if (!string.IsNullOrWhiteSpace(archiveRarity) && GetRarityRank(archiveRarity) == 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.archiveRarity",
                    IssueSeverity.Error,
                    "pending_abode_offering archiveRarity должна быть canonical rarity tier",
                    code: "abode_offering_archive_invalid_rarity",
                    section: "GuardianOfferings",
                    expected: "Common | Uncommon | Rare | Epic | Legendary | Unique",
                    actual: archiveRarity));
            }
        }

        RequireString(root, contextPrefix, issues, "returnCycleId");
        var createdAtUtc = RequireString(root, contextPrefix, issues, "createdAtUtc");
        if (!string.IsNullOrWhiteSpace(createdAtUtc) && !DateTimeOffset.TryParse(createdAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.createdAtUtc",
                IssueSeverity.Error,
                "pending_abode_offering.json.createdAtUtc должен быть ISO 8601 timestamp",
                code: "abode_offering_invalid_created_at",
                section: "GuardianOfferings",
                expected: "ISO 8601 timestamp",
                actual: createdAtUtc));
        }
    }

    private async Task ValidateArchiveCandidateManifestAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(AfterlifeArchiveCandidateService.ManifestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    AfterlifeArchiveCandidateService.ManifestPath,
                    IssueSeverity.Error,
                    "archive_candidate_manifest.json должен быть JSON object",
                    code: "archive_candidate_manifest_invalid_json",
                    section: "AfterlifeArchive",
                    expected: "JSON object with sourceLife, lastExtractedAt and candidates",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: "Сохраняй archive_candidate_manifest.json как client-authored JSON object с sourceLife, lastExtractedAt и массивом candidates."));
                return;
            }

            ValidateArchiveCandidateManifestStateFile(doc.RootElement, AfterlifeArchiveCandidateService.ManifestPath, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveCandidateService.ManifestPath,
                IssueSeverity.Error,
                $"archive_candidate_manifest.json не читается как валидный JSON: {ex.Message}",
                code: "archive_candidate_manifest_invalid_json",
                section: "AfterlifeArchive",
                repairHint: "Исправь archive_candidate_manifest.json; файл должен быть корректным JSON object."));
        }
    }

    private void ValidateArchiveCandidateManifestStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "sourceLife", "AfterlifeArchive");

        if (root.TryGetProperty("lastExtractedAt", out var extractedAt) &&
            extractedAt.ValueKind == JsonValueKind.String &&
            !DateTimeOffset.TryParse(extractedAt.GetString(), out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.lastExtractedAt",
                IssueSeverity.Error,
                "archive_candidate_manifest.lastExtractedAt должен быть ISO 8601 timestamp",
                code: "archive_candidate_manifest_invalid_extracted_at",
                section: "AfterlifeArchive",
                expected: "ISO 8601 timestamp",
                actual: extractedAt.GetString() ?? string.Empty));
        }

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.candidates",
                IssueSeverity.Error,
                "archive_candidate_manifest обязан содержать массив candidates",
                code: "archive_candidate_manifest_missing_candidates",
                section: "AfterlifeArchive",
                expected: "candidates array",
                actual: !root.TryGetProperty("candidates", out _) ? "missing" : candidates.ValueKind.ToString(),
                repairHint: "Сохраняй archive_candidate_manifest с обязательным массивом candidates, даже если он пуст."));
            return;
        }

        var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archivedCount = 0;
        var archivedSecretCount = 0;
        var sourceLife = root.TryGetProperty("sourceLife", out var sourceLifeProp) &&
                         sourceLifeProp.ValueKind == JsonValueKind.Number &&
                         sourceLifeProp.TryGetInt32(out var parsedSourceLife)
            ? parsedSourceLife
            : -1;
        var index = 0;
        foreach (var candidate in candidates.EnumerateArray())
        {
            var candidateContext = $"{contextPrefix}.candidates[{index++}]";
            if (!RequireObject(candidate, candidateContext, issues))
                continue;

            var candidateId = RequireString(candidate, candidateContext, issues, "candidateId");
            var sourceEntryId = RequireString(candidate, candidateContext, issues, "sourceEntryId");
            ValidateNonNegativeIntegerField(candidate, candidateContext, issues, "sourceLife", "AfterlifeArchive");
            var candidateLife = candidate.TryGetProperty("sourceLife", out var candidateLifeProp) &&
                                candidateLifeProp.ValueKind == JsonValueKind.Number &&
                                candidateLifeProp.TryGetInt32(out var parsedCandidateLife)
                ? parsedCandidateLife
                : -1;
            var proposedEntryType = RequireString(candidate, candidateContext, issues, "proposedEntryType");
            RequireString(candidate, candidateContext, issues, "title");
            RequireString(candidate, candidateContext, issues, "summary");
            var rarity = RequireString(candidate, candidateContext, issues, "rarity");
            var status = RequireString(candidate, candidateContext, issues, "status");
            var sourceKind = GetFirstNonEmptyString(candidate, "sourceKind");

            if (!string.IsNullOrWhiteSpace(candidateId) && !candidateIds.Add(candidateId))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.candidateId",
                    IssueSeverity.Error,
                    "archive candidate manifest не должен содержать duplicate candidateId",
                    code: "archive_candidate_manifest_duplicate_candidate_id",
                    section: "AfterlifeArchive",
                    expected: "unique candidateId",
                    actual: candidateId));
            }

            if (!string.IsNullOrWhiteSpace(sourceEntryId) && !sourceEntryIds.Add(sourceEntryId))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.sourceEntryId",
                    IssueSeverity.Error,
                    "archive candidate manifest не должен содержать duplicate sourceEntryId",
                    code: "archive_candidate_manifest_duplicate_source_entry",
                    section: "AfterlifeArchive",
                    expected: "unique sourceEntryId",
                    actual: sourceEntryId));
            }

            if (!string.IsNullOrWhiteSpace(proposedEntryType) && !AfterlifeArchiveState.IsAllowedEntryType(proposedEntryType))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.proposedEntryType",
                    IssueSeverity.Error,
                    "archive candidate использует неподдерживаемый тип архивной записи",
                    code: "archive_candidate_manifest_invalid_entry_type",
                    section: "AfterlifeArchive",
                    expected: "lore_fragment | secret_record",
                    actual: proposedEntryType));
            }

            if (!string.IsNullOrWhiteSpace(rarity) && !AfterlifeArchiveState.IsSupportedArchiveRarity(rarity))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.rarity",
                    IssueSeverity.Error,
                    "archive candidate rarity должна быть canonical archive rarity",
                    code: "archive_candidate_manifest_invalid_rarity",
                    section: "AfterlifeArchive",
                    actual: rarity));
            }

            if (!string.IsNullOrWhiteSpace(status) && !AfterlifeArchiveCandidateService.IsSupportedStatus(status))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.status",
                    IssueSeverity.Error,
                    "archive candidate status использует неподдерживаемое значение",
                    code: "archive_candidate_manifest_invalid_status",
                    section: "AfterlifeArchive",
                    expected: "pending | archived | skipped",
                    actual: status));
            }

            if (!string.IsNullOrWhiteSpace(sourceKind) && !AfterlifeArchiveState.IsSupportedSourceKind(sourceKind))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.sourceKind",
                    IssueSeverity.Error,
                    "archive candidate sourceKind должен быть canonical afterlife source label",
                    code: "archive_candidate_manifest_invalid_source_kind",
                    section: "AfterlifeArchive",
                    expected: $"{AfterlifeArchiveState.SourceKindCodex} | {AfterlifeArchiveState.SourceKindSystem}",
                    actual: sourceKind));
            }

            if (sourceLife >= 0 && candidateLife >= 0 && candidateLife != sourceLife)
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.sourceLife",
                    IssueSeverity.Error,
                    "archive candidate sourceLife должен совпадать с sourceLife manifest-а",
                    code: "archive_candidate_manifest_source_life_mismatch",
                    section: "AfterlifeArchive",
                    expected: sourceLife.ToString(),
                    actual: candidateLife.ToString()));
            }

            if (candidate.TryGetProperty("discoveredAt", out var discoveredAt) &&
                discoveredAt.ValueKind == JsonValueKind.String &&
                !DateTimeOffset.TryParse(discoveredAt.GetString(), out _))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.discoveredAt",
                    IssueSeverity.Error,
                    "archive candidate discoveredAt должен быть ISO 8601 timestamp",
                    code: "archive_candidate_manifest_invalid_discovered_at",
                    section: "AfterlifeArchive",
                    expected: "ISO 8601 timestamp",
                    actual: discoveredAt.GetString() ?? string.Empty));
            }

            if (candidate.TryGetProperty("tags", out var tags))
                RequireArrayOfStrings(tags, $"{candidateContext}.tags", issues);

            if (candidate.TryGetProperty("archivedAtUtc", out var archivedAtUtc) &&
                archivedAtUtc.ValueKind == JsonValueKind.String &&
                !DateTimeOffset.TryParse(archivedAtUtc.GetString(), out _))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.archivedAtUtc",
                    IssueSeverity.Error,
                    "archive candidate archivedAtUtc должен быть ISO 8601 timestamp",
                    code: "archive_candidate_manifest_invalid_archived_at",
                    section: "AfterlifeArchive",
                    expected: "ISO 8601 timestamp",
                    actual: archivedAtUtc.GetString() ?? string.Empty));
            }

            if (candidate.TryGetProperty("skippedAtUtc", out var skippedAtUtc) &&
                skippedAtUtc.ValueKind == JsonValueKind.String &&
                !DateTimeOffset.TryParse(skippedAtUtc.GetString(), out _))
            {
                issues.Add(new ValidationIssue(
                    $"{candidateContext}.skippedAtUtc",
                    IssueSeverity.Error,
                    "archive candidate skippedAtUtc должен быть ISO 8601 timestamp",
                    code: "archive_candidate_manifest_invalid_skipped_at",
                    section: "AfterlifeArchive",
                    expected: "ISO 8601 timestamp",
                    actual: skippedAtUtc.GetString() ?? string.Empty));
            }

            if (string.Equals(status, AfterlifeArchiveCandidateService.StatusArchived, StringComparison.OrdinalIgnoreCase))
            {
                archivedCount++;
                if (string.Equals(proposedEntryType, AfterlifeArchiveState.EntryTypeSecretRecord, StringComparison.OrdinalIgnoreCase))
                    archivedSecretCount++;
            }
        }

        if (archivedCount > AfterlifeArchiveCandidateService.MaxArchivedPerLife)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.candidates",
                IssueSeverity.Error,
                "archive candidate manifest превысил лимит архивирования за одну жизнь",
                code: "archive_candidate_manifest_total_cap_exceeded",
                section: "AfterlifeArchive",
                expected: $"<= {AfterlifeArchiveCandidateService.MaxArchivedPerLife} archived candidates",
                actual: archivedCount.ToString()));
        }

        if (archivedSecretCount > AfterlifeArchiveCandidateService.MaxSecretArchivedPerLife)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.candidates",
                IssueSeverity.Error,
                "archive candidate manifest превысил лимит тайных записей за одну жизнь",
                code: "archive_candidate_manifest_secret_cap_exceeded",
                section: "AfterlifeArchive",
                expected: $"<= {AfterlifeArchiveCandidateService.MaxSecretArchivedPerLife} archived secret_record candidates",
                actual: archivedSecretCount.ToString()));
        }
    }

    private async Task ValidatePendingAbodeOfferingContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(GuardianAbodeOfferingState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeOfferingState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_abode_offering.json должен быть JSON object",
                    code: "abode_offering_invalid_root",
                    section: "GuardianOfferings",
                    expected: "JSON object",
                    actual: doc.RootElement.ValueKind.ToString()));
                return;
            }

            ValidatePendingAbodeOfferingStateFile(doc.RootElement, GuardianAbodeOfferingState.PendingRequestPath, issues);

            var currentRealm = await TryResolvePreTurnRealmAsync();
            if (!IsChaosSeaRealm(currentRealm))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeOfferingState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_abode_offering.json допустим только в afterlife realm",
                    code: "abode_offering_wrong_realm",
                    section: "GuardianOfferings",
                    expected: "Chaos Sea or Shining Abode pre-turn realm",
                    actual: currentRealm ?? "unknown",
                    repairHint: "Не создавай pending_abode_offering.json в Mortal World."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeOfferingState.PendingRequestPath,
                IssueSeverity.Error,
                $"pending_abode_offering.json не читается как валидный JSON: {ex.Message}",
                code: "abode_offering_invalid_json",
                section: "GuardianOfferings",
                repairHint: "Исправь pending_abode_offering.json; файл должен быть корректным JSON object с guardianId, guardianName, offeringType и type-specific offering fields."));
        }
    }

    private async Task ValidatePendingAbodeOfferingResolutionAsync(List<ValidationIssue> issues)
    {
        var request = await GuardianAbodeOfferingState.ReadAsync(_fs);
        if (request == null)
            return;

        var expectedGain = GuardianAbodeOfferingState.ResolvePowerGainForPendingRequest(request);
        if (expectedGain <= 0)
            return;

        var preJournalJson = await ReadPreTurnTrackedFileAsync(GuardianPowerEventState.JournalPath);
        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var matchingOfferingEventFound = CollectNewGuardianPowerJournalEntries(preJournalJson, postJournalJson)
            .Any(entry => JournalEntryMatchesPendingAbodeOffering(entry, request, expectedGain));

        if (!matchingOfferingEventFound)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "pending_abode_offering.json не привёл к ожидаемому offering event в journal",
                code: "abode_offering_missing_matching_power_event",
                section: "GuardianOfferings",
                repairHint: "Разрешай pending_abode_offering.json через guardianPowerEvents.reasonType=offering с audit, совпадающим с client-authored request."));
        }

        var previousPower = await ReadGuardianAbodePowerAsync(await ReadPreTurnTrackedFileAsync("game_state/meta/guardians.json"), request.GuardianId);
        var currentPower = await ReadGuardianAbodePowerAsync(await _fs.ReadFileAsync("game_state/meta/guardians.json"), request.GuardianId);
        if (!previousPower.HasValue || !currentPower.HasValue || currentPower.Value - previousPower.Value < expectedGain)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering не дал ожидаемый рост силы Обители",
                code: "abode_offering_expected_power_gain_missing",
                section: "GuardianOfferings",
                expected: $">= +{expectedGain}",
                actual: previousPower.HasValue && currentPower.HasValue ? (currentPower.Value - previousPower.Value).ToString() : "missing guardian power",
                repairHint: "Каждое valid abode offering должно реально увеличивать guardian.abodePower.currentPower и оставлять matching offering event в journal."));
        }

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (SoulStateContainsSoulRelic(soulJson, request.RelicId))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Поднесённая Реликвия Души всё ещё находится в soul_state после pending abode offering",
                    code: "abode_offering_soul_relic_not_consumed",
                    section: "GuardianOfferings",
                    repairHint: "Клиентский offering path должен локально удалить offered Soul Relic из soul_state перед GM turn."));
            }
        }
        else if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (SoulStateContainsAfterlifeArchiveEntry(soulJson, request.ArchiveId))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Поднесённая архивная запись всё ещё находится в afterlifeArchive после pending abode offering",
                    code: "abode_offering_archive_entry_not_consumed",
                    section: "GuardianOfferings",
                    repairHint: "Клиентский offering path должен локально удалить offered archive entry из soul_state.afterlifeArchive.stored перед GM turn."));
            }
        }
    }

    private async Task ValidatePendingGuardianTradeRequestContextAsync(List<ValidationIssue> issues)
    {
        var request = await GuardianTradeRequestState.ReadAsync(_fs);
        if (request == null)
            return;

        if (!IsChaosSeaRealm(await TryResolvePreTurnRealmAsync()))
        {
            issues.Add(new ValidationIssue(
                GuardianTradeRequestState.PendingRequestPath,
                IssueSeverity.Error,
                "pending_guardian_trade_request.json допустим только в afterlife realm",
                code: "guardian_trade_request_wrong_realm",
                section: "GuardianTrade"));
        }

        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.GuardianName) ||
            string.IsNullOrWhiteSpace(request.AbodeId) ||
            string.IsNullOrWhiteSpace(request.ReturnCycleId) ||
            request.DerivedTradeSlotCount <= 0)
        {
            issues.Add(new ValidationIssue(
                GuardianTradeRequestState.PendingRequestPath,
                IssueSeverity.Error,
                "pending_guardian_trade_request.json должен содержать полный client-authored contract",
                code: "guardian_trade_request_missing_fields",
                section: "GuardianTrade"));
        }
    }

    private async Task ValidatePendingGuardianTradeRequestResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadPreTurnTrackedFileAsync(GuardianTradeRequestState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        GuardianTradeRequestState.PendingGuardianTradeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<GuardianTradeRequestState.PendingGuardianTradeRequest>(preTurnJson);
        }
        catch
        {
            return;
        }

        if (request == null)
            return;

        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot ||
                guardiansRoot["guardians"] is not JsonArray guardians)
            {
                return;
            }

            var guardian = guardians.OfType<JsonObject>()
                .FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["guardianId"]), request.GuardianId, StringComparison.OrdinalIgnoreCase));
            var tradeInventory = guardian?["tradeInventory"] as JsonObject;
            if (!GuardianTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request))
            {
                issues.Add(new ValidationIssue(
                    GuardianTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_trade_request из pre-turn snapshot не привёл к matching guardian.tradeInventory",
                    code: "guardian_trade_request_missing_inventory_resolution",
                    section: "GuardianTrade",
                    repairHint: "На accepted turn обязательно materialize-ь guardian.tradeInventory по exact client-authored request contract; не игнорируй request и не закрывай его частично совпадающей витриной."));
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingArchiveConsultationRequestContextAsync(List<ValidationIssue> issues)
    {
        var request = await AfterlifeArchiveActionState.ReadConsultationAsync(_fs);
        if (request == null)
            return;

        if (!IsChaosSeaRealm(await TryResolvePreTurnRealmAsync()))
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                IssueSeverity.Error,
                "pending_archive_consultation_request.json допустим только в afterlife realm",
                code: "archive_consultation_request_wrong_realm",
                section: "AfterlifeArchive"));
        }

        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.ArchiveId) ||
            !AfterlifeArchiveState.IsAllowedEntryType(request.ArchiveEntryType) ||
            !AfterlifeArchiveState.IsSupportedArchiveRarity(request.ArchiveRarity) ||
            !string.Equals(request.RequestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase) ||
            request.TargetIncarnation <= 0)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                IssueSeverity.Error,
                "pending_archive_consultation_request.json должен содержать полный client-authored contract",
                code: "archive_consultation_request_missing_fields",
                section: "AfterlifeArchive"));
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            if (!AfterlifeArchiveState.HasMatchingReservation(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindConsultation) &&
                !AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId))
            {
                issues.Add(new ValidationIssue(
                    AfterlifeArchiveActionState.ConsultationRequestPath,
                    IssueSeverity.Error,
                    "pending_archive_consultation_request должен ссылаться либо на активную reservation, либо на уже записанный action receipt",
                    code: "archive_consultation_request_missing_reservation",
                    section: "AfterlifeArchive"));
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingArchiveConsultationResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadPreTurnTrackedFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        AfterlifeArchiveActionState.PendingArchiveConsultationRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AfterlifeArchiveActionState.PendingArchiveConsultationRequest>(preTurnJson);
        }
        catch
        {
            return;
        }

        if (request == null)
            return;

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            var receipt = receipts.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase));
            if (receipt == null)
            {
                issues.Add(new ValidationIssue(
                    AfterlifeArchiveActionState.ConsultationRequestPath,
                    IssueSeverity.Error,
                    "pending_archive_consultation_request из pre-turn snapshot не был закрыт в текущем accepted turn",
                    code: "archive_consultation_request_missing_resolution",
                    section: "AfterlifeArchive",
                    repairHint: "Каждый archive consultation request должен закрываться в ближайшем accepted turn через archiveActionResolutions со status=accepted|rejected|cancelled."));
                return;
            }

            var status = GetNodeString(receipt["status"]);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                var trackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
                if (!ArchiveConsultationReceiptHasMatchingCompletedProject(trackerJson, request.RequestId, request.ArchiveId, request.GuardianId, receipt))
                {
                    issues.Add(new ValidationIssue(
                        AfterlifeArchiveActionState.ConsultationRequestPath,
                        IssueSeverity.Error,
                        "Accepted archive consultation request не привёл к canonical archive_consultation result",
                        code: "archive_consultation_request_missing_canonical_result",
                        section: "AfterlifeArchive",
                        repairHint: "Для accepted archive consultation materialize-ь completed lore_research project с projectOrigin=archive_consultation и matching consultationRequestId/consultationArchiveId."));
                }
            }
            else if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusRejected, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                if (!ArchiveEntryIsAvailableAfterRejectedResolution(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindConsultation))
                {
                    issues.Add(new ValidationIssue(
                        AfterlifeArchiveActionState.ConsultationRequestPath,
                        IssueSeverity.Error,
                        "Rejected/cancelled archive consultation request должен возвращать запись в Архив без активной reservation",
                        code: "archive_consultation_request_rejected_entry_not_restored",
                        section: "AfterlifeArchive"));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingArchiveProjectFuelRequestContextAsync(List<ValidationIssue> issues)
    {
        var request = await AfterlifeArchiveActionState.ReadProjectFuelAsync(_fs);
        if (request == null)
            return;

        if (!IsChaosSeaRealm(await TryResolvePreTurnRealmAsync()))
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                IssueSeverity.Error,
                "pending_archive_project_fuel_request.json допустим только в afterlife realm",
                code: "archive_project_fuel_request_wrong_realm",
                section: "AfterlifeArchive"));
        }

        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.ArchiveId) ||
            string.IsNullOrWhiteSpace(request.TargetProjectId) ||
            !AfterlifeArchiveState.IsAllowedEntryType(request.ArchiveEntryType) ||
            !AfterlifeArchiveState.IsSupportedArchiveRarity(request.ArchiveRarity) ||
            !string.Equals(request.RequestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                IssueSeverity.Error,
                "pending_archive_project_fuel_request.json должен содержать полный client-authored contract",
                code: "archive_project_fuel_request_missing_fields",
                section: "AfterlifeArchive"));
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            if (!AfterlifeArchiveState.HasMatchingReservation(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindProjectFuel) &&
                !AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId))
            {
                issues.Add(new ValidationIssue(
                    AfterlifeArchiveActionState.ProjectFuelRequestPath,
                    IssueSeverity.Error,
                    "pending_archive_project_fuel_request должен ссылаться либо на активную reservation, либо на уже записанный action receipt",
                    code: "archive_project_fuel_request_missing_reservation",
                    section: "AfterlifeArchive"));
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingArchiveProjectFuelResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadPreTurnTrackedFileAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest>(preTurnJson);
        }
        catch
        {
            return;
        }

        if (request == null)
            return;

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
                return;

            AfterlifeArchiveState.NormalizeShape(soulRoot);
            var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
            var receipt = receipts.OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase));
            if (receipt == null)
            {
                issues.Add(new ValidationIssue(
                    AfterlifeArchiveActionState.ProjectFuelRequestPath,
                    IssueSeverity.Error,
                    "pending_archive_project_fuel_request из pre-turn snapshot не был закрыт в текущем accepted turn",
                    code: "archive_project_fuel_request_missing_resolution",
                    section: "AfterlifeArchive",
                    repairHint: "Каждый archive project fuel request должен закрываться в ближайшем accepted turn через archiveActionResolutions со status=accepted|rejected|cancelled."));
                return;
            }

            var status = GetNodeString(receipt["status"]);
            var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
            if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                var journalJson = await _fs.ReadFileAsync(GuardianProjectState.JournalPath);
                if (!ArchiveProjectFuelReceiptHasMatchingJournalEntry(journalJson, request.RequestId, request.ArchiveId, request.GuardianId, request.TargetProjectId))
                {
                    issues.Add(new ValidationIssue(
                        AfterlifeArchiveActionState.ProjectFuelRequestPath,
                        IssueSeverity.Error,
                        "Accepted archive project fuel request не привёл к canonical project fuel result",
                        code: "archive_project_fuel_request_missing_canonical_result",
                        section: "AfterlifeArchive",
                        repairHint: "Для accepted archive project fuel materialize-ь matching assisted journal entry и canonical project update по targetProjectId."));
                }
            }
            else if (string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusRejected, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, AfterlifeArchiveActionState.ResolutionStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                if (!ArchiveEntryIsAvailableAfterRejectedResolution(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindProjectFuel))
                {
                    issues.Add(new ValidationIssue(
                        AfterlifeArchiveActionState.ProjectFuelRequestPath,
                        IssueSeverity.Error,
                        "Rejected/cancelled archive project fuel request должен возвращать запись в Архив без активной reservation",
                        code: "archive_project_fuel_request_rejected_entry_not_restored",
                        section: "AfterlifeArchive"));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidateLifeTransitionContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        if (!TryReadLifeTransitionControlFile(json))
            return;

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        if (string.IsNullOrWhiteSpace(preTurnRealm))
            return;

        if (IsChaosSeaRealm(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/life_transitions.json",
                IssueSeverity.Error,
                "TriggerLifeEnd допустим только в Mortal World и не может срабатывать в afterlife realm",
                code: "life_transition_invalid_realm",
                section: "Lifecycle",
                expected: "Mortal World realm",
                actual: preTurnRealm,
                repairHint: "Проверяй realm на начало accepted turn. Если pre-turn realm уже Chaos Sea/Shining Abode, убери TriggerLifeEnd; если trigger был легален, не переключай soul_state.currentRealm вручную в том же ходе."));
        }
    }

    private async Task ValidateAscensionContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync("game_state/control/ascension.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        if (!TryReadAscensionControlFile(json))
            return;

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        if (!string.IsNullOrWhiteSpace(preTurnRealm) &&
            !IsExactChaosSeaRealm(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/ascension.json",
                IssueSeverity.Error,
                "AscensionTrigger допустим только в Chaos Sea",
                code: "ascension_invalid_realm",
                section: "Lifecycle",
                expected: "Chaos Sea",
                actual: preTurnRealm,
                repairHint: "Проверяй realm на начало accepted turn. Если pre-turn realm уже не Chaos Sea, убери ascension.json; если trigger был легален, не переключай soul_state.currentRealm вручную в том же ходе."));
        }

        var lifeTransitionJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        if (!string.IsNullOrWhiteSpace(lifeTransitionJson) &&
            TryReadLifeTransitionControlFile(lifeTransitionJson))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/ascension.json",
                IssueSeverity.Error,
                "AscensionTrigger нельзя смешивать с TriggerLifeEnd в одном accepted turn",
                code: "ascension_mixed_with_life_transition",
                section: "Lifecycle",
                expected: "Either ascension.json or life_transitions.json in one accepted turn",
                actual: "Both ascension.json and life_transitions.json are present",
                repairHint: "Выбери один lifecycle route на ход: либо TriggerLifeEnd, либо AscensionTrigger. Не смешивай mortal-life end и ascension в одном accepted turn."));
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (!HasMaximumEnlightenment(doc.RootElement))
            {
                issues.Add(new ValidationIssue(
                    "game_state/control/ascension.json",
                    IssueSeverity.Error,
                    "AscensionTrigger допустим только при максимальном Enlightenment",
                    code: "ascension_requires_max_enlightenment",
                    section: "Lifecycle",
                    expected: "Maximum Enlightenment / Transcendence before ascension",
                    actual: "Soul progression is below ascension threshold",
                    repairHint: "Перед AscensionTrigger доведи soul progression до максимального уровня Enlightenment/Transcendence и только потом создавай ascension.json."));
            }
        }
        catch (JsonException)
        {
            // soul_state.json shape errors are reported elsewhere
        }
    }

    private async Task ValidateIncarnationContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync("game_state/control/incarnation_trigger.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        if (!IncarnationTriggerContract.TryParse(json, out var payload))
            return;

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        if (string.IsNullOrWhiteSpace(preTurnRealm))
            return;

        if (!IsExactChaosSeaRealm(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "TriggerIncarnation допустим только в Chaos Sea",
                code: "incarnation_trigger_invalid_realm",
                section: "Lifecycle",
                expected: "Chaos Sea",
                actual: preTurnRealm,
                repairHint: "Проверяй realm на начало accepted turn. Если pre-turn realm уже не Chaos Sea, убери incarnation_trigger.json; если trigger был легален, не переводи soul_state.currentRealm в Mortal World вручную в том же ходе."));
        }

        if (payload.IsGuardianForced)
            await ValidateForcedGuardianIncarnationContextAsync(payload, issues);
    }

    private async Task ValidateForcedGuardianIncarnationContextAsync(IncarnationTriggerPayload payload, List<ValidationIssue> issues)
    {
        var manifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (!string.Equals(manifest?.SourceLabel, "обработки хода", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation допустим только на обычном player-driven afterlife turn.",
                code: "forced_incarnation_invalid_source_turn",
                section: "Lifecycle",
                expected: "ordinary player-driven Chaos Sea turn",
                actual: manifest?.SourceLabel ?? "missing sourceLabel",
                repairHint: "Не навязывай принудительное воплощение на lifecycle/system turns. Сначала верни душу в обитель, дай ей хотя бы один обычный afterlife turn, и только затем реагируй на провокацию."));
        }

        var playerAction = manifest?.PlayerAction ?? string.Empty;
        if (!HasGuardianProvocationEvidence(playerAction, payload))
        {
            issues.Add(new ValidationIssue(
                "input/turn_request.json.playerAction",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation требует явной провокации в реальном playerAction, а не только provocationSummary в trigger payload.",
                code: "forced_incarnation_missing_player_action_provocation_evidence",
                section: "Lifecycle",
                expected: "playerAction with explicit provocation against the current Guardian",
                actual: string.IsNullOrWhiteSpace(playerAction) ? "missing or empty playerAction" : playerAction,
                repairHint: "Если игрок реально не провоцировал Хранителя, убери guardian_forced incarnation. Для детерминированного разрешения используй явную провокацию в playerAction или тег [GUARDIAN_PROVOCATION: guardianId]."));
        }

        var guardJson = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
        if (!string.IsNullOrWhiteSpace(guardJson))
        {
            if (!AfterlifeReturnGuardService.TryParse(guardJson, out var returnGuard))
            {
                issues.Add(new ValidationIssue(
                    AfterlifeReturnGuardService.GuardPath,
                    IssueSeverity.Error,
                    "Повреждённый afterlife_return_guard.json не может отключить защиту первого afterlife-turn; guardian-forced incarnation блокируется fail-closed.",
                    code: "forced_incarnation_blocked_by_invalid_safe_return_guard",
                    section: "Lifecycle",
                    expected: "valid afterlife_return_guard.json or no forced incarnation",
                    actual: "invalid guard file",
                    repairHint: "На этом ходе убери guardian_forced incarnation. Клиентский afterlife_return_guard.json должен быть валидным или очищенным самой runtime-нормализацией."));
            }
            else if (returnGuard.RemainingProtectedTurns > 0)
            {
                issues.Add(new ValidationIssue(
                    AfterlifeReturnGuardService.GuardPath,
                    IssueSeverity.Error,
                    "Душа только что вернулась из смертной жизни и должна получить хотя бы один обычный afterlife turn до Guardian-forced incarnation.",
                    code: "forced_incarnation_blocked_by_safe_return_turn",
                    section: "Lifecycle",
                    expected: "no guardian_forced incarnation while remainingProtectedTurns > 0",
                    actual: $"remainingProtectedTurns={returnGuard.RemainingProtectedTurns}",
                    repairHint: "Не пинай душу обратно немедленно после возвращения. На защищённом return turn убери guardian_forced incarnation и дай игроку обычный ход в обители."));
            }
        }

        var guardianContext = await TryReadActiveGuardianIncarnationContextAsync();
        if (guardianContext == null)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json.activeGuardian",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation требует materialized activeGuardian и его текущую обитель.",
                code: "forced_incarnation_missing_active_guardian_context",
                section: "Lifecycle",
                expected: "activeGuardian + current abode context",
                actual: "missing or invalid guardians active context",
                repairHint: "Сначала materialize activeGuardian, его abode и chaosSeaNavigation.currentAbodeId. Только после этого возможна санкция конкретного Хранителя."));
            return;
        }

        if (!string.Equals(payload.GuardianId, guardianContext.GuardianId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json.guardianId",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation должен ссылаться на текущего activeGuardian.",
                code: "forced_incarnation_guardian_not_active",
                section: "Lifecycle",
                expected: guardianContext.GuardianId,
                actual: string.IsNullOrWhiteSpace(payload.GuardianId) ? "missing" : payload.GuardianId,
                repairHint: "Используй guardianId текущего activeGuardian и не назначай forced incarnation от постороннего Хранителя."));
        }

        if (!guardianContext.IsInCurrentAbode)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json.chaosSeaNavigation.currentAbodeId",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation допустим только когда душа находится в текущей обители активного Хранителя.",
                code: "forced_incarnation_requires_current_guardian_abode",
                section: "Lifecycle",
                expected: guardianContext.ExpectedAbodeId,
                actual: guardianContext.CurrentAbodeId,
                repairHint: "Сначала materialize корректную currentAbodeId/current activeGuardian связь. Только текущий activeGuardian в своей обители может навязать воплощение."));
        }

        if (guardianContext.CurrentReputation > -21)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation требует достаточно отрицательной репутации.",
                code: "forced_incarnation_reputation_too_high",
                section: "Lifecycle",
                expected: "<= -21",
                actual: guardianContext.CurrentReputation.ToString(),
                repairHint: "Для guardian-forced incarnation репутация должна быть хотя бы -21 или ниже. При слабой неприязни ограничься угрозами, отказом или жёстким roleplay без lifecycle trigger."));
        }

        var expectedSeverityBand = guardianContext.CurrentReputation <= -51
            ? IncarnationTriggerContract.SevereSeverityBand
            : IncarnationTriggerContract.HarshSeverityBand;
        if (guardianContext.CurrentReputation <= -21 &&
            !string.Equals(payload.SeverityBand, expectedSeverityBand, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json.severityBand",
                IssueSeverity.Error,
                "severityBand Guardian-forced incarnation должен соответствовать текущему диапазону враждебности Хранителя.",
                code: "forced_incarnation_severity_mismatch",
                section: "Lifecycle",
                expected: expectedSeverityBand,
                actual: string.IsNullOrWhiteSpace(payload.SeverityBand) ? "missing" : payload.SeverityBand,
                repairHint: "Используй harsh при репутации -21..-50 и severe при репутации -51..-100."));
        }
    }

    private static bool HasGuardianProvocationEvidence(string playerAction, IncarnationTriggerPayload payload)
    {
        if (string.IsNullOrWhiteSpace(playerAction))
            return false;

        var match = GuardianProvocationTagRegex.Match(playerAction);
        if (match.Success)
        {
            var taggedGuardian = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(taggedGuardian))
                return true;

            if (!string.IsNullOrWhiteSpace(payload.GuardianId) &&
                taggedGuardian.Equals(payload.GuardianId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var keyword in GuardianProvocationKeywords)
        {
            if (playerAction.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<ForcedGuardianIncarnationContext?> TryReadActiveGuardianIncarnationContextAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var guardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id") ?? "";
            if (string.IsNullOrWhiteSpace(guardianId))
                return null;

            var expectedAbodeId = "";
            if (activeGuardian.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object)
                expectedAbodeId = GetFirstNonEmptyString(abode, "abodeId") ?? "";

            var currentAbodeId = "";
            if (root.TryGetProperty("chaosSeaNavigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object)
                currentAbodeId = GetFirstNonEmptyString(navigation, "currentAbodeId") ?? "";

            var currentReputation = 0;
            if (activeGuardian.TryGetProperty("relationshipData", out var relationshipData) &&
                relationshipData.ValueKind == JsonValueKind.Object &&
                relationshipData.TryGetProperty("currentReputation", out var reputationNode) &&
                reputationNode.ValueKind == JsonValueKind.Number &&
                reputationNode.TryGetInt32(out var relationshipReputation))
            {
                currentReputation = relationshipReputation;
            }
            else if (activeGuardian.TryGetProperty("reputation", out var reputationProp) &&
                     reputationProp.ValueKind == JsonValueKind.Number &&
                     reputationProp.TryGetInt32(out var directReputation))
            {
                currentReputation = directReputation;
            }

            return new ForcedGuardianIncarnationContext
            {
                GuardianId = guardianId,
                ExpectedAbodeId = expectedAbodeId,
                CurrentAbodeId = currentAbodeId,
                CurrentReputation = currentReputation,
                IsInCurrentAbode = !string.IsNullOrWhiteSpace(expectedAbodeId) &&
                                   string.Equals(expectedAbodeId, currentAbodeId, StringComparison.OrdinalIgnoreCase)
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool HasMaximumEnlightenment(JsonElement soulRoot)
    {
        if (soulRoot.TryGetProperty("soulProgression", out var progression) &&
            progression.ValueKind == JsonValueKind.Object)
        {
            if (progression.TryGetProperty("progressPercent", out var progressPercent) &&
                progressPercent.ValueKind == JsonValueKind.Number &&
                progressPercent.TryGetDouble(out var parsedPercent) &&
                parsedPercent >= 100)
            {
                return true;
            }

            if (progression.TryGetProperty("tier", out var tier) &&
                tier.ValueKind == JsonValueKind.Number &&
                tier.TryGetInt32(out var parsedTier) &&
                parsedTier >= 4)
            {
                return true;
            }

            var tierName = progression.TryGetProperty("tierName", out var tierNameProp) &&
                           tierNameProp.ValueKind == JsonValueKind.String
                ? tierNameProp.GetString() ?? ""
                : "";
            if (IsTranscendenceTier(tierName))
                return true;
        }

        if (soulRoot.TryGetProperty("enlightenment", out var enlightenment))
        {
            if (enlightenment.ValueKind == JsonValueKind.Object)
            {
                var currentTier = enlightenment.TryGetProperty("currentTier", out var currentTierProp) &&
                                  currentTierProp.ValueKind == JsonValueKind.String
                    ? currentTierProp.GetString() ?? ""
                    : "";
                if (IsTranscendenceTier(currentTier))
                    return true;

                if (enlightenment.TryGetProperty("level", out var levelProp) &&
                    levelProp.ValueKind == JsonValueKind.Number &&
                    levelProp.TryGetInt32(out var parsedLevel) &&
                    parsedLevel >= 4)
                {
                    return true;
                }

                if (enlightenment.TryGetProperty("progressPercent", out var progressPercent) &&
                    progressPercent.ValueKind == JsonValueKind.Number &&
                    progressPercent.TryGetDouble(out var parsedPercent) &&
                    parsedPercent >= 100)
                {
                    return true;
                }
            }
            else if (enlightenment.ValueKind == JsonValueKind.Number &&
                     enlightenment.TryGetDouble(out var numericEnlightenment) &&
                     numericEnlightenment >= 100)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTranscendenceTier(string tierName)
    {
        return string.Equals(tierName, "Transcendence", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tierName, "Трансценденция", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateFactionStructureStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateWorldQuestCombatFactionContract(root, contextPrefix, issues);
        if (!TryGetArray(root, "entries", $"{contextPrefix}.entries", issues, out var entries))
            return;

        var index = 0;
        foreach (var item in entries.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateCanonicalFactionIdentity(item, itemContext, issues);
            var missingStructureFields = new List<string>();
            if (!item.TryGetProperty("ranks", out var ranks))
                missingStructureFields.Add("ranks");
            if (!item.TryGetProperty("structuredBonuses", out var structuredBonuses))
                missingStructureFields.Add("structuredBonuses");

            if (missingStructureFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Canonical faction_structure entry не содержит обязательные корневые поля",
                    code: "canonical_faction_structure_missing_required_fields",
                    section: "Factions",
                    expected: "ranks and structuredBonuses in each entries[] item",
                    actual: string.Join(", ", missingStructureFields),
                    repairHint: "Для canonical faction_structure.json каждая запись в entries[] должна хранить полные ranks и structuredBonuses, а не partial fragment."));
                continue;
            }

            if (RequireObject(ranks, $"{itemContext}.ranks", issues))
            {
                if (!TryGetArray(ranks, "branches", $"{itemContext}.ranks.branches", issues, out var branches))
                    continue;
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
                        ValidateFactionRankObject(rank, $"{branchContext}.ranks[{rankIndex++}]", issues);
                }
            }

            if (structuredBonuses.ValueKind != JsonValueKind.Null)
                ValidateArrayItems(structuredBonuses, $"{itemContext}.structuredBonuses", issues, ValidateFactionStructuredBonusObject);
        }
    }

    private void ValidateFactionResourcesStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateWorldQuestCombatFactionContract(root, contextPrefix, issues);
        if (!TryGetArray(root, "entries", $"{contextPrefix}.entries", issues, out var entries))
            return;

        var index = 0;
        foreach (var item in entries.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateCanonicalFactionIdentity(item, itemContext, issues);
            var missingResourceFields = new List<string>();
            if (!item.TryGetProperty("metaResources", out var metaResources))
                missingResourceFields.Add("metaResources");
            if (!item.TryGetProperty("strategicGoods", out var strategicGoods))
                missingResourceFields.Add("strategicGoods");

            if (missingResourceFields.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "Canonical faction_resources entry не содержит обязательные корневые поля",
                    code: "canonical_faction_resources_missing_required_fields",
                    section: "Factions",
                    expected: "metaResources and strategicGoods in each entries[] item",
                    actual: string.Join(", ", missingResourceFields),
                    repairHint: "Для canonical faction_resources.json каждая запись в entries[] должна хранить оба массива: metaResources и strategicGoods."));
                continue;
            }

            ValidateFactionSidecarResourceArray(metaResources, $"{itemContext}.metaResources", issues, requireUpkeep: true);
            ValidateFactionSidecarResourceArray(strategicGoods, $"{itemContext}.strategicGoods", issues, requireUpkeep: false);
        }
    }

    private void ValidateFactionProjectsStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateWorldQuestCombatFactionContract(root, contextPrefix, issues);
        if (root.TryGetProperty("activeProjects", out var activeProjects))
            ValidateCanonicalFactionProjectsArray(activeProjects, $"{contextPrefix}.activeProjects", issues, completed: false);
        if (root.TryGetProperty("completedProjects", out var completedProjects))
            ValidateCanonicalFactionProjectsArray(completedProjects, $"{contextPrefix}.completedProjects", issues, completed: true);
    }

    private void ValidateFactionCustomStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateWorldQuestCombatFactionContract(root, contextPrefix, issues);
        if (!TryGetArray(root, "entries", $"{contextPrefix}.entries", issues, out var entries))
            return;

        var index = 0;
        foreach (var item in entries.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateCanonicalFactionIdentity(item, itemContext, issues);
            if (!TryGetArray(item, "customStates", $"{itemContext}.customStates", issues, out var customStates))
                continue;

            var stateIndex = 0;
            foreach (var state in customStates.EnumerateArray())
            {
                var stateContext = $"{itemContext}.customStates[{stateIndex++}]";
                ValidateCanonicalFactionCustomStateObject(state, stateContext, issues);
            }
        }
    }

    private void ValidateInventoryItemTextStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidatePlayerContract(root, contextPrefix, issues);

        if (!TryGetArray(root, "entries", $"{contextPrefix}.entries", issues, out var entries))
            return;

        var index = 0;
        foreach (var item in entries.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.entries[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateInventoryItemReference(item, itemContext, issues);
            if (!TryGetArray(item, "textContent", $"{itemContext}.textContent", issues, out var textContent))
                continue;

            var textIndex = 0;
            foreach (var entry in textContent.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(entry.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.textContent[{textIndex}]",
                        IssueSeverity.Error,
                        "textContent entries must be non-empty strings"));
                }

                textIndex++;
            }
        }
    }

    private async Task ValidateFlexibleStateFile(string filePath, HashSet<string>? allowedKeys,
        List<ValidationIssue> issues, Action<JsonElement, string, List<ValidationIssue>> validator)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object &&
                doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Файл должен иметь корневой JSON object или array",
                    code: "flexible_state_invalid_root",
                    section: "StateFile",
                    expected: "JSON object or JSON array",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Сохрани {filePath} как canonical JSON object или array для этого state surface."));
                return;
            }

            if (allowedKeys != null &&
                doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var visibleProps = doc.RootElement.EnumerateObject()
                    .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (visibleProps.Count > 0 && !visibleProps.Any(prop => allowedKeys.Contains(prop.Name)))
                {
                    issues.Add(new ValidationIssue(
                        filePath,
                        IssueSeverity.Error,
                        "Файл не содержит ни одного допустимого top-level ключа для своего контракта",
                        code: "missing_allowed_top_level_key",
                        expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                        actual: string.Join(", ", visibleProps.Select(prop => prop.Name)),
                        repairHint: "Используй только допустимые top-level ключи для этого state file и не подменяй canonical command name произвольным alias-ом."));
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
                            code: "flexible_state_unknown_top_level_key",
                            section: "StateFile",
                            expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                            actual: prop.Name,
                            repairHint: "Удали неподдерживаемый top-level ключ и используй только canonical state command names для этого файла."));
                    }
                }
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind is not (JsonValueKind.Object or JsonValueKind.String))
                    {
                        issues.Add(new ValidationIssue(
                            $"{filePath}[{index}]",
                            IssueSeverity.Error,
                            "Элемент корневого массива должен быть объектом или строкой"));
                    }
                    index++;
                }
                return;
            }

            validator(doc.RootElement, filePath, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                $"Невалидный JSON: {ex.Message}",
                code: "flexible_state_invalid_json",
                section: "StateFile",
                expected: "valid JSON object or array",
                actual: "invalid JSON",
                repairHint: $"Исправь {filePath} до валидного JSON, не меняя его canonical state contract."));
        }
    }

    private async Task ValidateStrictTopLevelObjectFileAsync(string filePath, HashSet<string> allowedKeys, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Файл должен иметь корневой JSON object",
                    code: "strict_state_invalid_root",
                    section: "StateFile",
                    expected: "JSON object",
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Сохрани {filePath} как canonical JSON object с допустимыми top-level ключами: {string.Join(", ", allowedKeys.OrderBy(x => x))}."));
                return;
            }

            var visibleProps = doc.RootElement.EnumerateObject()
                .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (visibleProps.Count == 0)
                return;

            if (!visibleProps.Any(prop => allowedKeys.Contains(prop.Name)))
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Файл не содержит ни одного допустимого top-level ключа для своего контракта",
                    code: "strict_state_missing_allowed_top_level_key",
                    section: "StateFile",
                    expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                    actual: string.Join(", ", visibleProps.Select(prop => prop.Name)),
                    repairHint: "Используй только canonical top-level ключи для этого state file и не подменяй их произвольными alias-ами."));
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
                        code: "strict_state_unknown_top_level_key",
                        section: "StateFile",
                        expected: string.Join(", ", allowedKeys.OrderBy(x => x)),
                        actual: prop.Name,
                        repairHint: "Удали неподдерживаемый top-level ключ и оставь только canonical state contract keys для этого файла."));
                }
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                $"Невалидный JSON: {ex.Message}",
                code: "strict_state_invalid_json",
                section: "StateFile",
                expected: "valid JSON object",
                actual: "invalid JSON",
                repairHint: $"Исправь {filePath} до валидного JSON-объекта, не меняя canonical state contract."));
        }
    }

    private async Task<string?> ReadGmThoughtsMarkdownAsync()
    {
        var json = await _fs.ReadFileAsync("output/debug_logs.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gm) &&
                   gm.ValueKind == JsonValueKind.String
                ? gm.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

}

