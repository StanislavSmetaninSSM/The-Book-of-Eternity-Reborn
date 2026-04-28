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
| `shiningTradeCyclesExpectedThisTurn` / `mustEvaluateShiningTradeProgression` | Mandatory Shining trade/economy progression using Shining/Guardian trade surfaces, receipts, and afterlife notifications derivation rules. |
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
- `game_state/meta/shining_abode_state.json`: Shining availability, Light Sparks, Radiance, halls, gates, factions, projects, `shiningPoliticalActors`, trade inventories, receipts, prepared incarnation package.
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
- `progressionProcessingReport`

Forbidden substitutions for afterlife scheduler debt:
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
  "guardians": "array of canonical guardian_objects when a contract explicitly requires full guardian-state authority",
  "activeGuardian": "canonical active guardian object or id-bearing object for guardian-state synchronization",
  "chaosSeaNavigation": "object with currentAbodeId and discoveredAbodes for afterlife navigation; [CHAOS_SEA_TRAVEL] must also keep activeGuardian synced to the target guardian and target guardian abode.isDiscovered=true",
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
  "TriggerIncarnation": "object with mandatory worldDescription, characterDescription, circumstances (ordinary Chaos Sea lifecycle control, or Shining Abode pending-bootstrap handoff that preserves the existing preparedIncarnationPackage for later runtime bootstrap consumption; GM sends player to Mortal World)",
  "AscensionTrigger": "boolean (real Chaos Sea-only ascension transition; valid only if Enlightenment is 100% and playerChoice=Ascension; must not be combined with TriggerLifeEnd)",
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
- `game_state/meta/guardians.json` ← `UpdateGuardians`, `guardianPowerEvents`, `UpdateGuardianTradeInventoryReceipts`, and explicit canonical roots `guardians`, `activeGuardian`, `chaosSeaNavigation`, `playerGuardianFoundationHistory` when required by afterlife contract resolution
- `game_state/meta/guardian_abode_residents.json` ← `UpdateGuardianAbodeResidents`, `UpdateGuardianAbodeResidentRosterReceipts`, `UpdateGuardianAbodeResidentInteractionReceipts`, `UpdateGuardianAbodeResidentTransferReceipts`, `UpdateGuardianAbodeResidentHistoryLog`, `residentThoughtJournalUpdates`, `residentInteractionLogUpdates`
- `game_state/meta/guardian_projects.json` ← `startGuardianProjects`, `guardianProjectUpdates`, `completeGuardianProjects`
- `game_state/meta/guardian_project_journal.json` ← client-generated readable guardian project chronology
- `game_state/meta/abode_power_journal.json` ← client-generated readable guardian power chronology
- `game_state/meta/guardian_thought_journal.json` ← `guardianThoughtJournalUpdates`
- `game_state/meta/guardian_social_journal.json` ← `guardianSocialJournalUpdates`
- `game_state/meta/shining_abode_state.json` ← canonical Shining Abode state, Shining factions, halls, gates, radiance, Light Sparks, `shiningPoliticalActors`, Shining trade inventories, Shining receipts, prepared incarnation package. If a faction uses `leadership.headActorType = radiant_actor`, `leadership.headActorId` must resolve to an existing `shiningPoliticalActors[].actorId`.
- `game_state/meta/player_behavior.json` ← `playerBehaviorAssessment`, `historyManipulationCoefficient`
- `game_state/meta/character_chronicle.json` ← `characterChronicleUpdates`
- `game_state/meta/achievements.json` ← `achievementUnlocks`

