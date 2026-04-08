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
        await ValidateFlexibleStateFile(NpcInteractionJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty, NpcInteractionJournalState.UpdateProperty
            }, issues, ValidateNpcInteractionJournalStateFile);
        await ValidateStrictTopLevelObjectFileAsync(NpcInteractionJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty
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
        await ValidateFlexibleStateFile(GuardianAbodeResidentState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentState.UpdateProperty,
                GuardianAbodeResidentState.EntriesProperty,
                GuardianAbodeResidentState.UpdateRosterReceiptsProperty,
                GuardianAbodeResidentState.RosterReceiptsProperty,
                GuardianAbodeResidentState.UpdateInteractionReceiptsProperty,
                GuardianAbodeResidentState.InteractionReceiptsProperty,
                GuardianAbodeResidentState.UpdateHistoryLogProperty,
                GuardianAbodeResidentState.HistoryLogProperty,
                GuardianAbodeResidentState.UpdateThoughtJournalProperty,
                GuardianAbodeResidentState.ThoughtJournalProperty,
                GuardianAbodeResidentState.UpdateInteractionLogProperty,
                GuardianAbodeResidentState.InteractionLogProperty
            }, issues, ValidateGuardianAbodeResidentsStateFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentState.UpdateProperty,
                GuardianAbodeResidentState.EntriesProperty,
                GuardianAbodeResidentState.UpdateRosterReceiptsProperty,
                GuardianAbodeResidentState.RosterReceiptsProperty,
                GuardianAbodeResidentState.UpdateInteractionReceiptsProperty,
                GuardianAbodeResidentState.InteractionReceiptsProperty,
                GuardianAbodeResidentState.UpdateHistoryLogProperty,
                GuardianAbodeResidentState.HistoryLogProperty,
                GuardianAbodeResidentState.UpdateThoughtJournalProperty,
                GuardianAbodeResidentState.ThoughtJournalProperty,
                GuardianAbodeResidentState.UpdateInteractionLogProperty,
                GuardianAbodeResidentState.InteractionLogProperty
            }, issues);
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
        await ValidateFlexibleStateFile(GuardianThoughtJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty, GuardianThoughtJournalState.UpdateProperty
            }, issues, ValidateGuardianThoughtJournalStateFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianThoughtJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianSocialJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty, GuardianSocialJournalState.UpdateProperty
            }, issues, ValidateGuardianSocialJournalStateFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianSocialJournalState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorJournalState.EntriesProperty
            }, issues);
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
        await ValidateFlexibleStateFile(NpcTradeRequestState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NpcTradeRequestState.RequestsProperty,
                "requestId", "npcId", "npcName", "merchantProfile", "tradeCycleId", "derivedTradeSlotCount", "createdAtTurn", "createdAtUtc", "createdAtWorldDate", "refreshAfterWorldDate"
            }, issues, ValidatePendingNpcTradeInventoryRequestsFile);
        await ValidateStrictTopLevelObjectFileAsync(NpcTradeRequestState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NpcTradeRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ResidentsRequestsProperty,
                "requestId", "guardianId", "guardianName", "abodeId", "abodeName", "currentReputation", "createdAtTurn", "createdAtUtc"
            }, issues, ValidatePendingGuardianAbodeResidentsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ResidentsRequestsProperty,
                "requestId", "guardianId", "guardianName", "abodeId", "abodeName", "currentReputation", "createdAtTurn", "createdAtUtc"
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.InteractionRequestsProperty
            }, issues, ValidatePendingGuardianAbodeResidentInteractionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.InteractionRequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ManifestationRequestsProperty
            }, issues, ValidatePendingResidentCompanionManifestationRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.ManifestationRequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorSocialInteractionRequestState.RequestsProperty
            }, issues, ValidatePendingGuardianSocialInteractionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorSocialInteractionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ActorSocialInteractionRequestState.PendingNpcRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorSocialInteractionRequestState.RequestsProperty
            }, issues, ValidatePendingNpcSocialInteractionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ActorSocialInteractionRequestState.RequestsProperty
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

            var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
            if (!guardianPolicyContext.CurrentStateReadable)
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

            if (!TryGetCurrentAuthorityActiveGuardian(guardianPolicyContext, out var activeGuardian))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.activeGuardian",
                    IssueSeverity.Error,
                    "После deterministic attraction к системному Хранителю должен существовать kernel-authoritative activeGuardian.",
                    code: "system_guardian_attraction_missing_active_guardian",
                    section: "SystemGuardianPresets",
                    expected: $"activeGuardian.sourcePreset.presetId = {targetPresetId}",
                    actual: guardianPolicyContext.HasCurrentActiveGuardian
                        ? $"raw mirror without current guardian authority ({DescribeCurrentGuardianAuthorityFailure(guardianPolicyContext)})"
                        : DescribeCurrentGuardianAuthorityFailure(guardianPolicyContext)));
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
        => ValidatePendingAbodeOfferingContract(root, contextPrefix, "GuardianOfferings", issues);

    private void ValidatePendingAbodeOfferingContract(JsonElement root, string contextPrefix, string section, List<ValidationIssue> issues)
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
                section: section,
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
                    section: section,
                    expected: "50 | 100 | 150",
                    actual: offered.ToString(),
                    repairHint: "Поддерживаемые offering amounts: 50, 100 или 150 Чернильных Перьев."));
            }
        }
        else if (string.Equals(offeringType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            RequireString(root, contextPrefix, issues, "relicId");
            RequireString(root, contextPrefix, issues, "relicName");
            var relicRarity = RequireString(root, contextPrefix, issues, "relicRarity");
            if (!string.IsNullOrWhiteSpace(relicRarity) && !GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(relicRarity))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.relicRarity",
                    IssueSeverity.Error,
                    "pending_abode_offering relicRarity должна быть canonical Soul Relic rarity tier",
                    code: "abode_offering_soul_relic_invalid_rarity",
                    section: section,
                    expected: GuardianAbodeOfferingState.DescribeCanonicalSoulRelicRarities(),
                    actual: relicRarity,
                    repairHint: "Используй для soul_relic только canonical rarity/quality enum из Soul Relic contract."));
            }
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
                    section: section,
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
                    section: section,
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
                section: section,
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

    private async Task<string?> ResolveGuardianValidatedPreTurnRealmForContextAsync(
        string requestPath,
        List<ValidationIssue> issues,
        string code,
        string section)
    {
        var resolution = await ResolveValidatedCurrentPreTurnRealmAsync();
        if (resolution.SnapshotStatus != ValidatedPendingTurnSnapshotStatus.Usable)
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                $"{requestPath} нельзя проверить по pre-turn realm, потому что validated pending turn snapshot недоступен или не соответствует текущему accepted-turn context.",
                code: code,
                section: section,
                expected: "current validated pending turn snapshot with game_state/meta/soul_state.json",
                actual: DescribeValidatedPendingTurnSnapshotStatus(resolution.SnapshotStatus),
                repairHint: "Не используй stale или изменённый pending turn snapshot; manifest должен совпадать с текущими sessionId/requestId/turnNumber и содержать snapshot game_state/meta/soul_state.json."));
        }

        return resolution.Realm;
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
            await ValidatePendingAbodeOfferingRequestRealmContextAsync(
                GuardianAbodeOfferingState.PendingRequestPath,
                "GuardianOfferings",
                issues);
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
        var requestResolution = await ReadValidatedPendingResolutionContractAsync<GuardianAbodeOfferingState.PendingAbodeOfferingRequest>(
            GuardianAbodeOfferingState.PendingRequestPath,
            issues,
            missingCode: "abode_offering_missing_validated_snapshot_request",
            invalidCode: "abode_offering_invalid_validated_snapshot_request",
            section: "GuardianOfferings",
            missingMessage: "Strict validated pending turn snapshot contract для pending_abode_offering.json недоступен. Guardian offering нельзя проверить строго.",
            invalidMessage: "validated pending turn snapshot для pending_abode_offering.json существует, но request contract внутри него unreadable или malformed. Guardian offering нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_abode_offering.json в manifest.Files и snapshotFileHashes, а snapshot copy должна оставаться валидным JSON contract с теми же session/request/turn coordinates.",
            semanticValidator: (root, contextPrefix, validationIssues) => ValidatePendingAbodeOfferingContract(root, contextPrefix, "GuardianOfferings", validationIssues));
        if (requestResolution.Status != PendingResolutionContractStatus.Resolved || requestResolution.Contract == null)
            return;

        var request = requestResolution.Contract;
        var issueCountBeforeContextValidation = issues.Count;
        await ValidatePendingAbodeOfferingRequestRealmContextAsync(
            GuardianAbodeOfferingState.PendingRequestPath,
            "GuardianOfferings",
            issues);
        if (issues.Count > issueCountBeforeContextValidation)
            return;

        var expectedGain = GuardianAbodeOfferingState.ResolvePowerGainForPendingRequest(request);
        if (expectedGain <= 0)
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeOfferingState.PendingRequestPath,
                IssueSeverity.Error,
                "validated pending_abode_offering snapshot не даёт корректный canonical offering power gain contract.",
                code: "abode_offering_invalid_validated_snapshot_request",
                section: "GuardianOfferings",
                expected: "supported offering contract with positive power gain",
                actual: "resolved offering gain <= 0",
                repairHint: "Исправь validated snapshot contract pending_abode_offering.json; offeringType и audit-driving fields должны задавать положительный canonical power gain."));
            return;
        }

        var preJournalJson = await ReadRequiredValidatedPendingTurnSnapshotFileAsync(
            GuardianPowerEventState.JournalPath,
            issues,
            code: "abode_offering_missing_validated_snapshot_journal",
            section: "GuardianOfferings",
            message: "pending_abode_offering strict resolution требует validated pre-turn abode_power_journal baseline; без него journal proof нельзя проверить строго.",
            repairHint: $"Сохраняй {GuardianPowerEventState.JournalPath} в validated pending turn snapshot и не проверяй pending_abode_offering по current-only journal.");
        if (string.IsNullOrWhiteSpace(preJournalJson))
            return;

        var offeringProofScope = CreateGuardianPowerEventProofScopeForOffering(request);
        var preTurnJournalKnowledgeResult = await ReadValidatedPreTurnGuardianPowerJournalProofKnowledgeAsync(offeringProofScope);
        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotGuardians)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует canonical validated snapshot guardians baseline для journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_guardians",
                section: "GuardianOfferings",
                expected: "canonical validated snapshot guardians.json for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot guardians baseline invalid",
                repairHint: "Сохраняй в validated pending turn snapshot canonical game_state/meta/guardians.json. Proof knowledge не может строиться из partial или invalid guardian snapshot."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotTracker)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует canonical validated snapshot guardian project tracker baseline для journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_tracker",
                section: "GuardianOfferings",
                expected: $"canonical validated snapshot {GuardianProjectState.TrackerPath} for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot tracker baseline invalid",
                repairHint: $"Сохраняй в validated pending turn snapshot canonical {GuardianProjectState.TrackerPath}. Proof knowledge не может строиться из partial или invalid tracker snapshot."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Status == GuardianPowerJournalProofKnowledgeStatus.InvalidValidatedSnapshotJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует canonical validated snapshot abode_power_journal baseline для journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: "GuardianOfferings",
                expected: $"canonical validated snapshot {GuardianPowerEventState.JournalPath} for offering proof knowledge",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot journal baseline invalid",
                repairHint: $"Сохраняй в validated pending turn snapshot canonical {GuardianPowerEventState.JournalPath}; offering proof knowledge не может строиться из missing, stale или invalid journal baseline."));
            return;
        }

        if (preTurnJournalKnowledgeResult.Knowledge == null)
        {
            issues.Add(new ValidationIssue(
                PendingTurnSnapshotManifestPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует usable validated snapshot proof context для journal proof knowledge.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: "GuardianOfferings",
                expected: "usable validated snapshot proof knowledge context",
                actual: preTurnJournalKnowledgeResult.FailureDescription ?? "validated snapshot context unavailable",
                repairHint: "Сохраняй current validated pending turn snapshot manifest и canonical guardian/tracker baselines перед strict pending_abode_offering resolution."));
            return;
        }

        var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        var journalProof = SummarizeOfferingJournalProof(
            preJournalJson,
            postJournalJson,
            request,
            expectedGain,
            preTurnJournalKnowledgeResult.Knowledge,
            "offering");
        if (journalProof.Status == OfferingJournalProofStatus.InvalidValidatedBaseline)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует readable validated pre-turn abode_power_journal baseline; corrupted snapshot journal нельзя считать empty baseline.",
                code: "abode_offering_invalid_validated_snapshot_journal",
                section: "GuardianOfferings",
                expected: "readable validated snapshot abode_power_journal baseline",
                actual: "validated snapshot journal unreadable or malformed",
                repairHint: "Сохраняй в validated pending turn snapshot корректный JSON abode_power_journal и не доказывай offering через current-only journal."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentGuardianAuthority)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering strict resolution не может доказать journal outcome: current guardian authority unreadable или unavailable.",
                code: "abode_offering_invalid_current_guardian_authority",
                section: "GuardianOfferings",
                expected: "readable current guardian authority root",
                actual: journalProof.FailureDescription ?? "current guardian authority unavailable",
                repairHint: "Исправь current game_state/meta/guardians.json и validated guardian baseline так, чтобы kernel построил strict current guardian authority перед proving pending_abode_offering outcome."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentTrackerAuthority)
        {
            issues.Add(new ValidationIssue(
                GuardianProjectState.TrackerPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution не может доказать journal outcome: current guardian project tracker authority unreadable или unavailable.",
                code: "abode_offering_invalid_current_tracker_authority",
                section: "GuardianOfferings",
                expected: $"readable current authority root for {GuardianProjectState.TrackerPath}",
                actual: journalProof.FailureDescription ?? "current tracker authority unavailable",
                repairHint: $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил strict current tracker authority перед proving pending_abode_offering outcome."));
            return;
        }

        if (journalProof.Status == OfferingJournalProofStatus.InvalidCurrentJournal)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "pending_abode_offering strict resolution не может доказать journal outcome: current abode_power_journal.json unreadable или malformed.",
                code: "abode_offering_invalid_current_journal_proof",
                section: "GuardianOfferings",
                expected: "readable current abode_power_journal proof",
                actual: journalProof.FailureDescription ?? "current journal unreadable or malformed",
                repairHint: "Делай current abode_power_journal.json корректным JSON и materialize offering proof только через читаемый strict journal."));
            return;
        }

        if (!journalProof.MatchingOfferingEventFound)
        {
            issues.Add(new ValidationIssue(
                GuardianPowerEventState.JournalPath,
                IssueSeverity.Error,
                "pending_abode_offering.json не привёл к ожидаемому offering event в journal",
                code: "abode_offering_missing_matching_power_event",
                section: "GuardianOfferings",
                repairHint: "Разрешай pending_abode_offering.json через guardianPowerEvents.reasonType=offering с audit, совпадающим с client-authored request."));
        }

        var preTurnGuardiansJson = await ReadRequiredValidatedPendingTurnSnapshotFileAsync(
            "game_state/meta/guardians.json",
            issues,
            code: "abode_offering_missing_validated_snapshot_guardians",
            section: "GuardianOfferings",
            message: "pending_abode_offering strict resolution требует validated pre-turn guardians baseline; без него pre-turn guardian power нельзя проверить строго.",
            repairHint: "Сохраняй game_state/meta/guardians.json в validated pending turn snapshot и доказывай pending_abode_offering power proof через canonical pre-turn guardian baseline.");
        if (string.IsNullOrWhiteSpace(preTurnGuardiansJson))
            return;

        var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var snapshotManifest = snapshotLookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
            ? snapshotLookup.Manifest
            : null;
        var preTurnTrackerJson = snapshotManifest == null
            ? null
            : await ReadValidatedPendingTurnSnapshotFileAsync(snapshotManifest, GuardianProjectState.TrackerPath);
        var preTurnSnapshotSoulJson = snapshotManifest == null
            ? null
            : await ReadValidatedPendingTurnSnapshotFileAsync(snapshotManifest, "game_state/meta/soul_state.json");

        if (!TryReadCanonicalGuardianSnapshotForProof(
                preTurnGuardiansJson,
                "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
                preTurnTrackerJson,
                snapshotManifest?.Files != null && snapshotManifest.Files.ContainsKey(GuardianProjectState.TrackerPath),
                $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}",
                preJournalJson,
                $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}",
                preTurnSnapshotSoulJson,
                offeringProofScope,
                CreateGuardianPowerEventAuthorityScopeForGuardian(request.GuardianId),
                out _,
                out var preTurnGuardiansById,
                out _,
                out var preTurnGuardianFailureDescription))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует canonical validated snapshot guardians baseline для pre-turn guardian power proof.",
                code: "abode_offering_invalid_validated_snapshot_guardians",
                section: "GuardianOfferings",
                expected: "canonical validated snapshot guardians.json for pending abode offering power proof",
                actual: preTurnGuardianFailureDescription,
                repairHint: "Сохраняй в validated pending turn snapshot canonical game_state/meta/guardians.json; pre-turn offering power proof не может строиться из partial или invalid guardian baseline."));
            return;
        }

        if (!preTurnGuardiansById.TryGetValue(request.GuardianId, out var preTurnGuardian))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering strict resolution требует guardian baseline для того Хранителя, относительно которого доказывается power gain.",
                code: "abode_offering_invalid_validated_snapshot_guardians",
                section: "GuardianOfferings",
                expected: $"canonical guardian baseline entry for {request.GuardianId}",
                actual: $"guardianId {request.GuardianId} missing from validated snapshot guardians[]",
                repairHint: $"Сохраняй в validated pending turn snapshot canonical guardians[] entry для {request.GuardianId}; pending_abode_offering power proof не может опираться на baseline без target guardian."));
            return;
        }

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        var previousPower = (int?)AbodePowerRules.GetCurrentPower(preTurnGuardian);
        if (!TryEnsureCurrentGuardianAuthorityForPowerEventSensitiveOutcome(guardianPolicyContext, out var currentGuardianAuthorityFailure))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "pending_abode_offering не может быть доказан: current guardian authority unreadable или unavailable для strict power proof.",
                code: "abode_offering_invalid_current_guardian_authority",
                section: "GuardianOfferings",
                expected: "readable current guardian authority root",
                actual: currentGuardianAuthorityFailure,
                repairHint: "Исправь current game_state/meta/guardians.json, validated guardian baselines и raw guardianPowerEvents так, чтобы validator построил strict current guardian authority перед pending_abode_offering power proof."));
            return;
        }

        var currentPower = TryReadGuardianCurrentAbodePowerFromPolicyContext(guardianPolicyContext, request.GuardianId);
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

        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            var preTurnSoulJson = await ReadRequiredValidatedPendingTurnSnapshotFileAsync(
                "game_state/meta/soul_state.json",
                issues,
                code: "abode_offering_missing_validated_snapshot_soul_state",
                section: "GuardianOfferings",
                message: "pending_abode_offering strict resolution требует validated pre-turn soul_state baseline; без него consumption proof нельзя проверить строго.",
                repairHint: "Сохраняй game_state/meta/soul_state.json в validated pending turn snapshot и доказывай offering через pre-turn ownership плюс current consumption.");
            if (string.IsNullOrWhiteSpace(preTurnSoulJson))
                return;

            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            ValidatePendingAbodeOfferingConsumptionProof(
                preTurnSoulJson,
                soulJson,
                request,
                "GuardianOfferings",
                issues);
        }
    }

    private async Task ValidatePendingGuardianTradeRequestContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(GuardianTradeRequestState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        GuardianTradeRequestState.PendingGuardianTradeRequest? request;
        try
        {
            using var doc = JsonDocument.Parse(json);
            ValidatePendingGuardianTradeRequestStateFile(doc.RootElement, GuardianTradeRequestState.PendingRequestPath, "GuardianTrade", issues);
            request = JsonSerializer.Deserialize<GuardianTradeRequestState.PendingGuardianTradeRequest>(json);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                GuardianTradeRequestState.PendingRequestPath,
                IssueSeverity.Error,
                $"pending_guardian_trade_request.json не читается как валидный JSON: {ex.Message}",
                code: "guardian_trade_request_invalid_json",
                section: "GuardianTrade",
                repairHint: "Сохраняй pending_guardian_trade_request.json как корректный client-authored JSON contract."));
            return;
        }

        if (request == null)
            return;

        await ValidatePendingGuardianTradeRequestRealmContextAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "GuardianTrade",
            issues);
    }

    private async Task ValidatePendingGuardianTradeRequestResolutionAsync(List<ValidationIssue> issues)
    {
        var requestResolution = await ReadValidatedPendingResolutionContractAsync<GuardianTradeRequestState.PendingGuardianTradeRequest>(
            GuardianTradeRequestState.PendingRequestPath,
            issues,
            missingCode: "guardian_trade_request_missing_validated_snapshot_request",
            invalidCode: "guardian_trade_request_invalid_validated_snapshot_request",
            section: "GuardianTrade",
            missingMessage: "Strict validated pending turn snapshot contract для pending_guardian_trade_request.json недоступен. Guardian trade нельзя проверить строго.",
            invalidMessage: "validated pending turn snapshot для pending_guardian_trade_request.json существует, но request contract внутри него unreadable или malformed. Guardian trade нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй current pending_guardian_trade_request.json в manifest.Files и snapshotFileHashes, а snapshot copy должна оставаться валидным JSON contract.",
            semanticValidator: (root, contextPrefix, validationIssues) => ValidatePendingGuardianTradeRequestStateFile(root, contextPrefix, "GuardianTrade", validationIssues));
        if (requestResolution.Status != PendingResolutionContractStatus.Resolved || requestResolution.Contract == null)
            return;

        var request = requestResolution.Contract;
        var issueCountBeforeContextValidation = issues.Count;
        await ValidatePendingGuardianTradeRequestRealmContextAsync(
            GuardianTradeRequestState.PendingRequestPath,
            "GuardianTrade",
            issues);
        if (issues.Count > issueCountBeforeContextValidation)
            return;

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!guardianPolicyContext.CurrentStateReadable)
        {
            issues.Add(new ValidationIssue(
                GuardianTradeRequestState.PendingRequestPath,
                IssueSeverity.Error,
                "pending_guardian_trade_request не может быть строго разрешён: current guardians.json unreadable.",
                code: "guardian_trade_request_missing_guardian_resolution",
                section: "GuardianTrade",
                expected: $"readable materialized guardian {request.GuardianId} in current guardians[]",
                actual: "current guardians.json unreadable",
                repairHint: "Сделай current guardians.json readable и materialize-ь guardian entry, которая закрывает pending_guardian_trade_request exact contract."));
            return;
        }

        try
        {
            if (!TryBuildGuardianTradeResolutionGuardian(guardianPolicyContext, request.GuardianId, out var guardian, out var tradeResolutionActual))
            {
                issues.Add(new ValidationIssue(
                    GuardianTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_trade_request из pre-turn snapshot не привёл к authority-backed guardian resolution.",
                    code: "guardian_trade_request_missing_guardian_resolution",
                    section: "GuardianTrade",
                    expected: $"authority-backed guardian {request.GuardianId} in current guardians state",
                    actual: tradeResolutionActual,
                    repairHint: "Разрешай pending_guardian_trade_request только через authority-backed guardian state. Current guardian materialization должна совпадать с kernel authority, а tradeInventory/receipt должны materialize exact client-authored request contract."));
                return;
            }

            JsonObject? tradeInventory = null;
            if (guardian["tradeInventory"] is JsonObject tradeInventoryObject)
                tradeInventory = tradeInventoryObject;

            if (!GuardianTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request))
            {
                issues.Add(new ValidationIssue(
                    GuardianTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_trade_request из pre-turn snapshot не привёл к matching guardian.tradeInventory",
                    code: "guardian_trade_request_missing_inventory_resolution",
                    section: "GuardianTrade",
                    repairHint: "На accepted turn обязательно materialize-ь guardian.tradeInventory по exact client-authored request contract; не игнорируй request и не закрывай его частично совпадающей витриной."));
                return;
            }

            if (!GuardianTradeRequestState.ReceiptMatchesRequestContract(
                    guardian == null ? null : GuardianTradeRequestState.FindMatchingReceipt(guardian, request),
                    request,
                    tradeInventory))
            {
                issues.Add(new ValidationIssue(
                    GuardianTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_trade_request из pre-turn snapshot не был закрыт canonical tradeInventory receipt",
                    code: "guardian_trade_request_missing_receipt_resolution",
                    section: "GuardianTrade",
                    repairHint: $"После materialize explicit guardian.tradeInventory обязательно закрой запрос через {GuardianTradeRequestState.UpdateReceiptsProperty} и запиши matching requestId/tradeCycleId/itemCount timing в guardians[].{GuardianTradeRequestState.ReceiptsProperty}."));
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task<PendingResolutionContractReadResult<TContract>> ReadValidatedPendingResolutionContractAsync<TContract>(
        string relativePath,
        List<ValidationIssue> issues,
        string missingCode,
        string invalidCode,
        string section,
        string missingMessage,
        string invalidMessage,
        string repairHint,
        Action<JsonElement, string, List<ValidationIssue>>? semanticValidator = null)
        where TContract : class
    {
        var currentJson = await _fs.ReadFileAsync(relativePath);
        var hasCurrentFile = !string.IsNullOrWhiteSpace(currentJson);
        var rawManifestJson = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        var rawManifestExists = !string.IsNullOrWhiteSpace(rawManifestJson);
        var rawManifest = await LoadValidationPendingTurnSnapshotManifestAsync();
        if (rawManifestExists && rawManifest == null)
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                invalidMessage,
                code: invalidCode,
                section: section,
                expected: $"readable pending turn snapshot manifest registering {relativePath}",
                actual: "pending_turn_snapshot.json exists but is unreadable or malformed",
                repairHint: repairHint));
            return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.InvalidValidatedSnapshot, null);
        }

        var hasConventionalSnapshotCopy = _fs.FileExists(_fs.ResolvePath($"game_state/control/pending_turn_snapshot/{relativePath}"));
        var hasRawManifestReference = rawManifestJson?.Contains(relativePath, StringComparison.OrdinalIgnoreCase) == true;
        var hasPreTurnContractEvidence = hasCurrentFile ||
                                         hasConventionalSnapshotCopy ||
                                         hasRawManifestReference ||
                                         (rawManifest != null && HasPendingResolutionValidatedSnapshotRegistration(rawManifest, relativePath));
        if (!hasPreTurnContractEvidence)
            return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.NoPreTurnContract, null);

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                missingMessage,
                code: missingCode,
                section: section,
                expected: $"validated pending turn snapshot manifest with {relativePath}",
                actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                repairHint: repairHint));
            return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.MissingValidatedSnapshot, null);
        }

        var hasValidatedSnapshotRegistration = HasPendingResolutionValidatedSnapshotRegistration(lookup.Manifest, relativePath);
        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, relativePath);
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                missingMessage,
                code: missingCode,
                section: section,
                expected: $"validated snapshot entry for {relativePath}",
                actual: (hasValidatedSnapshotRegistration || !hasCurrentFile)
                    ? "validated snapshot contract is registered but snapshot entry/file is missing or unreadable"
                    : "current request exists but manifest.Files/snapshotFileHashes entry is missing or unreadable",
                repairHint: repairHint));
            return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.MissingValidatedSnapshot, null);
        }

        try
        {
            using var snapshotDoc = JsonDocument.Parse(snapshotJson);
            if (semanticValidator != null)
            {
                var issueCountBeforeValidation = issues.Count;
                semanticValidator(snapshotDoc.RootElement, relativePath, issues);
                if (issues.Count > issueCountBeforeValidation)
                    return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.InvalidValidatedSnapshot, null);
            }

            var contract = JsonSerializer.Deserialize<TContract>(snapshotDoc.RootElement.GetRawText());
            if (contract != null)
                return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.Resolved, contract);
        }
        catch
        {
            // explicit fail-closed issue emitted below
        }

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            invalidMessage,
            code: invalidCode,
            section: section,
            expected: $"well-formed validated snapshot contract for {relativePath}",
            actual: "validated snapshot entry exists but JSON contract is unreadable or malformed",
            repairHint: repairHint));

        return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.InvalidValidatedSnapshot, null);
    }

    private static bool HasPendingResolutionValidatedSnapshotRegistration(ValidationPendingTurnSnapshotManifest manifest, string relativePath)
    {
        return manifest.Files.ContainsKey(relativePath) ||
               manifest.SnapshotFileHashes.ContainsKey(relativePath) ||
               manifest.RollbackBackups.ContainsKey(relativePath);
    }

    private async Task ValidatePendingAbodeOfferingRequestRealmContextAsync(
        string requestPath,
        string section,
        List<ValidationIssue> issues)
    {
        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            requestPath,
            issues,
            code: "abode_offering_invalid_validated_snapshot_context",
            section: section);
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                "pending_abode_offering.json допустим только в afterlife realm",
                code: "abode_offering_wrong_realm",
                section: section,
                expected: "Chaos Sea or Shining Abode pre-turn realm",
                actual: currentRealm,
                repairHint: "Не создавай и не разрешай pending_abode_offering.json вне afterlife realm."));
        }
    }

    private async Task ValidatePendingGuardianTradeRequestRealmContextAsync(
        string requestPath,
        string section,
        List<ValidationIssue> issues)
    {
        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            requestPath,
            issues,
            code: "guardian_trade_request_invalid_validated_snapshot_context",
            section: section);
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                "pending_guardian_trade_request.json допустим только в afterlife realm",
                code: "guardian_trade_request_wrong_realm",
                section: section,
                expected: "Chaos Sea or Shining Abode pre-turn realm",
                actual: currentRealm,
                repairHint: "Не создавай и не разрешай pending_guardian_trade_request.json вне afterlife realm."));
        }
    }

    private void ValidatePendingAbodeOfferingConsumptionProof(
        string? preTurnSoulJson,
        string? currentSoulJson,
        GuardianAbodeOfferingState.PendingAbodeOfferingRequest request,
        string section,
        List<ValidationIssue> issues)
    {
        if (string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            var preTurnRelicProof = ReadSoulRelicProofEntry(preTurnSoulJson, request.RelicId);
            if (preTurnRelicProof.Status == SoulStateEntryPresenceStatus.Unreadable ||
                preTurnRelicProof.Status == SoulStateEntryPresenceStatus.InvalidShape)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "pending abode offering с Soul Relic не может быть доказан: validated pre-turn soul_state unreadable или malformed.",
                    code: "abode_offering_invalid_validated_snapshot_soul_state",
                    section: section,
                    expected: "readable validated pre-turn soul_state baseline",
                    actual: "validated pre-turn soul_state unreadable or malformed",
                    repairHint: "Сохраняй в validated snapshot корректный JSON soul_state и доказывай relic offering через читаемый pre-turn ownership baseline."));
                return;
            }

            if (preTurnRelicProof.Status != SoulStateEntryPresenceStatus.Present)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "pending abode offering с Soul Relic не доказан: offered relic отсутствовал в validated pre-turn soul_state.",
                    code: "abode_offering_soul_relic_missing_preturn_ownership",
                    section: section,
                    expected: request.RelicId ?? "validated pre-turn owned Soul Relic",
                    actual: "relic missing from validated pre-turn soul_state",
                    repairHint: "Доказывай offering Soul Relic через validated pre-turn ownership и current consumption, а не только через journal/power outcome."));
                return;
            }

            if (preTurnRelicProof.Entry == null || !SoulRelicRequestMatchesProofEntry(request, preTurnRelicProof.Entry))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "pending abode offering с Soul Relic не доказан: authored relic metadata не совпадают с реально consumed pre-turn relic.",
                    code: "abode_offering_soul_relic_metadata_mismatch",
                    section: section,
                    expected: DescribeSoulRelicProofEntry(preTurnRelicProof.Entry),
                    actual: $"{request.RelicId} / {request.RelicName} / {request.RelicRarity}",
                    repairHint: "Синхронизируй pending_abode_offering и offering audit с canonical metadata той Soul Relic, которая реально существовала в validated pre-turn soul_state."));
                return;
            }

            var currentRelicProof = ReadSoulRelicProofEntry(currentSoulJson, request.RelicId);
            if (currentRelicProof.Status == SoulStateEntryPresenceStatus.Unreadable ||
                currentRelicProof.Status == SoulStateEntryPresenceStatus.InvalidShape)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "pending abode offering с Soul Relic не может быть доказан: current soul_state unreadable или malformed.",
                    code: "abode_offering_invalid_current_soul_state",
                    section: section,
                    expected: "readable current soul_state proving relic consumption",
                    actual: "current soul_state unreadable or malformed",
                    repairHint: "Делай current soul_state.json корректным JSON и доказывай relic consumption через читаемое current состояние души."));
                return;
            }

            if (currentRelicProof.Status == SoulStateEntryPresenceStatus.Present)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Поднесённая Реликвия Души всё ещё находится в soul_state после pending abode offering",
                    code: "abode_offering_soul_relic_not_consumed",
                    section: section,
                    repairHint: "Клиентский offering path должен локально удалить offered Soul Relic из soul_state перед GM turn."));
            }

            return;
        }

        if (!string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var preTurnArchiveProof = ReadAfterlifeArchiveProofEntry(preTurnSoulJson, request.ArchiveId);
        if (preTurnArchiveProof.Status == SoulStateEntryPresenceStatus.Unreadable ||
            preTurnArchiveProof.Status == SoulStateEntryPresenceStatus.InvalidShape)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "pending abode offering с archive entry не может быть доказан: validated pre-turn soul_state unreadable или malformed.",
                code: "abode_offering_invalid_validated_snapshot_soul_state",
                section: section,
                expected: "readable validated pre-turn soul_state baseline",
                actual: "validated pre-turn soul_state unreadable or malformed",
                repairHint: "Сохраняй в validated snapshot корректный JSON soul_state и доказывай archive offering через читаемый pre-turn ownership baseline."));
            return;
        }

        if (preTurnArchiveProof.Status != SoulStateEntryPresenceStatus.Present)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "pending abode offering с archive entry не доказан: offered archive entry отсутствовал в validated pre-turn soul_state.",
                code: "abode_offering_archive_entry_missing_preturn_ownership",
                section: section,
                expected: request.ArchiveId ?? "validated pre-turn owned archive entry",
                actual: "archive entry missing from validated pre-turn soul_state",
                repairHint: "Доказывай archive offering через validated pre-turn ownership и current consumption, а не только через journal/power outcome."));
            return;
        }

        if (preTurnArchiveProof.Entry == null || !ArchiveRequestMatchesProofEntry(request, preTurnArchiveProof.Entry))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "pending abode offering с archive entry не доказан: authored archive metadata не совпадают с реально consumed pre-turn archive entry.",
                code: "abode_offering_archive_entry_metadata_mismatch",
                section: section,
                expected: DescribeArchiveProofEntry(preTurnArchiveProof.Entry),
                actual: $"{request.ArchiveId} / {request.ArchiveTitle} / {request.ArchiveEntryType} / {request.ArchiveRarity}",
                repairHint: "Синхронизируй pending_abode_offering и offering audit с canonical metadata той archive entry, которая реально существовала в validated pre-turn soul_state."));
            return;
        }

        var currentArchiveProof = ReadAfterlifeArchiveProofEntry(currentSoulJson, request.ArchiveId);
        if (currentArchiveProof.Status == SoulStateEntryPresenceStatus.Unreadable ||
            currentArchiveProof.Status == SoulStateEntryPresenceStatus.InvalidShape)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "pending abode offering с archive entry не может быть доказан: current soul_state unreadable или malformed.",
                code: "abode_offering_invalid_current_soul_state",
                section: section,
                expected: "readable current soul_state proving archive consumption",
                actual: "current soul_state unreadable or malformed",
                repairHint: "Делай current soul_state.json корректным JSON и доказывай archive offering через читаемое current состояние души."));
            return;
        }

        if (currentArchiveProof.Status == SoulStateEntryPresenceStatus.Present)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Поднесённая архивная запись всё ещё находится в afterlifeArchive после pending abode offering",
                code: "abode_offering_archive_entry_not_consumed",
                section: section,
                repairHint: "Клиентский offering path должен локально удалить offered archive entry из soul_state.afterlifeArchive.stored перед GM turn."));
        }
    }

    private void ValidatePendingGuardianTradeRequestStateFile(JsonElement root, string contextPrefix, string section, List<ValidationIssue> issues)
    {
        var requestId = RequireString(root, contextPrefix, issues, "requestId");
        var guardianId = RequireString(root, contextPrefix, issues, "guardianId");
        var guardianName = RequireString(root, contextPrefix, issues, "guardianName");
        var abodeId = RequireString(root, contextPrefix, issues, "abodeId");
        var returnCycleId = RequireString(root, contextPrefix, issues, "returnCycleId");
        ValidateIntegerField(root, contextPrefix, issues, "currentReputation");
        ValidatePositiveNumberField(root, contextPrefix, issues, "derivedTradeSlotCount");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "effectiveRarityCeilingBonusSteps", section);
        var projectBonusSignature = RequireString(root, contextPrefix, issues, "projectBonusSignature");
        var derivedTradeSlotCount = TryReadIntField(root, "derivedTradeSlotCount", out var parsedSlotCount) ? parsedSlotCount : 0;
        var hasCurrentReputation = root.TryGetProperty("currentReputation", out var currentReputationProp) &&
                                   currentReputationProp.ValueKind == JsonValueKind.Number &&
                                   currentReputationProp.TryGetInt32(out _);
        var hasEffectiveRarityCeilingBonusSteps = root.TryGetProperty("effectiveRarityCeilingBonusSteps", out var rarityBonusProp) &&
                                                  rarityBonusProp.ValueKind == JsonValueKind.Number &&
                                                  rarityBonusProp.TryGetInt32(out var parsedRarityBonus) &&
                                                  parsedRarityBonus >= 0;
        var createdAtUtc = RequireString(root, contextPrefix, issues, "createdAtUtc");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", section);
        var hasCreatedAtTurn = root.TryGetProperty("createdAtTurn", out var createdAtTurnProp) &&
                               createdAtTurnProp.ValueKind == JsonValueKind.Number &&
                               createdAtTurnProp.TryGetInt32(out var createdAtTurn) &&
                               createdAtTurn >= 0;

        if (!string.IsNullOrWhiteSpace(createdAtUtc) && !DateTimeOffset.TryParse(createdAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.createdAtUtc",
                IssueSeverity.Error,
                "pending_guardian_trade_request.json.createdAtUtc должен быть ISO 8601 timestamp",
                code: "guardian_trade_request_invalid_created_at",
                section: section,
                expected: "ISO 8601 timestamp",
                actual: createdAtUtc));
        }

        if (string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(guardianId) ||
            string.IsNullOrWhiteSpace(guardianName) ||
            string.IsNullOrWhiteSpace(abodeId) ||
            string.IsNullOrWhiteSpace(returnCycleId) ||
            !hasCurrentReputation ||
            derivedTradeSlotCount <= 0 ||
            !hasEffectiveRarityCeilingBonusSteps ||
            string.IsNullOrWhiteSpace(projectBonusSignature) ||
            !hasCreatedAtTurn)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_guardian_trade_request.json должен содержать полный client-authored contract",
                code: "guardian_trade_request_missing_fields",
                section: section));
        }
    }

    private async Task<TContract?> ReadRequiredValidatedCurrentPreTurnTrackedContractAsync<TContract>(
        string relativePath,
        List<ValidationIssue> issues,
        string missingCode,
        string invalidCode,
        string section,
        string missingMessage,
        string invalidMessage,
        string repairHint)
        where TContract : class
    {
        var snapshotJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            relativePath,
            issues,
            code: missingCode,
            section: section,
            message: missingMessage,
            repairHint: repairHint);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        try
        {
            var contract = JsonSerializer.Deserialize<TContract>(snapshotJson);
            if (contract != null)
                return contract;
        }
        catch
        {
            // explicit fail-closed issue emitted below
        }

        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            invalidMessage,
            code: invalidCode,
            section: section,
            expected: $"well-formed validated snapshot contract for {relativePath}",
            actual: "validated snapshot entry exists but JSON contract is unreadable or malformed",
            repairHint: repairHint));

        return null;
    }

    private static bool TryBuildGuardianTradeResolutionGuardian(
        GuardianPolicyContext context,
        string guardianId,
        out JsonObject guardian,
        out string actual)
    {
        guardian = null!;
        actual = "guardian authority unavailable";

        if (!TryGetCurrentGuardian(context, guardianId, out var authorityGuardianElement))
        {
            actual = context.HasCurrentAuthorityRoot
                ? $"guardian {guardianId} missing from current guardian authority"
                : DescribeCurrentGuardianAuthorityFailure(context);
            return false;
        }

        if (JsonNode.Parse(authorityGuardianElement.GetRawText()) is not JsonObject authorityGuardian)
        {
            actual = $"guardian {guardianId} current authority unreadable";
            return false;
        }

        if (!TryGetCurrentMaterializedGuardian(context, guardianId, out var materializedGuardianElement))
        {
            actual = $"guardian {guardianId} missing from current guardians[]";
            return false;
        }

        if (JsonNode.Parse(materializedGuardianElement.GetRawText()) is not JsonObject materializedGuardian)
        {
            actual = $"guardian {guardianId} materialized guardian state unreadable";
            return false;
        }

        var authorityAbodeId = authorityGuardian["abode"] is JsonObject authorityAbode
            ? authorityAbode["abodeId"]?.GetValue<string>()
            : null;
        var materializedAbodeId = materializedGuardian["abode"] is JsonObject materializedAbode
            ? materializedAbode["abodeId"]?.GetValue<string>()
            : null;
        if (!string.Equals(materializedAbodeId, authorityAbodeId, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"guardian {guardianId} materialized guardian state points to non-authoritative abode";
            return false;
        }

        if (!MaterializedGuardianMatchesAuthorityOutsideTradeResolutionSurface(authorityGuardian, materializedGuardian))
        {
            actual = $"guardian {guardianId} materialized guardian state diverges from current authority outside trade resolution surfaces";
            return false;
        }

        guardian = authorityGuardian.DeepClone().AsObject();
        guardian.Remove("tradeInventory");
        guardian.Remove(GuardianTradeRequestState.ReceiptsProperty);
        if (materializedGuardian["tradeInventory"] != null)
            guardian["tradeInventory"] = materializedGuardian["tradeInventory"]?.DeepClone();
        if (materializedGuardian[GuardianTradeRequestState.ReceiptsProperty] != null)
            guardian[GuardianTradeRequestState.ReceiptsProperty] = materializedGuardian[GuardianTradeRequestState.ReceiptsProperty]?.DeepClone();

        return true;
    }

    private static bool MaterializedGuardianMatchesAuthorityOutsideTradeResolutionSurface(
        JsonObject authorityGuardian,
        JsonObject materializedGuardian)
    {
        var authorityComparable = authorityGuardian.DeepClone().AsObject();
        var materializedComparable = materializedGuardian.DeepClone().AsObject();
        authorityComparable.Remove("tradeInventory");
        authorityComparable.Remove(GuardianTradeRequestState.ReceiptsProperty);
        materializedComparable.Remove("tradeInventory");
        materializedComparable.Remove(GuardianTradeRequestState.ReceiptsProperty);
        return JsonNode.DeepEquals(authorityComparable, materializedComparable);
    }

    private async Task ValidatePendingNpcTradeInventoryRequestContextAsync(List<ValidationIssue> issues)
    {
        var requests = await NpcTradeRequestState.ReadRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        if (IsChaosSeaRealm(await TryResolvePreTurnRealmAsync()))
        {
            issues.Add(new ValidationIssue(
                NpcTradeRequestState.PendingRequestPath,
                IssueSeverity.Error,
                "pending_npc_trade_inventory_requests.json допустим только в mortal realm",
                code: "npc_trade_request_wrong_realm",
                section: "NpcTrade"));
        }

        var npcRoot = await ReadJsonObjectAsync("game_state/npcs/npc_core.json");
        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.NpcId) ||
                string.IsNullOrWhiteSpace(request.NpcName) ||
                string.IsNullOrWhiteSpace(request.MerchantProfile) ||
                string.IsNullOrWhiteSpace(request.TradeCycleId) ||
                request.DerivedTradeSlotCount <= 0 ||
                request.RefreshAfterWorldDate <= request.CreatedAtWorldDate)
            {
                issues.Add(new ValidationIssue(
                    NpcTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_npc_trade_inventory_requests.json должен содержать полный client-authored contract",
                    code: "npc_trade_request_missing_fields",
                    section: "NpcTrade"));
                continue;
            }

            if (npcRoot != null &&
                FindNpcTradeValidationEntry(npcRoot, request.NpcId) == null)
            {
                issues.Add(new ValidationIssue(
                    NpcTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending NPC trade request должен ссылаться на существующего NPC",
                    code: "npc_trade_request_unknown_npc",
                    section: "NpcTrade",
                    repairHint: "Создавай NPC trade request только для существующего merchant NPC из canonical npc_core state."));
            }
        }
    }

    private async Task ValidatePendingNpcTradeInventoryRequestResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadPreTurnTrackedFileAsync(NpcTradeRequestState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = NpcTradeRequestState.ParseRequests(preTurnJson);
        if (requests.Count == 0)
            return;

        var npcRoot = await ReadJsonObjectAsync("game_state/npcs/npc_core.json");
        if (npcRoot == null)
            return;

        foreach (var request in requests)
        {
            var npc = FindNpcTradeValidationEntry(npcRoot, request.NpcId);
            var tradeInventory = npc?["tradeInventory"] as JsonObject;
            if (!NpcTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request))
            {
                issues.Add(new ValidationIssue(
                    NpcTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_npc_trade_inventory_requests из pre-turn snapshot не привёл к matching npc.tradeInventory",
                    code: "npc_trade_request_missing_inventory_resolution",
                    section: "NpcTrade",
                    repairHint: "На accepted turn обязательно materialize-ь explicit npc.tradeInventory по client-authored request contract; не игнорируй request и не закрывай его частично совпадающей витриной."));
                continue;
            }

            if (!NpcTradeRequestState.ReceiptMatchesRequestContract(
                    npc == null ? null : NpcTradeRequestState.FindMatchingReceipt(npc, request),
                    request,
                    tradeInventory))
            {
                issues.Add(new ValidationIssue(
                    NpcTradeRequestState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_npc_trade_inventory_requests из pre-turn snapshot не был закрыт canonical tradeInventory receipt",
                    code: "npc_trade_request_missing_receipt_resolution",
                    section: "NpcTrade",
                    repairHint: $"После materialize explicit npc.tradeInventory обязательно закрой запрос через {NpcTradeRequestState.UpdateReceiptsProperty} и запиши matching requestId/npcId/tradeCycleId/itemCount timing в npc.{NpcTradeRequestState.ReceiptsProperty}."));
            }
        }
    }

    private async Task ValidatePendingGuardianAbodeResidentInteractionRequestContextAsync(List<ValidationIssue> issues)
    {
        var requests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            issues,
            code: "abode_resident_interactions_invalid_validated_snapshot_context",
            section: "AfterlifeResidents");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_resident_interactions.json допустим только в afterlife realm",
                code: "abode_resident_interactions_wrong_realm",
                section: "AfterlifeResidents"));
        }

        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            var receipts = GuardianAbodeResidentState.EnsureInteractionReceiptsArray(residentsRoot);
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.ResidentId) ||
                    string.IsNullOrWhiteSpace(request.GuardianId) ||
                    string.IsNullOrWhiteSpace(request.AbodeId) ||
                    !GuardianAbodeResidentState.IsSupportedInteractionType(request.InteractionType))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                        IssueSeverity.Error,
                        "pending_guardian_abode_resident_interactions.json должен содержать полный client-authored contract",
                        code: "abode_resident_interactions_missing_fields",
                        section: "AfterlifeResidents"));
                    continue;
                }

                if (GuardianAbodeResidentState.FindResident(residentsRoot, request.ResidentId) == null &&
                    GuardianAbodeResidentState.FindInteractionReceipt(receipts, request.RequestId) == null)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                        IssueSeverity.Error,
                        "pending resident interaction request должен ссылаться либо на существующего resident, либо на уже записанный interaction receipt",
                        code: "abode_resident_interaction_missing_resident_or_receipt",
                        section: "AfterlifeResidents",
                        repairHint: "Не держи pending resident interaction request без resident roster и без matching receipt." ));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingGuardianAbodeResidentsRequestContextAsync(List<ValidationIssue> issues)
    {
        var requests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            issues,
            code: "abode_resident_roster_invalid_validated_snapshot_context",
            section: "AfterlifeResidents");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_residents_request.json допустим только в afterlife realm",
                code: "abode_resident_roster_wrong_realm",
                section: "AfterlifeResidents"));
        }

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.GuardianId) ||
                string.IsNullOrWhiteSpace(request.AbodeId))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_abode_residents_request.json должен содержать полный client-authored roster contract",
                    code: "abode_resident_roster_missing_fields",
                    section: "AfterlifeResidents"));
            }
        }
    }

    private async Task ValidatePendingGuardianAbodeResidentInteractionResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            issues,
            code: "abode_resident_interaction_missing_validated_snapshot_request",
            section: "AfterlifeResidents",
            message: "pending_guardian_abode_resident_interactions.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Resident interaction contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_guardian_abode_resident_interactions.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ReadPendingGuardianAbodeResidentInteractionRequests(preTurnJson);
        if (requests.Count == 0)
            return;

        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            var currentResidentsRoot = JsonNode.Parse(residentsJson) as JsonObject;
            if (currentResidentsRoot == null)
                return;

            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            var receipts = GuardianAbodeResidentState.EnsureInteractionReceiptsArray(currentResidentsRoot);
            var historyLog = GuardianAbodeResidentState.EnsureHistoryLogArray(currentResidentsRoot);
            var preTurnResidentsJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeResidentState.StatePath);
            JsonObject? preTurnResidentsRoot = null;
            if (!string.IsNullOrWhiteSpace(preTurnResidentsJson))
            {
                preTurnResidentsRoot = JsonNode.Parse(preTurnResidentsJson) as JsonObject;
                if (preTurnResidentsRoot != null)
                    GuardianAbodeResidentState.NormalizeShape(preTurnResidentsRoot);
            }

            foreach (var request in requests)
            {
                var receipt = GuardianAbodeResidentState.FindInteractionReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                        IssueSeverity.Error,
                        "pending resident interaction request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "abode_resident_interaction_missing_resolution",
                        section: "AfterlifeResidents",
                        repairHint: "Каждый resident talk/history request должен закрываться в ближайшем accepted turn через guardian_abode_residents.json.interactionReceipts[]."));
                    continue;
                }

                var status = GetNodeString(receipt["status"]);
                if (!string.Equals(status, GuardianAbodeResidentState.InteractionStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isHistoryRequest = string.Equals(request.InteractionType, GuardianAbodeResidentState.InteractionTypeHistory, StringComparison.OrdinalIgnoreCase);
                if (isHistoryRequest &&
                    !ResidentHistoryRequestHasCanonicalResult(preTurnResidentsRoot, currentResidentsRoot, historyLog, request, receipt))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                        IssueSeverity.Error,
                        "Accepted resident history request не привёл к canonical history result",
                        code: "abode_resident_history_missing_canonical_result",
                        section: "AfterlifeResidents",
                        repairHint: "Для accepted history request либо установи historyRevealed=true, либо добавь historyLog entry, либо реально обнови mortalWorldImprint резидента."));
                }

                if (!ResidentHasNewThoughtOrInteractionMemory(preTurnResidentsRoot, currentResidentsRoot, request.ResidentId))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                        IssueSeverity.Error,
                        isHistoryRequest
                            ? "Accepted resident history request не оставил curated memory update"
                            : "Accepted resident talk request не оставил curated memory update",
                        code: isHistoryRequest
                            ? "abode_resident_history_missing_memory_update"
                            : "abode_resident_talk_missing_memory_update",
                        section: "AfterlifeResidents",
                        repairHint: isHistoryRequest
                            ? "После accepted history request добавь residentThoughtJournalUpdates и/или residentInteractionLogUpdates, чтобы у ГМа осталась краткая память о результате сцены."
                            : "После accepted talk request добавь residentThoughtJournalUpdates и/или residentInteractionLogUpdates, чтобы у ГМа осталась краткая память о результате сцены."));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingGuardianAbodeResidentsResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            issues,
            code: "abode_resident_roster_missing_validated_snapshot_request",
            section: "AfterlifeResidents",
            message: "pending_guardian_abode_residents_request.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Resident roster contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_guardian_abode_residents_request.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ReadPendingGuardianAbodeResidentsRequests(preTurnJson);
        if (requests.Count == 0)
            return;

        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            var rosterReceipts = GuardianAbodeResidentState.EnsureRosterReceiptsArray(residentsRoot);
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.GuardianId) ||
                    string.IsNullOrWhiteSpace(request.AbodeId))
                {
                    continue;
                }

                var matchingReceipt = GuardianAbodeResidentState.FindRosterReceipt(rosterReceipts, request.RequestId);
                if (matchingReceipt == null)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                        IssueSeverity.Error,
                        "pending abode residents request из pre-turn snapshot не привёл к canonical roster receipt",
                        code: "abode_resident_roster_missing_receipt_resolution",
                        section: "AfterlifeResidents",
                        repairHint: "На accepted turn закрывай pending roster request через guardian_abode_residents.json.rosterReceipts[] и сохраняй matching requestId."));
                    continue;
                }

                var presentCount = CountPresentResidentsForAbode(residentsRoot, request.GuardianId, request.AbodeId);
                var rosterCount = GetNodeInt(matchingReceipt["rosterCount"]);
                if (presentCount <= 0 && rosterCount <= 0)
                    continue;

                if (presentCount <= 0 && rosterCount > 0)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentState.StatePath,
                        IssueSeverity.Error,
                        "Resident roster receipt сообщает о materialized roster, но текущий resident roster пуст",
                        code: "abode_resident_roster_receipt_without_entries",
                        section: "AfterlifeResidents",
                        repairHint: "Если roster receipt указывает на materialized residents, entries[] должны содержать резидентов для соответствующей Обители."));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingGuardianSocialInteractionRequestContextAsync(List<ValidationIssue> issues)
    {
        var requests = await ActorSocialInteractionRequestState.ReadGuardianRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            issues,
            code: "guardian_social_interaction_invalid_validated_snapshot_context",
            section: "GuardianSocial");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                IssueSeverity.Error,
                "pending_guardian_social_interactions.json допустим только в afterlife realm",
                code: "guardian_social_interactions_wrong_realm",
                section: "GuardianSocial"));
        }

        var guardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
        var journalRoot = await ReadJsonObjectAsync(GuardianSocialJournalState.StatePath);
        if (journalRoot != null)
            ActorJournalState.NormalizeShape(journalRoot, GuardianSocialJournalState.ActorIdProperty, GuardianSocialJournalState.UpdateProperty);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.GuardianId) ||
                !ActorSocialInteractionRequestState.IsSupportedGuardianInteractionType(request.InteractionType))
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                    IssueSeverity.Error,
                    "pending_guardian_social_interactions.json должен содержать полный client-authored contract",
                    code: "guardian_social_interactions_missing_fields",
                    section: "GuardianSocial"));
                continue;
            }

            if (!GuardianExistsInState(guardiansRoot, request.GuardianId) &&
                ActorSocialInteractionRequestState.FindGuardianResolutionEntry(journalRoot, request.GuardianId, request.RequestId) == null)
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                    IssueSeverity.Error,
                    "pending guardian social request должен ссылаться либо на существующего guardian, либо на уже записанный social journal closure",
                    code: "guardian_social_interaction_missing_guardian_or_receipt",
                    section: "GuardianSocial",
                    repairHint: "Не держи pending guardian social request без существующего Хранителя и без matching guardian social journal entry."));
            }
        }
    }

    private async Task ValidatePendingNpcSocialInteractionRequestContextAsync(List<ValidationIssue> issues)
    {
        var requests = await ActorSocialInteractionRequestState.ReadNpcRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        if (IsChaosSeaRealm(await TryResolvePreTurnRealmAsync()))
        {
            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingNpcRequestPath,
                IssueSeverity.Error,
                "pending_npc_social_interactions.json допустим только в mortal realm",
                code: "npc_social_interactions_wrong_realm",
                section: "NpcSocial"));
        }

        var npcRoot = await ReadJsonObjectAsync("game_state/npcs/npc_core.json");
        var journalRoot = await ReadJsonObjectAsync(NpcInteractionJournalState.StatePath);
        if (journalRoot != null)
            ActorJournalState.NormalizeShape(journalRoot, NpcInteractionJournalState.ActorIdProperty, NpcInteractionJournalState.UpdateProperty);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.NpcId) ||
                !ActorSocialInteractionRequestState.IsSupportedNpcInteractionType(request.InteractionType))
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingNpcRequestPath,
                    IssueSeverity.Error,
                    "pending_npc_social_interactions.json должен содержать полный client-authored contract",
                    code: "npc_social_interactions_missing_fields",
                    section: "NpcSocial"));
                continue;
            }

            if (!NpcExistsInState(npcRoot, request.NpcId) &&
                ActorSocialInteractionRequestState.FindNpcResolutionEntry(journalRoot, request.NpcId, request.RequestId) == null)
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingNpcRequestPath,
                    IssueSeverity.Error,
                    "pending NPC social request должен ссылаться либо на существующего NPC, либо на уже записанный npc interaction closure",
                    code: "npc_social_interaction_missing_npc_or_receipt",
                    section: "NpcSocial",
                    repairHint: "Не держи pending NPC social request без существующего NPC и без matching npc interaction journal entry."));
            }
        }
    }

    private async Task ValidatePendingGuardianSocialInteractionResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            issues,
            code: "guardian_social_interaction_missing_validated_snapshot_request",
            section: "GuardianSocial",
            message: "pending_guardian_social_interactions.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Guardian social contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_guardian_social_interactions.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ReadPendingGuardianSocialInteractionRequests(preTurnJson);
        if (requests.Count == 0)
            return;

        var journalRoot = await ReadJsonObjectAsync(GuardianSocialJournalState.StatePath);
        if (journalRoot == null)
        {
            foreach (var request in requests)
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                    IssueSeverity.Error,
                    "pending guardian social request из pre-turn snapshot не был закрыт в текущем accepted turn",
                    code: "guardian_social_interaction_missing_resolution",
                    section: "GuardianSocial",
                    repairHint: "Каждый guardian social request должен закрываться guardianSocialJournalUpdates entry с matching requestId, guardianId, interactionType и status."));
            }

            return;
        }

        ActorJournalState.NormalizeShape(journalRoot, GuardianSocialJournalState.ActorIdProperty, GuardianSocialJournalState.UpdateProperty);
        foreach (var request in requests)
        {
            var resolution = ActorSocialInteractionRequestState.FindGuardianResolutionEntry(journalRoot, request.GuardianId, request.RequestId);
            if (resolution != null)
                continue;

            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                IssueSeverity.Error,
                "pending guardian social request из pre-turn snapshot не был закрыт в текущем accepted turn",
                code: "guardian_social_interaction_missing_resolution",
                section: "GuardianSocial",
                repairHint: "Каждый guardian social request должен закрываться guardianSocialJournalUpdates entry с matching requestId, guardianId, interactionType и status."));
        }
    }

    private async Task ValidatePendingNpcSocialInteractionResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadPreTurnTrackedFileAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ReadPendingNpcSocialInteractionRequests(preTurnJson);
        if (requests.Count == 0)
            return;

        var journalRoot = await ReadJsonObjectAsync(NpcInteractionJournalState.StatePath);
        if (journalRoot == null)
        {
            foreach (var request in requests)
            {
                issues.Add(new ValidationIssue(
                    ActorSocialInteractionRequestState.PendingNpcRequestPath,
                    IssueSeverity.Error,
                    "pending NPC social request из pre-turn snapshot не был закрыт в текущем accepted turn",
                    code: "npc_social_interaction_missing_resolution",
                    section: "NpcSocial",
                    repairHint: "Каждый NPC social request должен закрываться npcInteractionJournalUpdates entry с matching requestId, npcId, interactionType и status."));
            }

            return;
        }

        ActorJournalState.NormalizeShape(journalRoot, NpcInteractionJournalState.ActorIdProperty, NpcInteractionJournalState.UpdateProperty);
        foreach (var request in requests)
        {
            var resolution = ActorSocialInteractionRequestState.FindNpcResolutionEntry(journalRoot, request.NpcId, request.RequestId);
            if (resolution != null)
                continue;

            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingNpcRequestPath,
                IssueSeverity.Error,
                "pending NPC social request из pre-turn snapshot не был закрыт в текущем accepted turn",
                code: "npc_social_interaction_missing_resolution",
                section: "NpcSocial",
                repairHint: "Каждый NPC social request должен закрываться npcInteractionJournalUpdates entry с matching requestId, npcId, interactionType и status."));
        }
    }

    private static JsonObject? FindNpcTradeValidationEntry(JsonObject root, string npcId)
    {
        foreach (var propertyName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (root[propertyName] is not JsonArray entries)
                continue;

            foreach (var npc in entries.OfType<JsonObject>())
            {
                var currentNpcId = GetNodeString(npc["npcId"]) ?? GetNodeString(npc["NPCId"]);
                if (string.Equals(currentNpcId, npcId, StringComparison.OrdinalIgnoreCase))
                    return npc;
            }
        }

        return null;
    }

    private async Task ValidateResidentMechanicalOutcomeMemoryAsync(List<ValidationIssue> issues)
    {
        var currentResidentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(currentResidentsJson))
            return;

        JsonObject? currentResidentsRoot;
        try
        {
            currentResidentsRoot = JsonNode.Parse(currentResidentsJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (currentResidentsRoot == null)
            return;

        GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);

        JsonObject? preTurnResidentsRoot = null;
        var preTurnResidentsJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeResidentState.StatePath);
        if (!string.IsNullOrWhiteSpace(preTurnResidentsJson))
        {
            try
            {
                preTurnResidentsRoot = JsonNode.Parse(preTurnResidentsJson) as JsonObject;
                if (preTurnResidentsRoot != null)
                    GuardianAbodeResidentState.NormalizeShape(preTurnResidentsRoot);
            }
            catch
            {
                // parse issues reported elsewhere
            }
        }

        var preTurnSoulQuestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/quests/soul_quests.json");
        var currentSoulQuestJson = await _fs.ReadFileAsync("game_state/quests/soul_quests.json");
        var preTurnQuestFingerprintsByResident = CollectResidentSoulQuestFingerprints(preTurnSoulQuestJson);
        var currentQuestFingerprintsByResident = CollectResidentSoulQuestFingerprints(currentSoulQuestJson);

        foreach (var resident in GuardianAbodeResidentState.EnsureEntriesArray(currentResidentsRoot).OfType<JsonObject>())
        {
            var residentId = GetNodeString(resident["residentId"]);
            if (string.IsNullOrWhiteSpace(residentId))
                continue;

            var preTurnResident = preTurnResidentsRoot == null ? null : GuardianAbodeResidentState.FindResident(preTurnResidentsRoot, residentId);
            var currentLinkedSoulQuestId = GetNodeString(resident["linkedSoulQuestId"]);
            var previousLinkedSoulQuestId = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["linkedSoulQuestId"]);
            var currentRewardState = GetNodeString(resident["bondRewardState"]);
            var previousRewardState = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["bondRewardState"]);
            var currentGrantedRelicId = GetNodeString(resident["grantedRelicId"]);
            var previousGrantedRelicId = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["grantedRelicId"]);

            var changedResidentQuest = !string.IsNullOrWhiteSpace(currentLinkedSoulQuestId) &&
                                       !string.Equals(currentLinkedSoulQuestId, previousLinkedSoulQuestId, StringComparison.OrdinalIgnoreCase);
            if (!changedResidentQuest)
            {
                var previousQuestFingerprints = preTurnQuestFingerprintsByResident.TryGetValue(residentId, out var prevQuestSet)
                    ? prevQuestSet
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (currentQuestFingerprintsByResident.TryGetValue(residentId, out var currentQuestFingerprints))
                {
                    changedResidentQuest = currentQuestFingerprints.Any(pair =>
                        !previousQuestFingerprints.TryGetValue(pair.Key, out var previousFingerprint) ||
                        !string.Equals(previousFingerprint, pair.Value, StringComparison.Ordinal));
                }
            }

            if (changedResidentQuest && !ResidentHasNewInteractionLogMemory(preTurnResidentsRoot, currentResidentsRoot, residentId))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Resident-linked soul quest появился без нового resident interaction log entry",
                    code: "abode_resident_quest_missing_interaction_log_update",
                    section: "AfterlifeResidents",
                    repairHint: "Когда resident получает или продвигает личную просьбу через soul quest, добавь residentInteractionLogUpdates с кратким summary этого шага."));
            }

            var rewardAdvanced =
                (string.Equals(currentRewardState, GuardianAbodeResidentState.RewardStateGranted, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(currentRewardState, GuardianAbodeResidentState.RewardStateConsumed, StringComparison.OrdinalIgnoreCase)) &&
                (!string.Equals(currentRewardState, previousRewardState, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(currentGrantedRelicId, previousGrantedRelicId, StringComparison.OrdinalIgnoreCase));
            if (rewardAdvanced && !ResidentHasNewInteractionLogMemory(preTurnResidentsRoot, currentResidentsRoot, residentId))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Resident reward outcome не оставил нового resident interaction log entry",
                    code: "abode_resident_reward_missing_interaction_log_update",
                    section: "AfterlifeResidents",
                    repairHint: "Когда resident дарует реликвию связи или переводит reward outcome в granted/consumed, добавь residentInteractionLogUpdates с кратким summary и consequence."));
            }
        }
    }

    private async Task ValidatePendingArchiveConsultationRequestContextAsync(List<ValidationIssue> issues)
    {
        var request = await AfterlifeArchiveActionState.ReadConsultationAsync(_fs);
        if (request == null)
            return;

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            issues,
            code: "archive_consultation_request_invalid_validated_snapshot_context",
            section: "AfterlifeArchive");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
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
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            issues,
            code: "archive_consultation_request_missing_validated_snapshot_request",
            section: "AfterlifeArchive",
            message: "pending_archive_consultation_request.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Archive consultation contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_archive_consultation_request.json в manifest.Files и snapshotFileHashes.");
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
                if (!TryResolveGuardianProjectTrackerValidationRoot(
                        AfterlifeArchiveActionState.ConsultationRequestPath,
                        "Accepted archive consultation request требует readable current guardian project tracker authority и не использует isolated pre-turn tracker baseline как authority fallback.",
                        "archive_consultation_request_missing_current_tracker_authority",
                        "AfterlifeArchive",
                        $"Исправь current {GuardianProjectState.TrackerPath} и validated tracker baseline так, чтобы validator построил guardian-backed current tracker authority перед archive consultation resolution.",
                        issues,
                        out var trackerRoot))
                {
                }
                else if (!ArchiveConsultationReceiptHasMatchingCompletedProject(trackerRoot, request.RequestId, request.ArchiveId, request.GuardianId, receipt))
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

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            issues,
            code: "archive_project_fuel_request_invalid_validated_snapshot_context",
            section: "AfterlifeArchive");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
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
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            issues,
            code: "archive_project_fuel_request_missing_validated_snapshot_request",
            section: "AfterlifeArchive",
            message: "pending_archive_project_fuel_request.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Archive project fuel contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_archive_project_fuel_request.json в manifest.Files и snapshotFileHashes.");
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

        var preTurnRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            "game_state/control/incarnation_trigger.json",
            issues,
            code: "incarnation_trigger_invalid_validated_snapshot_context",
            section: "Lifecycle");
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
        var manifest = await LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(
            "game_state/control/incarnation_trigger.json",
            issues,
            code: "forced_incarnation_invalid_validated_snapshot_context",
            section: "Lifecycle",
            message: "Guardian-forced TriggerIncarnation требует current validated pending turn snapshot ordinary afterlife context.",
            repairHint: "Используй current validated pending turn snapshot с корректными sessionId/requestId/turnNumber, ordinary afterlife sourceLabel и сохранённым playerAction текущего хода.");
        if (manifest != null &&
            !string.Equals(manifest.SourceLabel, "обработки хода", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation допустим только на обычном player-driven afterlife turn.",
                code: "forced_incarnation_invalid_source_turn",
                section: "Lifecycle",
                expected: "ordinary player-driven Chaos Sea turn",
                actual: manifest.SourceLabel ?? "missing sourceLabel",
                repairHint: "Не навязывай принудительное воплощение на lifecycle/system turns. Сначала верни душу в обитель, дай ей хотя бы один обычный afterlife turn, и только затем реагируй на провокацию."));
        }

        var playerAction = manifest?.PlayerAction ?? string.Empty;
        if (manifest != null && !HasGuardianProvocationEvidence(playerAction, payload))
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
            var guardSemanticState = AfterlifeReturnGuardService.Classify(guardJson, out var returnGuard);
            if (guardSemanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid)
            {
                issues.Add(new ValidationIssue(
                    AfterlifeReturnGuardService.GuardPath,
                    IssueSeverity.Error,
                    "Невалидный afterlife_return_guard.json не может отключить защиту первого afterlife-turn; guardian-forced incarnation блокируется fail-closed.",
                    code: "forced_incarnation_blocked_by_invalid_safe_return_guard",
                    section: "Lifecycle",
                    expected: "valid afterlife_return_guard.json or no forced incarnation",
                    actual: "malformed or semantically invalid guard file",
                    repairHint: "На этом ходе убери guardian_forced incarnation. Клиентский afterlife_return_guard.json должен быть валидным по semantic contract (`reason=post_life_return`) или очищенным самой runtime-нормализацией."));
            }
            else if (guardSemanticState == AfterlifeReturnGuardSemanticState.ActiveValid && returnGuard != null)
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

        var guardianContext = await TryReadCanonicalForcedGuardianIncarnationContextAsync();
        if (guardianContext == null)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/guardians.json",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation требует activeGuardian selector и matching canonical guardian context из guardians[].",
                code: "forced_incarnation_missing_active_guardian_context",
                section: "Lifecycle",
                expected: "activeGuardian.guardianId matching guardians[] entry + current abode context",
                actual: "missing or invalid canonical guardian context",
                repairHint: "Сначала синхронизируй activeGuardian с guardians[] по guardianId и materialize canonical guardian abode/reputation state вместе с chaosSeaNavigation.currentAbodeId."));
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

    private async Task<ForcedGuardianIncarnationContext?> TryReadCanonicalForcedGuardianIncarnationContextAsync()
    {
        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!guardianPolicyContext.CurrentStateReadable ||
            !TryGetCurrentAuthorityActiveGuardian(guardianPolicyContext, out var activeGuardian))
            return null;

        try
        {
            var guardianId = GetFirstNonEmptyString(activeGuardian, "guardianId", "id") ?? "";
            if (string.IsNullOrWhiteSpace(guardianId))
                return null;

            if (!TryGetCurrentGuardian(guardianPolicyContext, guardianId, out var canonicalGuardian))
                return null;

            var expectedAbodeId = TryReadGuardianAbodeId(canonicalGuardian) ?? "";

            var currentAbodeId = "";
            if (guardianPolicyContext.HasCurrentRoot &&
                guardianPolicyContext.CurrentRoot.TryGetProperty("chaosSeaNavigation", out var navigation) &&
                navigation.ValueKind == JsonValueKind.Object)
            {
                currentAbodeId = GetFirstNonEmptyString(navigation, "currentAbodeId") ?? "";
            }

            var currentReputation = TryReadGuardianCurrentReputation(canonicalGuardian) ?? 0;

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

    private static IReadOnlyList<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest> ReadPendingGuardianAbodeResidentInteractionRequests(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(GuardianAbodeResidentRequestState.InteractionRequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest>();
            }

            var result = new List<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest>();
            foreach (var item in requestsNode.EnumerateArray())
            {
                try
                {
                    var request = JsonSerializer.Deserialize<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest>(item.GetRawText());
                    if (request != null)
                        result.Add(request);
                }
                catch
                {
                    // ignore malformed item; shape issues are reported elsewhere
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest>();
        }
    }

    private static IReadOnlyList<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest> ReadPendingGuardianAbodeResidentsRequests(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(GuardianAbodeResidentRequestState.ResidentsRequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest>();
            }

            var result = new List<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest>();
            foreach (var item in requestsNode.EnumerateArray())
            {
                try
                {
                    var request = JsonSerializer.Deserialize<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest>(item.GetRawText());
                    if (request != null)
                        result.Add(request);
                }
                catch
                {
                    // ignore malformed item; shape issues are reported elsewhere
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest>();
        }
    }

    private static int CountPresentResidentsForAbode(JsonObject residentsRoot, string guardianId, string abodeId)
    {
        GuardianAbodeResidentState.NormalizeShape(residentsRoot);
        if (residentsRoot[GuardianAbodeResidentState.EntriesProperty] is not JsonArray entries)
            return 0;

        return entries.OfType<JsonObject>().Count(entry =>
            string.Equals(GetNodeString(entry["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(entry["abodeId"]), abodeId, StringComparison.OrdinalIgnoreCase) &&
            !(entry["isPresent"] is JsonValue isPresentValue &&
              isPresentValue.TryGetValue<bool>(out var isPresent) &&
              !isPresent));
    }

    private void ValidatePendingGuardianSocialInteractionsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(ActorSocialInteractionRequestState.RequestsProperty, out var requests) ||
            requests.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_guardian_social_interactions.json должен содержать requests array",
                code: "pending_guardian_social_interactions_missing_requests",
                section: "GuardianSocial"));
            return;
        }

        var index = 0;
        foreach (var request in requests.EnumerateArray())
        {
            var requestContext = $"{contextPrefix}.{ActorSocialInteractionRequestState.RequestsProperty}[{index++}]";
            if (!RequireObject(request, requestContext, issues))
                continue;

            ValidatePendingGuardianSocialInteractionRequestObject(request, requestContext, issues);
        }
    }

    private void ValidatePendingNpcTradeInventoryRequestsFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(NpcTradeRequestState.RequestsProperty, out var requests) ||
            requests.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_npc_trade_inventory_requests.json должен содержать requests array",
                code: "pending_npc_trade_inventory_requests_missing_requests",
                section: "NpcTrade"));
            return;
        }

        var index = 0;
        foreach (var request in requests.EnumerateArray())
        {
            var requestContext = $"{contextPrefix}.{NpcTradeRequestState.RequestsProperty}[{index++}]";
            if (!RequireObject(request, requestContext, issues))
                continue;

            ValidatePendingNpcTradeInventoryRequestObject(request, requestContext, issues);
        }
    }

    private void ValidatePendingNpcSocialInteractionsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(ActorSocialInteractionRequestState.RequestsProperty, out var requests) ||
            requests.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_npc_social_interactions.json должен содержать requests array",
                code: "pending_npc_social_interactions_missing_requests",
                section: "NpcSocial"));
            return;
        }

        var index = 0;
        foreach (var request in requests.EnumerateArray())
        {
            var requestContext = $"{contextPrefix}.{ActorSocialInteractionRequestState.RequestsProperty}[{index++}]";
            if (!RequireObject(request, requestContext, issues))
                continue;

            ValidatePendingNpcSocialInteractionRequestObject(request, requestContext, issues);
        }
    }

    private void ValidatePendingNpcTradeInventoryRequestObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "requestId");
        RequireString(root, contextPrefix, issues, "npcId");
        ValidateOptionalString(root, contextPrefix, issues, "npcName");
        var merchantProfile = RequireString(root, contextPrefix, issues, "merchantProfile");
        RequireString(root, contextPrefix, issues, "tradeCycleId");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "derivedTradeSlotCount");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "NpcTrade");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtWorldDate", "NpcTrade");
        ValidatePositiveIntegerField(root, contextPrefix, issues, "refreshAfterWorldDate");
        ValidateRequiredIsoTimestampField(
            root,
            contextPrefix,
            issues,
            "createdAtUtc",
            "NpcTrade",
            "pending_npc_trade_inventory_missing_created_at_utc",
            "pending_npc_trade_inventory_invalid_created_at_utc",
            "pending_npc_trade_inventory_requests.json должен содержать createdAtUtc в ISO 8601 формате.");

        if (!string.IsNullOrWhiteSpace(merchantProfile) && !NpcTradeService.IsValidMerchantProfileCode(merchantProfile))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.merchantProfile",
                IssueSeverity.Error,
                "pending NPC trade request merchantProfile должен быть допустимым merchant profile",
                code: "pending_npc_trade_inventory_invalid_merchant_profile",
                section: "NpcTrade",
                actual: merchantProfile));
        }
    }

    private void ValidatePendingGuardianSocialInteractionRequestObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "requestId");
        RequireString(root, contextPrefix, issues, "guardianId");
        ValidateOptionalString(root, contextPrefix, issues, "guardianName");
        var interactionType = RequireString(root, contextPrefix, issues, "interactionType");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "GuardianSocial");
        ValidateRequiredIsoTimestampField(
            root,
            contextPrefix,
            issues,
            "createdAtUtc",
            "GuardianSocial",
            "pending_guardian_social_interaction_missing_created_at_utc",
            "pending_guardian_social_interaction_invalid_created_at_utc",
            "pending_guardian_social_interactions.json должен содержать createdAtUtc в ISO 8601 формате.");

        if (!string.IsNullOrWhiteSpace(interactionType) &&
            !ActorSocialInteractionRequestState.IsSupportedGuardianInteractionType(interactionType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.interactionType",
                IssueSeverity.Error,
                "guardian social request должен использовать canonical interactionType",
                code: "pending_guardian_social_interaction_invalid_type",
                section: "GuardianSocial",
                expected: "talk | lore",
                actual: interactionType,
                repairHint: "Для pending guardian social request используй interactionType = talk или lore."));
        }
    }

    private void ValidatePendingNpcSocialInteractionRequestObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "requestId");
        RequireString(root, contextPrefix, issues, "npcId");
        ValidateOptionalString(root, contextPrefix, issues, "npcName");
        var interactionType = RequireString(root, contextPrefix, issues, "interactionType");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "NpcSocial");
        ValidateRequiredIsoTimestampField(
            root,
            contextPrefix,
            issues,
            "createdAtUtc",
            "NpcSocial",
            "pending_npc_social_interaction_missing_created_at_utc",
            "pending_npc_social_interaction_invalid_created_at_utc",
            "pending_npc_social_interactions.json должен содержать createdAtUtc в ISO 8601 формате.");

        if (!string.IsNullOrWhiteSpace(interactionType) &&
            !ActorSocialInteractionRequestState.IsSupportedNpcInteractionType(interactionType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.interactionType",
                IssueSeverity.Error,
                "NPC social request должен использовать canonical interactionType",
                code: "pending_npc_social_interaction_invalid_type",
                section: "NpcSocial",
                expected: "talk",
                actual: interactionType,
                repairHint: "Для pending NPC social request пока используй только interactionType = talk."));
        }
    }

    private static IReadOnlyList<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest> ReadPendingGuardianSocialInteractionRequests(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(ActorSocialInteractionRequestState.RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest>();
            }

            var result = new List<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest>();
            foreach (var item in requestsNode.EnumerateArray())
            {
                try
                {
                    var request = JsonSerializer.Deserialize<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest>(item.GetRawText());
                    if (request != null)
                        result.Add(request);
                }
                catch
                {
                    // ignore malformed item; shape issues are reported elsewhere
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest>();
        }
    }

    private static IReadOnlyList<ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest> ReadPendingNpcSocialInteractionRequests(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(ActorSocialInteractionRequestState.RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest>();
            }

            var result = new List<ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest>();
            foreach (var item in requestsNode.EnumerateArray())
            {
                try
                {
                    var request = JsonSerializer.Deserialize<ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest>(item.GetRawText());
                    if (request != null)
                        result.Add(request);
                }
                catch
                {
                    // ignore malformed item; shape issues are reported elsewhere
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest>();
        }
    }

    private async Task<JsonObject?> ReadJsonObjectAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadGuardianAbodeId(JsonElement guardian)
    {
        return guardian.TryGetProperty("abode", out var abode) && abode.ValueKind == JsonValueKind.Object
            ? GetFirstNonEmptyString(abode, "abodeId")
            : null;
    }

    private static int? TryReadGuardianCurrentReputation(JsonElement guardian)
    {
        if (guardian.TryGetProperty("relationshipData", out var relationshipData) &&
            relationshipData.ValueKind == JsonValueKind.Object &&
            relationshipData.TryGetProperty("currentReputation", out var reputationNode) &&
            reputationNode.ValueKind == JsonValueKind.Number &&
            reputationNode.TryGetInt32(out var relationshipReputation))
        {
            return relationshipReputation;
        }

        if (guardian.TryGetProperty("reputation", out var reputationProp) &&
            reputationProp.ValueKind == JsonValueKind.Number &&
            reputationProp.TryGetInt32(out var directReputation))
        {
            return directReputation;
        }

        return null;
    }

    private static bool GuardianExistsInState(JsonObject? guardiansRoot, string guardianId)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return false;

        return guardiansRoot["guardians"] is JsonArray guardians &&
               guardians.OfType<JsonObject>().Any(guardian =>
                   string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NpcExistsInState(JsonObject? npcRoot, string npcId)
    {
        if (npcRoot == null || string.IsNullOrWhiteSpace(npcId))
            return false;

        foreach (var propertyName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (npcRoot[propertyName] is not JsonArray npcs)
                continue;

            if (npcs.OfType<JsonObject>().Any(npc =>
                    string.Equals(GetNodeString(npc["NPCId"]) ?? GetNodeString(npc["npcId"]) ?? GetNodeString(npc["id"]), npcId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ResidentHistoryRequestHasCanonicalResult(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentResidentsRoot,
        JsonArray historyLog,
        GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest request,
        JsonObject receipt)
    {
        var currentResident = GuardianAbodeResidentState.FindResident(currentResidentsRoot, request.ResidentId);
        if (currentResident == null)
            return false;

        if (currentResident["historyRevealed"] is JsonValue revealedValue &&
            revealedValue.TryGetValue<bool>(out var historyRevealed) &&
            historyRevealed)
        {
            return true;
        }

        var historyEntryId = GetNodeString(receipt["historyEntryId"]);
        if (!string.IsNullOrWhiteSpace(historyEntryId) &&
            historyLog.OfType<JsonObject>().Any(entry =>
                string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var preTurnResident = preTurnResidentsRoot == null ? null : GuardianAbodeResidentState.FindResident(preTurnResidentsRoot, request.ResidentId);
        var currentImprint = currentResident["mortalWorldImprint"]?.ToJsonString();
        var previousImprint = preTurnResident?["mortalWorldImprint"]?.ToJsonString();
        return !string.Equals(previousImprint ?? string.Empty, currentImprint ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool ResidentHasNewThoughtOrInteractionMemory(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentResidentsRoot,
        string residentId)
    {
        return ResidentHasJournalDelta(preTurnResidentsRoot, currentResidentsRoot, residentId, GuardianAbodeResidentState.ThoughtJournalProperty) ||
               ResidentHasJournalDelta(preTurnResidentsRoot, currentResidentsRoot, residentId, GuardianAbodeResidentState.InteractionLogProperty);
    }

    private static bool ResidentHasNewInteractionLogMemory(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentResidentsRoot,
        string residentId)
    {
        return ResidentHasJournalDelta(preTurnResidentsRoot, currentResidentsRoot, residentId, GuardianAbodeResidentState.InteractionLogProperty);
    }

    private static bool ResidentHasJournalDelta(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentResidentsRoot,
        string residentId,
        string journalProperty)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        var currentEntries = CollectResidentJournalFingerprints(currentResidentsRoot, residentId, journalProperty);
        if (currentEntries.Count == 0)
            return false;

        var previousEntries = preTurnResidentsRoot == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : CollectResidentJournalFingerprints(preTurnResidentsRoot, residentId, journalProperty);

        foreach (var pair in currentEntries)
        {
            if (!previousEntries.TryGetValue(pair.Key, out var previousFingerprint) ||
                !string.Equals(previousFingerprint, pair.Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> CollectResidentJournalFingerprints(
        JsonObject root,
        string residentId,
        string journalProperty)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetPropertyValue(journalProperty, out var node) || node is not JsonArray entries)
            return result;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            var entryId = GetNodeString(entry["entryId"]);
            if (string.IsNullOrWhiteSpace(entryId))
                continue;

            result[entryId] = entry.ToJsonString();
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> CollectResidentSoulQuestFingerprints(string? soulQuestJson)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(soulQuestJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(soulQuestJson);
            JsonElement questsArray = default;
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("quests", out var quests) &&
                quests.ValueKind == JsonValueKind.Array)
            {
                questsArray = quests;
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                     doc.RootElement.TryGetProperty("UpdateSoulQuests", out var updates) &&
                     updates.ValueKind == JsonValueKind.Array)
            {
                questsArray = updates;
            }

            if (questsArray.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var quest in questsArray.EnumerateArray())
            {
                if (quest.ValueKind != JsonValueKind.Object)
                    continue;

                var residentId = GetFirstNonEmptyString(quest, "relatedAfterlifeResidentId");
                var questId = GetFirstNonEmptyString(quest, "questId");
                if (string.IsNullOrWhiteSpace(residentId) || string.IsNullOrWhiteSpace(questId))
                    continue;

                if (!result.TryGetValue(residentId, out var questsById))
                {
                    questsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[residentId] = questsById;
                }

                questsById[questId] = quest.GetRawText();
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }

        return result;
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

