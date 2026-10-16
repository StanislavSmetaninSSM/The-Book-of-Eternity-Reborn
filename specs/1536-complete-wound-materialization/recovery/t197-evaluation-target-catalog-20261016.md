# T197 Q-L1c: LifeEvaluation target owner and leaf catalog

DESIGN ONLY — UNACCEPTED — PARTIAL — FINALSTOP. Historical source: `901435333b00ad7e92cef80429ce99ef010c7db9`. Configured Astra/High per assignment; backend telemetry UNKNOWN. No repository edits, runtime, builds, tests, activation or F2. All prior pairs remain immutable.

The source resolves 40 ordered normalizer stages and 14 surrounding owners, but does **not** close EP/E1 or the full target catalog. The concrete missing boundary is an original-bound complete candidate inventory plus typed owner receipts covering health, payload projection, canonical publication and still-live cleanup obligations. The current sequential implementation and returned `Accepted`/`Plan` do not issue that durable family receipt.

The normalizer itself can write `game_state/meta/afterlife_chronicles.json` and `game_state/meta/afterlife_global_flags.json`, both absent from N82. The genuine evaluation reward validator also requires an appended `lore/chaos_sea/player_chronicle.json`, outside N82 and outside the normalizer writers. Accepted health can additionally change system mods/config, notifications and QTE runtime. These are source-backed inclusion obligations; this report does not change their owners or fix their implementation.

The paired JSON carries fully expanded current/original/write/delete leaves, branch guards, no-change/ineligible witnesses, exact definition/callsite spans, static registry expansions, DTO routing data and historical Git blob/SHA256/byte pins. Input lists explicitly identify unresolved transitive helper closure; they are not represented as exhaustive read grants.

## Actual boundary and phase projection

`GameEngine.TurnLifecycle.cs:2747–2809` waits for the real evaluation result and calls the actual accepted validation/repair loop with target original and expected n+1. The loop performs health, upstream raw validation, conditional spiritual continuation, resource intake, canonical refresh, final validators, progression/repair cleanup and guardian command cleanup. The canonical helper captures actual item/spiritual/treatment/guardian authority before normalization and verifies after publication; errors may compensate. There is no ordinary evaluation `StateDistributor.DistributeAsync` call; that API’s response-field map is data for its own caller.

Proposed EP freezes the **completed candidate**, original identity, all owner-selected inputs, final after-images, absent/no-change witnesses and cleanup obligations. Output/health normalizations are candidate work before E1. Proposed E1 publishes exact frozen leaves once with the actual owners; after E1, recovery consumes durable receipts and cannot rerun output normalization, health time fallbacks, reward/dice calculation or source execution. E2P progression, E2S story/runtime, E3 candidate-manifest/return-guard, E4C health synchronization, E4I genuine incarnation and exact cleanup remain distinct. Existing code interleaves some of these; this is an unaccepted extraction requirement, not an accepted migration plan.

## Surrounding owners

### A01 — Genuine evaluation request/original/terminal admission

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:2747–2809`, `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:519–570`.

Accepted non-forced LifeEvaluation in Chaos Sea at evaluation n+1, source k_death; genuine signed original + matching terminal + actual current G. User-source tuple, DTO or M cannot make this target request. Missing/wrong original, stale G, wrong turn/realm or unaccepted payload -> blocked, no target receipt.

Phase: H2 prerequisite; EP binds it. Actual H2 lifecycle custody and durable complete-target issuer remain missing; this task does not resolve them.

### A02 — System-mod/settings accepted health

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:145–180`, `BookOfEternityClient/Services/SystemModService.cs:69–101`, `BookOfEternityClient/Services/SystemModService.Prepared.cs:30–62`, `BookOfEternityClient/Core/StateManager.cs:116–125`.

