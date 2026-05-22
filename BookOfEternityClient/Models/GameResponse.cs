using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Models;

/// <summary>
/// Complete JSON response from the Game Master - 95+ fields per CLI API Specification.
/// All fields are nullable since the GM only returns fields that changed.
/// </summary>
public class GameResponse
{
    // ═══════════════════════════════════════════════
    // NARRATIVE & CORE
    // ═══════════════════════════════════════════════
    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("gm_thoughts_markdown")]
    public string? GmThoughtsMarkdown { get; set; }

    [JsonPropertyName("combat_log_markdown")]
    public string? CombatLogMarkdown { get; set; }

    [JsonPropertyName("image_prompt")]
    public string? ImagePrompt { get; set; }

    // ═══════════════════════════════════════════════
    // PLAYER STATUS & RESOURCES
    // ═══════════════════════════════════════════════
    [JsonPropertyName("playerStatus")]
    public PlayerStatus? PlayerStatus { get; set; }

    [JsonPropertyName("currentPoiseChange")]
    public int? CurrentPoiseChange { get; set; }

    [JsonPropertyName("currentEnergyChange")]
    public int? CurrentEnergyChange { get; set; }

    [JsonPropertyName("currentHealthChange")]
    public int? CurrentHealthChange { get; set; }

    [JsonPropertyName("experienceGained")]
    public int? ExperienceGained { get; set; }

    [JsonPropertyName("moneyChange")]
    public int? MoneyChange { get; set; }

    [JsonPropertyName("statsIncreased")]
    public string[]? StatsIncreased { get; set; }

    [JsonPropertyName("statsDecreased")]
    public string[]? StatsDecreased { get; set; }

    [JsonPropertyName("setCharacteristics")]
    public Dictionary<string, int>? SetCharacteristics { get; set; }

    [JsonPropertyName("multipliers")]
    public double[]? Multipliers { get; set; }

    // ═══════════════════════════════════════════════
    // PLAYER IDENTITY & APPEARANCE
    // ═══════════════════════════════════════════════
    [JsonPropertyName("playerAppearanceChange")]
    public string? PlayerAppearanceChange { get; set; }

    [JsonPropertyName("playerImagePromptChange")]
    public string? PlayerImagePromptChange { get; set; }

    [JsonPropertyName("playerRaceChange")]
    public string? PlayerRaceChange { get; set; }

    [JsonPropertyName("playerRaceDescriptionChange")]
    public string? PlayerRaceDescriptionChange { get; set; }

    [JsonPropertyName("playerClassChange")]
    public string? PlayerClassChange { get; set; }

    [JsonPropertyName("playerClassDescriptionChange")]
    public string? PlayerClassDescriptionChange { get; set; }

    [JsonPropertyName("playerCharacterNameChange")]
    public string? PlayerCharacterNameChange { get; set; }

    [JsonPropertyName("playerAutoCombatSkillChange")]
    public string? PlayerAutoCombatSkillChange { get; set; }

    // ═══════════════════════════════════════════════
    // PLAYER SKILLS
    // ═══════════════════════════════════════════════
    [JsonPropertyName("activeSkillChanges")]
    public JsonElement[]? ActiveSkillChanges { get; set; }

    [JsonPropertyName("removeActiveSkills")]
    public string[]? RemoveActiveSkills { get; set; }

    [JsonPropertyName("skillMasteryChanges")]
    public JsonElement[]? SkillMasteryChanges { get; set; }

    [JsonPropertyName("passiveSkillChanges")]
    public JsonElement[]? PassiveSkillChanges { get; set; }

    [JsonPropertyName("removePassiveSkills")]
    public string[]? RemovePassiveSkills { get; set; }

    // ═══════════════════════════════════════════════
    // PLAYER EFFECTS, WOUNDS, MISC
    // ═══════════════════════════════════════════════
    [JsonPropertyName("playerActiveEffectsChanges")]
    public JsonElement[]? PlayerActiveEffectsChanges { get; set; }

    [JsonPropertyName("calculatedWeightData")]
    public JsonElement? CalculatedWeightData { get; set; }

    [JsonPropertyName("playerEffortTrackerChange")]
    public JsonElement? PlayerEffortTrackerChange { get; set; }

    [JsonPropertyName("playerWoundChanges")]
    public JsonElement[]? PlayerWoundChanges { get; set; }

    [JsonPropertyName("customStateChanges")]
    public JsonElement[]? CustomStateChanges { get; set; }

    [JsonPropertyName("playerStealthStateChange")]
    public JsonElement? PlayerStealthStateChange { get; set; }

    [JsonPropertyName("playerBehaviorAssessment")]
    public JsonElement? PlayerBehaviorAssessment { get; set; }

    [JsonPropertyName("characterChronicleUpdates")]
    public JsonElement[]? CharacterChronicleUpdates { get; set; }

    // ═══════════════════════════════════════════════
    // INVENTORY MANAGEMENT
    // ═══════════════════════════════════════════════
    [JsonPropertyName("UpdateInventory")]
    public JsonElement[]? UpdateInventory { get; set; }

    [JsonPropertyName("inventoryItemsResources")]
    public JsonElement[]? InventoryItemsResources { get; set; }

    [JsonPropertyName("updateItemTextContents")]
    public JsonElement[]? UpdateItemTextContents { get; set; }

    [JsonPropertyName("moveInventoryItems")]
    public JsonElement[]? MoveInventoryItems { get; set; }

    [JsonPropertyName("removeInventoryItems")]
    public JsonElement[]? RemoveInventoryItems { get; set; }

    [JsonPropertyName("itemBondLevelChanges")]
    public JsonElement[]? ItemBondLevelChanges { get; set; }

    [JsonPropertyName("itemFateCardUnlocks")]
    public JsonElement[]? ItemFateCardUnlocks { get; set; }

    [JsonPropertyName("addOrUpdateRecipes")]
    public JsonElement[]? AddOrUpdateRecipes { get; set; }

    [JsonPropertyName("removeRecipes")]
    public string[]? RemoveRecipes { get; set; }

    [JsonPropertyName("moveToLocationStorage")]
    public JsonElement[]? MoveToLocationStorage { get; set; }

    [JsonPropertyName("retrieveFromLocationStorage")]
    public JsonElement[]? RetrieveFromLocationStorage { get; set; }

    [JsonPropertyName("itemJournalUpdates")]
    public JsonElement[]? ItemJournalUpdates { get; set; }

    // ═══════════════════════════════════════════════
    // WORLD STATE
    // ═══════════════════════════════════════════════
    [JsonPropertyName("currentLocationData")]
    public JsonElement? CurrentLocationData { get; set; }

    [JsonPropertyName("worldMapUpdates")]
    public JsonElement? WorldMapUpdates { get; set; }

    [JsonPropertyName("worldEventsLog")]
    public JsonElement[]? WorldEventsLog { get; set; }

    [JsonPropertyName("UpdateRivalSoulArcs")]
    public JsonElement[]? UpdateRivalSoulArcs { get; set; }

    [JsonPropertyName("worldStateFlags")]
    public JsonElement[]? WorldStateFlags { get; set; }

    [JsonPropertyName("removeWorldStateFlags")]
    public string[]? RemoveWorldStateFlags { get; set; }

    [JsonPropertyName("timeChange")]
    public int? TimeChange { get; set; }

    [JsonPropertyName("setWorldTime")]
    public JsonElement? SetWorldTime { get; set; }

    [JsonPropertyName("weatherChange")]
    public JsonElement? WeatherChange { get; set; }

    [JsonPropertyName("updateWorldProgressionTracker")]
    public JsonElement? UpdateWorldProgressionTracker { get; set; }

    [JsonPropertyName("updateFactionProgressionTracker")]
    public JsonElement? UpdateFactionProgressionTracker { get; set; }

    [JsonPropertyName("progressionProcessingReport")]
    public JsonElement? ProgressionProcessingReport { get; set; }

    // ═══════════════════════════════════════════════
    // QUEST SYSTEM
    // ═══════════════════════════════════════════════
    [JsonPropertyName("UpdateQuests")]
    public JsonElement[]? UpdateQuests { get; set; }

    [JsonPropertyName("UpdateSoulQuests")]
    public JsonElement[]? UpdateSoulQuests { get; set; }

    [JsonPropertyName("questLog")]
    public JsonElement[]? QuestLog { get; set; }

    [JsonPropertyName("plotOutline")]
    public JsonElement? PlotOutline { get; set; }

    [JsonPropertyName("dialogueOptions")]
    public DialogueOption[]? DialogueOptions { get; set; }

    // ═══════════════════════════════════════════════
    // NPC SYSTEM
    // ═══════════════════════════════════════════════
    [JsonPropertyName("UpdateNPCs")]
    public JsonElement[]? UpdateNPCs { get; set; }

    [JsonPropertyName("UpdateNpcTradeInventoryReceipts")]
    public JsonElement[]? UpdateNpcTradeInventoryReceipts { get; set; }

    [JsonPropertyName("NPCsRenameData")]
    public JsonElement[]? NPCsRenameData { get; set; }

    [JsonPropertyName("NPCsInScene")]
    public JsonElement[]? NPCsInScene { get; set; }

    [JsonPropertyName("NPCActiveSkillChanges")]
    public JsonElement[]? NPCActiveSkillChanges { get; set; }

    [JsonPropertyName("NPCPassiveSkillChanges")]
    public JsonElement[]? NPCPassiveSkillChanges { get; set; }

    [JsonPropertyName("NPCSkillMasteryChanges")]
    public JsonElement[]? NPCSkillMasteryChanges { get; set; }

    [JsonPropertyName("NPCPassiveSkillMasteryChanges")]
    public JsonElement[]? NPCPassiveSkillMasteryChanges { get; set; }

    [JsonPropertyName("NPCEffectChanges")]
    public JsonElement[]? NPCEffectChanges { get; set; }

    [JsonPropertyName("NPCWoundChanges")]
    public JsonElement[]? NPCWoundChanges { get; set; }

    [JsonPropertyName("interNPCRelationshipChanges")]
    public JsonElement[]? InterNPCRelationshipChanges { get; set; }

    [JsonPropertyName("NPCRelationshipChanges")]
    public JsonElement[]? NPCRelationshipChanges { get; set; }

    [JsonPropertyName("NPCRelationshipLockUpdates")]
    public JsonElement[]? NPCRelationshipLockUpdates { get; set; }

    [JsonPropertyName("NPCGoalUpdates")]
    public JsonElement[]? NPCGoalUpdates { get; set; }

    [JsonPropertyName("NPCQuestUpdates")]
    public JsonElement[]? NPCQuestUpdates { get; set; }

    [JsonPropertyName("NPCInventoryAdds")]
    public JsonElement[]? NPCInventoryAdds { get; set; }

    [JsonPropertyName("NPCInventoryUpdates")]
    public JsonElement[]? NPCInventoryUpdates { get; set; }

    [JsonPropertyName("NPCInventoryRemovals")]
    public JsonElement[]? NPCInventoryRemovals { get; set; }

    [JsonPropertyName("NPCEquipmentChanges")]
    public JsonElement[]? NPCEquipmentChanges { get; set; }

    [JsonPropertyName("NPCInventoryResourcesChanges")]
    public JsonElement[]? NPCInventoryResourcesChanges { get; set; }

    [JsonPropertyName("NPCMaskAdds")]
    public JsonElement[]? NPCMaskAdds { get; set; }

    [JsonPropertyName("NPCMaskUpdates")]
    public JsonElement[]? NPCMaskUpdates { get; set; }

    [JsonPropertyName("NPCMaskRemovals")]
    public JsonElement[]? NPCMaskRemovals { get; set; }

    [JsonPropertyName("NPCActiveMaskChange")]
    public JsonElement[]? NPCActiveMaskChange { get; set; }

    [JsonPropertyName("NPCJournals")]
    public JsonElement[]? NPCJournals { get; set; }

    [JsonPropertyName("npcInteractionJournalUpdates")]
    public JsonElement[]? NpcInteractionJournalUpdates { get; set; }

    [JsonPropertyName("NPCUnlockedMemories")]
    public JsonElement[]? NPCUnlockedMemories { get; set; }

    [JsonPropertyName("NPCPersonalityTraitChanges")]
    public JsonElement[]? NPCPersonalityTraitChanges { get; set; }

    [JsonPropertyName("NPCActivityUpdates")]
    public JsonElement[]? NPCActivityUpdates { get; set; }

    [JsonPropertyName("completeNPCActivities")]
    public JsonElement[]? CompleteNPCActivities { get; set; }

    [JsonPropertyName("NPCFateCardUnlocks")]
    public JsonElement[]? NPCFateCardUnlocks { get; set; }

    [JsonPropertyName("NPCCustomStateChanges")]
    public JsonElement[]? NPCCustomStateChanges { get; set; }

    // ═══════════════════════════════════════════════
    // COMBAT STATE
    // ═══════════════════════════════════════════════
    [JsonPropertyName("enemiesData")]
    public JsonElement[]? EnemiesData { get; set; }

    [JsonPropertyName("alliesData")]
    public JsonElement[]? AlliesData { get; set; }

    // ═══════════════════════════════════════════════
    // FACTION SYSTEM
    // ═══════════════════════════════════════════════
    [JsonPropertyName("factionDataChanges")]
    public JsonElement[]? FactionDataChanges { get; set; }

    [JsonPropertyName("factionRankChanges")]
    public JsonElement[]? FactionRankChanges { get; set; }

    [JsonPropertyName("factionBonusChanges")]
    public JsonElement[]? FactionBonusChanges { get; set; }

    [JsonPropertyName("factionResourceChanges")]
    public JsonElement[]? FactionResourceChanges { get; set; }

    [JsonPropertyName("factionProjectUpdates")]
    public JsonElement[]? FactionProjectUpdates { get; set; }

    [JsonPropertyName("completeFactionProjects")]
    public JsonElement[]? CompleteFactionProjects { get; set; }

    [JsonPropertyName("factionCustomStateChanges")]
    public JsonElement[]? FactionCustomStateChanges { get; set; }

    [JsonPropertyName("factionChronicleUpdates")]
    public JsonElement[]? FactionChronicleUpdates { get; set; }

    // ═══════════════════════════════════════════════
    // META-GAME SYSTEM
    // ═══════════════════════════════════════════════
    [JsonPropertyName("metaStateUpdates")]
    public JsonElement? MetaStateUpdates { get; set; }

    [JsonPropertyName("afterlifeArchiveUpdates")]
    public JsonElement[]? AfterlifeArchiveUpdates { get; set; }

    [JsonPropertyName("archiveActionResolutions")]
    public JsonElement[]? ArchiveActionResolutions { get; set; }

    [JsonPropertyName("afterlifeSpiritualConflictUpdate")]
    public JsonElement? AfterlifeSpiritualConflictUpdate { get; set; }

    [JsonPropertyName("afterlifeChronicleUpdates")]
    public JsonElement[]? AfterlifeChronicleUpdates { get; set; }

    [JsonPropertyName("afterlifeGlobalFlagUpdates")]
    public JsonElement[]? AfterlifeGlobalFlagUpdates { get; set; }

    [JsonPropertyName("afterlifeRelationshipChanges")]
    public JsonElement[]? AfterlifeRelationshipChanges { get; set; }

    [JsonPropertyName("afterlifeRelationshipLockUpdates")]
    public JsonElement[]? AfterlifeRelationshipLockUpdates { get; set; }

    [JsonPropertyName("afterlifeBreakthroughQuestUpdates")]
    public JsonElement[]? AfterlifeBreakthroughQuestUpdates { get; set; }

    [JsonPropertyName("afterlifeActorMaskAdds")]
    public JsonElement[]? AfterlifeActorMaskAdds { get; set; }

    [JsonPropertyName("afterlifeActorMaskUpdates")]
    public JsonElement[]? AfterlifeActorMaskUpdates { get; set; }

    [JsonPropertyName("afterlifeActorMaskRemovals")]
    public JsonElement[]? AfterlifeActorMaskRemovals { get; set; }

    [JsonPropertyName("afterlifeActorActiveMaskChanges")]
    public JsonElement[]? AfterlifeActorActiveMaskChanges { get; set; }

    [JsonPropertyName("afterlifeThreatsToAdd")]
    public JsonElement[]? AfterlifeThreatsToAdd { get; set; }

    [JsonPropertyName("afterlifeThreatsToUpdate")]
    public JsonElement[]? AfterlifeThreatsToUpdate { get; set; }

    [JsonPropertyName("completeAfterlifeThreatActivities")]
    public JsonElement[]? CompleteAfterlifeThreatActivities { get; set; }

    [JsonPropertyName("afterlifeThreatsToRemove")]
    public JsonElement[]? AfterlifeThreatsToRemove { get; set; }

    [JsonPropertyName("UpdateGuardians")]
    public JsonElement[]? UpdateGuardians { get; set; }

    [JsonPropertyName("guardianQuestProgressUpdates")]
    public JsonElement[]? GuardianQuestProgressUpdates { get; set; }

    [JsonPropertyName("UpdateGuardianTradeInventoryReceipts")]
    public JsonElement[]? UpdateGuardianTradeInventoryReceipts { get; set; }

    [JsonPropertyName("guardians")]
    public JsonElement[]? Guardians { get; set; }

    [JsonPropertyName("activeGuardian")]
    public JsonElement? ActiveGuardian { get; set; }

    [JsonPropertyName("chaosSeaNavigation")]
    public JsonElement? ChaosSeaNavigation { get; set; }

    [JsonPropertyName("playerGuardianFoundationHistory")]
    public JsonElement[]? PlayerGuardianFoundationHistory { get; set; }

    [JsonPropertyName("UpdateGuardianAbodeResidents")]
    public JsonElement[]? UpdateGuardianAbodeResidents { get; set; }

    [JsonPropertyName("UpdateGuardianAbodeResidentRosterReceipts")]
    public JsonElement[]? UpdateGuardianAbodeResidentRosterReceipts { get; set; }

    [JsonPropertyName("UpdateGuardianAbodeResidentInteractionReceipts")]
    public JsonElement[]? UpdateGuardianAbodeResidentInteractionReceipts { get; set; }

    [JsonPropertyName("UpdateGuardianAbodeResidentTransferReceipts")]
    public JsonElement[]? UpdateGuardianAbodeResidentTransferReceipts { get; set; }

    [JsonPropertyName("UpdateGuardianAbodeResidentHistoryLog")]
    public JsonElement[]? UpdateGuardianAbodeResidentHistoryLog { get; set; }

    [JsonPropertyName("residentThoughtJournalUpdates")]
    public JsonElement[]? ResidentThoughtJournalUpdates { get; set; }

    [JsonPropertyName("residentInteractionLogUpdates")]
    public JsonElement[]? ResidentInteractionLogUpdates { get; set; }

    [JsonPropertyName("guardianThoughtJournalUpdates")]
    public JsonElement[]? GuardianThoughtJournalUpdates { get; set; }

    [JsonPropertyName("guardianSocialJournalUpdates")]
    public JsonElement[]? GuardianSocialJournalUpdates { get; set; }

    [JsonPropertyName("startGuardianProjects")]
    public JsonElement[]? StartGuardianProjects { get; set; }

    [JsonPropertyName("guardianProjectUpdates")]
    public JsonElement[]? GuardianProjectUpdates { get; set; }

    [JsonPropertyName("completeGuardianProjects")]
    public JsonElement[]? CompleteGuardianProjects { get; set; }

    [JsonPropertyName("guardianPowerEvents")]
    public JsonElement[]? GuardianPowerEvents { get; set; }

    [JsonPropertyName("historyManipulationCoefficient")]
    public double? HistoryManipulationCoefficient { get; set; }

    [JsonPropertyName("loreCodexUpdates")]
    public JsonElement[]? LoreCodexUpdates { get; set; }

    [JsonPropertyName("achievementUnlocks")]
    public JsonElement[]? AchievementUnlocks { get; set; }

    [JsonPropertyName("mathRequests")]
    public JsonElement[]? MathRequests { get; set; }

    [JsonPropertyName("mathAudit")]
    public JsonElement[]? MathAudit { get; set; }

    // ═══════════════════════════════════════════════
    // MISCELLANEOUS
    // ═══════════════════════════════════════════════
    [JsonPropertyName("UpdateVehicles")]
    public JsonElement[]? UpdateVehicles { get; set; }

    [JsonPropertyName("removeVehicles")]
    public string[]? RemoveVehicles { get; set; }

    [JsonPropertyName("activeVehicleChange")]
    public string? ActiveVehicleChange { get; set; }

    [JsonPropertyName("grantStorageAccess")]
    public JsonElement[]? GrantStorageAccess { get; set; }

    [JsonPropertyName("revokeStorageAccess")]
    public JsonElement[]? RevokeStorageAccess { get; set; }

    [JsonPropertyName("shareStorageAccess")]
    public JsonElement[]? ShareStorageAccess { get; set; }

    [JsonPropertyName("otherPlayersInteractions")]
    public JsonElement? OtherPlayersInteractions { get; set; }

    // ═══════════════════════════════════════════════
    // LIFE CONTROL
    // ═══════════════════════════════════════════════
    [JsonPropertyName("TriggerLifeEnd")]
    public JsonElement? TriggerLifeEnd { get; set; }

    [JsonPropertyName("TriggerIncarnation")]
    public JsonElement? TriggerIncarnation { get; set; }

    [JsonPropertyName("AscensionTrigger")]
    public JsonElement? AscensionTrigger { get; set; }

    [JsonPropertyName("playerChoice")]
    public string? PlayerChoice { get; set; }
}

public class PlayerStatus
{
    [JsonPropertyName("healthPercentage")]
    public string? HealthPercentage { get; set; }

    [JsonPropertyName("energyPercentage")]
    public string? EnergyPercentage { get; set; }

    [JsonPropertyName("poisePercentage")]
    public string? PoisePercentage { get; set; }

    [JsonPropertyName("activeConditions")]
    public string[]? ActiveConditions { get; set; }

    [JsonPropertyName("currentCondition")]
    public string? CurrentCondition { get; set; }
}

public class DialogueOption
{
    [JsonPropertyName("optionId")]
    public string? OptionId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}
