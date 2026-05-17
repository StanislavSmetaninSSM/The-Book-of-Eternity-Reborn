namespace BookOfEternityClient.Configuration;

/// <summary>
/// Maps GameResponse JSON fields → game_state file paths per CLI API Specification.
/// </summary>
public static class FileMapping
{
    public static readonly Dictionary<string, string> FieldToFile = new()
    {
        // CORE
        ["playerStatus"] = "game_state/core/player_status.json",
        ["currentPoiseChange"] = "game_state/core/player_status.json",

        // PLAYER CHARACTER
        ["activeSkillChanges"] = "game_state/player/skills_active.json",
        ["removeActiveSkills"] = "game_state/player/skills_active.json",
        ["passiveSkillChanges"] = "game_state/player/skills_passive.json",
        ["removePassiveSkills"] = "game_state/player/skills_passive.json",
        ["skillMasteryChanges"] = "game_state/player/skill_mastery.json",
        ["playerActiveEffectsChanges"] = "game_state/player/effects.json",
        ["calculatedWeightData"] = "game_state/player/weight_calc.json",
        ["statsIncreased"] = "game_state/player/status_changes.json",
        ["statsDecreased"] = "game_state/player/status_changes.json",
        ["currentEnergyChange"] = "game_state/player/status_changes.json",
        ["currentHealthChange"] = "game_state/player/status_changes.json",
        ["moneyChange"] = "game_state/player/status_changes.json",
        ["experienceGained"] = "game_state/player/experience.json",
        ["playerEffortTrackerChange"] = "game_state/player/experience.json",
        ["playerWoundChanges"] = "game_state/player/wounds.json",
        ["customStateChanges"] = "game_state/player/custom_states.json",
        ["playerStealthStateChange"] = "game_state/player/stealth.json",
        ["playerAppearanceChange"] = "game_state/player/transformation.json",
        ["playerImagePromptChange"] = "game_state/player/transformation.json",
        ["playerRaceChange"] = "game_state/player/transformation.json",
        ["playerRaceDescriptionChange"] = "game_state/player/transformation.json",
        ["playerClassChange"] = "game_state/player/transformation.json",
        ["playerClassDescriptionChange"] = "game_state/player/transformation.json",
        ["playerCharacterNameChange"] = "game_state/player/transformation.json",
        ["playerAutoCombatSkillChange"] = "game_state/player/transformation.json",
        ["setCharacteristics"] = "game_state/misc/characteristics.json",

        // INVENTORY SYSTEM
        ["UpdateInventory"] = "game_state/inventory/items.json",
        ["inventoryItemsResources"] = "game_state/inventory/item_resources.json",
        ["updateItemTextContents"] = "game_state/inventory/item_text_updates.json",
        ["moveInventoryItems"] = "game_state/inventory/item_movements.json",
        ["removeInventoryItems"] = "game_state/inventory/item_removals.json",
        ["itemBondLevelChanges"] = "game_state/inventory/item_bonds.json",
        ["itemFateCardUnlocks"] = "game_state/inventory/item_bonds.json",
        ["addOrUpdateRecipes"] = "game_state/inventory/recipes.json",
        ["removeRecipes"] = "game_state/inventory/recipes.json",
        ["moveToLocationStorage"] = "game_state/inventory/storage_operations.json",
        ["retrieveFromLocationStorage"] = "game_state/inventory/storage_operations.json",

        // WORLD STATE
        ["currentLocationData"] = "game_state/world/current_location.json",
        ["worldMapUpdates"] = "game_state/world/world_map.json",
        ["worldEventsLog"] = "game_state/world/world_events.json",
        ["UpdateRivalSoulArcs"] = "game_state/world/rival_soul_arcs.json",
        ["worldStateFlags"] = "game_state/world/world_flags.json",
        ["removeWorldStateFlags"] = "game_state/world/world_flags.json",
        ["timeChange"] = "game_state/world/world_time.json",
        ["setWorldTime"] = "game_state/world/world_time.json",
        ["weatherChange"] = "game_state/world/weather.json",
        ["updateWorldProgressionTracker"] = "game_state/world/progression.json",
        ["updateFactionProgressionTracker"] = "game_state/world/progression.json",

        // QUEST SYSTEM
        ["UpdateQuests"] = "game_state/quests/regular_quests.json",
        ["UpdateSoulQuests"] = "game_state/quests/soul_quests.json",
        ["questLog"] = "game_state/quests/quest_history.json",
        ["plotOutline"] = "game_state/quests/plot_outline.json",

        // NPC SYSTEM (14 files)
        ["UpdateNPCs"] = "game_state/npcs/npc_core.json",
        ["UpdateNpcTradeInventoryReceipts"] = "game_state/npcs/npc_core.json",
        ["NPCsRenameData"] = "game_state/npcs/npc_core.json",
        ["NPCsInScene"] = "game_state/npcs/npc_core.json",
        ["NPCActiveSkillChanges"] = "game_state/npcs/npc_skills.json",
        ["NPCPassiveSkillChanges"] = "game_state/npcs/npc_skills.json",
        ["NPCSkillMasteryChanges"] = "game_state/npcs/npc_skills.json",
        ["NPCPassiveSkillMasteryChanges"] = "game_state/npcs/npc_skills.json",
        ["NPCEffectChanges"] = "game_state/npcs/npc_effects.json",
        ["NPCWoundChanges"] = "game_state/npcs/npc_effects.json",
        ["NPCRelationshipChanges"] = "game_state/npcs/npc_relationships.json",
        ["interNPCRelationshipChanges"] = "game_state/npcs/npc_relationships.json",
        ["NPCRelationshipLockUpdates"] = "game_state/npcs/npc_relationships.json",
        ["NPCGoalUpdates"] = "game_state/npcs/npc_goals.json",
        ["NPCQuestUpdates"] = "game_state/npcs/npc_goals.json",
        ["NPCInventoryAdds"] = "game_state/npcs/npc_inventory.json",
        ["NPCInventoryUpdates"] = "game_state/npcs/npc_inventory.json",
        ["NPCInventoryRemovals"] = "game_state/npcs/npc_inventory.json",
        ["NPCEquipmentChanges"] = "game_state/npcs/npc_inventory.json",
        ["NPCInventoryResourcesChanges"] = "game_state/npcs/npc_inventory.json",
        ["NPCMaskAdds"] = "game_state/npcs/npc_masks.json",
        ["NPCMaskUpdates"] = "game_state/npcs/npc_masks.json",
        ["NPCMaskRemovals"] = "game_state/npcs/npc_masks.json",
        ["NPCActiveMaskChange"] = "game_state/npcs/npc_masks.json",
        ["NPCJournals"] = "game_state/npcs/npc_journals.json",
        ["npcInteractionJournalUpdates"] = "game_state/npcs/npc_interaction_journal.json",
        ["NPCUnlockedMemories"] = "game_state/npcs/npc_memory.json",
        ["itemJournalUpdates"] = "game_state/npcs/item_journals.json",
        ["NPCPersonalityTraitChanges"] = "game_state/npcs/npc_personality.json",
        ["NPCActivityUpdates"] = "game_state/npcs/npc_activities.json",
        ["completeNPCActivities"] = "game_state/npcs/npc_activities.json",
        ["NPCFateCardUnlocks"] = "game_state/npcs/npc_fate_cards.json",
        ["NPCCustomStateChanges"] = "game_state/npcs/npc_custom_states.json",

        // COMBAT STATE
        ["enemiesData"] = "game_state/combat/enemies.json",
        ["alliesData"] = "game_state/combat/allies.json",
        ["combat_log_markdown"] = "game_state/combat/combat_log.json",

        // FACTION SYSTEM
        ["factionDataChanges"] = "game_state/factions/faction_core.json",
        ["factionRankChanges"] = "game_state/factions/faction_structure.json",
        ["factionBonusChanges"] = "game_state/factions/faction_structure.json",
        ["factionResourceChanges"] = "game_state/factions/faction_resources.json",
        ["factionProjectUpdates"] = "game_state/factions/faction_projects.json",
        ["completeFactionProjects"] = "game_state/factions/faction_projects.json",
        ["factionCustomStateChanges"] = "game_state/factions/faction_custom.json",
        ["factionChronicleUpdates"] = "game_state/factions/faction_chronicles.json",

        // META-GAME SYSTEM
        ["metaStateUpdates"] = "game_state/meta/soul_state.json",
        ["afterlifeArchiveUpdates"] = "game_state/meta/soul_state.json",
        ["archiveActionResolutions"] = "game_state/meta/soul_state.json",
        ["afterlifeSpiritualConflictUpdate"] = "game_state/meta/afterlife_spiritual_conflict_state.json",
        ["afterlifeEntityProfiles"] = "game_state/meta/afterlife_entity_profiles.json",
        ["afterlifeEntityProfileUpdates"] = "game_state/meta/afterlife_entity_profiles.json",
        ["afterlifeEntityCustomStateChanges"] = "game_state/meta/afterlife_entity_profiles.json",
        ["UpdateGuardians"] = "game_state/meta/guardians.json",
        ["guardianQuestProgressUpdates"] = "game_state/meta/guardians.json",
        ["UpdateGuardianTradeInventoryReceipts"] = "game_state/meta/guardians.json",
        ["guardians"] = "game_state/meta/guardians.json",
        ["activeGuardian"] = "game_state/meta/guardians.json",
        ["chaosSeaNavigation"] = "game_state/meta/guardians.json",
        ["playerGuardianFoundationHistory"] = "game_state/meta/guardians.json",
        ["UpdateGuardianAbodeResidents"] = "game_state/meta/guardian_abode_residents.json",
        ["UpdateGuardianAbodeResidentRosterReceipts"] = "game_state/meta/guardian_abode_residents.json",
        ["UpdateGuardianAbodeResidentInteractionReceipts"] = "game_state/meta/guardian_abode_residents.json",
        ["UpdateGuardianAbodeResidentTransferReceipts"] = "game_state/meta/guardian_abode_residents.json",
        ["UpdateGuardianAbodeResidentHistoryLog"] = "game_state/meta/guardian_abode_residents.json",
        ["residentThoughtJournalUpdates"] = "game_state/meta/guardian_abode_residents.json",
        ["residentInteractionLogUpdates"] = "game_state/meta/guardian_abode_residents.json",
        ["guardianThoughtJournalUpdates"] = "game_state/meta/guardian_thought_journal.json",
        ["guardianSocialJournalUpdates"] = "game_state/meta/guardian_social_journal.json",
        ["startGuardianProjects"] = "game_state/meta/guardian_projects.json",
        ["guardianProjectUpdates"] = "game_state/meta/guardian_projects.json",
        ["completeGuardianProjects"] = "game_state/meta/guardian_projects.json",
        ["guardianPowerEvents"] = "game_state/meta/guardians.json",
        ["playerBehaviorAssessment"] = "game_state/meta/player_behavior.json",
        ["historyManipulationCoefficient"] = "game_state/meta/player_behavior.json",
        ["characterChronicleUpdates"] = "game_state/meta/character_chronicle.json",
        ["loreCodexUpdates"] = "lore/codex_entries.json",
        ["achievementUnlocks"] = "game_state/meta/achievements.json",

        // MISCELLANEOUS
        ["UpdateVehicles"] = "game_state/misc/vehicles.json",
        ["removeVehicles"] = "game_state/misc/vehicles.json",
        ["activeVehicleChange"] = "game_state/misc/vehicles.json",
        ["grantStorageAccess"] = "game_state/misc/storage_access.json",
        ["revokeStorageAccess"] = "game_state/misc/storage_access.json",
        ["shareStorageAccess"] = "game_state/misc/storage_access.json",
        ["multipliers"] = "game_state/misc/multipliers.json",
        ["otherPlayersInteractions"] = "game_state/misc/player_interactions.json",

        // GAME FLOW CONTROL
        ["TriggerLifeEnd"] = "game_state/control/life_transitions.json",
        ["TriggerIncarnation"] = "game_state/control/incarnation_trigger.json",
        ["AscensionTrigger"] = "game_state/control/ascension.json",
        ["playerChoice"] = "game_state/control/ascension.json",
        ["progressionProcessingReport"] = "game_state/control/progression_report.json",
    };

    /// <summary>
    /// Output files written separately from game state distribution.
    /// </summary>
    public static readonly Dictionary<string, string> OutputFiles = new()
    {
        ["narrative"] = "output/narrative_response.json",
        ["interface"] = "output/interface_updates.json",
        ["debug"] = "output/debug_logs.json",
        ["inkFeatherAction"] = "output/ink_feather_action_result.json",
        ["qteOffer"] = "output/qte_offer.json",
    };

    /// <summary>
    /// GameResponse fields written through dedicated output/* files instead of game_state mirrors.
    /// </summary>
    public static readonly HashSet<string> OutputOnlyResponseFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "response",
        "gm_thoughts_markdown",
        "dialogueOptions",
        "image_prompt"
    };
}