Writes (conditional): `config.json`, `game_state/core/system_mods.json`.
ordinaryEntry=false accepted/repair branch in every realm; mods roster is only top-level mods/*.* with extension .json/.txt/.md, TrustedLocalFileScope checked; enabled list from actual GameSettings. Semantic manifest equality excludes time field; unchanged enabled sequence -> no config save. Missing mods dir gives finite empty roster; bad path/read failure does not grant no-change.

Phase: Candidate health before E1; subsequent health must validate frozen agreement. PrepareManifestForGmAsync exists for detached manifest, but accepted route still mutates settings then independently saves config. Need typed combined health/config receipt plus live GameSettings application obligation. Freeze descriptor contents/metadata, enabled set and UTC once; never replay after E1.

### A03 — Afterlife notification health

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:165–179`, `BookOfEternityClient/Services/AfterlifeNotificationState.cs:158–207`.

Writes (conditional): `game_state/control/afterlife_notifications.json`.
Unconditional accepted-health call, no Chaos-only gate. Cleans invalid/duplicate entries, createdAt fallback and pressure markers according to actual helper. Absent -> no change; stable canonical result -> no change; record actual read bytes and frozen UTC.

Phase: Candidate health projection before E1. Detached EnsureHealthy projection/receipt required. It is not SyncFromCurrentState; lifecycle notification synchronization/delivery remain separate E4C obligations.

### A04 — QTE runtime health

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:177–179`, `BookOfEternityClient/Services/QteSceneService.cs:423–538`.

Writes (conditional): `game_state/control/qte_runtime.json`.
Deletes (conditional): `game_state/control/qte_runtime.json`.
Every realm; strict runtime shape. For inactive scene remove stale pending offer; remove invalid summary reminder. Absent -> no change; invalid strict JSON throws. Changed meaningful root -> write; changed meaningless root -> delete. No activation/new scene grant.

Phase: Candidate health before E1. Freeze exact health projection and witness. game_state/control/qte_runtime.json is canonical state, distinct from native .boe_runtime lifecycle custody; no QTE gameplay grant.

### A05 — Progression health versus accepted progression

Sources: `BookOfEternityClient/Services/ProgressionScheduleService.cs:132–205`, `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:290–315`.

Writes (conditional): `game_state/control/progression_schedule.json`.
EnsureInitialized sanitizes existing schedule or creates from readable realm/worldtime; malformed schedule rejects. Genuine HP already prepared schedule. Exact HP after-image + successful health policy may prove no change; absent schedule during retained HP is not authority to recreate it using new time.

Phase: Health candidate/EP guard; accepted advancement belongs E2P. Reuse owned progression preparation/projection, frozen clocks and actual HP receipt. Current success loop advances progression; cannot run it at E1 and again at E2P. Transitive sanitize predicates must retain Shining/realm data requirements.

### A06 — Return-guard/guardian attraction health exclusion

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:151–176`, `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1566–1583`.

Actual preserveControlFiles = terminal-ready + usable original suppresses both EnsureHealthy calls for genuine accepted target. No raw source tuple substitute. Authenticated predicate true is the ineligible witness for both health writers. If false, route is recovery/repair; not silently part of successful E1.

Phase: Pre-E1 guard. Do not broaden E1 to execute lifecycle/attraction repair on a failed retained target. Their real effects require their own receipts if later admitted.

### A07 — Accepted narrative/interface canonicalization

Sources: `BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:219–235`, `BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:705–717`, `BookOfEternityClient/Services/AcceptedTurnOutputProjector.cs:1–180`.

Writes (conditional): `output/interface_updates.json`, `output/narrative_response.json`.
Actual accepted validators call pure ProjectNarrative and ProjectInterface; interface uses UTC fallback only when RequiresInterfaceTimestampFallback. Full narrative/interface validators must still accept projected payloads. Changed=false retains exact original text; invalid/missing required output remains issue, not empty successful plan.

Phase: Authoring/candidate transforms before E1; exact final bytes included only under payload owner receipt. Pure projectors already exist: routine extraction, not new gameplay grant. Need exact input/output/payload validation receipt in complete EP. Never reexecute transforms/time fallback after accepted E1.

### A08 — LifeEvaluation-specific reward/read-only validators

Sources: `BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:329–398`, `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs:263–275`.

Authenticated source label recognized as LifeEvaluation; original/current soul delta and original/current lore player chronicle. Requires >=10 feathers, at least one new relic, meaningful appended life summary. Other source/realm/relic validators also remain required. No source label match bypasses only this check, not target admission. For genuine evaluation, absent/unchanged mandatory reward is rejection.

Phase: EP validation of GM candidate; E1 must own final accepted soul AND player_chronicle bytes. Player chronicle is raw accepted target data with no normalizer writer. Need validated raw-candidate inclusion receipt; N82 and FileMapping union do not supply it.

### A09 — Spiritual original/C2/C4 continuation

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:115–226`, `BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:400–495`, `BookOfEternityClient/Services/Validation/ValidationService.SpiritualOriginalC1ImageInventory.cs:50–108`, `BookOfEternityClient/Services/Validation/ValidationService.SpiritualC4Publication.cs:19–165`.

Writes (conditional): `game_state/control/pending_spiritual_wound_decisions.json`, `game_state/control/spiritual_wound_capture_checkpoint.json`, `game_state/control/validation_repair_ready.json`, `game_state/control/validation_repair_request.json`.
Signed original/current exchange intent, genuine fresh original intake and actual private checkpoint owner. ReadCurrent classified no_checkpoint/match/completed_unpublished; completed owner registers C4 and actual registry take required. Raw checkpoint/transport or identical JSON is insufficient. No changed exchange/start-only -> ordinary admission only; pending unresolved -> retry-held/rejected, never E1 acceptance. Source-only completion still requires actual C4 capability.

Phase: Conditional pre-EP continuation with owned intermediate writes; E1 only completed final reduction. C1 inventory is actual frozen PathInventory ∪ W40 with signed service-root originals/physical witnesses; not a generic namespace grant. C4 footprint currently N82 ∪ actual plan Touched/Consumed ∪ FixedOriginalOutputPaths. Need bounded evaluation-family adapter exposing exact registered candidate and service leaves/receipts; raw maps cannot restore capability or old G.

### A10 — Ordinary Mortal guaranteed treatment is ineligible

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.MortalGuaranteedTreatment.cs:15–55`.

Exact original.Realm must be mortal_world and current-G held guaranteed request must match original/frame. Genuine Chaos Sea original fails frame gate if treatment command attempted. No command -> null/no treatment. This is an ineligible witness, not a fabricated successful treatment receipt.

Phase: Pre-EP refusal/absence. No treatment authority extension proposed. Spiritual wound decisions are a separate actual C2/C4 family, not this Mortal treatment API.

### A11 — Accepted common mechanics/resources/effects/wounds

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs:93–350`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs:854–915`, `BookOfEternityClient/Services/AcceptedMechanicsPlanner.CommonCalculation.cs:344–474`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Wounds.cs:18–75`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs:9–145`.

Writes (conditional): `game_state/effects/effect_identity_index.json`, `game_state/resources/resource_definitions.json`, `game_state/resources/resource_history.json`, `game_state/resources/resource_owner_authority.json`, `game_state/resources/resource_state.json`.
Actual taken validated plan/current lease; actual C4 reference for live spiritual proof; genuine item/effect/owner before-image bindings; pending plan is not completion. NoPlan proves only no currently accepted mechanics authority. AwaitPending retains checkpoint, no complete E1. Wound no bundle/liveproof returns empty; receipt-only Mortal branch ineligible for Chaos.

Phase: Existing sequential publication within canonical refresh; proposed E1 final leaves only. Finite destination families expanded in mechanics_leaf_families below. Aggregate durable E1 receipt is absent. Preserve whole-root conflict detection and compose owner/effect/wound overlap before final bytes; typed owner transitions only NPC creation or guardian gacha.

### A12 — Guardian completed musings and quest command cleanup

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:1750–1762`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:1890–1933`, `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:2627–2690`.

Writes (conditional): `game_state/meta/guardians.json`.
CaptureGuardianMusingsPublication and completed validation are actual owners; PrepareQuestCleanupAsync then Remove(QuestProgressUpdatesProperty), write, cleanup.CompleteAsync under same G. Property absent -> no cleanup write; missing/malformed guardian with completed capability fails, not success.

Phase: Guardian candidate transforms N03/N38/N39 then E1; actual completion/cleanup obligation must be explicitly placed. Missing typed combined guardian projection/completion receipt across normalizer and cleanup. A completed capability and its pending cleanup cannot be represented by arbitrary guardian after-image; retain audit/journal boundaries.

### A13 — Repair transport cleanup and diagnostic audit

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:290–315`, `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:8249–8295`.

Writes (conditional): `game_state/control/gm_trajectory_ledger.jsonl`.
Deletes (conditional): `game_state/control/gm_validation_repair_artifact_stall_report.json`, `game_state/control/validation_repair_ready.json`, `game_state/control/validation_repair_request.json`.
Only actual successful validation; repaired case records trajectory, expected-G cleanup if repair cycle retained. No repairs -> no cleared trajectory append; exact absent controls are deletion no-change witnesses, never source-request admission.

Phase: Separate accepted cleanup obligation, not arbitrary normalizer output. Freeze exact cleanup inventory and actual diagnostic owner receipt; retain original until all consumers release. Do not replay repair or append same audit again after E1.

### A14 — StateDistributor mapping boundary

Sources: `BookOfEternityClient/IO/StateDistributor.cs:110–180`, `BookOfEternityClient/IO/StateDistributor.cs:240–329`, `BookOfEternityClient/Services/QteSceneService.cs:2290–2299`.

LifeEvaluation caller validates filesystem GM results; no ordinary evaluation DistributeAsync call found. Production distribution found in QTE scene path. Maps non-null recognized GameResponse fields, excludes output-only and client-consumed fields. Raw wound decisions/treatment authorings reject without typed accepted adapter. DTO recognized field is only routing data.

Phase: Outside ordinary evaluation caller; response_field_mapping_data retained only for gap comparison. Do not add a second response-distribution pass to evaluation. Need actual validated GM candidate capture from original-bound physical result, not response DTO union as authority.

## Ordered normalizer leaf catalog

Every row is called from `CanonicalStateNormalizer.cs:418–471` in this order. Fixed leaf destinations are not per-actor files: actor IDs, creation refs and journal IDs select JSON members. No blanket Chaos skip exists for these calls. The actual original-sensitive validators must permit each change. Absent/equal helper output is only a lawful no-change witness after the required source and validation checks; malformed required authority is never a no-change receipt. N15/N16 are skipped only when an actually taken spiritual C4 plan owns their whole root. Full inputs and witnesses are in the JSON.

| Row | Owner | Conditional write destinations | Definition |
|---|---|---|---|
| N01 | `NormalizeMortalLocationsAsync` | `game_state/control/mortal_bootstrap_scaffold.json`, `game_state/npcs/npc_core.json`, `game_state/world/current_location.json`, `game_state/world/location_identity_index.json`, `game_state/world/location_storage_contents.json`, `game_state/world/world_map.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs:21–178` |
| N02 | `NormalizeMortalItemsAsync` | `game_state/inventory/item_bonds.json`, `game_state/inventory/item_identity_index.json`, `game_state/inventory/item_removals.json`, `game_state/inventory/item_text_updates.json`, `game_state/inventory/items.json`, `game_state/inventory/recipes.json`, `game_state/misc/vehicles.json`, `game_state/npcs/item_journals.json`, `game_state/npcs/npc_core.json`, `game_state/npcs/npc_inventory.json`, `game_state/quests/quest_history.json`, `game_state/world/current_location.json`, `game_state/world/location_storage_contents.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:65–131` |
| N03 | `NormalizeGuardiansAsync` | `game_state/meta/abode_power_journal.json`, `game_state/meta/guardians.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:403–496` |
| N04 | `NormalizeGuardianAbodeResidentsAsync` | `game_state/meta/guardian_abode_residents.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:569–742` |
| N05 | `NormalizeShiningAbodeStateAsync` | `game_state/meta/shining_abode_state.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs:7–41` |
| N06 | `NormalizeGuardianProjectsAsync` | `game_state/meta/abode_power_journal.json`, `game_state/meta/guardian_project_journal.json`, `game_state/meta/guardian_projects.json`, `game_state/meta/guardians.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:823–910` |
| N07 | `NormalizeCharacterChronicleAsync` | `game_state/meta/character_chronicle.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:804–823` |
| N08 | `NormalizeAchievementsAsync` | `game_state/meta/achievements.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:1003–1051` |
| N09 | `NormalizeCodexAsync` | `lore/codex_entries.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:1051–1078` |
| N10 | `NormalizeQuestStateAsync` | `game_state/quests/regular_quests.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:26–45` |
| N11 | `NormalizeQuestStateAsync` | `game_state/quests/soul_quests.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:26–45` |
| N12 | `NormalizeQuestHistoryAsync` | `game_state/quests/quest_history.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:10–26` |
| N13 | `NormalizeRivalSoulArcsAsync` | `game_state/meta/guardian_project_journal.json`, `game_state/meta/guardian_projects.json`, `game_state/world/rival_soul_arcs.json`, `game_state/world/world_events.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:45–138` |
| N14 | `NormalizeSoulStateAsync` | `game_state/meta/soul_state.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:377–392` |
| N15 | `NormalizeAfterlifeSpiritualConflictStateAsync` | `game_state/meta/afterlife_spiritual_conflict_state.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs:7–24` |
| N16 | `NormalizeAfterlifeEntityProfilesAsync` | `game_state/meta/afterlife_entity_profiles.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs:7–25` |
| N17 | `NormalizeAfterlifeActiveThreatsAsync` | `game_state/meta/afterlife_active_threats.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeActiveThreats.cs:7–21` |
| N18 | `NormalizeChaosSeaGuardianPoliticsAsync` | `game_state/meta/chaos_sea_guardian_politics.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ChaosSeaGuardianPolitics.cs:7–18` |
| N19 | `NormalizeAfterlifeChroniclesAsync` | `game_state/meta/afterlife_chronicles.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeChronicles.cs:7–21` |
| N20 | `NormalizeAfterlifeGlobalFlagsAsync` | `game_state/meta/afterlife_global_flags.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeGlobalFlags.cs:7–21` |
| N21 | `NormalizeAfterlifeStoryOutlineAsync` | `game_state/meta/afterlife_story_outline.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeStoryOutline.cs:7–21` |
| N22 | `NormalizeSarefMainStoryStateAsync` | `game_state/meta/main_story_saref_state.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs:7–28` |
| N23 | `NormalizeFactionCoreChangesAsync` | `game_state/factions/faction_core.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:10–49` |
| N24 | `NormalizeFactionStructureAsync` | `game_state/factions/faction_structure.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:138–198` |
| N25 | `NormalizeFactionResourcesAsync` | `game_state/factions/faction_resources.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:198–238` |
| N26 | `NormalizeFactionProjectsAsync` | `game_state/factions/faction_projects.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:238–290` |
| N27 | `NormalizeFactionCustomAsync` | `game_state/factions/faction_custom.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:290–330` |
| N28 | `NormalizeFactionChroniclesAsync` | `game_state/factions/faction_chronicles.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:330–372` |
| N29 | `NormalizeFactionCoreAsync` | `game_state/factions/faction_core.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:102–138` |
| N30 | `NormalizeNpcCoreChangesAsync` | `game_state/npcs/npc_core.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:8–61` |
| N31 | `NormalizeNpcTradeCoreAsync` | `game_state/npcs/npc_core.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:186–222` |
| N32 | `NormalizeNpcJournalsAsync` | `game_state/npcs/npc_journals.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:64–87` |
| N33 | `NormalizeNpcInteractionJournalAsync` | `game_state/npcs/npc_interaction_journal.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:74–79` |
| N34 | `NormalizeInventoryItemsAsync` | `game_state/inventory/items.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:10–26` |
| N35 | `NormalizeInventoryItemBondsAsync` | `game_state/inventory/item_bonds.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:26–42` |
| N36 | `NormalizeInventoryItemTextsAsync` | `game_state/inventory/item_text_updates.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:42–58` |
| N37 | `NormalizeItemJournalsAsync` | `game_state/npcs/item_journals.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:58–74` |
| N38 | `NormalizeGuardianThoughtJournalAsync` | `game_state/meta/guardian_thought_journal.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:79–84` |
| N39 | `NormalizeGuardianSocialJournalAsync` | `game_state/meta/guardian_social_journal.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:84–89` |
| N40 | `NormalizePlayerSkillStateAsync` | `game_state/player/skills_active.json`, `game_state/player/skills_passive.json` | `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Skills.cs:7–60` |

The set above has 50 distinct candidate-write destinations. It is a destination union for the enumerated stages, **not** the whole accepted target. N02’s IP13 describes the actual sealed projection family; ordinary fallback transitive reads and transfers remain explicitly open. Whole-root overlaps (NPC/location/items; guardians/projects/rivals; soul/profiles; effects/wounds) must compose against ordered projected state and retain the actual receipt owners. Current `WriteIfChangedAsync` uses serialized node equality, not exact byte equality.

## Structural path rules and authority roles

- Original logical-to-physical mappings come only from the actual authenticated target original; source tuple, copied bytes, a hash or rollback image is not original authority. Present/absent witnesses and safe exact spellings survive separately.
- Resource quartet, six effect carriers plus identity, five wound carriers plus identity/history, NPC/guardian typed-transition roots and spiritual controls are finite fixed families listed in JSON. Real plan dictionaries select their admitted subset; arbitrary dictionary keys are not a new path grant. `ResourcePendingResolutionState.PendingPath` is exactly `game_state/control/pending_effect_resolutions.json`—one root, not two pending files.
- The actual C1 registered inventory is frozen draft PathInventory ∪ W40. Draft candidate syntax permits selected game_state/lore/world_profiles/output leaves and excludes specified service/rollback/control roots; the matched owner’s exact inventory and signed service originals are required. This existing broad selection rule does not close a bounded evaluation-family archive inventory.
- Mods are the one filesystem roster explicitly needed by health here: trusted top-level `mods/*.*` with `.json`, `.txt` or `.md`, fixed settings/manifest outputs, frozen contents and descriptor metadata. It grants no recursive mod or namespace export.
- Native/runtime G state is not a normalizer output or archive capability. `.boe_runtime` custody, source stop/release and current-G admission stay DEP-C/L. Canonical `qte_runtime.json` is a distinct game-state health root.
- DTO union/FieldToFile is data only. No new response distribution pass, incarnation acceptance, Explorer Archive/Skip, source replay or old-G rebinding is admitted.

## Remaining interfaces and questions

**Q-E1 — Complete validated raw target inventory (OPEN).** A bounded CaptureLifeEvaluationCandidate operation on the genuine target original/request/terminal owner, producing exact original/current/accepted leaf sets with present/absent witnesses and per-domain validator ownership. Source snapshot enumerates broad game_state/lore physical roots for rollback and original proof; that does not authorize every candidate path. Mandatory raw player_chronicle is outside normalizer writes/N82. FileMapping maps DTO routing only. Which exact unchanged and changed raw roots are accepted must be frozen by actual domain owners, including unknown/unclaimed-root rejection. Routine owned extraction where existing domain acceptance covers the leaf; unresolved domain membership requires design review, not a grant from missing helper. No generic namespace admission.

**Q-E2 — Health and output candidate projection boundary (OPEN).** PrepareEvaluationHealthInputs/ProjectEvaluationHealth and accepted-payload capture must freeze current bytes, top-level trusted mods roster/metadata, actual settings, finite UTC inputs and exact outcomes. Existing PrepareManifestForGmAsync/AcceptedTurnOutputProjector can be specialized. Model live settings/cache update and lifecycle/notification side effects as actual obligations; exclude post-E1 rerun. Genuine HP progression agreement must prevent accidental initialization/re-timing. Routine D11 owner specialization/pure extraction when policy unchanged; no new lifecycle/gameplay grant. Receipt composition still unaccepted.

**Q-E3 — Ordered normalizer read/write overlap and compensation coverage (OPEN).** PrepareEvaluationCanonicalProjection must preserve exact 40-stage order and pre-read guardian inputs, resolving later reads against earlier projected roots. One final path producer/cohort must reconcile guardian gacha/quests/journals, projects/rivals/events, soul/profiles, item/NPC/location, effect/wound carriers and owner authority. Read-set and rollback coverage must include afterlife_chronicles and afterlife_global_flags; N82 omission is source fact, not repaired here. Preserve raw/original/rollback distinctions and serializer/frozen-clock semantics. Routine D11 extraction for same owned behavior. No blanket root merge or new owner assignment accepted.

**Q-E4 — Guardian completion and separate cleanup obligations (OPEN).** Expose genuine guardian musings completed-validation and exact quest cleanup calculation/completion. Need separate receipts for repair/audit cleanup, progression E2P, runtime/story E2S, candidate manifest/returnguard E3, health synchronization E4C and exact original/native/main cleanup. File after-images cannot stand in for these capabilities/side effects. Routine owner specialization where existing owner can issue; lifecycle source-release and generation custody remain DEP-C/L, not routine new authority.

**Q-E5 — Transitive mechanics/item/spiritual closed inventories (OPEN).** Expose from actual registered owners exact finite InputInventory/CompanionInventory/ConsumedInventory with typed family tags, original/physical image roles and explicit ineligible/no-change receipts. This report resolves fixed carriers, resource quartet, transition roots and spiritual controls, but does not claim every producer of plan dictionaries or ordinary item fallback helper is transitively closed. Spiritual PathInventory uses actual frozen declared/current draft scope including world_profiles; it cannot be replaced with arbitrary snapshot of that namespace. No new independent replay or lost-current-G restoration. Existing actual C1/C4/item/resource/effect authorities support specialization; absence of an extraction helper is not a principled grant. Supporting unowned new path/type or resurrection across G is not authorized.

**Q-E6 — Durable EP/E1 family receipt and crash ownership (OPEN).** Current ordinary publisher writes individual roots then validates/compensates; returned Accepted/Plan is not a durable complete evaluation acceptance. Proposed PrepareEvaluationTargetEnvelope/PublishEvaluationTarget/ReadEvaluationTargetReceipt must be tied to all actual owners and one reviewed native cohort, exact sameG target identity and M ack. Persist attempted/committed/uncertain state and rollback/readback ownership; post-E1 recovery reads receipt, never reruns authoring, rewards, dice or source. Existing native APIs alone do not establish this family contract. Unaccepted family composition proposal; no new gameplay/lifecycle power assumed. Any needed new lifecycle issuer remains separately G-C/DEP-L discussion; no missing helper magically grants it.

Missing pure extraction helpers alone are routine D11 specialization when the actual existing owner and policy are retained. No new principled grant is sought for fixed-root projection. The unresolved durable family composition is still subject to review; an absent lifecycle/current-G issuer cannot be replaced by a nominal helper. G-C, DEP-I/L, fulltarget, H2/H3 and new-QTE blockers stay open.

## Finite future checks — all UNRUN

- T01: Genuine non-forced Chaos n+1/k_death target: exact original/request/terminal/G; reject source-tuple-only, wrong request, stale original, crossG.
- T02: Enumerate all 40 normalizer callsites and exact paths; chronicle/global flags coverage present; N82 never used as full acceptance set.
- T03: Required soul reward and appended lore/chaos_sea/player_chronicle accepted once; absent/unchanged rewards reject; no extra Mortal tick/dice/source replay.
- T04: Health mods unchanged/changed enabled list, absent dir, rejected links/aliases, changed content/metadata, frozen UTC; live config/settings receipt agrees.
- T05: Notification absent/stable/repairable and QTE absent/valid/stale offer/empty-delete/malformed; exact health plans; no activation or fake no-op.
- T06: Output normalizers changed/unchanged/invalid payload and interface timestamp fallback; freeze before E1, never rerun after accepted receipt.
- T07: Actual HP schedule valid/missing/malformed/changed; accepted progression runs once only at E2P, no new clock on restart.
- T08: Guardian pre-read/project/rival/gacha/quest/journal overlaps: unique final roots, required source absent vs malformed, genuine musings/cleanup receipts.
- T09: Spiritual no-intent/start-only/source-only/active exchange, no-offer/decline/accepted/pending/repaired; actual C1/C2/C4; retained unresolved blocks E1.
- T10: Mortal treatment/QTE/joint capability attempted on Chaos target rejects; no grafting source Mortal receipt, M flags or raw DTO.
- T11: Mechanics NoPlan/validated/pending, fixed resource/effect/wound families, same-root conflict, null pending deletion; unregistered dictionary keys reject.
- T12: Raw accepted target root outside N82, unknown path, wrong casing, alias, declared absent, extra candidate path; actual domain owner must admit or reject.
- T13: Crash before EP, after EP, during E1, uncertain E1, E1-before-M-ack, later cleanup; exactly one native result/readback; no replay from matching JSON.
- T14: Restart and distinct root/newG cannot restore target capability from copied metadata; DEP-C/L receipts still required; source original and native debt retained.
- T15: E2P/E2S/E3/E4C/E4I boundaries unchanged; E3 excludes Archive/Skip player actions and soul writes; cleanup does not delete still-owned original.
- T16: Console/browser same semantic family; no user-visible technical flow; preserve D4/guided/visual/D8 and no F2/new QTE activation.

## Historical evidence and stop

135 source files have exact commit/Git blob/SHA256/byte pins in the paired JSON; SHA-256 covers raw Git blob bytes, not current working-tree contents. The 16 prior declared registries were statically re-expanded at this commit (including N82/W40/IP13); no runtime or test result is inferred. The normalizer stage count and finite literal/path expansions are static report derivations only. Current HEAD observed for context: `2622abcf9163153ea4f3a78d3a0e136adc38658f`; every claim above remains pinned to `901435333b00ad7e92cef80429ce99ef010c7db9`.

Immutable prior: `/tmp/t197-depl-leaf-catalog-corrected-20261016.md`, SHA256 `42aa4c29c028feb0212e3fbe62b91a692efe1b14a72fba92d291e10aa153861c`, 33884 bytes.
Immutable prior: `/tmp/t197-depl-leaf-catalog-corrected-20261016.json`, SHA256 `0b754c394dd64371a1d36045139162ab74eab3ddb029e09b18c70156206d4f60`, 234797 bytes.

No source lifecycle custody, target acceptance, archive schema, global gameplay grant, F2 or activation is declared complete. Original/validation/rollback roles, source Mortal n/action/k_death, evaluation n+1, genuine bootstrap n+2/k+1 when applicable, and D4/guided/visual/D8 remain unchanged. FINALSTOP.
