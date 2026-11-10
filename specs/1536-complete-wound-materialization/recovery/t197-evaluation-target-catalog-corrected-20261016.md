# T197 Q-L1c: corrected LifeEvaluation target owner and leaf catalog

DESIGN ONLY — UNACCEPTED — PARTIAL — FINALSTOP. Historical source: `901435333b00ad7e92cef80429ce99ef010c7db9`. Configured Astra/High per assignment; backend telemetry UNKNOWN. No repository edits, runtime, builds, tests, activation or F2. All prior pairs remain immutable.

The source resolves 40 ordered normalizer stages and 15 surrounding owners, but does **not** close EP/E1 or the full target catalog. The concrete missing boundary is an original-bound complete candidate inventory plus typed owner receipts covering health, payload projection, canonical publication and still-live cleanup obligations. The current sequential implementation and returned `Accepted`/`Plan` do not issue that durable family receipt.

The normalizer itself can write `game_state/meta/afterlife_chronicles.json` and `game_state/meta/afterlife_global_flags.json`, both absent from N82. The genuine evaluation reward validator also requires an appended `lore/chaos_sea/player_chronicle.json`, outside N82 and outside the normalizer writers. Accepted health can additionally change system mods/config, notifications and QTE runtime. These are source-backed inclusion obligations; this report does not change their owners or fix their implementation.

The paired JSON carries fully expanded current/original/write/delete leaves, branch guards, no-change/ineligible witnesses, exact definition/callsite spans, static registry expansions, DTO routing data and historical Git blob/SHA256/byte pins. Input lists explicitly identify unresolved transitive helper closure; they are not represented as exhaustive read grants.

## Correction provenance — ET-R1 and ET-R2

This new pair supersedes only the initial catalog defects and the associated source predicates/obligations. The initial pair remains immutable. All 40 local no-change predicates were checked against the pinned owner code. A15 records the previously omitted early/repeated profile-mirror writer. The original 135 historical pins, 16 registries, 73 constant records, 181 mapping data rows and 50 normalizer destination union are unchanged; two supplemental helper pins support the corrected predicates. No code or policy was changed.

Initial independent review is FINALSTOP / CHANGES_REQUIRED for ET-R1 and ET-R2 only; both are addressed here as author corrections. Affected independent rereview remains pending. This is not an independent PASS, complete target closure or new grant. The common normalizer span now starts at :416; the QTE-health citation now identifies :174.

Original `/tmp/t197-evaluation-target-catalog-20261016.md`: SHA256 `859b2f2e3088cf241a5a0577d5b4428ee57b8fb2349e7cb9740b796370289b15`, 37866 bytes.
Original `/tmp/t197-evaluation-target-catalog-20261016.json`: SHA256 `433594bd787f343fb532898893209bc39c0b9f7c42e05ed0fd3e24216bfc16b5`, 259726 bytes.

Initial review `/tmp/t197-evaluation-target-independent-review-20261016.md`: SHA256 `387f12a8e59cc752ba96632b93325df91ca5547543dc379b721574a742879b3d`, 12766 bytes.
Initial review `/tmp/t197-evaluation-target-independent-review-20261016.json`: SHA256 `679da2a38700bf66e63915090c1743acb2fd5995ce206ee58a8eea26997ae513`, 85678 bytes.

Exact old-to-new MD/JSON unified diff: `/tmp/t197-evaluation-target-catalog-corrected-20261016.diff`. Structured per-row old/new witness and input-list changes are retained in JSON correction_provenance.

The initial reader descriptions also needed a precise distinction: `ReadNodeAsync` can return null for blank/missing text, JSON null or caught parse failure; `ReadObjectAsync` further collapses nonobjects. Those return values do not prove authenticated absence. `WriteIfChangedAsync` compares ordinal equality of reserialized nodes, while validated IP13 uses `JsonNode.DeepEquals`. Neither proves whole-family no-change. Source: `CanonicalStateNormalizer.SharedAndSoulHelpers.cs:556–576,711–725`; `CanonicalStateNormalizer.PrivateImplementation.cs:151–202`.

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

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:174`, `BookOfEternityClient/Services/QteSceneService.cs:423–538`.

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

### A15 — Health/runtime refresh player-soul profile mirror and native recovery

**Actual order:** EnsureClientOwnedSystemFilesHealthyCoreAsync calls RefreshGameStateAsync after orphan cleanup and BEFORE preserveControlFiles calculation and A02. RefreshRuntimeStateAsync -> RefreshGameStateAsync also occurs after successful canonical refresh and again later in accepted validation. N16 is a different later normalizer, not this writer.

Sources: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:145–153`, `BookOfEternityClient/Core/StateManager.cs:134–174`, `BookOfEternityClient/Core/StateManager.cs:619–623`, `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs:352–408`, `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs:425–473`, `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:183–202`, `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:252–283`, `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:1176–1179`.

