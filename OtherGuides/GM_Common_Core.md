# GM_Common_Core.md

Read when: An actual ordinary turn selects common obligations, or an exact current repair/spiritual-frontier packet selects only its matching correlation/terminal/repair paragraphs. Bootstrap and dedicated receipt-only tasks read no core body.
Prerequisites: Current dispatch, authoritative realm/request and exact issued permitted-read/write scope; select topic dependencies through [CLI_Rules_Index.md](CLI_Rules_Index.md).
Author here: only existing contract-authorized outputs; this reading guide grants no additional authority.
Never author: do not invent client-owned identities, seals or unissued receipts, or mutate pending/canonical state outside the current contract. Legitimate GM-authored receipts and pending closures remain permitted only through their existing explicitly authorized owner/response surface.
Worked examples: [E_CLI_Step_Main.txt](Examples/E_CLI_Step_Main.txt) for its authorized ordinary flow; exact afterlife pairs are named in [CLI_Rules_Index.md](CLI_Rules_Index.md). Do not apply ordinary examples to a restricted task.

Ordinary-turn workflows below apply only to an actual ordinary turn. Bootstrap and dedicated receipt-only tasks read no core body. Spiritual frontier/validation repair use only their current matching correlation/terminal/repair paragraphs, without adding an ordinary request, scene, actor, scheduler or lifecycle obligation.

<!-- GM1557:A001:START -->
# Afterlife Contract Matrix — Chaos Sea and Shining Abode

<!-- GM1557:A001:END -->
<!-- GM1557:A005:START -->
This matrix is the afterlife navigation entrypoint. Read the compact common core and only topics selected by the current request, realm/lifecycle and exact active contract union in CLI_Rules_Index.md; bootstrap and dedicated receipt-only tasks retain their narrower reading scope.

The GM must not read client implementation code during normal play or validation repair. Use current request, named copied GM docs, validation_repair_request.json.harnessRepairPackets, safe probes, allowed session state/control files and helper commands. Report an undocumented/unavailable contract in gm_thoughts_markdown; never invent gameplay outcomes or bypass realm/pending/canonical restrictions.

## Universal Afterlife Rules

<!-- GM1557:A005:END -->
<!-- GM1557:A007:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A007:START -->
- Always inspect `game_state/control/` pending afterlife files before deciding that the turn is only narrative.
- Always inspect non-pending afterlife control guards such as `system_guardian_attraction.json` and `afterlife_return_guard.json`; these are client-owned contracts/guards, not optional lore.
- If a pending file exists, it is a client-authored contract. It must close through the exact canonical state/receipt surface named below.
<!-- GM1557:A007:END -->
<!-- GM1557:A010:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A010:START -->
- Copy `sessionId`, `requestId`, and `turnNumber` exactly from the current request into every required report/receipt that carries turn correlation.
- Declare all changed Guardians, residents, Shining factions, halls, gates, head actors, and institutions in `gm_thoughts_markdown` actor scope.
<!-- GM1557:A010:END -->
<!-- GM1557:A018:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A018:START -->
- Do not write `game_state/control/afterlife_notifications.json`; the client derives notifications from canonical state and receipts such as archive receipts, trade receipts, resident/Guardian social receipts, Shining core receipts, and Shining blessing-effect consumption.
- Do not satisfy afterlife meaning through Mortal World channels. File-level rule: during `Chaos Sea` / `Shining Abode`, no response surface may write or mutate any file mapped to `game_state/core/player_status.json`, `game_state/player/*`, `game_state/inventory/*`, `game_state/world/*`, `game_state/npcs/*`, `game_state/combat/*`, `game_state/factions/*`, `lore/current_world/*`, Mortal quest files (`game_state/quests/regular_quests.json`, `quest_history.json`, `plot_outline.json`), or Mortal misc files (`game_state/misc/characteristics.json`, `vehicles.json`, `storage_access.json`, `player_interactions.json`). This includes `UpdateNPCs`, `NPCsInScene`, `UpdateQuests`, `worldEventsLog`, `factionDataChanges`, `factionProjectUpdates`, `completeFactionProjects`, `factionChronicleUpdates`, `currentLocationData`, `worldMapUpdates`, `timeChange`, `setWorldTime`, `weatherChange`, combat fields, XP, skills, money, health, poise, ordinary item inventory, vehicles, storage access, and current-world lore. Ordinary `Chaos Sea` turns also must not mutate `game_state/meta/shining_abode_state.json`; that owner-state file belongs to ordinary active `Shining Abode` contracts, Shining pending-bootstrap preservation, or client-owned local routes.
- If an accepted afterlife turn violates that file-level rule, the client may restore/delete the forbidden wrong-realm files from the validated `game_state/control/pending_turn_snapshot` before asking the GM for repair. The audit is written to `game_state/control/validation_auto_rollback_report.json`; it is client-owned diagnostic evidence, not a GM-authored state surface and not permission to use Mortal World substitutions.
- If restored forbidden files exactly match the validated snapshot, snapshot-matched baseline validation errors from those files are suppressed from the current afterlife repair request. Treat them as pre-existing baseline hygiene outside the current turn, not as permission or obligation to edit MortalWorldProfile files.

