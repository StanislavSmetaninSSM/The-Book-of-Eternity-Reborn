# Afterlife_Guardian_Contracts.md

Read when: Current nonempty pending_guardian_trade_request. Follow only the current dispatch/contract; Stale/empty Guardian trade bundle without contract does not independently activate this topic.
Prerequisites: Current request, authoritative realm and exact active pending/issued packet; [applicable common-core paragraphs](GM_Common_Core.md). Topic prerequisites: [Afterlife_Lifecycle.md](Afterlife_Lifecycle.md).
Author here: only existing contract-authorized outputs; this reading guide grants no additional authority.
Never author: do not invent client-owned identities, seals or unissued receipts, or mutate pending/canonical state outside the current contract. Legitimate GM-authored receipts and pending closures remain permitted only through their existing explicitly authorized owner/response surface.
Worked examples: [E_CLI_Afterlife_Guardian_Contracts.txt](../Examples/E_CLI_Afterlife_Guardian_Contracts.txt); paired rules: [Afterlife_Guardian_Contracts.md](Afterlife_Guardian_Contracts.md). Whole original examples/negative labels remain below.

<!-- GM1557:A016:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A016:START -->
- For an output-only repair after a valid original `UpdateGuardians.addMusings` turn, preserve the materialized musings and mirror, repair only the listed outputs, and close the matching repair request. Do not replay the consumed command or duplicate the same thought in the journal. The client rechecks its sealed original request/turn/generation and unchanged Guardian state for one validation iteration; changed state or original evidence cannot borrow that completion. See `guardian_musings_original_bound_v1` in Examples/E_CLI_Afterlife_Turns.txt.
<!-- GM1557:A016:END -->
<!-- GM1557:A034:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A034:START -->
| `pending_abode_offering.json` with `offeringType=ink_feathers` | Afterlife only | `guardianPowerEvents` with `reasonType=offering` and `sourceSurface=guardianAbodeOffering`; `output/ink_feather_action_result.json` with `actionTag=ABODE_OFFERING` | Ink Feather receipt `stateEvidence` includes affected files plus `guardianId`, `returnCycleId`, `powerGain`, `powerEventId`; power event id matches the Abode Power journal/event | Do not use an unsupported feather amount; do not omit `powerGain`; do not list unrelated proof files | 8, 16 |
<!-- GM1557:A034:END -->
<!-- GM1557:A035:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A035:START -->
| `pending_abode_offering.json` with `soul_relic`, `archive_lore_fragment`, or `archive_secret_record` | Afterlife only | `guardianPowerEvents` with `reasonType=offering` and `sourceSurface=guardianAbodeOffering` | Matching offering audit and Abode Power event; for `soul_relic`, preserve pending `relicRarity` exactly as the client's canonical snapshot resolved from stored relic aliases `rarity -> quality -> relicRarity` | Do not write `output/ink_feather_action_result.json`; no fake `costInFeathers`; do not recompute a Soul Relic rarity from a different alias | 8 |
<!-- GM1557:A035:END -->
<!-- GM1557:A036:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A036:START -->
| `pending_guardian_trade_request.json` | Afterlife with current Guardian trade context | Preferred: after dot-sourcing `gm_turn_helper.bootstrap.ps1`, call `Complete-BoeGuardianTradeInventoryRequest -RequestId '<requestId>' -Items <items>`. Manual fallback: materialized `guardian.tradeInventory` plus `UpdateGuardianTradeInventoryReceipts`. | Matching `requestId`, `guardianId`, `abodeId`, `tradeCycleId`, `status=ready`, `itemCount`, timing; every `tradeInventory.items[].slotId` is unique inside the inventory; helper computes canonical generation/pricing reputation tier, `priceInFeathers`, `projectBonusSignature` fields, and `activeGuardian` mirror. | Do not place commandless `tradeInventory` inside ignored `UpdateGuardians`; stock without receipt is not closed; duplicate `slotId` values make local purchase ambiguous and are invalid; do not hand-calculate Guardian prices when the helper is available. | 4 |
<!-- GM1557:A036:END -->
<!-- GM1557:A040:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A040:START -->
| `pending_guardian_social_interactions.json` | Afterlife | `guardianSocialJournalUpdates` | Matching `requestId`, `guardianId`, `interactionType` (`talk` or `lore`), `status`, title/summary/timing | Do not use NPC social journals or `UpdateNPCs` for Guardians | 12 |
<!-- GM1557:A040:END -->
<!-- GM1557:A049:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A049:START -->
| `pending_archive_consultation_request.json` | Afterlife | `archiveActionResolutions` and canonical `soul_state.afterlifeArchive.actionReceipts[]` | Matching `requestId`, `archiveId`, requested mode/status, available archive reservation or completed receipt | Do not create mortal inventory/lore notifications as the closure | 5 |
| `pending_archive_project_fuel_request.json` | Afterlife | `archiveActionResolutions`; project/log effects only when allowed by the request | Matching archive action receipt; `lore_fragment` may resolve as project work, `secret_record` resolves as pressure relief | Do not use a `secret_record` as `project_work` fuel | 5 |

