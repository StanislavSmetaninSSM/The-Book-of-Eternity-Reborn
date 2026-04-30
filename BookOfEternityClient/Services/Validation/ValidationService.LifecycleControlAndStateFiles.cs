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
                "UpdateNPCs", "NPCsRenameData", "NPCsInScene", "UpdateNpcTradeInventoryReceipts"
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
                "sessionId", "requestId", "turnNumber",
                "worldCyclesProcessed", "factionCyclesProcessed",
                "chaosSeaCyclesProcessed", "guardianProjectCyclesProcessed",
                "residentAgencyCyclesProcessed", "shiningAbodeCyclesProcessed",
                "shiningFactionCyclesProcessed", "shiningTradeCyclesProcessed",
                "newLastWorldSimulationTimeInMinutes", "newLastFactionSimulationTimeInMinutes",
                "newLastChaosSeaSimulationOrdinal", "newLastGuardianProjectCycleOrdinal",
                "newLastResidentAgencyCycleOrdinal", "newLastShiningAbodeCycleOrdinal",
                "newLastShiningFactionCycleOrdinal", "newLastShiningTradeCycleOrdinal",
                "afterlifeCatchupProcessed", "afterlifeCatchupSummaryEventsProcessed"
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
                "soulImprint", "pendingMemoryLegacy", ShiningBlessingEffectState.SoulStateProperty,
                PlayerGuardianFoundationState.SoulStateGuardianIdProperty,
                PlayerGuardianFoundationState.SoulStateFoundationStatusProperty
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile("game_state/meta/guardians.json",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UpdateGuardians", "guardianPowerEvents", "guardians", "activeGuardian", "chaosSeaNavigation", "pendingGuardianCreation",
                PlayerGuardianFoundationState.HistoryProperty
            }, issues, ValidateMetaMiscContract);
        await ValidateFlexibleStateFile(PlayerGuardianFoundationState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "requestId", "mode", "founderSoulName", "previousGuardianId", "previousGuardianName", "sourceShiningAvailability",
                "proposedDisplayName", "mantleSummary", "mantleCreed", "appearanceMotifs", "dominantAspect", "createdAtTurn", "createdAtUtc"
            }, issues, ValidatePendingPlayerGuardianFoundationStateFile);
        await ValidateStrictTopLevelObjectFileAsync(PlayerGuardianFoundationState.PendingRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "requestId", "mode", "founderSoulName", "previousGuardianId", "previousGuardianName", "sourceShiningAvailability",
                "proposedDisplayName", "mantleSummary", "mantleCreed", "appearanceMotifs", "dominantAspect", "createdAtTurn", "createdAtUtc"
            }, issues);
        await ValidateFlexibleStateFile(ShiningAbodeState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "availability", "radiance", "lightSparks", "halls", "factions", "shiningPoliticalActors",
                "pendingNativeFactionDiscovery", "gates", "preparedIncarnationPackage", "gachaSystem",
                "coreActionReceipts",
                "factionFoundingReceipts", "factionRealignmentReceipts"
            }, issues, ValidateShiningAbodeStateFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningAbodeState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "availability", "radiance", "lightSparks", "halls", "factions", "shiningPoliticalActors",
                "pendingNativeFactionDiscovery", "gates", "preparedIncarnationPackage", "gachaSystem",
                "coreActionReceipts",
                "factionFoundingReceipts", "factionRealignmentReceipts"
            }, issues);
        await ValidateFlexibleStateFile(ShiningCoreActionRequestState.PendingActionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningCoreActionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningCoreActionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningCoreActionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ShiningTradeRequestState.PendingRequestsPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningTradeRequestState.RequestsProperty
            }, issues, ValidatePendingShiningTradeInventoryRequestsFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningTradeRequestState.PendingRequestsPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningTradeRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(GuardianAbodeResidentState.StatePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentState.UpdateProperty,
                GuardianAbodeResidentState.EntriesProperty,
                GuardianAbodeResidentState.UpdateRosterReceiptsProperty,
                GuardianAbodeResidentState.RosterReceiptsProperty,
                GuardianAbodeResidentState.UpdateInteractionReceiptsProperty,
                GuardianAbodeResidentState.InteractionReceiptsProperty,
                GuardianAbodeResidentState.UpdateTransferReceiptsProperty,
                GuardianAbodeResidentState.TransferReceiptsProperty,
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
                GuardianAbodeResidentState.UpdateTransferReceiptsProperty,
                GuardianAbodeResidentState.TransferReceiptsProperty,
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
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingFoundingsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionFoundingsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingFoundingsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingRealignmentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionRealignmentsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues);
        await ValidateFlexibleStateFile(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
            }, issues, ValidatePendingShiningFactionLeadershipTransitionsRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ShiningFactionRequestState.RequestsProperty
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
        await ValidateFlexibleStateFile(GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.TransferRequestsProperty,
                "requestId", "residentId", "residentName",
                "sourceGuardianId", "sourceGuardianName", "sourceAbodeId", "sourceAbodeName",
                "targetGuardianId", "targetGuardianName", "targetAbodeId", "targetAbodeName",
                "abodeDevotionLevel", "abodeDevotionTier", "restlessness", "migrationState",
                "transferMode", "selectionMode", "competitionScore", "competitionLabel", "competitionReason",
                "createdAtTurn", "createdAtUtc"
            }, issues, ValidatePendingGuardianAbodeResidentTransfersRequestFile);
        await ValidateStrictTopLevelObjectFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                GuardianAbodeResidentRequestState.TransferRequestsProperty
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
        await ValidatePendingResidentCompanionManifestationRealmContextAsync(issues);
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
        await ValidatePendingShiningCoreActionRequestContextAsync(issues);
        await ValidatePendingShiningTradeInventoryRequestContextAsync(issues);
        await ValidatePendingShiningFoundingRequestContextAsync(issues);
        await ValidatePendingShiningRealignmentRequestContextAsync(issues);
        await ValidatePendingShiningLeadershipTransitionRequestContextAsync(issues);
        await ValidateSystemGuardianAttractionContextAsync(issues);
    }

    private static void AddMissingShiningResolutionCurrentFileIssue(
        List<ValidationIssue> issues,
        string relativePath,
        string code,
        string message,
        string repairHint)
    {
        issues.Add(new ValidationIssue(
            relativePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: "ShiningAbode",
            expected: "current authoritative state file required to resolve pre-turn Shining request",
            actual: "missing or empty file",
            repairHint: repairHint));
    }

    private async Task ValidatePendingShiningFoundingResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            issues,
            code: "shining_founding_missing_validated_snapshot_request",
            section: "ShiningAbode",
            message: "pending_shining_faction_foundings.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Shining founding contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_shining_faction_foundings.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ShiningFactionRequestState.ReadFoundingRequests(preTurnJson);
        if (requests.Count == 0)
        {
            if (HasExplicitEmptyShiningRequestsArray(preTurnJson))
                return;

            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_shining_faction_foundings.json unreadable или malformed.",
                code: "shining_founding_malformed_validated_snapshot_request",
                section: "ShiningAbode",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_shining_faction_foundings.json exact client-authored contract."));
            return;
        }

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(shiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_founding_missing_current_shining_state",
                "Resolved Shining founding request требует current shining_abode_state.json для строгой проверки результата.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining founding; materialize hall/faction и receipt в current authoritative state.");
        }

        if (string.IsNullOrWhiteSpace(residentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_founding_missing_current_resident_state",
                "Resolved Shining founding request требует current guardian_abode_residents.json для строгой проверки residents/halls cross-state.",
                "Оставь guardian_abode_residents.json доступным на accepted turn с pending Shining founding.");
        }

        if (string.IsNullOrWhiteSpace(soulJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                "game_state/meta/soul_state.json",
                "shining_founding_missing_current_soul_state",
                "Resolved Shining founding request требует current soul_state.json для строгой проверки зарезервированных Ink Feathers.",
                "Оставь soul_state.json доступным на accepted turn с pending Shining founding; не возвращай клиентом уже зарезервированные Перья.");
        }

        if (string.IsNullOrWhiteSpace(shiningJson) || string.IsNullOrWhiteSpace(residentsJson))
            return;

        var preTurnShiningJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            issues,
            code: "shining_founding_missing_pre_turn_shining_state",
            section: "ShiningAbode",
            message: "Shining founding resolution требует validated pre-turn shining_abode_state.json для проверки, что GM не откатил локально зарезервированные Light Sparks.",
            repairHint: "Сохраняй canonical pre-turn shining_abode_state.json в validated pending snapshot после создания founding pending request.");
        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "shining_founding_missing_pre_turn_soul_state",
            section: "ShiningAbode",
            message: "Shining founding resolution требует validated pre-turn soul_state.json для проверки, что GM не откатил локально зарезервированные Ink Feathers.",
            repairHint: "Сохраняй canonical pre-turn soul_state.json в validated pending snapshot после создания founding pending request.");

        try
        {
            if (JsonNode.Parse(shiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(residentsJson) is not JsonObject currentResidentsRoot)
            {
                return;
            }

            var currentSoulRoot = TryParseJsonObject(soulJson);
            var preTurnShiningRoot = TryParseJsonObject(preTurnShiningJson);
            var preTurnSoulRoot = TryParseJsonObject(preTurnSoulJson);
            var currentGuardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, currentGuardiansRoot);
            var receipts = ShiningAbodeState.EnsureFactionFoundingReceiptsArray(currentShiningRoot);

            foreach (var request in requests)
            {
                var receipt = ShiningAbodeState.FindReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingFoundingsRequestPath,
                        IssueSeverity.Error,
                        "pending Shining founding request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "shining_founding_missing_resolution",
                        section: "ShiningAbode",
                        repairHint: "Каждый Shining founding request должен закрываться в ближайшем accepted turn через shining_abode_state.json.factionFoundingReceipts[]."));
                    continue;
                }

                if (!ShiningFoundingReceiptMatchesRequest(receipt, request, out var receiptActual))
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingFoundingsRequestPath,
                        IssueSeverity.Error,
                        "Shining founding receipt не совпадает с client-authored founding contract.",
                        code: "shining_founding_receipt_mismatch",
                        section: "ShiningAbode",
                        expected: $"{request.ProposedFactionId} / {request.ProposedHallId} / {request.Charter.FactionName}",
                        actual: receiptActual,
                        repairHint: "Синхронизируй factionFoundingReceipts[] с pending founding request exact ids, hall payload и supporter list."));
                    continue;
                }

                var status = GetNodeString(receipt["status"]) ?? string.Empty;
                var currentFaction = ShiningAbodeState.FindFaction(currentShiningRoot, request.ProposedFactionId);
                var currentHall = FindShiningHall(currentShiningRoot, request.ProposedHallId);
                if (string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateAcceptedShiningFoundingReservedResources(
                        issues,
                        currentShiningRoot,
                        currentSoulRoot,
                        preTurnShiningRoot,
                        preTurnSoulRoot);

                    var hallActual = currentHall == null ? "hall_missing" : string.Empty;
                    if (currentHall == null || !ShiningHallMatchesFoundingRequest(currentHall, request, out hallActual))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingFoundingsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining founding не materialize-ил canonical hall exact founding payload.",
                            code: "shining_founding_missing_hall_materialization",
                            section: "ShiningAbode",
                            expected: $"{request.ProposedHallId} / {request.ProposedHallName}",
                            actual: hallActual,
                            repairHint: "При accepted founding создавай hall точно из proposedHallId/proposedHallName/proposedHallDescription/proposedHallServiceTags."));
                    }

                    var factionActual = currentFaction == null ? "faction_missing" : string.Empty;
                    if (currentFaction == null || !ShiningFactionMatchesAcceptedFounding(currentFaction, request, out factionActual))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingFoundingsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining founding не materialize-ил canonical player_founded faction.",
                            code: "shining_founding_missing_faction_materialization",
                            section: "ShiningAbode",
                            expected: $"{request.ProposedFactionId} / player_founded / player_soul",
                            actual: factionActual,
                            repairHint: "При accepted founding materialize-ь faction с originType=player_founded, charter из request и leadership на player_soul."));
                    }

                    foreach (var supporterId in request.SupportingResidentIds
                                 .Where(id => !string.IsNullOrWhiteSpace(id))
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var resident = GuardianAbodeResidentState.FindResident(currentResidentsRoot, supporterId);
                        if (resident != null &&
                            string.Equals(GetNodeString(resident["shiningFactionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingFoundingsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining founding не перенёс supporter resident в новую фракцию.",
                            code: "shining_founding_supporter_not_reassigned",
                            section: "ShiningAbode",
                            expected: request.ProposedFactionId,
                            actual: resident == null ? "resident_missing" : (GetNodeString(resident["shiningFactionId"]) ?? "null"),
                            repairHint: "После accepted founding обнови resident.shiningFactionId для каждого supporter из request."));
                    }
                }
                else if (currentFaction != null || currentHall != null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingFoundingsRequestPath,
                        IssueSeverity.Error,
                        "Refused/withdrawn Shining founding не должен materialize-ить hall или faction из запроса.",
                        code: "shining_founding_unexpected_materialization_after_non_accept",
                        section: "ShiningAbode",
                        repairHint: "Если founding request завершён не как accepted, не materialize hall/faction из его payload."));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingShiningCoreActionResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            issues,
            code: "shining_core_action_missing_validated_snapshot_request",
            section: "ShiningAbode",
            message: "pending_shining_abode_actions.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Shining core action contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_shining_abode_actions.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ShiningCoreActionRequestState.ReadRequests(preTurnJson);
        if (requests.Count == 0)
        {
            if (!HasExplicitEmptyShiningRequestsArray(preTurnJson))
            {
                issues.Add(new ValidationIssue(
                    ShiningCoreActionRequestState.PendingActionsRequestPath,
                    IssueSeverity.Error,
                    "validated snapshot pending_shining_abode_actions.json unreadable или malformed.",
                    code: "shining_core_action_malformed_validated_snapshot_request",
                    section: "ShiningAbode",
                    repairHint: "Сохраняй в validated pending snapshot machine-readable pending_shining_abode_actions.json exact client-authored contract."));
            }

            return;
        }

        var currentShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var currentResidentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(currentShiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_core_action_missing_current_shining_state",
                "Resolved Shining core action требует current shining_abode_state.json для строгой проверки результата.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining core action; запиши receipt и все state mutations в current authoritative state.");
        }

        if (string.IsNullOrWhiteSpace(currentResidentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_core_action_missing_current_resident_state",
                "Resolved Shining core action требует current guardian_abode_residents.json для проверки resident side effects.",
                "Оставь guardian_abode_residents.json доступным на accepted turn с pending Shining core action.");
        }

        if (string.IsNullOrWhiteSpace(currentSoulJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                "game_state/meta/soul_state.json",
                "shining_core_action_missing_current_soul_state",
                "Resolved Shining core action требует current soul_state.json для проверки Ink Feathers, relics и afterlife data.",
                "Оставь soul_state.json доступным на accepted turn с pending Shining core action.");
        }

        if (string.IsNullOrWhiteSpace(currentShiningJson) ||
            string.IsNullOrWhiteSpace(currentResidentsJson) ||
            string.IsNullOrWhiteSpace(currentSoulJson))
        {
            return;
        }

        var preTurnShiningJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            issues,
            code: "shining_core_action_missing_pre_turn_shining_state",
            section: "ShiningAbode",
            message: "Shining core action resolution требует validated pre-turn shining_abode_state.json.",
            repairHint: "Сохраняй canonical pre-turn shining_abode_state.json в validated pending snapshot для строгой проверки core action resolution.");
        var preTurnResidentsJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            issues,
            code: "shining_core_action_missing_pre_turn_resident_state",
            section: "ShiningAbode",
            message: "Shining core action resolution требует validated pre-turn guardian_abode_residents.json.",
            repairHint: "Сохраняй canonical pre-turn guardian_abode_residents.json в validated pending snapshot для строгой проверки Shining resident deltas.");
        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "shining_core_action_missing_pre_turn_soul_state",
            section: "ShiningAbode",
            message: "Shining core action resolution требует validated pre-turn soul_state.json.",
            repairHint: "Сохраняй canonical pre-turn soul_state.json в validated pending snapshot для строгой проверки Ink Feather costs and realm handoff.");
        if (string.IsNullOrWhiteSpace(preTurnShiningJson) ||
            string.IsNullOrWhiteSpace(preTurnResidentsJson) ||
            string.IsNullOrWhiteSpace(preTurnSoulJson))
        {
            return;
        }

        try
        {
            if (JsonNode.Parse(currentShiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(currentResidentsJson) is not JsonObject currentResidentsRoot ||
                JsonNode.Parse(currentSoulJson) is not JsonObject currentSoulRoot ||
                JsonNode.Parse(preTurnShiningJson) is not JsonObject preTurnShiningRoot ||
                JsonNode.Parse(preTurnResidentsJson) is not JsonObject preTurnResidentsRoot ||
                JsonNode.Parse(preTurnSoulJson) is not JsonObject preTurnSoulRoot)
            {
                return;
            }

            var compositePreTurnShiningRoot = CloneJsonObject(preTurnShiningRoot);
            var compositePreTurnResidentsRoot = CloneJsonObject(preTurnResidentsRoot);
            var hasConcurrentShiningClosure = await TryApplyConcurrentShiningClosuresAsync(
                compositePreTurnShiningRoot,
                compositePreTurnResidentsRoot,
                currentShiningRoot,
                currentResidentsRoot);
            var receipts = ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot);
            foreach (var request in requests)
            {
                var receipt = ShiningAbodeState.FindReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningCoreActionRequestState.PendingActionsRequestPath,
                        IssueSeverity.Error,
                        "pending Shining core action request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "shining_core_action_missing_resolution",
                        section: "ShiningAbode",
                        repairHint: "Каждый Shining core action request должен закрываться в ближайшем accepted turn через shining_abode_state.json.coreActionReceipts[]."));
                    continue;
                }

                if (!ShiningCoreActionReceiptMatchesRequest(receipt, request, out var receiptActual))
                {
                    issues.Add(new ValidationIssue(
                        ShiningCoreActionRequestState.PendingActionsRequestPath,
                        IssueSeverity.Error,
                        "Shining core action receipt не совпадает с client-authored core action contract.",
                        code: "shining_core_action_receipt_mismatch",
                        section: "ShiningAbode",
                        expected: $"{request.ActionType} / {request.FactionId} / {request.ProjectId}",
                        actual: receiptActual,
                        repairHint: "Синхронизируй coreActionReceipts[] с pending_shining_abode_actions.json exact actionType, target ids и selected cards."));
                    continue;
                }

                ValidateShiningCoreActionReceiptAuditFields(receipt, request, issues);

                var status = GetNodeString(receipt["status"]) ?? string.Empty;
                if (string.Equals(status, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateAcceptedShiningCoreActionOutcome(
                        request,
                        receipt,
                        compositePreTurnShiningRoot,
                        compositePreTurnResidentsRoot,
                        preTurnSoulRoot,
                        currentShiningRoot,
                        currentResidentsRoot,
                        currentSoulRoot,
                        issues,
                        hasConcurrentShiningClosure);
                }
                else
                {
                    ValidateNonAcceptedShiningCoreActionOutcome(
                        request,
                        compositePreTurnShiningRoot,
                        compositePreTurnResidentsRoot,
                        preTurnSoulRoot,
                        currentShiningRoot,
                        currentResidentsRoot,
                        currentSoulRoot,
                        issues,
                        hasConcurrentShiningClosure);
                }
            }
        }
        catch
        {
            // parse issues are reported elsewhere
        }
    }

    private async Task ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync(List<ValidationIssue> issues)
    {
        var currentShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var currentHasLegacyPending = false;
        try
        {
            currentHasLegacyPending = JsonNode.Parse(currentShiningJson ?? string.Empty) is JsonObject currentProbe &&
                                      currentProbe["pendingNativeFactionDiscovery"] is JsonObject;
        }
        catch
        {
            // parse issues are reported elsewhere
        }

        var preTurnShiningJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnShiningJson))
        {
            if (currentHasLegacyPending)
            {
                issues.Add(new ValidationIssue(
                    ShiningAbodeState.StatePath,
                    IssueSeverity.Error,
                    "Accepted turn содержит live pendingNativeFactionDiscovery, но отсутствует validated pre-turn shining_abode_state.json для строгой проверки closure.",
                    code: "shining_legacy_native_discovery_missing_pre_turn_shining_state",
                    section: "ShiningAbode",
                    expected: "validated pending turn snapshot with pre-turn shining_abode_state.json",
                    actual: "current pendingNativeFactionDiscovery object",
                    repairHint: "Сохраняй pre-turn shining_abode_state.json в pending_turn_snapshot перед accepted turn с legacy pendingNativeFactionDiscovery и закрывай его в этом же ходе."));
            }

            return;
        }

        try
        {
            if (JsonNode.Parse(preTurnShiningJson) is not JsonObject preTurnShiningProbe ||
                preTurnShiningProbe["pendingNativeFactionDiscovery"] is not JsonObject pendingDiscovery)
            {
                return;
            }
        }
        catch
        {
            return;
        }

        var currentResidentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(currentShiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_legacy_native_discovery_missing_current_shining_state",
                "Legacy pendingNativeFactionDiscovery требует current shining_abode_state.json для строгой проверки результата.",
                "Не удаляй shining_abode_state.json на accepted turn с pendingNativeFactionDiscovery; materialize native faction, запиши receipt и очисти pendingNativeFactionDiscovery.");
        }

        if (string.IsNullOrWhiteSpace(currentResidentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_legacy_native_discovery_missing_current_resident_state",
                "Legacy pendingNativeFactionDiscovery требует current guardian_abode_residents.json для проверки новых residents.",
                "Оставь guardian_abode_residents.json доступным и materialize 2..4 ascended residents из discovery receipt.");
        }

        if (string.IsNullOrWhiteSpace(currentSoulJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                "game_state/meta/soul_state.json",
                "shining_legacy_native_discovery_missing_current_soul_state",
                "Legacy pendingNativeFactionDiscovery требует current soul_state.json для проверки Ink Feather cost.",
                "Оставь soul_state.json доступным на accepted turn с pendingNativeFactionDiscovery.");
        }

        if (string.IsNullOrWhiteSpace(currentShiningJson) ||
            string.IsNullOrWhiteSpace(currentResidentsJson) ||
            string.IsNullOrWhiteSpace(currentSoulJson))
        {
            return;
        }

        var preTurnResidentsJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            issues,
            code: "shining_legacy_native_discovery_missing_pre_turn_resident_state",
            section: "ShiningAbode",
            message: "Legacy pendingNativeFactionDiscovery resolution требует validated pre-turn guardian_abode_residents.json.",
            repairHint: "Сохраняй canonical pre-turn guardian_abode_residents.json в validated pending snapshot для строгой проверки новых discovery residents.");
        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "shining_legacy_native_discovery_missing_pre_turn_soul_state",
            section: "ShiningAbode",
            message: "Legacy pendingNativeFactionDiscovery resolution требует validated pre-turn soul_state.json.",
            repairHint: "Сохраняй canonical pre-turn soul_state.json в validated pending snapshot для проверки Ink Feather cost.");
        if (string.IsNullOrWhiteSpace(preTurnResidentsJson) ||
            string.IsNullOrWhiteSpace(preTurnSoulJson))
        {
            return;
        }

        try
        {
            if (JsonNode.Parse(preTurnShiningJson) is not JsonObject preTurnShiningRoot ||
                preTurnShiningRoot["pendingNativeFactionDiscovery"] is not JsonObject pendingDiscovery ||
                JsonNode.Parse(preTurnResidentsJson) is not JsonObject preTurnResidentsRoot ||
                JsonNode.Parse(preTurnSoulJson) is not JsonObject preTurnSoulRoot ||
                JsonNode.Parse(currentShiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(currentResidentsJson) is not JsonObject currentResidentsRoot ||
                JsonNode.Parse(currentSoulJson) is not JsonObject currentSoulRoot)
            {
                return;
            }

            GuardianAbodeResidentState.NormalizeShape(preTurnResidentsRoot);
            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            var guardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            ShiningAbodeState.NormalizeStateRoot(preTurnShiningRoot, preTurnResidentsRoot, guardiansRoot);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, guardiansRoot);

            var request = new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                RequestId = GetNodeString(pendingDiscovery["requestId"]) ?? string.Empty,
                ActionType = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
                RadianceTierAtRequest = GetNodeInt(pendingDiscovery["radianceTierAtRequest"]),
                QuotedCostFeathers = GetNodeInt(pendingDiscovery["costFeathers"]),
                QuotedCostLightSparks = 0,
                CreatedAtTurn = GetNodeInt(pendingDiscovery["createdAtTurn"]),
                CreatedAtUtc = GetNodeString(pendingDiscovery["createdAtUtc"]) ?? string.Empty
            };

            var receipts = ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot);
            var receipt = ShiningAbodeState.FindReceipt(receipts, request.RequestId);
            if (receipt == null)
            {
                issues.Add(new ValidationIssue(
                    ShiningAbodeState.StatePath,
                    IssueSeverity.Error,
                    "Legacy pendingNativeFactionDiscovery не был закрыт в текущем accepted turn.",
                    code: "shining_legacy_native_discovery_missing_resolution",
                    section: "ShiningAbode",
                    repairHint: "Закрой pendingNativeFactionDiscovery через coreActionReceipts[] с actionType=discover_native_faction, materialize native faction/residents/projects и очисти pendingNativeFactionDiscovery."));
                return;
            }

            if (!ShiningCoreActionReceiptMatchesRequest(receipt, request, out var receiptActual))
            {
                issues.Add(new ValidationIssue(
                    ShiningAbodeState.StatePath,
                    IssueSeverity.Error,
                    "Legacy pendingNativeFactionDiscovery receipt не совпадает с state-local contract.",
                    code: "shining_legacy_native_discovery_receipt_mismatch",
                    section: "ShiningAbode",
                    expected: $"{request.ActionType} / {request.RequestId}",
                    actual: receiptActual,
                    repairHint: "Синхронизируй coreActionReceipts[] с pendingNativeFactionDiscovery.requestId и actionType=discover_native_faction."));
                return;
            }

            ValidateRequiredCoreActionReceiptIntAudit(receipt, request, "quotedCostFeathers", request.QuotedCostFeathers, issues);
            ValidateRequiredCoreActionReceiptIntAudit(receipt, request, "quotedCostLightSparks", request.QuotedCostLightSparks, issues);

            if (currentShiningRoot["pendingNativeFactionDiscovery"] is JsonObject)
            {
                issues.Add(new ValidationIssue(
                    ShiningAbodeState.StatePath,
                    IssueSeverity.Error,
                    "Legacy pendingNativeFactionDiscovery должен быть очищен после закрытия accepted turn.",
                    code: "shining_legacy_native_discovery_not_cleared",
                    section: "ShiningAbode",
                    expected: "pendingNativeFactionDiscovery = null",
                    actual: "pendingNativeFactionDiscovery object",
                    repairHint: "После materialization и matching coreActionReceipts[] установи shining_abode_state.pendingNativeFactionDiscovery в null."));
            }

            var status = GetNodeString(receipt["status"]) ?? string.Empty;
            if (string.Equals(status, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                ValidateAcceptedShiningNativeDiscoveryOutcome(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues);
            }
            else
            {
                ValidateNonAcceptedShiningCoreActionOutcome(
                    request,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure: false);
            }
        }
        catch
        {
            // parse issues are reported elsewhere
        }
    }

    private async Task ValidatePendingShiningRealignmentResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            issues,
            code: "shining_realignment_missing_validated_snapshot_request",
            section: "ShiningAbode",
            message: "pending_shining_faction_realignments.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Shining realignment contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_shining_faction_realignments.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ShiningFactionRequestState.ReadRealignmentRequests(preTurnJson);
        if (requests.Count == 0)
        {
            if (HasExplicitEmptyShiningRequestsArray(preTurnJson))
                return;

            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_shining_faction_realignments.json unreadable или malformed.",
                code: "shining_realignment_malformed_validated_snapshot_request",
                section: "ShiningAbode",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_shining_faction_realignments.json exact client-authored contract."));
            return;
        }

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_realignment_missing_current_shining_state",
                "Resolved Shining realignment request требует current shining_abode_state.json для проверки faction receipt.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining realignment; закрой request через factionRealignmentReceipts.");
        }

        if (string.IsNullOrWhiteSpace(residentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_realignment_missing_current_resident_state",
                "Resolved Shining realignment request требует current guardian_abode_residents.json для проверки membership/history.",
                "Оставь guardian_abode_residents.json доступным и обнови resident membership/history на accepted turn с pending Shining realignment.");
        }

        if (string.IsNullOrWhiteSpace(shiningJson) || string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            if (JsonNode.Parse(shiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(residentsJson) is not JsonObject currentResidentsRoot)
            {
                return;
            }

            var currentGuardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, currentGuardiansRoot);
            var receipts = ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(currentShiningRoot);
            var historyLog = GuardianAbodeResidentState.EnsureHistoryLogArray(currentResidentsRoot);

            foreach (var request in requests)
            {
                var receipt = ShiningAbodeState.FindReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingRealignmentsRequestPath,
                        IssueSeverity.Error,
                        "pending Shining realignment request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "shining_realignment_missing_resolution",
                        section: "ShiningAbode",
                        repairHint: "Каждый Shining realignment request должен закрываться в ближайшем accepted turn через shining_abode_state.json.factionRealignmentReceipts[]."));
                    continue;
                }

                if (!ShiningRealignmentReceiptMatchesRequest(receipt, request, out var receiptActual))
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingRealignmentsRequestPath,
                        IssueSeverity.Error,
                        "Shining realignment receipt не совпадает с client-authored realignment contract.",
                        code: "shining_realignment_receipt_mismatch",
                        section: "ShiningAbode",
                        expected: $"{request.ResidentId} / {request.SourceFactionId} / {request.TargetFactionId} / {request.RealignmentMode}",
                        actual: receiptActual,
                        repairHint: "Синхронизируй factionRealignmentReceipts[] с pending realignment request exact resident, source/target faction и realignmentMode."));
                    continue;
                }

                var status = GetNodeString(receipt["status"]) ?? string.Empty;
                var resident = GuardianAbodeResidentState.FindResident(currentResidentsRoot, request.ResidentId);
                if (resident == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingRealignmentsRequestPath,
                        IssueSeverity.Error,
                        "Resolved Shining realignment должен ссылаться на существующего resident в current roster.",
                        code: "shining_realignment_missing_current_resident",
                        section: "ShiningAbode",
                        repairHint: "Не удаляй resident из guardian_abode_residents.json при Shining faction realignment; обновляй membership на той же resident identity."));
                    continue;
                }

                var residentFactionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
                if (string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    if (ShiningAbodeState.FindFaction(currentShiningRoot, request.TargetFactionId) == null)
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingRealignmentsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining realignment с target faction требует существующую target faction в current state.",
                            code: "shining_realignment_missing_target_faction",
                            section: "ShiningAbode",
                            actual: request.TargetFactionId,
                            repairHint: "При accepted_transfer resident может перейти только в существующую target faction."));
                    }

                    if (!string.Equals(residentFactionId, request.TargetFactionId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingRealignmentsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining realignment не обновил resident.shiningFactionId до target faction.",
                            code: "shining_realignment_missing_membership_update",
                            section: "ShiningAbode",
                            expected: request.TargetFactionId,
                            actual: residentFactionId));
                    }

                    var historyEntryId = GetNodeString(receipt["residentHistoryEntryId"]);
                    if (string.IsNullOrWhiteSpace(historyEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, historyEntryId))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingRealignmentsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining realignment должен оставлять resident history entry.",
                            code: "shining_realignment_missing_history_entry",
                            section: "ShiningAbode",
                            repairHint: "После accepted realignment запиши residentHistoryEntryId в receipt и matching historyLog entry в guardian_abode_residents.json."));
                    }
                }
                else if (string.Equals(status, ShiningFactionRequestState.RequestStatusDepartedToNeutral, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(residentFactionId))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingRealignmentsRequestPath,
                            IssueSeverity.Error,
                            "departure_to_neutral должен очистить resident.shiningFactionId.",
                            code: "shining_realignment_neutral_departure_still_bound",
                            section: "ShiningAbode",
                            expected: "empty_or_null",
                            actual: residentFactionId));
                    }

                    var historyEntryId = GetNodeString(receipt["residentHistoryEntryId"]);
                    if (string.IsNullOrWhiteSpace(historyEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, historyEntryId))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingRealignmentsRequestPath,
                            IssueSeverity.Error,
                            "departure_to_neutral должен оставлять resident history entry.",
                            code: "shining_realignment_neutral_departure_missing_history",
                            section: "ShiningAbode",
                            repairHint: "После departed_to_neutral запиши residentHistoryEntryId в receipt и matching historyLog entry."));
                    }
                }
                else if (!string.Equals(residentFactionId, request.SourceFactionId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingRealignmentsRequestPath,
                        IssueSeverity.Error,
                        "Refused/withdrawn Shining realignment не должен менять resident membership.",
                        code: "shining_realignment_unexpected_membership_change_after_non_accept",
                        section: "ShiningAbode",
                        expected: request.SourceFactionId,
                        actual: residentFactionId));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidatePendingShiningTradeInventoryResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningTradeRequestState.PendingRequestsPath,
            issues,
            code: "shining_trade_request_missing_validated_snapshot_request",
            section: "ShiningAbode",
            message: "pending_shining_trade_inventory_requests.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Shining trade contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_shining_trade_inventory_requests.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ShiningTradeRequestState.ReadRequests(preTurnJson);
        if (requests.Count == 0)
        {
            if (HasExplicitEmptyShiningRequestsArray(preTurnJson))
                return;

            issues.Add(new ValidationIssue(
                ShiningTradeRequestState.PendingRequestsPath,
                IssueSeverity.Error,
                "validated snapshot pending_shining_trade_inventory_requests.json unreadable или malformed.",
                code: "shining_trade_request_malformed_validated_snapshot_request",
                section: "ShiningAbode",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_shining_trade_inventory_requests.json exact client-authored contract."));
            return;
        }

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(shiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_trade_request_missing_current_shining_state",
                "Resolved Shining trade inventory request требует current shining_abode_state.json для проверки faction.tradeInventory.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining trade request; materialize tradeInventory и receipt внутри current faction.");
            return;
        }

        try
        {
            if (JsonNode.Parse(shiningJson) is not JsonObject currentShiningRoot)
                return;

            var currentResidentsRoot = !string.IsNullOrWhiteSpace(residentsJson)
                ? JsonNode.Parse(residentsJson) as JsonObject
                : null;
            var currentSoulRoot = !string.IsNullOrWhiteSpace(soulJson)
                ? JsonNode.Parse(soulJson) as JsonObject
                : null;
            var ownedSoulRelicIds = CollectOwnedSoulRelicIds(currentSoulRoot);
            var preTurnSoulRoot = TryParseJsonObject(await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json"));
            ownedSoulRelicIds.UnionWith(CollectOwnedSoulRelicIds(preTurnSoulRoot));
            var currentGuardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, currentGuardiansRoot);

            foreach (var request in requests)
            {
                var faction = ShiningAbodeState.FindFaction(currentShiningRoot, request.FactionId);
                if (faction == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningTradeRequestState.PendingRequestsPath,
                        IssueSeverity.Error,
                        "Resolved Shining trade request требует существующую faction в current state.",
                        code: "shining_trade_request_missing_current_faction",
                        section: "ShiningAbode",
                        actual: request.FactionId,
                        repairHint: "Materialize explicit Shining tradeInventory только внутри существующей current faction."));
                    continue;
                }

                var tradeInventory = faction["tradeInventory"] as JsonObject;
                if (!ShiningTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request))
                {
                    issues.Add(new ValidationIssue(
                        ShiningTradeRequestState.PendingRequestsPath,
                        IssueSeverity.Error,
                        "pending_shining_trade_inventory_requests из pre-turn snapshot не привёл к matching faction.tradeInventory",
                        code: "shining_trade_request_missing_inventory_resolution",
                        section: "ShiningAbode",
                        repairHint: "На accepted turn materialize explicit faction.tradeInventory по exact client-authored Shining trade request contract.")); 
                    continue;
                }

                AddShiningTradeInventoryOwnedRelicCollisions(tradeInventory, ownedSoulRelicIds, issues);

                if (!ShiningTradeRequestState.ReceiptMatchesRequestContract(
                        ShiningTradeRequestState.FindMatchingReceipt(faction, request),
                        request,
                        tradeInventory))
                {
                    issues.Add(new ValidationIssue(
                        ShiningTradeRequestState.PendingRequestsPath,
                        IssueSeverity.Error,
                        "pending_shining_trade_inventory_requests из pre-turn snapshot не был закрыт canonical tradeInventory receipt",
                        code: "shining_trade_request_missing_receipt_resolution",
                        section: "ShiningAbode",
                        repairHint: "После materialize explicit faction.tradeInventory обязательно закрой запрос через faction.tradeInventoryReceipts[] и matching requestId/tradeCycleId/itemCount timing."));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private static HashSet<string> CollectOwnedSoulRelicIds(JsonObject? soulRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var soulRelics = soulRoot?["soulRelics"];
        if (soulRelics is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                {
                    var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                    if (!string.IsNullOrWhiteSpace(relicId))
                        result.Add(relicId);
                }
            }
        }
        else if (soulRelics is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
            {
                var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                if (!string.IsNullOrWhiteSpace(relicId))
                    result.Add(relicId);
            }
        }

        return result;
    }

    private static void AddShiningTradeInventoryOwnedRelicCollisions(
        JsonObject? tradeInventory,
        HashSet<string> ownedSoulRelicIds,
        List<ValidationIssue> issues)
    {
        if (tradeInventory?["items"] is not JsonArray items || ownedSoulRelicIds.Count == 0)
            return;

        var index = 0;
        foreach (var item in items.OfType<JsonObject>())
        {
            var relicData = item["relicData"] as JsonObject;
            var relicId = GetNodeString(relicData?["relicId"]) ?? GetNodeString(relicData?["id"]);
            if (!string.IsNullOrWhiteSpace(relicId) && ownedSoulRelicIds.Contains(relicId))
            {
                issues.Add(new ValidationIssue(
                    ShiningTradeRequestState.PendingRequestsPath,
                    IssueSeverity.Error,
                    "Shining tradeInventory не должен материализовать relicData.relicId, который уже есть в soul_state.soulRelics.",
                    code: "shining_trade_inventory_owned_relic_id_collision",
                    section: "ShiningAbode",
                    expected: "new unique Soul Relic identity",
                    actual: $"items[{index}].relicData.relicId={relicId}",
                    repairHint: "Для каждого торгового слота создай новый relicId, отсутствующий в soulRelics.equipped и soulRelics.stored."));
            }

            index++;
        }
    }

    private async Task ValidatePendingShiningLeadershipTransitionResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            issues,
            code: "shining_leadership_missing_validated_snapshot_request",
            section: "ShiningAbode",
            message: "pending_shining_faction_leadership_transitions.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Shining leadership contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_shining_faction_leadership_transitions.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ShiningFactionRequestState.ReadLeadershipTransitionRequests(preTurnJson);
        if (requests.Count == 0)
        {
            if (HasExplicitEmptyShiningRequestsArray(preTurnJson))
                return;

            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_shining_faction_leadership_transitions.json unreadable или malformed.",
                code: "shining_leadership_malformed_validated_snapshot_request",
                section: "ShiningAbode",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_shining_faction_leadership_transitions.json exact client-authored contract."));
            return;
        }

        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_leadership_missing_current_shining_state",
                "Resolved Shining leadership request требует current shining_abode_state.json для проверки leadership receipt/history.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining leadership transition; обнови faction leadership и receipts.");
        }

        if (string.IsNullOrWhiteSpace(residentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_leadership_missing_current_resident_state",
                "Resolved Shining leadership request требует current guardian_abode_residents.json для проверки candidate actor bindings.",
                "Оставь guardian_abode_residents.json доступным на accepted turn с pending Shining leadership transition.");
        }

        if (string.IsNullOrWhiteSpace(shiningJson) || string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            if (JsonNode.Parse(shiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(residentsJson) is not JsonObject currentResidentsRoot)
            {
                return;
            }

            var currentGuardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, currentGuardiansRoot);

            foreach (var request in requests)
            {
                var faction = ShiningAbodeState.FindFaction(currentShiningRoot, request.FactionId);
                if (faction == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "Shining leadership resolution требует существующую faction в current state.",
                        code: "shining_leadership_missing_current_faction",
                        section: "ShiningAbode",
                        actual: request.FactionId,
                        repairHint: "Не удаляй target faction при leadership resolution; обновляй её nested leadership/receipts/history canonically."));
                    continue;
                }

                var receipts = faction["leadershipReceipts"] as JsonArray ?? new JsonArray();
                var history = faction["leadershipHistory"] as JsonArray ?? new JsonArray();
                var receipt = ShiningAbodeState.FindReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "pending Shining leadership request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "shining_leadership_missing_resolution",
                        section: "ShiningAbode",
                        repairHint: "Каждый Shining leadership request должен закрываться в ближайшем accepted turn через faction.leadershipReceipts[]."));
                    continue;
                }

                if (!ShiningLeadershipReceiptMatchesRequest(receipt, request, out var receiptActual))
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "Shining leadership receipt не совпадает с client-authored leadership contract.",
                        code: "shining_leadership_receipt_mismatch",
                        section: "ShiningAbode",
                        expected: $"{request.FactionId} / {request.TransitionMode} / {request.CandidateHeadActorType}:{request.CandidateHeadActorId}",
                        actual: receiptActual,
                        repairHint: "Синхронизируй leadershipReceipts[] с pending leadership request exact mode, incumbent и resolved candidate/vacancy."));
                    continue;
                }

                var status = GetNodeString(receipt["status"]) ?? string.Empty;
                var historyEntry = ShiningAbodeState.FindLeadershipHistoryEntry(history, request.RequestId);
                if ((string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(status, ShiningFactionRequestState.RequestStatusRefused, StringComparison.OrdinalIgnoreCase)) &&
                    historyEntry == null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "Accepted/refused Shining leadership transition должен иметь matching leadershipHistory entry.",
                        code: "shining_leadership_missing_history",
                        section: "ShiningAbode",
                        repairHint: "Для accepted/refused leadership transition добавляй matching leadershipHistory[] entry с тем же requestId."));
                }

                if (historyEntry != null &&
                    !ShiningLeadershipHistoryMatchesOutcome(historyEntry, receipt, request, out var historyActual))
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "Shining leadership history не совпадает с resolved transition outcome.",
                        code: "shining_leadership_history_mismatch",
                        section: "ShiningAbode",
                        expected: ResolveExpectedLeadershipHistoryEventType(request, status),
                        actual: historyActual,
                        repairHint: "Синхронизируй leadershipHistory[] eventType/turnNumber с resolved receipt status и transition mode."));
                }

                var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
                if (string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ShiningLeadershipMatchesAcceptedOutcome(leadership, request, out var leadershipActual))
                    {
                        issues.Add(new ValidationIssue(
                            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                            IssueSeverity.Error,
                            "Accepted Shining leadership transition не materialize-ил canonical faction.leadership outcome.",
                            code: "shining_leadership_missing_state_update",
                            section: "ShiningAbode",
                            expected: string.IsNullOrWhiteSpace(request.CandidateHeadActorType)
                                ? "vacant leadership"
                                : $"{request.CandidateHeadActorType}:{request.CandidateHeadActorId}",
                            actual: leadershipActual,
                            repairHint: "После accepted leadership transition обнови faction.leadership exact resolved head или vacancy."));
                    }
                }
                else if (string.Equals(GetNodeString(leadership["headActorType"]), request.IncumbentHeadActorType, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(GetNodeString(leadership["headActorId"]), request.IncumbentHeadActorId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else
                {
                    issues.Add(new ValidationIssue(
                        ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                        IssueSeverity.Error,
                        "Refused/withdrawn Shining leadership transition не должен менять текущего главу фракции.",
                        code: "shining_leadership_unexpected_state_change_after_non_accept",
                        section: "ShiningAbode",
                        expected: $"{request.IncumbentHeadActorType}:{request.IncumbentHeadActorId}",
                        actual: $"{GetNodeString(leadership["headActorType"])}:{GetNodeString(leadership["headActorId"])}"));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task ValidateShiningClosureCompositeDiffAsync(List<ValidationIssue> issues)
    {
        var hasFoundingRequests = ShiningFactionRequestState
            .ReadFoundingRequests(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingFoundingsRequestPath))
            .Count > 0;
        var hasRealignmentRequests = ShiningFactionRequestState
            .ReadRealignmentRequests(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath))
            .Count > 0;
        var hasLeadershipRequests = ShiningFactionRequestState
            .ReadLeadershipTransitionRequests(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath))
            .Count > 0;
        var hasTradeRequests = ShiningTradeRequestState
            .ReadRequests(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningTradeRequestState.PendingRequestsPath))
            .Count > 0;

        if (!hasFoundingRequests && !hasRealignmentRequests && !hasLeadershipRequests && !hasTradeRequests)
            return;

        // Core-action resolution already projects these concurrent closure deltas before comparing full state.
        if (ShiningCoreActionRequestState
                .ReadRequests(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath))
                .Count > 0)
        {
            return;
        }

        var currentShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var currentResidentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(currentShiningJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                ShiningAbodeState.StatePath,
                "shining_closure_missing_current_shining_state",
                "Shining closure composite diff требует current shining_abode_state.json.",
                "Не удаляй shining_abode_state.json на accepted turn с pending Shining closure; strict composite diff должен доказать только разрешённые closure deltas.");
        }

        if (string.IsNullOrWhiteSpace(currentResidentsJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                GuardianAbodeResidentState.StatePath,
                "shining_closure_missing_current_resident_state",
                "Shining closure composite diff требует current guardian_abode_residents.json.",
                "Не удаляй guardian_abode_residents.json на accepted turn с pending Shining closure; unrelated resident edits должны быть проверяемы.");
        }

        if (string.IsNullOrWhiteSpace(currentSoulJson))
        {
            AddMissingShiningResolutionCurrentFileIssue(
                issues,
                "game_state/meta/soul_state.json",
                "shining_closure_missing_current_soul_state",
                "Shining closure composite diff требует current soul_state.json.",
                "Не удаляй soul_state.json на accepted turn с pending Shining closure; unrelated soul edits должны быть проверяемы.");
        }

        if (string.IsNullOrWhiteSpace(currentShiningJson) ||
            string.IsNullOrWhiteSpace(currentResidentsJson) ||
            string.IsNullOrWhiteSpace(currentSoulJson))
        {
            return;
        }

        var preTurnShiningJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            issues,
            code: "shining_closure_missing_pre_turn_shining_state",
            section: "ShiningAbode",
            message: "Shining closure composite diff требует validated pre-turn shining_abode_state.json.",
            repairHint: "Сохраняй canonical pre-turn shining_abode_state.json в validated pending snapshot для строгой проверки Shining closure deltas.");
        var preTurnResidentsJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            issues,
            code: "shining_closure_missing_pre_turn_resident_state",
            section: "ShiningAbode",
            message: "Shining closure composite diff требует validated pre-turn guardian_abode_residents.json.",
            repairHint: "Сохраняй canonical pre-turn guardian_abode_residents.json в validated pending snapshot для строгой проверки Shining closure resident deltas.");
        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "shining_closure_missing_pre_turn_soul_state",
            section: "ShiningAbode",
            message: "Shining closure composite diff требует validated pre-turn soul_state.json.",
            repairHint: "Сохраняй canonical pre-turn soul_state.json в validated pending snapshot для строгой проверки, что Shining closures не мутируют unrelated soul state.");

        if (string.IsNullOrWhiteSpace(preTurnShiningJson) ||
            string.IsNullOrWhiteSpace(preTurnResidentsJson) ||
            string.IsNullOrWhiteSpace(preTurnSoulJson))
        {
            return;
        }

        try
        {
            if (JsonNode.Parse(currentShiningJson) is not JsonObject currentShiningRoot ||
                JsonNode.Parse(currentResidentsJson) is not JsonObject currentResidentsRoot ||
                JsonNode.Parse(currentSoulJson) is not JsonObject currentSoulRoot ||
                JsonNode.Parse(preTurnShiningJson) is not JsonObject expectedShiningRoot ||
                JsonNode.Parse(preTurnResidentsJson) is not JsonObject expectedResidentsRoot ||
                JsonNode.Parse(preTurnSoulJson) is not JsonObject expectedSoulRoot)
            {
                return;
            }

            var currentGuardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            GuardianAbodeResidentState.NormalizeShape(expectedResidentsRoot);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, currentResidentsRoot, currentGuardiansRoot);
            ShiningAbodeState.NormalizeStateRoot(expectedShiningRoot, expectedResidentsRoot, currentGuardiansRoot);

            await TryApplyConcurrentShiningClosuresAsync(
                expectedShiningRoot,
                expectedResidentsRoot,
                currentShiningRoot,
                currentResidentsRoot);

            ShiningAbodeState.NormalizeStateRoot(expectedShiningRoot, expectedResidentsRoot, currentGuardiansRoot);
            var progressionControl = await ResolveValidatedCurrentProgressionControlAsync();
            var hasVerifiedProgressionReport = await HasVerifiedAfterlifeProgressionReportForCompositeAsync(progressionControl);
            var allowShiningProgressionDeltas = hasVerifiedProgressionReport &&
                                               progressionControl != null &&
                                               (progressionControl.ShiningAbodeCyclesExpectedThisTurn > 0 ||
                                                progressionControl.ShiningFactionCyclesExpectedThisTurn > 0 ||
                                                progressionControl.ShiningTradeCyclesExpectedThisTurn > 0 ||
                                                progressionControl.AfterlifeCatchupRequired);
            var allowResidentProgressionDeltas = hasVerifiedProgressionReport &&
                                                progressionControl != null &&
                                                (progressionControl.ResidentAgencyCyclesExpectedThisTurn > 0 ||
                                                 progressionControl.AfterlifeCatchupRequired);

            if (!allowShiningProgressionDeltas && !JsonNode.DeepEquals(expectedShiningRoot, currentShiningRoot))
            {
                issues.Add(new ValidationIssue(
                    ShiningAbodeState.StatePath,
                    IssueSeverity.Error,
                    "Shining closure accepted turn содержит посторонние изменения shining_abode_state.json сверх разрешённых pending closure deltas.",
                    code: "shining_closure_unexpected_shining_state_diff",
                    section: "ShiningAbode",
                    expected: "pre-turn Shining state plus exact founding/realignment/leadership/trade closure deltas",
                    actual: "current shining_abode_state.json differs from projected closure-only state",
                    repairHint: "Откати все unrelated Shining mutations; accepted closure может менять только target receipt/materialization/history/tradeInventory, разрешённые client-authored request."));
            }

            if (!allowResidentProgressionDeltas && !JsonNode.DeepEquals(expectedResidentsRoot, currentResidentsRoot))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Shining closure accepted turn содержит посторонние изменения guardian_abode_residents.json сверх разрешённых pending closure deltas.",
                    code: "shining_closure_unexpected_resident_state_diff",
                    section: "ShiningAbode",
                    expected: "pre-turn resident state plus exact supporter/realignment/history closure deltas",
                    actual: "current guardian_abode_residents.json differs from projected closure-only state",
                    repairHint: "Не меняй unrelated residents во время закрытия Shining founding/realignment/leadership/trade contract."));
            }

            var expectedSoulForComparison = CloneJsonObject(expectedSoulRoot);
            if (!JsonNode.DeepEquals(expectedSoulForComparison, currentSoulRoot))
            {
                var projectedSoulRoot = CloneJsonObject(expectedSoulRoot);
                if (await TryProjectConcurrentShiningClosureSoulDeltasAsync(projectedSoulRoot, currentSoulRoot) &&
                    JsonNode.DeepEquals(projectedSoulRoot, currentSoulRoot))
                {
                    expectedSoulForComparison = projectedSoulRoot;
                }
            }

            if (!JsonNode.DeepEquals(expectedSoulForComparison, currentSoulRoot))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Shining closure accepted turn содержит посторонние изменения soul_state.json.",
                    code: "shining_closure_unexpected_soul_state_diff",
                    section: "ShiningAbode",
                    expected: "pre-turn soul_state.json unchanged during Shining closure resolution",
                    actual: "current soul_state.json differs from validated pre-turn snapshot",
                    repairHint: "Shining political/trade closure не должна скрыто менять soul_state.json; отдельные soul mutations требуют собственного client-authored contract."));
            }
        }
        catch
        {
            // parse issues reported elsewhere
        }
    }

    private async Task<ProgressionControl?> ResolveValidatedCurrentProgressionControlAsync()
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        return lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
            ? lookup.Manifest?.ProgressionControl
            : null;
    }

    private async Task<bool> HasVerifiedAfterlifeProgressionReportForCompositeAsync(ProgressionControl? control)
    {
        if (control == null || !RealmSemantics.IsAfterlifeRealm(control.CurrentRealm))
            return false;

        var report = await ReadCurrentProgressionProcessingReportForCompositeAsync();
        if (report == null)
            return false;

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var manifest = lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
            ? lookup.Manifest
            : null;
        if (manifest == null ||
            report.TurnNumber != manifest.TurnNumber ||
            !string.Equals(report.SessionId, manifest.SessionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(report.RequestId, manifest.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (report.WorldCyclesProcessed ?? 0) == 0 &&
               (report.FactionCyclesProcessed ?? 0) == 0 &&
               (report.ChaosSeaCyclesProcessed ?? 0) == Math.Max(0, control.ChaosSeaCyclesExpectedThisTurn) &&
               (report.GuardianProjectCyclesProcessed ?? 0) == Math.Max(0, control.GuardianProjectCyclesExpectedThisTurn) &&
               (report.ResidentAgencyCyclesProcessed ?? 0) == Math.Max(0, control.ResidentAgencyCyclesExpectedThisTurn) &&
               (report.ShiningAbodeCyclesProcessed ?? 0) == Math.Max(0, control.ShiningAbodeCyclesExpectedThisTurn) &&
               (report.ShiningFactionCyclesProcessed ?? 0) == Math.Max(0, control.ShiningFactionCyclesExpectedThisTurn) &&
               (report.ShiningTradeCyclesProcessed ?? 0) == Math.Max(0, control.ShiningTradeCyclesExpectedThisTurn);
    }

    private async Task<ProgressionProcessingReport?> ReadCurrentProgressionProcessingReportForCompositeAsync()
    {
        var json = await _fs.ReadFileAsync(ProgressionScheduleService.ReportPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("progressionProcessingReport", out var nested) &&
                nested.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<ProgressionProcessingReport>(nested.GetRawText(), ManifestJsonOpts);
            }

            return JsonSerializer.Deserialize<ProgressionProcessingReport>(root.GetRawText(), ManifestJsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> TryProjectConcurrentShiningClosureSoulDeltasAsync(JsonObject expectedSoulRoot, JsonObject currentSoulRoot)
    {
        var projectedAny = false;
        projectedAny |= await TryProjectConcurrentAbodeOfferingSoulDeltaAsync(expectedSoulRoot);
        projectedAny |= await TryProjectConcurrentArchiveActionSoulDeltaAsync(
            expectedSoulRoot,
            currentSoulRoot,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.RequestedModeConsultation);
        projectedAny |= await TryProjectConcurrentArchiveActionSoulDeltaAsync(
            expectedSoulRoot,
            currentSoulRoot,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            AfterlifeArchiveActionState.RequestedModeProjectFuel);

        return projectedAny;
    }

    private async Task<bool> TryProjectConcurrentAbodeOfferingSoulDeltaAsync(JsonObject expectedSoulRoot)
    {
        var offeringJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeOfferingState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(offeringJson))
            return false;

        GuardianAbodeOfferingState.PendingAbodeOfferingRequest? offeringRequest;
        try
        {
            offeringRequest = JsonSerializer.Deserialize<GuardianAbodeOfferingState.PendingAbodeOfferingRequest>(offeringJson);
        }
        catch
        {
            return false;
        }

        if (offeringRequest == null)
            return false;

        if (string.Equals(offeringRequest.OfferingType, GuardianAbodeOfferingState.OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            if (offeringRequest.InkFeathersOffered <= 0)
                return false;

            ApplyFeatherCostToSoul(expectedSoulRoot, offeringRequest.InkFeathersOffered);
            return true;
        }

        if (string.Equals(offeringRequest.OfferingType, GuardianAbodeOfferingState.OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
            return TryRemoveSoulRelicFromSoulState(expectedSoulRoot, offeringRequest.RelicId);

        if (string.Equals(offeringRequest.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(offeringRequest.OfferingType, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return TryRemoveAfterlifeArchiveEntryFromSoulState(expectedSoulRoot, offeringRequest.ArchiveId);
        }

        return false;
    }

    private async Task<bool> TryProjectConcurrentArchiveActionSoulDeltaAsync(
        JsonObject expectedSoulRoot,
        JsonObject currentSoulRoot,
        string pendingRequestPath,
        string requestedMode)
    {
        var requestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(pendingRequestPath);
        if (string.IsNullOrWhiteSpace(requestJson))
            return false;

        if (!TryReadArchiveActionRequestIdentity(
                requestJson,
                requestedMode,
                out var requestId,
                out var archiveId,
                out var canonicalRequestedMode))
        {
            return false;
        }

        var resolution = FindCurrentArchiveActionResolution(
            currentSoulRoot,
            requestId,
            archiveId,
            canonicalRequestedMode);
        if (resolution == null)
            return false;

        try
        {
            AfterlifeArchiveState.ApplyActionResolutions(
                expectedSoulRoot,
                new JsonArray(CloneJsonObject(resolution)),
                GetNodeInt(resolution["resolvedAtTurn"]));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadArchiveActionRequestIdentity(
        string requestJson,
        string requestedMode,
        out string requestId,
        out string archiveId,
        out string canonicalRequestedMode)
    {
        requestId = string.Empty;
        archiveId = string.Empty;
        canonicalRequestedMode = requestedMode;

        try
        {
            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase))
            {
                var state = AfterlifeArchiveActionState.ParseConsultationState(requestJson);
                if (state.IsMalformed || state.Request == null)
                    return false;

                requestId = state.Request.RequestId;
                archiveId = state.Request.ArchiveId;
                canonicalRequestedMode = state.Request.RequestedMode;
                return !string.IsNullOrWhiteSpace(requestId) &&
                       !string.IsNullOrWhiteSpace(archiveId) &&
                       string.Equals(canonicalRequestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase))
            {
                var state = AfterlifeArchiveActionState.ParseProjectFuelState(requestJson);
                if (state.IsMalformed || state.Request == null)
                    return false;

                requestId = state.Request.RequestId;
                archiveId = state.Request.ArchiveId;
                canonicalRequestedMode = state.Request.RequestedMode;
                return !string.IsNullOrWhiteSpace(requestId) &&
                       !string.IsNullOrWhiteSpace(archiveId) &&
                       string.Equals(canonicalRequestedMode, AfterlifeArchiveActionState.RequestedModeProjectFuel, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static JsonObject? FindCurrentArchiveActionResolution(
        JsonObject currentSoulRoot,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        if (currentSoulRoot["archiveActionResolutions"] is JsonArray resolutions)
        {
            var resolution = FindArchiveActionObject(resolutions, requestId, archiveId, requestedMode);
            if (resolution != null)
                return resolution;
        }

        if (currentSoulRoot[AfterlifeArchiveState.ContainerProperty] is JsonObject archiveRoot &&
            archiveRoot[AfterlifeArchiveState.ActionReceiptsProperty] is JsonArray receipts)
        {
            return FindArchiveActionObject(receipts, requestId, archiveId, requestedMode);
        }

        return null;
    }

    private static JsonObject? FindArchiveActionObject(
        JsonArray entries,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        return entries
            .OfType<JsonObject>()
            .FirstOrDefault(entry =>
                string.Equals(GetNodeString(entry["requestId"]), requestId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["archiveId"]), archiveId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["requestedMode"]), requestedMode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryRemoveSoulRelicFromSoulState(JsonObject soulRoot, string? relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is JsonArray collection &&
                    TryRemoveArrayEntryById(collection, relicId, "relicId", "id"))
                {
                    return true;
                }
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            return TryRemoveArrayEntryById(flatCollection, relicId, "relicId", "id");
        }

        return false;
    }

    private static bool TryRemoveAfterlifeArchiveEntryFromSoulState(JsonObject soulRoot, string? archiveId)
    {
        if (string.IsNullOrWhiteSpace(archiveId))
            return false;

        return soulRoot["afterlifeArchive"] is JsonObject archiveRoot &&
               archiveRoot["stored"] is JsonArray stored &&
               TryRemoveArrayEntryById(stored, archiveId, "archiveId", "entryId", "id");
    }

    private static bool TryRemoveArrayEntryById(JsonArray array, string id, params string[] idFieldNames)
    {
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject entry)
                continue;

            if (!idFieldNames.Any(fieldName => string.Equals(GetNodeString(entry[fieldName]), id, StringComparison.OrdinalIgnoreCase)))
                continue;

            array.RemoveAt(i);
            return true;
        }

        return false;
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

    private async Task ValidatePendingPlayerGuardianFoundationContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(PlayerGuardianFoundationState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest? request;
        try
        {
            using var doc = JsonDocument.Parse(json);
            ValidatePendingPlayerGuardianFoundationStateFile(doc.RootElement, PlayerGuardianFoundationState.PendingRequestPath, issues);
            request = JsonSerializer.Deserialize<PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest>(json);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue(
                PlayerGuardianFoundationState.PendingRequestPath,
                IssueSeverity.Error,
                $"pending_player_guardian_foundation.json не читается как валидный JSON: {ex.Message}",
                code: "player_guardian_foundation_invalid_json",
                section: "PlayerGuardianFoundation",
                repairHint: "Сохраняй pending_player_guardian_foundation.json как корректный client-authored JSON contract."));
            return;
        }

        if (request == null)
            return;

        await ValidatePendingPlayerGuardianFoundationRealmContextAsync(
            PlayerGuardianFoundationState.PendingRequestPath,
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

    private async Task ValidatePendingPlayerGuardianFoundationResolutionAsync(List<ValidationIssue> issues)
    {
        var requestResolution = await ReadValidatedPendingResolutionContractAsync<PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest>(
            PlayerGuardianFoundationState.PendingRequestPath,
            issues,
            missingCode: "player_guardian_foundation_missing_validated_snapshot_request",
            invalidCode: "player_guardian_foundation_invalid_validated_snapshot_request",
            section: "PlayerGuardianFoundation",
            missingMessage: "Strict validated pending turn snapshot contract для pending_player_guardian_foundation.json недоступен. Foundation branch нельзя проверить строго.",
            invalidMessage: "validated pending turn snapshot для pending_player_guardian_foundation.json существует, но request contract внутри него unreadable или malformed. Foundation branch нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_player_guardian_foundation.json в manifest.Files и snapshotFileHashes, а snapshot copy должна оставаться валидным JSON contract.",
            semanticValidator: (root, contextPrefix, validationIssues) => ValidatePendingPlayerGuardianFoundationStateFile(root, contextPrefix, validationIssues));
        if (requestResolution.Status != PendingResolutionContractStatus.Resolved || requestResolution.Contract == null)
            return;

        await ValidatePendingPlayerGuardianFoundationRealmContextAsync(PlayerGuardianFoundationState.PendingRequestPath, issues);
        if (issues.Any(issue => string.Equals(issue.Section, "PlayerGuardianFoundation", StringComparison.OrdinalIgnoreCase)))
            return;

        var guardianPolicyContext = await ResolveGuardianPolicyContextAsync();
        if (!guardianPolicyContext.CurrentStateReadable || !guardianPolicyContext.HasCurrentRoot)
        {
            issues.Add(new ValidationIssue(
                PlayerGuardianFoundationState.PendingRequestPath,
                IssueSeverity.Error,
                "Foundation branch не может быть строго проверена: current guardians.json unreadable.",
                code: "player_guardian_foundation_missing_guardian_resolution",
                section: "PlayerGuardianFoundation",
                expected: "readable current guardians.json with authority-backed new guardian",
                actual: "current guardians.json unreadable",
                repairHint: "Сделай current guardians.json читаемым и materialize-ь нового guardian actor через UpdateGuardians.create."));
            return;
        }

        try
        {
            if (!TryBuildPlayerFoundedGuardianResolution(guardianPolicyContext, requestResolution.Contract, out var createdGuardian, out var materializedGuardian, out var createdGuardianId, out var actual))
            {
                issues.Add(new ValidationIssue(
                    PlayerGuardianFoundationState.PendingRequestPath,
                    IssueSeverity.Error,
                    "pending_player_guardian_foundation из pre-turn snapshot не привёл к authority-backed materialization нового guardian actor.",
                    code: "player_guardian_foundation_missing_guardian_resolution",
                    section: "PlayerGuardianFoundation",
                    expected: "authority-backed player-founded guardian with foundationRequestId matching requestId",
                    actual: actual,
                    repairHint: "Разрешай foundation branch через UpdateGuardians.create и synchronously materialize нового guardian actor в guardians[]."));
                return;
            }

            var currentRoot = guardianPolicyContext.CurrentRoot;
            var historyEntry = PlayerGuardianFoundationState.FindHistoryEntry(TryParseJsonObject(currentRoot), requestResolution.Contract.RequestId);
            if (historyEntry == null ||
                !string.Equals(GetNodeString(historyEntry["guardianId"]), createdGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    PlayerGuardianFoundationState.PendingRequestPath,
                    IssueSeverity.Error,
                    "Foundation request не был закрыт canonical foundation history receipt.",
                    code: "player_guardian_foundation_missing_history",
                    section: "PlayerGuardianFoundation",
                    repairHint: $"После materialization нового guardian actor обязательно append-ь matching receipt в guardians.json.{PlayerGuardianFoundationState.HistoryProperty}."));
            }
            else
            {
                var formerPatronGuardianId = GetNodeString(historyEntry["formerPatronGuardianId"]);
                if (!string.Equals(formerPatronGuardianId, requestResolution.Contract.PreviousGuardianId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        $"{PlayerGuardianFoundationState.PendingRequestPath}.previousGuardianId",
                        IssueSeverity.Error,
                        "Foundation history receipt расходится с requested previous guardian identity.",
                        code: "player_guardian_foundation_previous_guardian_mismatch",
                        section: "PlayerGuardianFoundation",
                        expected: requestResolution.Contract.PreviousGuardianId,
                        actual: formerPatronGuardianId ?? "missing",
                        repairHint: "Сохрани в foundation receipt того же previousGuardianId, который был зафиксирован клиентом в ritual request."));
                }
            }

            if (!TryGetCurrentMaterializedGuardian(guardianPolicyContext, requestResolution.Contract.PreviousGuardianId, out _))
            {
                issues.Add(new ValidationIssue(
                    PlayerGuardianFoundationState.PendingRequestPath,
                    IssueSeverity.Error,
                    "Прежний guardian пропал из guardians[] после foundation branch.",
                    code: "player_guardian_foundation_previous_guardian_missing",
                    section: "PlayerGuardianFoundation",
                    expected: $"current guardians[] still contains {requestResolution.Contract.PreviousGuardianId}",
                    actual: $"guardian {requestResolution.Contract.PreviousGuardianId} missing from current guardians[]",
                    repairHint: "Не удаляй старого patron guardian. Новый guardian должен добавляться рядом, а не заменять прежнюю сущность."));
            }
            else if (TryGetCurrentMaterializedGuardian(guardianPolicyContext, requestResolution.Contract.PreviousGuardianId, out var previousGuardian) &&
                     !string.Equals(
                         PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(previousGuardian),
                         PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                         StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"game_state/meta/guardians.json.guardians.{requestResolution.Contract.PreviousGuardianId}.relationshipData.{PlayerGuardianFoundationState.GuardianRoleToPlayerProperty}",
                    IssueSeverity.Error,
                    "Foundation resolution должна явно оставить прежнему activeGuardian роль former_patron.",
                    code: "player_guardian_foundation_missing_former_patron_role",
                    section: "PlayerGuardianFoundation",
                    expected: PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                    actual: PlayerGuardianFoundationState.TryReadGuardianRoleToPlayer(previousGuardian) ?? "missing",
                    repairHint: $"При foundation resolution записывай relationshipData.{PlayerGuardianFoundationState.GuardianRoleToPlayerProperty} = {PlayerGuardianFoundationState.GuardianRoleFormerPatron} для прежнего patron guardian."));
            }

            if (!guardianPolicyContext.CurrentRoot.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object ||
                !string.Equals(GetFirstNonEmptyString(activeGuardian, "guardianId", "id"), createdGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.activeGuardian",
                    IssueSeverity.Error,
                    "Player-founded guardian должен стать activeGuardian по умолчанию на turn of foundation resolution.",
                    code: "player_guardian_foundation_active_guardian_mismatch",
                    section: "PlayerGuardianFoundation",
                    expected: createdGuardianId,
                    actual: guardianPolicyContext.CurrentRoot.TryGetProperty("activeGuardian", out var rawActiveGuardian)
                        ? GetFirstNonEmptyString(rawActiveGuardian, "guardianId", "id") ?? "missing/empty"
                        : "missing",
                    repairHint: "После accepted foundation branch синхронно обновляй activeGuardian на нового player-founded guardian."));
            }

            var expectedAbodeId = materializedGuardian["abode"] is JsonObject abodeNode
                ? GetNodeString(abodeNode["abodeId"])
                : null;
            var currentAbodeId = guardianPolicyContext.CurrentRoot.TryGetProperty("chaosSeaNavigation", out var navigation) &&
                                 navigation.ValueKind == JsonValueKind.Object
                ? GetFirstNonEmptyString(navigation, "currentAbodeId")
                : null;
            if (string.IsNullOrWhiteSpace(expectedAbodeId) ||
                !string.Equals(currentAbodeId, expectedAbodeId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/guardians.json.chaosSeaNavigation.currentAbodeId",
                    IssueSeverity.Error,
                    "Foundation resolution должен привязать currentAbodeId к обители нового guardian.",
                    code: "player_guardian_foundation_current_abode_mismatch",
                    section: "PlayerGuardianFoundation",
                    expected: expectedAbodeId ?? "new founded guardian abodeId",
                    actual: currentAbodeId ?? "missing",
                    repairHint: "После foundation branch синхронно обновляй chaosSeaNavigation.currentAbodeId на abodeId новой guardian mantle."));
            }

            var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(currentSoulJson))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Foundation branch не может быть строго проверена: current soul_state.json unreadable.",
                    code: "player_guardian_foundation_missing_soul_link",
                    section: "PlayerGuardianFoundation",
                    expected: $"readable current soul_state.json with {PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                    actual: "current soul_state.json missing or unreadable",
                    repairHint: $"После foundation branch записывай soul_state.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}."));
                return;
            }

            using var soulDoc = JsonDocument.Parse(currentSoulJson);
            var linkedGuardianId = GetFirstNonEmptyString(soulDoc.RootElement, PlayerGuardianFoundationState.SoulStateGuardianIdProperty);
            if (!string.Equals(linkedGuardianId, createdGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty}",
                    IssueSeverity.Error,
                    "Foundation branch должен оставить additive soul link на player-founded guardian.",
                    code: "player_guardian_foundation_missing_soul_link",
                    section: "PlayerGuardianFoundation",
                    expected: createdGuardianId,
                    actual: linkedGuardianId ?? "missing",
                    repairHint: $"Запиши soul_state.{PlayerGuardianFoundationState.SoulStateGuardianIdProperty} = {createdGuardianId} при успешном foundation resolution."));
            }

            var foundationStatus = GetFirstNonEmptyString(soulDoc.RootElement, PlayerGuardianFoundationState.SoulStateFoundationStatusProperty);
            if (!string.Equals(foundationStatus, PlayerGuardianFoundationState.SoulStateFoundationStatusFounded, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"game_state/meta/soul_state.json.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty}",
                    IssueSeverity.Error,
                    "Foundation resolution должна оставлять soul-side completion marker founded.",
                    code: "player_guardian_foundation_missing_soul_status",
                    section: "PlayerGuardianFoundation",
                    expected: PlayerGuardianFoundationState.SoulStateFoundationStatusFounded,
                    actual: foundationStatus ?? "missing",
                    repairHint: $"После successful foundation resolution записывай soul_state.{PlayerGuardianFoundationState.SoulStateFoundationStatusProperty} = {PlayerGuardianFoundationState.SoulStateFoundationStatusFounded}."));
            }
        }
        catch
        {
            // detailed parse issues reported elsewhere
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

        var hasManifestRegistrationEvidence = rawManifest != null && HasPendingResolutionValidatedSnapshotRegistration(rawManifest, relativePath);
        var hasDeletedCurrentRequestRecoveryBridgeCandidate =
            !hasCurrentFile &&
            _fs.FileExists("ready/turn_complete.json") &&
            _fs.FileExists(_fs.ResolvePath($"game_state/control/pending_turn_snapshot/{relativePath}"));
        var hasPreTurnContractEvidence = hasCurrentFile ||
                                         hasManifestRegistrationEvidence ||
                                         hasDeletedCurrentRequestRecoveryBridgeCandidate;
        if (!hasPreTurnContractEvidence)
            return new PendingResolutionContractReadResult<TContract>(PendingResolutionContractStatus.NoPreTurnContract, null);

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var hasValidatedSnapshotRegistration = lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable &&
                                              lookup.Manifest != null &&
                                              HasPendingResolutionValidatedSnapshotRegistration(lookup.Manifest, relativePath);
        var hasDeletedCurrentRequestRecoveryBridge = hasDeletedCurrentRequestRecoveryBridgeCandidate &&
                                                     (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
                                                      lookup.Manifest == null ||
                                                      !hasValidatedSnapshotRegistration);
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
                actual: (hasValidatedSnapshotRegistration || hasDeletedCurrentRequestRecoveryBridge)
                    ? "validated snapshot contract or deleted-current recovery bridge exists, but snapshot entry/file is missing or unreadable"
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
               manifest.RollbackBaselineFiles.Contains(relativePath);
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

    private async Task ValidatePendingPlayerGuardianFoundationRealmContextAsync(
        string requestPath,
        List<ValidationIssue> issues)
    {
        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            requestPath,
            issues,
            code: "player_guardian_foundation_invalid_validated_snapshot_context",
            section: "PlayerGuardianFoundation");
        if (currentRealm != null && !IsExactChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                "pending_player_guardian_foundation.json допустим только в Chaos Sea realm",
                code: "player_guardian_foundation_wrong_realm",
                section: "PlayerGuardianFoundation",
                expected: "Chaos Sea pre-turn realm",
                actual: currentRealm,
                repairHint: "Не создавай и не разрешай pending_player_guardian_foundation.json вне Моря Хаоса."));
        }
    }

    private async Task ValidatePendingResidentCompanionManifestationRealmContextAsync(List<ValidationIssue> issues)
    {
        if (!_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath))
            return;

        var currentRealm = await TryResolveCurrentRealmAsync();
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return;

        issues.Add(new ValidationIssue(
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            IssueSeverity.Error,
            "pending_resident_companion_manifestation_request.json является MortalWorldProfile-only contract и не может быть live pending file в afterlife realm.",
            code: "pending_resident_companion_manifestation_afterlife_forbidden",
            section: "AfterlifeResidents",
            expected: "file absent while currentRealm is Chaos Sea or Shining Abode",
            actual: currentRealm ?? "unknown current realm",
            repairHint: "В afterlife не создавай и не обрабатывай pending_resident_companion_manifestation_request.json; оставь файл только для следующей смертной жизни или убери stale contract через repair."));
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
            var preTurnRelicProof = ReadSoulRelicProofEntry(preTurnSoulJson, request.RelicId, strictCurrentShape: false);
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

            var currentRelicProof = ReadSoulRelicProofEntry(currentSoulJson, request.RelicId, strictCurrentShape: true);
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

        if (RealmSemantics.IsAfterlifeRealm(await TryResolvePreTurnRealmAsync()))
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

        var effectiveNpcRoot = NpcTradeRequestState.CreateReceiptAppliedValidationView(npcRoot);
        if (effectiveNpcRoot == null)
            return;

        foreach (var request in requests)
        {
            var npc = FindNpcTradeValidationEntry(effectiveNpcRoot, request.NpcId);
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
        if (await GuardianAbodeResidentRequestState.IsInteractionRequestFileMalformedAsync(_fs))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_resident_interactions.json unreadable, malformed или содержит malformed request entry.",
                code: "abode_resident_interactions_malformed_file",
                section: "AfterlifeResidents",
                repairHint: "Сохраняй pending resident interaction bundle как корректный JSON contract без malformed sibling entries; validator не должен терять corruption через partial read." ));
            return;
        }

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
        if (await GuardianAbodeResidentRequestState.IsResidentsRequestFileMalformedAsync(_fs))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_residents_request.json unreadable, malformed или содержит malformed request entry.",
                code: "abode_resident_roster_malformed_file",
                section: "AfterlifeResidents",
                repairHint: "Сохраняй pending resident roster bundle как корректный JSON contract без malformed sibling entries; validation не должна видеть усечённый surviving-набор вместо полной corruption state." ));
            return;
        }

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

        var guardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
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
                continue;
            }

            if (string.Equals(request.RequestMode, GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction, StringComparison.OrdinalIgnoreCase))
            {
                var foundedGuardian = PlayerGuardianFoundationState.FindGuardianById(guardiansRoot, request.GuardianId);
                if (!PlayerGuardianFoundationState.IsPlayerFoundedGuardian(foundedGuardian))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                        IssueSeverity.Error,
                        "founder_attraction roster request допустим только для founded guardian",
                        code: "abode_resident_roster_founder_mode_without_founded_guardian",
                        section: "AfterlifeResidents",
                        repairHint: "Используй founder_attraction только для guardian с originType=player_founded_ascended_soul."));
                }

                if (string.IsNullOrWhiteSpace(request.FounderFeatureTitle) ||
                    string.IsNullOrWhiteSpace(request.FounderFeatureSummary))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
                        IssueSeverity.Error,
                        "founder_attraction roster request должен нести founder feature title/summary",
                        code: "abode_resident_roster_missing_founder_feature_context",
                        section: "AfterlifeResidents",
                        repairHint: "Для founder_attraction request сохраняй founderFeatureTitle и founderFeatureSummary из founded guardian abode features."));
                }
            }
        }
    }

    private void ValidatePendingPlayerGuardianFoundationStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var requestId = RequireString(root, contextPrefix, issues, "requestId");
        var mode = RequireString(root, contextPrefix, issues, "mode");
        var founderSoulName = RequireString(root, contextPrefix, issues, "founderSoulName");
        var previousGuardianId = RequireString(root, contextPrefix, issues, "previousGuardianId");
        var previousGuardianName = RequireString(root, contextPrefix, issues, "previousGuardianName");
        var sourceShiningAvailability = RequireString(root, contextPrefix, issues, "sourceShiningAvailability");
        var proposedDisplayName = RequireString(root, contextPrefix, issues, "proposedDisplayName");
        var mantleSummary = RequireString(root, contextPrefix, issues, "mantleSummary");
        var mantleCreed = RequireString(root, contextPrefix, issues, "mantleCreed");
        ValidateOptionalNullableStringField(root, contextPrefix, issues, "dominantAspect");
        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "createdAtTurn", "PlayerGuardianFoundation");
        var createdAtUtc = RequireString(root, contextPrefix, issues, "createdAtUtc");

        if (!string.IsNullOrWhiteSpace(mode) &&
            !string.Equals(mode, PlayerGuardianFoundationState.RequestMode, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.mode",
                IssueSeverity.Error,
                "pending_player_guardian_foundation.json.mode должен быть player_founded_guardian",
                code: "player_guardian_foundation_invalid_mode",
                section: "PlayerGuardianFoundation",
                expected: PlayerGuardianFoundationState.RequestMode,
                actual: mode));
        }

        if (!string.IsNullOrWhiteSpace(sourceShiningAvailability) &&
            !string.Equals(sourceShiningAvailability, ShiningAbodeState.AvailabilitySealedUntilNextAscension, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.sourceShiningAvailability",
                IssueSeverity.Error,
                "sourceShiningAvailability должен фиксировать sealed_until_next_ascension",
                code: "player_guardian_foundation_invalid_shining_availability",
                section: "PlayerGuardianFoundation",
                expected: ShiningAbodeState.AvailabilitySealedUntilNextAscension,
                actual: sourceShiningAvailability));
        }

        if (!string.IsNullOrWhiteSpace(createdAtUtc) && !DateTimeOffset.TryParse(createdAtUtc, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.createdAtUtc",
                IssueSeverity.Error,
                "pending_player_guardian_foundation.json.createdAtUtc должен быть ISO 8601 timestamp",
                code: "player_guardian_foundation_invalid_created_at",
                section: "PlayerGuardianFoundation",
                expected: "ISO 8601 timestamp",
                actual: createdAtUtc));
        }

        if (!root.TryGetProperty("appearanceMotifs", out var appearanceMotifs) ||
            appearanceMotifs.ValueKind != JsonValueKind.Array ||
            appearanceMotifs.GetArrayLength() == 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.appearanceMotifs",
                IssueSeverity.Error,
                "pending_player_guardian_foundation.json должен содержать непустой appearanceMotifs array",
                code: "player_guardian_foundation_missing_appearance_motifs",
                section: "PlayerGuardianFoundation",
                expected: "non-empty array of strings",
                actual: root.TryGetProperty("appearanceMotifs", out var actualMotifs) ? actualMotifs.ValueKind.ToString() : "missing"));
        }
        else
        {
            var motifIndex = 0;
            foreach (var motif in appearanceMotifs.EnumerateArray())
            {
                if (motif.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(motif.GetString()))
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.appearanceMotifs[{motifIndex}]",
                        IssueSeverity.Error,
                        "appearanceMotifs должен содержать только непустые строки",
                        code: "player_guardian_foundation_invalid_appearance_motif",
                        section: "PlayerGuardianFoundation"));
                }

                motifIndex++;
            }
        }

        if (string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(founderSoulName) ||
            string.IsNullOrWhiteSpace(previousGuardianId) ||
            string.IsNullOrWhiteSpace(previousGuardianName) ||
            string.IsNullOrWhiteSpace(proposedDisplayName) ||
            string.IsNullOrWhiteSpace(mantleSummary) ||
            string.IsNullOrWhiteSpace(mantleCreed))
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_player_guardian_foundation.json должен содержать полный client-authored ritual contract",
                code: "player_guardian_foundation_missing_fields",
                section: "PlayerGuardianFoundation"));
        }
    }

    private static bool TryBuildPlayerFoundedGuardianResolution(
        GuardianPolicyContext context,
        PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest request,
        out JsonObject authorityGuardian,
        out JsonObject materializedGuardian,
        out string guardianId,
        out string actual)
    {
        authorityGuardian = null!;
        materializedGuardian = null!;
        guardianId = string.Empty;
        actual = "guardian authority unavailable";

        var hasStrictCurrentAuthorityRoot =
            context.HasStrictCurrentAuthorityRoot &&
            context.StrictCurrentAuthorityRoot.ValueKind == JsonValueKind.Object;
        var currentAuthorityRoot = hasStrictCurrentAuthorityRoot
            ? context.StrictCurrentAuthorityRoot
            : context.CurrentAuthorityRoot;
        if (currentAuthorityRoot.ValueKind != JsonValueKind.Object)
        {
            actual = DescribeCurrentGuardianAuthorityFailure(context);
            return false;
        }

        var authorityMatches = new List<JsonObject>();
        if (currentAuthorityRoot.TryGetProperty("guardians", out var authorityGuardians) &&
            authorityGuardians.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in authorityGuardians.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !string.Equals(GetFirstNonEmptyString(entry, "foundationRequestId"), request.RequestId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (JsonNode.Parse(entry.GetRawText()) is JsonObject parsed)
                    authorityMatches.Add(parsed);
            }
        }

        if (authorityMatches.Count != 1)
        {
            actual = authorityMatches.Count == 0
                ? "no authority-backed guardian carries foundationRequestId"
                : "multiple authority-backed guardians carry the same foundationRequestId";
            return false;
        }

        authorityGuardian = authorityMatches[0];
        guardianId = GetNodeString(authorityGuardian["guardianId"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(guardianId))
        {
            actual = "authority-backed founded guardian is missing guardianId";
            return false;
        }

        if (!TryGetCurrentMaterializedGuardian(context, guardianId, out var materializedGuardianElement) ||
            JsonNode.Parse(materializedGuardianElement.GetRawText()) is not JsonObject parsedMaterializedGuardian)
        {
            actual = $"guardian {guardianId} missing from current guardians[]";
            return false;
        }

        AbodePowerRules.EnsureCanonicalState(parsedMaterializedGuardian);
        GuardianGachaChargeRules.NormalizeGuardianGachaState(parsedMaterializedGuardian);
        GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(parsedMaterializedGuardian);
        materializedGuardian = parsedMaterializedGuardian;
        if (TryBuildAuthorizedFoundationCreateGuardian(context, guardianId, out var authorizedCreateGuardian) &&
            !JsonNode.DeepEquals(authorizedCreateGuardian, materializedGuardian))
        {
            actual = $"guardian {guardianId} materialized state diverges from authority-backed create surface";
            return false;
        }

        if (!PlayerGuardianFoundationState.TryDescribeFoundedGuardianContractMismatch(materializedGuardian, request, out actual))
        {
            actual = $"guardian {guardianId} {actual}";
            return false;
        }

        return true;
    }

    private static bool TryBuildAuthorizedFoundationCreateGuardian(
        GuardianPolicyContext context,
        string guardianId,
        out JsonObject guardian)
    {
        guardian = null!;
        if (string.IsNullOrWhiteSpace(guardianId) ||
            !context.AuthorizedSameTurnCreateGuardiansById.TryGetValue(guardianId, out var createGuardianElement) ||
            createGuardianElement.ValueKind != JsonValueKind.Object ||
            JsonNode.Parse(createGuardianElement.GetRawText()) is not JsonObject parsedGuardian)
        {
            return false;
        }

        AbodePowerRules.EnsureCanonicalState(parsedGuardian);
        GuardianGachaChargeRules.NormalizeGuardianGachaState(parsedGuardian);
        GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(parsedGuardian);
        guardian = parsedGuardian;
        return true;
    }

    private async Task ValidatePendingGuardianAbodeResidentTransferRequestContextAsync(List<ValidationIssue> issues)
    {
        if (await GuardianAbodeResidentRequestState.IsTransferRequestFileMalformedAsync(_fs))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_resident_transfers.json unreadable, malformed или содержит malformed request entry.",
                code: "abode_resident_transfer_malformed_file",
                section: "AfterlifeResidents",
                repairHint: "Сохраняй pending resident transfer bundle как корректный JSON contract без malformed sibling entries; validator не должен silently drop corrupted requests." ));
            return;
        }

        var requests = await GuardianAbodeResidentRequestState.ReadTransferRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        var currentRealm = await ResolveGuardianValidatedPreTurnRealmForContextAsync(
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            issues,
            code: "abode_resident_transfer_invalid_validated_snapshot_context",
            section: "AfterlifeResidents");
        if (currentRealm != null && !IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                IssueSeverity.Error,
                "pending_guardian_abode_resident_transfers.json допустим только в afterlife realm",
                code: "abode_resident_transfer_wrong_realm",
                section: "AfterlifeResidents"));
        }

        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return;

        var preTurnResidentsJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            issues,
            code: "abode_resident_transfer_missing_validated_snapshot_roster",
            section: "AfterlifeResidents",
            message: "pending_guardian_abode_resident_transfers.json существует, но validated pending turn snapshot не содержит guardian_abode_residents.json. Нельзя строго доказать, что resident уже достиг ready_to_transfer.",
            repairHint: "При создании pending turn snapshot сохраняй guardian_abode_residents.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnResidentsJson))
            return;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject residentsRoot)
                return;

            if (JsonNode.Parse(preTurnResidentsJson) is not JsonObject preTurnResidentsRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(residentsRoot);
            GuardianAbodeResidentState.NormalizeShape(preTurnResidentsRoot);
            var receipts = GuardianAbodeResidentState.EnsureTransferReceiptsArray(residentsRoot);
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) ||
                    string.IsNullOrWhiteSpace(request.ResidentId) ||
                    string.IsNullOrWhiteSpace(request.SourceGuardianId) ||
                    string.IsNullOrWhiteSpace(request.SourceAbodeId) ||
                    !GuardianAbodeResidentState.IsSupportedTransferMode(request.TransferMode))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "pending_guardian_abode_resident_transfers.json должен содержать полный client-authored transfer contract",
                        code: "abode_resident_transfer_missing_fields",
                        section: "AfterlifeResidents"));
                    continue;
                }

                if (!TryResolveEligiblePreTurnTransferResident(preTurnResidentsRoot, request, out _, out var preTurnEligibilityActual))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "Resident transfer request не подтверждается validated pre-turn source resident state.",
                        code: "abode_resident_transfer_invalid_preturn_eligibility",
                        section: "AfterlifeResidents",
                        expected: "resident present in source abode with migrationState=ready_to_transfer",
                        actual: preTurnEligibilityActual,
                        repairHint: "Создавай resident transfer request только для validated pre-turn resident из source Обители, который уже находится в состоянии ready_to_transfer."));
                    continue;
                }

                if (GuardianAbodeResidentState.FindResident(residentsRoot, request.ResidentId) == null &&
                    GuardianAbodeResidentState.FindTransferReceipt(receipts, request.RequestId) == null)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "pending resident transfer request должен ссылаться либо на существующего resident, либо на уже записанный transfer receipt",
                        code: "abode_resident_transfer_missing_resident_or_receipt",
                        section: "AfterlifeResidents",
                        repairHint: "Не держи pending resident transfer request без resident roster и без matching transfer receipt."));
                }
            }
        }
        catch
        {
            // parse issues reported elsewhere
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

    private async Task ValidatePendingGuardianAbodeResidentTransferResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            issues,
            code: "abode_resident_transfer_missing_validated_snapshot_request",
            section: "AfterlifeResidents",
            message: "pending_guardian_abode_resident_transfers.json существует в accepted turn, но отсутствует в validated pending turn snapshot. Resident transfer contract нельзя проверить строго.",
            repairHint: "При создании pending turn snapshot сохраняй pending_guardian_abode_resident_transfers.json в manifest.Files и snapshotFileHashes.");
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var requests = ReadPendingGuardianAbodeResidentTransferRequests(preTurnJson);
        if (requests.Count == 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(preTurnJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(GuardianAbodeResidentRequestState.TransferRequestsProperty, out var requestsNode) &&
                    requestsNode.ValueKind == JsonValueKind.Array &&
                    requestsNode.GetArrayLength() == 0)
                {
                    return;
                }
            }
            catch
            {
                // explicit fail-closed issue below
            }

            issues.Add(new ValidationIssue(
                GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_guardian_abode_resident_transfers.json unreadable или malformed.",
                code: "abode_resident_transfer_malformed_validated_snapshot_request",
                section: "AfterlifeResidents",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_guardian_abode_resident_transfers.json exact client-authored contract."));
            return;
        }

        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return;

        try
        {
            if (JsonNode.Parse(residentsJson) is not JsonObject currentResidentsRoot)
                return;

            GuardianAbodeResidentState.NormalizeShape(currentResidentsRoot);
            var receipts = GuardianAbodeResidentState.EnsureTransferReceiptsArray(currentResidentsRoot);
            var historyLog = GuardianAbodeResidentState.EnsureHistoryLogArray(currentResidentsRoot);

            JsonObject? preTurnResidentsRoot = null;
            var preTurnResidentsJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeResidentState.StatePath);
            if (!string.IsNullOrWhiteSpace(preTurnResidentsJson))
            {
                preTurnResidentsRoot = JsonNode.Parse(preTurnResidentsJson) as JsonObject;
                if (preTurnResidentsRoot != null)
                    GuardianAbodeResidentState.NormalizeShape(preTurnResidentsRoot);
            }

            var guardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
            var currentGuardianPowerById = GuardianAbodeResidentState.CollectGuardianAbodePowerById(guardiansRoot);

            foreach (var request in requests)
            {
                if (!TryResolveEligiblePreTurnTransferResident(preTurnResidentsRoot, request, out var preTurnResident, out var preTurnEligibilityActual))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "Validated pending resident transfer request не подтверждается pre-turn resident eligibility.",
                        code: "abode_resident_transfer_invalid_preturn_eligibility",
                        section: "AfterlifeResidents",
                        expected: "resident present in source abode with migrationState=ready_to_transfer",
                        actual: preTurnEligibilityActual,
                        repairHint: "Сохраняй в pending resident transfer request только resident, который в validated pre-turn roster ещё находится в source Обители и уже достиг ready_to_transfer."));
                    continue;
                }

                var receipt = GuardianAbodeResidentState.FindTransferReceipt(receipts, request.RequestId);
                if (receipt == null)
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "pending resident transfer request из pre-turn snapshot не был закрыт в текущем accepted turn",
                        code: "abode_resident_transfer_missing_resolution",
                        section: "AfterlifeResidents",
                        repairHint: "Каждый resident transfer request должен закрываться в ближайшем accepted turn через guardian_abode_residents.json.transferReceipts[]."));
                    continue;
                }

                if (!TransferReceiptMatchesMode(receipt, request))
                {
                    issues.Add(new ValidationIssue(
                        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                        IssueSeverity.Error,
                        "Resident transfer receipt использует status/transferMode, несовместимый с исходным request contract.",
                        code: "abode_resident_transfer_receipt_mode_mismatch",
                        section: "AfterlifeResidents",
                        expected: request.TransferMode,
                        actual: $"{GetNodeString(receipt["status"])} / {GetNodeString(receipt["transferMode"])}",
                        repairHint: "Синхронизируй transfer receipt с canonical request mode: departure_only -> departed_only, accepted transfer -> accepted, refused transfer -> refused."));
                    continue;
                }

                var status = GetNodeString(receipt["status"]);
                var departureHistoryEntryId = GetNodeString(receipt["departureHistoryEntryId"]);
                var arrivalHistoryEntryId = GetNodeString(receipt["arrivalHistoryEntryId"]);
                var currentResidents = GuardianAbodeResidentState.EnsureEntriesArray(currentResidentsRoot).OfType<JsonObject>().ToList();
                var sourceResidentPresent = currentResidents.Any(resident =>
                    string.Equals(GetNodeString(resident["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(resident["guardianId"]), request.SourceGuardianId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(resident["abodeId"]), request.SourceAbodeId, StringComparison.OrdinalIgnoreCase) &&
                    ResidentIsPresent(resident));
                var targetResident = currentResidents.FirstOrDefault(resident =>
                    string.Equals(GetNodeString(resident["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(resident["guardianId"]), request.TargetGuardianId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(GetNodeString(resident["abodeId"]), request.TargetAbodeId, StringComparison.OrdinalIgnoreCase) &&
                    ResidentIsPresent(resident));

                if (string.Equals(status, GuardianAbodeResidentState.TransferStatusAccepted, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(departureHistoryEntryId) ||
                        string.IsNullOrWhiteSpace(arrivalHistoryEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, departureHistoryEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, arrivalHistoryEntryId) ||
                        sourceResidentPresent ||
                        !ResidentTransferArrivalMatches(targetResident, preTurnResident, request, currentGuardianPowerById))
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                            IssueSeverity.Error,
                            "Accepted resident transfer не привёл к canonical departure/arrival resolution.",
                            code: "abode_resident_transfer_invalid_accepted_resolution",
                            section: "AfterlifeResidents",
                            expected: "source departure + target arrival + matching history entries",
                            actual: BuildResidentTransferStateSummary(sourceResidentPresent, targetResident, departureHistoryEntryId, arrivalHistoryEntryId),
                            repairHint: "Для accepted transfer убери resident из source Обители, materialize-ь того же residentId в target Обители с canonical arrival state и запиши обе history references в transfer receipt."));
                    }

                    continue;
                }

                if (string.Equals(status, GuardianAbodeResidentState.TransferStatusRefused, StringComparison.OrdinalIgnoreCase))
                {
                    var sourceResident = currentResidents.FirstOrDefault(resident =>
                        string.Equals(GetNodeString(resident["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(GetNodeString(resident["guardianId"]), request.SourceGuardianId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(GetNodeString(resident["abodeId"]), request.SourceAbodeId, StringComparison.OrdinalIgnoreCase) &&
                        ResidentIsPresent(resident));
                    if (sourceResident == null ||
                        targetResident != null ||
                        string.IsNullOrWhiteSpace(departureHistoryEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, departureHistoryEntryId))
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                            IssueSeverity.Error,
                            "Refused resident transfer не привёл к canonical refusal resolution.",
                            code: "abode_resident_transfer_invalid_refused_resolution",
                            section: "AfterlifeResidents",
                            expected: "resident remains in source abode with refusal history and no target arrival",
                            actual: BuildResidentTransferStateSummary(sourceResident != null, targetResident, departureHistoryEntryId, arrivalHistoryEntryId),
                            repairHint: "Для refused transfer сохрани resident в source Обители, не materialize-ь target arrival и запиши refusal history entry в transfer receipt."));
                    }

                    continue;
                }

                if (string.Equals(status, GuardianAbodeResidentState.TransferStatusDepartedOnly, StringComparison.OrdinalIgnoreCase))
                {
                    if (sourceResidentPresent ||
                        targetResident != null ||
                        string.IsNullOrWhiteSpace(departureHistoryEntryId) ||
                        !GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, departureHistoryEntryId))
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
                            IssueSeverity.Error,
                            "Departure-only resident transfer не привёл к canonical departure resolution.",
                            code: "abode_resident_transfer_invalid_departure_resolution",
                            section: "AfterlifeResidents",
                            expected: "resident removed from source abode with departure history and no target arrival",
                            actual: BuildResidentTransferStateSummary(sourceResidentPresent, targetResident, departureHistoryEntryId, arrivalHistoryEntryId),
                            repairHint: "Для departure_only убери resident из source Обители, не materialize-ь target arrival и запиши departure history entry в transfer receipt."));
                    }
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
        var requestState = await ActorSocialInteractionRequestState.ReadGuardianRequestsStateAsync(_fs);
        if (requestState.IsMalformed)
        {
            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingGuardianRequestPath,
                IssueSeverity.Error,
                "pending_guardian_social_interactions.json unreadable or malformed in current runtime state",
                code: "guardian_social_interactions_malformed_runtime_state",
                section: "GuardianSocial",
                repairHint: "Сохраняй machine-readable pending_guardian_social_interactions.json и не очищай malformed guardian social contract до явной repair/validation обработки."));
            return;
        }

        var requests = requestState.Requests;
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
        var requestState = await ActorSocialInteractionRequestState.ReadNpcRequestsStateAsync(_fs);
        if (requestState.IsMalformed)
        {
            issues.Add(new ValidationIssue(
                ActorSocialInteractionRequestState.PendingNpcRequestPath,
                IssueSeverity.Error,
                "pending_npc_social_interactions.json unreadable or malformed in current runtime state",
                code: "npc_social_interactions_malformed_runtime_state",
                section: "NpcSocial",
                repairHint: "Сохраняй machine-readable pending_npc_social_interactions.json и не очищай malformed NPC social contract до явной repair/validation обработки."));
            return;
        }

        var requests = requestState.Requests;
        if (requests.Count == 0)
            return;

        if (RealmSemantics.IsAfterlifeRealm(await TryResolvePreTurnRealmAsync()))
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
        var preTurnGuardiansJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/guardians.json");
        var currentGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        JsonObject? preTurnGuardiansRoot = null;
        JsonObject? currentGuardiansRoot = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(preTurnGuardiansJson))
                preTurnGuardiansRoot = JsonNode.Parse(preTurnGuardiansJson) as JsonObject;
        }
        catch
        {
            // parse issues reported elsewhere
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(currentGuardiansJson))
                currentGuardiansRoot = JsonNode.Parse(currentGuardiansJson) as JsonObject;
        }
        catch
        {
            // parse issues reported elsewhere
        }

        var previousGuardianPowerById = GuardianAbodeResidentState.CollectGuardianAbodePowerById(preTurnGuardiansRoot);
        var currentGuardianPowerById = GuardianAbodeResidentState.CollectGuardianAbodePowerById(currentGuardiansRoot);
        var preTurnTransferRequestsJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        var preTurnTransferRequests = ReadPendingGuardianAbodeResidentTransferRequests(preTurnTransferRequestsJson);
        var acceptedTransferArrivalResidentIds = CollectAcceptedTransferArrivalResidentIds(
            currentResidentsRoot,
            preTurnResidentsRoot,
            preTurnTransferRequests,
            currentGuardianPowerById);

        foreach (var resident in GuardianAbodeResidentState.EnsureEntriesArray(currentResidentsRoot).OfType<JsonObject>())
        {
            var residentId = GetNodeString(resident["residentId"]);
            if (string.IsNullOrWhiteSpace(residentId))
                continue;

            var preTurnResident = preTurnResidentsRoot == null ? null : GuardianAbodeResidentState.FindResident(preTurnResidentsRoot, residentId);
            if (preTurnResident != null && !acceptedTransferArrivalResidentIds.Contains(residentId))
            {
                var previousDevotionLevel = GetNodeInt(preTurnResident["abodeDevotionLevel"]);
                var currentDevotionLevel = GetNodeInt(resident["abodeDevotionLevel"]);
                var previousRestlessness = GetNodeInt(preTurnResident["restlessness"]);
                var currentRestlessness = GetNodeInt(resident["restlessness"]);
                var previousMigrationState = GetNodeString(preTurnResident["migrationState"]);
                var currentMigrationState = GetNodeString(resident["migrationState"]);
                var hasAbodeShift =
                    previousDevotionLevel != currentDevotionLevel ||
                    previousRestlessness != currentRestlessness ||
                    !string.Equals(previousMigrationState, currentMigrationState, StringComparison.OrdinalIgnoreCase);

                if (hasAbodeShift)
                {
                    var driftContext = GuardianAbodeResidentState.BuildCanonicalDriftContext(
                        preTurnResidentsRoot,
                        currentResidentsRoot,
                        preTurnResident,
                        resident,
                        previousGuardianPowerById,
                        currentGuardianPowerById,
                        preTurnQuestFingerprintsByResident,
                        currentQuestFingerprintsByResident);
                    var projection = GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(preTurnResident, resident, driftContext);
                    if (!projection.HasCanonicalTrigger)
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentState.StatePath,
                            IssueSeverity.Error,
                            "Resident abode devotion/restlessness shift произошёл без canonical trigger.",
                            code: "abode_resident_devotion_shift_missing_canonical_trigger",
                            section: "AfterlifeResidents",
                            actual: $"{residentId}: {previousDevotionLevel}/{previousRestlessness}/{previousMigrationState} -> {currentDevotionLevel}/{currentRestlessness}/{currentMigrationState}",
                            repairHint: "Меняй abode devotion/restlessness только при canonical resident-facing trigger: power-tier shift, resident talk/history, quest progression, reward outcome или явная resident сцена."));
                    }
                    else if (currentDevotionLevel != projection.AbodeDevotionLevel ||
                             currentRestlessness != projection.Restlessness ||
                             !string.Equals(currentMigrationState, projection.MigrationState, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentState.StatePath,
                            IssueSeverity.Error,
                            "Resident abode devotion/restlessness shift не совпадает с canonical drift projection.",
                            code: "abode_resident_devotion_projection_mismatch",
                            section: "AfterlifeResidents",
                            expected: $"{projection.AbodeDevotionLevel}/{projection.Restlessness}/{projection.MigrationState} ({projection.TriggerSummary})",
                            actual: $"{currentDevotionLevel}/{currentRestlessness}/{currentMigrationState}",
                            repairHint: "Для resident abode drift используй canonical bounded step projection от abodeDisposition, bondLevel, abode power direction и resident-facing accepted outcome."));
                    }

                    var requiresCuratedMemory =
                        Math.Abs(currentDevotionLevel - previousDevotionLevel) >= 8 ||
                        Math.Abs(currentRestlessness - previousRestlessness) >= 8;
                    if (requiresCuratedMemory &&
                        !ResidentHasNewThoughtOrInteractionMemory(preTurnResidentsRoot, currentResidentsRoot, residentId))
                    {
                        issues.Add(new ValidationIssue(
                            GuardianAbodeResidentState.StatePath,
                            IssueSeverity.Error,
                            "Meaningful resident abode shift не оставил curated memory update.",
                            code: "abode_resident_devotion_shift_missing_memory_update",
                            section: "AfterlifeResidents",
                            repairHint: "Если resident devotion/restlessness заметно сместились, добавь residentThoughtJournalUpdates и/или residentInteractionLogUpdates с кратким объяснением этого сдвига."));
                    }
                }
            }

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
        var requestState = await AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs);
        if (!requestState.Exists)
            return;

        if (requestState.IsMalformed || requestState.Request == null)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                IssueSeverity.Error,
                "pending_archive_consultation_request.json не читается как валидный JSON contract.",
                code: "archive_consultation_request_malformed_file",
                section: "AfterlifeArchive",
                repairHint: "Сохраняй pending_archive_consultation_request.json как корректный client-authored JSON object."));
            return;
        }

        var request = requestState.Request;

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

        var soulRoot = await TryReadStrictCurrentArchiveOwnerState(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            "archive_consultation_request_missing_current_archive_authority",
            "AfterlifeArchive",
            "Исправь current game_state/meta/soul_state.json так, чтобы afterlifeArchive оставался canonical object со stored[] и actionReceipts[]; consultation request нельзя валидировать по malformed current archive owner state.",
            issues);
        if (soulRoot == null)
            return;

        var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
        if (!AfterlifeArchiveState.HasMatchingReservation(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindConsultation) &&
            !AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId, request.ArchiveId, request.RequestedMode))
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                IssueSeverity.Error,
                "pending_archive_consultation_request должен ссылаться либо на активную reservation, либо на уже записанный action receipt",
                code: "archive_consultation_request_missing_reservation",
                section: "AfterlifeArchive"));
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

        var requestState = AfterlifeArchiveActionState.ParseConsultationState(preTurnJson);
        if (requestState.IsMalformed || requestState.Request == null)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ConsultationRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_archive_consultation_request unreadable или malformed.",
                code: "archive_consultation_request_malformed_validated_snapshot_request",
                section: "AfterlifeArchive",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_archive_consultation_request.json exact client-authored contract."));
            return;
        }

        var request = requestState.Request;
        if (request == null)
            return;

        var soulRoot = await TryReadStrictCurrentArchiveOwnerState(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            "archive_consultation_request_missing_current_archive_authority",
            "AfterlifeArchive",
            "Исправь current game_state/meta/soul_state.json так, чтобы validator читал canonical afterlifeArchive owner state перед строгой archive consultation resolution.",
            issues);
        if (soulRoot == null)
            return;

        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
        var receipt = AfterlifeArchiveState.FindActionReceipt(receipts, request.RequestId, request.ArchiveId, request.RequestedMode);
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
                return;
            }

            if (!ArchiveConsultationReceiptHasMatchingCompletedProject(trackerRoot, request.RequestId, request.ArchiveId, request.GuardianId, receipt))
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

    private async Task ValidatePendingArchiveProjectFuelRequestContextAsync(List<ValidationIssue> issues)
    {
        var requestState = await AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs);
        if (!requestState.Exists)
            return;

        if (requestState.IsMalformed || requestState.Request == null)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                IssueSeverity.Error,
                "pending_archive_project_fuel_request.json не читается как валидный JSON contract.",
                code: "archive_project_fuel_request_malformed_file",
                section: "AfterlifeArchive",
                repairHint: "Сохраняй pending_archive_project_fuel_request.json как корректный client-authored JSON object."));
            return;
        }

        var request = requestState.Request;

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

        var soulRoot = await TryReadStrictCurrentArchiveOwnerState(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            "archive_project_fuel_request_missing_current_archive_authority",
            "AfterlifeArchive",
            "Исправь current game_state/meta/soul_state.json так, чтобы afterlifeArchive оставался canonical object со stored[] и actionReceipts[]; project fuel request нельзя валидировать по malformed current archive owner state.",
            issues);
        if (soulRoot == null)
            return;

        var stored = AfterlifeArchiveState.EnsureStoredArray(soulRoot);
        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
        if (!AfterlifeArchiveState.HasMatchingReservation(stored, request.ArchiveId, request.RequestId, AfterlifeArchiveState.ReservationKindProjectFuel) &&
            !AfterlifeArchiveState.HasActionReceipt(receipts, request.RequestId, request.ArchiveId, request.RequestedMode))
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                IssueSeverity.Error,
                "pending_archive_project_fuel_request должен ссылаться либо на активную reservation, либо на уже записанный action receipt",
                code: "archive_project_fuel_request_missing_reservation",
                section: "AfterlifeArchive"));
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

        var requestState = AfterlifeArchiveActionState.ParseProjectFuelState(preTurnJson);
        if (requestState.IsMalformed || requestState.Request == null)
        {
            issues.Add(new ValidationIssue(
                AfterlifeArchiveActionState.ProjectFuelRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_archive_project_fuel_request unreadable или malformed.",
                code: "archive_project_fuel_request_malformed_validated_snapshot_request",
                section: "AfterlifeArchive",
                repairHint: "Сохраняй в validated pending snapshot machine-readable pending_archive_project_fuel_request.json exact client-authored contract."));
            return;
        }

        var request = requestState.Request;
        if (request == null)
            return;

        var soulRoot = await TryReadStrictCurrentArchiveOwnerState(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            "archive_project_fuel_request_missing_current_archive_authority",
            "AfterlifeArchive",
            "Исправь current game_state/meta/soul_state.json так, чтобы validator читал canonical afterlifeArchive owner state перед строгой archive project fuel resolution.",
            issues);
        if (soulRoot == null)
            return;

        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(soulRoot);
        var receipt = AfterlifeArchiveState.FindActionReceipt(receipts, request.RequestId, request.ArchiveId, request.RequestedMode);
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
            if (!ArchiveProjectFuelReceiptHasMatchingJournalEntry(journalJson, request.RequestId, request.GuardianId, request.TargetProjectId))
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

    private async Task<JsonObject?> TryReadStrictCurrentArchiveOwnerState(
        string requestPath,
        string code,
        string section,
        string repairHint,
        List<ValidationIssue> issues)
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                "Archive request не может быть строго проверен: current soul_state.json unreadable.",
                code: code,
                section: section,
                expected: "readable current soul_state.json with canonical afterlifeArchive owner state",
                actual: "current soul_state.json missing or unreadable",
                repairHint: repairHint));
            return null;
        }

        try
        {
            if (JsonNode.Parse(soulJson) is not JsonObject soulRoot)
            {
                issues.Add(new ValidationIssue(
                    requestPath,
                    IssueSeverity.Error,
                    "Archive request не может быть строго проверен: current soul_state.json malformed.",
                    code: code,
                    section: section,
                    expected: "canonical soul_state object with current afterlifeArchive owner state",
                    actual: "current soul_state root is not a JSON object",
                    repairHint: repairHint));
                return null;
            }

            var failureDescription = string.Empty;
            if (soulRoot[AfterlifeArchiveState.ContainerProperty] is not JsonObject ||
                AfterlifeArchiveState.TryDescribeInvalidCanonicalArchiveRoot(soulRoot, out failureDescription))
            {
                issues.Add(new ValidationIssue(
                    requestPath,
                    IssueSeverity.Error,
                    "Archive request не может быть строго проверен: current afterlifeArchive authority unreadable или malformed.",
                    code: code,
                    section: section,
                    expected: "canonical current afterlifeArchive owner state",
                    actual: string.IsNullOrWhiteSpace(failureDescription)
                        ? "afterlifeArchive container missing or malformed"
                        : failureDescription,
                    repairHint: repairHint));
                return null;
            }

            return soulRoot;
        }
        catch
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                "Archive request не может быть строго проверен: current soul_state.json malformed.",
                code: code,
                section: section,
                expected: "canonical soul_state object with current afterlifeArchive owner state",
                actual: "current soul_state unreadable or malformed",
                repairHint: repairHint));
            return null;
        }
    }

    private async Task ValidateLifeTransitionContextAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        if (string.IsNullOrWhiteSpace(json))
            return;

        var preTurnRealm = await TryResolvePreTurnRealmAsync();
        var currentRealm = await TryResolveCurrentRealmAsync();
        var hasCanonicalTriggerLifeEnd = TryReadLifeTransitionControlFile(json) &&
                                         CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(
                                             json,
                                             preTurnRealm,
                                             currentRealm);
        var hasRecordLifeCompletion = await HasCurrentTurnRecordLifeCompletionAsync();

        if (!string.IsNullOrWhiteSpace(preTurnRealm) &&
            !IsChaosSeaRealm(preTurnRealm) &&
            IsChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/life_transitions.json",
                IssueSeverity.Error,
                "TriggerLifeEnd turn не может одновременно переключать currentRealm в afterlife.",
                code: "life_transition_current_realm_switched_same_turn",
                section: "Lifecycle",
                expected: "current realm remains Mortal World on TriggerLifeEnd turn",
                actual: currentRealm ?? "unknown current realm",
                repairHint: "Не переключай soul_state.currentRealm в Chaos Sea/Shining Abode на том же accepted turn, где TriggerLifeEnd только запускает lifecycle."));
        }

        if (!string.IsNullOrWhiteSpace(preTurnRealm) && IsChaosSeaRealm(preTurnRealm))
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

        if (hasRecordLifeCompletion && !hasCanonicalTriggerLifeEnd)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "recordLifeCompletion не может появляться без canonical TriggerLifeEnd contract.",
                code: "life_transition_record_without_trigger_life_end",
                section: "Lifecycle",
                expected: "canonical TriggerLifeEnd on a Mortal World turn before recordLifeCompletion",
                actual: "recordLifeCompletion present without authorized TriggerLifeEnd",
                repairHint: "Пиши metaStateUpdates.lifeTransitions.recordLifeCompletion только на accepted turn с валидным TriggerLifeEnd из Mortal World без same-turn realm switch."));
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
        var currentRealm = await TryResolveCurrentRealmAsync();
        if (string.IsNullOrWhiteSpace(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/ascension.json",
                IssueSeverity.Error,
                "AscensionTrigger требует validated pre-turn realm и не может fallback-иться к current soul_state.currentRealm.",
                code: "ascension_invalid_validated_snapshot_realm",
                section: "Lifecycle",
                expected: "validated pre-turn realm = Chaos Sea",
                actual: "validated pre-turn soul_state realm missing or unreadable",
                repairHint: "Сохраняй game_state/meta/soul_state.json в validated pending turn snapshot и проверяй Ascension только по pre-turn realm, а не по current realm."));
        }
        else if (!IsExactChaosSeaRealm(preTurnRealm))
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

        if (!string.IsNullOrWhiteSpace(preTurnRealm) &&
            IsExactChaosSeaRealm(preTurnRealm) &&
            !IsExactChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/ascension.json",
                IssueSeverity.Error,
                "AscensionTrigger turn не может заранее переключать currentRealm в другой bucket до клиентского ascension handoff.",
                code: "ascension_current_realm_switched_same_turn",
                section: "Lifecycle",
                expected: "current realm remains Chaos Sea while ascension.json is present",
                actual: currentRealm ?? "unknown current realm",
                repairHint: "Не переключай soul_state.currentRealm вручную на accepted turn с AscensionTrigger. Оставь currentRealm в Chaos Sea и дай клиенту завершить handoff по ascension.json."));
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

    private async Task<bool> HasCurrentTurnRecordLifeCompletionAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.TryGetProperty("metaStateUpdates", out var metaStateUpdates) &&
                   metaStateUpdates.ValueKind == JsonValueKind.Object &&
                   metaStateUpdates.TryGetProperty("lifeTransitions", out var lifeTransitions) &&
                   lifeTransitions.ValueKind == JsonValueKind.Object &&
                   lifeTransitions.TryGetProperty("recordLifeCompletion", out var recordLifeCompletion) &&
                   recordLifeCompletion.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
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

        var currentRealm = await TryResolveCurrentRealmAsync();

        var allowsShiningPendingBootstrap = false;
        if (!IsExactChaosSeaRealm(preTurnRealm) &&
            (string.Equals(preTurnRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(preTurnRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase)))
        {
            var preTurnShiningJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
                ShiningAbodeState.StatePath,
                issues,
                code: "incarnation_trigger_missing_pre_turn_shining_state",
                section: "Lifecycle",
                message: "TriggerIncarnation из Shining pending-bootstrap handoff требует validated pre-turn shining_abode_state.json.",
                repairHint: "Если TriggerIncarnation должен стартовать из frozen Shining package, сохраняй pre-turn shining_abode_state.json с preparedIncarnationPackage в validated pending snapshot.");
            if (!string.IsNullOrWhiteSpace(preTurnShiningJson))
            {
                try
                {
                    if (JsonNode.Parse(preTurnShiningJson) is JsonObject preTurnShiningRoot &&
                        preTurnShiningRoot["preparedIncarnationPackage"] is JsonObject)
                    {
                        allowsShiningPendingBootstrap = true;
                    }
                }
                catch
                {
                    // parse issues are reported elsewhere
                }
            }
        }

        if (!IsExactChaosSeaRealm(preTurnRealm) && !allowsShiningPendingBootstrap)
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "TriggerIncarnation допустим только в Chaos Sea или в Shining pending-bootstrap handoff с frozen package",
                code: "incarnation_trigger_invalid_realm",
                section: "Lifecycle",
                expected: "Chaos Sea or Shining Abode with preparedIncarnationPackage",
                actual: preTurnRealm,
                repairHint: "Проверяй realm на начало accepted turn. Разрешены только Chaos Sea и Shining pending-bootstrap handoff с frozen package; другие realms не могут запускать incarnation_trigger.json."));
        }

        if (IsExactChaosSeaRealm(preTurnRealm) && !IsExactChaosSeaRealm(currentRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "TriggerIncarnation turn не может заранее переключать currentRealm в mortal realm до клиентского bootstrap handoff.",
                code: "incarnation_trigger_current_realm_switched_same_turn",
                section: "Lifecycle",
                expected: "current realm remains Chaos Sea while incarnation_trigger.json is present",
                actual: currentRealm ?? "unknown current realm",
                repairHint: "Не переключай soul_state.currentRealm вручную на accepted turn с TriggerIncarnation. Оставь currentRealm в Chaos Sea и дай клиенту завершить bootstrap handoff по incarnation_trigger.json."));
        }
        else if (allowsShiningPendingBootstrap &&
                 !string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "TriggerIncarnation из frozen Shining package не может заранее выводить currentRealm из Сияющей Обители до клиентского bootstrap handoff.",
                code: "incarnation_trigger_shining_handoff_current_realm_switched_same_turn",
                section: "Lifecycle",
                expected: "current realm remains Shining Abode while incarnation_trigger.json handoff is active",
                actual: currentRealm ?? "unknown current realm",
                repairHint: "На accepted turn с frozen Shining handoff не переключай soul_state.currentRealm вручную. Оставь currentRealm в Сияющей Обители и дай клиенту завершить bootstrap handoff по incarnation_trigger.json."));
        }

        if (payload.IsGuardianForced && !IsExactChaosSeaRealm(preTurnRealm))
        {
            issues.Add(new ValidationIssue(
                "game_state/control/incarnation_trigger.json",
                IssueSeverity.Error,
                "Guardian-forced TriggerIncarnation допустим только из Chaos Sea, не из Shining pending-bootstrap handoff.",
                code: "forced_incarnation_invalid_shining_handoff_source",
                section: "Lifecycle",
                expected: "Chaos Sea",
                actual: preTurnRealm,
                repairHint: "Не смешивай guardian-forced incarnation с frozen Shining package handoff. Для handoff из Сияющей Обители используй обычный non-forced TriggerIncarnation."));
        }

        if (payload.IsGuardianForced && IsExactChaosSeaRealm(preTurnRealm))
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

    private static IReadOnlyList<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest> ReadPendingGuardianAbodeResidentTransferRequests(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty(GuardianAbodeResidentRequestState.TransferRequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest>();
            }

            var result = new List<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest>();
            foreach (var item in requestsNode.EnumerateArray())
            {
                try
                {
                    var request = JsonSerializer.Deserialize<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest>(item.GetRawText());
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
            return Array.Empty<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest>();
        }
    }

    private static HashSet<string> CollectAcceptedTransferArrivalResidentIds(
        JsonObject currentResidentsRoot,
        JsonObject? preTurnResidentsRoot,
        IReadOnlyList<GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest> requests,
        IReadOnlyDictionary<string, int> currentGuardianPowerById)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (preTurnResidentsRoot == null || requests.Count == 0)
            return result;

        var receipts = GuardianAbodeResidentState.EnsureTransferReceiptsArray(currentResidentsRoot);
        var currentResidents = GuardianAbodeResidentState.EnsureEntriesArray(currentResidentsRoot).OfType<JsonObject>().ToList();
        foreach (var request in requests)
        {
            var receipt = GuardianAbodeResidentState.FindTransferReceipt(receipts, request.RequestId);
            if (receipt == null ||
                !string.Equals(GetNodeString(receipt["status"]), GuardianAbodeResidentState.TransferStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryResolveEligiblePreTurnTransferResident(preTurnResidentsRoot, request, out var preTurnResident, out _))
                continue;

            var targetResident = currentResidents.FirstOrDefault(resident =>
                string.Equals(GetNodeString(resident["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(resident["guardianId"]), request.TargetGuardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(resident["abodeId"]), request.TargetAbodeId, StringComparison.OrdinalIgnoreCase) &&
                ResidentIsPresent(resident));
            if (ResidentTransferArrivalMatches(targetResident, preTurnResident, request, currentGuardianPowerById))
                result.Add(request.ResidentId);
        }

        return result;
    }

    private static bool TryResolveEligiblePreTurnTransferResident(
        JsonObject? preTurnResidentsRoot,
        GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest request,
        out JsonObject? preTurnResident,
        out string actual)
    {
        preTurnResident = null;
        if (preTurnResidentsRoot == null)
        {
            actual = "validated pre-turn guardian_abode_residents.json is missing or unreadable";
            return false;
        }

        var matchedResident = GuardianAbodeResidentState.FindResident(preTurnResidentsRoot, request.ResidentId);
        if (matchedResident == null)
        {
            actual = "resident is missing from validated pre-turn roster";
            return false;
        }

        var actualGuardianId = GetNodeString(matchedResident["guardianId"]);
        var actualAbodeId = GetNodeString(matchedResident["abodeId"]);
        var isPresent = ResidentIsPresent(matchedResident);
        if (!string.Equals(actualGuardianId, request.SourceGuardianId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actualAbodeId, request.SourceAbodeId, StringComparison.OrdinalIgnoreCase) ||
            !isPresent)
        {
            actual = $"resident source state is {actualGuardianId}/{actualAbodeId}, present={isPresent}";
            return false;
        }

        var actualMigrationState = GetNodeString(matchedResident["migrationState"]);
        if (!string.Equals(actualMigrationState, GuardianAbodeResidentState.MigrationStateReadyToTransfer, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"resident migrationState is {actualMigrationState}";
            return false;
        }

        preTurnResident = matchedResident;
        actual = $"{actualGuardianId}/{actualAbodeId} ready_to_transfer";
        return true;
    }

    private static bool TransferReceiptMatchesMode(
        JsonObject receipt,
        GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest request)
    {
        var status = GetNodeString(receipt["status"]);
        var transferMode = GetNodeString(receipt["transferMode"]);
        if (string.Equals(request.TransferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(status, GuardianAbodeResidentState.TransferStatusDepartedOnly, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(transferMode, GuardianAbodeResidentState.TransferModeDepartureOnly, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(status, GuardianAbodeResidentState.TransferStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(transferMode, GuardianAbodeResidentState.TransferModeAcceptedTransfer, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(status, GuardianAbodeResidentState.TransferStatusRefused, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(transferMode, GuardianAbodeResidentState.TransferModeRefusedTransfer, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(request.TransferMode, GuardianAbodeResidentState.TransferModeAcceptedTransfer, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool ResidentTransferArrivalMatches(
        JsonObject? currentTargetResident,
        JsonObject? preTurnResident,
        GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest request,
        IReadOnlyDictionary<string, int> currentGuardianPowerById)
    {
        if (currentTargetResident == null || preTurnResident == null)
            return false;

        var expectedArrival = preTurnResident.DeepClone().AsObject();
        expectedArrival["guardianId"] = request.TargetGuardianId;
        expectedArrival["guardianName"] = request.TargetGuardianName;
        expectedArrival["abodeId"] = request.TargetAbodeId;
        expectedArrival["abodeName"] = request.TargetAbodeName;
        var targetAbodePower = currentGuardianPowerById.TryGetValue(request.TargetGuardianId, out var parsedPower)
            ? parsedPower
            : (int?)null;
        var canonicalArrival = GuardianAbodeResidentState.BuildCanonicalTransferArrivalResident(expectedArrival, targetAbodePower);

        return string.Equals(GetNodeString(currentTargetResident["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(currentTargetResident["guardianId"]), request.TargetGuardianId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(currentTargetResident["abodeId"]), request.TargetAbodeId, StringComparison.OrdinalIgnoreCase) &&
               ResidentIsPresent(currentTargetResident) &&
               GetNodeInt(currentTargetResident["bondLevel"]) == GetNodeInt(preTurnResident["bondLevel"]) &&
               string.Equals(GetNodeString(currentTargetResident["bondTier"]), GetNodeString(preTurnResident["bondTier"]), StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(currentTargetResident["abodeDevotionLevel"]) == GetNodeInt(canonicalArrival["abodeDevotionLevel"]) &&
               string.Equals(GetNodeString(currentTargetResident["abodeDevotionTier"]), GetNodeString(canonicalArrival["abodeDevotionTier"]), StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(currentTargetResident["restlessness"]) == GetNodeInt(canonicalArrival["restlessness"]) &&
               string.Equals(GetNodeString(currentTargetResident["migrationState"]), GetNodeString(canonicalArrival["migrationState"]), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildResidentTransferStateSummary(
        bool sourceResidentPresent,
        JsonObject? targetResident,
        string? departureHistoryEntryId,
        string? arrivalHistoryEntryId)
    {
        var targetSummary = targetResident == null
            ? "target=missing"
            : $"target={GetNodeString(targetResident["guardianId"])}/{GetNodeString(targetResident["abodeId"])} state={GetNodeString(targetResident["migrationState"])} devotion={GetNodeInt(targetResident["abodeDevotionLevel"])} restlessness={GetNodeInt(targetResident["restlessness"])}";
        return $"sourcePresent={sourceResidentPresent}; {targetSummary}; departureHistory={departureHistoryEntryId ?? ""}; arrivalHistory={arrivalHistoryEntryId ?? ""}";
    }

    private static bool ResidentIsPresent(JsonObject resident) =>
        resident["isPresent"] is not JsonValue isPresentValue ||
        !isPresentValue.TryGetValue<bool>(out var isPresent) ||
        isPresent;

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

    private static bool HasExplicitEmptyShiningRequestsArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty(ShiningFactionRequestState.RequestsProperty, out var requestsNode) &&
                   requestsNode.ValueKind == JsonValueKind.Array &&
                   requestsNode.GetArrayLength() == 0;
        }
        catch
        {
            return false;
        }
    }

    private static JsonObject? FindShiningHall(JsonObject shiningRoot, string hallId)
    {
        return shiningRoot["halls"] is JsonArray halls
            ? halls.OfType<JsonObject>().FirstOrDefault(hall =>
                string.Equals(GetNodeString(hall["hallId"]), hallId, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static bool ShiningFoundingReceiptMatchesRequest(
        JsonObject receipt,
        ShiningFactionRequestState.PendingShiningFactionFoundingRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(receipt["proposedFactionId"])} / {GetNodeString(receipt["proposedHallId"])} / {GetNodeString(receipt["status"])} / cost {GetNodeInt(receipt["quotedCostFeathers"])}/{GetNodeInt(receipt["quotedCostLightSparks"])}";

        var status = GetNodeString(receipt["status"]);
        if (!ShiningFactionRequestState.IsSupportedFoundingStatus(status) ||
            !HasCanonicalShiningPoliticalClosure(receipt))
            return false;

        var receiptSupporters = ReadStringSet(receipt["supportingResidentIds"]);
        var requestSupporters = request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["proposedFactionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["proposedHallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["hallName"]), request.ProposedHallName, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(receipt["factionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               request.QuotedCostFeathers == ShiningFactionRequestState.FactionFoundingCostFeathers &&
               request.QuotedCostLightSparks == ShiningFactionRequestState.FactionFoundingCostLightSparks &&
               GetNodeInt(receipt["quotedCostFeathers"]) == ShiningFactionRequestState.FactionFoundingCostFeathers &&
               GetNodeInt(receipt["quotedCostLightSparks"]) == ShiningFactionRequestState.FactionFoundingCostLightSparks &&
               receiptSupporters.SetEquals(requestSupporters);
    }

    private static void ValidateAcceptedShiningFoundingReservedResources(
        List<ValidationIssue> issues,
        JsonObject currentShiningRoot,
        JsonObject? currentSoulRoot,
        JsonObject? preTurnShiningRoot,
        JsonObject? preTurnSoulRoot)
    {
        if (preTurnShiningRoot != null)
        {
            var preTurnLightSparks = GetNodeInt(preTurnShiningRoot["lightSparks"]);
            var currentLightSparks = GetNodeInt(currentShiningRoot["lightSparks"]);
            if (currentLightSparks > preTurnLightSparks)
            {
                issues.Add(new ValidationIssue(
                    ShiningFactionRequestState.PendingFoundingsRequestPath,
                    IssueSeverity.Error,
                    "Accepted Shining founding откатил локально зарезервированные Light Sparks.",
                    code: "shining_founding_reserved_light_sparks_rollback",
                    section: "ShiningAbode",
                    expected: $"lightSparks <= validated pre-turn reserved balance {preTurnLightSparks}",
                    actual: currentLightSparks.ToString(),
                    repairHint: "Не восстанавливай pre-reservation Light Sparks в GM output. Founding cost уже зарезервирован клиентом при создании pending request."));
            }
        }

        if (currentSoulRoot == null || preTurnSoulRoot == null)
            return;

        var preTurnFeathers = CurrentSoulFeathers(preTurnSoulRoot);
        var currentFeathers = CurrentSoulFeathers(currentSoulRoot);
        if (currentFeathers > preTurnFeathers)
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                IssueSeverity.Error,
                "Accepted Shining founding откатил локально зарезервированные Ink Feathers.",
                code: "shining_founding_reserved_ink_feathers_rollback",
                section: "ShiningAbode",
                expected: $"inkFeathers.current <= validated pre-turn reserved balance {preTurnFeathers}",
                actual: currentFeathers.ToString(),
                repairHint: "Не восстанавливай pre-reservation Ink Feathers в GM output. Founding cost уже зарезервирован клиентом при создании pending request."));
        }
    }

    private static bool ShiningHallMatchesFoundingRequest(
        JsonObject hall,
        ShiningFactionRequestState.PendingShiningFactionFoundingRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(hall["hallId"])} / {GetNodeString(hall["hallName"])} / {string.Join(",", ReadStringSet(hall["serviceTags"]).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}";

        return string.Equals(GetNodeString(hall["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(hall["hallName"]), request.ProposedHallName, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(hall["description"]), request.ProposedHallDescription, StringComparison.Ordinal) &&
               ReadStringSet(hall["serviceTags"]).SetEquals(request.ProposedHallServiceTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()));
    }

    private static bool ShiningFactionMatchesAcceptedFounding(
        JsonObject faction,
        ShiningFactionRequestState.PendingShiningFactionFoundingRequest request,
        out string actual)
    {
        var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
        actual =
            $"{GetNodeString(faction["factionId"])} / {GetNodeString(faction["originType"])} / {GetNodeString(leadership["headActorType"])}:{GetNodeString(leadership["headActorId"])} / {GetNodeString(leadership["leadershipState"])}";

        return string.Equals(GetNodeString(faction["factionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["originType"]), ShiningAbodeState.OriginTypePlayerFounded, StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(faction["baseStrength"]) == 35 &&
               string.Equals(GetNodeString(faction["charter"]?["factionName"]), request.Charter.FactionName, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(faction["charter"]?["favoredArchetype"]), request.Charter.FavoredArchetype, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["charter"]?["patronEffectFamily"]), request.Charter.PatronEffectFamily, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["charter"]?["summary"]), request.Charter.Summary, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(leadership["headActorType"]), ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["headActorId"]), ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateSecure, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShiningRealignmentReceiptMatchesRequest(
        JsonObject receipt,
        ShiningFactionRequestState.PendingShiningFactionRealignmentRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(receipt["residentId"])} / {GetNodeString(receipt["sourceFactionId"])} / {GetNodeString(receipt["targetFactionId"])} / {GetNodeString(receipt["status"])} / {GetNodeString(receipt["realignmentMode"])}";

        var status = GetNodeString(receipt["status"]);
        if (!ShiningFactionRequestState.IsSupportedRealignmentStatus(status) ||
            !HasCanonicalShiningPoliticalClosure(receipt) ||
            !string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["sourceFactionId"]), request.SourceFactionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["realignmentMode"]), request.RealignmentMode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(GetNodeString(receipt["targetFactionId"]) ?? string.Empty, request.TargetFactionId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            return false;

        return (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ShiningFactionRequestState.RequestStatusAccepted => string.Equals(request.RealignmentMode, ShiningFactionRequestState.RealignmentModeAcceptedTransfer, StringComparison.OrdinalIgnoreCase),
            ShiningFactionRequestState.RequestStatusRefused => string.Equals(request.RealignmentMode, ShiningFactionRequestState.RealignmentModeRefusedTransfer, StringComparison.OrdinalIgnoreCase),
            ShiningFactionRequestState.RequestStatusDepartedToNeutral => string.Equals(request.RealignmentMode, ShiningFactionRequestState.RealignmentModeDepartureToNeutral, StringComparison.OrdinalIgnoreCase),
            ShiningFactionRequestState.RequestStatusWithdrawn => true,
            _ => false
        };
    }

    private static bool ShiningLeadershipReceiptMatchesRequest(
        JsonObject receipt,
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(receipt["transitionMode"])} / {GetNodeString(receipt["previousHeadActorType"])}:{GetNodeString(receipt["previousHeadActorId"])} / {GetNodeString(receipt["newHeadActorType"])}:{GetNodeString(receipt["newHeadActorId"])} / {GetNodeString(receipt["status"])}";

        var status = GetNodeString(receipt["status"]);
        if (!ShiningFactionRequestState.IsSupportedLeadershipStatus(status) ||
            !HasCanonicalShiningPoliticalClosure(receipt) ||
            !string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["transitionMode"]), request.TransitionMode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["previousHeadActorType"]) ?? string.Empty, request.IncumbentHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["previousHeadActorId"]) ?? string.Empty, request.IncumbentHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            return true;

        var expectedCandidateType = request.CandidateHeadActorType ?? string.Empty;
        var expectedCandidateId = request.CandidateHeadActorId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedCandidateType) && string.IsNullOrWhiteSpace(expectedCandidateId))
        {
            return string.IsNullOrWhiteSpace(GetNodeString(receipt["newHeadActorType"])) &&
                   string.IsNullOrWhiteSpace(GetNodeString(receipt["newHeadActorId"]));
        }

        return string.Equals(GetNodeString(receipt["newHeadActorType"]) ?? string.Empty, expectedCandidateType, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["newHeadActorId"]) ?? string.Empty, expectedCandidateId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShiningLeadershipMatchesAcceptedOutcome(
        JsonObject leadership,
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(leadership["headActorType"])}:{GetNodeString(leadership["headActorId"])} / {GetNodeString(leadership["leadershipState"])}";

        if (string.IsNullOrWhiteSpace(request.CandidateHeadActorType) && string.IsNullOrWhiteSpace(request.CandidateHeadActorId))
        {
            return string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase) &&
                   string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorType"])) &&
                   string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorId"]));
        }

        return string.Equals(GetNodeString(leadership["headActorType"]) ?? string.Empty, request.CandidateHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["headActorId"]) ?? string.Empty, request.CandidateHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateSecure, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShiningLeadershipHistoryMatchesOutcome(
        JsonObject historyEntry,
        JsonObject receipt,
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request,
        out string actual)
    {
        actual = $"{GetNodeString(historyEntry["eventType"])} / turn {GetNodeInt(historyEntry["turnNumber"])}";
        var expectedEventType = ResolveExpectedLeadershipHistoryEventType(request, GetNodeString(receipt["status"]));
        if (string.IsNullOrWhiteSpace(expectedEventType))
            return true;

        return string.Equals(GetNodeString(historyEntry["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(historyEntry["eventType"]), expectedEventType, StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(historyEntry["turnNumber"]) == GetNodeInt(receipt["resolvedAtTurn"]);
    }

    private static string ResolveExpectedLeadershipHistoryEventType(
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request,
        string? status)
    {
        if (string.Equals(status, ShiningFactionRequestState.RequestStatusRefused, StringComparison.OrdinalIgnoreCase))
            return "refused";

        if (!string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (string.Equals(request.TransitionMode, ShiningFactionRequestState.TransitionModeRevolt, StringComparison.OrdinalIgnoreCase))
            return "revolted";
        if (string.Equals(request.TransitionMode, ShiningFactionRequestState.TransitionModePeacefulSuccession, StringComparison.OrdinalIgnoreCase))
            return "succeeded";

        return string.IsNullOrWhiteSpace(request.CandidateHeadActorType) &&
               string.IsNullOrWhiteSpace(request.CandidateHeadActorId)
            ? "vacated"
            : "abdicated";
    }

    private void ValidateAcceptedShiningCoreActionOutcome(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues,
        bool hasConcurrentPoliticalClosure)
    {
        switch ((request.ActionType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction:
                ValidateAcceptedShiningNativeDiscoveryOutcome(request, receipt, preTurnShiningRoot, preTurnResidentsRoot, preTurnSoulRoot, currentShiningRoot, currentResidentsRoot, currentSoulRoot, issues);
                return;

            case ShiningCoreActionRequestState.ActionTypeInvestInFaction:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TryInvestInFaction(cloneRoot, CloneJsonObject(preTurnResidentsRoot), request.FactionId, out _));
                return;

            case ShiningCoreActionRequestState.ActionTypeCompleteProject:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TryCompleteProject(
                        cloneRoot,
                        CloneJsonObject(preTurnResidentsRoot),
                        request.FactionId,
                        request.ProjectDraft?.DeepClone().AsObject() ?? new JsonObject(),
                        GetNodeInt(receipt["resolvedAtTurn"]),
                        GetNodeString(receipt["projectId"]),
                        GetNodeString(receipt["resolvedAtUtc"]),
                        out _,
                        out _));
                return;

            case ShiningCoreActionRequestState.ActionTypeSupportProject:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TrySupportProject(cloneRoot, request.FactionId, request.ProjectId, out _));
                return;

            case ShiningCoreActionRequestState.ActionTypeUnsupportProject:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TryUnsupportProject(cloneRoot, request.FactionId, request.ProjectId, out _));
                return;

            case ShiningCoreActionRequestState.ActionTypeRetireProject:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TryRetireProject(cloneRoot, CloneJsonObject(preTurnResidentsRoot), request.FactionId, request.ProjectId, out _));
                return;

            case ShiningCoreActionRequestState.ActionTypeOpenGates:
                ValidateAcceptedProjectedShiningAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues,
                    hasConcurrentPoliticalClosure,
                    mutate: cloneRoot => ShiningAbodeState.TryOpenGates(cloneRoot, CloneJsonObject(preTurnResidentsRoot), out _));
                return;

            case ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage:
                ValidateAcceptedPrepareIncarnationPackageOutcome(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues);
                return;

            case ShiningCoreActionRequestState.ActionTypePullRelicGacha:
                ValidateAcceptedShiningRelicGachaOutcome(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues);
                return;

            case ShiningCoreActionRequestState.ActionTypeForgeRelicReshape:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho:
            case ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity:
                ValidateAcceptedProjectedShiningForgeAction(
                    request,
                    receipt,
                    preTurnShiningRoot,
                    preTurnResidentsRoot,
                    preTurnSoulRoot,
                    currentShiningRoot,
                    currentResidentsRoot,
                    currentSoulRoot,
                    issues);
                return;
        }
    }

    private void ValidateAcceptedProjectedShiningAction(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues,
        bool hasConcurrentPoliticalClosure,
        Func<JsonObject, bool> mutate)
    {
        var expectedShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var expectedSoulRoot = CloneJsonObject(preTurnSoulRoot);

        if (!mutate(expectedShiningRoot))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Не удалось спроецировать expected Shining core action outcome из pre-turn state.",
                code: "shining_core_action_projection_failed",
                section: "ShiningAbode",
                expected: request.ActionType,
                actual: request.RequestId,
                repairHint: "Проверь, что pending core action request не противоречит canonical pre-turn Shining state."));
            return;
        }

        ApplyFeatherCostToSoul(expectedSoulRoot, request.QuotedCostFeathers);

        if (!ShiningCoreActionProjectedStateMatches(
                request,
                expectedShiningRoot,
                currentShiningRoot,
                hasConcurrentPoliticalClosure))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Current Shining state не совпадает с canonical projected outcome для accepted core action.",
                code: "shining_core_action_projected_state_mismatch",
                section: "ShiningAbode",
                expected: $"{request.ActionType} accepted projected state",
                actual: $"{GetNodeString(receipt["actionType"])} receipt resolvedAtTurn={GetNodeInt(receipt["resolvedAtTurn"])}",
                repairHint: "Для accepted core action materialize-ь shining_abode_state.json exactly as canonical helper projection dictates."));
        }

        if (!JsonNode.DeepEquals(preTurnResidentsRoot, currentResidentsRoot))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentState.StatePath,
                IssueSeverity.Error,
                "Accepted Shining core action не должен менять resident state, если этот action не является discover_native_faction.",
                code: "shining_core_action_unexpected_resident_state_change",
                section: "ShiningAbode",
                repairHint: "Не меняй guardian_abode_residents.json во время обычного Shining core action outcome; resident deltas должны закрываться отдельным resident contract."));
        }

        if (GetNodeInt(expectedShiningRoot["lightSparks"]) != GetNodeInt(currentShiningRoot["lightSparks"]))
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Light Sparks delta не совпадает с accepted Shining core action cost.",
                code: "shining_core_action_light_sparks_cost_mismatch",
                section: "ShiningAbode",
                expected: GetNodeInt(expectedShiningRoot["lightSparks"]).ToString(),
                actual: GetNodeInt(currentShiningRoot["lightSparks"]).ToString(),
                repairHint: "При accepted Shining core action списывай lightSparks exactly по canonical projected Shining-side cost."));
        }

        if (CurrentSoulFeathers(expectedSoulRoot) != CurrentSoulFeathers(currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Ink Feather delta не совпадает с accepted Shining core action cost.",
                code: "shining_core_action_feather_cost_mismatch",
                section: "ShiningAbode",
                expected: CurrentSoulFeathers(expectedSoulRoot).ToString(),
                actual: CurrentSoulFeathers(currentSoulRoot).ToString(),
                repairHint: "При accepted Shining core action списывай Ink Feathers exactly по quotedCostFeathers из client-authored request."));
        }

        if (!JsonNode.DeepEquals(expectedSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted Shining core action изменил Soul state вне разрешённого Ink Feather cost delta.",
                code: "shining_core_action_unexpected_soul_state_change",
                section: "ShiningAbode",
                repairHint: "Для этого Shining core action оставь soul_state.json equal to pre-turn snapshot except exact quotedCostFeathers debit."));
        }
    }

    private void ValidateAcceptedProjectedShiningForgeAction(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues)
    {
        var expectedShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var expectedSoulRoot = CloneJsonObject(preTurnSoulRoot);
        var expectedResidentsRoot = CloneJsonObject(preTurnResidentsRoot);

        if (!ShiningAbodeState.TryApplyForgeAction(
                expectedShiningRoot,
                expectedSoulRoot,
                expectedResidentsRoot,
                request.ActionType,
                request.FactionId,
                request.RelicId,
                request.TargetFormTag,
                request.PropertyIndex,
                request.ReplacementProperty?.DeepClone().AsObject(),
                request.AddedProperties?.DeepClone().AsArray(),
                GetNodeInt(receipt["resolvedAtTurn"]),
                GetNodeString(receipt["resolvedAtUtc"]),
                out _,
                out _))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Не удалось спроецировать expected forge outcome из pre-turn Shining и Soul Relic state.",
                code: "shining_forge_action_projection_failed",
                section: "ShiningAbode",
                repairHint: "Accepted forge action должен быть совместим с pre-turn faction, relic и quoted cost contract."));
            return;
        }

        var shiningMatches = ShiningRootsMatchExceptCoreActionReceipts(expectedShiningRoot, currentShiningRoot);
        if (!shiningMatches)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted forge action не materialize-ил canonical Shining-side lightSparks/state outcome.",
                code: "shining_forge_action_projected_state_mismatch",
                section: "ShiningAbode",
                repairHint: "При accepted forge action меняй только canonical Shining resources/state, предсказанные forge helper’ом."));
        }

        if (!JsonNode.DeepEquals(preTurnResidentsRoot, currentResidentsRoot))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentState.StatePath,
                IssueSeverity.Error,
                "Accepted forge action не должен менять resident state.",
                code: "shining_forge_action_unexpected_resident_state_change",
                section: "ShiningAbode",
                repairHint: "Forge actions изменяют только Shining resources/receipts and target Soul Relic; resident state должен закрываться отдельным contract."));
        }

        var soulRelicsMatch = JsonNode.DeepEquals(expectedSoulRoot["soulRelics"], currentSoulRoot["soulRelics"]);
        var featherMatch = CurrentSoulFeathers(expectedSoulRoot) == CurrentSoulFeathers(currentSoulRoot);
        if (!soulRelicsMatch || !featherMatch)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted forge action не materialize-ил canonical Soul Relic mutation или Ink Feather delta.",
                code: "shining_forge_action_soul_state_mismatch",
                section: "ShiningAbode",
                repairHint: "При accepted forge action обновляй Soul Relic и Ink Feather cost exactly as canonical forge projection dictates."));
        }

        if (!JsonNode.DeepEquals(expectedSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted forge action изменил Soul state вне canonical forge projection.",
                code: "shining_forge_action_unexpected_soul_state_change",
                section: "ShiningAbode",
                repairHint: "Forge action должен materialize-ить exactly projected Soul Relic mutation, resource cost and blessing entitlement lifecycle without unrelated soul_state edits."));
        }

        var expectedEntitlements = expectedSoulRoot[ShiningBlessingEffectState.SoulStateProperty]?["relicRefinementEntitlements"];
        var currentEntitlements = currentSoulRoot[ShiningBlessingEffectState.SoulStateProperty]?["relicRefinementEntitlements"];
        if (!JsonNode.DeepEquals(expectedEntitlements, currentEntitlements))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted forge action не materialize-ил canonical blessing entitlement lifecycle outcome.",
                code: "shining_forge_action_blessing_entitlement_mismatch",
                section: "ShiningAbode",
                expected: DescribeForgeBlessingEntitlements(expectedEntitlements),
                actual: DescribeForgeBlessingEntitlements(currentEntitlements),
                repairHint: "При accepted forge action синхронизируй entitlement status, allowances и consumedAt markers с canonical forge projection."));
        }
    }

    private static string DescribeForgeBlessingEntitlements(JsonNode? value)
    {
        if (value is not JsonObject entitlements)
            return "missing";

        return string.Join(", ", new[]
        {
            $"status={GetNodeString(entitlements["status"]) ?? "missing"}",
            $"rerolls={GetNodeInt(entitlements["rerolls"])}",
            $"freeShape={entitlements["freeShape"] is JsonValue freeShapeValue && freeShapeValue.TryGetValue<bool>(out var freeShape) && freeShape}",
            $"freeRetune={entitlements["freeRetune"] is JsonValue freeRetuneValue && freeRetuneValue.TryGetValue<bool>(out var freeRetune) && freeRetune}",
            $"rerollsSpent={GetNodeInt(entitlements["rerollsSpent"])}",
            $"consumedAtTurn={GetNodeInt(entitlements["consumedAtTurn"])}",
            $"consumedAtUtc={GetNodeString(entitlements["consumedAtUtc"]) ?? "missing"}"
        });
    }

    private void ValidateAcceptedPrepareIncarnationPackageOutcome(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues)
    {
        var expectedShiningRoot = CloneJsonObject(preTurnShiningRoot);
        if (expectedShiningRoot["gates"] is not JsonObject gates)
        {
            return;
        }

        var selectedCardIds = request.SelectedCardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => (JsonNode?)id.Trim())
            .ToArray();
        var normalizedSelectedCardIds = selectedCardIds
            .OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (normalizedSelectedCardIds.Count != normalizedSelectedCardIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "prepare_incarnation_package не допускает duplicate selectedCardIds ни в pending request, ни в accepted projection",
                code: "shining_prepare_package_duplicate_selected_card_ids",
                section: "ShiningAbode",
                repairHint: "Передавай в selectedCardIds уникальный ordered snapshot без повторов."));
            return;
        }

        gates["selectedBlessingCardIds"] = new JsonArray(selectedCardIds);

        if (!ShiningAbodeState.TryPrepareIncarnationPackage(
                expectedShiningRoot,
                GetNodeInt(receipt["resolvedAtTurn"]),
                out _,
                GetNodeString(receipt["resolvedAtUtc"])))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Не удалось спроецировать expected preparedIncarnationPackage из pre-turn draft.",
                code: "shining_prepare_package_projection_failed",
                section: "ShiningAbode",
                repairHint: "prepare_incarnation_package должен опираться на свежий open draft и selectedCardIds exact из client request."));
            return;
        }

        if (!ShiningRootsMatchExceptCoreActionReceipts(expectedShiningRoot, currentShiningRoot))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted prepare_incarnation_package не materialize-ил canonical frozen package и gates cleanup.",
                code: "shining_prepare_package_state_mismatch",
                section: "ShiningAbode",
                repairHint: "При accepted prepare_incarnation_package записывай preparedIncarnationPackage и очищай gates exactly как canonical helper projection dictates."));
        }

        if (!JsonNode.DeepEquals(preTurnResidentsRoot, currentResidentsRoot))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentState.StatePath,
                IssueSeverity.Error,
                "prepare_incarnation_package не должен менять resident state.",
                code: "shining_prepare_package_unexpected_resident_state_change",
                section: "ShiningAbode",
                repairHint: "При prepare_incarnation_package меняй только Shining gates/prepared package/receipt surfaces."));
        }

        var expectedSelectedCards = expectedShiningRoot["preparedIncarnationPackage"]?["selectedCards"];
        var actualSelectedCards = receipt["selectedCards"];
        if (!JsonNode.DeepEquals(expectedSelectedCards, actualSelectedCards))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted prepare_incarnation_package receipt не сохраняет stable snapshot выбранных карт.",
                code: "shining_prepare_package_receipt_snapshot_mismatch",
                section: "ShiningAbode",
                expected: expectedSelectedCards?.ToJsonString() ?? "missing",
                actual: actualSelectedCards?.ToJsonString() ?? "missing",
                repairHint: "Новый accepted receipt для prepare_incarnation_package должен включать selectedCards exact из canonical frozen package."));
        }

        if (CurrentSoulFeathers(preTurnSoulRoot) != CurrentSoulFeathers(currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "prepare_incarnation_package не должен сам по себе менять Ink Feathers.",
                code: "shining_prepare_package_unexpected_feather_delta",
                section: "ShiningAbode",
                expected: CurrentSoulFeathers(preTurnSoulRoot).ToString(),
                actual: CurrentSoulFeathers(currentSoulRoot).ToString(),
                repairHint: "Не списывай Ink Feathers на accepted prepare_incarnation_package, если client request не содержит feather cost."));
        }

        if (!JsonNode.DeepEquals(preTurnSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "prepare_incarnation_package не должен менять Soul state.",
                code: "shining_prepare_package_unexpected_soul_state_change",
                section: "ShiningAbode",
                repairHint: "Оставь soul_state.json unchanged; prepared package lives in shining_abode_state.json until runtime consumes it."));
        }
    }

    private void ValidateAcceptedShiningRelicGachaOutcome(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues)
    {
        var baseRarityFromTurn = TryReadCurrentTurnGachaBaseRaritySync();
        var receiptBaseRarity = GetNodeString(receipt["baseRarity"]) ?? string.Empty;
        var receiptFinalRarity = GetNodeString(receipt["finalRarity"]) ?? string.Empty;
        var relicId = GetNodeString(receipt["relicId"]) ?? string.Empty;
        var relicName = GetNodeString(receipt["relicName"]) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(baseRarityFromTurn) &&
            !string.Equals(baseRarityFromTurn, receiptBaseRarity, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted Shining gacha receipt должен использовать current turn gachaBaseResult.baseRarity как базовый минимум.",
                code: "shining_gacha_receipt_base_rarity_mismatch",
                section: "ShiningAbode",
                expected: baseRarityFromTurn,
                actual: receiptBaseRarity,
                repairHint: "Синхронизируй receipt.baseRarity с input/turn_request.json.gachaBaseResult.baseRarity текущего хода."));
        }

        var baseRank = GetRarityRank(receiptBaseRarity);
        var finalRank = GetRarityRank(receiptFinalRarity);
        if (baseRank > 0 && finalRank > 0)
        {
            if (finalRank < baseRank)
            {
                issues.Add(new ValidationIssue(
                    ShiningCoreActionRequestState.PendingActionsRequestPath,
                    IssueSeverity.Error,
                    "Accepted Shining gacha не может понизить финальную редкость ниже baseRarity.",
                    code: "shining_gacha_final_rarity_below_base",
                    section: "ShiningAbode",
                    expected: $">= {receiptBaseRarity}",
                    actual: receiptFinalRarity,
                    repairHint: "Shining banner modifiers могут только повышать или сохранять base rarity."));
            }
            else if (finalRank - baseRank > request.ProjectedGachaBonusSteps)
            {
                issues.Add(new ValidationIssue(
                    ShiningCoreActionRequestState.PendingActionsRequestPath,
                    IssueSeverity.Error,
                    "Accepted Shining gacha превысила допустимый projected bonus ceiling.",
                    code: "shining_gacha_bonus_steps_exceeded",
                    section: "ShiningAbode",
                    expected: $"+{request.ProjectedGachaBonusSteps} step(s) max",
                    actual: $"+{finalRank - baseRank} step(s)",
                    repairHint: "Не поднимай finalRarity выше baseRarity + projectedGachaBonusSteps из client-authored request."));
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
                "Accepted Shining gacha не должна удалять уже существующие Soul Relics.",
                code: "shining_gacha_unexpected_existing_relic_removal",
                section: "ShiningAbode",
                repairHint: "При banner pull только добавляй новую реликвию; не удаляй и не подменяй существующие Soul Relics."));
        }

        if (string.IsNullOrWhiteSpace(relicId) ||
            !newRelicIds.SetEquals(new[] { relicId }))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted Shining gacha должен materialize-ить ровно одну новую Soul Relic из receipt.relicId.",
                code: "shining_gacha_missing_new_relic_materialization",
                section: "ShiningAbode",
                expected: string.IsNullOrWhiteSpace(relicId) ? "one new relic id" : relicId,
                actual: newRelicIds.Count == 0 ? "no_new_relics" : string.Join(", ", newRelicIds),
                repairHint: "Добавляй в soul_state ровно одну новую Soul Relic и синхронизируй её id с coreAction receipt."));
        }
        else if (TryFindSoulRelicNode(currentSoulRoot, relicId, out var currentRelic))
        {
            var currentRarity = GetNodeString(currentRelic["quality"]) ?? GetNodeString(currentRelic["rarity"]) ?? string.Empty;
            if (!string.Equals(currentRarity, receiptFinalRarity, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json",
                    IssueSeverity.Error,
                    "Materialized Shining gacha relic должна сохранять finalRarity из receipt.",
                    code: "shining_gacha_relic_rarity_mismatch",
                    section: "ShiningAbode",
                    expected: receiptFinalRarity,
                    actual: currentRarity,
                    repairHint: "Сохраняй в result-реликвии итоговую редкость, совпадающую с receipt.finalRarity."));
            }
        }

        var expectedShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var expectedSoulRoot = CloneJsonObject(preTurnSoulRoot);
        var expectedResidentsRoot = CloneJsonObject(preTurnResidentsRoot);
        if (!ShiningAbodeState.TryApplyRelicGachaAccounting(
                expectedShiningRoot,
                expectedSoulRoot,
                expectedResidentsRoot,
                request.FactionId,
                request.RequestId,
                relicId,
                relicName,
                receiptBaseRarity,
                receiptFinalRarity,
                GetNodeInt(receipt["resolvedAtTurn"]),
                GetNodeString(receipt["resolvedAtUtc"]),
                out _,
                out _,
                out var projectionError))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Не удалось спроецировать expected Shining gacha accounting outcome из pre-turn state.",
                code: "shining_gacha_projection_failed",
                section: "ShiningAbode",
                actual: projectionError,
                repairHint: "Проверь, что pending pull_relic_gacha request совместим с canonical return-cycle, charges и cost contract."));
            return;
        }

        if (!ShiningRootsMatchExceptCoreActionReceipts(expectedShiningRoot, currentShiningRoot))
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Accepted Shining gacha не materialize-ила exact canonical Shining state outcome.",
                code: "shining_gacha_system_mismatch",
                section: "ShiningAbode",
                repairHint: "Обновляй только gachaSystem chargesUsedThisReturn/currentReturnCycleId/gachaHistory and coreActionReceipts exactly по accepted pull receipt; не меняй unrelated Shining state."));
        }

        if (!JsonNode.DeepEquals(preTurnResidentsRoot, currentResidentsRoot))
        {
            issues.Add(new ValidationIssue(
                GuardianAbodeResidentState.StatePath,
                IssueSeverity.Error,
                "Accepted Shining gacha не должна менять resident state.",
                code: "shining_gacha_unexpected_resident_state_change",
                section: "ShiningAbode",
                repairHint: "Shining relic gacha закрывает только gachaSystem, receipt, Ink Feather cost и одну новую Soul Relic; resident changes require separate contract."));
        }

        if (CurrentSoulFeathers(expectedSoulRoot) != CurrentSoulFeathers(currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted Shining gacha должна списывать exact Ink Feather cost.",
                code: "shining_gacha_feather_cost_mismatch",
                section: "ShiningAbode",
                expected: CurrentSoulFeathers(expectedSoulRoot).ToString(),
                actual: CurrentSoulFeathers(currentSoulRoot).ToString(),
                repairHint: "Списывай Ink Feathers exactly по quotedCostFeathers из pull_relic_gacha request."));
        }

        if (newRelicIds.SetEquals(new[] { relicId }) &&
            TryFindSoulRelicNode(currentSoulRoot, relicId, out var materializedRelic))
        {
            TryAppendRelicCloneToMatchingExpectedCollection(expectedSoulRoot, currentSoulRoot, relicId, materializedRelic);
        }

        if (!JsonNode.DeepEquals(expectedSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted Shining gacha изменил Soul state вне разрешённого diff: exact Ink Feather cost plus exactly one new Soul Relic.",
                code: "shining_gacha_soul_state_diff_mismatch",
                section: "ShiningAbode",
                repairHint: "Сохраняй все pre-turn Soul Relic nodes and unrelated soul_state fields byte-for-byte/canonically unchanged; добавляй только receipt.relicId and exact quotedCostFeathers debit."));
        }
    }

    private void ValidateAcceptedShiningNativeDiscoveryOutcome(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues)
    {
        var hallId = GetNodeString(receipt["hallId"]) ?? string.Empty;
        var factionId = GetNodeString(receipt["resolvedFactionId"]) ?? string.Empty;
        var residentIds = ReadStringSet(receipt["newResidentIds"]);
        var projectIds = ReadStringSet(receipt["seededProjectIds"]);
        var currentHall = FindShiningHall(currentShiningRoot, hallId);
        var currentFaction = ShiningAbodeState.FindFaction(currentShiningRoot, factionId);
        if (currentShiningRoot["pendingNativeFactionDiscovery"] is not null)
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction через pending_shining_abode_actions.json должен очищать legacy pendingNativeFactionDiscovery.",
                code: "shining_discovery_legacy_pending_not_cleared",
                section: "ShiningAbode",
                expected: "pendingNativeFactionDiscovery = null",
                actual: "pendingNativeFactionDiscovery present",
                repairHint: "После accepted new discover_native_faction не оставляй legacy shining_abode_state.pendingNativeFactionDiscovery live; этот legacy slot должен быть null.")); 
        }

        if (!string.IsNullOrWhiteSpace(hallId) && FindShiningHall(preTurnShiningRoot, hallId) != null)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted discover_native_faction не может переиспользовать существующий Shining hallId.",
                code: "shining_discovery_reused_existing_hall_id",
                section: "ShiningAbode",
                expected: "new hallId absent from pre-turn shining_abode_state.halls[]",
                actual: hallId,
                repairHint: "Для discover_native_faction создавай новый hallId, которого не было в pre-turn Shining state."));
        }

        if (!string.IsNullOrWhiteSpace(factionId) && ShiningAbodeState.FindFaction(preTurnShiningRoot, factionId) != null)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted discover_native_faction не может переиспользовать существующий Shining factionId.",
                code: "shining_discovery_reused_existing_faction_id",
                section: "ShiningAbode",
                expected: "new factionId absent from pre-turn shining_abode_state.factions[]",
                actual: factionId,
                repairHint: "Для discover_native_faction создавай новый factionId, которого не было в pre-turn Shining state."));
        }

        if (string.IsNullOrWhiteSpace(hallId) || currentHall == null)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted discover_native_faction не materialize-ил hall, указанный в receipt.",
                code: "shining_discovery_missing_hall_materialization",
                section: "ShiningAbode",
                expected: hallId,
                actual: currentHall == null ? "hall_missing" : hallId,
                repairHint: "В discovery receipt указывай hallId materialized нативного зала и создавай сам hall в shining_abode_state.json."));
        }

        if (string.IsNullOrWhiteSpace(factionId) || currentFaction == null ||
            !string.Equals(GetNodeString(currentFaction?["originType"]), ShiningAbodeState.OriginTypeNativeRadiant, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(currentFaction?["hallId"]), hallId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Accepted discover_native_faction не materialize-ил canonical native_radiant faction из receipt.",
                code: "shining_discovery_missing_faction_materialization",
                section: "ShiningAbode",
                expected: $"{factionId} / native_radiant / hall {hallId}",
                actual: currentFaction == null ? "faction_missing" : $"{GetNodeString(currentFaction["factionId"])} / {GetNodeString(currentFaction["originType"])} / hall {GetNodeString(currentFaction["hallId"])}",
                repairHint: "При accepted discover_native_faction создавай native_radiant faction и связывай её с materialized hallId из receipt."));
        }

        if (residentIds.Count is < 2 or > 4)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Discovery receipt должен содержать 2..4 newResidentIds.",
                code: "shining_discovery_invalid_new_resident_count",
                section: "ShiningAbode",
                expected: "2..4",
                actual: residentIds.Count.ToString(),
                repairHint: "Accepted discover_native_faction должен materialize-ить 2..4 ascended residents и перечислить их ids в receipt."));
        }

        foreach (var residentId in residentIds)
        {
            var previousResident = GuardianAbodeResidentState.FindResident(preTurnResidentsRoot, residentId);
            var currentResident = GuardianAbodeResidentState.FindResident(currentResidentsRoot, residentId);
            if (previousResident != null ||
                currentResident == null ||
                !string.Equals(GetNodeString(currentResident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(currentResident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    GuardianAbodeResidentState.StatePath,
                    IssueSeverity.Error,
                    "Accepted discover_native_faction должен materialize-ить новые ascended residents, привязанные к discovery faction.",
                    code: "shining_discovery_invalid_new_resident_materialization",
                    section: "ShiningAbode",
                    expected: $"{residentId} -> {factionId} (ascended)",
                    actual: currentResident == null ? "resident_missing" : $"{GetNodeString(currentResident["shiningFactionId"])} / {GetNodeString(currentResident["ascensionState"])}",
                    repairHint: "Новые discovery residents должны быть новыми ids, ascended и сразу принадлежать materialized native faction."));
            }
        }

        if (projectIds.Count != 2)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Discovery receipt должен содержать ровно 2 seededProjectIds.",
                code: "shining_discovery_invalid_seeded_project_count",
                section: "ShiningAbode",
                expected: "2",
                actual: projectIds.Count.ToString(),
                repairHint: "Accepted discover_native_faction должен materialize-ить ровно 2 seeded completed projects и перечислить их ids в receipt."));
        }

        if (currentFaction?["projects"] is JsonArray currentProjects)
        {
            foreach (var projectId in projectIds)
            {
                if (FindShiningProject(preTurnShiningRoot, projectId) != null)
                {
                    issues.Add(new ValidationIssue(
                        ShiningCoreActionRequestState.PendingActionsRequestPath,
                        IssueSeverity.Error,
                        "Accepted discover_native_faction не может переиспользовать существующий Shining projectId.",
                        code: "shining_discovery_reused_existing_project_id",
                        section: "ShiningAbode",
                        expected: "new seededProjectId absent from all pre-turn Shining faction projects",
                        actual: projectId,
                        repairHint: "Seeded discovery projects должны получать новые projectId, которых не было в pre-turn Shining state."));
                }

                var project = currentProjects.OfType<JsonObject>().FirstOrDefault(item =>
                    string.Equals(GetNodeString(item["projectId"]), projectId, StringComparison.OrdinalIgnoreCase));
                if (project == null ||
                    !string.Equals(GetNodeString(project["status"]), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        ShiningCoreActionRequestState.PendingActionsRequestPath,
                        IssueSeverity.Error,
                        "Seeded discovery project missing or not completed.",
                        code: "shining_discovery_missing_seeded_project",
                        section: "ShiningAbode",
                        expected: $"{projectId} completed",
                        actual: project == null ? "missing" : GetNodeString(project["status"]) ?? "unknown",
                        repairHint: "Accepted discover_native_faction должен создавать seeded completed projects внутри новой native faction."));
                }
            }
        }

        var expectedRadianceExperience = GetNodeInt(preTurnShiningRoot["radiance"]?["experience"]) + 20;
        if (GetNodeInt(currentShiningRoot["radiance"]?["experience"]) != expectedRadianceExperience)
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction должен начислить +20 Radiance XP.",
                code: "shining_discovery_missing_radiance_reward",
                section: "ShiningAbode",
                expected: expectedRadianceExperience.ToString(),
                actual: GetNodeInt(currentShiningRoot["radiance"]?["experience"]).ToString(),
                repairHint: "При accepted discover_native_faction увеличивай radiance.experience ровно на 20 и пересчитывай tier canonically."));
        }

        if (GetNodeInt(currentShiningRoot["lightSparks"]) != GetNodeInt(preTurnShiningRoot["lightSparks"]) - request.QuotedCostLightSparks)
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction должен списать exact Light Sparks cost.",
                code: "shining_discovery_light_sparks_cost_mismatch",
                section: "ShiningAbode",
                expected: (GetNodeInt(preTurnShiningRoot["lightSparks"]) - request.QuotedCostLightSparks).ToString(),
                actual: GetNodeInt(currentShiningRoot["lightSparks"]).ToString(),
                repairHint: "Списывай lightSparks exactly по quotedCostLightSparks из discovery request."));
        }

        if (CurrentSoulFeathers(currentSoulRoot) != CurrentSoulFeathers(preTurnSoulRoot) - request.QuotedCostFeathers)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted discover_native_faction должен списать exact Ink Feather cost.",
                code: "shining_discovery_feather_cost_mismatch",
                section: "ShiningAbode",
                expected: (CurrentSoulFeathers(preTurnSoulRoot) - request.QuotedCostFeathers).ToString(),
                actual: CurrentSoulFeathers(currentSoulRoot).ToString(),
                repairHint: "Списывай Ink Feathers exactly по quotedCostFeathers из discovery request."));
        }

        ValidateAcceptedShiningNativeDiscoveryConstrainedDiff(
            request,
            receipt,
            hallId,
            factionId,
            residentIds,
            preTurnShiningRoot,
            preTurnResidentsRoot,
            preTurnSoulRoot,
            currentShiningRoot,
            currentResidentsRoot,
            currentSoulRoot,
            issues);
    }

    private void ValidateAcceptedShiningNativeDiscoveryConstrainedDiff(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt,
        string hallId,
        string factionId,
        IReadOnlySet<string> residentIds,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues)
    {
        var allowedShiningTopLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pendingNativeFactionDiscovery",
            "radiance",
            "lightSparks",
            "halls",
            "factions",
            "coreActionReceipts",
            "shiningPoliticalActors"
        };
        ValidateObjectPropertiesUnchangedExcept(
            preTurnShiningRoot,
            currentShiningRoot,
            allowedShiningTopLevel,
            ShiningAbodeState.StatePath,
            "shining_discovery_unexpected_shining_state_change",
            issues,
            "Accepted discover_native_faction может менять только native discovery surfaces: pending slot, resources/radiance, halls, factions, political actors and receipt.");

        ValidateExistingArrayItemsUnchangedById(
            preTurnShiningRoot["halls"],
            currentShiningRoot["halls"],
            "hallId",
            ShiningAbodeState.StatePath,
            "shining_discovery_existing_hall_changed",
            issues,
            "Accepted discover_native_faction не должен менять pre-existing halls.");
        ValidateNoUnexpectedNewArrayItemsById(
            preTurnShiningRoot["halls"],
            currentShiningRoot["halls"],
            "hallId",
            new[] { hallId },
            ShiningAbodeState.StatePath,
            "shining_discovery_unexpected_new_hall",
            issues,
            "Accepted discover_native_faction должен добавлять только hallId из receipt.");

        ValidateExistingArrayItemsUnchangedById(
            preTurnShiningRoot["factions"],
            currentShiningRoot["factions"],
            "factionId",
            ShiningAbodeState.StatePath,
            "shining_discovery_existing_faction_changed",
            issues,
            "Accepted discover_native_faction не должен менять pre-existing factions/projects.");
        ValidateNoUnexpectedNewArrayItemsById(
            preTurnShiningRoot["factions"],
            currentShiningRoot["factions"],
            "factionId",
            new[] { factionId },
            ShiningAbodeState.StatePath,
            "shining_discovery_unexpected_new_faction",
            issues,
            "Accepted discover_native_faction должен добавлять только factionId из receipt.");

        ValidateExistingArrayItemsUnchangedById(
            preTurnResidentsRoot["entries"],
            currentResidentsRoot["entries"],
            "residentId",
            GuardianAbodeResidentState.StatePath,
            "shining_discovery_existing_resident_changed",
            issues,
            "Accepted discover_native_faction не должен менять pre-existing residents.");
        ValidateNoUnexpectedNewArrayItemsById(
            preTurnResidentsRoot["entries"],
            currentResidentsRoot["entries"],
            "residentId",
            residentIds,
            GuardianAbodeResidentState.StatePath,
            "shining_discovery_unexpected_new_resident",
            issues,
            "Accepted discover_native_faction должен добавлять только newResidentIds из receipt.");

        var expectedSoulRoot = CloneJsonObject(preTurnSoulRoot);
        ApplyFeatherCostToSoul(expectedSoulRoot, request.QuotedCostFeathers);
        if (!JsonNode.DeepEquals(expectedSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Accepted discover_native_faction изменил Soul state вне разрешённого Ink Feather cost delta.",
                code: "shining_discovery_unexpected_soul_state_change",
                section: "ShiningAbode",
                repairHint: "Для discover_native_faction оставь soul_state.json equal to pre-turn snapshot except exact quotedCostFeathers debit."));
        }

        ValidateExistingArrayItemsUnchangedById(
            preTurnShiningRoot["shiningPoliticalActors"],
            currentShiningRoot["shiningPoliticalActors"],
            "actorId",
            ShiningAbodeState.StatePath,
            "shining_discovery_existing_political_actor_changed",
            issues,
            "Accepted discover_native_faction не должен менять pre-existing Shining political actors.");
        ValidateNoUnexpectedNativeDiscoveryPoliticalActors(
            preTurnShiningRoot["shiningPoliticalActors"],
            currentShiningRoot["shiningPoliticalActors"],
            factionId,
            ShiningAbodeState.StatePath,
            issues);
    }

    private static void ValidateObjectPropertiesUnchangedExcept(
        JsonObject expected,
        JsonObject actual,
        IReadOnlySet<string> allowedChangedProperties,
        string filePath,
        string code,
        List<ValidationIssue> issues,
        string repairHint)
    {
        foreach (var propertyName in expected.Select(pair => pair.Key).Union(actual.Select(pair => pair.Key), StringComparer.OrdinalIgnoreCase))
        {
            if (allowedChangedProperties.Contains(propertyName))
                continue;

            expected.TryGetPropertyValue(propertyName, out var expectedNode);
            actual.TryGetPropertyValue(propertyName, out var actualNode);
            if (JsonNode.DeepEquals(expectedNode, actualNode))
                continue;

            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction изменил Shining state вне разрешённого constrained diff.",
                code: code,
                section: "ShiningAbode",
                expected: $"{propertyName} unchanged",
                actual: propertyName,
                repairHint: repairHint));
        }
    }

    private static void ValidateExistingArrayItemsUnchangedById(
        JsonNode? expectedNode,
        JsonNode? actualNode,
        string idProperty,
        string filePath,
        string code,
        List<ValidationIssue> issues,
        string repairHint)
    {
        var expectedById = IndexObjectsById(expectedNode, idProperty);
        var actualById = IndexObjectsById(actualNode, idProperty);
        foreach (var (id, expectedItem) in expectedById)
        {
            if (!actualById.TryGetValue(id, out var actualItem) ||
                !JsonNode.DeepEquals(expectedItem, actualItem))
            {
                issues.Add(new ValidationIssue(
                    filePath,
                    IssueSeverity.Error,
                    "Accepted discover_native_faction изменил pre-existing объект вне разрешённого constrained diff.",
                    code: code,
                    section: "ShiningAbode",
                    expected: $"{idProperty}={id} unchanged",
                    actual: actualById.ContainsKey(id) ? "changed" : "missing",
                    repairHint: repairHint));
            }
        }
    }

    private static void ValidateNoUnexpectedNewArrayItemsById(
        JsonNode? expectedNode,
        JsonNode? actualNode,
        string idProperty,
        IEnumerable<string> allowedNewIds,
        string filePath,
        string code,
        List<ValidationIssue> issues,
        string repairHint)
    {
        var expectedIds = IndexObjectsById(expectedNode, idProperty).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualIds = IndexObjectsById(actualNode, idProperty).Keys.ToList();
        var allowed = allowedNewIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var id in actualIds)
        {
            if (expectedIds.Contains(id) || allowed.Contains(id))
                continue;

            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction добавил объект вне разрешённого constrained diff.",
                code: code,
                section: "ShiningAbode",
                expected: allowed.Count == 0 ? "no new ids" : string.Join(", ", allowed),
                actual: id,
                repairHint: repairHint));
        }
    }

    private static Dictionary<string, JsonObject> IndexObjectsById(JsonNode? node, string idProperty)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonArray array)
            return result;

        foreach (var item in array.OfType<JsonObject>())
        {
            var id = GetNodeString(item[idProperty]);
            if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
                result[id] = item;
        }

        return result;
    }

    private static void ValidateNoUnexpectedNativeDiscoveryPoliticalActors(
        JsonNode? expectedNode,
        JsonNode? actualNode,
        string factionId,
        string filePath,
        List<ValidationIssue> issues)
    {
        var expectedIds = IndexObjectsById(expectedNode, "actorId").Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualById = IndexObjectsById(actualNode, "actorId");
        foreach (var (id, actor) in actualById)
        {
            if (expectedIds.Contains(id))
                continue;

            var currentFactionId = GetNodeString(actor["currentFactionId"]);
            var originFactionId = GetNodeString(actor["originFactionId"]);
            if (string.Equals(currentFactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(originFactionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                filePath,
                IssueSeverity.Error,
                "Accepted discover_native_faction добавил political actor вне новой native faction.",
                code: "shining_discovery_unexpected_new_political_actor",
                section: "ShiningAbode",
                expected: $"new actor.currentFactionId/originFactionId = {factionId}",
                actual: $"{id}: current={currentFactionId ?? "missing"}, origin={originFactionId ?? "missing"}",
                repairHint: "Discovery может добавлять только radiant actors, принадлежащие новой materialized native faction; не меняй чужую политическую карту."));
        }
    }

    private static JsonObject? FindShiningProject(JsonObject shiningRoot, string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || shiningRoot["factions"] is not JsonArray factions)
            return null;

        foreach (var faction in factions.OfType<JsonObject>())
        {
            if (faction["projects"] is not JsonArray projects)
                continue;

            var project = projects.OfType<JsonObject>().FirstOrDefault(item =>
                string.Equals(GetNodeString(item["projectId"]), projectId, StringComparison.OrdinalIgnoreCase));
            if (project != null)
                return project;
        }

        return null;
    }

    private void ValidateNonAcceptedShiningCoreActionOutcome(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnResidentsRoot,
        JsonObject preTurnSoulRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot,
        JsonObject currentSoulRoot,
        List<ValidationIssue> issues,
        bool hasConcurrentPoliticalClosure)
    {
        var stateChanged =
            !ShiningCoreActionProjectedStateMatches(null, preTurnShiningRoot, currentShiningRoot, hasConcurrentPoliticalClosure) ||
            !JsonNode.DeepEquals(preTurnResidentsRoot, currentResidentsRoot) ||
            !JsonNode.DeepEquals(preTurnSoulRoot, currentSoulRoot);

        if (stateChanged)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Refused/withdrawn Shining core action не должен менять canonical Shining or soul state.",
                code: "shining_core_action_unexpected_state_change_after_non_accept",
                section: "ShiningAbode",
                repairHint: "Если core action завершён не как accepted, не применяй его world-state mutation в shining_abode_state.json или soul_state.json."));
        }
    }

    private static bool ShiningCoreActionReceiptMatchesRequest(
        JsonObject receipt,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        out string actual)
    {
        actual =
            $"{GetNodeString(receipt["actionType"])} / {GetNodeString(receipt["status"])} / {GetNodeString(receipt["factionId"])} / {GetNodeString(receipt["projectId"])} / cost {GetNodeInt(receipt["quotedCostFeathers"])}/{GetNodeInt(receipt["quotedCostLightSparks"])} / draft {GetNodeInt(receipt["generatedDraftVersion"])}";

        var status = GetNodeString(receipt["status"]);
        return ShiningCoreActionRequestState.IsSupportedStatus(status) &&
               GetNodeInt(receipt["resolvedAtTurn"]) > 0 &&
               !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"])) &&
               string.Equals(GetNodeString(receipt["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["actionType"]), request.ActionType, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["factionId"]) ?? string.Empty, request.FactionId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               ShiningCoreActionProjectIdentityMatches(request, GetNodeString(receipt["projectId"])) &&
               ShiningCoreActionRelicIdentityMatches(request, GetNodeString(receipt["relicId"]), status) &&
               string.Equals(GetNodeString(receipt["returnCycleId"]) ?? string.Empty, request.ReturnCycleId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(receipt["targetFormTag"]) ?? string.Empty, request.TargetFormTag ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               OptionalCoreActionReceiptIntAuditMatches(receipt, "quotedCostFeathers", request.QuotedCostFeathers) &&
               OptionalCoreActionReceiptIntAuditMatches(receipt, "quotedCostLightSparks", request.QuotedCostLightSparks) &&
               ShiningCoreActionGeneratedDraftVersionMatches(request, status, GetNodeInt(receipt["generatedDraftVersion"])) &&
               (receipt["propertyIndex"] is JsonValue propertyIndexNode &&
                propertyIndexNode.TryGetValue<int>(out var propertyIndex)
                    ? propertyIndex
                   : -1) == request.PropertyIndex &&
               ReadOrderedStringList(receipt["selectedCardIds"]).SequenceEqual(
                   request.SelectedCardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()),
                   StringComparer.OrdinalIgnoreCase);
    }

    private static bool OptionalCoreActionReceiptIntAuditMatches(JsonObject receipt, string propertyName, int expected)
    {
        return !receipt.ContainsKey(propertyName) ||
               receipt[propertyName] is null ||
               GetNodeInt(receipt[propertyName]) == expected;
    }

    private static void ValidateShiningCoreActionReceiptAuditFields(
        JsonObject receipt,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        List<ValidationIssue> issues)
    {
        ValidateRequiredCoreActionReceiptIntAudit(receipt, request, "quotedCostFeathers", request.QuotedCostFeathers, issues);
        ValidateRequiredCoreActionReceiptIntAudit(receipt, request, "quotedCostLightSparks", request.QuotedCostLightSparks, issues);

        if (!ShiningCoreActionForgePayloadMatches(request, receipt))
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "Shining forge receipt не совпадает с client-authored mutation payload.",
                code: "shining_core_action_receipt_forge_payload_mismatch",
                section: "ShiningAbode",
                expected: "receipt replacementProperty/addedProperties exactly match pending request",
                actual: $"replacementProperty={receipt["replacementProperty"]?.ToJsonString() ?? "missing"}; addedProperties={receipt["addedProperties"]?.ToJsonString() ?? "missing"}",
                repairHint: "Для forge receipts echo canonical mutation payload из pending request: replacementProperty для retune_property и addedProperties для uplift_rarity."));
        }
    }

    private static void ValidateRequiredCoreActionReceiptIntAudit(
        JsonObject receipt,
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string propertyName,
        int expected,
        List<ValidationIssue> issues)
    {
        if (receipt.ContainsKey(propertyName) &&
            receipt[propertyName] is not null &&
            GetNodeInt(receipt[propertyName]) == expected)
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            IssueSeverity.Error,
            "Shining core action receipt должен явно echo quoted cost из pending request.",
            code: "shining_core_action_receipt_cost_audit_mismatch",
            section: "ShiningAbode",
            expected: $"{propertyName}={expected}",
            actual: $"{propertyName}={receipt[propertyName]?.ToJsonString() ?? "missing"} for {request.ActionType}/{request.RequestId}",
            repairHint: "Добавь quotedCostFeathers и quotedCostLightSparks в coreActionReceipts[] и синхронизируй их с pending_shining_abode_actions.json."));
    }

    private static bool ShiningCoreActionForgePayloadMatches(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        JsonObject receipt)
    {
        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty, StringComparison.OrdinalIgnoreCase))
            return JsonNode.DeepEquals(request.ReplacementProperty, receipt["replacementProperty"]);

        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity, StringComparison.OrdinalIgnoreCase))
            return JsonNode.DeepEquals(request.AddedProperties, receipt["addedProperties"]);

        if (IsShiningForgeActionType(request.ActionType))
            return receipt["replacementProperty"] is null && receipt["addedProperties"] is null;

        return true;
    }

    private static bool IsShiningForgeActionType(string? actionType)
    {
        return string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicReshape, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShiningCoreActionGeneratedDraftVersionMatches(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string? receiptStatus,
        int receiptGeneratedDraftVersion)
    {
        if (!string.Equals(receiptStatus, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            return receiptGeneratedDraftVersion == 0;

        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
            return receiptGeneratedDraftVersion == request.SourceDraftVersion;

        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypeOpenGates, StringComparison.OrdinalIgnoreCase))
            return receiptGeneratedDraftVersion > 0;

        return receiptGeneratedDraftVersion == 0;
    }

    private static bool HasCanonicalShiningPoliticalClosure(JsonObject receipt) =>
        GetNodeInt(receipt["resolvedAtTurn"]) > 0 &&
        !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"]));

    private static bool ShiningCoreActionProjectIdentityMatches(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string? receiptProjectId)
    {
        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypeCompleteProject, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return !string.IsNullOrWhiteSpace(receiptProjectId);
        }

        return string.Equals(receiptProjectId ?? string.Empty, request.ProjectId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShiningCoreActionRelicIdentityMatches(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest request,
        string? receiptRelicId,
        string? receiptStatus)
    {
        if (string.Equals(request.ActionType, ShiningCoreActionRequestState.ActionTypePullRelicGacha, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(request.RelicId))
                return string.Equals(receiptRelicId, request.RelicId, StringComparison.OrdinalIgnoreCase);

            return string.Equals(receiptStatus, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase)
                ? !string.IsNullOrWhiteSpace(receiptRelicId)
                : string.IsNullOrWhiteSpace(receiptRelicId);
        }

        return string.Equals(receiptRelicId ?? string.Empty, request.RelicId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryApplyConcurrentShiningClosuresAsync(
        JsonObject expectedShiningRoot,
        JsonObject expectedResidentsRoot,
        JsonObject currentShiningRoot,
        JsonObject currentResidentsRoot)
    {
        var changed = false;
        var foundingJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingFoundingsRequestPath);
        foreach (var request in ShiningFactionRequestState.ReadFoundingRequests(foundingJson))
        {
            var receipt = ShiningAbodeState.FindReceipt(
                ShiningAbodeState.EnsureFactionFoundingReceiptsArray(currentShiningRoot),
                request.RequestId);
            if (receipt != null &&
                ShiningFoundingReceiptMatchesRequest(receipt, request, out _) &&
                string.Equals(GetNodeString(receipt["status"]), ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            {
                ApplyAcceptedFoundingToComposite(expectedShiningRoot, expectedResidentsRoot, request, receipt);
                changed = true;
            }
            else if (receipt != null && ShiningFoundingReceiptMatchesRequest(receipt, request, out _))
            {
                ShiningAbodeState.EnsureFactionFoundingReceiptsArray(expectedShiningRoot).Add(CloneJsonObject(receipt));
                changed = true;
            }
        }

        var realignmentJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath);
        foreach (var request in ShiningFactionRequestState.ReadRealignmentRequests(realignmentJson))
        {
            var receipt = ShiningAbodeState.FindReceipt(
                ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(currentShiningRoot),
                request.RequestId);
            if (receipt != null && ShiningRealignmentReceiptMatchesRequest(receipt, request, out _))
            {
                ApplyRealignmentToComposite(expectedShiningRoot, expectedResidentsRoot, currentResidentsRoot, request, receipt);
                changed = true;
            }
        }

        var leadershipJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);
        foreach (var request in ShiningFactionRequestState.ReadLeadershipTransitionRequests(leadershipJson))
        {
            var faction = ShiningAbodeState.FindFaction(currentShiningRoot, request.FactionId);
            var receipt = ShiningAbodeState.FindReceipt(faction?["leadershipReceipts"] as JsonArray ?? new JsonArray(), request.RequestId);
            if (receipt != null && ShiningLeadershipReceiptMatchesRequest(receipt, request, out _))
            {
                ApplyLeadershipToComposite(expectedShiningRoot, currentShiningRoot, request, receipt);
                changed = true;
            }
        }

        var tradeJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningTradeRequestState.PendingRequestsPath);
        foreach (var request in ShiningTradeRequestState.ReadRequests(tradeJson))
        {
            if (ApplyTradeInventoryToComposite(expectedShiningRoot, currentShiningRoot, request))
                changed = true;
        }

        return changed;
    }

    private static void ApplyAcceptedFoundingToComposite(
        JsonObject expectedShiningRoot,
        JsonObject expectedResidentsRoot,
        ShiningFactionRequestState.PendingShiningFactionFoundingRequest request,
        JsonObject receipt)
    {
        var halls = expectedShiningRoot["halls"] as JsonArray;
        if (halls == null)
        {
            halls = new JsonArray();
            expectedShiningRoot["halls"] = halls;
        }
        if (FindShiningHall(expectedShiningRoot, request.ProposedHallId) == null)
        {
            halls.Add(new JsonObject
            {
                ["hallId"] = request.ProposedHallId,
                ["hallName"] = request.ProposedHallName,
                ["description"] = request.ProposedHallDescription,
                ["serviceTags"] = new JsonArray(request.ProposedHallServiceTags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => (JsonNode?)tag.Trim())
                    .ToArray())
            });
        }

        var factions = expectedShiningRoot["factions"] as JsonArray;
        if (factions == null)
        {
            factions = new JsonArray();
            expectedShiningRoot["factions"] = factions;
        }
        if (ShiningAbodeState.FindFaction(expectedShiningRoot, request.ProposedFactionId) == null)
        {
            factions.Add(new JsonObject
            {
                ["factionId"] = request.ProposedFactionId,
                ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
                ["hallId"] = request.ProposedHallId,
                ["charter"] = new JsonObject
                {
                    ["factionName"] = request.Charter.FactionName,
                    ["favoredArchetype"] = request.Charter.FavoredArchetype,
                    ["patronEffectFamily"] = request.Charter.PatronEffectFamily,
                    ["summary"] = request.Charter.Summary
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 35,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            });
        }

        var supporterIds = request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (expectedResidentsRoot["entries"] is JsonArray entries)
        {
            foreach (var resident in entries.OfType<JsonObject>())
            {
                var residentId = GetNodeString(resident["residentId"]);
                if (!string.IsNullOrWhiteSpace(residentId) && supporterIds.Contains(residentId))
                    resident["shiningFactionId"] = request.ProposedFactionId;
            }
        }

        ShiningAbodeState.EnsureFactionFoundingReceiptsArray(expectedShiningRoot).Add(CloneJsonObject(receipt));
        ShiningAbodeState.NormalizeStateRoot(expectedShiningRoot, expectedResidentsRoot, null);
    }

    private static void ApplyRealignmentToComposite(
        JsonObject expectedShiningRoot,
        JsonObject expectedResidentsRoot,
        JsonObject currentResidentsRoot,
        ShiningFactionRequestState.PendingShiningFactionRealignmentRequest request,
        JsonObject receipt)
    {
        AddUniqueReceipt(
            ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(expectedShiningRoot),
            receipt,
            request.RequestId);

        var status = GetNodeString(receipt["status"]) ?? string.Empty;
        var resident = GuardianAbodeResidentState.FindResident(expectedResidentsRoot, request.ResidentId);
        if (resident != null)
        {
            if (string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
                resident["shiningFactionId"] = request.TargetFactionId;
            else if (string.Equals(status, ShiningFactionRequestState.RequestStatusDepartedToNeutral, StringComparison.OrdinalIgnoreCase))
                resident["shiningFactionId"] = string.Empty;
        }

        CopyResidentHistoryEntryToComposite(expectedResidentsRoot, currentResidentsRoot, receipt);
        ShiningAbodeState.NormalizeStateRoot(expectedShiningRoot, expectedResidentsRoot, null);
    }

    private static void ApplyLeadershipToComposite(
        JsonObject expectedShiningRoot,
        JsonObject currentShiningRoot,
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request,
        JsonObject receipt)
    {
        var expectedFaction = ShiningAbodeState.FindFaction(expectedShiningRoot, request.FactionId);
        if (expectedFaction == null)
            return;

        AddUniqueReceipt(
            EnsureNestedArray(expectedFaction, "leadershipReceipts"),
            receipt,
            request.RequestId);

        var currentFaction = ShiningAbodeState.FindFaction(currentShiningRoot, request.FactionId);
        var currentHistory = currentFaction?["leadershipHistory"] as JsonArray;
        var historyEntry = currentHistory?.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase));
        if (historyEntry != null)
        {
            AddUniqueReceipt(
                EnsureNestedArray(expectedFaction, "leadershipHistory"),
                historyEntry,
                request.RequestId);
        }

        var status = GetNodeString(receipt["status"]) ?? string.Empty;
        if (string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            expectedFaction["leadership"] = string.IsNullOrWhiteSpace(request.CandidateHeadActorType) &&
                                           string.IsNullOrWhiteSpace(request.CandidateHeadActorId)
                ? new JsonObject
                {
                    ["headActorType"] = null,
                    ["headActorId"] = null,
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateVacant
                }
                : new JsonObject
                {
                    ["headActorType"] = request.CandidateHeadActorType,
                    ["headActorId"] = request.CandidateHeadActorId,
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                };

            ApplyRadiantActorLeadershipStatusToComposite(
                expectedShiningRoot,
                request.IncumbentHeadActorType,
                request.IncumbentHeadActorId,
                request.FactionId,
                ShiningAbodeState.PoliticalStatusFormerHead);
            ApplyRadiantActorLeadershipStatusToComposite(
                expectedShiningRoot,
                request.CandidateHeadActorType,
                request.CandidateHeadActorId,
                request.FactionId,
                ShiningAbodeState.PoliticalStatusHead);
        }
    }

    private static void ApplyRadiantActorLeadershipStatusToComposite(
        JsonObject expectedShiningRoot,
        string? actorType,
        string? actorId,
        string factionId,
        string politicalStatus)
    {
        if (!string.Equals(actorType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(actorId))
        {
            return;
        }

        var actor = FindShiningPoliticalActor(expectedShiningRoot, actorId);
        if (actor == null)
            return;

        actor["currentFactionId"] = factionId;
        actor["politicalStatus"] = politicalStatus;
    }

    private static JsonObject? FindShiningPoliticalActor(JsonObject shiningRoot, string actorId)
    {
        if (shiningRoot["shiningPoliticalActors"] is not JsonArray actors)
            return null;

        return actors.OfType<JsonObject>()
            .FirstOrDefault(actor => string.Equals(GetNodeString(actor["actorId"]), actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ApplyTradeInventoryToComposite(
        JsonObject expectedShiningRoot,
        JsonObject currentShiningRoot,
        ShiningTradeRequestState.PendingShiningTradeInventoryRequest request)
    {
        var currentFaction = ShiningAbodeState.FindFaction(currentShiningRoot, request.FactionId);
        var expectedFaction = ShiningAbodeState.FindFaction(expectedShiningRoot, request.FactionId);
        if (currentFaction == null || expectedFaction == null)
            return false;

        var tradeInventory = currentFaction["tradeInventory"] as JsonObject;
        var receipt = ShiningTradeRequestState.FindMatchingReceipt(currentFaction, request);
        if (!ShiningTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request) ||
            !ShiningTradeRequestState.ReceiptMatchesRequestContract(receipt, request, tradeInventory))
        {
            return false;
        }

        expectedFaction["tradeInventory"] = CloneJsonObject(tradeInventory!);
        AddUniqueReceipt(
            EnsureNestedArray(expectedFaction, ShiningTradeRequestState.ReceiptsProperty),
            receipt!,
            request.RequestId);
        return true;
    }

    private static void CopyResidentHistoryEntryToComposite(
        JsonObject expectedResidentsRoot,
        JsonObject currentResidentsRoot,
        JsonObject receipt)
    {
        var historyEntryId = GetNodeString(receipt["residentHistoryEntryId"]);
        if (string.IsNullOrWhiteSpace(historyEntryId))
            return;

        var currentHistoryLog = currentResidentsRoot[GuardianAbodeResidentState.HistoryLogProperty] as JsonArray;
        var historyEntry = currentHistoryLog?.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase));
        if (historyEntry != null)
        {
            AddUniqueReceipt(
                GuardianAbodeResidentState.EnsureHistoryLogArray(expectedResidentsRoot),
                historyEntry,
                historyEntryId,
                idProperty: "entryId");
        }
    }

    private static JsonArray EnsureNestedArray(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        parent[propertyName] = array;
        return array;
    }

    private static void AddUniqueReceipt(
        JsonArray receipts,
        JsonObject receipt,
        string requestId,
        string idProperty = "requestId")
    {
        for (var i = receipts.Count - 1; i >= 0; i--)
        {
            if (receipts[i] is not JsonObject existing)
            {
                receipts.RemoveAt(i);
                continue;
            }

            if (string.Equals(GetNodeString(existing[idProperty]), requestId, StringComparison.OrdinalIgnoreCase))
                receipts.RemoveAt(i);
        }

        receipts.Add(CloneJsonObject(receipt));
    }

    private static bool ShiningCoreActionProjectedStateMatches(
        ShiningCoreActionRequestState.PendingShiningCoreActionRequest? request,
        JsonObject expectedRoot,
        JsonObject currentRoot,
        bool hasConcurrentShiningClosure)
    {
        if (!hasConcurrentShiningClosure)
            return ShiningRootsMatchExceptCoreActionReceipts(expectedRoot, currentRoot);

        var expectedComparable = CloneJsonObject(expectedRoot);
        var currentComparable = CloneJsonObject(currentRoot);
        expectedComparable.Remove("coreActionReceipts");
        currentComparable.Remove("coreActionReceipts");

        return JsonNode.DeepEquals(expectedComparable, currentComparable);
    }

    private static bool ShiningRootsMatchExceptCoreActionReceipts(JsonObject expectedRoot, JsonObject currentRoot)
    {
        var expectedComparable = CloneJsonObject(expectedRoot);
        var currentComparable = CloneJsonObject(currentRoot);
        expectedComparable.Remove("coreActionReceipts");
        currentComparable.Remove("coreActionReceipts");
        return JsonNode.DeepEquals(expectedComparable, currentComparable);
    }

    private static JsonObject CloneJsonObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static int CurrentSoulFeathers(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject inkFeathers)
            return GetNodeInt(inkFeathers["current"]);

        return GetNodeInt(soulRoot["inkFeathers"]);
    }

    private static HashSet<string> CollectSoulRelicIds(JsonObject soulRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                {
                    var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                    if (!string.IsNullOrWhiteSpace(relicId))
                        result.Add(relicId);
                }
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
            {
                var relicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
                if (!string.IsNullOrWhiteSpace(relicId))
                    result.Add(relicId);
            }
        }

        return result;
    }

    private static bool TryFindSoulRelicNode(JsonObject soulRoot, string? relicId, out JsonObject relic)
    {
        relic = null!;
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        if (soulRoot["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var candidate in collection.OfType<JsonObject>())
                {
                    var candidateId = GetNodeString(candidate["relicId"]) ?? GetNodeString(candidate["id"]);
                    if (!string.Equals(candidateId, relicId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    relic = candidate;
                    return true;
                }
            }
        }
        else if (soulRoot["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var candidate in flatCollection.OfType<JsonObject>())
            {
                var candidateId = GetNodeString(candidate["relicId"]) ?? GetNodeString(candidate["id"]);
                if (!string.Equals(candidateId, relicId, StringComparison.OrdinalIgnoreCase))
                    continue;

                relic = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryAppendRelicCloneToMatchingExpectedCollection(
        JsonObject expectedSoulRoot,
        JsonObject currentSoulRoot,
        string relicId,
        JsonObject materializedRelic)
    {
        if (expectedSoulRoot["soulRelics"] is JsonObject expectedRelics &&
            currentSoulRoot["soulRelics"] is JsonObject currentRelics)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (currentRelics[collectionName] is not JsonArray currentCollection ||
                    !currentCollection.OfType<JsonObject>().Any(candidate =>
                    {
                        var candidateId = GetNodeString(candidate["relicId"]) ?? GetNodeString(candidate["id"]);
                        return string.Equals(candidateId, relicId, StringComparison.OrdinalIgnoreCase);
                    }))
                {
                    continue;
                }

                if (expectedRelics[collectionName] is not JsonArray expectedCollection)
                {
                    expectedCollection = new JsonArray();
                    expectedRelics[collectionName] = expectedCollection;
                }

                expectedCollection.Add(materializedRelic.DeepClone());
                return true;
            }
        }
        else if (expectedSoulRoot["soulRelics"] is JsonArray expectedFlatCollection &&
                 currentSoulRoot["soulRelics"] is JsonArray currentFlatCollection &&
                 currentFlatCollection.OfType<JsonObject>().Any(candidate =>
                 {
                     var candidateId = GetNodeString(candidate["relicId"]) ?? GetNodeString(candidate["id"]);
                     return string.Equals(candidateId, relicId, StringComparison.OrdinalIgnoreCase);
                 }))
        {
            expectedFlatCollection.Add(materializedRelic.DeepClone());
            return true;
        }

        return false;
    }

    private static void ApplyFeatherCostToSoul(JsonObject soulRoot, int feathers)
    {
        if (feathers <= 0)
            return;

        var inkFeathers = soulRoot["inkFeathers"] as JsonObject ?? new JsonObject();
        var current = Math.Max(0, GetNodeInt(inkFeathers["current"]) - feathers);
        inkFeathers["current"] = current;
        soulRoot["inkFeathers"] = inkFeathers;
    }

    private static HashSet<string> ReadStringSet(JsonNode? node)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonArray array)
            return result;

        foreach (var valueNode in array.OfType<JsonValue>())
        {
            if (!valueNode.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            result.Add(value.Trim());
        }

        return result;
    }

    private static List<string> ReadOrderedStringList(JsonNode? node)
    {
        var result = new List<string>();
        if (node is not JsonArray array)
            return result;

        foreach (var valueNode in array.OfType<JsonValue>())
        {
            if (!valueNode.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            result.Add(value.Trim());
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