#### **AFTERLIFE CONTROL / REQUEST FILES**
- `game_state/control/pending_abode_offering.json` ← client-authored Abode offering request; GM reads it as input only and always resolves through `guardianPowerEvents.reasonType = offering`. Only `offeringType = ink_feathers` is also `[INK_FEATHER_ACTION: ABODE_OFFERING]` and requires `output/ink_feather_action_result.json`; `soul_relic`, `archive_lore_fragment`, and `archive_secret_record` use plain `[ABODE_OFFERING]` and must not write an Ink Feather receipt.
- `game_state/control/pending_guardian_trade_request.json` ← client-authored Guardian trade inventory request; close with `UpdateGuardianTradeInventoryReceipts`.
- `game_state/control/pending_guardian_abode_residents_request.json` ← client-authored resident roster requests; close with `UpdateGuardianAbodeResidentRosterReceipts`.
- `game_state/control/pending_guardian_abode_resident_interactions.json` ← client-authored resident talk/history requests; close with `UpdateGuardianAbodeResidentInteractionReceipts` plus resident logs/history when accepted.
- `game_state/control/pending_guardian_abode_resident_transfers.json` ← client-authored Guardian Abode resident transfer requests; close with `UpdateGuardianAbodeResidentTransferReceipts`, matching history entries, and canonical resident source/target state.
- `game_state/control/pending_guardian_social_interactions.json` ← client-authored Guardian talk/lore social requests; close with `guardianSocialJournalUpdates` carrying matching `requestId`, `guardianId`, `interactionType`, and `status`.
- `game_state/control/pending_player_guardian_foundation.json` ← Chaos Sea-only player-founded Guardian ritual; close with `UpdateGuardians.create`, former-patron preservation, `activeGuardian`, `chaosSeaNavigation.currentAbodeId`, `soul_state.playerFoundedGuardianId`, `soul_state.playerGuardianFoundationStatus=founded`, and `playerGuardianFoundationHistory`.
- `game_state/control/system_guardian_attraction.json` ← client-owned deterministic Eternal Guardian attraction guard; resolve only to the requested preset Guardian, preserve `sourcePreset`, and do not substitute a similar Guardian or Mortal NPC.
- `game_state/control/pending_resident_companion_manifestation_request.json` ← MortalWorldProfile-only next-life companion manifestation requests. In `Chaos Sea` / `Shining Abode`, treat this file as stale/repair-only context and do not materialize mortal NPCs or encounters from it.
- `game_state/control/pending_archive_consultation_request.json` and `pending_archive_project_fuel_request.json` ← close with `archiveActionResolutions`.
- `game_state/control/pending_shining_abode_actions.json` ← client-authored Shining core actions; contains exactly one active `request`/`requests[0]`, not a GM-managed queue; close through canonical `shining_abode_state.json` mutation plus `shining_abode_state.coreActionReceipts[]`. Supported `actionType` values are `discover_native_faction`, `invest_in_faction`, `complete_project`, `support_project`, `unsupport_project`, `retire_project`, `open_gates`, `prepare_incarnation_package`, `pull_relic_gacha`, `forge_relic.reshape`, `forge_relic.retune_property`, `forge_relic.strengthen_band`, `forge_relic.stabilize_echo`, and `forge_relic.uplift_rarity`; use `OtherGuides/Afterlife_Contract_Matrix.md` plus `Examples/E_CLI_Afterlife_Turns.txt` example 14 for accepted receipt/state patterns.
- `game_state/meta/shining_abode_state.json.pendingNativeFactionDiscovery` ← legacy state-local Shining discovery contract. If non-null, close it as legacy `discover_native_faction`: materialize the native hall/faction/residents/projects, spend only `costFeathers` from Soul, preserve current Light Sparks because `costLightSparks` was already reserved, append `coreActionReceipts[]`, set `pendingNativeFactionDiscovery = null`, and do not create a duplicate `pending_shining_abode_actions.json`.
- `game_state/control/pending_shining_faction_foundings.json` ← close through `shining_abode_state.factionFoundingReceipts[]`.
- `game_state/control/pending_shining_faction_realignments.json` ← close through `shining_abode_state.factionRealignmentReceipts[]`.
- `game_state/control/pending_shining_faction_leadership_transitions.json` ← close through faction `leadershipReceipts[]` and leadership history.
- `game_state/control/pending_shining_trade_inventory_requests.json` ← close through faction `tradeInventory` plus `tradeInventoryReceipts[]`; supports `requests[]`, but `(factionId, tradeCycleId)` is the uniqueness key and duplicate contracts for the same faction/cycle are invalid.
- `game_state/control/afterlife_notifications.json` is client-owned; GM must not author inbox entries manually.

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
- `game_state/control/incarnation_trigger.json` ← `TriggerIncarnation` (ordinary Chaos Sea lifecycle control, or Shining Abode pending-bootstrap handoff that preserves `preparedIncarnationPackage` for later runtime bootstrap consumption; GM sends player to Mortal World: `{ "worldDescription": "...", "characterDescription": "...", "circumstances": "..." }`)
- `game_state/control/incarnation_world_setup.json` ← client-authored pending world setup chosen before incarnation; GM must read it when authoring `TriggerIncarnation` and the first Mortal World bootstrap
- `game_state/control/ascension.json` ← `AscensionTrigger`, `playerChoice` (real Chaos Sea-only ascension transition; only when Enlightenment is max and `playerChoice=Ascension`; never combine with `TriggerLifeEnd`)
- `game_state/control/validation_repair_request.json` ← client-written contract repair request when a GM turn is rejected after validation
- `game_state/control/validation_repair_ready.json` ← GM-written recheck signal after in-place fixes
- `game_state/control/terminal_protocol_failure_request.json` ← client-written notification that the terminal ready signal itself was malformed, mismatched, or ambiguously duplicated
- `game_state/control/pending_ink_actions.json` ← deferred pending Ink Feather resolutions such as `SEAL_IN_INK`
- `game_state/control/pending_turn_snapshot.json` and `game_state/control/pending_turn_snapshot/` are client-owned transient files and must not be treated as GM-authored game state

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
- In `Shining Abode pending-bootstrap handoff mode`, GM must not remove, clear, rename, or mutate `game_state/meta/shining_abode_state.json.preparedIncarnationPackage` in the accepted `TriggerIncarnation` turn. Preserve the package exactly as provided; the client runtime reads it after accepting the trigger, materializes the frozen blessing/world setup, and clears it only after successful Mortal World bootstrap.
- If the Shining handoff package is missing or malformed, do not "repair" it by deleting or nulling the package. Preserve the current state and use the normal validation repair/error path so the bootstrap contract can be fixed without losing the prepared package.
- `AscensionTrigger` is valid only in Chaos Sea, only with maximum Enlightenment and explicit `playerChoice=Ascension`, and must never be mixed with `TriggerLifeEnd`.