<!-- GM1557:A049:END -->
<!-- GM1557:A050:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A050:START -->
Guardian quest hooks must preserve their machine origin. Ordinary lore-research hooks use `questOrigin=lore_research_hook`; special lines unlocked by lore research use `questOrigin=lore_research_special_line`; archive consultation hooks use `questOrigin=archive_consultation_hook`; voluntary / добровольные baseline assignments for a future mortal life use `questOrigin=guardian_baseline_mortal_life_hook`. When the hook came from a concrete Guardian project or consultation tied to a project, keep `sourceProjectId=<projectId>` in `availableQuests`, `activeQuests`, and `completedQuests`; never replace this with only title text. Baseline hooks do not require `sourceProjectId`, are offers in `availableQuests`, and become `activeQuests` only after explicit player acceptance. Do not fail validation only because a Guardian currently has no available quests.

Guardian quest lifecycle across Mortal life:
- `availableQuests[]` are offers; `status` may be omitted or `available`.
- `activeQuests[]` are accepted quests; `status` may be omitted for legacy entries or be `active`, `ready_to_turn_in`, `failed`, or `expired`.
- Mortal World may use only `guardianQuestProgressUpdates[]` to update an existing pre-turn `activeQuests[]` entry. It must not use `UpdateGuardians`, Guardian reputation, Abode navigation, Gacha, projects, Guardian identity mutation, or direct writes to `game_state/meta/guardian_abode_residents.json`, `game_state/meta/guardian_thought_journal.json`, `game_state/meta/guardian_social_journal.json`, `game_state/meta/guardian_projects.json`, `game_state/meta/guardian_project_journal.json`, `game_state/meta/abode_power_journal.json`, or `game_state/meta/shining_abode_state.json` from Mortal World.
- `ready_to_turn_in` means the mortal objective is satisfied and waits for afterlife hand-in. Required proof lives in `readyToTurnInEvidence` and is non-physical: `memoryImprint`, `lifeEventEvidence`, `itemEcho`, `locationWitness`, `craftedOutcome`, `knowledgeTrace`, or `soulResonance`.
- A Mortal inventory item never crosses into afterlife. Do not write `physicalItem`, `inventoryItem`, `transferredItem`, `transferredItemId`, or `mortalInventoryTransfer` as Guardian quest proof.
- Final closure after return to the Guardian uses `UpdateGuardians.completeQuest`; it can target only accepted quests in `activeQuests[]`, never unaccepted offers in `availableQuests[]`. Explicit `status=active`, `failed`, or `expired` is not ready for successful hand-in.
<!-- GM1557:A050:END -->
<!-- GM1557:A056:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A056:START -->
| `[GUARDIAN_TRADE_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_trade_request.json`; close the Guardian trade row in the Pending Contract Matrix. | Materialize `guardians[].tradeInventory` and `UpdateGuardianTradeInventoryReceipts`. Do not put commandless trade inventory into `UpdateGuardians`. | 4, 22 |
| `[ARCHIVE_CONSULTATION_REQUEST]` | Pending-backed routing marker | Read `pending_archive_consultation_request.json`; close the archive consultation row. | Use `archiveActionResolutions` and canonical `soul_state.afterlifeArchive.actionReceipts[]`; do not create mortal lore/inventory output. | 5, 22 |
| `[ARCHIVE_PROJECT_FUEL_REQUEST]` | Pending-backed routing marker | Read `pending_archive_project_fuel_request.json`; close the archive project-fuel row. | Use `archiveActionResolutions`; `lore_fragment` may become `project_work`, while `secret_record` is only `pressure_relief`. | 5, 22 |
<!-- GM1557:A056:END -->
<!-- GM1557:A059:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A059:START -->
| `[GUARDIAN_SOCIAL_TALK_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_social_interactions.json`; close the matching Guardian `interactionType=talk` request. | Use `guardianSocialJournalUpdates` with matching `requestId`, `guardianId`, `interactionType`, `status`, optional Guardian `responseMode`, title/summary/turn/timestamp. | 12, 22 |
| `[GUARDIAN_SOCIAL_LORE_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_social_interactions.json`; close the matching Guardian `interactionType=lore` request. | Use `guardianSocialJournalUpdates`; `responseMode` is normally `lore_revealed`, `lore_refused`, `warning`, or `refusal` depending on what the Guardian actually does. | 12, 22 |
<!-- GM1557:A059:END -->
<!-- GM1557:A063:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A063:START -->
Guardian trade repair discipline: if validation reports `guardian_trade_request_missing_guardian_resolution`, `guardian_trade_request_missing_inventory_resolution`, or `guardian_trade_request_missing_receipt_resolution`, use the `guardian_trade_inventory_resolution_repair` harness packet and prefer `Complete-BoeGuardianTradeInventoryRequest -RequestId '<requestId>' -Items <items>` before broad examples. `pending_guardian_trade_request.json` stays read-only; repair only the matching `guardian.tradeInventory`, the matching ready receipt (`UpdateGuardianTradeInventoryReceipts` or `guardians[].tradeInventoryReceipts[]`), and necessary Guardian-centric debug reasoning for the same rejected turn.