Current inputs: `game_state/meta/afterlife_entity_profiles.json`, `game_state/meta/soul_state.json`, `game_state/meta/shining_abode_state.json`. Conditional output: the whole `game_state/meta/afterlife_entity_profiles.json` root only. No deletes.

No realm early return in mirror owner. Current profile and soul object availability is required; current Shining may be null. Actual UsesTrustedLocalWriter decides native local cohort versus original receipt-returning dispatcher. Actual profile player-soul selector determines projected members; raw realm/M/DTO is not authority.

ResolveBackupPublicationRecovery runs before mirror reads. Local mirror returns after resolving recovery when DecodeMirrorObject(current profile) or soul object is null, or JsonNode.DeepEquals(currentRoot, projectedRoot). Equal projection preserves exact original bytes/encoding; null-decoder result includes absent/blank/malformed/nonobject, so it is not authenticated absence or accepted validity. Original-dispatch helper also returns null for no usable profile/soul or DeepEquals. Neither no-op proves native cleanup debt resolved without the real recovery call.

ReadFileSnapshotAsync on profiles retains exact before bytes; soul and Shining use leased byte readers; projection clones current root. On changed projection, serialize SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, EncodeUtf8WithPreamble, PublishLocalFilesAsync with one CanonicalLocalFileChange(profilePath, exact before Content, desired bytes). Uncertain disposition throws CoordinatedStatePublicationUncertainException; RequireCommittedLocalPublication and ResolveBackupPublicationRecovery follow. No copied capability/native file admission.

When !UsesTrustedLocalWriter, call ApplyPlayerSoulProfileClientAuthorityAsync; retain its actual WriteFileAtomicWithPublicationAsync/optional publisher route and then ResolveBackupPublicationRecovery. No synthetic equality receipt replaces original transaction semantics.

Public StateManager.RefreshGameStateAsync catches/logs repair exceptions EXCEPT CoordinatedStatePublicationUncertainException and SessionReplacedException, then runs RefreshGameStateCoreAsync; those two propagate. The lease-taking internal overload does not swallow repair exceptions. Live aggregation/runtime cache update is a separate obligation; its full read closure is not enumerated here. A caught helper failure is not an accepted E1 no-change receipt.

Pre-EP health candidate writer; repeated post-canonical/pre-acceptance refresh is another actual writer. Proposed post-E1 refresh must validate agreed final projection and discharge real native debt/live aggregation obligation, without reauthoring accepted E1.

Extend Q-E2/Q-E3/Q-E6 to compose mirror profiles with N16 and spiritual/common profile owners and bind exact source roles/current bytes to actual target original. Expose real projection/publication/recovery/no-change/live-aggregation receipts. Missing full aggregation input closure and complete evaluation receipt remain OPEN; no new lifecycle grant.

A02 clarification from the completed independent review: config/mods/manifest logical paths resolve under `game_session`. No additional `game_settings.json` output is added to accepted health. Policy-preserving extraction may reuse existing preparation helpers; a combined cross-owner atomic receipt remains UNACCEPTED.

## Ordered normalizer leaf catalog

Every row is called from `CanonicalStateNormalizer.cs:416–471` in this order. Fixed leaf destinations are not per-actor files: actor IDs, creation refs and journal IDs select JSON members. No blanket Chaos skip exists for these calls. The actual original-sensitive validators must permit each change. There is no blanket absent/nonobject early return. Each corrected local predicate below controls only that method or write call; original/core-derived state, supported arrays and other writers can still require publication. Malformed/unusable data and method-level no-op remain separate from full accepted validation. N15/N16 are skipped only when an actually taken spiritual C4 plan owns their whole root. Full inputs and witnesses are in the JSON.

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

## Corrected local predicates — all 40 rows

These predicates describe actual source control flow, not accepted validity or whole-family no-change. The JSON retains literal source excerpts, derived inputs, separate malformed/realm/plan guards and overlapping normalizer row IDs for every row. No normalizer-wide Chaos skip or generic NoPlan no-op is inferred. All other fixed outputs and open interfaces remain as in the initial report.

**N01 — NormalizeMortalLocationsAsync.** After current map/location object reads, !hasCurrentCommand && !hasMapCommand, where command presence means currentLocationData/worldMapUpdates is JsonObject. Taken spiritual receipt freshness is checked before this return.

Write guard: No generic equality check: write fixed plan roots only when TouchedPaths includes them; current-location requires nonnull final projection; scaffold also requires final scaffold and original scaffold. NPC rewrite writes only when governed NPC rewrite list is nonempty.