### Afterlife Realm Model
- Empty, missing, or `null` `currentRealm` is not `Chaos Sea`; it is an unresolved realm fault. GM must not infer realm from pending files, scheduler state, old logs, or narrative context.
- `Chaos Sea` and `Shining Abode` are both afterlife realms for validator/runtime purposes.
- Both afterlife realms use guardian/soul/meta systems and forbid mortal-world combat/NPC/faction/location mechanics.
- Both afterlife realms still have a living-world scheduler through `progressionControl`. This is afterlife-specific progression, not Mortal World `worldEventsLog` / `factionDataChanges` progression.
- `Shining Abode` is the ascended endgame free-roleplay zone above the Chaos Sea and still uses afterlife guardian/soul/meta systems instead of Mortal World systems.
- `Shining Abode pending-bootstrap handoff mode` is not an ordinary active Shining turn; process only lifecycle/bootstrap mutation and suppress ordinary Guardian/Shining scheduler progression for that handoff.

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
- `Relevant actors` must cover the actors changed through structured actor mutation surfaces such as `UpdateNPCs`, actor-specific NPC update arrays, and `UpdateGuardians`.
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
- Soul Relic Gacha processing
- Direct Chaos Sea gacha via `/gacha` remains a Chaos-Sea-specific exception and does not use current Guardian modifiers
- Abode navigation data
- Explicit afterlife Ink Feather whitelist actions may also legally produce guardian/meta/soul outputs.

### Forbidden In Afterlife Realms
- Combat, experience, leveling, stat gains/losses, regular inventory management, regular NPC mechanics,
  mortal quests, Mortal World faction/world progression, weather, time progression, mortal location tracking.

### Forbidden In Mortal World
- Guardian presence as active entities, Guardian reputation changes, Abode navigation, Gacha, afterlife-only Ink Feather spending.

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
- `Cultivate Enlightenment`
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
- If persisted NPC stock is missing, stale, malformed, or no longer matches the current world-time cycle, the client creates `pending_npc_trade_inventory_requests.json` and waits for a GM-materialized inventory.
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
- `check.primaryCharacteristic` must be one of these canonical lowercase ids:
  - `strength`, `dexterity`, `constitution`, `intelligence`, `wisdom`, `faith`, `attractiveness`, `trade`, `persuasion`, `perception`, `luck`, `speed`