<!-- GM1557:A018:END -->
<!-- GM1557:A020:START -->
## GM Decision Loop

Use this loop for every `Chaos Sea` / `Shining Abode` turn before writing files:

1. Classify the realm mode. If this is Shining pending-bootstrap handoff, stop ordinary afterlife processing and preserve `preparedIncarnationPackage`. If the package is present but invalid, fail closed: preserve the package and all pending Shining files for repair.
2. Read `progressionControl` and list every due contour, expected count, catch-up pressure tier, and catch-up contour.
3. Read all afterlife pending files. Each present pending file activates one row in this matrix and must close through the exact state surface and receipt listed here.
4. Resolve in order: bounded catch-up summary outcomes first, ordinary due cycles second, pending contracts and direct player action third.
5. Write only afterlife state surfaces plus exact receipts/reports. If a due contour produces no state mutation, record the reason in `gm_thoughts_markdown` and still report the processed cycle.
6. Audit the response for forbidden Mortal World substitutions, missing actor scope, missing receipts, stale gates, and package mutation before terminal completion.

<!-- GM1557:A020:END -->
<!-- GM1557:A023:START -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:A023:END -->
<!-- GM1557:A024:START -->
| Client-authored pending/control files in this matrix | Pre-turn input contract | Close through the row's canonical response/state surfaces only | The live pending/control file remains byte-identical to the validated pre-turn snapshot until the client cleanup step removes it | Do not rewrite, retarget, replace, clear, or "advance" the pending/control file in GM output; validator treats valid-looking replacements as GM-side mutation | All |
<!-- GM1557:A024:END -->
<!-- GM1557:A053:START -->
## Preview Ownership Rules

- A contract-backed preview is a player-facing audit for a GM-resolved afterlife action. If the preview names a `playerAction` tag, pending/control file, request id, cost, or closure surface, the GM must close that exact contract through the matrix row for the tag/file.
- A client-local mutation preview is not an afterlife-turn contract. Soul Relic equip/unequip is the canonical local example: no GM turn is sent, no pending/control file is created, no receipt/progression report/gm_thoughts output is required, and no existing pending contract is closed. The same ownership rule applies to the browser parity entry points `/soul_relic_equip` and `/soul_relic_unequip` plus their Russian aliases, which the client routes through `SoulRelicEquipmentService` against `game_state/meta/soul_state.json` without producing a GM turn.
- Do not convert a client-local preview into GM-authored state. Only the client applies the listed local state mutation after the player confirms the preview.
- High-cost or destructive Chaos Sea previews must expose the exact target/source identity before confirmation: Guardian id/name for donation/favor/offering/trade, resident id/name for resident notifications, Soul Relic/archive id/name/rarity/type for consumed objects, and companion/NPC id/name plus traits/personality markers for `SOUL_IMPRINT`.
- `SOUL_IMPRINT` receipt proof must include `stateEvidence.imprintId`, `stateEvidence.companionName`, source provenance (`sourceCompanionId`/`companionId` or `NPCId`/`sourceNpcId`), and `stateEvidence.affectedFiles` pointing at `game_state/meta/soul_state.json`; the persisted `soulImprint` must keep the same source provenance. It prepares a future companion/imprint path and does not create a Mortal NPC during the afterlife turn.
- The preview must show projected state/resource deltas when the client can know them: Ink Feathers before/after, Abode Power current/projected plus `baseDelta`/`finalDelta`, Guardian reputation expected change, trade/buyback price and projected balance, and the pending/control file or local state surface that will change.
- Post-resolution player-visible detail surfaces must expose the matching receipt/audit identifiers when available: `requestId`, `historyEntryId`, `guardianPowerEvents.powerEventId`, trade `slotId`, buyback `buybackEntryId`, archive action receipt id, resident roster/interaction/transfer receipt identity, and the derived current state snapshot.
- Guardian trade purchase/sale/buyback previews are client-local coordinated writes, not GM turns, but they still need full audit JSON so the player can see `priceInFeathers`, `currentFeathers`, `projectedFeathers`, sold-out or buyback status, and cloned `relicData`.