Derived inputs: Original map/index mandatory after command presence; optional original current/storage/scaffold, companions, raw NPC/faction roots and positive request turn. Returning null for no location command does not prevent item/common mechanics or other normalizers writing shared roots.

Guards: ReadMortalLocationObjectRootAsync rejects nonblank malformed/nonobject JSON; missing/blank is null. Missing required backup or stale scaffold/plan rejects. Actual spiritual completed plan required when a spiritual receipt accompanies commands.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs:21–178`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs:180–237`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs:281–316`.

**N02 — NormalizeMortalItemsAsync.** Validated mode returns only AFTER walking all IP13 leaves; no absent-root blanket return. Other modes call NormalizeOrdinaryMortalItemsAsync.

Write guard: Validated mode: JsonNode.DeepEquals(currentRoots[path], projectedRoots[path]) skips that path; changed null after-image throws. Ordinary mode has change booleans for carriers/companions but writes normalized item identity index unconditionally after successful normalization. Transfer helper separately uses DeepEquals per IP13.

Derived inputs: Frozen actual validated snapshot supplies current/final/item-phase/backup projection and route/identity proof. Ordinary mode reads transfers and route/catalog/current roots, validates or allocates identities under its actual mode. Ordinary transfer helper runs first except ClientOwnedBootstrap; zero accepted transfers returns only from transfer helper, not entire N02. It can publish IP13 changes before subsequent identity write. Shared NPC/location/items roots remain writable by other stages.

Guards: Invalid final/item phase or recomputation rejects; projection errors reject; null mode rejects. ClientOwnedBootstrap is a separate caller, never evaluation authority. No accepted no-change can be inferred from empty carriers.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:65–132`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:132–172`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs:355–407`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItemTransfers.cs:16–74`.

**N03 — NormalizeGuardiansAsync.** currentNode == null only; JsonArray is explicitly copied into guardians, and other nonnull node kinds still reach original-derived normalization.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Previous guardian object is cloned; object commands/receipts, array guardians, current request turn and guardian canonicalization determine result. Only powerJournalEntries.Count > 0 calls AppendGuardianPowerJournalEntriesAsync. Equal guardians does not skip that append. That helper may RepairAndAppend, but N03 does NOT independently call journal repair when entries are empty. Later N06/common guardian owner/quest cleanup can write same root.

Guards: Actual plan selects retained gacha commands; absent UpdateGuardians is not a no-change predicate. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:403–494`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:910–935`.

**N04 — NormalizeGuardianAbodeResidentsAsync.** currentNode == null only; arrays/nonobject nonnull nodes are not an early-return condition.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original residents, current collected entries/receipts; current+original guardians, current Shining, current+original soul quests; canonical arrays/shape and conditional drift. Only residents is published here; detached Shining projection affects resident fields but is not a Shining file write. Other normalizers can subsequently write shared contextual roots.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:569–740`.

**N05 — NormalizeShiningAbodeStateAsync.** currentNode == null OR currentNode is not JsonObject (the latter occurs after reading original/default root).

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Current/original Shining plus resident and guardian objects; political update and NormalizeStateRoot run before equality. Detached resident normalization is not a resident write here; N04 may write residents independently.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs:7–39`.

**N06 — NormalizeGuardianProjectsAsync.** currentObj == null && previous == null && activeProjects.Count == 0 && completedProjects.Count == 0 && temporaryModifiers.Count == 0, after initial collection and token/gacha-use consumption logic.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call. applies only to tracker. Guardians write uses guardiansChanged && guardiansRoot != null; journal append/repair has its own content-null predicate.

Derived inputs: Frozen current/original tracker and original guardians, current guardians, soul-derived incarnation/realm, request turn; original projects can produce writes even with absent current tracker. After tracker equality, journal entries may append; pending power events may change guardians; if powerJournalEntries.Count == 0, RepairGuardianPowerJournalAsync still runs. PreserveDuringQte only reads journal; ordinary repair writes when BuildRepairedJournalAsync returns nonnull.

Guards: Pre-read helpers require readable original/current roots when their predicates demand them. No blanket absence no-op. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:823–960`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs:619–658`.

**N07 — NormalizeCharacterChronicleAsync.** currentNode == null only; arrays/nonobject nonnull roots continue.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Clone original chronicle (or empty object), EnsureArray(entries), collect original/current entries, remove characterChronicleUpdates. No secondary file write in this method; no-change of this root does not satisfy mandatory lore/chaos_sea/player_chronicle reward.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs:804–821`.