<!-- GM1557:A063:END -->
<!-- GM1557:A065:START -->
## Social Response Modes And Transfer Metadata

- Guardian social `responseMode` values are `talk_scene`, `lore_revealed`, `lore_refused`, `warning`, `refusal`, `trust_shift`, and `attitude_shift`. Use them only on `guardianSocialJournalUpdates` / Guardian social closure entries.
- Resident interaction `responseMode` values are `talk_scene`, `history_revealed`, `history_refused`, `history_partial`, and `bond_shift_only`. Use them on `UpdateGuardianAbodeResidentInteractionReceipts` and matching resident journal/history outputs.
- Resident transfer `selectionMode` values are `competition_recommended`, `manual_override`, and `departure_only`. When the pending request carries a competition decision, preserve `competitionScore` in the range `0..100`, `competitionLabel` exactly as one of `strong_pull`, `plausible_pull`, `weak_pull`, and non-empty `competitionReason`; `departure_only` must not carry target Guardian/Abode competition metadata.

<!-- GM1557:A065:END -->
<!-- GM1557:A070:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A070:START -->
| Direct Chaos Sea `/gacha` / `[CHAOS_SEA_DIRECT_GACHA]` | Ordinary Chaos Sea | Soul Relic result through Soul/meta surfaces | Read full `turn_request.json.gachaBaseResult` (`diceUsed`, `baseScore`, `baseRarity`, `formula`) for audit; do not emit `UpdateGuardians.processGacha`; do not spend feathers a second time if the client already deducted cost; preserve exact `<N> Чернильных Перьев` / `<N> Ink Feathers` phrase from playerAction; final rarity must equal `gachaBaseResult.baseRarity` exactly with no upgrade/downgrade path; accepted state may only contain exact feather spend plus one new Soul Relic append, with all pre-existing Soul Relic JSON and unrelated soul fields byte-equivalent to the pre-turn snapshot | 7 |
<!-- GM1557:A070:END -->
<!-- GM1557:A073:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A073:START -->
| Guardian-mediated gacha | Ordinary afterlife with eligible Guardian and remaining charges | `UpdateGuardians.processGacha` plus matching `metaStateUpdates.inkFeatherChanges.spend` and `metaStateUpdates.soulRelicOperations.addRelic` for the resulting Soul Relic | Respect per-return charge limits from Guardian reputation tier; rarity upgrades are only Abode Power ceiling / completed `relic_forging`; when `gachaBonusAudit.relicForgingBonusSteps > 0`, `sourceProjectId` must point to a completed relic-forging project available in the current Guardian project tracker authority, not only in a stale pre-turn snapshot; direct `/gacha` does not consume Guardian charges | 7 |
<!-- GM1557:A073:END -->
<!-- GM1557:A074:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A074:START -->
| Freeform Guardian command | Afterlife only | Supported `UpdateGuardians` command, Guardian journals/projects/power surfaces as needed | Guardians are not NPCs; actor scope must cover changed Guardians. If validation writes `validation_repair_request.json.harnessRepairPackets[]` with `kind=guardian_scope_repair`, read that packet before individual errors: use `canonicalActorNames[]` for `Relevant actors` and reasoning headings, repair only `targetFiles`, treat `activeGuardian` / `guardians[]` as materialized mirrors reconstructed from validated pre-turn Guardian authority plus authorized same-turn mutations, and if `guardian_projects.json` is listed, restore project trackers from validated pre-turn project authority plus authorized same-turn project commands. If the packet kind is `actor_reasoning_subpoint_repair`, repair only `output/debug_logs.json.gm_thoughts_markdown` by following its complete Actor Brain `debugLogTemplate`; if it is `actor_memory_persistence_repair`, append only the missing first-person Guardian thought without replaying the scene. Guardian repair defaults to the packet-listed `guardianThoughtJournalUpdates` / `guardian_thought_journal.json`; do not touch `guardians.json` unless a separate current error lists it. First-pass authoring may still use `UpdateGuardians.addMusings`. Close with `Complete-BoeValidationRepair`. Do not read implementation code (`BookOfEternityClient/**/*.cs`) to infer these repair rules | 15 |
| Afterlife Ink Feather action | Afterlife only and action-specific prerequisites satisfied | `output/ink_feather_action_result.json` plus the canonical state surface for the action | Afterlife whitelist is only Donate to Guardian, Cultivate Enlightenment, Guardian Favor, Memory Gates, Soul Imprint, and Ink Feather Abode Offering. Cultivate Enlightenment must apply `experienceGain = costInFeathers * 4` and the Shining Abode ascension-ready threshold is `60` Enlightenment XP. Donate to Guardian must apply exactly `min(25, max(15, costInFeathers / 3))` to the target Guardian's reputation, not just any positive increase | see Ink Feather examples |