<!-- GM1557:A053:END -->
<!-- GM1557:A054:START -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:A054:END -->
<!-- GM1557:A077:START -->
## Canonical State Reminders

<!-- GM1557:A077:END -->
<!-- GM1557:A091:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A091:START -->
- Derived notification triggers: `afterlife_notifications.json` is client-owned and derived from canonical state/receipts. The list below is the current exhaustive notification-type vocabulary; future runtime additions must update this matrix and coverage tests in the same change. The GM writes only the canonical receipt/state, never manual notification entries.
  - Guardian trade and quest/foundation: `guardian_trade_inventory_ready`, `guardian_trade_pending_attention`, `guardian_quest_available`, `player_guardian_foundation_resolved`.
  - Resident roster/direct rewards/manifestation/social pressure/transfer: `abode_residents_ready`, `abode_resident_quest_available`, `abode_resident_relic_granted`, `abode_resident_manifestation_ready`, `companion_imprint_manifestation_ready`, `abode_resident_talk_answered`, `abode_resident_history_revealed`, `abode_resident_history_refused`, `abode_resident_wavering`, `abode_resident_restless`, `abode_resident_considering_departure`, `abode_resident_transfer_pending`, `abode_resident_transfer_accepted`, `abode_resident_transfer_refused`, `abode_resident_transfer_departed`.
  - Archive actions: `archive_consultation_accepted`, `archive_consultation_rejected`, `archive_consultation_cancelled`, `archive_consultation_pending_attention`, `archive_project_fuel_accepted`, `archive_project_fuel_rejected`, `archive_project_fuel_cancelled`, `archive_project_fuel_pending_attention`.
  - Shining Abode: `shining_trade_inventory_ready`, `shining_core_action_resolved`, `shining_faction_founding_resolved`, `shining_faction_realignment_resolved`, `shining_faction_leadership_resolved`.
- `pendingShiningBlessingEffects` is runtime-created from `preparedIncarnationPackage.selectedCards` after Mortal bootstrap. Supported pending families/statuses include `memorySelection.status=pending_pre_turn_one_selection`, `resourceGrant.status=applied_at_bootstrap`, `relicRefinementEntitlements.status=pending_relic_entitlement`, `pendingSocialEffects.status=pending_first_relation_commit`, `pendingRouteEffects.status=pending_early_route_seed`, `pendingLoreEffects.status=pending_lore_insertion`, `pendingSurvivalEffects.status=pending_first_ruinous_failure`, and `pendingDescentEffects.status=pending_resident_descent`. Most families are later consumed through ordinary Mortal surfaces, but `relicRefinementEntitlements` is the explicit exception: Shining forge previews/requests can consume its rerolls/freeShape/freeRetune entitlement in Shining Abode, and the GM closes only the resulting `pending_shining_abode_actions.json` forge receipt/state contract. For `relicRefinementEntitlements`, terminal status is only `consumed` with `consumedAtTurn` and `consumedAtUtc`; do not write `expired` there. Deadline-based `expired` statuses with `expiredAtTurn`/`expiredAtUtc` apply only to supported deferred effect arrays such as route/lore/descent. `resourceGrant.grantId` and hidden `_shiningBootstrapResourceGrantIds` markers in player/inventory state are client-owned idempotency audit data; the GM must not author or spend them manually.
- `afterlife_notifications.json` is never a GM-authored proof surface.
<!-- GM1557:A091:END -->
<!-- GM1557:A097:START -->
- CLI_Agent_Daemon_Specification.md
- TaskGuides/CLI_Step_Main.txt
- Rules/Block_0.txt
- Rules/Block_CLI_Operations.txt
- CLI_API_Specification.md
- OtherGuides/Afterlife_Contract_Matrix.md