**N08 — NormalizeAchievementsAsync.** current = await ReadObjectAsync(path); current == null.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Merge original/current objects; rebuild unlocked/tracked/stats and remove achievementUnlocks. No other writer in this method; lack of command alone does not imply equality.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:1003–1048`.

**N09 — NormalizeCodexAsync.** current = await ReadObjectAsync(path); current == null.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Merge original/current objects; rebuild entries/count/category stats and remove loreCodexUpdates. No other writer here; original-only entries can change an existing current object.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:1051–1073`.

**N10 — NormalizeQuestStateAsync.** currentNode == null only; nonnull arrays/nonobject roots continue.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current quest collectors with exact caller property UpdateQuests; clone original, EnsureArray(quests), upsert then remove wrapper. No secondary write here. An array can produce a canonical object and therefore requires a write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:26–42`.

**N11 — NormalizeQuestStateAsync.** currentNode == null only; nonnull arrays/nonobject roots continue.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current quest collectors with exact caller property UpdateSoulQuests; clone original, EnsureArray(quests), upsert then remove wrapper. No secondary write here. An array can produce a canonical object and therefore requires a write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:26–42`.

**N12 — NormalizeQuestHistoryAsync.** currentNode is not JsonObject; then defensive result is not JsonObject return. Given object current, actual QuestHistory helper constructs a JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current quest-history entries, rewards and chains; wrapper removal and canonical shape. No other file write here; transformation can be nonempty without questLog command.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:10–24`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:9–46`.

**N13 — NormalizeRivalSoulArcsAsync.** ReadCurrentAuthorityNodeAsync(...) returns null; required is CanonicalFileExists(path). No nonobject early return after a nonnull node.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call. is followed by independent unconditional writes when clue consumption succeeds.

Derived inputs: Original/current rivals, tracker, original/current world events, request turn and conditional readable soul incarnation determine clue consumption. tracker != null && requiresCurrentIncarnation && currentIncarnation > 0 && Consume... returns true writes tracker and rivals even if initial W equality held; world events write additionally needs worldEventsChanged && currentWorldEvents != null; nonempty projectJournalEntries appends journal.

Guards: Present blank/malformed required root throws in ReadCurrentAuthorityNodeAsync; required soul/world-event predicates have their own guards. Parsed JSON null is still a distinct case needing validation, not accepted physical absence. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs:45–136`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs:675–709`.

**N14 — NormalizeSoulStateAsync.** ReadObjectAsync current == null; or BuildNormalizedSoulStateRootAsync result == null (normally excluded by supplied nonnull current).

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current soul, request turn and lifecycle authorization from life_transitions plus validated original context; strict root projection. No secondary write here; original reward delta and mandatory player chronicle validation remain separate.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:316–333`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:377–418`.

**N15 — NormalizeAfterlifeSpiritualConflictStateAsync.** Caller skips method only if actual spiritualPublicationReceipt != null AND SpiritualPlanOwnsRoot(plan, conflictPath). If called: currentNode is not JsonObject OR ResponseField is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Projection baseline from current canonical root (wrapper removed), else original/default; ApplyUpdate. Caller skip assigns this root to actual taken spiritual common publication; it is NOT root-level no-change.

Guards: Only actual C4 root ownership supplies handled-path witness; malformed/absent command shape is method skip, not target acceptance. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs:438–443`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeSpiritualConflict.cs:7–58`.

**N16 — NormalizeAfterlifeEntityProfilesAsync.** Caller skips only actual spiritual plan ownership of profiles root. If called: currentNode == null or currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current profiles, matching current-turn progression report and current soul/Shining client projection. C4 handled path can still publish profiles; health mirror refresh can also publish profiles (A15). Equal N16 alone is not a whole-root no-change witness.

Guards: Actual C4 root ownership only, never DTO ownership; report tuple validation remains required. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs:441–444`, `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs:7–79`.

**N17 — NormalizeAfterlifeActiveThreatsAsync.** currentNode == null or currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current object passed to exact ProjectCanonicalRoot; no absent-current derivation in this wrapper. No secondary write in this method; other stages/cohort owners are not suppressed by this local return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeActiveThreats.cs:7–19`.

**N18 — NormalizeChaosSeaGuardianPoliticsAsync.** currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current object passed to exact ProjectCanonicalRoot; no absent-current derivation in this wrapper. No secondary write in this method; other stages/cohort owners are not suppressed by this local return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ChaosSeaGuardianPolitics.cs:7–16`.

**N19 — NormalizeAfterlifeChroniclesAsync.** currentNode == null or currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current object passed to exact ProjectCanonicalRoot; no absent-current derivation in this wrapper. No secondary write in this method; other stages/cohort owners are not suppressed by this local return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeChronicles.cs:7–19`.