- For `BranchChoice`, `check.config.choiceGrade` is required and must be exactly one of:
  - `success`
  - `partial`
  - `fail`
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
- `GUARDIAN_FAVOR` no longer requires a typed quest/buff outcome.
- The guaranteed mechanical minimum is:
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

5. **Ascension Transition (Enlightenment 100% + explicit player choice):**
   - Write `AscensionTrigger = true` and `playerChoice = "Ascension"` only when Enlightenment is at maximum.
   - Runtime performs a real transition from `Chaos Sea` into `Shining Abode`.
   - `Shining Abode` remains an afterlife hub with guardian/soul/meta systems, not a Mortal World.
   - Optional `New Game+` from `Shining Abode` resets Enlightenment and Ink Feathers while preserving Soul Relics and existing Guardians.
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
  // Direct Chaos Sea pull via /gacha should NOT use processGacha.
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

Guardian quest origin contract for lore-derived quests:
- `questOrigin = lore_research_hook` -> ordinary lore-research hook quest, consumes one `questHookToken`
- `questOrigin = lore_research_special_line` -> special quest line unlocked by `lore_research`
- `questOrigin = archive_consultation_hook` -> guaranteed extra guardian quest created from `archive consultation` with a `lore_fragment`
- Every object in `guardian.questManagement.availableQuests`, `activeQuests`, and `completedQuests` must carry a non-empty `questId`; do not use `title`, `questOrigin`, or `sourceProjectId` as surrogate identity.
- All three origins must carry `sourceProjectId` of the completed `lore_research` project that granted the effect.
- If a quest with `archive_consultation_hook` is completed, keep `questOrigin` and `sourceProjectId` in `completedQuests` so the guaranteed-origin audit trail survives completion.

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
- `game_state/control/next_life_scenario_core.json` ← client-owned Scenario Core manifest for the next life
- `game_state/world/guardian_corrections.json` ← applied guardian corrections for the current life
- `game_state/control/pending_abode_offering.json` ← client-authored pending offering request
- `game_state/control/archive_candidate_manifest.json` ← client-authored Life Evaluation manifest for codex-derived archive candidates
- `game_state/control/pending_guardian_trade_request.json` ← client-authored request to materialize an explicit guardian trade inventory for the current return-cycle
- `UpdateGuardianTradeInventoryReceipts` ← GM-authored receipt surface written into `game_state/meta/guardians.json`; each receipt closes one pending guardian trade inventory request with `status = ready`
- `game_state/control/pending_npc_trade_inventory_requests.json` ← client-authored request to materialize an explicit NPC trade inventory for the current world-time cycle
- `UpdateNpcTradeInventoryReceipts` ← GM-authored receipt surface written into `game_state/npcs/npc_core.json`; each receipt closes one pending NPC trade inventory request with `status = ready`
- `game_state/control/pending_guardian_abode_residents_request.json` ← client-authored requests to materialize explicit afterlife residents for one or more Guardian Abodes, stored as `requests[]`
- `game_state/control/pending_guardian_abode_resident_interactions.json` ← client-authored talk/history requests for afterlife residents, stored as `requests[]`
- `game_state/control/pending_resident_companion_manifestation_request.json` ← MortalWorldProfile-only next-life manifestation requests for equipped `companion_echo` Soul Relics and for equipped Soul Relics that carry embedded `soulImprint` / `npcSoulImprint`, stored as `requests[]`; do not process this file as an afterlife turn contract in `Chaos Sea` / `Shining Abode`. This file can originate from an afterlife resident reward or imprint, but closure happens later after Mortal bootstrap using source fields such as `sourceResidentId`, `sourceImprintId`, `sourceGuardianId`, `futureCompanionPrompt`, and `targetIncarnation`.
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
  - `[ABODE_RESIDENT_RELIC_GRANT]` means the player accepts a companion-echo reward from an existing afterlife resident. The accepted turn must add a `companion_echo` Soul Relic with a complete `companionSeed`, update that resident with `bondRewardState=granted` and `grantedRelicId=<new relicId>`, and add `residentInteractionLogUpdates`.
  - `[ABODE_RESIDENT_QUEST_REQUEST]` means the player accepts or helps a request from an existing afterlife resident. The accepted turn must use ordinary `UpdateSoulQuests` with `relatedAfterlifeResidentId`, may update the resident with `linkedSoulQuestId` / bond fields through `UpdateGuardianAbodeResidents`, and must add `residentInteractionLogUpdates`.
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
- `pending_guardian_social_interactions.json` and `pending_npc_social_interactions.json` are new client-authored closure surfaces for explicit guardian/NPC social requests.
- When those files are used, the accepted turn must close each request canonically through the corresponding event journal:
  - guardians -> `guardianSocialJournalUpdates`
  - NPCs -> `npcInteractionJournalUpdates`