Use this example navigator for the exact active route in CLI_Rules_Index.md. Read only the matching complete thematic examples and their actual prerequisites; availability never requires all example bodies, and bootstrap/receipt-only scope remains narrow.

Before using an example, first select the matching rows in `OtherGuides/Afterlife_Contract_Matrix.md`. The matrix tells the GM which scheduler contour, pending file, direct action, canonical output surface, receipt/report, and forbidden substitution apply. These examples then show worked accepted-turn patterns. They are not a replacement for the full schemas.

<!-- GM1557:A097:END -->
<!-- GM1557:A100:START -->
==================================================
GLOBAL AFTERLIFE TURN RULE
==================================================

Every accepted afterlife turn must follow this order:

1. Realm gate:
   - Read `game_state/meta/soul_state.json.currentRealm`.
   - Decide whether this is ordinary Chaos Sea, ordinary active Shining Abode, or Shining Abode pending-bootstrap handoff.

2. Lifecycle guards:
   - Check `afterlife_return_guard.json`, `ascension.json`, `incarnation_trigger.json`, and `shining_abode_state.json.preparedIncarnationPackage`.
   - Do not mix return, life evaluation, ascension, incarnation, and Shining bootstrap logic.

3. Scheduler first:
   - Read `input/turn_request.json.progressionControl` before resolving the player action.
   - Every `mustEvaluate* = true` is mandatory.
   - `afterlifeCatchupRequired = true` is one bounded summary obligation, not thousands of raw cycles.
   - `game_state/control/progression_schedule.json` is client-owned; never edit it directly. Close scheduler obligations only through `progressionProcessingReport` / `game_state/control/progression_report.json`.

4. State loading:
   - Read `soul_state.json`.
   - Treat `soul_state.soulName`, `previousSoulNames`, and optional `soulFormDescription` as player-authored identity for the same soul. Use the form in afterlife prose when relevant; do not overwrite it in ordinary GM output.
   - Read `guardians.json`.
   - Read `guardian_projects.json`.
   - Read `guardian_abode_residents.json`.
   - In Shining Abode, also read `shining_abode_state.json`.
   - If `game_state/control/next_life_scenario_core.json` exists, read it as client-owned next-life bootstrap context. Do not edit or close it. `scenarioCoreAssertions[]` are hard facts for the next life; `candidateAssertions[]` are candidate-only until later accepted state confirms them.

5. Pending contracts:
   - Read pending afterlife request files in `game_state/control/`.
   - Close every present pending contract through its canonical receipt/state surface.

6. Actor scope:
   - Declare every changed Guardian, resident, Shining faction, hall, gate, or institution as relevant.
   - Explain why plausible non-processed actors are outside scope.

7. Living-world debt:
   - Process due contours through Guardian/Soul/resident/Shining surfaces.
   - Do not use Mortal World NPC/faction/location/time/weather/combat/inventory channels.

8. Direct player action:
   - Process the player action only after the above context is known.

9. Report:
   - If any afterlife cycle is due, write `progressionProcessingReport` with exact processed counts and exact per-contour `newLast*Ordinal`.
   - Copy `sessionId`, `requestId`, and `turnNumber` from the current `input/turn_request.json`; do not invent new correlation values.
   - Do not delete or rewrite `input/turn_request.json`; it remains client-owned authority until the client closes the wait cycle.
   - The report is the only GM-authored scheduler proof. Never write processed counts, `last*Ordinal` markers, catch-up baselines, or pending cycle counts into `progression_schedule.json`.

10. Final audit:
   - Remove stale Mortal World outputs.
   - Do not author `afterlife_notifications.json`; the client derives it from canonical receipts/state.

<!-- GM1557:A100:END -->