**N20 — NormalizeAfterlifeGlobalFlagsAsync.** currentNode == null or currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current object passed to exact ProjectCanonicalRoot; no absent-current derivation in this wrapper. No secondary write in this method; other stages/cohort owners are not suppressed by this local return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeGlobalFlags.cs:7–19`.

**N21 — NormalizeAfterlifeStoryOutlineAsync.** currentNode == null or currentNode is not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current object passed to exact ProjectCanonicalRoot; no absent-current derivation in this wrapper. No secondary write in this method; other stages/cohort owners are not suppressed by this local return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeStoryOutline.cs:7–19`.

**N22 — NormalizeSarefMainStoryStateAsync.** currentNode is not JsonObject; otherwise StateResponseField object branch invokes W then returns (handled write, not no-change). If no state-wrapper object and ResponseField is not JsonObject, returns without W.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call. independently in direct canonical-root and update branches.

Derived inputs: StateResponseField object directly cloned; update branch resolves current/original/default baseline and applies update. The return at line 17 is after a potential write. No secondary root write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs:7–49`.

**N23 — NormalizeFactionCoreChangesAsync.** currentNode is not JsonObject OR no FactionCoreChangesContract.PropertyName; or original object == null; or !evaluation.CanApply.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Current+original faction core and current/original NPC-dependent authority; Apply only after actual Evaluate CanApply. N24–N28 may derive sidecars from this core; N29 later normalizes core regardless of this command-handler return. Cannot promote CanApply=false to target acceptance.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:10–75`.

**N24 — NormalizeFactionStructureAsync.** currentNode == null && previous == null && entries.Count == 0 AFTER original sidecar, original/current core-derived structure, current sidecar, materialized governance/leadership, rank and bonus commands are collected.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current faction_core can materialize absent faction_structure; current array/nonobject roots are handled through CollectFactionEntryObjects, not rejected by this wrapper. No secondary write in this method. Both sidecar roots absent does NOT permit skip if derived core entries exist. Explicit empty previous object also prevents early return.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:138–195`.

**N25 — NormalizeFactionResourcesAsync.** currentNode == null && previous == null && entries.Count == 0 AFTER original sidecar, original/current core resources, current entries and resource-change commands are collected.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current faction_core can materialize absent faction_resources. Nonobject current flows through collector else branch. No secondary write. Absence of current/previous sidecar is insufficient; derived entries require canonical output.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:198–235`.

**N26 — NormalizeFactionProjectsAsync.** currentNode == null && previous == null && activeProjects.Count == 0 && completedProjects.Count == 0 && !hasExplicitProjectSurface.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current core determine HasExplicitMaterializedFactionProjectSurface and project lists; original/current sidecar and object commands contribute; nonobject current uses collectors. Even empty derived lists must produce sidecar when either core explicitly declares project surface. No independent secondary write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:238–287`.

**N27 — NormalizeFactionCustomAsync.** currentNode == null && previous == null && entries.Count == 0 AFTER original sidecar, original/current core custom entries, current entries and custom-state commands are collected.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Core-derived original/current entries can create an absent custom sidecar; nonobject current flows through collector. No secondary write. Need exact derived empty inventory, not merely missing sidecar.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:290–327`.

**N28 — NormalizeFactionChroniclesAsync.** currentNode == null && previous == null && initialPreviousEntries.Length == 0 && initialCurrentEntries.Length == 0.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Both original/current faction_core initial chronicle entries are calculated BEFORE return; if continuing, merge original/current chronicle collectors plus both core-derived inventories. No secondary write. Core-derived entries can create absent chronicles; array current is consumed by collector.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:330–367`.

**N29 — NormalizeFactionCoreAsync.** currentNode == null only; array/nonobject nonnull current proceeds.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Original/current faction entries are collected/normalized; materialized carrier fields removed; factions array assigned; object command property preserved if present. Prior N23 may have written same root; N24–N28 may already have derived sidecars. N29 return never retroactively skips those.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs:102–135`.

**N30 — NormalizeNpcCoreChangesAsync.** currentNode is not JsonObject OR missing NPCCoreChanges property; or selected original/treatment baseline == null; or defensive helper result not JsonObject.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Actual treatment authority may supply baseline/current authority/pending; otherwise original NPC, current world/map/faction/characteristics and trade/training pending. Helper returns unchanged cloned current when evaluation.CanApply is false. N01/N02/N31/common owner transitions may write same NPC root. CanApply=false or handler return is not whole-target acceptance.

Guards: Non-null resource publication must HasValidSeal before baseline selection. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:8–59`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:48–82`.