<!-- GM1557:A074:END -->
<!-- GM1557:A085:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A085:START -->
  - Founder-attraction and ordinary roster residents use the same Abode resident vocabulary; `guest` is never a valid `abodeDevotionTier`.
- Guardian project `assistAudit` / `sabotageAudit` scoring is deterministic. Every axis is an integer `0..2`. `assistScore = DomainRelevance + RiskOrCost + ScarcityOrUniqueness + DirectProjectImpact`; classifications are `<=2 not qualified as project assist`, `3..4 minor assist`, `5..6 meaningful assist`, `7+ major breakthrough`. With `auditKind=defense`, the same bands become `minor defensive help`, `meaningful protection`, and `major defensive breakthrough`, and the Abode Power reason routes as `rival_defense`.
- Guardian project sabotage uses `sabotageSeverityScore = HostileReach + ProjectExposure + DamageIntent + DamageAchieved + PlayerComplicity`; classifications are `<=2 nuisance`, `3..4 minor interference`, `5..7 major sabotage`, `8+ grand strike`. Abode Power deltas are assist `+1/+2/+3` for minor/meaningful/major and sabotage `-1/-2/-4` for minor/major/grand.
- Guardian project `soul_preparation` is next-life-only setup, not a current afterlife reward. Completed `soul_preparation` projects require `projectOutcomeAudit.preparationBudgetPoints` and `projectOutcomeAudit.preparationClaimPriorityBonus`; their `effectState` carries `targetIncarnation`, `preparationBudgetPointsGranted`, `preparationBudgetPointsSpent`, `preparationClaimPriorityBonusGranted`, and `consumedAtLifeStart`. Sabotaged `soul_preparation` projects require `projectOutcomeAudit.hostilePriorityTokensGranted`; their `effectState` carries `targetIncarnation`, `hostilePriorityTokensGranted`, `hostilePriorityTokensSpent`, and `consumedAtLifeStart`. The client consumes these fields during next-life Guardian correction resolution, so the GM must not spend them manually or leave them as perpetual bonuses.
- Guardian corrections are generated by the client at Mortal bootstrap from `next_life_scenario_core.json`, active Guardian state, rival claims, and temporary `soul_preparation` effects. The resulting `game_state/world/guardian_corrections.json` is a readable current-life journal, not a pending file. Matching Abode Power spends use life-scoped `eventId=gce_life_<lifeIncarnation>_<correctionId>`, `guardianPowerEvents.reasonType=correction_spend`, `sourceSurface=guardian_corrections`, `sourceId=<correctionId>`, and audit fields `lifeIncarnation`, `correctionId`, `slotId`, `slotType`, `severity`, `claimStrength`, and `intent`; the event appears once per accepted life application in Guardian/Abode Power chronology, while same-life replay is a no-op, and the GM does not author `guardian_corrections.json`.
<!-- GM1557:A085:END -->
<!-- GM1557:A090:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A090:START -->
- `afterlifeArchiveUpdates` is a GM-authored Soul-state update surface only for exceptional/system archive rewards. Valid commands are `add` with a complete archive `entry` (`archiveId`, `entryType=lore_fragment|secret_record`, `title`, `summary`, `rarity`, `sourceLife`, `acquiredAtUtc`, optional source Guardian fields/tags/reservation) or `remove` with `archiveId`. Ordinary archive consultation/project-fuel closures use `archiveActionResolutions`, not improvised add/remove.
- Archive enum vocabulary is strict. Archive entries use `entryType = lore_fragment | secret_record`; `sourceKind = codex | system`; `reservation.kind = consultation | project_fuel`; pending archive requests use `requestedMode = consultation | project_fuel`; archive action resolutions use `status = accepted | rejected | cancelled`; project-fuel accepted results use `resultMode = project_work | pressure_relief`; consultation accepted outcomes may use `guaranteedArchiveQuestCount`, `questHookCount`, `specialQuestLineUnlocks`, `visibleRivalClueBonus`, and `archiveWarningTierBonus`. Every accepted archive resolution must preserve `requestId`, `archiveId`, requested mode, and the concrete reservation/source identity instead of summarizing them away.
<!-- GM1557:A090:END -->