- Journal closure entries must carry `requestId`, actor id, `interactionType`, `status = accepted | rejected | cancelled`, optional `responseMode`, plus the ordinary `title/summary/turn/timestamp`.
- Guardian social `responseMode` values are `talk_scene`, `lore_revealed`, `lore_refused`, `warning`, `refusal`, `trust_shift`, and `attitude_shift`.
- Resident interaction `responseMode` values are `talk_scene`, `history_revealed`, `history_refused`, `history_partial`, and `bond_shift_only`.
- Resident transfer `selectionMode` values are `competition_recommended`, `manual_override`, and `departure_only`; preserve `competitionScore`, `competitionLabel`, and `competitionReason` from pending transfer requests when they are present.
- Freeform guardian/NPC scenes that do not use these explicit request surfaces still remain advisory-memory only.
- The next-life companion manifestation bullets below apply to `MortalWorldProfile` bootstrap / early Mortal World turns only. In `Chaos Sea` or `Shining Abode`, `pending_resident_companion_manifestation_request.json` is stale/repair-only context and must not create mortal NPC output.
- A resident who can be carried into a future life should grant a Soul Relic with `relicType = companion_echo` and a complete `companionSeed`.
- If a resident has already granted a companion-carrying relic, preserve `grantedRelicId` in resident state so the reward is cross-linked to the actual Soul Relic.
- Independently of that, any Soul Relic that carries an embedded `soulImprint` / `npcSoulImprint` is also eligible for next-life companion manifestation through the same pending request layer.
- Companion manifestation eligibility is NOT capped by ordinary relic-slot overlap. If several equipped companion-carrying relics are present, the client creates manifestation requests for all of them, even if they share the same displayed slot.
- When a companion fully manifests into mortal NPC state, write `sourceCompanionRelicId` on that NPC. If applicable, also write `sourceAfterlifeResidentId` and/or `sourceSoulImprintId`, but `sourceCompanionRelicId` is the canonical unambiguous resolution key.
- Afterlife inbox may report successful companion manifestation through two distinct notification types:
  - `abode_resident_manifestation_ready` for resident-linked companion echoes
  - `companion_imprint_manifestation_ready` for relics that carry embedded `soulImprint` / `npcSoulImprint`
- Pending archive actions reserve the archive entry immediately. The entry is consumed only on `accepted`; on `rejected` or `cancelled` it returns to normal archive availability.
- `pendingShiningBlessingEffects` is runtime-created from `preparedIncarnationPackage.selectedCards` after Mortal bootstrap, not authored during afterlife. Supported families/statuses include `memorySelection.status=pending_pre_turn_one_selection`, `resourceGrant.status=applied_at_bootstrap`, `relicRefinementEntitlements.status=pending_relic_entitlement`, `pendingSocialEffects.status=pending_first_relation_commit`, `pendingRouteEffects.status=pending_early_route_seed`, `pendingLoreEffects.status=pending_lore_insertion`, `pendingSurvivalEffects.status=pending_first_ruinous_failure`, and `pendingDescentEffects.status=pending_resident_descent`; later Mortal turns consume them through ordinary Mortal state surfaces.
- `shining_abode_state.json.availability` uses accepted values such as `active` and `sealed_until_next_ascension`; pending-bootstrap is derived from non-null `preparedIncarnationPackage`, not from an `availability` value.
- `afterlife_notifications.json` is fully client-derived from existing canonical request/result surfaces and canonical guardian quest availability; the GM does not author inbox text manually. Important triggers include Guardian trade ready receipts, resident roster/interaction/transfer receipts, Guardian social entries, archive action receipts, Shining core/trade receipts, and consumed `pendingShiningBlessingEffects`.

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
