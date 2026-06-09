# 🎮 **The Book of Eternity Reborn - CLI API Specification**

**Version:** 1.1
**Date:** 2026-03-08
**Target:** CLI Agents (Gemini, Claude, etc.)  

---

## 📋 **Table of Contents**

1. [System Overview](#system-overview)
2. [Input Structures](#input-structures)
3. [JSON Response Schema](#json-response-schema)
4. [File System Architecture](#file-system-architecture)
5. [CLI Operations Protocol](#cli-operations-protocol)
6. [Validation Rules](#validation-rules)
7. [Error Handling](#error-handling)
8. [Lore Codex System](#lore-codex-system)
9. [Achievement System](#achievement-system)
10. [Guardian System](#guardian-system-updateguardians)
11. [Examples](#examples)

---

## 🏗️ **System Overview**

### Architecture Type
**File-Based Distributed Game State Management**

### Core Workflow
```
📖 READ: input/ files → 🧠 PROCESS: Rules/Block_*.txt → 📝 GENERATE: JSON response → 🗂️ DISTRIBUTE: fields to files → 🚦 SIGNAL: ready/
```

### Key Principles
- **Unified Processing**: Single-step complete turn handling
- **Atomic Operations**: All-or-nothing file updates with rollback
- **State Consistency**: Cross-file reference validation
- **Rule Preservation**: 100% compatibility with existing Rules/Block_*.txt

---

## 📥 **Input Structures**

### Primary Input: `input/turn_request.json`
```json
{
  "sessionId": "string (correlation token)",
  "requestId": "string (unique per turn request correlation token)",
  "turnNumber": "integer",
  "playerAction": "string (user's action in Russian)",
  "timestamp": "string (ISO 8601)",
  "gameMode": "string (normal|debug|test)",
  "progressionControl": {
    "currentRealm": "string",
    "currentWorldTimeInMinutes": "integer",
    "lastWorldSimulationTimeInMinutes": "integer",
    "lastFactionSimulationTimeInMinutes": "integer",
    "worldCycleMinutes": "integer (240)",
    "factionCycleMinutes": "integer (1440)",
    "worldCyclesAlreadyPendingBeforeTurn": "integer",
    "factionCyclesAlreadyPendingBeforeTurn": "integer",
    "mustEvaluateWorldProgression": "boolean (true iff worldCyclesAlreadyPendingBeforeTurn > 0)",
    "mustEvaluateFactionProgression": "boolean (true iff factionCyclesAlreadyPendingBeforeTurn > 0)",
    "currentChaosSeaTurnOrdinal": "integer",
    "nextChaosSeaTurnOrdinal": "integer",
    "lastChaosSeaSimulationOrdinal": "integer",
    "lastGuardianProjectCycleOrdinal": "integer",
    "nextGuardianProjectCycleOrdinal": "integer",
    "lastResidentAgencyCycleOrdinal": "integer",
    "lastShiningAbodeCycleOrdinal": "integer",
    "lastShiningFactionCycleOrdinal": "integer",
    "lastShiningTradeCycleOrdinal": "integer",
    "chaosSeaCycleEquivalentHours": "integer (24)",
    "nextResidentAgencyCycleOrdinal": "integer",
    "nextShiningAbodeCycleOrdinal": "integer",
    "nextShiningFactionCycleOrdinal": "integer",
    "nextShiningTradeCycleOrdinal": "integer",
    "chaosSeaCyclesExpectedThisTurn": "integer",
    "guardianProjectCyclesExpectedThisTurn": "integer",
    "residentAgencyCyclesExpectedThisTurn": "integer",
    "shiningAbodeCyclesExpectedThisTurn": "integer",
    "shiningFactionCyclesExpectedThisTurn": "integer",
    "shiningTradeCyclesExpectedThisTurn": "integer",
    "mustEvaluateChaosSeaProgression": "boolean (true iff chaosSeaCyclesExpectedThisTurn > 0)",
    "mustEvaluateGuardianProjectProgression": "boolean (true iff guardianProjectCyclesExpectedThisTurn > 0)",
    "mustEvaluateResidentAgencyProgression": "boolean (true iff residentAgencyCyclesExpectedThisTurn > 0)",
    "mustEvaluateShiningAbodeProgression": "boolean (true iff shiningAbodeCyclesExpectedThisTurn > 0)",
    "mustEvaluateShiningFactionProgression": "boolean (true iff shiningFactionCyclesExpectedThisTurn > 0)",
    "mustEvaluateShiningTradeProgression": "boolean (true iff shiningTradeCyclesExpectedThisTurn > 0)",
    "afterlifeCatchupRequired": "boolean",
    "afterlifeCatchupElapsedCycles": "integer",
    "afterlifeCatchupPressureTier": "string (none|minor|major|severe|epochal)",
    "afterlifeCatchupSummaryEventsRequired": "integer",
    "afterlifeCatchupContours": "array of strings naming affected afterlife contours"
  },
  "additionalContext": {
    "urgency": "string (low|medium|high)",
    "expectedResponse": "string (narrative|combat|dialogue)"
  }
}
```

### `progressionControl` Afterlife Scheduler Fields

The GM must treat every `mustEvaluate* = true` field as mandatory processing debt for this turn. The client decides when cycles are due; the GM decides the concrete fictional consequences and reports exactly what was processed.

| Field family | Meaning for GM |
|---|---|
| `chaosSeaCyclesExpectedThisTurn` / `mustEvaluateChaosSeaProgression` | Mandatory Chaos Sea hub progression: sea-wide omens, metaphysical pressure, guardian politics visible from the hub, and other Chaos-Sea-only living-world changes. |
| `guardianProjectCyclesExpectedThisTurn` / `mustEvaluateGuardianProjectProgression` | Mandatory Guardian project progression. Advance or resolve Guardian projects, musings, lore discoveries, rivalries, or abode power events only through Guardian/afterlife surfaces. |
| `residentAgencyCyclesExpectedThisTurn` / `mustEvaluateResidentAgencyProgression` | Mandatory agency for authored afterlife residents in Guardian Abodes: their choices, conversations, requests, memory/history changes, resident-linked Soul Quests, or relic grants. |
| `shiningAbodeCyclesExpectedThisTurn` / `mustEvaluateShiningAbodeProgression` | Mandatory Shining Abode state progression: public mood, order, crises, ascended social changes, and abode-level consequences. |
| `shiningFactionCyclesExpectedThisTurn` / `mustEvaluateShiningFactionProgression` | Mandatory Shining faction progression using Shining-specific state, not Mortal World `factionDataChanges`. |
| `shiningTradeCyclesExpectedThisTurn` / `mustEvaluateShiningTradeProgression` | Mandatory Shining trade/economy progression using Shining faction `tradeInventory`, faction `tradeInventoryReceipts[]`, availability/sold-out state, and afterlife notification derivation rules. Do not use Guardian trade inventory for Shining trade. |
| `next*Ordinal` / `last*Ordinal` | Per-contour scheduler markers. Do not invent ordinals; report the new last marker matching the processed count for that exact contour. |
| `afterlifeCatchup*` | Bounded catch-up summary. Do not simulate all raw elapsed cycles; produce exactly `afterlifeCatchupSummaryEventsRequired` meaningful summary outcomes affecting `afterlifeCatchupContours`. |

### Afterlife Living-World Operational Contract

Afterlife realms use the same "living world" principle as Mortal World progression: off-screen actors continue to act. The difference is that afterlife progression must stay inside afterlife state and commands.

For every `Chaos Sea` / `Shining Abode` turn, the GM must use `OtherGuides/Afterlife_Contract_Matrix.md` as the operational contract map before writing files. The matrix lists the decision loop, each scheduler contour, pending request file, direct afterlife action, canonical state surface, required receipt/report, living-world outcome selection, and forbidden Mortal World substitution.

The GM must read these state groups before resolving afterlife scheduler debt:
- `game_state/meta/soul_state.json`: current realm, Soul Relics, Enlightenment, Ink Feathers, afterlife archive, current incarnation metadata.
- `game_state/meta/guardians.json`: Guardian identity, reputation, mood, musings, trade inventory, relationships, buyback relics.
- `game_state/meta/guardian_projects.json`: active/completed Guardian projects and project pressure.
- `game_state/meta/guardian_abode_residents.json`: authored residents, resident memory, history, rewards, linked Soul Quests, Shining alignment fields.
- `game_state/meta/shining_abode_state.json`: Shining availability, Light Sparks, Radiance, client-owned treasury, halls, gates, factions, projects, `shiningPoliticalActors`, trade inventories, receipts, prepared incarnation package.
- `game_state/control/pending_*afterlife*` and `game_state/control/pending_shining_*` files: client-authored contracts that must be resolved canonically when present.

Legal afterlife progression surfaces include:
- `UpdateGuardians`
- `guardianThoughtJournalUpdates`
- `guardianSocialJournalUpdates`
- `startGuardianProjects`
- `guardianProjectUpdates`
- `completeGuardianProjects`
- `guardianPowerEvents`
- `UpdateGuardianTradeInventoryReceipts`
- `UpdateGuardianAbodeResidents`
- `UpdateGuardianAbodeResidentRosterReceipts`
- `UpdateGuardianAbodeResidentInteractionReceipts`
- `UpdateGuardianAbodeResidentHistoryLog`
- `residentThoughtJournalUpdates`
- `residentInteractionLogUpdates`
- `UpdateSoulQuests` with `relatedAfterlifeResidentId`
- `metaStateUpdates` for canonical Soul/Ink/Soul Relic effects
- `afterlifeArchiveUpdates` and `archiveActionResolutions`
- canonical `shining_abode_state.json` mutation plus Shining receipt arrays when resolving Shining contracts
- `shiningFactionChronicleUpdates`, `shiningFactionInfluenceUpdates`, `shiningFactionStrategicMemoryUpdates`, and `shiningFactionResourceLedgerUpdates` for Shining faction political memory in `game_state/meta/shining_abode_state.json`; they project into faction `chronicle`, `territorialInfluence`, `strategicMemory`, and `resourceLedger`
- `afterlifeEntityProfileUpdates` / `afterlifeEntityCustomStateChanges` / `afterlifeFateCardUnlocks` / `afterlifeActorGoalUpdates` / `afterlifeActorQuestUpdates` / `afterlifeActorActivityUpdates` / `completeAfterlifeActorActivities` / `afterlifeRelationshipChanges` / `afterlifeRelationshipLockUpdates` / `afterlifeBreakthroughQuestUpdates` / `afterlifeActorMaskAdds` / `afterlifeActorMaskUpdates` / `afterlifeActorMaskRemovals` / `afterlifeActorActiveMaskChanges` / `afterlifeEntityProgressionOverrides` / `afterlifeSpecialArtLearningReceipts` for Профили сущностей посмертия, their customStates, Fate Cards, actor goals/personal quests/current activities, relationship gates (`relationshipLock`, `breakthroughQuestId`, `redemptionQuestId`, `pointOfNoReturn`, `_clear_`), masks (`masks`, `activeMaskId`, `_true_self_`, `concealedTruth`), explicit GM progression overrides (`specialArtTierDeltas`, `soulDissipationTierDelta`, and other explicit deltas), and special-art learning in `game_state/meta/afterlife_entity_profiles.json`
- `afterlifeThreatsToAdd` / `afterlifeThreatsToUpdate` / `completeAfterlifeThreatActivities` / `afterlifeThreatsToRemove` for persistent afterlife active threats in `game_state/meta/afterlife_active_threats.json`; canonical `threats[]` use `currentActivity`, `impactProfile`, `visibleToPlayer`, and optional `sarefLink`
- `afterlifeChronicleUpdates` for afterlife external memory in `game_state/meta/afterlife_chronicles.json`; write `lastEventsDescription`, not Mortal `worldEventsLog`
- `afterlifeGlobalFlagUpdates` for afterlife global flags in `game_state/meta/afterlife_global_flags.json`; write targeted flags, not Mortal `worldStateFlags`
- `progressionProcessingReport`

Forbidden substitutions for afterlife scheduler debt:
- File-level rule: during `Chaos Sea` / `Shining Abode`, no response surface may write or mutate any file mapped to `game_state/core/player_status.json`, `game_state/player/*`, `game_state/inventory/*`, `game_state/world/*`, `game_state/npcs/*`, `game_state/combat/*`, `game_state/factions/*`, `lore/current_world/*`, Mortal quest files, or Mortal misc files.
- Do not use `worldEventsLog` for Chaos Sea or Shining Abode events.
- Do not use `factionDataChanges`, `factionProjectUpdates`, `completeFactionProjects`, or `factionChronicleUpdates` for Shining factions.
- Do not use `UpdateNPCs`, `NPCsInScene`, `NPCGoalUpdates`, or `NPCActivityUpdates` for Guardians or afterlife residents.
- Do not use `currentLocationData`, `worldMapUpdates`, `timeChange`, `setWorldTime`, or `weatherChange` for afterlife movement, time, or environment.
- Do not hand-author `afterlife_notifications.json`; the client derives notifications from canonical receipts and result surfaces.

Required `gm_thoughts_markdown` coverage when afterlife debt is due:
- List every due afterlife contour and its expected count.
- Explain which state files were checked.
- Declare all changed Guardians, residents, and Shining institutions as relevant actors.
- Explain outside-scope Guardians/residents/institutions when they could plausibly matter but are not processed.
- Summarize each processed contour's consequence, including stability/no-mutation decisions.
- If catch-up is required, list `afterlifeCatchupPressureTier`, `afterlifeCatchupContours`, and each bounded summary outcome.

Example afterlife report with mixed backlog:

```json
{
  "sessionId": "session-id-from-request",
  "requestId": "request-id-from-request",
  "turnNumber": 42,
  "chaosSeaCyclesProcessed": 0,
  "guardianProjectCyclesProcessed": 2,
  "residentAgencyCyclesProcessed": 3,
  "shiningAbodeCyclesProcessed": 4,
  "shiningFactionCyclesProcessed": 5,
  "shiningTradeCyclesProcessed": 6,
  "newLastGuardianProjectCycleOrdinal": 10,
  "newLastResidentAgencyCycleOrdinal": 11,
  "newLastShiningAbodeCycleOrdinal": 12,
  "newLastShiningFactionCycleOrdinal": 13,
  "newLastShiningTradeCycleOrdinal": 14,
  "afterlifeCatchupProcessed": true,
  "afterlifeCatchupSummaryEventsProcessed": 3
}
```

The example intentionally uses different `newLast*Ordinal` values. Each contour advances by its own expected count, never by the maximum backlog of another contour.

### Legacy Note: `input/player_command.json`
Historical drafts mentioned a separate `input/player_command.json` surface for save/load/debug/end_life.
That file is not part of the current GM daemon contract.

Current baseline:
- GM-authored work is triggered only through `input/turn_request.json`.
- Save/load/options are handled by the client/runtime, not by a GM-authored input file.
- Voluntary life end is initiated by client UX and materialized as the normal lifecycle turn flow, not as a separate `player_command.json` channel.

### Context Loading
CLI Agent automatically loads current game state from:
- `game_state/` (all subdirectories)
- `lore/` (world lore plus player-authored current-world dossier)
- `saves/` (if loading game)

---

## 📤 **JSON Response Schema**

### Complete Response Template
**Source:** Rules/Block_2.txt

```json
{
  "response": "string (main narrative in Russian)",
  "gm_thoughts_markdown": "structured NPC-scope + reasoning markdown string in Russian",
  
  // PLAYER CHARACTER
  "playerStatus": {
    "healthPercentage": "string (e.g., '85%')",
    "energyPercentage": "string (e.g., '60%')",
    "poisePercentage": "string (e.g., '100%')",
    "currentCondition": "string (e.g., 'Усталый')"
  },
  "currentPoiseChange": "integer (change in player's poise this turn)",
  "activeSkillChanges": "array of skill_change_objects",
  "removeActiveSkills": "array of skill_ids",
  "passiveSkillChanges": "array of skill_change_objects", 
  "removePassiveSkills": "array of skill_ids",
  "skillMasteryChanges": "array of mastery_change_objects",
  "playerActiveEffectsChanges": "array of effect_change_objects",
  "calculatedWeightData": "object with current weight calculations",
  "statsIncreased": "object with characteristic increases",
  "statsDecreased": "object with characteristic decreases",
  "currentEnergyChange": "integer (can be negative)",
  "currentHealthChange": "integer (can be negative)", 
  "moneyChange": "integer (can be negative)",
  "experienceGained": "integer",
  "playerEffortTrackerChange": "object with lastUsedCharacteristic + consecutivePartialSuccesses",
  "playerWoundChanges": "array of wound_change_objects",
  "customStateChanges": "array of custom_state_objects",
  "playerStealthStateChange": "object with stealth updates",
  "playerAppearanceChange": "string (new appearance description)",
  "playerImagePromptChange": "string (new image generation prompt)",
  "playerRaceChange": "string (new race if transformed)",
  "playerRaceDescriptionChange": "string (new race description)",
  "playerClassChange": "string (new class if changed)",
  "playerClassDescriptionChange": "string (new class description)",
  "playerAutoCombatSkillChange": "string (new auto-combat skill)",
  "playerCharacterNameChange": "string (new character name)",
  
  // INVENTORY MANAGEMENT
  "UpdateInventory": "array of inventory_command_objects",
  "inventoryItemsResources": "array of resource_change_objects",
  "updateItemTextContents": "array of item_text_update_objects",
  "moveInventoryItems": "array of item_movement_objects",
  "removeInventoryItems": "array of item_removal_objects", 
  "itemBondLevelChanges": "array of bond_change_objects",
  "itemFateCardUnlocks": "array of fate_card_objects",
  "addOrUpdateRecipes": "array of recipe_objects",
  "removeRecipes": "array of recipe_ids",
  "moveToLocationStorage": "array of storage_deposit_objects",
  "retrieveFromLocationStorage": "array of storage_withdrawal_objects",
  
  // WORLD STATE
  "currentLocationData": "object (full Location Object for a new location, or known-location shorthand with locationId + coordinates + lastEventsDescription)",
  "worldMapUpdates": "object with map changes",
  "worldEventsLog": "array of world_event_objects", 
  "UpdateRivalSoulArcs": "array of rival_soul_arc_objects for other souls' milestone-based destiny lines in the current mortal life",
  "worldStateFlags": "array of flag_objects",
  "removeWorldStateFlags": "array of flag_ids",
  "timeChange": "integer (minutes passed this turn)",
  "setWorldTime": "object with absolute time setting",
  "weatherChange": "object with tendency and description",
  "updateWorldProgressionTracker": "array of world_progression_objects",
  "updateFactionProgressionTracker": "array of faction_progression_objects",
  "progressionProcessingReport": "object with bounded processed cycle counts, optional afterlife catch-up proof, and new last-* markers",
  
  // QUEST SYSTEM
  "UpdateQuests": "array of quest_command_objects",
  "UpdateSoulQuests": "array of soul_quest_command_objects",
  "plotOutline": "object with mainArc, characterSubplots, loomingThreatsOrOpportunities, lastUpdatedTurn",
  "afterlifeStoryOutline": "object with mainArc, realmArc, actorSubplots, factionOrInstitutionArcs, loomingThreatsOrOpportunities, pendingRevelations, nextLikelySceneBeats, playerAgencyNotes, lastUpdatedTurn",
  
  // NPC SYSTEM
  "UpdateNPCs": "array of complete npc_objects",
  "NPCsRenameData": "array of npc_rename_objects",
  "NPCsInScene": "array of complete npc_objects",
  "NPCActiveSkillChanges": "array of npc_skill_change_objects",
  "NPCPassiveSkillChanges": "array of npc_skill_change_objects",
  "NPCSkillMasteryChanges": "array of npc_mastery_change_objects",
  "NPCPassiveSkillMasteryChanges": "array of npc_mastery_change_objects",
  "NPCEffectChanges": "array of npc_effect_change_objects",
  "NPCWoundChanges": "array of npc_wound_change_objects",
  "interNPCRelationshipChanges": "array of inter_npc_relationship_objects",
  "NPCRelationshipChanges": "array of npc_relationship_change_objects",
  "NPCRelationshipLockUpdates": "array of relationship_lock_objects",
  "NPCGoalUpdates": "array of npc_goal_objects",
  "NPCQuestUpdates": "array of npc_quest_objects",
  "NPCInventoryAdds": "array of npc_inventory_add_objects",
  "NPCInventoryUpdates": "array of npc_inventory_update_objects", 
  "NPCInventoryRemovals": "array of npc_inventory_removal_objects",
  "NPCEquipmentChanges": "array of npc_equipment_change_objects",
  "NPCInventoryResourcesChanges": "array of npc_resource_change_objects",
  "NPCMaskAdds": "array of npc_mask_add_objects",
  "NPCMaskUpdates": "array of npc_mask_update_objects",
  "NPCMaskRemovals": "array of npc_mask_removal_objects",
  "NPCActiveMaskChange": "array of npc_active_mask_objects",
  "NPCJournals": "array of npc_journal_objects",
  "npcInteractionJournalUpdates": "array of npc_interaction_journal_objects",
  "itemJournalUpdates": "array of item_journal_objects (mandatory itemId + itemName + entryToAppend; never send journalEntries fragments here)",
  "NPCUnlockedMemories": "array of npc_memory_objects",
  "NPCPersonalityTraitChanges": "array of npc_personality_objects",
  "NPCActivityUpdates": "array of npc_activity_objects",
  "completeNPCActivities": "array of npc_activity_completion_objects",
  "NPCFateCardUnlocks": "array of npc_fate_card_objects",
  "NPCCustomStateChanges": "array of npc_custom_state_objects",
  
  // COMBAT STATE
  "enemiesData": "array of complete enemy_objects",
  "alliesData": "array of complete ally_objects", 
  "combat_log_markdown": "string (detailed combat log in Russian)",
  
  // FACTION SYSTEM
  "factionDataChanges": "array of faction_change_objects",
  "factionRankChanges": "array of faction_rank_objects",
  "factionBonusChanges": "array of faction_bonus_objects",
  "factionResourceChanges": "array of faction_resource_objects",
  "factionProjectUpdates": "array of faction_project_objects",
  "completeFactionProjects": "array of faction_project_completion_objects",
  "factionCustomStateChanges": "array of faction_custom_state_objects",
  "factionChronicleUpdates": "array of faction_chronicle_objects",
  
  // META-GAME SYSTEM  
  "metaStateUpdates": "object with soul progression changes",
  "UpdateGuardians": "array of guardian_command_objects (see Guardian Commands below)",
  "guardianQuestProgressUpdates": "array of restricted Guardian active-quest progress updates; Mortal World may use this only to mark already accepted Guardian quests as active/ready_to_turn_in/failed/expired with non-physical proof",
  "guardians": "array of canonical guardian_objects when a contract explicitly requires full guardian-state authority",
  "activeGuardian": "canonical active guardian object or id-bearing object for guardian-state synchronization",
  "chaosSeaNavigation": "object with currentAbodeId and discoveredAbodes for afterlife navigation; [CHAOS_SEA_TRAVEL] target must already be in pre-turn discoveredAbodes and must also keep activeGuardian synced to the target guardian and target guardian abode.isDiscovered=true",
  "playerGuardianFoundationHistory": "array of player-founded guardian foundation receipt objects",
  "UpdateGuardianAbodeResidents": "array of guardian_abode_resident_objects",
  "UpdateGuardianAbodeResidentRosterReceipts": "array of guardian_abode_resident_roster_receipt_objects",
  "UpdateGuardianAbodeResidentInteractionReceipts": "array of guardian_abode_resident_interaction_receipt_objects",
  "UpdateGuardianAbodeResidentTransferReceipts": "array of guardian_abode_resident_transfer_receipt_objects",
  "UpdateGuardianAbodeResidentHistoryLog": "array of guardian_abode_resident_history_log_objects",
  "guardianThoughtJournalUpdates": "array of guardian_thought_journal_objects",
  "guardianSocialJournalUpdates": "array of guardian_social_journal_objects",
  "residentThoughtJournalUpdates": "array of guardian_abode_resident_thought_objects",
  "residentInteractionLogUpdates": "array of guardian_abode_resident_interaction_log_objects",
  "startGuardianProjects": "array of guardian_project_start_objects",
  "guardianProjectUpdates": "array of guardian_project_update_objects",
  "completeGuardianProjects": "array of guardian_project_completion_objects",
  "guardianPowerEvents": "array of guardian_abode_power_event_objects",
  "afterlifeArchiveUpdates": "array of afterlife_archive_update_objects",
  "playerBehaviorAssessment": "object with behavior analysis",
  "historyManipulationCoefficient": "number (0.0-2.0)",
  "characterChronicleUpdates": "array of character_chronicle_objects",
  
  // LORE CODEX SYSTEM
  "loreCodexUpdates": "array of lore_codex_command_objects (see Lore Codex section below)",
  
  // ACHIEVEMENT SYSTEM
  "achievementUnlocks": "array of achievement_unlock_objects (see Achievements section below)",

  // MATH ASSISTANT / МАТЕМАТИК
  "mathRequests": "array of deterministic calculation request objects for the local Math Assistant / Математик",
  "mathAudit": "array of deterministic calculation audit objects with formulaVersion = math_assistant_v1",
  
  // MISCELLANEOUS
  "UpdateVehicles": "array of vehicle_command_objects",
  "removeVehicles": "array of vehicle_ids",
  "activeVehicleChange": "string (vehicle_id or null)",
  "grantStorageAccess": "array of storage_access_objects",
  "revokeStorageAccess": "array of storage_revocation_objects", 
  "shareStorageAccess": "array of storage_sharing_objects",
  "multipliers": "array of exactly five numeric coefficients",
  "otherPlayersInteractions": "object keyed by playerId or array of player_interaction_objects",
  "setCharacteristics": "object with characteristic values",
  
  // UI ELEMENTS
  "image_prompt": "string (image generation prompt)",
  "dialogueOptions": "array of dialogue_option_objects",
  
  // LIFE CONTROL
  "TriggerLifeEnd": "object with mandatory reason and summary (Mortal World only; reason must be Death|Voluntary; triggers a separate Life Evaluation request after the turn is accepted)",
  "TriggerIncarnation": "object with mandatory worldDescription, characterDescription, circumstances (ordinary Chaos Sea lifecycle control, or Shining Abode pending-bootstrap handoff that preserves the existing preparedIncarnationPackage; GM writes only the trigger and the client performs Mortal bootstrap after accepting it)",
  "AscensionTrigger": "boolean (real Chaos Sea-only ascension transition; valid only if Enlightenment is ascension-ready: enlightenment.experience or soulProgression.totalExperience >= 60, or legacy max/Transcendence marker, and playerChoice=Ascension; must not be combined with TriggerLifeEnd)",
  "playerChoice": "string (required only with AscensionTrigger; must equal Ascension)"
}
```

### `progressionProcessingReport`

When any `progressionControl.mustEvaluate*` flag is true, or when `afterlifeCatchupRequired = true`, the response must include `progressionProcessingReport`. The report is distributed to `game_state/control/progression_report.json` and consumed by the client scheduler.

```json
{
  "sessionId": "string (copy from turn_request.json)",
  "requestId": "string (copy from turn_request.json)",
  "turnNumber": "integer (copy from turn_request.json)",
  "worldCyclesProcessed": "integer, optional; Mortal World only",
  "factionCyclesProcessed": "integer, optional; Mortal World only",
  "chaosSeaCyclesProcessed": "integer, optional",
  "guardianProjectCyclesProcessed": "integer, optional",
  "residentAgencyCyclesProcessed": "integer, optional",
  "shiningAbodeCyclesProcessed": "integer, optional",
  "shiningFactionCyclesProcessed": "integer, optional",
  "shiningTradeCyclesProcessed": "integer, optional",
  "newLastWorldSimulationTimeInMinutes": "integer, optional; Mortal World only",
  "newLastFactionSimulationTimeInMinutes": "integer, optional; Mortal World only",
  "newLastChaosSeaSimulationOrdinal": "integer, optional",
  "newLastGuardianProjectCycleOrdinal": "integer, optional",
  "newLastResidentAgencyCycleOrdinal": "integer, optional",
  "newLastShiningAbodeCycleOrdinal": "integer, optional",
  "newLastShiningFactionCycleOrdinal": "integer, optional",
  "newLastShiningTradeCycleOrdinal": "integer, optional",
  "afterlifeCatchupProcessed": "boolean, optional",
  "afterlifeCatchupSummaryEventsProcessed": "integer, optional"
}
```

For afterlife turns, processed counts must match the due contour counts. Each `newLast*Ordinal` must advance only its own contour; do not use the maximum pending backlog for all contours. If `afterlifeCatchupRequired = true`, set `afterlifeCatchupProcessed = true` and set `afterlifeCatchupSummaryEventsProcessed` to exactly `afterlifeCatchupSummaryEventsRequired`.

---

## 🗂️ **File System Architecture**

### Directory Structure
```
game_session/
├── input/                    # Input files (read-only for CLI)
│   └── turn_request.json     # Main turn request
├── game_state/               # Distributed game state
│   ├── core/                 # Core game state
│   ├── player/               # Player character data  
│   ├── inventory/            # Inventory management
│   ├── world/                # World state
│   ├── quests/               # Quest system
│   ├── npcs/                 # NPC management (14 files)
│   ├── combat/               # Combat state
│   ├── factions/             # Faction system (6 files)
│   ├── meta/                 # Meta-game progression (5 files)
│   ├── misc/                 # Miscellaneous systems
│   ├── control/              # Game flow control
│   └── history/              # Event history & logging
├── lore/                     # World and rule context
│   ├── chaos_sea/            # Meta-world lore
│   ├── current_world/        # Current incarnation world
├── mods/                     # Global system mods (one file = one mod; client toggles them on/off)
├── world_profiles/           # Reusable mortal-world templates (one file = one profile)
├── output/                   # Generated output
│   ├── narrative_response.json    # Main narrative
│   ├── interface_updates.json     # Optional UI updates for the current turn
│   └── debug_logs.json            # Debug information
├── ready/                    # Terminal signals
│   ├── turn_complete.json    # Turn terminal success signal
│   └── turn_error.json       # Turn terminal error signal
└── saves/                    # Save system
    ├── manual_saves/         # Player-initiated saves
    ├── autosaves/           # Automatic saves
    └── checkpoint_saves/     # Story milestone saves
```

### Core File Mappings

#### **NARRATIVE & CORE**
- Authoritative GM-authored outputs remain:
  - `output/narrative_response.json` ← `response`
  - `output/debug_logs.json` ← `gm_thoughts_markdown`
  - `output/interface_updates.json` ← optional `dialogueOptions`, `image_prompt` payload for turns that actually change the interface
- These `output/*.json` files are fresh per-turn transient artifacts for the current `sessionId/requestId/turnNumber`.
- Rewrite them for the current request only; never append cross-turn history there and never reuse stale payload from a previous turn.
- If a surface is unused for this turn, leave the corresponding `output/*.json` file absent instead of preserving old content.
- `game_state/core/player_status.json` ← `playerStatus`, `currentPoiseChange`
- `playerStatus` is flattened into the root of `game_state/core/player_status.json`; do not store it there as a nested `playerStatus` object.
- In Mortal World, `game_state/core/player_status.json` is a mandatory core file; accepted state is invalid if it is missing.
- `game_state/core/system_mods.json` ← client-authored manifest of global system mods
- Source mod files live in `game_session/mods/`; each file is one global system mod file.
- Only `game_state/core/system_mods.json.activeMods[]` is canonical for GM processing. Disabled files in `game_session/mods/` must be ignored.
- Active system mods are global highest-priority overlays on top of the normal rule stack.
- `game_state/control/incarnation_world_setup.json` ← client-authored pending world setup used before `TriggerIncarnation`
- `lore/current_world/world_directives.json` ← client/player-authored persistent world dossier for the active mortal world
- `game_session/world_profiles/*.json|txt|md` are reusable player-authored world templates; each file is one profile and may be selected or merged into pending world setup

#### **PLAYER CHARACTER** 
- `game_state/player/skills_active.json` ← `activeSkillChanges`, `removeActiveSkills`
- `game_state/player/skills_passive.json` ← `passiveSkillChanges`, `removePassiveSkills`
- `game_state/player/skill_mastery.json` ← `skillMasteryChanges`
- `game_state/player/effects.json` ← `playerActiveEffectsChanges`
- `game_state/player/weight_calc.json` ← `calculatedWeightData`
- `game_state/player/status_changes.json` ← `statsIncreased`, `statsDecreased`, `currentEnergyChange`, `currentHealthChange`, `moneyChange`
- `game_state/player/experience.json` ← `experienceGained`, `playerEffortTrackerChange`
- `game_state/player/wounds.json` ← `playerWoundChanges`
- `game_state/player/custom_states.json` ← `customStateChanges`
- `game_state/player/stealth.json` ← `playerStealthStateChange`
- `game_state/player/transformation.json` ← `playerAppearanceChange`, `playerImagePromptChange`, `playerRaceChange`, etc.

#### **INVENTORY SYSTEM**
- `game_state/inventory/items.json` ← `UpdateInventory`, `equipmentChanges`
- `UpdateInventory` is dual-shape:
  - new item: full Item Object with `existedId = null`
  - existing item update: partial object with `existedId` plus only the fields that changed this turn
- Do not use `UpdateInventory` to move an existing item between containers/locations. Use `moveInventoryItems` for relocation and keep `UpdateInventory` for the item's own property changes.
- `game_state/inventory/item_resources.json` ← `inventoryItemsResources`
- Canonical stored shape for `item_resources.json`: `entries[]` with item identity + `resource`, `maximumResource`, `resourceType`
- `game_state/inventory/item_text_updates.json` ← `updateItemTextContents`
- Canonical stored shape for `item_text_updates.json`: `entries[]` with item identity + `textContent[]`; incoming `updateItemTextContents[].textToAppend` is normalized into appended `textContent` entries after distribution
- `game_state/inventory/item_movements.json` ← `moveInventoryItems`
- `game_state/inventory/item_removals.json` ← `removeInventoryItems`
- `game_state/inventory/item_bonds.json` ← `itemBondLevelChanges`, `itemFateCardUnlocks`
- Canonical stored shape for `item_bonds.json`: `entries[]` with item identity + `ownerBondLevelCurrent` and any unlocked `fateCards`
- Fate Card unlock contract: `itemFateCardUnlocks` is only the unlock event signal; the same turn must also carry the resulting full updated Item Object in `UpdateInventory`. A partial existing-item patch is not sufficient for Fate Card unlock reporting.
- Bond/Fate Card reverse contract for existing items: if an already existing item changes `ownerBondLevelCurrent` or gains a newly unlocked Fate Card this turn, the resulting item state in `UpdateInventory` must be accompanied by the matching `itemBondLevelChanges` / `itemFateCardUnlocks` event. Direct state-only mutation is not sufficient.
- `game_state/inventory/recipes.json` ← `addOrUpdateRecipes`, `removeRecipes`
- `game_state/inventory/storage_operations.json` ← `moveToLocationStorage`, `retrieveFromLocationStorage`

#### **WORLD STATE**
- `game_state/world/current_location.json` ← `currentLocationData`
- `game_state/world/world_map.json` ← `worldMapUpdates`
- `worldMapUpdates` atomic commands include `newLocations`, `locationUpdates`, `storageUpdates`, `storagesToRemove`, `newLinks`, `linkUpdates`, `linksToRemove`, `threatsToAdd`, `threatsToUpdate`, `threatsToRemove`, `completeThreatActivities`
- `game_state/world/world_events.json` ← `worldEventsLog`
- `game_state/world/rival_soul_arcs.json` ← `UpdateRivalSoulArcs`
- `game_state/world/world_flags.json` ← `worldStateFlags`, `removeWorldStateFlags`
- `game_state/world/world_time.json` ← `timeChange`, `setWorldTime`
- `game_state/world/weather.json` ← `weatherChange`
- `game_state/world/progression.json` ← `updateWorldProgressionTracker`, `updateFactionProgressionTracker`
- `game_state/control/progression_report.json` ← `progressionProcessingReport`
- `game_state/control/progression_schedule.json` ← client-authoritative progression scheduler state (not GM-authored content)
- If an accepted GM turn changes `progression_schedule.json`, the client rejects the turn as a protocol violation.

World-location contract notes:
- `world_time.json` is intentionally mixed-shape in the current CLI runtime:
  - accepted-turn command surface may persist `timeChange` or `setWorldTime`
  - normalized absolute state may instead appear as `year`, `monthName`, `dayOfMonth`, `timeOfDay`, `currentTimeInMinutes`
  - the client/runtime reads both forms for compatibility
- `weather.json` is also dual-shape in the current CLI runtime:
  - it may be stored as direct weather fields (`tendency`, `description`, etc.)
  - or as the accepted-turn wrapper object under `weatherChange`
  - the client/runtime reads both forms for compatibility
- `currentLocationData` is dual-shape:
  - For a truly new current location, send the full Location Object.
  - For returning to a known location, the base shorthand is `locationId`, `coordinates`, and `lastEventsDescription`.
  - If the current turn must also update player-facing current-location substructures, the known-location shape may additionally carry `internalDifficultyProfile`, `externalDifficultyProfile`, and/or `locationStorages`.
- `eventDescriptions` is a read-only historical archive. Read it from location context if present, but do not emit it in `currentLocationData` or `worldMapUpdates`.
- For current-turn location history, write only `lastEventsDescription`.
- `rival_soul_arcs.json` is a life-scoped background-pressure surface for OTHER souls, not a player quest journal.
- Keep at most 1 active `major` arc and 1 active `minor` arc.
- If a hostile arc directly targets the player, it must leave at least two visible clues before direct collision or terminal harm.

#### **QUEST SYSTEM**
- `game_state/quests/regular_quests.json` ← `UpdateQuests`
- `game_state/quests/soul_quests.json` ← `UpdateSoulQuests`
- `game_state/quests/quest_history.json` ← `questLog` (legacy shorthand), canonical stored shape uses `questHistory`, `questRewards`, `questChains`
- `game_state/quests/plot_outline.json` ← `plotOutline`
- `game_state/meta/afterlife_story_outline.json` ← `afterlifeStoryOutline` (private afterlife Writer's Room; flexible GM plan, not player-visible text, not a forced prophecy)

Quest state contract notes:
- `regular_quests.json` and `soul_quests.json` are canonically stored as full `quests[]` arrays.
- On quest creation, write the full quest object including `detailsLog`.
- On incremental quest-log updates, rules still require `questId + newDetailsLogEntry` instead of resending the whole `detailsLog`; the client canonicalizes the saved quest file back into a full `detailsLog` array.
- Soul-quest objective statuses may use `Active`, `Pending`, `Completed`, or `Failed`; `Pending` is valid for future cross-incarnation subgoals that are not yet actionable in the current life.
- Soul quests may optionally carry `relatedAfterlifeResidentId` when they come from an afterlife resident in a Guardian's Abode.
- `quest_history.json` is canonically read as `questHistory`, `questRewards`, and `questChains`; legacy `questLog` remains accepted only as an input shorthand.

#### **NPC SYSTEM (14 FILES)**
- `game_state/npcs/npc_core.json` ← `UpdateNPCs`, `NPCsRenameData`, `NPCsInScene`
- `game_state/npcs/npc_skills.json` ← `NPCActiveSkillChanges`, `NPCPassiveSkillChanges`, etc.
- `game_state/npcs/npc_effects.json` ← `NPCEffectChanges`, `NPCWoundChanges`
- `game_state/npcs/npc_relationships.json` ← `NPCRelationshipChanges`, `interNPCRelationshipChanges`, etc.
- `game_state/npcs/npc_goals.json` ← `NPCGoalUpdates`, `NPCQuestUpdates`
- `game_state/npcs/npc_inventory.json` ← `NPCInventoryAdds/Updates/Removals`, `NPCEquipmentChanges`, etc.
- `game_state/npcs/npc_masks.json` ← `NPCMaskAdds/Updates/Removals`, `NPCActiveMaskChange`
- `game_state/npcs/npc_memory.json` ← `NPCUnlockedMemories`
- `game_state/npcs/npc_journals.json` ← `NPCJournals`
- `game_state/npcs/item_journals.json` ← `itemJournalUpdates`
- Incoming `itemJournalUpdates[]` command objects must use `itemId + itemName + entryToAppend`
- Do not send prebuilt `journalEntries` fragments through `itemJournalUpdates`; append only through `entryToAppend`
- Canonical stored shape for `item_journals.json`: `entries[]` with item identity + `journalEntries[]`
- `game_state/npcs/npc_personality.json` ← `NPCPersonalityTraitChanges`
- `game_state/npcs/npc_activities.json` ← `NPCActivityUpdates`, `completeNPCActivities`
- `NPCActivityUpdates` is for non-terminal changes to an already existing NPC activity.
- `completeNPCActivities` must target the NPC's currently active `currentActivity` from canonical `npc_core` state.
- Do not use `NPCActivityUpdates` or `completeNPCActivities` for a newly created same-turn NPC; a new NPC must start with `currentActivity = null`.
- `npc_activities.json` is a command / turn-news surface, not the canonical long-lived NPC activity store.
- Canonical persistent NPC activity state still lives in `npc_core.json` as `currentActivity` and `completedActivities`.
- `game_state/npcs/npc_fate_cards.json` ← `NPCFateCardUnlocks`
- `game_state/npcs/npc_custom_states.json` ← `NPCCustomStateChanges`

#### **COMBAT STATE**
- `game_state/combat/enemies.json` ← `enemiesData`
- `game_state/combat/allies.json` ← `alliesData`
- `game_state/combat/combat_log.json` ← `combat_log_markdown`

#### **FACTION SYSTEM**
- `game_state/factions/faction_core.json` ← `factionDataChanges`
- `game_state/factions/faction_structure.json` ← `factionRankChanges`, `factionBonusChanges`
- `game_state/factions/faction_resources.json` ← `factionResourceChanges`
- `game_state/factions/faction_projects.json` ← `factionProjectUpdates`, `completeFactionProjects`
- `game_state/factions/faction_custom.json` ← `factionCustomStateChanges`
- `game_state/factions/faction_chronicles.json` ← `factionChronicleUpdates`
- `faction_core.json` is a full faction-core surface: if you use `factionDataChanges`, send the complete updated faction object, not a partial patch fragment.
- Existing faction updates inside `factionDataChanges` must use the existing permanent `factionId`; `initialId` is only for a genuinely new same-turn faction.
- A genuinely new same-turn faction inside `factionDataChanges` must use `factionId = null`, a temporary `initialId`, `isNewFaction = true`, and a non-empty faction `image_prompt`.
- If a full faction object includes optional extension arrays such as `structuredBonuses`, `customStates`, `activeProjects`, or `completedProjects`, they must already be the complete canonical arrays for that faction, not shorthand fragments; the client projects them into the canonical faction sidecar files after distribution.
- `faction_structure.json` is canonically stored as `entries[]` with `ranks` and `structuredBonuses`; `factionRankChanges` / `factionBonusChanges` are accepted command shorthands that the client normalizes after distribution.
- `faction_resources.json` is canonically stored as `entries[]` with top-level `metaResources` / `strategicGoods`; each stored meta-resource must carry `resourceName`, `currentStockpile`, `incomePerCycle`, `upkeepPerCycle`, and each stored strategic good must carry `resourceName`, `currentStockpile`, `incomePerCycle`.
- `factionResourceChanges` is the shorthand for stockpile deltas.
- `faction_projects.json` is canonically stored as `activeProjects[]` and `completedProjects[]`; `factionProjectUpdates` is for non-terminal progress changes only, while `completeFactionProjects` is mandatory for `Completed` / `Abandoned`.
- `faction_custom.json` is canonically stored as `entries[]` with `customStates`; each stored custom state must remain a complete canonical Custom State Object, not an identity-only stub.
- `factionCustomStateChanges` is the shorthand input surface for isolated custom-state updates.
- If a turn already requires a complete faction-core object in `factionDataChanges`, that full object may instead carry the resulting full canonical `customStates` array for the faction.
- Existing factions in atomic faction commands must be targeted by permanent `factionId`.
- `initialFactionId` is reserved for a new same-turn faction that does not yet have a permanent `factionId`.
- `factionChronicleUpdates` is not one of those same-turn creation channels; it still requires the permanent `factionId` of an already materialized faction.
- If a new same-turn faction is authored only with `initialId`/`initialFactionId`, the client normalizes that temporary tag into the stored `factionId` inside canonical faction files for that accepted turn. Subsequent turns must target the faction by `factionId`.

#### **META-GAME SYSTEM**
- `game_state/meta/soul_state.json` ← `metaStateUpdates`, `afterlifeArchiveUpdates`, `archiveActionResolutions`
- `game_state/meta/afterlife_spiritual_conflict_state.json` ← `afterlifeSpiritualConflictUpdate` (afterlife-only spiritual conflict state: `activeConflict`, `recentConflicts[]`; never Mortal combat files)
- `game_state/meta/afterlife_entity_profiles.json` ← `afterlifeEntityProfileUpdates`, `afterlifeEntityCustomStateChanges`, `afterlifeFateCardUnlocks`, `afterlifeActorGoalUpdates`, `afterlifeActorQuestUpdates`, `afterlifeActorActivityUpdates`, `completeAfterlifeActorActivities`, `afterlifeRelationshipChanges`, `afterlifeRelationshipLockUpdates`, `afterlifeBreakthroughQuestUpdates`, `afterlifeActorMaskAdds`, `afterlifeActorMaskUpdates`, `afterlifeActorMaskRemovals`, `afterlifeActorActiveMaskChanges`, `afterlifeEntityProgressionOverrides`, `afterlifeSpecialArtLearningReceipts`, `afterlifeEntityProfiles` (Профили сущностей посмертия: `profiles[]` with `actorType`, `actorId`, `displayName`, realm/location, `currencies`, `progression`, `standardArts`, `specialArts`, `customStates`, `fateCards[]`, `goals`, `personalQuests[]`, `currentActivity`, `completedActivities[]`, `relationships[]`, `relationshipLock`, `breakthroughQuestId`, `redemptionQuestId`, `pointOfNoReturn`, `masks[]`, `activeMaskId`, `_true_self_`, `soulDissipationTier`, `targetStabilityCoefficient` proof context, `progressionStrategy`, `progressionLedger`, warnings, and `ledger`)
- `game_state/meta/afterlife_active_threats.json` ← `afterlifeThreatsToAdd`, `afterlifeThreatsToUpdate`, `completeAfterlifeThreatActivities`, `afterlifeThreatsToRemove` (persistent afterlife threats: canonical `threats[]` with `threatId`, `realm`, `scopeId`, `displayName`, `threatArchetype`, `intensity`, `currentActivity`, `impactProfile`, `visibleToPlayer`, optional `linkedFactionId`, optional `linkedGuardianId`, optional `sarefLink`, and `ledger[]`; complete live activity through `completeAfterlifeThreatActivities`, not raw nulling)
- `game_state/meta/chaos_sea_guardian_politics.json` ← `guardianPoliticalRelationUpdates`, `guardianPoliticalProjectUpdates`, `guardianPoliticalInfluenceUpdates`, `guardianPoliticalChronicleUpdates`, `completeGuardianPoliticalProjects` (Chaos Sea Guardian politics: canonical `relations[]`, `projects[]`, `influenceZones[]`, `chronicle[]`, `playerRole`, `sarefLinks[]`, and `openConflicts[]`; relation types include `alliance`, `rivalry`, `debt`, `fear`, `patronage`, `memory_oath`, `trade`, `hostility`, and `hidden_dependency`; player UI `/guardian_politics` shows known politics but hides `visibility=hidden|gm_only` and `isPlayerVisible=false` details; do not use Shining faction political memory or Mortal faction files)
- `game_state/meta/afterlife_chronicles.json` ← `afterlifeChronicleUpdates` (Внешняя память посмертия: canonical `chronicles[]` with `chronicleId`, `scopeType`, `scopeId`, `displayName`, read-only archive `eventDescriptions[]`, current-turn report `lastEventsDescription`, `persistentConsequences[]`, `openThreads[]`, and `lastUpdatedTurn`; valid `scopeType` values are `chaos_sea_region`, `shining_abode_district`, `guardian_abode`, `guardian_scene`, `resident_scene`, `faction_zone`, `memory_scene`, `source_of_light`, `saref_story`; do not use Mortal `worldEventsLog` / `currentLocationData`)
- `game_state/meta/afterlife_global_flags.json` ← `afterlifeGlobalFlagUpdates` (Глобальные флаги посмертия: canonical `flags[]` with `flagId`, `category`, `state=active|resolved|obsolete`, `visibility=visible|hidden|gm_only`, `createdAtTurn`, `updatedAtTurn`, `reason`, `evidence`, `linkedActors[]`, `linkedChronicles[]`, and `obsoleteReason` when obsolete; supported categories include `saref`, `source_of_light`, `guardian_memory`, `chaos_politics`, `shining_politics`, `soul_dissipation`, `realm_lifecycle`, `relationship_gate`; every update needs `gmThoughtsSummary`; hidden/gm_only flags must not leak into normal player UI; do not delete by full `flags[]` replacement and do not use Mortal `worldStateFlags`)
- `game_state/meta/main_story_saref_state.json` ← `sarefMainStoryState` or `sarefMainStoryUpdate` for hidden `Крылья над Бездной` state. `factionLinks.shadowTraces[]` stores omens by `shadow|name|faction`; `factionLinks.knownAgents[]` stores mixed `supporterArchetype=deceived|oathbound|fanatic|opportunist` agents and important-agent `interactionRoutes[]`. Hidden Shining Wings actors use `sarefFactionRole=wings_of_angels` and `sarefVisibility=hidden|rumored`; normal faction UI must not expose them until `sarefVisibility=revealed`. The `/сареф найти_крылья` pending contract closes only through `sarefMainStoryUpdate.mode=reveal_wings|refuse_wings|block_wings`, matching `requestId`, positive `resolvedAtTurn`, and `wingsInfiltration.status=revealed|refused|blocked`. Saref victory or deal scenes are recorded through `sarefMainStoryUpdate.mode=record_final_confrontation` into `finalConfrontation`: `status=resolved`, `directScene=true`, `resolvedAtTurn`, `routeType=combat|political|oath_law|metaphysical|hybrid|deal`, `victoryTier=pyrrhic|clean|deep|deal`, route proof for non-deal routes, `advantageUseIds[]`, `sarefOutcome`, and `wingsFactionOutcome`; `deep` needs broad Guardian preparation and Wings `broken|dissolved` outcomes must match Shining `factionLifecycle.state`. Deal endings require `sarefOutcome=allied`, `wingsFactionOutcome=joined`, `playerOathState.state=oathbound`, and `endings[].rewardBundle` fields `resourceReward`, `wingsAccess`, `sarefArt`, `sarefPassive`, `oathCost`; they also require `postStoryAgenda.state=oathbound_to_saref`, `assignments[]` linked to Shining `factionConflictCampaigns[]`, and `dominationScene.status=completed` once no significant non-Wings faction can oppose Saref. The deal route is not game over, and ordinary voluntary departure from Wings is invalid until a separate oath-break contract changes the oath. `sarefMainStoryUpdate.mode=record_oathbound_agenda` may update assignments and domination scene. `sarefMainStoryUpdate.mode=record_oath_break` writes `postStoryAgenda.oathBreakArc`; states are `not_started|active|failed|broken`, routes are `seret|lucian|ilarion|veyra|deep_story_evidence`, and a broken oath requires proof, `sceneType=oath_break` `advantageUseIds[]`, consequences `renegade_from_wings|oath_reversed|beloved_traitor|second_confrontation_unlocked`, and `playerOathState.state=broken|oath_reversed`. Victory endings require `antiOathProtection`, `antiForeignProtection`, `guardianRelationshipEffects[]`, plus `relic` or `passive`, and `deepWorldStateEffects[]` for deep. Saref defeat scenes are recorded through `sarefMainStoryUpdate.mode=record_defeat_outcome` into `defeatOutcomes[]`; supported `outcomeType` values are `forced_oath`, `exile_to_chaos_sea`, `memory_suppression`, `soul_dissipation`, and `pyrrhic_escape`, with `playerOathState`, `exileAudit`, `memorySuppressionAudit`, `soulDissipationProofId`, and `mitigation.mitigatedByAdvantages[]` required by outcome.
- `sarefMainStoryUpdate.mode=record_memory_scene` records playable quest-4 memory restoration. It writes `memoryScene` with `layer="Воспоминание"`, `role`, `boundaries[]`, 3-5 `abilities[]`, `requiredStoryNodes[]`, `successCondition`, `closureTarget`, and `resolvedAtTurn`; it also persists matching `guardianQuestlines[].questStates[].memorySceneProof` with `successConditionSatisfied=true`. This is not Mortal World and not `Memory Gates`, and it does not create `pendingMemoryLegacy` unless a separate Memory Gates action grants it. Evidence is non-physical only: memory, image, echo, knowledge trace, or soul resonance. Quest-4 `sarefRevelations[]` / `sarefAdvantages[]` require this proof.
- `game_state/meta/guardians.json` ← `UpdateGuardians`, `guardianPowerEvents`, `UpdateGuardianTradeInventoryReceipts`, and explicit canonical roots `guardians`, `activeGuardian`, `chaosSeaNavigation`, `playerGuardianFoundationHistory` when required by afterlife contract resolution
- `game_state/meta/guardians.json` ← `guardianQuestProgressUpdates` for the restricted Mortal World progress exception: only existing `activeQuests[]` may receive progress/status/evidence fields, and `ready_to_turn_in` must use memory/imprint/echo/proof fields rather than mortal inventory transfer
- `game_state/meta/guardian_abode_residents.json` ← `UpdateGuardianAbodeResidents`, `UpdateGuardianAbodeResidentRosterReceipts`, `UpdateGuardianAbodeResidentInteractionReceipts`, `UpdateGuardianAbodeResidentTransferReceipts`, `UpdateGuardianAbodeResidentHistoryLog`, `residentThoughtJournalUpdates`, `residentInteractionLogUpdates`
- `game_state/meta/guardian_projects.json` ← `startGuardianProjects`, `guardianProjectUpdates`, `completeGuardianProjects`
- `game_state/meta/guardian_project_journal.json` ← client-generated readable guardian project chronology
- `game_state/meta/abode_power_journal.json` ← client-generated readable guardian power chronology
- `game_state/meta/guardian_thought_journal.json` ← `guardianThoughtJournalUpdates`
- `game_state/meta/guardian_social_journal.json` ← `guardianSocialJournalUpdates`
- `game_state/meta/shining_abode_state.json` ← canonical Shining Abode state, Shining factions, halls, gates, radiance, Light Sparks, client-owned `treasury`, `shiningPoliticalActors`, Shining trade inventories, Shining receipts, prepared incarnation package. If a faction uses `leadership.headActorType = radiant_actor`, `leadership.headActorId` must resolve to an existing `shiningPoliticalActors[].actorId`. Each faction may carry `factionLifecycle.state = active|weakened|leaderless|broken|dissolved`; missing legacy lifecycle is treated as `active`, but new/changed factions should write it. Do not delete defeated factions from `factions[]`: `broken` and `dissolved` are historical/remnant states with `factionStrength=0`, `leadership.leadershipState=vacant`, no active `tradeInventory`, all projects `isSupported=false`, and defeat/remnant audit fields `defeatedAtTurn`, `defeatedAtUtc`, `defeatReason`, and `remnantsSummary`; `leaderless` also requires vacant leadership and blocks ordinary headed faction operations until leadership is restored. Shining faction political memory uses response fields `shiningFactionChronicleUpdates`, `shiningFactionInfluenceUpdates`, `shiningFactionStrategicMemoryUpdates`, and `shiningFactionResourceLedgerUpdates`; accepted-turn normalization writes faction `chronicle`, `territorialInfluence`, `strategicMemory`, and `resourceLedger`. Do not use Mortal `factionChronicleUpdates`, `factionDataChanges`, or `worldMapUpdates` for this.
- `game_state/meta/player_behavior.json` ← `playerBehaviorAssessment`, `historyManipulationCoefficient`
- `game_state/meta/character_chronicle.json` ← `characterChronicleUpdates`
- `game_state/meta/achievements.json` ← `achievementUnlocks`
- `game_state/meta/math_audit.json` ← `mathRequests`, `mathAudit`

#### **MATH ASSISTANT / МАТЕМАТИК**
- `mathRequests[]` is the GM-authored request surface for deterministic arithmetic that should be checked by the local client calculator. It is a calculation request only; it does not change game state by itself.
- `mathAudit[]` is the GM-authored calculation proof surface. It records the normalized expression, resolved numeric variables, raw result, rounded result, rounding mode, `formulaVersion = math_assistant_v1`, warnings, and optional `referencedBy[]` links to combat logs, reward audits, economy receipts, project reports, or other state surfaces that used the number.
- Both arrays are distributed to `game_state/meta/math_audit.json`.
- Supported expression syntax is deliberately small: numbers, variables, parentheses, `+`, `-`, `*`, `/`, and functions `min`, `max`, `clamp`, `round`, `floor`, `ceil`/`ceiling`, `abs`. Do not use `%`; percentages must be explicit variables with division by `100`.
- Supported rounding modes are `none`, `floor`, `ceiling`, `to_zero`, `away_from_zero`, and `to_nearest`. `decimalPlaces` is optional and must be `0..8`.
- `mathRequests[].applicationState`, if present, must be `requested_only`.
- `mathAudit[].applicationState` must be one of `calculated_only`, `applied_to_state`, or `mismatch_repair_blocking`.
- `calculated_only` means the number was calculated but not applied to state. `applied_to_state` means some other response/state surface actually used it. `mismatch_repair_blocking` means the GM saw a mismatch and intentionally leaves the turn blocked for repair; it is still a validation error, not silent acceptance.
- Manual totals must match the local Math Assistant result. A mismatched `expectedResult`, `rawResult`, or `result` fails closed with repair hints.
- For Mortal combat/status delta fields, if `mathAudit[].applicationState = applied_to_state` and `referencedBy[]` points to `currentHealthChange`, `currentPoiseChange`, or `currentEnergyChange`, then `mathAudit.result` must be the exact signed numeric change written to that response field. Example: a 13 damage hit to the player is `currentHealthChange: -13` and the audit result is also `-13`, not `13`.
- For afterlife combat, use Math Assistant for non-trivial GM-authored arithmetic that is copied into response state, especially `afterlifeSpiritualConflictUpdate.resolution.rewardAudit.finalAmount` and contested `diceAudit.margin` paths. It is optional for trivial one-step sums and unnecessary for client-owned calculations the GM never authors. If `referencedBy[]` names a supported afterlife numeric path, `mathAudit.result` must exactly equal that field; built-in afterlife validators still run separately.

```json
{
  "mathRequests": [
    {
      "requestId": "calc_discount_1",
      "purpose": "treasury exchange discount",
      "expression": "baseCost * discountPercent / 100",
      "variables": { "baseCost": 250, "discountPercent": 15 },
      "rounding": { "mode": "away_from_zero", "decimalPlaces": 0 },
      "expectedResult": 38,
      "applicationState": "requested_only"
    }
  ],
  "mathAudit": [
    {
      "auditId": "calc_discount_1",
      "requestId": "calc_discount_1",
      "purpose": "treasury exchange discount",
      "expression": "baseCost * discountPercent / 100",
      "normalizedExpression": "baseCost*discountPercent/100",
      "variables": { "baseCost": 250, "discountPercent": 15 },
      "rawResult": 37.5,
      "result": 38,
      "rounding": { "mode": "away_from_zero", "decimalPlaces": 0 },
      "formulaVersion": "math_assistant_v1",
      "applicationState": "applied_to_state",
      "referencedBy": [ "treasuryReceipt:exchange_1" ],
      "warnings": []
    }
  ]
}
```

#### **AFTERLIFE CONTROL / REQUEST FILES**
- `game_state/control/pending_abode_offering.json` ← client-authored Abode offering request; GM reads it as input only and always resolves through `guardianPowerEvents.reasonType = offering`. Only `offeringType = ink_feathers` is also `[INK_FEATHER_ACTION: ABODE_OFFERING]` and requires `output/ink_feather_action_result.json`; `soul_relic`, `archive_lore_fragment`, and `archive_secret_record` use plain `[ABODE_OFFERING]` and must not write an Ink Feather receipt.
- `game_state/control/pending_guardian_trade_request.json` ← client-authored Guardian trade inventory request; close with `UpdateGuardianTradeInventoryReceipts`.
- `game_state/control/pending_guardian_abode_residents_request.json` ← client-authored resident roster requests; close with `UpdateGuardianAbodeResidentRosterReceipts`.
- `game_state/control/pending_guardian_abode_resident_interactions.json` ← client-authored resident talk/history requests; close with `UpdateGuardianAbodeResidentInteractionReceipts` plus resident logs/history when accepted.
- `game_state/control/pending_guardian_abode_resident_transfers.json` ← client-authored Guardian Abode resident transfer requests; close with `UpdateGuardianAbodeResidentTransferReceipts`, matching history entries, and canonical resident source/target state.
- `game_state/control/pending_guardian_social_interactions.json` ← client-authored Guardian talk/lore social requests; close with `guardianSocialJournalUpdates` carrying matching `requestId`, `guardianId`, `interactionType`, and `status`.
- `game_state/control/pending_npc_social_interactions.json` ← MortalWorldProfile-only NPC social requests; requests may include an optional player-supplied `topic` that the Mortal accepted turn must answer explicitly before closing through `npcInteractionJournalUpdates`. In `Chaos Sea` / `Shining Abode`, treat the file as wrong-realm repair-only context and do not close through `npcInteractionJournalUpdates`, `UpdateNPCs`, or any `game_state/npcs/*` surface. Non-empty or malformed files block Soul Gates until repaired or resolved in Mortal World.
- `game_state/control/pending_player_guardian_foundation.json` ← Chaos Sea-only player-founded Guardian ritual. Process it only when the current realm is `Chaos Sea`, the soul has a non-empty name/founder identity, `shining_abode_state.json.availability = sealed_until_next_ascension`, `preparedIncarnationPackage` is null/absent, there is no active/malformed/wrong-reason/blocking `afterlife_return_guard.json`, no malformed/existing competing foundation request, and no existing founded Guardian for this soul. The request must echo `sourceShiningAvailability = sealed_until_next_ascension`. Close with `UpdateGuardians.create`, former-patron preservation, `activeGuardian`, `chaosSeaNavigation.currentAbodeId`, `soul_state.playerFoundedGuardianId`, `soul_state.playerGuardianFoundationStatus=founded`, and `playerGuardianFoundationHistory`.
- `game_state/control/system_guardian_attraction.json` ← client-owned deterministic Eternal Guardian attraction guard; valid only from ordinary `Chaos Sea`, not `Shining Abode`; resolve only to the requested preset Guardian, preserve `sourcePreset`, and do not substitute a similar Guardian or Mortal NPC.
- `game_state/control/pending_resident_companion_manifestation_request.json` ← MortalWorldProfile-only next-life companion manifestation requests. In `Chaos Sea` / `Shining Abode`, valid non-empty files are preserved as next-life context and do not block Soul Gates; malformed files are repair-only blockers. Do not materialize mortal NPCs, encounters, NPC journals, quests, or afterlife receipts from it.
- `game_state/control/pending_archive_consultation_request.json` and `pending_archive_project_fuel_request.json` ← close with `archiveActionResolutions`.
- `game_state/control/pending_shining_abode_actions.json` ← client-authored Shining core actions; file root is always `requests[]` and contains exactly one active request, not a GM-managed queue; do not write a singular root request property; close through canonical `shining_abode_state.json` mutation plus `shining_abode_state.coreActionReceipts[]` only in ordinary active `Shining Abode`. If this file appears while current realm is `Chaos Sea`, preserve it as wrong-realm repair-only context and do not resolve Shining state/receipts from the Chaos Sea turn. Supported receipt statuses are only `accepted`, `refused`, and `withdrawn`. Every receipt must echo exact `quotedCostFeathers` and `quotedCostLightSparks` from the request, including zero-cost actions; forge receipts also echo exact request mutation payloads (`replacementProperty` for retune, `addedProperties` for uplift). Supported `actionType` values are `discover_native_faction`, `invest_in_faction`, `complete_project`, `support_project`, `unsupport_project`, `retire_project`, `open_gates`, `prepare_incarnation_package`, `pull_relic_gacha`, `forge_relic.reshape`, `forge_relic.retune_property`, `forge_relic.strengthen_band`, `forge_relic.stabilize_echo`, and `forge_relic.uplift_rarity`; use `OtherGuides/Afterlife_Contract_Matrix.md` plus `Examples/E_CLI_Afterlife_Turns.txt` example 14 for accepted receipt/state patterns.
- For `complete_project`, `favoredArchetype` is a cost-only rule: matching archetype reduces the quoted completion cost by 5 Ink Feathers and 5 Light Sparks, but `strengthReward` is always tier-based (`tier 1 = 8`, `tier 2 = 12`, `tier 3 = 16`).
- `game_state/meta/shining_abode_state.json.pendingNativeFactionDiscovery` ← legacy state-local Shining discovery contract. If non-null, close it as legacy `discover_native_faction`: materialize the native hall/faction/residents/projects, spend only `costFeathers` from Soul, preserve current Light Sparks because `costLightSparks` was already reserved, append `coreActionReceipts[]` with exact legacy cost audit fields `quotedCostFeathers = costFeathers` and `quotedCostLightSparks = 0`, set `pendingNativeFactionDiscovery = null`, and do not create a duplicate `pending_shining_abode_actions.json`. The accepted closure has a constrained diff: do not mutate pre-existing halls, factions/projects, residents, political actors, or unrelated Soul state.
- `game_state/control/pending_shining_faction_foundings.json` ← close through `shining_abode_state.factionFoundingReceipts[]`; both pending request and receipt carry exact cost audit fields `quotedCostFeathers = 25` and `quotedCostLightSparks = 15`. A live client-authored request also carries `reservedInkFeathersBefore` and `reservedLightSparksBefore`; current `soul_state.inkFeathers.current` and `shining_abode_state.lightSparks` must equal those reserved balances minus the quoted costs.
- `game_state/control/pending_shining_faction_realignments.json` ← close through `shining_abode_state.factionRealignmentReceipts[]`.
- `game_state/control/pending_shining_faction_leadership_transitions.json` ← close through faction `leadershipReceipts[]` and leadership history.
- `game_state/control/pending_shining_trade_inventory_requests.json` ← close through faction `tradeInventory` plus `tradeInventoryReceipts[]` only in ordinary active `Shining Abode` (`currentRealm = Shining Abode`, `availability = active`, `preparedIncarnationPackage` null/absent); supports `requests[]`, but `(factionId, tradeCycleId)` is the uniqueness key and duplicate contracts for the same faction/cycle are invalid. In Chaos Sea, sealed Shining, pending-bootstrap handoff, or package fault, preserve it as wrong-realm/mode repair context and do not resolve Shining trade receipts.
- `game_state/control/pending_source_of_light_capstone.json` ← close only in ordinary active `Shining Abode` after full Radiance (`radiance.tier = 4`, `radiance.experience >= 580`) and no active/malformed afterlife pending/control contract, including no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`. The request is one direct object, not `requests[]`: `requestId`, `createdAtTurn`, `createdAtUtc`, `radianceExperienceAtRequest`, `radianceTierAtRequest`, `rewardPassiveId = light_incarnate`, `rewardRelicId = source_of_light_incarnated_light`. Close by writing a Source of Light scene, `shining_abode_state.json.sourceOfLightCapstone.completed = true`, `soul_state.afterlifeCombatProfile.capstones.lightIncarnate` (`light_incarnate` / `Воплощение Света`), and exactly one Soul Relic `source_of_light_incarnated_light` / `Воплощенный Свет`. This is not a Shining core action, has no `coreActionReceipts[]`, blocks Soul Gates and `/return_to_chaos_sea` while unresolved, and is one-per-soul.
- `game_state/control/pending_saref_wings_infiltration.json` ← client-owned `/сареф найти_крылья` / `/saref find_wings` request; valid only in ordinary active `Shining Abode` with enough `Крылья над Бездной` route fragments and no overlapping afterlife pending/control contract. The request is a direct object with `routeSafety=safe|risky|desperate`, `routeFragments[]`, `substituteFragments[]`, `availableAdvantages[]`, `disadvantages[]`, and `expectedResponseSurface=sarefMainStoryUpdate`. Close through `sarefMainStoryUpdate.mode=reveal_wings|refuse_wings|block_wings`; `reveal_wings` sets `revealStage=wings_revealed`, `wingsInfiltration.status=revealed`, `sceneType=wings_infiltration`, reveals `factionLinks.visibility`, and must make the linked `wingsFactionId` an actionable Shining faction with `sarefVisibility=revealed`. Risky/desperate routes require the GM to apply explicit disadvantages.
- `game_state/meta/shining_abode_state.json.factionConflictCampaigns[]` ← player-driven long campaigns against Shining factions started through `/фракции`; GM advances them only in ordinary active Shining by appending `breakthroughLog[]` proof and matching target `factions[].factionLifecycle` / leadership state on completion. Goals: `weaken`, `expose`, `depose_leader`, `break`, `dissolve`. Statuses: `active`, `breakthrough_ready`, `completed`, `failed`, `abandoned`. Breakthrough types: `exposure`, `duel_victory`, `defection`, `sabotage`, `resource_disruption`, `oath_break`, `trial`, `saref_directive`. Spiritual combat can prove `duel_victory`, but Mortal `factionDataChanges` must not be used.
- `game_state/control/afterlife_notifications.json` is client-owned; GM must not author inbox entries manually.
- `game_state/control/progression_schedule.json` is client-owned scheduler state; GM must not edit it directly. If `input/turn_request.json.progressionControl` requires afterlife work, write only `progressionProcessingReport` / `game_state/control/progression_report.json` with exact current turn correlation, processed counts, catch-up proof, and per-contour `newLast*Ordinal` markers.
- When a Shining pending/core contract is closed in the same accepted turn as a verified `progressionProcessingReport`, the scheduler allowance is limited to scheduler-owned Shining/resident/trade progression fields. It does not authorize unrelated `availability`, `coreActionReceipts[]`, `gates`, `gachaSystem.gachaHistory`, `pendingNativeFactionDiscovery`, `preparedIncarnationPackage`, `lightSparks`, `treasury`, or `sourceOfLightCapstone` unless that surface's own client-authored contract is also closed in the same turn.
- `game_state/world/guardian_corrections.json` is client-owned current-life Guardian correction state created during Mortal bootstrap from `next_life_scenario_core.json`, Guardian power/claims, and temporary `soul_preparation` project effects. In afterlife, do not create, edit, clear, or close it; in Mortal World, read it as explanation of compatible corrections already applied. Matching Abode Power spend events use `guardianPowerEvents.reasonType=correction_spend`, `sourceSurface=guardian_corrections`, and `sourceId=<correctionId>`.
- `soul_preparation` Guardian projects are next-life-only. Completed projects require `projectOutcomeAudit.preparationBudgetPoints`, `projectOutcomeAudit.preparationClaimPriorityBonus`, and `effectState.preparationBudgetPointsGranted` / `preparationBudgetPointsSpent` / `preparationClaimPriorityBonusGranted` / `consumedAtLifeStart`; sabotaged projects require `projectOutcomeAudit.hostilePriorityTokensGranted` and `effectState.hostilePriorityTokensGranted` / `hostilePriorityTokensSpent` / `consumedAtLifeStart`.

#### **AFTERLIFE SPIRITUAL CONFLICT**
- `afterlifeSpiritualConflictUpdate` is the only GM-authored response surface for afterlife duels/conflicts in ordinary `Chaos Sea` or ordinary active `Shining Abode`.
- Russian player/GM labels are fixed in `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`; keep canonical JSON keys and enum values in English.
- It writes `game_state/meta/afterlife_spiritual_conflict_state.json`, whose canonical root is `{ "schemaVersion": 1, "activeConflict": object|null, "recentConflicts": [] }`.
- Supported update modes are `start`, `exchange`, `resolve`, and `repair_cancel`.
- The explicit `[AFTERLIFE_SPIRITUAL_ACTION: conflictId]` tag is optional routing help, not a requirement for roleplay. If `activeConflict` already exists and the player's ordinary prose is a clear action inside that conflict, the GM should resolve it as `mode=exchange` or `mode=resolve`; if the tag is present, its `conflictId` must match the active conflict.
- Active conflicts are side-vs-side, not root-opponent based. Use `playerSide`, `oppositionSide`, `playerSideStrain`, `oppositionSideStrain`, `conflictPosition`, `controlState`, `resolutionState`, and `exchangeLog[]`. Do not use `opponent`, `playerStrain`, or `opponentStrain`.
- Supported side models are `direct_duel`, `assisted_duel`, and `champion_duel`. Each side has exactly one `leadContestant`; supporters are listed under `supporters[]`.
- Non-player lead contestants must include `actorArtTierSnapshot` and `artAuthoritySource` so Guardian/resident/radiant actor combat authority is explicit and not copied from the player.
- Supported operation types are `pressure`, `counter`, `guard`, `maneuver`, `binding`, `break_binding`, `force_binding`, `force_incarnation`, `incarnation_resistance`, `champion_coordination`, `recover_spiritual_power` / Собрать Средоточие, `withdraw`, `surrender`, and `negotiate`; supported outcomes are `success`, `partial_success`, `blocked`, `countered`, `setback`, and `no_effect`.
- A `blocked` exchange may leave before/after identical only when `incomingAction` states what was prevented. Otherwise exchanges should have a meaningful state delta or explicit `outcome=no_effect`.
- `controlState` is the canonical control / оковы axis, separate from strain and position. Missing or `null` means no active control for legacy entries. Active control must use `level=hindered|bound|locked`, `controllerSide=player|opposition`, non-empty `controlId`, `sourceOperation=binding|force_binding|force_incarnation|break_binding|incarnation_resistance|counter|guard|repair`, non-empty `restrictedOperations`, and `summary`; `sourceOperation` is not a free operation id.
- `actionEconomy` is the afterlife-only ОД pool, not Mortal HP/energy/stamina. Active conflicts with current exchanges carry `actionEconomy.player` and `actionEconomy.opposition` (`current`, `max`, `source`). On `mode=start`, `actionEconomy.player.current/max` comes from client-owned `soul_state.afterlifeCombatProfile.spiritFocusTier` / `Средоточие Души`: tier `0/1/2/3/4/5` gives max ОД `6/7/8/10/12/15`, with source `Средоточие Души tier N`. Every new/current exchange that spends or restores ОД must carry `actionCostAudit.player` (`operationType`, `baseCost`, `minCost`, `artTier`, `effectiveCost`, `before`, `after`) with `effectiveCost = max(minCost, baseCost - artTier)`. Terminal/free player operations (`withdraw`, `surrender`, `negotiate`) must not include `actionCostAudit.player`; they cannot mutate player ОД through fake audit. Every new/current exchange that resolves an active costed opposition operation must also carry `actionCostAudit.opposition` in the same shape, even if the player's own operation is terminal/free (`withdraw`, `surrender`, `negotiate`); the opposition operation is taken from `incomingAction.finalOperationType` when present; otherwise use `incomingAction.operationType` or the matching `matchupAudit.oppositionOperation`. `finalOperationType` is authoritative: do not fall back to an earlier/stale `incomingAction.operationType` for `matchupAudit`, `actionCostAudit.opposition`, or non-player special-art costs. `actionCostAudit.player.artTier` is checked against validated pre-turn authority, not trusted from GM output: standard actions use `soul_state.afterlifeCombatProfile.artTiers`, and player-owned named special arts use the learned special arts in `afterlife_entity_profiles.json`. `actionCostAudit.opposition.artTier` is checked against the validated pre-turn `afterlife_entity_profiles.json` profile for the opposition lead actor, with the pre-turn `oppositionSide.leadContestant.actorArtTierSnapshot` used only as compatibility fallback. The final `activeConflict.actionEconomy.player.current` and `.opposition.current` must equal the last current `actionCostAudit.player.after` and `.opposition.after`; if a side has no current `actionCostAudit.<side>`, its final `current` must remain equal to the validated pre-turn active conflict. Base/min costs: `pressure 3/1`, `guard 2/1`, `counter 4/2`, `maneuver 3/1`, `binding 4/2`, `force_binding 5/2`, `break_binding 3/1`, `incarnation_resistance 3/1`, `champion_coordination 2/1`, `recover_spiritual_power 0/0`. If the exchange uses a special art, use either `specialArtAudit` for one used art or `specialArtAudits[]` when player and opposition both use named special arts in one exchange; never write both fields on one exchange. Each audit object must match the validated pre-turn profile of `ownerActorType/ownerActorId`. Only player-owned special arts that power the player's exchange operation multiply the player's action cost: `actionCostAudit.player.specialCostMultiplierPercent`, `specialArtId`, and `standardEffectiveCost` must prove the higher `effectiveCost`. Non-player or incoming-action special arts keep the player's ordinary ОД cost, but when they power the opposition operation they must belong to the resolved opposition actor from `incomingAction` or `oppositionSide.leadContestant`; `actionCostAudit.opposition.artTier` is the matching `specialArts[].tier`, and `actionCostAudit.opposition` requires `specialArtId`, `specialCostMultiplierPercent`, `standardEffectiveCost`, and the multiplied `effectiveCost`. A non-player `specialArtAudit` is invalid unless its `baseOperation` matches the single resolved opposition operation used for `actionCostAudit.opposition`; do not bind it to the player's `exchange.operationType` or an earlier `incomingAction.operationType` when `matchupAudit.oppositionOperation` / `finalOperationType` selected another costed action.
- Spiritual Arts operation rules are part of the contract: `pressure` primarily changes `oppositionSideStrain` and must not create control; `guard` protects `playerSideStrain` / consequence, may block new control, does not remove existing control, and even on setback against direct `pressure` limits `playerSideStrain` worsening to at most one rank; `counter` requires `incomingAction` and a measured payoff on `success`/`partial_success`/`countered` (non-empty `counterPayoff`, improved `conflictPosition`, worsened `oppositionSideStrain`, or weakened/reversed existing opposition `controlState`; it cannot create fresh player control from none); `maneuver` changes `conflictPosition` and must not directly change side strain or bypass active opposition `controlState`; if opposition `controlState.restrictedOperations` lists the attempted operation, that operation cannot succeed until the control is answered; `binding` / `force_binding` requires advantage, setup, or `decisive_player_success` and must create/strengthen player `controlState` only when no active opposition control remains; failed binding/force_binding outcomes (`blocked`, `countered`, `setback`) leave `controlState` unchanged on both sides, including player-control rewrites and opposition anti-control deltas; `force_binding` requires strong leverage and must restrict at least two distinct operations; `break_binding` requires a binding/coercive context and must weaken/remove/reverse opposition `controlState`; same-level narrowing of opposition `restrictedOperations` counts as weakened `controlState`, while equal/reordered sets do not count; `incarnation_resistance` is limited to `force_incarnation` / `guardian_forced` control and must not clear ordinary binding control; failed incarnation_resistance outcomes leave forced-incarnation `controlState` unchanged; `champion_coordination` is limited to `champion_duel`; `recover_spiritual_power` restores ОД up to max (+3 success, +2 partial_success, +0..1 when punished).
- New/current contested exchanges with `diceAudit` must include `matchupAudit` (`playerOperation`, `oppositionOperation`, `primaryResolutionLane`, `riskProfile`, `matchupRationale`). If `incomingAction.finalOperationType` is present, `oppositionOperation` must match that final operation, not stale `incomingAction.operationType`; otherwise it must match `incomingAction.operationType`. If an active conflict already has active `controlState`, or the current exchange creates/changes active `controlState`, the exchange must explicitly include both `before.controlState` and `after.controlState`; use `null` or `{ "level": "none" }` to record no active control instead of omitting the field. The tactical matrix is mechanical: pressure beats maneuver/passive repositioning by worsening opposition strain; guard beats pressure by safely reducing or preventing player-side strain and by capping setback harm against pressure; counter beats a named incoming direct action but is risky and must record a downside on setback; maneuver beats passive guard by improving position but is stopped by pressure, opposing maneuver, or control; binding needs leverage and cannot answer active opposition control; force_binding needs stronger leverage and broader restrictedOperations than binding; break_binding answers binding/coercion; incarnation_resistance answers forced incarnation only; champion_coordination is only for champion duels; recover_spiritual_power is strong against guard/counter/passive timing and weak against pressure, maneuver, binding, force_binding, and force_incarnation.
- Contested afterlife conflict results are not GM-fiat. Every contested `mode=exchange` must write `exchange.diceAudit`; every contested `mode=resolve` that decides victory/loss, surrender under pressure, concession under pressure, or Guardian-forced incarnation proof must write `resolution.diceAudit`.
- `diceAudit` reads only `input/turn_request.json.preGeneratedDices1d20`, never `gachaBaseResult`, hidden randomness, Mortal combat rolls, or prose preference. Use two visible d20 entries, one for `playerSide` and one for `oppositionSide`, and record exact zero-based `sourceIndex` values in `diceUsed[]`.
- Преимущество / Помеха uses `diceAudit.rollMode.<side>` with `effectiveMode=normal|advantage|great_advantage|disadvantage|dire_disadvantage`, `advantageSources[]`, and `disadvantageSources[]`. `advantage` / Преимущество consumes exactly 2d20 and selects the highest; `great_advantage` / Великое Преимущество consumes exactly 3d20 and selects the highest; `disadvantage` / Помеха consumes exactly 2d20 and selects the lowest; `dire_disadvantage` / Тяжкая Помеха consumes exactly 3d20 and selects the lowest. Sources may be legacy strings or objects with `summary`/`source`/`sourceId`, `level`, and optional `sourceType`; only the strongest positive and strongest negative source are used, same-direction sources do not stack, and cancellation is stepwise. Critical success/failure applies only to the selected die, never to discarded dice. Full `guard` success against direct pressure creates `tempoAdvantage`; the next eligible non-terminal combat action consumes it through `rollMode.player.advantageSources[]` with `sourceType=guard_tempo_window` or explicitly expires/clears it.
- If `game_state/core/game_settings.json.difficulty` is readable, every new/current contested `diceAudit` must include `difficultyAudit`: `difficulty`, `source="game_state/core/game_settings.json.difficulty"`, `oppositionModifier`, and `rewardMultiplierPercent`. Difficulty table: `normal` / Нормальная => opposition `+0`, reward `100%`; `hard` / Тяжёлая => opposition `+1`, reward `125%`; `impossible` / Невозможная => opposition `+2`, reward `150%`. Add exactly the matching `game_difficulty` value to `modifierBreakdown.opposition[]`; never add a player-side difficulty modifier. The difficulty bonus is deliberately smaller than position dominance `+4` and `light_incarnate` lead `+8`, so progression and tactical choices remain stronger than difficulty/dice.
- Canonical `diceAudit` shape is `{ "formulaVersion": "afterlife_spiritual_conflict_v1", "diceSource": "input/turn_request.json.preGeneratedDices1d20", "diceUsed": [{ "side": "player", "sourceIndex": 0, "sides": 20, "value": 14, "selection": "selected" }, { "side": "opposition", "sourceIndex": 1, "sides": 20, "value": 9, "selection": "selected" }], "rollMode": { "player": { "effectiveMode": "normal", "advantageSources": [], "disadvantageSources": [] }, "opposition": { "effectiveMode": "normal", "advantageSources": [], "disadvantageSources": [] } }, "playerTotal": 18, "oppositionTotal": 17, "margin": 1, "outcomeBand": "mixed_or_no_effect", "modifierBreakdown": { "player": [], "opposition": [] } }`. `margin` is always `playerTotal - oppositionTotal`; narrated outcome, strain changes, `conflictPosition`, and terminal proof must match it.
- `exchange.before.conflictPosition` is mandatory whenever an exchange carries `diceAudit`; do not omit it to avoid position accounting. Non-`contested` `before.conflictPosition` must be listed as exactly one explicit `conflict_position` modifier with exact matching `position`: `player_advantaged/player_dominant` give player +2/+4, `opposition_advantaged/opposition_dominant` give opposition +2/+4. Do not split or duplicate this bonus across multiple entries, and do not omit or blank the modifier `position`. `contested` means zero `conflict_position` entries.
- Natural 20/1 is bounded critical logic, not arbitrary fiat, and bounded criticals are symmetric. A favorable critical for the player (player natural 20 or opposition natural 1) raises a worse margin result only to ordinary `player_success`; an unfavorable critical for the player (player natural 1 or opposition natural 20) lowers a better margin result only to ordinary `opposition_success`; opposed natural criticals cancel back to the margin band. Criticals never create decisive results by themselves. If this changes the margin-derived band, add `diceAudit.criticalResult` with `playerNaturalRoll`, `oppositionNaturalRoll`, `marginOutcomeBand`, `normalizedOutcomeBand`, `scaleLimit`, and `narrativeConstraint`; the narration must stay plausible for the current action and relative side strength.
- Outcome bands for `afterlife_spiritual_conflict_v1`: `margin >= 8` => `decisive_player_success`; `3..7` => `player_success`; `-2..2` => `mixed_or_no_effect`; `-7..-3` => `opposition_success`; `<= -8` => `decisive_opposition_success`.
- `soul_state.afterlifeCombatProfile` is the soul-owned profile for `enlightenmentRank`, `radianceRank`, `retainedRadianceRank`, `artTiers`, and `spiritFocusTier`; it is not Mortal XP/skills/combat state. `/spiritual_arts` is the only client-owned upgrade surface for `artTiers`, `Средоточие Души`, and learned special arts / изученные особые духовные искусства in the player profile; the GM reads these values but must not author upgrade receipts/reports or directly mutate `spiritFocusTier` or player special-art tiers. Learned special art `upgradeCost` must be non-empty, use only `inkFeathers`/`lightSparks`, include at least one positive value, and may be Ink-only, Spark-only, or mixed; Spark-only upgrades are usable only in ordinary active Shining Abode. Spiritual Art tiers reduce action costs; `spiritFocusTier` increases max ОД.
- A resolved contested player victory may grant one small validated afterlife currency reward through `recentConflicts[].rewardAudit`. Chaos Sea rewards are `currency="ink_feathers"` and must be mirrored by `metaStateUpdates.inkFeatherChanges.add`; ordinary active Shining Abode rewards are `currency="light_sparks"` and must be mirrored by the exact `shining_abode_state.json.lightSparks` increase. `rewardAudit` fields are mandatory when a conflict grants currency: `realm`, `currency`, `baseAmount`, `opposingLeadStrength`, `sideModel`, `startingConflictPosition`, `challengeTier`, `outcomeMultiplierPercent`, `riskMultiplierPercent`, `riskReason`, `difficultyAudit` when readable `game_settings` difficulty exists, `finalAmount`, `narrativeReason`. For a current-turn reward that resolves the validated pre-turn `activeConflict`, the formula inputs are not GM-free: `sideModel` must match pre-turn `activeConflict.sideModel`, `startingConflictPosition` must match pre-turn `activeConflict.conflictPosition`, and `opposingLeadStrength` must be max standard art/authority value in pre-turn `oppositionSide.leadContestant.actorArtTierSnapshot` + 1. Formula is deterministic: Chaos base `10`, Shining base `1`; `challengeTier` comes from opposing lead strength + side model + starting position; outcome multiplier is `100` for `player_success`, `150` for `decisive_player_success`; risk multiplier is `150/125/100/75/50` for `opposition_dominant/opposition_advantaged/contested/player_advantaged/player_dominant`; difficulty multiplier is `100/125/150` for `normal`/`hard`/`impossible`; `finalAmount = clamp(baseAmount * challengeTier * outcomeMultiplierPercent * riskMultiplierPercent * difficultyAudit.rewardMultiplierPercent / 1_000_000, 0, realmCap)`; cap is `120` Ink Feathers or `8` Light Sparks. No reward is allowed for `repair_cancel`, `no_effect`, voluntary withdrawal/surrender, pure negotiation/no-contest closure, duplicate reward for the same `conflictId`, invalid realm, wrong currency, XP, money, Mortal items, Mortal skills, Radiance, or Enlightenment.
- Voluntary `TriggerIncarnation` remains the normal Soul Gates/handoff path. Guardian-forced incarnation is coercive: the GM must either use the legacy explicit provocation path, or first resolve a spiritual conflict where the player side loses, surrenders, or concedes, then write `TriggerIncarnation.source=guardian_forced` as the lifecycle consequence. The conflict proof must be in current-turn `recentConflicts[]` with `mode=resolve`, `resolutionState=resolved`, `resolvedAtTurn=<current turn>`, matching `guardianId`, `operationType=force_incarnation`, and `playerOutcome=lost|surrendered|conceded` or matching `resolutionKind`.

#### **AFTERLIFE ENTITY PROFILES**
- `game_state/meta/afterlife_entity_profiles.json` is the canonical afterlife actor profile sidecar for Профили сущностей посмертия.
- The GM writes `afterlifeEntityProfileUpdates[]` when a significant afterlife actor is created, revealed, or materially changes. The client normalizes updates into canonical `profiles[]` by unique `actorType + actorId/actorRef`.
- The GM writes `afterlifeEntityCustomStateChanges[]` when only profile `customStates[]` need to change. Each entry targets `actorType + actorId/actorRef`; the target profile must already exist in `profiles[]` or the same-turn `afterlifeEntityProfileUpdates[]`, otherwise the command is repair-blocking. `statesToAddOrUpdate[]` carries full custom state objects with `stateId`, current/min/max values, `description`, `progressionRule`, and `thresholds`; `statesToRemove[]` explicitly deletes ended states. At least one of these arrays must be non-empty.
- The GM writes `afterlifeFateCardUnlocks[]` only when a Guardian/afterlife actor's `fateCards[]` entry is actually unlocked by current story evidence. Canonical `fateCards[]` entries use `cardId`, `nameRu`, `status=locked|hidden|available|unlocked`, `unlockConditions`, `storyMeaning`, and, after unlock, `appliedAtTurn`, `evidence`, plus at least one active mechanical effect array: `guardianEffects`, `playerUnlocks`, `politicalEffects`, `combatEffects`, or `trainingUnlocks`. Locked/hidden/available cards must not grant training, scene, passive, political, or combat effects. Secret cards may hide conditions from player-facing UI, but the canonical profile still stores enough GM contract data.
- The GM writes actor agency only through `afterlifeActorGoalUpdates[]`, `afterlifeActorQuestUpdates[]`, `afterlifeActorActivityUpdates[]`, and `completeAfterlifeActorActivities[]`. Canonical profiles store this as `goals`, `personalQuests[]`, `currentActivity`, and `completedActivities[]`. Goals require `goalId`, `shortTermGoal`, `longTermGoal`, `plan`, `gmThoughtsSummary`, and `updatedAtTurn`; personal quests require `questId`, `goalId`, `title`, `status=active|blocked|completed|failed|cancelled`, `planSummary`, `successCondition`, and `createdAtTurn`; current activity requires `activityId`, `goalId`, `linkedQuestId`, `activityType`, `summary`, `status=active`, `gmThoughtsSummary`, and `startedAtTurn`. `currentActivity.goalId` must match `goals.goalId`, and `linkedQuestId` must point to an active personal quest. Close the current activity only with `completeAfterlifeActorActivities[]` and an `outcome=completed|failed|cancelled|blocked`, `completedAtTurn`, `summary`, and optional `resultingQuestStatus`; do not directly erase it or leave actor reasoning implicit.
- The GM writes afterlife actor masks only through `afterlifeActorMaskAdds[]`, `afterlifeActorMaskUpdates[]`, `afterlifeActorMaskRemovals[]`, and `afterlifeActorActiveMaskChanges[]`. Canonical profiles store `masks[]` and `activeMaskId`; `activeMaskId` must be `_true_self_` or an existing `masks[].maskId`, and `_true_self_` is required instead of `null` when returning to the true self or removing the active mask. Each mask needs `maskId`, `displayName`, `publicArchetype`, `visiblePersonality`, hidden `concealedTruth`, `directives[]`, `revealConditions[]`, and `deceptionRisk=low|medium|high|critical`, with optional `linkedThreatId` / `linkedSarefAgentId`. Ordinary player UI must not reveal `concealedTruth`, `directives`, `linkedThreatId`, or `linkedSarefAgentId` until `isRevealed=true`; do not use Mortal `NPCMaskAdds` / `NPCActiveMaskChange`.
- If the GM does not write `afterlifeEntityProgressionOverrides[]`, the client deterministically applies entity progression only from the current-turn `progression_report.json` whose `sessionId/requestId/turnNumber` match the current request: income is added, one affordable `progressionStrategy.priorityOrder` upgrade is bought, `lastAutoProgressionCycleKey` is set, and `progressionLedger[]` records income/spending/upgrades. Stale or missing-context reports are ignored for this auto-progression. This is per profile realm: Chaos Sea profiles consume Chaos/Guardian/Resident contours and receive Ink Feather income only; Shining Abode profiles consume Shining contours and receive Ink Feather + Light Spark income. `progressionStrategy.priorityOrder` may target standard art ids, a profile `specialArts[].artId`, `enlightenment`, `radiance`, or `soul_dissipation`. Strategy constraints are executable: `resourceReserve` is a hard minimum remaining balance, `allowedSpends` is an allow-list of spend categories, and `forbiddenSpends` is a deny-list; valid categories are `standardArts`, `specialArts`, `enlightenment`, `radiance`, and `soulDissipationTier`. If both lists are present, the allow-list is applied first and the deny-list can still block the spend. Overrides must include `cycleKey`, `reason`, `summary`, and explicit deltas; supported groups include `currencyDeltas`, `standardArtTierDeltas`, `specialArtTierDeltas`, `soulDissipationTierDelta`, and `progressionExperienceDeltas`. Every `specialArtTierDeltas` key must match an existing `specialArts[].artId` on the target profile; unknown ids are repair-blocking, not no-op.
- Chaos Sea profiles must keep `currencies.lightSparks = 0`. Do not add Light Sparks to Chaos Sea profiles through `afterlifeEntityProfileUpdates[]`, full canonical `profiles[]`, or `afterlifeEntityProgressionOverrides[].currencyDeltas`; only ordinary active Shining Abode profiles may hold/use Light Sparks.
- `afterlifeEntityProgressionOverrides[].currencyDeltas` supports only `inkFeathers` and `lightSparks`; `progressionExperienceDeltas` supports only `enlightenment` and `radiance`. Delta objects must be non-empty; unsupported keys or non-integer values are repair-blocking instead of being ignored.
- Required profile fields: `actorType`, `actorId` or `actorRef`, `displayName`, `realm`, optional location fields, `currencies.inkFeathers`, `currencies.lightSparks`, `progression.enlightenment`, `progression.radiance`, `standardArts`, `specialArts`, `customStates`, `fateCards`, `soulDissipationTier`, `progressionStrategy`, warnings, and `ledger`.
- `standardArts` are the normal spiritual actions (`pressure`, `guard`, `counter`, `maneuver`, `binding`, `force_binding`, `break_binding`, `incarnation_resistance`, `champion_coordination`, `recover_spiritual_power`) with tier `0..5`. The player soul profile identity is reserved as `actorType=player_soul` plus `actorId/actorRef=player_soul`; no non-player profile may use `actorId=player_soul`. `specialArts[]` are named variants with `artId`, Russian `displayName`, `ownerActorType`, `ownerActorId`, `baseOperation`, `tier`, `costMultiplierPercent`, `upgradeCost`, `effectSummary`, `canTeachPlayer`, and `trainingConditions`; `ownerActorType/ownerActorId` must match the enclosing profile's `actorType/actorId`; `baseOperation` must name one of those actions. `upgradeCost` may contain only `inkFeathers` and `lightSparks`, must not be empty, and must have at least one positive amount; `upgradeCost.inkFeathers` may be `0` only when `upgradeCost.lightSparks` is positive; the client blocks that Spark-only learned-art upgrade outside ordinary active Shining Abode. If a special art effect is used in a conflict, the GM must include `specialArtAudit.effectNote` or, when multiple sides use special arts in one exchange, `specialArtAudits[].effectNote`; every audit must match the pre-turn owner profile. Player-owned special arts that power the player's exchange operation require `actionCostAudit.player.specialArtId`, `specialCostMultiplierPercent`, `standardEffectiveCost`, and multiplied `effectiveCost`; non-player/incoming special arts that power the opposition operation require the same fields under `actionCostAudit.opposition`, must be owned by the current opposition actor, and use the special art `tier` for `actionCostAudit.opposition.artTier`. Do not attach a non-player special art to the player's own `exchange.operationType`, to an earlier `incomingAction.operationType`, or to any other stale candidate; its `baseOperation` must match the resolved opposition operation used for `actionCostAudit.opposition`.
- Current/new teachable `specialArts[]` also use `specialArts[].combatEffect` as the first-class afterlife-only combat contract beside `effectSummary`. Required string fields are `summary`, `trigger`, `mechanicalAxis`, `allowedPayoff`, `limit`, and `auditRequirement`. `mechanicalAxis` is limited to legal afterlife spiritual-conflict surfaces such as `rollMode`, `conflictPosition`, `controlState`, `sideStrain`, `tempoAdvantage`, `counterPayoff`, `actionEconomy`, `actionCostAudit`, and `combatConditions`; it must preserve `baseOperation` and must not describe Mortal HP/status, passive unlimited stacking, or tactical-matrix bypasses. When the GM uses the art, `specialArtAudit.effectNote` / `specialArtAudits[].effectNote` must cite the trigger and applied legal payoff. Legacy persisted profiles with only `effectSummary` remain loadable/readable.
- `afterlifeSpecialArtLearningReceipts[]` is the GM recognition surface for teaching the player: `receiptId`, `teacherActorType`, `teacherActorId`, `playerActorId`, `artId`, `learnedAtTurn`, `trainingConditionSatisfied=true`, `roleplayEvidence`, and `summary`. The teacher profile, player profile, and teacher `specialArts[].artId` must exist in profile authority, and the source art must have `canTeachPlayer=true`; otherwise the receipt is repair-blocking instead of being ignored. Learned special arts always start at tier 0; `initialTier` must be omitted or exactly `0` and never grants progression.
- `afterlifeRelationshipChanges[]`, `afterlifeRelationshipLockUpdates[]`, and `afterlifeBreakthroughQuestUpdates[]` are the afterlife-only relationship gate surfaces. Canonical `relationships[]` entries use `relationshipId`, `axis=trust|romance|rivalry|oath|fear|reverence|debt`, target actor identity, `value`, `relationshipTier`, optional `relationshipLock`, and `relationshipGateQuests[]`. Important thresholds are `value >= 50` for positive breakthrough and `value <= -50` for redemption/point-of-no-return. A positive lock needs `relationshipLock.lockState=positive_locked`, `breakthroughQuestId`, `reason`, `evidence`, and `gmThoughtsSummary`; a negative lock needs `redemptionQuestId`; `pointOfNoReturn` is valid only with explicit proof. Completing a breakthrough/redemption quest clears the linked quest id only by writing `breakthroughQuestId="_clear_"` or `redemptionQuestId="_clear_"`. These scenes must be meaningful narrative tests, not routine fetch chores, and must not use Mortal `NPCRelationshipChanges`.
- `soulDissipationTier` is informational and dangerous: if it is above zero, the profile should clearly warn that the entity can potentially kill/disperse souls after victory when its motives allow it. Whether the entity chooses to do so remains roleplay/contract context, not automatic execution. The target resistance value is `targetStabilityCoefficient = max(enlightenment.tier, radiance.tier)` clamped to `0..4`; final soul death requires `soulDissipationTier > targetStabilityCoefficient`.
- Final soul death is recorded only on a resolved afterlife conflict proof. Add `recentConflicts[].soulDissipationProof` with `proofId`, `actorType`, `actorId`, `targetActorType`, `targetActorId`, `dissipationTier`, `targetStabilityCoefficient`, `resolvedAtTurn`, victory proof, `gmMotivation`, and `outcome`; if the player soul is the target, also add `soul_state.terminalGameOver.message = "Вы мертвы. Ваша душа окончательно развеяна. Загрузите последнее сохранение и попробуйте снова"`.
- This file is afterlife-only. Do not mutate it from Mortal World turns, and do not store these profiles in Mortal NPC, skill, inventory, or combat files.

#### **AFTERLIFE ACTIVE THREATS**
- `game_state/meta/afterlife_active_threats.json` is the afterlife-only persistent threat file for soul hunters, Chaos Sea storms, hidden Wings cells, faction conspiracies, cursed resonances, and other durable dangers. Do not use Mortal `worldMapUpdates.activeThreats` for Chaos Sea or Shining Abode threats.
- The GM writes `afterlifeThreatsToAdd[]`, `afterlifeThreatsToUpdate[]`, `completeAfterlifeThreatActivities[]`, or `afterlifeThreatsToRemove[]`; the client normalizes them into canonical `threats[]` by unique `threatId`.
- Required threat fields are `threatId`, `realm`, `scopeId`, `displayName`, `threatArchetype`, `intensity`, `currentActivity`, `impactProfile`, `visibleToPlayer`, optional `linkedFactionId`, optional `linkedGuardianId`, optional `sarefLink`, and `ledger[]`.
- `currentActivity` is the live activity slot. To finish, fail, cancel, or abandon it, write `completeAfterlifeThreatActivities[]` with `threatId`, optional `activityId`, `finalState`, `completionSummary`, and `completedAtTurn`; do not set `currentActivity` to `null` in `afterlifeThreatsToUpdate[]` or a raw canonical overwrite. Terminal `currentActivity.activeState` values such as `completed`, `failed`, `cancelled`, or `abandoned` are invalid while still stored as current activity.
- `impactProfile` is a pressure metadata object with `primaryTargetType`, `primaryTargetId`, `primaryTargetName`, `primaryImpact`, and `baseImpactValue`; it is not authority to mutate other systems. If the threat changes combat, politics, relationship gates, chronicles, global flags, Saref state, or Shining factions, also use that system's documented contract.
- `visibleToPlayer=false` means hidden GM-facing threat context. Normal player UI must not reveal the threat name, hidden cell, `sarefLink`, or private activity details until a separate reveal makes it visible.
- `sarefLink` may connect a hidden threat to `Крылья над Бездной`, but it does not reveal Сареф or Крылья Ангелов by itself and must still obey the main story reveal gates.
- `afterlifeThreatsToRemove[]` is only for threats that are truly resolved or intentionally retired; when an active activity exists, first close that activity through `completeAfterlifeThreatActivities[]` so `ledger[]` preserves proof.

#### **AFTERLIFE EXTERNAL MEMORY / CHRONICLES**
- `game_state/meta/afterlife_chronicles.json` is the afterlife-only external memory file for places and scenes that must remember consequences across turns.
- The GM writes `afterlifeChronicleUpdates[]`; the client normalizes updates into canonical `chronicles[]` by unique `chronicleId`.
- Required chronicle fields: `chronicleId`, `scopeType`, `scopeId`, `displayName`, `eventDescriptions[]`, `lastEventsDescription`, `persistentConsequences[]`, `openThreads[]`, and `lastUpdatedTurn`.
- `eventDescriptions[]` is read-only historical archive memory. Do not include it inside `afterlifeChronicleUpdates[]`; write only `lastEventsDescription` for the current turn and let the client preserve/archive prior reports.
- Supported `scopeType` values are `chaos_sea_region`, `shining_abode_district`, `guardian_abode`, `guardian_scene`, `resident_scene`, `faction_zone`, `memory_scene`, `source_of_light`, and `saref_story`.
- This file replaces Mortal external-memory channels in afterlife. Do not use `worldEventsLog`, `currentLocationData`, `worldMapUpdates`, Mortal faction chronicles, Mortal NPC journals, or current-world lore for Chaos Sea / Shining Abode memory.

#### **AFTERLIFE GLOBAL FLAGS**
- `game_state/meta/afterlife_global_flags.json` is the afterlife-only global fact file for irreversible or long-lived facts. It is similar in purpose to Mortal `worldStateFlags`, but it is a separate contract and must never be written through `worldStateFlags`.
- The GM writes targeted `afterlifeGlobalFlagUpdates[]`; the client normalizes updates into canonical `flags[]` by unique `flagId`.
- Required canonical fields: `flagId`, `category`, `state`, `visibility`, `createdAtTurn`, `updatedAtTurn`, `reason`, `evidence`, `linkedActors[]`, `linkedChronicles[]`, and `obsoleteReason` when `state=obsolete`.
- Supported categories are `saref`, `source_of_light`, `guardian_memory`, `chaos_politics`, `shining_politics`, `soul_dissipation`, `realm_lifecycle`, and `relationship_gate`.
- Every update requires `gmThoughtsSummary`. Do not remove flags by resending a shorter full array; write `state=obsolete` with `obsoleteReason`.
- `visibility=hidden` and `visibility=gm_only` are private. They can guide GM continuity but must not leak into normal player-facing status, public summaries, or narrative before a separate visible/revealed contract makes them public.
- A global flag summarizes state only. It cannot override canonical Saref state, Source of Light reward tuple validation, Shining faction lifecycle, or spiritual conflict proofs.

#### **LORE CODEX (GM writes directly to lore/ files)**
- `lore/chaos_sea/cosmology.json` — Core universe structure, planes of existence
- `lore/chaos_sea/guardians_lore.json` — Guardian histories, domains, relationships
- `lore/chaos_sea/soul_system_lore.json` — Soul mechanics, reincarnation, enlightenment
- `lore/chaos_sea/artifacts_lore.json` — Legendary Soul Relics and their origins
- `lore/chaos_sea/player_chronicle.json` — Cross-incarnation player milestones
- `lore/current_world/world_setting.json` — World type, genre, tone, general description
- `lore/current_world/world_directives.json` — Persistent player-authored world directives, restrictions, required elements, and amendments for the current life
- `lore/current_world/geography.json` — World map, nations, cities
- `lore/current_world/history.json` — World timeline, major events
- `lore/current_world/cultures.json` — Races, customs, religions
- `lore/current_world/threats.json` — Antagonists, world-ending threats
- `lore/current_world/npcs_lore.json` — Optional supplemental NPC backstory lore; useful when needed, but not part of the hard bootstrap minimum
- `lore/codex_entries.json` ← `loreCodexUpdates` (player-discovered knowledge index)
- `game_state/history/chat_log.json` — Client-maintained session metadata
- `stories/*.jsonl` — Client-maintained narrative continuity history; this is the canonical long-form story source for GM reading across turns and incarnations
- Curated actor memory lives in:
  - `game_state/npcs/npc_journals.json` — NPC thought journal
  - `game_state/npcs/npc_interaction_journal.json` — NPC event journal
  - `game_state/meta/guardian_thought_journal.json` — Guardian thought journal
  - `game_state/meta/guardian_social_journal.json` — Guardian event journal
  - `game_state/meta/guardian_abode_residents.json.thoughtJournal[]` — resident thought journal
  - `game_state/meta/guardian_abode_residents.json.interactionLog[]` — resident event journal
- `stories/*.jsonl` remains the only full raw continuity source; do not duplicate full transcripts into actor journals

#### **MISCELLANEOUS**
- `game_state/misc/vehicles.json` ← `UpdateVehicles`, `removeVehicles`, `activeVehicleChange`
- `UpdateVehicles` accepts partial updates for existing vehicles (`vehicleId` + changed fields). When granting a brand-new vehicle, use the full Vehicle Object from Block 10; that schema permits `vehicleId = null`, although examples often preassign a fresh id immediately.
- For vehicle availability changes, preserve the canonical availability/location invariant:
  - `Active` or `Pocket` vehicles must end with `currentLocationId = null`
  - `Parked` vehicles must end with a non-null `currentLocationId`
  - when a partial `UpdateVehicles` patch changes `availability`, include the matching `currentLocationId` change in the same turn when needed
- `game_state/misc/storage_access.json` ← `grantStorageAccess`, `revokeStorageAccess`, `shareStorageAccess`
- `game_state/misc/multipliers.json` ← `multipliers` array
- `game_state/misc/player_interactions.json` ← `otherPlayersInteractions`
- `output/interface_updates.json` ← optional `dialogueOptions`, `image_prompt` payload for turns that actually change the interface
- `game_state/misc/characteristics.json` ← `setCharacteristics`

#### **GAME FLOW CONTROL**
- `game_state/control/life_transitions.json` ← `TriggerLifeEnd` (Mortal World only; `reason` must be `Death` or `Voluntary`)
- `game_state/control/incarnation_trigger.json` ← `TriggerIncarnation` (ordinary Chaos Sea lifecycle control, or Shining Abode pending-bootstrap handoff that preserves `preparedIncarnationPackage`; GM writes only the trigger object `{ "worldDescription": "...", "characterDescription": "...", "circumstances": "..." }`, and the client performs Mortal bootstrap after accepting it)
- `game_state/control/incarnation_world_setup.json` ← client-authored pending world setup chosen before incarnation; GM must read it when authoring `TriggerIncarnation` and the first Mortal World bootstrap
- `game_state/control/ascension.json` ← `AscensionTrigger`, `playerChoice` (real Chaos Sea-only ascension transition; only when Enlightenment is max and `playerChoice=Ascension`; never combine with `TriggerLifeEnd`)
- `game_state/control/validation_repair_request.json` ← client-written contract repair request when a GM turn is rejected after validation
- `game_state/control/validation_repair_ready.json` ← GM-written recheck signal after in-place fixes
- `game_state/control/terminal_protocol_failure_request.json` ← client-written notification that the terminal ready signal itself was malformed, mismatched, or ambiguously duplicated
- `game_state/control/pending_ink_actions.json` ← deferred pending Ink Feather resolutions such as `SEAL_IN_INK`
- `game_state/control/pending_turn_snapshot.json`, `game_state/control/pending_turn_snapshot/`, and `game_state/control/pending_turn_snapshot.authority.json` are client-owned transient authority files and must not be treated as GM-authored game state; the authority file is a client hash/proof used by accepted-turn lifecycle checks, not a repair target

#### **OUTPUT INTERFACE**
- `output/narrative_response.json` ← `response` (main narrative)
- `output/interface_updates.json` ← optional `dialogueOptions`, `image_prompt` payload for turns that actually change the interface
- `output/debug_logs.json` ← `gm_thoughts_markdown`
- `output/ink_feather_action_result.json` ← mandatory structured receipt for every GM-side `[INK_FEATHER_ACTION: TAG]`
- `output/narrative_response.json`, `output/interface_updates.json`, and `output/debug_logs.json` are per-turn transient files: they must describe the current request only and must not be reused as stale payload from a previous turn

#### **READY SIGNALS**
- `ready/turn_complete.json` ← terminal success signal with metadata
- `ready/turn_error.json` ← terminal error signal with metadata

---

## 🌗 **Realm-Aware Field Validity**

Before populating any mechanical field, the agent must determine the active realm from
`game_state/meta/soul_state.json.currentRealm`, exposed in runtime context as `Context.worldState.currentRealm`.

The client validator hard-rejects accepted turns that mutate realm-forbidden state files for the active realm.

### Always Safe Narrative/UI Fields
- `response`
- `gm_thoughts_markdown`
- `dialogueOptions`
- `image_prompt`

### Lifecycle Control Realm Rules
- `TriggerLifeEnd` is Mortal World only; it starts the later Life Evaluation lifecycle and must not be emitted from Chaos Sea or Shining Abode.
- `TriggerIncarnation` is valid from ordinary Chaos Sea, or from `Shining Abode pending-bootstrap handoff mode` when an existing `preparedIncarnationPackage` is present and preserved for later runtime bootstrap consumption; it must not be mixed with ordinary Shining living-world progression.
- Guardian-forced `TriggerIncarnation.source=guardian_forced` is valid only on ordinary player-driven Chaos Sea turns with either explicit provocation against the current active Guardian or current-turn resolved afterlife spiritual conflict proof. Legacy deterministic evidence tags are `[GUARDIAN_PROVOCATION]` and `[GUARDIAN_PROVOCATION: guardianId]`; the id form must match `TriggerIncarnation.guardianId`. Conflict evidence must be `game_state/meta/afterlife_spiritual_conflict_state.json.recentConflicts[]` proof for the same `guardianId`, current `turnNumber`, `operationType=force_incarnation`, and player loss/surrender/concession. In both paths, pre-turn Guardian reputation `relationshipData.currentReputation` / `CurrentReputation` must be `<= -21`. Severity is mechanical: `-21..-50 => severityBand=harsh`, `-51..-100 => severityBand=severe`.
- In `Shining Abode pending-bootstrap handoff mode`, GM must not remove, clear, rename, or mutate `game_state/meta/shining_abode_state.json.preparedIncarnationPackage` in the accepted `TriggerIncarnation` turn. The GM writes only `TriggerIncarnation` / `game_state/control/incarnation_trigger.json` and preserves the package exactly as provided; the client runtime reads it after accepting the trigger, materializes the frozen blessing/world setup, and clears it only after successful Mortal World bootstrap. This mode is legal only after Soul Gates have no unresolved afterlife pending/control contracts and no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`: Guardian social/resident/archive/offering/foundation/trade contracts, Source of Light (`pending_source_of_light_capstone.json`), active spiritual conflicts, Shining core/trade/politics contracts, malformed wrong-realm NPC pending files, and legacy `pendingNativeFactionDiscovery` must be resolved or repaired before handoff, not closed together with it.
- If the Shining handoff package is missing, malformed, or present as a non-object value, do not "repair" it by deleting or nulling the package and do not treat the realm as ordinary active Shining. Preserve the current state and use the normal validation repair/error path so the bootstrap contract can be fixed without losing the prepared package.
- Client-owned `reenter_shining_abode` is a local Chaos Sea route into an already stored active Shining Abode. No GM-authored output is required or allowed for this route. It fails closed if `afterlife_return_guard.json` is malformed, wrong-reason, or otherwise blocking, or if `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict` is present; valid post-life guards and active spiritual conflicts must be consumed/closed by an ordinary afterlife turn first. Re-entry does not reset ascension-local counters and does not refill Light Sparks. The local runtime may sync Shining return-cycle gacha charges and may create a Shining trade auto-refresh request when trade inventory is stale; those are client-side side effects, not GM-authored receipts.
- Client-owned `return_to_chaos_sea` / legacy `new_game_plus` starts the Shining Abode New Cycle and is allowed only when there are no active, malformed, or non-empty Shining pending contracts, no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`, and `shining_abode_state.json.pendingNativeFactionDiscovery = null`. Resolve or repair `pending_shining_abode_actions.json`, Shining trade inventory requests, Shining founding requests, Shining realignment requests, Shining leadership transition requests, `pending_source_of_light_capstone.json`, active spiritual conflicts, and legacy native discovery before sealing the Abode. A valid explicit-empty `{ "requests": [] }` Shining pending file is stale client-owned clutter and may be deleted by runtime health logic; malformed files or any non-empty `requests[]` block the return fail-closed until repair, closure, or explicit cleanup. Any unresolved or malformed `pending_source_of_light_capstone.json` also blocks return. Any non-null `pendingNativeFactionDiscovery`, including a malformed string/array/non-object, also blocks return until repaired or explicitly closed. The local route resets Enlightenment/Просветление to baseline and preserves Ink Feathers, Soul Relics, Guardians, Shining achievements, halls, factions, and Radiance progress; it is not an Ink Feather wipe or destructive global reset.
- `afterlife_return_guard.json` is a client-owned post-life safety guard, not a Shining bootstrap marker. Ordinary afterlife turns consume or clear only a semantic-valid guard with `reason = post_life_return`; malformed JSON or a parsed guard with the wrong `reason` remains fail-closed until validation repair or explicit client/runtime clear. GM must not weaken protection by rewriting, decrementing, or nulling the guard manually.
- `AscensionTrigger` is valid only in Chaos Sea, only with ascension-ready Enlightenment (`enlightenment.experience` or `soulProgression.totalExperience >= 60`, or legacy max/Transcendence marker) and explicit `playerChoice=Ascension`, and must never be mixed with `TriggerLifeEnd`. The GM writes `game_state/control/ascension.json` / `AscensionTrigger=true` plus `playerChoice="Ascension"` only; the client performs the later realm handoff, so do not manually switch `soul_state.currentRealm` to `Shining Abode` in the same accepted response.

### Afterlife Realm Model
- Empty, missing, or `null` `currentRealm` is not `Chaos Sea`; it is an unresolved realm fault. GM must not infer realm from pending files, scheduler state, old logs, or narrative context.
- `Chaos Sea` and `Shining Abode` are both afterlife realms for validator/runtime purposes.
- Both afterlife realms use guardian/soul/meta systems and forbid mortal-world combat/NPC/faction/location mechanics.
- Both afterlife realms still have a living-world scheduler through `progressionControl`. This is afterlife-specific progression, not Mortal World `worldEventsLog` / `factionDataChanges` progression.
- Afterlife accepted turns must not write response surfaces mapped to `game_state/core/player_status.json`, `game_state/player/*`, `game_state/inventory/*`, `game_state/world/*`, `game_state/npcs/*`, `game_state/combat/*`, `game_state/factions/*`, `lore/current_world/*`, Mortal quest files (`regular_quests.json`, `quest_history.json`, `plot_outline.json`), or Mortal misc files (`characteristics.json`, `vehicles.json`, `storage_access.json`, `player_interactions.json`). Use afterlife Guardian/Soul/resident/Shining surfaces plus `progression_report.json`; for private afterlife planning use `afterlifeStoryOutline` / `game_state/meta/afterlife_story_outline.json`, not Mortal `plotOutline`.
- `Shining Abode` is the ascended endgame free-roleplay zone above the Chaos Sea and still uses afterlife guardian/soul/meta systems instead of Mortal World systems.
- `Shining Abode pending-bootstrap handoff mode` is not an ordinary active Shining turn; write only the `TriggerIncarnation` lifecycle control, preserve the prepared package, and suppress ordinary Guardian/Shining scheduler progression for that handoff. Do not mix handoff with unrelated pending contract closure; the client must block Soul Gates until those contracts are gone.
- Shining Gates select/deselect/reroll are local client mutations, not GM turns. They do not create `pending_shining_abode_actions.json`, do not require `coreActionReceipts[]`, and do not authorize `TriggerIncarnation`; they only mutate local `gates` draft fields such as `selectedBlessingCardIds`, `shownBlessingCardIds`, `availableBlessingCards`, `rerollsRemaining`, and `nextCandidateCursor` when the draft is open, non-stale, and no Shining core pending request is unresolved.
- Shining Treasury `/shining_treasury` / `/казначейство` is a local client-owned economy surface, not a GM turn. It may mutate only `shining_abode_state.json.treasury`, `shining_abode_state.json.lightSparks`, and `soul_state.json.inkFeathers`. Only Ink Feathers can be deposited; Light Sparks cannot be deposited, cannot earn interest, and cannot be exchanged back into Feathers. The fixed conservative exchange is `25` Ink Feathers -> `1` Light Spark, capped at `3` Light Sparks per Shining return cycle. Treasury interest is small, tiered by deposited Ink Feathers, capped at `5` Ink Feathers per cycle, and has no pending file, receipt, report, or daemon response field. If validated pre-turn `shining_abode_state.json` already contains `treasury`, GM accepted-turn output must preserve that object unchanged instead of omitting, resetting, or re-authoring it.
- Source of Light `/source_of_light` / `/источник_света` is the full-Radiance Shining capstone command. The client creates `pending_source_of_light_capstone.json`; the GM closes that pending request by preserving ordinary active Shining state invariants, writing the Source of Light scene, marking `sourceOfLightCapstone.completed`, granting `light_incarnate` / `Воплощение Света`, and adding one `source_of_light_incarnated_light` / `Воплощенный Свет` Soul Relic. The relic's Mortal effect is `+25` to every primary characteristic through `effects.characteristicBonuses`. The passive's afterlife conflict effect is mechanical and trusted only when the completed Shining marker, soul passive, and exactly one matching Soul Relic are all present: every contested `afterlife_spiritual_conflict_v1` dice audit from `grantedAtTurn` onward must include an explicit turn marker (`exchangeAtTurn`, `resolvedAtTurn`, or `turnNumber`) and player-side `light_incarnate` `+8` when the player leads, `+4` when the player only supports/contributes to a champion side, plus `+4` extra for `force_incarnation`, `force_binding`, or `break_binding`; pre-unlock or incomplete-closure `light_incarnate` modifiers are invalid.

### Contract Repair Handshake
- `validation_repair_request.json` is authoritative when the client rejects an already written GM turn after validation.
- GM must fix the current files **in place** and then write `validation_repair_ready.json`.
- `validation_repair_ready.json` must be valid JSON and must copy the exact `sessionId`, `requestId`, and `turnNumber` from the current `validation_repair_request.json`.
- If `validation_repair_ready.json` is malformed or mismatched, the client rewrites `validation_repair_request.json` with `invalid_repair_ready_json` or `mismatched_repair_ready_context` and waits again.
- This repair handshake is not a new turn and must not produce a new `turn_request.json`.
- If the client rolls back a rejected or failed turn, the rollback target is the full pre-turn tracked file set: existing tracked files are restored, and newly created tracked files are removed.
- Late `ready/turn_error.json` after a cancelled wait is still treated as a valid late signal and must be safe to consume/clean up.
- Malformed or mismatched terminal `ready/turn_complete.json` / `ready/turn_error.json` after retry window is a protocol failure of the current wait cycle, not a repair-loop case by itself.
- Client and daemon must correlate terminal `ready/turn_complete.json` / `ready/turn_error.json` by exact `sessionId`, `requestId`, and `turnNumber`; stale terminal files must be discarded and must not close a different turn.
- For one turn there must be exactly one correlated terminal signal. Simultaneous correlated `ready/turn_complete.json` and `ready/turn_error.json` for the same request are a protocol failure.
- When terminal protocol failure happens, the client writes `game_state/control/terminal_protocol_failure_request.json`. This is NOT `validation_repair_request.json` and does not use `validation_repair_ready.json`.
- Final success/error/failure choice for an active turn is determined only after re-reading and reconciling the current ready files on disk; the first file noticed by polling is not authoritative by itself.
- `terminal_protocol_failure_request.json` must survive restart by default and is not automatically stale merely because a pending snapshot manifest still exists.

### Actor Scope Consistency
- `gm_thoughts_markdown` scope declaration is not cosmetic.
- `Actor Brain 2.0` is the preferred shared reasoning protocol for all materially changed actors. It does not replace or simplify Mortal `NPC Brain 2.0`; Mortal NPC reasoning keeps its full knowledge/personality/culture/relationship/attraction-filter depth. For afterlife actors use `OtherGuides/Actor_Brain_2_0.md`: `Guardian pack`, `Resident pack`, and `Shining political` pack.
- `Relevant actors` must cover the actors changed through structured actor mutation surfaces such as `UpdateNPCs`, actor-specific NPC update arrays, `UpdateGuardians`, resident update/journal/history/receipt surfaces, `shining_abode_state.json.shiningPoliticalActors[]`, and important `shining_abode_state.json.factions[]` political/head-actor changes.
- `Scene-local` with `Relevant actors: нет` is valid only when no structured actor updates are emitted for that turn.
- `Guardian-centric` validation must not infer a Guardian from array order; only explicit `activeGuardian` state or the declared scope itself may be used.
- If a structured actor update contains only an unresolved ID and no reliable name alias can be recovered from current state, the client should not hard-reject solely on that unresolved identity.
- This is the canonical current runtime contract for `gm_thoughts_markdown`; older heavier all-turn templates in legacy docs do not override the current validator.

### Mortal World Only
- `currentPoiseChange`, `currentEnergyChange`, `currentHealthChange`
- `experienceGained`, `moneyChange`
- `statsIncreased`, `statsDecreased`, `setCharacteristics`
- `activeSkillChanges`, `passiveSkillChanges`, `skillMasteryChanges`
- `UpdateInventory`, `inventoryItemsResources`, `moveInventoryItems`, `removeInventoryItems`
- `UpdateNPCs`, `NPCsInScene`, regular NPC arrays, `UpdateQuests`
- `worldEventsLog`, `factionDataChanges`, `factionProjectUpdates`
- `currentLocationData`, `timeChange`, `setWorldTime`, `weatherChange`
- `enemiesData`, `alliesData`, `combat_log_markdown`
- Explicit Mortal-World Ink Feather whitelist actions may also legally produce mortal-world outputs such as XP, skills, buffs, item upgrades, or epic world events.

### Afterlife Realm Only
- `UpdateGuardians`
- Guardian reputation / project / musings / lore unlock commands
- Guardian project, resident agency, Shining Abode, Shining faction, and Shining trade progression when mandated by `progressionControl`
- `progressionProcessingReport` for afterlife scheduler debt and bounded catch-up acknowledgement
- `afterlifeSpiritualConflictUpdate` for roleplay-started afterlife spiritual conflicts; this is not Mortal combat and must not use `game_state/combat/*`
- `afterlifeStoryOutline` for private afterlife Writer's Room planning in `game_state/meta/afterlife_story_outline.json`; it is flexible, not player-visible, and не является приказом to force future outcomes
- Soul Relic Gacha processing
- Direct Chaos Sea gacha via `/gacha` remains a Chaos-Sea-specific exception and does not use current Guardian modifiers; its result rarity must equal `turn_request.json.gachaBaseResult.baseRarity` exactly, with no upgrade or downgrade path
- Abode navigation data
- Explicit afterlife Ink Feather whitelist actions may also legally produce guardian/meta/soul outputs.

### Forbidden In Afterlife Realms
- Combat, experience, leveling, stat gains/losses, regular inventory management, regular NPC mechanics,
  mortal quests, Mortal World faction/world progression, weather, time progression, mortal location tracking.
- File-level rule: during `Chaos Sea` / `Shining Abode`, no response surface may write or mutate any file mapped to `game_state/core/player_status.json`, `game_state/player/*`, `game_state/inventory/*`, `game_state/world/*`, `game_state/npcs/*`, `game_state/combat/*`, `game_state/factions/*`, `lore/current_world/*`, Mortal quest files, or Mortal misc files. Use Guardian/Soul/resident/Shining afterlife surfaces instead.
- Ordinary `Chaos Sea` turns must not write `game_state/meta/shining_abode_state.json`. They may read stored Shining state for lifecycle preconditions, but Shining owner-state mutations are reserved for ordinary active `Shining Abode` contracts, Shining pending-bootstrap preservation, or client-owned local routes.

### Forbidden In Mortal World
- Guardian presence as active entities, Guardian reputation changes, Abode navigation, Gacha, afterlife-only Ink Feather spending.
- Narrow exception: `guardianQuestProgressUpdates` may update only an existing pre-turn `guardian.questManagement.activeQuests[]` entry during Mortal World play. It cannot create Guardian quests, change reputation, change Guardian identity, navigate Abodes, process Gacha, complete the quest, or directly write afterlife authority files such as `game_state/meta/guardian_abode_residents.json`, `game_state/meta/guardian_thought_journal.json`, `game_state/meta/guardian_social_journal.json`, `game_state/meta/guardian_projects.json`, `game_state/meta/guardian_project_journal.json`, `game_state/meta/abode_power_journal.json`, or `game_state/meta/shining_abode_state.json`. Use it to mark accepted Guardian work as `active`, `ready_to_turn_in`, `failed`, or `expired` with non-physical evidence; final hand-in still uses `UpdateGuardians.completeQuest` after the soul returns to the Guardian.

### Mortal-World Ink Feather Exceptions
The following spending-based Ink Feather actions are explicitly allowed in `Mortal World`:
- `Reveal Fate`
- `Rewrite Fate`
- `Sacrifice to Chaos`
- `Absorb Feathers`
- `Learn Skill`
- `Fate Shield`
- `Seal in Ink`

These exceptions do NOT unlock Guardians, Abodes, Guardian reputation changes, or Gacha.
- `Absorb Feathers` is valid only if `experienceGained` is positive and an authoritative XP counter in `game_state/player/experience.json` really increases.
- `Learn Skill` is valid only if it creates a NEW skill object in the appropriate player skill file for this turn.
- `Fate Shield` is valid only if it creates a NEW `Щит Судьбы` effect instance for this turn.

### Afterlife Ink Feather Exceptions
The following spending-based Ink Feather actions are explicitly allowed in afterlife realms (`Chaos Sea` and `Shining Abode`) when their specific action prerequisites are satisfied:
- `Donate to Guardian`
- `Cultivate Enlightenment` uses `experienceGain = costInFeathers * 4`; 60 Enlightenment XP is ascension-ready for first Shining Abode entry.
- `Guardian Favor`
- `Memory Gates`
- `Soul Imprint`
- `ABODE_OFFERING` only when `game_state/control/pending_abode_offering.json.offeringType = ink_feathers`

These exceptions do NOT unlock Mortal-World-only mechanics such as combat, XP leveling, regular inventory changes, or regular NPC quest/world systems.
The Mortal-World and afterlife Ink Feather whitelists are mutually exclusive.
- Ink Feather `ABODE_OFFERING` is valid only with the matching client-authored `game_state/control/pending_abode_offering.json` where `offeringType = ink_feathers`; GM reads that file as input, resolves through `guardianPowerEvents` with `reasonType = offering`, and writes `output/ink_feather_action_result.json` with `actionTag = ABODE_OFFERING`.
- If `pending_abode_offering.json.offeringType` is `soul_relic`, `archive_lore_fragment`, or `archive_secret_record`, process it as plain `[ABODE_OFFERING]`: resolve through `guardianPowerEvents.reasonType = offering` only, do not invent `costInFeathers`, and do not write `output/ink_feather_action_result.json`.
- `Sell Relic` is a separate guardian trade interaction and is NOT part of the Ink Feather action contract.
- Local guardian trade panel (`Buy / Sell` Soul Relics with the current active Guardian) is handled entirely on the client side.
- It does NOT create `turn_request.json`, does NOT require `ink_feather_action_result.json`, and is separate from roleplay trade through the GM.
- Local guardian trade is available only with the current active Guardian in the current abode.
- In the buy flow, the client must let the player inspect a relic's properties before confirming purchase.
- Guardian sell-side remains local, but sold Soul Relics must persist canonically in `guardians[].buybackRelics[]`; do not treat them as disappearing from the afterlife economy.
- `guardians[].buybackRelics[]` is separate from authored `guardian.tradeInventory`: it stores exact Soul Relics the player sold to that Guardian and powers local reverse-buyback without another GM stock-generation round-trip.
- Guardian trade stock is explicit authored state in `guardian.tradeInventory`; the client does NOT derive stock from `guardian.domain`.
- If persisted guardian trade stock is missing, stale, malformed, or economically inconsistent, the client creates `pending_guardian_trade_request.json` and asks the GM to materialize a fresh explicit inventory for the current cycle instead of silently generating it.
- Guardian buy-side checks and request creation also require an actual current turn; do not let new buy-side flows emit `createdAtTurn = 0` when a stale inventory is discovered during purchase.
- A guardian trade request is closed canonically only when matching `guardian.tradeInventory` appears **and** `guardians[].tradeInventoryReceipts[]` gets a matching `ready` receipt for the same `requestId` / `tradeCycleId`.
- Once matching `guardian.tradeInventory` plus matching `tradeInventoryReceipts[]` receipt appear, the client derives a ready-notification in `afterlife_notifications.json`; the GM does not write this notification separately.
- Local guardian trade stock uses the authored guardian inventory contract:
  - stock size must still respect derived Abode Power slot count
  - stock contents/rarity are authored upstream and stored in `guardian.tradeInventory.items`
  - later reopen within the same cycle may reprice stock for the current reputation tier, but does not reroll or regenerate contents
  - generation-time rarity caps:
    - Hostile: no trade
    - Wary / Neutral: up to Uncommon
    - Friendly: up to Rare
    - Devoted / Legendary: up to Epic
  - base buy prices:
    - Common = 30
    - Uncommon = 70
    - Rare = 140
    - Epic = 260
  - base sell prices:
    - Common = 10
    - Uncommon = 25
    - Rare = 60
    - Epic = 150
    - Legendary = 400
  - buy multipliers:
    - Wary / Neutral: 1.15
    - Friendly: 1.00
    - Devoted: 0.90
    - Legendary: 0.80
  - sell multipliers:
    - Wary / Neutral: 0.85
    - Friendly: 1.00
    - Devoted: 1.10
    - Legendary: 1.20
  - persisted `tradeInventory` stores:
    - `generationReputationTier`
    - `pricingReputationTier`
  - persisted guardian trade closure also stores `guardians[].tradeInventoryReceipts[]` with:
    - `requestId`
    - `guardianId`
    - `guardianName`
    - `abodeId`
    - `tradeCycleId`
    - `status = ready`
    - `itemCount`
    - `resolvedAtTurn`
    - `resolvedAtUtc`
  - guardian trade stock is explicit authored state in `guardian.tradeInventory`; the client does NOT derive stock from `guardian.domain`
  - if `guardian.tradeInventory` is absent or malformed for the current cycle, the client writes `pending_guardian_trade_request.json` and waits for a GM-materialized inventory
  - when the pending request resolves into matching explicit inventory plus matching `tradeInventoryReceipts[]`, the client surfaces that response through `/afterlife_inbox` and related afterlife banners
  - the local sell panel offers only stored Soul Relics; equipped relics are not auto-sold
  - local guardian sell-side also persists `guardians[].buybackRelics[]` entries with:
    - `buybackEntryId`
    - `guardianId`
    - `guardianName`
    - `relicId`
    - `relicData`
    - `soldByPlayerAtTurn`
    - `soldByPlayerAtUtc`
    - `soldForPrice`
  - `buybackPrice`
  - `acquiredFromPlayer = true`
  - `status = available | rebought | removed`
- local reverse-buyback restores exact `relicData` from `buybackRelics[]`; it does NOT move those relics into authored `guardian.tradeInventory`
- local guardian sell-side and reverse-buyback require an actual current turn; do not treat `0` as an acceptable timing fallback for new writes

### Local NPC Trade Panel
- Some NPC merchants may have a separate local trade panel for mortal-world goods.
- This panel does NOT create `turn_request.json`, does NOT require `ink_feather_action_result.json`, and does NOT trade Soul Relics.
- Currency is `game_state/core/player_status.json.money`.
- Access rules:
  - the merchant must be in the same location as the player
  - `npc.tradeState.canTrade` must be true
  - the merchant must have a valid `merchantProfile`
- Canonical local NPC merchant profiles:
  - `GeneralGoods`
  - `Equipment`
  - `CraftingSupplies`
  - `Consumables`
  - `KnowledgeAndMedia`
  - `LuxuryAndDecor`
  - `ArtifactsAndCurios`
  - `TechnicalGoods`
  - `IllicitGoods`
- NPC stock is explicit authored state in `npc.tradeInventory`; the client does NOT generate stock locally anymore.
- If persisted NPC stock is missing, stale, malformed, or no longer matches the current world-time cycle, the MortalWorldProfile client creates `pending_npc_trade_inventory_requests.json` / `[NPC_TRADE_REQUEST]` and waits for a GM-materialized inventory. This contract is not valid in `Chaos Sea` or `Shining Abode`.
- A NPC trade request is closed canonically only when matching `npc.tradeInventory` appears **and** the same NPC gets a matching `tradeInventoryReceipts[]` receipt with `status = ready`.
- Stock refresh is tied to world time, not to return from mortal life:
  - stock belongs to a deterministic `tradeCycleId`
  - stock refreshes when the world-time window expires
  - v1 refresh window: 30 in-world days
- Persisted `npc.tradeInventory` stores:
  - `tradeCycleId`
  - `generatedAtWorldDate`
  - `refreshAfterWorldDate`
  - `generationTradeTier`
  - `pricingTradeTier`
  - `items`
- Persisted NPC trade closure also stores `npc.tradeInventoryReceipts[]` with:
  - `requestId`
  - `npcId`
  - `npcName`
  - `tradeCycleId`
  - `merchantProfile`
  - `status = ready`
  - `itemCount`
  - `resolvedAtTurn`
  - `resolvedAtUtc`
- Selling ordinary mortal-world goods to merchant NPCs remains a local client-side action, but the sold item no longer disappears canonically.
- Each merchant NPC may persist `npc.buybackInventory[]` with exact goods previously sold by the player:
  - `buybackEntryId`
  - `npcId`
  - `npcName`
  - `itemId`
  - `itemData`
  - `soldByPlayerAtTurn`
  - `soldByPlayerAtUtc`
  - `soldAtWorldDate`
  - `soldForPrice`
  - `buybackPrice`
  - `acquiredFromPlayer = true`
  - `sourceMerchantProfile`
  - `status = available | rebought | removed`
- Buyback inventory is a separate local layer from authored `npc.tradeInventory`:
  - it is available even while the current buy-side stock request is still pending
  - it is not auto-merged into ordinary authored stock
  - rebuying a buyback entry returns the exact stored `itemData` to player inventory and marks that entry as `rebought`
- local NPC sell-side and reverse-buyback require an actual current turn; do not emit `soldByPlayerAtTurn = 0` or `reboughtAtTurn = 0` in new writes
- Local NPC trade items are setting-agnostic mortal-world goods and may include ordinary equipment, materials, documents, media, containers, decor, household goods, technical parts, curios, and illicit goods.
- Each authored stock item belongs to one of three canonical classes, stored in `itemData.tradeItemClass`:
  - `Functional`
  - `Material`
  - `FlavorOrUtility`
- V1 stock size is `6..20` items depending on NPC level and NPC trade characteristic.
- Generation tier controls quality at stock generation time:
  - `Poor` → Common
  - `Standard` → Common / Uncommon
  - `Good` → Uncommon / Rare
  - `Premium` → Rare / sometimes Epic
  - `Elite` → Rare / Epic
- Pricing uses the item's own base price in `itemData.price`, current `player.trade`, current `npc.trade`, and relationship-derived `pricingTradeTier`.
- Ordinary local NPC trade goods may omit `bonuses`, `effects`, `passiveEffects`, and `structuredBonuses`; this is a normal mundane-item case, not an invalid one.
- Soul Relics are never shown or sold in NPC local trade.
- If the player asks that merchant about an item bought from the merchant's local stock, the merchant should know the item and may explain its use or origin.
- Selling ordinary mortal-world goods to merchant NPCs remains a local client-side operation; only buy-side stock preparation is request-driven, while sell-side canonically appends to `npc.buybackInventory[]`.
- NPC buy-side checks and request creation also require an actual current turn; do not let new buy-side flows emit `createdAtTurn = 0` when a stale merchant inventory is discovered during purchase.

### QTE Offer Flow
- QTE is a special cinematic tool for bounded scenes in `Mortal World`.
- The GM may offer QTE only if `game_state/core/game_settings.json.qteEventsEnabled = true`.
- QTE may be offered only on an ordinary player-driven Mortal World turn; it is forbidden on incarnation, life-evaluation, repair, transition, and other system-driven turns.
- The offer is written to `output/qte_offer.json`.
- A QTE-offer turn must not also resolve normal state changes for the same situation.
- In practice, a valid QTE-offer turn leaves ordinary `game_state/`, `lore/`, and `stories/` files untouched and uses only:
  - `output/qte_offer.json`
  - `output/narrative_response.json`
  - optional `output/interface_updates.json` when that QTE-offer turn actually changes `dialogueOptions` and/or `image_prompt`
  - `output/debug_logs.json`
- The client shows a native `Accept / Decline` prompt.
- If the player declines, the client sends a new ordinary `turn_request.json` asking for standard mechanical resolution and forbidding the same `qteId` from being re-offered.
- If the player accepts, the full QTE scene resolves locally on the client.
- A valid `qte_offer.json` must contain these top-level fields:
  - `qteId`
  - `title`
  - `offerText`
  - `introNarrative`
  - `startChapterId`
  - `chapters`
  - `terminalOutcomes`
- Optional but supported top-level fields:
  - `declineHint`
  - `cinematicJustification`
  - `sceneImagePrompt`
- `qte_offer.json` must contain `startChapterId`, and that field alone defines the opening chapter. The order of `chapters[]` has no mechanical meaning.
- Every chapter must contain a non-empty `actions[]` array.
- Every chapter must contain `chapterId`, `narrative`, and `actions`.
- Every action must contain `actionId`, `label`, `check`, and `routing`.
- Every `check` must contain `type`, `baseDifficulty`, and `primaryCharacteristic`.
- `baseDifficulty` must be an integer in the range `1..5`.
- Graph validity rules:
  - every `chapterId` and `outcomeId` must be unique
  - every routing branch must point to exactly one target: `nextChapterId` or `terminalOutcomeId`
  - every action routing must contain all three branch objects: `success`, `partial`, and `fail`
  - all branch references must resolve
  - every chapter must be reachable from `startChapterId`
  - at least one success-reachable terminal outcome must exist
- QTE v1 node types:
  - `BranchChoice`
  - `TimingBar`
  - `PromptChain`
  - `BalanceMeter`
  - `ChargeRelease`
- QTE v2 node types currently implemented:
  - `MashInput`
  - `PatternMemory`
  - `RhythmPulse`
- QTE reaction checks use physical QTE keys. Player-facing labels such as `Q / Й`, `W / Ц`, `E / У`, `A / Ф`, `S / Ы`, `D / В`, and `Space` describe physical keys; the client handles physical key/RU-EN normalization and must not tell the player to switch OS layout.
- GM-authored QTE configs do not encode player keyboard layout, and this QTE-only normalization does not apply to normal text input, dialogue, names, or narrative prose.
- `check.primaryCharacteristic` must be one of these canonical lowercase ids:
  - `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `faith`, `attractiveness`, `trade`, `persuasion`, `perception`, `luck`, `speed`
- For `BranchChoice`, `check.config.choiceGrade` is required and must be exactly one of:
  - `success`
  - `partial`
  - `fail`
- For `MashInput`, `check.config.keys`, `durationMs`, `targetPresses`, and `partialThreshold` are required.
  - `check.config.keys` must be a non-empty array of unique canonical QTE key tokens: `q`, `w`, `e`, `a`, `s`, `d`, `space`.
  - `durationMs` must be an integer from 750 to 10000.
  - `targetPresses` must be a positive integer from 1 to 80 and must be possible for `durationMs` at the client limit of 12 presses per second.
  - `partialThreshold` must be a number greater than 0 and less than or equal to 1.
  - Higher `baseDifficulty` raises the effective required press count; a higher relevant `primaryCharacteristic` tier lowers it.
  - Escape/cancel resolves as fail.
  - Browser interactive MashInput parity remains #918; browser surfaces must not claim live interactive support in this slice.
- For `PatternMemory`, `check.config.alphabet`, `sequenceLength`, `revealMs`, `inputTimeoutMs`, and `allowedMistakes` are required.
  - `check.config.alphabet` must be a non-empty array of unique canonical QTE key tokens: `q`, `w`, `e`, `a`, `s`, `d`, `space`.
  - `sequenceLength` must be an integer from 2 to 12.
  - `revealMs` must be an integer from 500 to 15000 for the фаза показа.
  - `inputTimeoutMs` must be an integer from 1000 to 30000 for the фаза ввода and at least `sequenceLength * 300`.
  - `allowedMistakes` must be an integer from 0 to `sequenceLength - 1` so failure remains possible.
  - Higher `baseDifficulty` may increase effective sequence length, reduce reveal/input time, or reduce mistake tolerance; a higher relevant `primaryCharacteristic` tier must not make the same config harder.
  - Perfect repeat resolves success; an imperfect repeat within tolerance and with meaningful progress resolves partial; too many mistakes, timeout, or Escape/cancel resolves fail.
  - Browser interactive PatternMemory parity remains #918; browser surfaces must not claim live interactive support in this slice.
- For `RhythmPulse`, `check.config.pulseCount`, `beatIntervalMs`, `hitWindowMs`, and `allowedMisses` are required; `patternVariation` is optional.
  - `pulseCount` must be an integer from 2 to 16.
  - `beatIntervalMs` must be an integer from 300 to 3000.
  - `hitWindowMs` must be an integer from 40 to 1000 and `hitWindowMs * 2` must be strictly less than `beatIntervalMs`.
  - `allowedMisses` must be an integer from 0 to `pulseCount - 1` so failure remains possible.
  - `patternVariation`, when present, must be `steady`, `accelerating`, or `swing`.
  - Higher `baseDifficulty` may increase effective pulse count, reduce the hit window, or reduce miss tolerance; a higher relevant `primaryCharacteristic` tier must not make the same config harder.
  - Console RhythmPulse uses Space as the local pulse key and must show visual/textual pulse timing; audio cues are optional enhancement only.
  - Success resolves when missed pulses stay within tolerance; at least half of pulses hit resolves partial; too few hits or Escape/cancel resolves fail.
  - Browser interactive RhythmPulse parity remains #918; browser surfaces must not claim live interactive support in this slice.
- Every terminal outcome must contain a local `responseFragment` using normal `GameResponse` field names.
- Every terminal outcome must contain `outcomeId`, `title`, `finalNarrative`, `gmSummary`, and `responseFragment`.
- `responseFragment` is the authoritative final mechanical outcome for an accepted QTE branch; the GM must not rely on a follow-up GM turn to add the real reward later.
- Successful terminal outcomes must include positive `experienceGained` at minimum.
- For validation purposes, a "successful terminal outcome" is any terminal outcome reachable by following one or more `success` branches from `startChapterId`.
- After a successful accepted QTE, the client locally applies that `experienceGained` to an authoritative XP counter in `game_state/player/experience.json`.
- If `experience.json` already contains `level`/`playerLevel`, `experience` or `currentExperience`, and `experienceForNextLevel`, the client also performs the local level-up transition using the standard progression formula from Rule `5.10.A.1`.
- `declineHint` and `cinematicJustification`, if present in the offer, are shown in the client-side QTE offer prompt.
- `responseFragment.image_prompt` is forbidden inside QTE. If the scene needs images, use `sceneImagePrompt`, `chapterImagePrompt`, or `outcomeImagePrompt`.
- During the active QTE challenge itself, external image viewing must not interrupt the player. Scene images may be offered only on the post-chapter / post-outcome result screens.

### `MEMORY_GATES` Contract
- For every GM-side `[INK_FEATHER_ACTION: TAG]`, GM MUST also write `output/ink_feather_action_result.json` with:
  - `sessionId`, `requestId`, `turnNumber`
  - `actionTag`
  - `resolved = true`
  - `costInFeathers`
  - `resolutionType`
  - `summary`
  - `stateEvidence`
- `stateEvidence` MUST include `affectedFiles` and action-specific proof of the actual stateful result.
- The client rejects a feather-action turn if feathers were spent but `ink_feather_action_result.json` is missing, mismatched, or not backed by real state changes.
- `Memory Gates` is NOT a lore-only reward. It MUST create one mechanical reward for the NEXT mortal incarnation.
- GM must write `metaStateUpdates.memoryLegacyGrant`, and canonical state must end up with `game_state/meta/soul_state.json -> pendingMemoryLegacy`.
- Exactly one active Memory Legacy may exist at a time. A new `Memory Gates` use REPLACES the old pending legacy.
- Canonical `pendingMemoryLegacy` stores:
  - `legacyId`
  - `sourceLifeHint`
  - `legacyType`
  - `grantSource`
  - `grantSnapshot`
  - `applicationState`
  - `grantedAtUtc`
  - type-specific fields
- `applicationAudit` appears only after the client has locally applied the legacy during an incarnation turn.
- Supported `memoryLegacyGrant.legacyType` values:
  - `startingCharacteristicBonus` → requires `legacyId`, `sourceLifeHint`, `characteristic`, `bonus = 2`
  - `startingPassiveKnowledgeSkill` → requires `legacyId`, `sourceLifeHint`, `skillName`, `skillDescription`, `group = "Knowledge"`, `playerStatBonus`, `structuredBonuses`
- The client rejects a `Memory Gates` turn if no valid pending memory legacy is produced.
- If a new `Memory Gates` purchase leaves the old `pendingMemoryLegacy` unchanged, the client rejects the turn.
- `pendingMemoryLegacy` must remain semantically consistent with its `grantSnapshot`; matching only `legacyId` is not sufficient.
- During the later incarnation turn, if the client has already applied the pending legacy locally, the GM must preserve that applied effect in `characteristics.json` or `skills_passive.json`; losing it is a contract violation.
- For `startingPassiveKnowledgeSkill`, preserving only the skill name is not sufficient: the surviving skill must still keep `group = "Knowledge"`, a non-empty `playerStatBonus`, and non-empty `structuredBonuses`.
- Comparison of `structuredBonuses` for Memory Legacy is semantic, not order-sensitive; equivalent bonus arrays with different JSON field order must still be accepted.
- `pendingMemoryLegacy` may temporarily carry `applicationState = "applied-awaiting-turn-accept"` during an incarnation turn. This means the client has already applied the reward locally but has NOT consumed it yet; do not strip or reset this state manually.
- If the client detects that an `applied-awaiting-turn-accept` legacy no longer survives in runtime files, it reverts the legacy back to `pending` instead of consuming it.
- `SEAL_IN_INK` is deferred and MUST create `game_state/control/pending_ink_actions.json` with `actionTag = SEAL_IN_INK`, `status = awaiting-item-choice`, `costInFeathers`, and `upgradeTierDelta = 1`.
- `DONATE_TO_GUARDIAN` requires exact formula proof: `stateEvidence.reputationChange = min(25, max(15, costInFeathers / 3))`, and the target Guardian's `game_state/meta/guardians.json` `relationshipData.currentReputation` must increase by exactly the same number from the validated pre-turn baseline. Any positive-but-different delta is invalid.
- `GUARDIAN_FAVOR` no longer requires a typed quest/buff outcome.
- For `GUARDIAN_FAVOR`, the guaranteed mechanical minimum is:
  - `guardianId`
  - `reputationChange > 0`
  - real guardian reputation increase in `game_state/meta/guardians.json`
- Any extra quest / buff / hint / service remains optional and roleplay-dependent.

---

## ⚙️ **CLI Operations Protocol**

### Phase 1: Input Reading
```typescript
// 1. Read primary input
const turnRequest = readJSON('input/turn_request.json');

// 2. Load current game state (all game_state/ subdirectories)
const gameState = loadGameState('game_state/');

// 3. Load reference data as needed
const lore = loadLore('lore/');
const activeSystemMods = readJSON('game_state/core/system_mods.json').activeMods ?? [];

// Only activeSystemMods[] are canonical. Ignore disabled files that still exist in game_session/mods/.
const pendingWorldSetup = readJSON('game_state/control/incarnation_world_setup.json'); // optional, client-authored
const activeWorldDirectives = readJSON('lore/current_world/world_directives.json'); // optional, client/player-authored
```

### Phase 2: Processing
```typescript
// Apply ALL existing Rules/Block_*.txt logic exactly as designed
// Generate the same logical turn result as API mode, then distribute it through CLI output/* and game_state/* surfaces
const response = processGameTurn(turnRequest, gameState, rules);
```

### Phase 3: Output Distribution
```typescript
// Create backup copies
createBackups(affectedFiles);

try {
  // Distribute JSON response fields to appropriate files
  distributeToFiles(response, CLI_MAPPING);
  
  // Update output interface
  writeJSON('output/narrative_response.json', {
    response: response.response,
    timestamp: new Date().toISOString()
  });
  const hasInterfacePayload =
    Object.prototype.hasOwnProperty.call(response, 'dialogueOptions') ||
    Object.prototype.hasOwnProperty.call(response, 'image_prompt');
  if (hasInterfacePayload) {
    writeJSON('output/interface_updates.json', {
      dialogueOptions: response.dialogueOptions,
      image_prompt: response.image_prompt,
      timestamp: new Date().toISOString()
    });
  }
  writeJSON('output/debug_logs.json', {
    gm_thoughts_markdown: response.gm_thoughts_markdown,
    timestamp: new Date().toISOString()
  });
  
  // Signal terminal success
  writeJSON('ready/turn_complete.json', {
    sessionId: turnRequest.sessionId,
    requestId: turnRequest.requestId,
    turnNumber: turnRequest.turnNumber,
    timestamp: new Date().toISOString(),
    status: 'success',
    filesModified: getModifiedFilesList()
  });
  
} catch (error) {
  // Rollback on error
  restoreBackups(affectedFiles);
  maybeWriteJSON('game_state/history/error_log.json', {
    timestamp: new Date().toISOString(),
    error: error.message,
    context: 'turn processing',
    stackTrace: error.stack
  });
  writeJSON('ready/turn_error.json', {
    sessionId: turnRequest.sessionId,
    requestId: turnRequest.requestId,
    turnNumber: turnRequest.turnNumber,
    timestamp: new Date().toISOString(),
    status: 'error',
    error: error.message
  });
}
```

---

## ✅ **Validation Rules**

### File Integrity
1. **JSON Structure**: All files must be valid JSON
2. **Schema Compliance**: Follow defined object structures
3. **Cross-Reference Integrity**: NPC IDs, item IDs, location IDs must be consistent
4. **Required Fields**: Core fields must always be present
5. **Strict State Contracts**: Accepted turns are rejected if location, faction, quest, achievement, or lore-codex state violates the canonical file contract
6. **Dialogue Option Contract**: `dialogueOptions` is an array of objects with mandatory `text` and optional `category`; legacy string arrays are off-contract

### Atomic Operations
1. **Backup Creation**: Always create .backup copies before modification
2. **Complete or Rollback**: Either all operations succeed or all are rolled back
3. **Consistency Check**: Validate cross-file references after updates
4. **Terminal Error Signal**: `ready/turn_error.json` is the authoritative client-facing error channel
5. **Optional Diagnostics**: `game_state/history/error_log.json` is an optional structured diagnostic surface; terminal failure is still signaled only through `ready/turn_error.json`

### Language Compliance
1. **Russian Content**: All user-facing text must be in Russian
2. **Consistent Terminology**: Use established game terms
3. **Character Encoding**: UTF-8 encoding for all files

### Runtime-Enforced Contract Notes
1. **Realm Segregation**: Accepted-turn validation enforces realm separation by actual changed-file surface, not only by narrative intent.
2. **Rich Object Contracts**: Locations, factions, and quests are validated against their richer canonical structures, not merely by ID presence.
3. **Meta Schema Validation**: Achievement and Lore Codex entries are validated field-by-field; malformed entries no longer pass as generic “array of objects”.

---

## 🚨 **Error Handling**

### Error Types
1. **File System Errors**: Permission denied, disk full, corrupted files
2. **JSON Errors**: Invalid syntax, malformed structure
3. **Validation Errors**: Missing required fields, invalid references
4. **Logic Errors**: Rule conflicts, impossible state transitions

### Error Response Protocol
```typescript
// On any error during file operations
async function handleError(error: Error, context: string) {
  // 1. Restore all backup files
  await restoreAllBackups();
  
  // 2. Optional structured diagnostics
  await maybeWriteJSON('game_state/history/error_log.json', {
    timestamp: new Date().toISOString(),
    error: error.message,
    context: context,
    stackTrace: error.stack
  });
  
  // 3. Signal terminal error (authoritative)
  await writeJSON('ready/turn_error.json', {
    sessionId: turnRequest.sessionId,
    requestId: turnRequest.requestId,
    turnNumber: turnRequest.turnNumber,
    timestamp: new Date().toISOString(),
    status: 'error',
    error: error.message
  });
}
```

---

## 📖 **Lore Codex System**

The Lore Codex tracks knowledge the player has discovered during gameplay. It serves as an in-game encyclopedia that grows as the player explores, interacts with NPCs, completes quests, and discovers lore fragments.

### Lore Codex Architecture

**Two-layer system:**
1. **Lore files** (`lore/` directory) — detailed world information written by the GM directly to files (cosmology, geography, cultures, etc.). These are the "source of truth" for world knowledge.
2. **Codex index** (`lore/codex_entries.json`) — a player-facing index of discovered knowledge entries, managed via `loreCodexUpdates` command.

### `loreCodexUpdates` Command

```json
"loreCodexUpdates": [
  {
    "command": "add",
    "entry": {
      "entryId": "string (unique ID, e.g., 'lore-elder-dragon-origin')",
      "title": "string (display title in Russian)",
      "category": "string (cosmology|geography|history|cultures|creatures|characters|artifacts|factions|magic|other)",
      "subcategory": "string (optional, e.g., 'Древние расы', 'Горные регионы')",
      "content": "string (Markdown-formatted lore text in Russian)",
      "sourceFile": "string (optional, reference to detailed lore file, e.g., 'chaos_sea/cosmology.json')",
      "discoveredAt": "string (ISO 8601 timestamp)",
      "discoveryContext": "string (brief note: how player discovered this, e.g., 'Рассказ хранителя Игниса')",
      "relatedEntries": ["array of entryIds (optional cross-references)"],
      "incarnation": "integer (which incarnation this was discovered in, 0 = Chaos Sea)",
      "tags": ["array of strings (searchable tags)"]
    }
  },
  {
    "command": "update",
    "entryId": "string",
    "updates": {
      "content": "string (appended or replaced content)",
      "relatedEntries": ["updated cross-references"],
      "tags": ["updated tags"]
    }
  }
]
```

### `lore/codex_entries.json` File Structure

```json
{
  "entries": [
    {
      "entryId": "lore-chaos-sea-nature",
      "title": "Природа Моря Хаоса",
      "category": "cosmology",
      "subcategory": "Фундаментальные законы",
      "content": "Море Хаоса — бесконечное пространство между мирами, где обитают души...",
      "sourceFile": "chaos_sea/cosmology.json",
      "discoveredAt": "2026-03-01T10:00:00Z",
      "discoveryContext": "Первое пробуждение в Обители Хранителей",
      "relatedEntries": ["lore-guardians-overview", "lore-reincarnation"],
      "incarnation": 0,
      "tags": ["море хаоса", "космология", "души"]
    }
  ],
  "totalEntries": 1,
  "categories": {
    "cosmology": 1,
    "geography": 0,
    "history": 0,
    "cultures": 0,
    "creatures": 0,
    "characters": 0,
    "artifacts": 0,
    "factions": 0,
    "magic": 0,
    "other": 0
  }
}
```

### Lore File Population Protocol

**CRITICAL: GM MUST populate lore files at these trigger points:**

1. **New Game (Turn 1):** Initialize:
   - `lore/chaos_sea/cosmology.json`
   - `lore/chaos_sea/soul_system_lore.json`
   - `lore/chaos_sea/guardians_lore.json`
   - `lore/codex_entries.json`
   - `game_state/meta/achievements.json`
   - `game_state/meta/character_chronicle.json`
   
   Add initial `loreCodexUpdates` entries for what the player knows at start and a mandatory `characterChronicleUpdates` entry for the first-turn character chronicle. Fresh new game must not reuse stale session lore or old `stories/*.jsonl` continuity from a previous run.
   Accepted Turn 1 is invalid if the bootstrap files exist only as empty stubs and `lore/codex_entries.json.entries` remains empty.

2. **New Incarnation (entering mortal world):** Create ALL `lore/current_world/` files — `world_setting.json`, `geography.json`, `history.json`, `cultures.json`, `threats.json`. These describe the world the player is born into. Add corresponding codex entries. Previous-incarnation `lore/current_world/*` files are stale and must not be reused as the bootstrap for a new life.

3. **During Gameplay:** Update codex when player:
   - Talks to NPCs who reveal lore (history, culture, faction info)
   - Explores new regions (geography, local threats)
   - Reads books, scrolls, inscriptions (any lore category)
   - Completes quests that reveal story secrets
   - Interacts with Guardians (cosmology, artifacts, soul system)

4. **Life Evaluation (completed mortal life):** Update `lore/chaos_sea/player_chronicle.json` with life summary.
   - Trigger: death or voluntary end-of-life transition back to the Chaos Sea.
   - Mandatory reward guarantee: every completed mortal life must grant at least 10 Ink Feathers and at least one new Soul Relic.
   - Reward quality may vary by achievements and life performance, but zero-reward life evaluation is invalid.
   - Accepted life-evaluation turn is also invalid if `player_chronicle.json` does not gain a new summary entry for the completed life.
   - `TriggerLifeEnd` only starts this return/evaluation lifecycle. The final Ink Feather and Soul Relic reward belongs to the later Life Evaluation turn, not to the trigger turn itself.

5. **Ascension Transition (60 Enlightenment XP or legacy max marker + explicit player choice):**
   - Write `AscensionTrigger = true` and `playerChoice = "Ascension"` only when Enlightenment is ascension-ready: `enlightenment.experience` or `soulProgression.totalExperience >= 60`, or legacy max/Transcendence markers.
   - Runtime performs a real transition from `Chaos Sea` into `Shining Abode`.
   - `Shining Abode` remains an afterlife hub with guardian/soul/meta systems, not a Mortal World.
   - Client-owned `return_to_chaos_sea` / legacy `new_game_plus` starts the Shining Abode New Cycle: it seals Shining Abode, returns the soul to Chaos Sea, resets Enlightenment/Просветление to baseline, and preserves Ink Feathers, Soul Relics, Guardians, Shining achievements, halls, factions, and Radiance progress.
   - Do not mix `AscensionTrigger` with `TriggerLifeEnd` on the same accepted turn.
   - Do not use `AscensionTrigger` as a substitute for `Life Evaluation`.

---

## 🏆 **Achievement System**

Achievements track notable player accomplishments across all incarnations. They are persistent (never lost) and serve as a record of the player's journey through multiple lives.

### `achievementUnlocks` Command

```json
"achievementUnlocks": [
  {
    "achievementId": "string (unique ID, e.g., 'ach-first-blood')",
    "name": "string (display name in Russian)",
    "description": "string (description of what was accomplished, in Russian)",
    "category": "string (combat|exploration|story|social|crafting|meta|death|secret)",
    "rarity": "string (common|uncommon|rare|epic|legendary)",
    "icon": "string (emoji icon, e.g., '⚔️', '🗺️', '💀')",
    "incarnation": "integer (which incarnation this was earned in, 0 = Chaos Sea)",
    "unlockedAt": "string (ISO 8601 timestamp)",
    "progress": {
      "current": "integer (current progress value)",
      "target": "integer (target value for completion)"
    },
    "hidden": "boolean (if true, was hidden until unlocked)",
    "reward": {
      "type": "string (optional: 'inkFeathers'|'soulXP'|'title'|'none')",
      "value": "string or integer (reward amount or title text)"
    }
  }
]
```

### `game_state/meta/achievements.json` File Structure

```json
{
  "unlockedAchievements": [
    {
      "achievementId": "ach-first-blood",
      "name": "Первая кровь",
      "description": "Одержите первую победу в бою",
      "category": "combat",
      "rarity": "common",
      "icon": "⚔️",
      "incarnation": 1,
      "unlockedAt": "2026-03-01T12:30:00Z",
      "hidden": false,
      "reward": { "type": "none" }
    }
  ],
  "trackedProgress": [
    {
      "achievementId": "ach-veteran-warrior",
      "name": "Ветеран",
      "description": "Одержите 50 побед в бою",
      "category": "combat",
      "rarity": "rare",
      "icon": "🗡️",
      "progress": { "current": 12, "target": 50 },
      "hidden": false
    }
  ],
  "stats": {
    "totalUnlocked": 1,
    "byCategory": {
      "combat": 1,
      "exploration": 0,
      "story": 0,
      "social": 0,
      "crafting": 0,
      "meta": 0,
      "death": 0,
      "secret": 0
    },
    "byRarity": {
      "common": 1,
      "uncommon": 0,
      "rare": 0,
      "epic": 0,
      "legendary": 0
    }
  }
}
```

### Achievement Categories

| Category | Icon | Description | Examples |
|----------|------|-------------|----------|
| `combat` | ⚔️ | Battle victories and combat feats | First kill, boss kills, flawless victory |
| `exploration` | 🗺️ | Discovering locations and secrets | Visit 10 locations, find hidden area |
| `story` | 📖 | Quest completions and story milestones | Complete main quest, critical choice |
| `social` | 🤝 | NPC relationships and faction standing | Max reputation, romance, betrayal |
| `crafting` | 🔨 | Item creation and economic achievements | Craft legendary item, earn 10000 gold |
| `meta` | 🌌 | Soul progression and cross-incarnation | Reach Enlightenment 5, collect 10 relics |
| `death` | 💀 | Death-related and incarnation achievements | Die 5 times, live 100 turns, heroic death |
| `secret` | ❓ | Hidden achievements revealed on unlock | Easter eggs, unique interactions |

### Achievement Trigger Protocol

**GM MUST check for achievement unlocks during turn processing:**

1. **After Combat Resolution:** Check combat achievement conditions (kills, combos, flawless wins)
2. **After Location Changes:** Check exploration achievements (new locations, biomes visited)
3. **After Quest Updates:** Check story achievements (quest completions, milestone reaches)
4. **After NPC Interaction:** Check social achievements (reputation thresholds, relationship milestones)
5. **After Crafting/Trading:** Check crafting achievements (items created, wealth accumulated)
6. **During Life Evaluation:** Check death/meta achievements (lives lived, soul progress)
7. **Always:** Check progress-based achievements and update `trackedProgress` counters

**Achievement unlocks MUST be mentioned in the narrative response** with the marker `[ACHIEVEMENT_UNLOCK: Achievement Name]` so the client can highlight them.
This narrative marker accompanies, but does not replace, the canonical `achievementUnlocks` command and resulting `game_state/meta/achievements.json` update.

---

## 🛡️ **Guardian System (UpdateGuardians)**

Guardians are metaphysical afterlife entities centered in the Chaos Sea and still present in the Shining Abode. They use their own system, completely separate from NPCs.

Image authoring contract for Guardian data:
- Every Guardian object should carry its own `image_prompt`.
- If a Guardian has an `abode` object, that `abode` should also carry its own `image_prompt`.
- The client can generate and store images both for the Guardian and for the Guardian's abode independently.

**Reputation Scale:** -100 to +300 (400 units total, 6 tiers)

| Tier | Range | Description |
|------|-------|-------------|
| Hostile | -100..-51 | Guardian actively opposes the player |
| Wary | -50..-21 | Guardian distrusts the player |
| Neutral | -20..+49 | Default starting tier |
| Friendly | +50..+129 | Strong positive standing, better rewards and quest access |
| Devoted | +130..+229 | Very high trust, personal quests and premium rewards |
| Legendary | +230..+300 | Near-mythic standing, unique relic and exclusive content access |

### Canonical Inter-Guardian Standing

Guardian-to-guardian politics uses canonical `guardianRelationships[]`, not player-facing Guardian reputation.

Each Guardian object should carry:

```json
"guardianRelationships": [
  {
    "targetGuardianId": "string",
    "targetName": "string|null",
    "attitudeScore": "integer (-100..100)",
    "attitudeTier": "string (trusted|ally|neutral|competitive|rival|enemy)",
    "reason": "string",
    "lastChangedAt": "string|null (ISO 8601 timestamp)"
  }
],
"socialProfile": {
  "jealousyFactor": "integer 0..100",
  "curiosityFactor": "integer 0..100",
  "competitiveFactor": "integer 0..100",
  "generosityFactor": "integer 0..100",
  "isolationistTendency": "integer 0..100"
}
```

- `guardianRelationships[]` is a directed network: `A -> B` may differ from `B -> A`.
- The canonical network should contain one directed entry for every other known Guardian.
- `socialProfile` is a personality modifier layer for seeding and reactions; it does not replace canonical pairwise standing.
- Strong hostile or friendly standing should usually come from authored reasons or major events, not from random drift.

### `UpdateGuardians` Commands

```json
"UpdateGuardians": [
  // Create a new Guardian
  {
    "command": "create",
    "data": { /* full Guardian object per Block_32 schema */ }
  },
  // Special case: create uses nested data with guardianId inside the full Guardian object.
  // Update reputation
  {
    "command": "updateReputation",
    "guardianId": "string",
    "reputationChange": "integer",
    "reason": "string"
  },
  // Complete a Guardian quest
  {
    "command": "completeQuest",
    "guardianId": "string",
    "questId": "string",
    "outcome": "string (success|failure|partial)"
  },
  // Process Gacha (Soul Relic pull)
  {
    "command": "processGacha",
    "guardianId": "string",
    "inkFeathersSpent": "integer",
    "result": { /* minimal Soul Relic result stub: relicId + name + rarity */ }
  },
  // processGacha.result is a reward/result surface, not the full canonical relic inventory payload.
  // Use the full Soul Relic Object when writing canonical soul/meta relic state.
  // Guardian-mediated processGacha is limited per Guardian per return from mortal life:
  // reputation tier defines the BASE charges: Hostile(-100..-51)=0, Wary/Neutral(-50..49)=1, Friendly(50..129)=2, Devoted/Legendary(130..300)=3.
  // abode power may add bonus charges on top of the reputation-based base.
  // Charges reset only when the Soul returns to the Chaos Sea after a new mortal life.
  // If a Guardian has no remaining charges this return, do NOT emit processGacha for that Guardian.
  // A successful processGacha consumes one Guardian charge for the current return.
  // Direct Chaos Sea pull via /gacha should NOT use processGacha and must keep final rarity exactly equal to gachaBaseResult.baseRarity.
  // It should resolve directly into soul/meta state without guardian-specific modifiers and without consuming Guardian charges.
  // --- Inner Life Commands (Block_32_extension_2) ---
  // Add Guardian musings (1-2 per turn)
  {
    "command": "addMusings",
    "guardianId": "string",
    "musings": [
      {
        "turn": "integer",
        "topic": "string (soul_assessment|domain_insight|guardian_politics|chaos_sea|personal_reflection|quest_planning)",
        "mood": "string (content|intrigued|concerned|amused|proud|disappointed|wary|nostalgic|determined|melancholic|excited|contemplative|irritated|hopeful)",
        "text": "string (Russian)"
      }
    ]
  },
  // Unlock a lore fragment at reputation threshold
  {
    "command": "unlockLore",
    "guardianId": "string",
    "loreFragment": {
      "fragmentId": "string",
      "category": "string (personal_history|cosmic_secret|domain_mastery|lost_world|other_guardians|soul_mechanics)",
      "title": "string",
      "content": "string (Russian)",
      "requiredReputation": "integer (0|50|130|230)"
    }
  },
  // Set Guardian mood
  {
    "command": "setMood",
    "guardianId": "string",
    "mood": {
      "current": "string (welcoming|contemplative|energized|melancholic|irritated|proud|suspicious|playful|focused|nostalgic)",
      "intensity": "integer (1-100)",
      "reason": "string",
      "since": "integer (turn number)"
    }
  }
]
```

### Guardian Project Commands

Guardian project lifecycle no longer uses `UpdateGuardians.updateProject`.
Use dedicated top-level surfaces instead:

```json
"startGuardianProjects": [
  {
    "guardianId": "string",
    "project": { /* full active guardian project object */ }
  }
],
"guardianProjectUpdates": [
  {
    "guardianId": "string",
    "projectId": "string",
    "activeState": "string",
    "workDone": "integer",
    "currentStage": "integer",
    "pressure": "integer",
    "stability": "integer",
    "pressureAudit": { /* object */ },
    "stabilityAudit": { /* object */ },
    "workAudit": { /* object */ }
  }
],
"completeGuardianProjects": [
  {
    "guardianId": "string",
    "projectId": "string",
    "finalState": "Completed | Abandoned | Sabotaged | Collapsed",
    "outcome": "string",
    "abodePowerDelta": "integer",
    "targetGuardianId": "string|null",
    "betrayalReason": "string|null",
    "offensiveImpactAudit": { /* object or null */ }
  }
]
```

- `betrayalReason` is optional by default, but completed `offensive_intrigue` against an `ally|trusted` target must have an explicit betrayal rationale either on the active project itself or on the completion command.
- Completed `offensive_intrigue` may include relation-derived `targetAttitudeScore`, `targetAttitudeTier`, `hostilityWeight`, and `preferredHostileTarget` fields inside `offensiveImpactAudit`.
- Completed `counter_rival_operation` may include relation-derived `coalitionSupportBonus` and `coalitionEligible` fields inside `projectOutcomeAudit` only when non-hostile Guardians coordinate against the same hostile target through an explicit current political project trace.

Guardian quest origin contract:
- `questOrigin = lore_research_hook` -> ordinary lore-research hook quest, consumes one `questHookToken`
- `questOrigin = lore_research_special_line` -> special quest line unlocked by `lore_research`
- `questOrigin = archive_consultation_hook` -> guaranteed extra guardian quest created from `archive consultation` with a `lore_fragment`
- `questOrigin = guardian_baseline_mortal_life_hook` -> voluntary / добровольное baseline Guardian assignment for a future mortal life; it is allowed for Wary/Neutral or better roleplay scenes when the Guardian offers a simple hook without forcing acceptance
- Every object in `guardian.questManagement.availableQuests`, `activeQuests`, and `completedQuests` must carry a non-empty `questId`; do not use `title`, `questOrigin`, or `sourceProjectId` as surrogate identity.
- The three lore/archive origins must carry `sourceProjectId` of the completed `lore_research` project that granted the effect; `guardian_baseline_mortal_life_hook` does not require `sourceProjectId`.
- A baseline Guardian hook is an offer, not an accepted quest. Do not move it to `activeQuests` unless the player explicitly accepts it in roleplay or through a later command. Do not treat an empty `availableQuests` array as a validation error.
- If a quest with `archive_consultation_hook` is completed, keep `questOrigin` and `sourceProjectId` in `completedQuests` so the guaranteed-origin audit trail survives completion.

Guardian quest lifecycle:
- `availableQuests[].status` may be omitted or `available`: the Guardian is offering the quest, but the player has not accepted it.
- `activeQuests[].status` may be omitted for legacy saves, or be `active`, `ready_to_turn_in`, `failed`, or `expired`.
- During Mortal World play, the GM can use `guardianQuestProgressUpdates[]` to update an already accepted active Guardian quest:
```json
"guardianQuestProgressUpdates": [
  {
    "guardianId": "guardian_azalia",
    "questId": "quest_azalia_rare_ore_echo",
    "status": "ready_to_turn_in",
    "progressSummary": "Игрок нашёл редкую руду в смертной жизни; физический предмет остался в мире.",
    "readyToTurnInEvidence": {
      "itemEcho": {
        "mortalItemName": "Серебряная руда сна",
        "proofKind": "memory_imprint",
        "summary": "Душа сохранила слепок структуры руды и место находки."
      },
      "lifeEventEvidence": "Событие поиска записано в памяти этой жизни."
    },
    "turnInRequirement": "Вернуться к Хранителю и передать слепок/резонанс, не физический предмет."
  }
]
```
- `ready_to_turn_in` requires non-physical evidence: `memoryImprint`, `lifeEventEvidence`, `itemEcho`, `locationWitness`, `craftedOutcome`, `knowledgeTrace`, or `soulResonance`.
- Forbidden evidence fields: `physicalItem`, `inventoryItem`, `transferredItem`, `transferredItemId`, `mortalInventoryTransfer`. Mortal inventory does not cross into afterlife.
- After the soul returns to the Guardian, close the quest with `UpdateGuardians.completeQuest`. If the active quest explicitly has `status=active`, `failed`, or `expired`, do not use `completeQuest` as if it were ready for hand-in.

### Guardian Abode Power Events

Canonical Abode Power changes must flow through `guardianPowerEvents` or be client-materialized from guardian project completion into the same journal/history model. Do not treat raw `guardian.abodePower.currentPower` mutation as the primary GM-facing contract.

```json
"guardianPowerEvents": [
  {
    "eventId": "string",
    "guardianId": "string",
    "delta": "integer",
    "reasonType": "guardian_quest | project_assist | project_completion | project_failure | offering | resonance | correction_spend | rival_strike | rival_defense",
    "sourceSurface": "string",
    "sourceId": "string",
    "title": "string",
    "summary": "string",
    "visibility": "player_known | hidden",
    "relatedGuardianId": "string|null",
    "audit": { /* machine-readable audit object required */ }
  }
]
```

### Guardian Corrections / Scenario Core / Afterlife Control Files
- `game_state/control/next_life_scenario_core.json` ← client-owned Scenario Core manifest for the next life. The GM may read it as bootstrap context but must not edit, clear, or close it through a receipt. `scenarioCoreAssertions[]` are hard facts that next-life bootstrap must not contradict; `candidateAssertions[]` are candidate-only hints until later accepted state confirms them.
- `game_state/world/guardian_corrections.json` ← applied guardian corrections for the current life
- `game_state/control/pending_abode_offering.json` ← client-authored pending offering request
- `game_state/control/archive_candidate_manifest.json` ← client-authored Life Evaluation manifest for codex-derived archive candidates
- `game_state/control/pending_guardian_trade_request.json` ← client-authored request to materialize an explicit guardian trade inventory for the current return-cycle
- `UpdateGuardianTradeInventoryReceipts` ← GM-authored receipt surface written into `game_state/meta/guardians.json`; each receipt closes one pending guardian trade inventory request with `status = ready`
- `game_state/control/pending_npc_trade_inventory_requests.json` / `[NPC_TRADE_REQUEST]` ← MortalWorldProfile-only client-authored request to materialize an explicit NPC trade inventory for the current world-time cycle; in `Chaos Sea` or `Shining Abode`, preserve as wrong-realm repair-only context and do not write NPC trade receipts or `game_state/npcs/*`
- `UpdateNpcTradeInventoryReceipts` ← GM-authored receipt surface written into `game_state/npcs/npc_core.json`; each receipt closes one pending NPC trade inventory request with `status = ready`
- `game_state/control/pending_guardian_abode_residents_request.json` ← client-authored requests to materialize explicit afterlife residents for one or more Guardian Abodes, stored as `requests[]`
- `game_state/control/pending_guardian_abode_resident_interactions.json` ← client-authored talk/history requests for afterlife residents, stored as `requests[]`
- `game_state/control/pending_resident_companion_manifestation_request.json` ← MortalWorldProfile-only next-life manifestation requests for equipped `companion_echo` Soul Relics and for equipped Soul Relics that carry embedded `soulImprint` / `npcSoulImprint`, stored as `requests[]`; do not process this file as an afterlife turn contract in `Chaos Sea` / `Shining Abode`. This file can originate from an afterlife resident reward or imprint, but closure happens later after Mortal bootstrap using source fields such as `sourceResidentId`, `sourceImprintId`, `sourceGuardianId`, `futureCompanionPrompt`, and `targetIncarnation`. If it appears during afterlife, valid non-empty files are preserved as next-life context and do not block Soul Gates; valid empty files may be removed as stale clutter; malformed files are preserved and block Soul Gates until repair. The GM must not create afterlife receipts, Mortal NPCs, or world events for it.
- `game_state/control/pending_archive_consultation_request.json` ← client-authored request over a reserved archive entry for consultation
- `game_state/control/pending_archive_project_fuel_request.json` ← client-authored request over a reserved archive entry for project fuel
- `game_state/control/afterlife_notifications.json` ← client-owned inbox of guardian-system events: GM responses for guardian trade readiness, archive action outcomes, new guardian quests materialized from canonical quest origins, and mechanical resident events (roster ready / resident-linked soul quest / relic grant)
- `archiveActionResolutions` ← GM-authored resolution surface written into `game_state/meta/soul_state.json`; each resolution closes a pending archive request with `status = accepted | rejected | cancelled`
  - for accepted `consultation`, also pass machine-readable whitelist outcome fields: `guaranteedArchiveQuestCount`, `questHookCount`, `specialQuestLineUnlocks`, `visibleRivalClueBonus`, `archiveWarningTierBonus`
  - for accepted `project_fuel`, also pass `resultMode = project_work | pressure_relief` and `resultAmount > 0`
- `afterlifeArchiveUpdates` ← exceptional/system archive rewards written into `game_state/meta/soul_state.json`. Valid commands are `add` with a complete `entry` object or `remove` with `archiveId`; ordinary archive consultation/project-fuel uses `archiveActionResolutions`, not improvised add/remove.
- `afterlifeArchive.stored[]` entries may carry:
  - optional `sourceGuardianId`
  - required archive identity when authored through `afterlifeArchiveUpdates.add`: `archiveId`, `entryType = lore_fragment | secret_record`, `title`, `summary`, `rarity`, `sourceLife`, `acquiredAtUtc`, optional `sourceGuardianId`, `sourceGuardianName`, `sourceEntryId`, `sourceKind = codex | system`, `tags[]`, and optional `reservation`
- `archive consultation` and `archive project fuel` are client-side afterlife actions built on top of `afterlifeArchive.stored[]`, but they now use pending client-authored requests plus GM-materialized canonical results; the client does not derive compatibility from guardian domain.
- `playerAction` may include hidden routing tags for afterlife pending contracts. They are not optional flavor text:
  - `[GUARDIAN_TRADE_REQUEST]` -> read `pending_guardian_trade_request.json` and close it through `guardian.tradeInventory` plus `UpdateGuardianTradeInventoryReceipts`
  - `[GUARDIAN_SOCIAL_TALK_REQUEST]` -> read `pending_guardian_social_interactions.json` and close the matching `interactionType=talk` through `guardianSocialJournalUpdates`
  - `[GUARDIAN_SOCIAL_LORE_REQUEST]` -> read `pending_guardian_social_interactions.json` and close the matching `interactionType=lore` through `guardianSocialJournalUpdates`
  - `[ARCHIVE_CONSULTATION_REQUEST]` -> read `pending_archive_consultation_request.json` and close it through `archiveActionResolutions`
  - `[ARCHIVE_PROJECT_FUEL_REQUEST]` -> read `pending_archive_project_fuel_request.json` and close it through `archiveActionResolutions`
  - `[ABODE_RESIDENT_ROSTER_REQUEST]` -> read `pending_guardian_abode_residents_request.json` and close it through `UpdateGuardianAbodeResidents` plus `UpdateGuardianAbodeResidentRosterReceipts`
  - `[ABODE_RESIDENT_HISTORY_REQUEST]` -> read `pending_guardian_abode_resident_interactions.json` and close the matching history request through `UpdateGuardianAbodeResidentInteractionReceipts` plus `UpdateGuardianAbodeResidentHistoryLog`
  - `[ABODE_RESIDENT_TALK]` -> read `pending_guardian_abode_resident_interactions.json` and close the matching talk request through `UpdateGuardianAbodeResidentInteractionReceipts`
  - `[ABODE_RESIDENT_TRANSFER_REQUEST]` -> read `pending_guardian_abode_resident_transfers.json` and close through `UpdateGuardianAbodeResidentTransferReceipts`, resident state, and transfer history
  - `[PLAYER_GUARDIAN_FOUNDATION]` -> read `pending_player_guardian_foundation.json` and close it through the player-founded Guardian authority surfaces
- `UpdateGuardianAbodeResidents` is an explicit authored roster surface for afterlife residents in a Guardian's Abode; the client does not derive residents from guardian domain.
- `guardian_abode_residents.json` may also carry:
  - `rosterReceipts[]` from `UpdateGuardianAbodeResidentRosterReceipts`
  - `interactionReceipts[]` from `UpdateGuardianAbodeResidentInteractionReceipts`
  - `transferReceipts[]` from `UpdateGuardianAbodeResidentTransferReceipts`
  - `historyLog[]` from `UpdateGuardianAbodeResidentHistoryLog`
  - `thoughtJournal[]` from `residentThoughtJournalUpdates`
  - `interactionLog[]` from `residentInteractionLogUpdates`
- `guardianThoughtJournalUpdates` writes `game_state/meta/guardian_thought_journal.json`
- `guardianSocialJournalUpdates` writes `game_state/meta/guardian_social_journal.json`
- `npcInteractionJournalUpdates` writes `game_state/npcs/npc_interaction_journal.json`
- Resident roleplay scenes remain freeform, but resident `talk` / `history` requests are now explicit client-authored requests that must be closed canonically through `interactionReceipts[]` with `status = accepted | rejected | cancelled`.
- Mechanical resident results should still materialize canonically in state:
  - resident-linked soul quests through `UpdateSoulQuests` + `relatedAfterlifeResidentId`
  - resident reward relics through `metaStateUpdates.soulRelicOperations.addRelic`
  - resident state changes such as `linkedSoulQuestId`, `historyRevealed`, `bondRewardState`, and `grantedRelicId` through `UpdateGuardianAbodeResidents`
  - revealed history fragments through `UpdateGuardianAbodeResidentHistoryLog`
- Direct resident action tags are player-action contracts without pending files:
  - `[ABODE_RESIDENT_RELIC_GRANT]` means the player accepts a companion-echo reward from an existing afterlife resident. The accepted turn must add a new current-turn `companion_echo` Soul Relic with a complete `companionSeed`, update that pre-existing resident in the same turn with `bondRewardState=granted` and `grantedRelicId=<new relicId>`, and add a new current-turn `residentInteractionLogUpdates` entry. A relic or granted reward that already existed before the turn does not close this tag.
  - `[ABODE_RESIDENT_QUEST_REQUEST]` means the player accepts or helps a request from an existing afterlife resident. The accepted turn must use ordinary `UpdateSoulQuests` with `relatedAfterlifeResidentId` to create or progress a Soul Quest in the current turn, may update the resident with `linkedSoulQuestId` / bond fields through `UpdateGuardianAbodeResidents`, and must add a new current-turn `residentInteractionLogUpdates` entry. An unchanged pre-existing quest or old log entry does not close this tag.
  - These direct tags do not close `pending_guardian_abode_resident_interactions.json`, `pending_guardian_abode_residents_request.json`, or `pending_guardian_abode_resident_transfers.json`; do not invent `UpdateGuardianAbodeResidentInteractionReceipts`, roster receipts, or transfer receipts unless the matching pending file exists.
  - Do not use `UpdateNPCs`, Mortal `UpdateQuests`, or `pending_resident_companion_manifestation_request.json` for these direct afterlife resident actions.
- Accepted resident social outcomes must also leave curated memory:
  - accepted `talk` -> `residentThoughtJournalUpdates` and/or `residentInteractionLogUpdates`
  - accepted `history` -> `residentThoughtJournalUpdates` and/or `residentInteractionLogUpdates`, in addition to canonical history aftermath
  - resident quest grant/progress -> `residentInteractionLogUpdates`
  - resident reward grant -> `residentInteractionLogUpdates`
- Actor memory journals are short summaries, not transcripts:
  - thought journals store current inner stance, intent, and attitude
  - event journals store significant past interactions and consequences
- Typical flow for GM continuity:
  1. read curated actor memory journals / continuity digest
  2. if details are needed, search `stories/*.jsonl`
- `Tools/Search-GmMemory.ps1` also supports `-Source` (for example `stories`, `journals`, `continuity`, or a concrete source name), `-Json` for machine-readable lookup, and `entityType=faction` for faction-scoped continuity search.
- Old `stories/*.jsonl` entries that predate `entityRefs[]` remain searchable through text fallback; they simply do not participate in actor-scoped matching until newer turns write explicit `entityRefs[]`.
- Accepted `history` interaction must leave a canonical result: `historyRevealed=true`, and/or a new `historyLog[]` entry, and/or an updated `mortalWorldImprint`.
- Resident memory obligations are enforced only where the client has explicit canonical hooks; the runtime does not try to infer social significance from freeform narrative text alone.
- `pending_guardian_social_interactions.json` and `pending_npc_social_interactions.json` are client-authored closure surfaces for explicit guardian/NPC social requests, but they are realm-scoped differently: Guardian social requests are afterlife-capable, while NPC social requests are MortalWorldProfile-only.
- When those files are used in their valid realm, the accepted turn must close each request canonically through the corresponding event journal:
  - guardians -> `guardianSocialJournalUpdates`
  - NPCs -> `npcInteractionJournalUpdates`
- In `Chaos Sea` or `Shining Abode`, `pending_npc_social_interactions.json` is wrong-realm repair-only context and must not be closed through `npcInteractionJournalUpdates`, `UpdateNPCs`, or `game_state/npcs/*`; non-empty or malformed files block Soul Gates until repair or Mortal resolution.
- NPC social requests may include `topic`; in Mortal World the accepted turn must answer that topic explicitly in the scene and include the outcome in the matching closure summary.
- Journal closure entries must carry `requestId`, actor id, `interactionType`, `status = accepted | rejected | cancelled`, optional `responseMode`, plus the ordinary `title/summary/turn/timestamp`.
- Guardian social `responseMode` values are `talk_scene`, `lore_revealed`, `lore_refused`, `warning`, `refusal`, `trust_shift`, and `attitude_shift`.
- Resident interaction `responseMode` values are `talk_scene`, `history_revealed`, `history_refused`, `history_partial`, and `bond_shift_only`.
- Resident transfer `selectionMode` values are `competition_recommended`, `manual_override`, and `departure_only`; preserve `competitionScore`, `competitionLabel`, and `competitionReason` from pending transfer requests when they are present.
- Freeform guardian/NPC scenes that do not use these explicit request surfaces still remain advisory-memory only.
- The next-life companion manifestation bullets below apply to `MortalWorldProfile` bootstrap / early Mortal World turns only. In `Chaos Sea` or `Shining Abode`, valid `pending_resident_companion_manifestation_request.json` is preserved next-life context and must not create mortal NPC output; malformed manifestation files are repair blockers.
- A resident who can be carried into a future life should grant a Soul Relic with `relicType = companion_echo` and a complete `companionSeed`.
- If a resident has already granted a companion-carrying relic, preserve `grantedRelicId` in resident state so the reward is cross-linked to the actual Soul Relic.
- Independently of that, any Soul Relic that carries an embedded `soulImprint` / `npcSoulImprint` is also eligible for next-life companion manifestation through the same pending request layer.
- Companion manifestation eligibility is NOT capped by ordinary relic-slot overlap. If several equipped companion-carrying relics are present, the client creates manifestation requests for all of them, even if they share the same displayed slot.
- When a companion fully manifests into mortal NPC state, write `sourceCompanionRelicId` on that NPC. If applicable, also write `sourceAfterlifeResidentId` and/or `sourceSoulImprintId`, but `sourceCompanionRelicId` is the canonical unambiguous resolution key.
- Afterlife inbox may report successful companion manifestation through two distinct notification types:
  - `abode_resident_manifestation_ready` for resident-linked companion echoes
  - `companion_imprint_manifestation_ready` for relics that carry embedded `soulImprint` / `npcSoulImprint`
- Pending archive actions reserve the archive entry immediately. The entry is consumed only on `accepted`; on `rejected` or `cancelled` it returns to normal archive availability.
- `pendingShiningBlessingEffects` is runtime-created from `preparedIncarnationPackage.selectedCards` after Mortal bootstrap, not authored during afterlife. Supported pending families/statuses include `memorySelection.status=pending_pre_turn_one_selection`, `resourceGrant.status=applied_at_bootstrap`, `relicRefinementEntitlements.status=pending_relic_entitlement`, `pendingSocialEffects.status=pending_first_relation_commit`, `pendingRouteEffects.status=pending_early_route_seed`, `pendingLoreEffects.status=pending_lore_insertion`, `pendingSurvivalEffects.status=pending_first_ruinous_failure`, and `pendingDescentEffects.status=pending_resident_descent`. Most families are consumed through ordinary Mortal state surfaces, but `relicRefinementEntitlements` is the explicit Shining forge exception: Shining forge previews/requests can consume rerolls/freeShape/freeRetune in Shining Abode, while the GM closes only the resulting `pending_shining_abode_actions.json` forge receipt/state contract. For `relicRefinementEntitlements`, terminal status is only `consumed` with `consumedAtTurn`/`consumedAtUtc`; do not write `expired` there. Deadline-based `expired` with `expiredAtTurn`/`expiredAtUtc` applies only to supported route/lore/descent style deferred effect arrays.
- `shining_abode_state.json.availability` uses accepted values such as `active` and `sealed_until_next_ascension`. Ordinary active Shining gameplay requires `availability = active` plus null/absent `preparedIncarnationPackage`; `sealed_until_next_ascension` is not an active Shining gameplay mode. Pending-bootstrap is derived from non-null `preparedIncarnationPackage`, not from an `availability` value.
- Shining resident normalization is canonical and may rewrite non-authoritative affiliation fields. A resident without `ascensionState = "ascended"` is normalized to `remained_in_chaos_sea`; its `shiningFactionId` and `residentRole` are cleared, `factionLoyaltyLevel` becomes `0`, `factionLoyaltyTier` becomes `alienated`, `factionRestlessness` becomes `0`, and `factionRealignmentState` becomes `settled`. When an ascended resident points at a missing Shining faction, runtime preserves `ascensionState = "ascended"` and treats the resident as ascended but unaffiliated: the same Shining affiliation fields are cleared/reset, but the resident is not downgraded to `remained_in_chaos_sea`. For valid ascended residents in an existing faction, the runtime derives or validates `residentRole`, `factionLoyaltyLevel`, `factionLoyaltyTier`, `factionRestlessness`, and `factionRealignmentState`; do not use ad-hoc fields such as `shiningAlignment`.
- `afterlife_notifications.json` is fully client-derived from existing canonical request/result surfaces and canonical guardian quest availability; the GM does not author inbox text manually. The current exhaustive notification-type vocabulary and trigger families are maintained in `OtherGuides/Afterlife_Contract_Matrix.md`; update that matrix and coverage tests whenever runtime adds a notification type.

### Rival Arc Bonus Clue Attribution
- If a player-visible extra rival clue is revealed through completed `lore_research`, use:
  - `bonusClueSourceProjectId`
  - `bonusClueRevealId`
  - optional `bonusClueCost`
- This applies to both `publicSignals` and linked `worldEventsLog` entries.
- Every `publicSignals[]` object must include boolean `visibleToPlayer`.
- Linked `worldEventsLog` entries count as player-visible rival clues only when `visibility` is `Public`, `Regional`, or `player_known`.
- If a `Secret` or `Faction-Internal` event becomes known to the player through actual play, use `visibility = player_known` on the linked player-facing world event entry.
- Hidden linked `Secret` / `Faction-Internal` world events do not consume visible rival clue budget until they become `Public`, `Regional`, or `player_known`.
- If the same extra clue is mirrored across both surfaces, reuse the same `bonusClueRevealId` so the client consumes clue budget only once.

### Guardian Data Storage
- `game_state/meta/guardians.json` ← Guardian core state (identity, reputation, abodePower, mood, lore, musings, socialProfile, guardianRelationships)
- `game_state/meta/guardian_projects.json` ← authoritative guardian project tracker
- `game_state/meta/guardian_project_journal.json` ← player-facing readable guardian project chronology
- `game_state/meta/abode_power_journal.json` ← player-facing readable journal of Abode Power changes
- **NOT** stored in `game_state/npcs/` — Guardians are NOT NPCs

### Guardian Inner-Life Metadata Contract
- The following Guardian inner-life fields are canonical machine-readable metadata and may be validator-enforced as exact enum/range values:
  - `musings[].topic`
  - `musings[].mood`
  - `loreFragment.category`
  - `loreFragment.requiredReputation`
  - `mood.current`
  - `mood.intensity`
- Source of truth for these enums/ranges:
  - `Rules/Block_32_extension_2.txt` for musings, lore fragments, and mood taxonomy
  - `Rules/Block_32_Guardians.txt` for Guardian reputation-tier/gacha/trade mappings
- Free-text Guardian roleplay fields are still narrative surfaces and are **not** meant to be enum-validated:
  - `musings[].text`
  - `loreFragment.title`
  - `loreFragment.content`
  - `mood.reason`
- Practical interpretation:
  - validator may reject wrong structured tags/ranges;
  - validator should not judge whether Guardian prose is “good roleplay”.

### Cross-System Rules
- Guardians belong to afterlife realms (`Chaos Sea` and `Shining Abode`) and never to `Mortal World`
- Do NOT use `UpdateNPCs` for Guardians
- Do NOT generate `NPCsInScene` with Guardians
- Guardian reputation uses its own scale (-100..+300), NOT NPC scale (-400..+400)
- See `Rules/Block_32_Guardians.txt`, `Block_32_extension.txt`, `Block_32_extension_2.txt` for full rules

---

## 📚 **Examples**

### Complete Turn Processing Example

**Input** (`input/turn_request.json`):
```json
{
  "sessionId": "game-session-123",
  "requestId": "request-42",
  "turnNumber": 42,
  "playerAction": "Я подхожу к торговцу и спрашиваю о доступных товарах",
  "timestamp": "2026-03-01T10:00:00Z",
  "gameMode": "normal"
}
```

**Generated JSON Response** (internal):
For brevity, the sample below shows only the NPC fields relevant to the turn. In a real CLI turn, each `UpdateNPCs` item must still be a complete NPC object, not a partial patch.
```json
{
  "response": "Торговец поднимает глаза от своих записей и дружелюбно улыбается...",
  "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: Торговец\n- Why relevant: Turn updates the merchant interaction state and dialogue.\n- Actors outside scope: other scene NPCs, Guardians\n- Why outside scope: No other structured actor surfaces are changed.\n\n## Reasoning\n### Торговец\n- Игрок инициирует торговый диалог, поэтому merchant interaction state и dialogueOptions обновляются в этом ходу.",
  "dialogueOptions": [
    {
      "optionId": "trade_weapons",
      "text": "«Покажите оружие»",
      "category": "trade"
    },
    {
      "optionId": "trade_armor", 
      "text": "«Есть ли доспехи?»",
      "category": "trade"
    }
  ],
  "UpdateNPCs": [
    {
      "NPCId": "npc-merchant-01",
      "name": "Merchant Arven",
      "currentLocationId": "market-square-001",
      "currentActivity": null,
      "completedActivities": [],
      "lastInteraction": "2026-03-01T10:00:00Z",
      "interactionType": "trade_inquiry",
      "playerReputation": 15
    }
  ]
}
```

**File Distribution Result**:
```
output/narrative_response.json ← response field
output/interface_updates.json ← optional dialogueOptions / image_prompt payload when this turn actually changes the interface
output/debug_logs.json ← gm_thoughts_markdown field
game_state/npcs/npc_core.json ← UpdateNPCs field
ready/turn_complete.json ← terminal success signal
ready/turn_error.json ← terminal error signal
```

### Soul System Example

See `Examples/CLI_Example_Soul_System.md` for complete Soul Relic distribution example.

---

## 🎯 **Quick Reference**

### Essential Files for CLI Agent
1. **TaskGuides/CLI_Step_Main.txt** - Main workflow instructions
2. **Rules/Block_CLI_Operations.txt** - Detailed file operations protocol  
3. **Examples/CLI_Translation_Guide.md** - How to handle API examples
4. **OtherGuides/Afterlife_Contract_Matrix.md** - Mandatory Chaos Sea / Shining Abode contract map: scheduler contours, pending files, legal surfaces, receipts, reports, and forbidden substitutions
5. **Examples/E_CLI_Afterlife_Turns.txt** - Worked Chaos Sea / Shining Abode examples, including Shining core actions and freeform Guardian command examples
6. **This API Spec** - Complete data structure reference

### Critical Success Factors
1. ✅ **Read ALL game rules** (Rules/Block_*.txt) before processing
2. ✅ **Generate complete JSON** using existing rule logic
3. ✅ **Distribute atomically** to files per CLI.3 mapping
4. ✅ **Validate consistency** across files
5. ✅ **Signal terminal outcome** via ready/turn_complete.json or ready/turn_error.json

### Language Requirements
- **Input processing**: Handle Russian player actions
- **Rule application**: Process in any language internally 
- **Output generation**: ALL user-facing content in Russian
- **File naming**: Use English for technical files, Russian for content

---

**🎮 END OF CLI API SPECIFICATION**

*This document provides comprehensive guidance for CLI agents working with The Book of Eternity Reborn game system. For implementation questions, refer to the detailed rule blocks and examples in the Rules/ and Examples/ directories.*