**N31 — NormalizeNpcTradeCoreAsync.** Disposition/authority checked BEFORE current read; currentNode is not JsonObject; or defensive helper result not JsonObject. SkipUntouchedTreatmentContinuation helper returns clone of current object, reaching W equality rather than method early return.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Current NPC, original or genuine treatment NPC baseline, and selected authenticated disposition. Apply branch preserves history, normalizes teacher/trade receipts; skip branch preserves current. Other NPC writers N01/N02/N30/common are independent. Treatment skip authority is not available merely from a flag.

Guards: Invalid nonnull publication seal throws; SelectRuntimeDisposition uses authenticated disposition or actual-continuation fallback. No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:186–220`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:84–112`, `BookOfEternityClient/Services/MortalItemNpcTradeTailPolicy.cs:7–19`.

**N32 — NormalizeNpcJournalsAsync.** currentNode is not JsonObject. Otherwise no write call if changed remains false after both NPCJournals/npcJournals object-entry loops.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call. only reached when changed == true.

Derived inputs: Current root only; NormalizeNpcJournalEntry on object entries in named arrays. No secondary writer in this method. Invalid/ignored shapes are not an accepted validity witness.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs:64–85`.

**N33 — NormalizeNpcInteractionJournalAsync.** Delegated NormalizeActorJournalAsync returns only currentNode == null; no nonobject guard.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Fixed caller path/update/actor ID keys; original object plus current node collections into canonical entries; ensure entries/remove update property. Array/nonobject current can reach canonical object write. No secondary file write in helper.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:74–114`.

**N34 — NormalizeInventoryItemsAsync.** currentNode is not JsonObject, or defensive InventoryItemsJournal result not JsonObject; with object current the helper clones then normalizes.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Current inventory only; journal normalization can change nested entries without UpdateInventory command. N02 and common item/effect-related owners remain independent writers.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:10–24`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:114–121`.

**N35 — NormalizeInventoryItemBondsAsync.** currentNode is not JsonObject, or defensive ItemBonds result not JsonObject. Object-current helper constructs canonical object.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current entries plus exact commands; canonical shape/wrapper removal can change root without a command. N02 projection may also write this fixed companion; equality here does not negate that earlier write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:26–40`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:123–159`.

**N36 — NormalizeInventoryItemTextsAsync.** currentNode is not JsonObject, or defensive ItemTexts result not JsonObject. Object-current helper constructs canonical object.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current entries plus exact commands; canonical shape/wrapper removal can change root without a command. N02 projection may also write this fixed companion; equality here does not negate that earlier write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:42–56`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:161–187`.

**N37 — NormalizeItemJournalsAsync.** currentNode is not JsonObject, or defensive NpcItemJournals result not JsonObject. Object-current helper constructs canonical object.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Own original/current entries plus exact commands; canonical shape/wrapper removal can change root without a command. N02 projection may also write this fixed companion; equality here does not negate that earlier write.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:58–72`, `BookOfEternityClient/Services/MortalItemPublicationTailTransforms.cs:189–224`.

**N38 — NormalizeGuardianThoughtJournalAsync.** Delegated NormalizeActorJournalAsync returns only currentNode == null; no nonobject guard.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Fixed caller path/update/actor ID keys; original object plus current node collections into canonical entries; ensure entries/remove update property. Array/nonobject current can reach canonical object write. No secondary file write in helper.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:74–114`.

**N39 — NormalizeGuardianSocialJournalAsync.** Delegated NormalizeActorJournalAsync returns only currentNode == null; no nonobject guard.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call.

Derived inputs: Fixed caller path/update/actor ID keys; original object plus current node collections into canonical entries; ensure entries/remove update property. Array/nonobject current can reach canonical object write. No secondary file write in helper.

Guards: No realm-based early return in this method. Chaos eligibility still requires genuine domain validators. NoPlan is a canonical writer guard requiring absent accepted mechanics authority, not a whole-method no-op or target-acceptance receipt.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs:74–114`.

**N40 — NormalizePlayerSkillStateAsync.** Both currentActive and currentPassive are null. Each nonnull root is composed; arrays/nonobject nonnull roots are not skipped by caller.

Write guard: WriteIfChangedAsync: String.Equals(currentNode?.ToJsonString(JsonOpts) ?? string.Empty, result.ToJsonString(JsonOpts), StringComparison.Ordinal). This compares reserialized nodes, not original bytes. Equality skips only this write call. per nonnull current/composed root; successful ComposeSkillAcceptedRoot returns JsonObject, not null.

Derived inputs: Each original/current skill root, exact change/removal property, metadata copying and composed capability catalog. One absent root does not prevent other root write. No extra file write; lack of skill change commands is not equality.

Guards: Ambiguous composed skills throw InvalidDataException; nonzero treatment capability issues throw. Actual realm validators still required; treatment capability schema checking is not treatment acceptance.

Sources: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Skills.cs:7–58`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs:867–889`, `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs:919–967`.

## Structural path rules and authority roles

- Original logical-to-physical mappings come only from the actual authenticated target original; source tuple, copied bytes, a hash or rollback image is not original authority. Present/absent witnesses and safe exact spellings survive separately.
- Resource quartet, six effect carriers plus identity, five wound carriers plus identity/history, NPC/guardian typed-transition roots and spiritual controls are finite fixed families listed in JSON. Real plan dictionaries select their admitted subset; arbitrary dictionary keys are not a new path grant. `ResourcePendingResolutionState.PendingPath` is exactly `game_state/control/pending_effect_resolutions.json`—one root, not two pending files.
- The actual C1 registered inventory is frozen draft PathInventory ∪ W40. Draft candidate syntax permits selected game_state/lore/world_profiles/output leaves and excludes specified service/rollback/control roots; the matched owner’s exact inventory and signed service originals are required. This existing broad selection rule does not close a bounded evaluation-family archive inventory.
- Mods are the one filesystem roster explicitly needed by health here: trusted top-level `mods/*.*` with `.json`, `.txt` or `.md`, fixed settings/manifest outputs, frozen contents and descriptor metadata. It grants no recursive mod or namespace export.
- Native/runtime G state is not a normalizer output or archive capability. `.boe_runtime` custody, source stop/release and current-G admission stay DEP-C/L. Canonical `qte_runtime.json` is a distinct game-state health root.
- DTO union/FieldToFile is data only. No new response distribution pass, incarnation acceptance, Explorer Archive/Skip, source replay or old-G rebinding is admitted.

Current-turn input correction: `TryReadCurrentTurnNumberAsync` reads only `input/turn_request.json` (`CanonicalStateNormalizer.GuardianProjectHelpers.cs:1383–1407`). N01/N02/N03/N06/N13/N14 no longer list ready/repair roots as direct turn-number inputs. This does not remove separate request/terminal/original validation obligations. N16 keeps its own progression-report tuple inputs.

## Remaining interfaces and questions

**Q-E1 — Complete validated raw target inventory (OPEN).** A bounded CaptureLifeEvaluationCandidate operation on the genuine target original/request/terminal owner, producing exact original/current/accepted leaf sets with present/absent witnesses and per-domain validator ownership. Source snapshot enumerates broad game_state/lore physical roots for rollback and original proof; that does not authorize every candidate path. Mandatory raw player_chronicle is outside normalizer writes/N82. FileMapping maps DTO routing only. Which exact unchanged and changed raw roots are accepted must be frozen by actual domain owners, including unknown/unclaimed-root rejection. Routine owned extraction where existing domain acceptance covers the leaf; unresolved domain membership requires design review, not a grant from missing helper. No generic namespace admission.

**Q-E2 — Health and output candidate projection boundary (OPEN).** PrepareEvaluationHealthInputs/ProjectEvaluationHealth and accepted-payload capture must freeze current bytes, top-level trusted mods roster/metadata, actual settings, finite UTC inputs and exact outcomes. Existing PrepareManifestForGmAsync/AcceptedTurnOutputProjector can be specialized. Model live settings/cache update and lifecycle/notification side effects as actual obligations; exclude post-E1 rerun. Genuine HP progression agreement must prevent accidental initialization/re-timing. Include A15 player-soul profile mirror before A02 and on repeated refresh: its current profile/soul/Shining inputs, trusted-local versus original dispatch, real native recovery and live-aggregation obligations. Full aggregation reads remain unclosed. Routine D11 owner specialization/pure extraction when policy unchanged; no new lifecycle/gameplay grant. Receipt composition still unaccepted.

**Q-E3 — Ordered normalizer read/write overlap and compensation coverage (OPEN).** PrepareEvaluationCanonicalProjection must preserve exact 40-stage order and pre-read guardian inputs, resolving later reads against earlier projected roots. One final path producer/cohort must reconcile guardian gacha/quests/journals, projects/rivals/events, soul/profiles, item/NPC/location, effect/wound carriers and owner authority. Read-set and rollback coverage must include afterlife_chronicles and afterlife_global_flags; N82 omission is source fact, not repaired here. Preserve raw/original/rollback distinctions and serializer/frozen-clock semantics. Every local skip/equality predicate must follow the corrected per-row branch witness; absent faction sidecars can be created from core, and nonobject/array input can normalize. Include A15/N16/C4 profile overlap. Routine D11 extraction for same owned behavior. No blanket root merge or new owner assignment accepted.

**Q-E4 — Guardian completion and separate cleanup obligations (OPEN).** Expose genuine guardian musings completed-validation and exact quest cleanup calculation/completion. Need separate receipts for repair/audit cleanup, progression E2P, runtime/story E2S, candidate manifest/returnguard E3, health synchronization E4C and exact original/native/main cleanup. File after-images cannot stand in for these capabilities/side effects. Routine owner specialization where existing owner can issue; lifecycle source-release and generation custody remain DEP-C/L, not routine new authority.

**Q-E5 — Transitive mechanics/item/spiritual closed inventories (OPEN).** Expose from actual registered owners exact finite InputInventory/CompanionInventory/ConsumedInventory with typed family tags, original/physical image roles and explicit ineligible/no-change receipts. This report resolves fixed carriers, resource quartet, transition roots and spiritual controls, but does not claim every producer of plan dictionaries or ordinary item fallback helper is transitively closed. Spiritual PathInventory uses actual frozen declared/current draft scope including world_profiles; it cannot be replaced with arbitrary snapshot of that namespace. No new independent replay or lost-current-G restoration. Existing actual C1/C4/item/resource/effect authorities support specialization; absence of an extraction helper is not a principled grant. Supporting unowned new path/type or resurrection across G is not authorized.

**Q-E6 — Durable EP/E1 family receipt and crash ownership (OPEN).** Current ordinary publisher writes individual roots then validates/compensates; returned Accepted/Plan is not a durable complete evaluation acceptance. Proposed PrepareEvaluationTargetEnvelope/PublishEvaluationTarget/ReadEvaluationTargetReceipt must be tied to all actual owners and one reviewed native cohort, exact sameG target identity and M ack. Persist attempted/committed/uncertain state and rollback/readback ownership; post-E1 recovery reads receipt, never reruns authoring, rewards, dice or source. Existing native APIs alone do not establish this family contract. Post-E1 runtime refresh cannot silently invoke A15 as an untracked canonical writer; preserve real mirror/native cleanup and live-state obligations with exact receipts. Unaccepted family composition proposal; no new gameplay/lifecycle power assumed. Any needed new lifecycle issuer remains separately G-C/DEP-L discussion; no missing helper magically grants it.

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

- T17: Absent current/previous faction sidecars with original/current core-derived entries MUST retain N24/N25/N27 writes; N26 explicit empty project surface still materializes; N28 core-derived chronicle creates root. Do not implement absence-only skip.
- T18: Array/nonobject nonnull roots for guardians/residents/character chronicle/quests/faction core/actor journals/skills: follow actual collectors and canonical shape, not generic object-only skip; separately validate malformed/JSON-null/blank/physical absence.
- T19: Equal guardian tracker/root with journal or power events, rival clue consumption, Saref direct canonical wrapper, NPC treatment skip disposition, and ordinary item zero-transfers plus unconditional identity write: check each local equality/return independently of other writes.
- T20: A15 before A02 and repeated refresh: changed/equal/missing/malformed profile/soul, optional Shining, player selector; exact-byte equality preservation, original/trusted-local dispatch, native committed/uncertain/cleanup debt, replacement, logged repair failure and live aggregation. No implicit write after accepted E1.

## Historical evidence and stop

135 original source files retain unchanged exact commit/Git blob/SHA256/byte pins in the paired JSON; two clearly separate supplemental helper pins support this correction; SHA-256 covers raw Git blob bytes, not current working-tree contents. The 16 prior declared registries were statically re-expanded at this commit (including N82/W40/IP13); no runtime or test result is inferred. The normalizer stage count and finite literal/path expansions are static report derivations only. Initial author HEAD observation: `2622abcf9163153ea4f3a78d3a0e136adc38658f`; corrected author HEAD observation: `fb6c1b22c9c1dee0b6fcd8dee5c017176921e27c` (metadata only); every claim above remains pinned to `901435333b00ad7e92cef80429ce99ef010c7db9`.

Immutable prior: `/tmp/t197-depl-leaf-catalog-corrected-20261016.md`, SHA256 `42aa4c29c028feb0212e3fbe62b91a692efe1b14a72fba92d291e10aa153861c`, 33884 bytes.
Immutable prior: `/tmp/t197-depl-leaf-catalog-corrected-20261016.json`, SHA256 `0b754c394dd64371a1d36045139162ab74eab3ddb029e09b18c70156206d4f60`, 234797 bytes.

Author correction complete; affected independent rereview is pending. All 20 future checks remain UNRUN and all six questions remain OPEN. No source lifecycle custody, target acceptance, archive schema, global gameplay grant, F2 or activation is declared complete. Original/validation/rollback roles, source Mortal n/action/k_death, evaluation n+1, genuine bootstrap n+2/k+1 when applicable, and D4/guided/visual/D8 remain unchanged. FINALSTOP.
