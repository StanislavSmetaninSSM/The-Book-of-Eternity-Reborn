# Afterlife_Abodes_Residents.md

Read when: Current resident roster/transfer request. Follow only the current dispatch/contract; Resident mentioned without request/state change does not independently activate this topic.
Prerequisites: Current request, authoritative realm and exact active pending/issued packet; [applicable common-core paragraphs](OtherGuides/GM_Common_Core.md). Topic prerequisites: [Afterlife_Lifecycle.md](OtherGuides/Afterlife_Lifecycle.md).
Author here: only existing contract-authorized outputs; this reading guide grants no additional authority.
Never author: do not invent client-owned identities, seals or unissued receipts, or mutate pending/canonical state outside the current contract. Legitimate GM-authored receipts and pending closures remain permitted only through their existing explicitly authorized owner/response surface.
Worked examples: [E_CLI_Afterlife_Abodes_Residents.txt](Examples/E_CLI_Afterlife_Abodes_Residents.txt); paired rules: [Afterlife_Abodes_Residents.md](OtherGuides/Afterlife_Abodes_Residents.md). Whole original examples/negative labels remain below.

<!-- GM1557:A037:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A037:START -->
| `pending_guardian_abode_residents_request.json` | Afterlife; `requestMode=standard_roster` creates an ordinary Abode roster, while `requestMode=founder_attraction` creates the first residents attracted by a player-founded Guardian feature | Full canonical residents through `UpdateGuardianAbodeResidents` plus `UpdateGuardianAbodeResidentRosterReceipts` | Receipt with matching request and roster count; resident objects include required identity, kind, origin, presence, bond, imprint/reward fields; founder-attraction requests also require `founderFeatureTitle` and `founderFeatureSummary` in the pending request context | Do not emit identity-only resident fragments; for `founder_attraction`, do not transfer old-patron residents or derive the roster from the former Guardian's Abode | 10 |
<!-- GM1557:A037:END -->
<!-- GM1557:A038:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A038:START -->
| `pending_guardian_abode_resident_interactions.json` | Afterlife | `UpdateGuardianAbodeResidentInteractionReceipts`; accepted outcomes also update resident logs/history/state when relevant | Matching `requestId`, `residentId`, `interactionType`, `status`, `abodeId`; history requests include `historyEntryId` pointing to `UpdateGuardianAbodeResidentHistoryLog` | Do not close a history request with a receipt that has no canonical history entry | 1, 10 |
<!-- GM1557:A038:END -->
<!-- GM1557:A039:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A039:START -->
| `pending_guardian_abode_resident_transfers.json` | Afterlife | `UpdateGuardianAbodeResidentTransferReceipts`, source/target resident state, matching history/log entries | Matching `requestId`, transfer mode/status, source and target abode/guardian data | Do not move a resident silently without a transfer receipt | 11 |
<!-- GM1557:A039:END -->
<!-- GM1557:A044:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A044:START -->
| `pending_player_guardian_foundation.json` | Ordinary Chaos Sea only, and only when the runtime preconditions below are true | `UpdateGuardians.create`, `guardians`, `activeGuardian`, `chaosSeaNavigation`, `metaStateUpdates` for soul foundation state, `playerGuardianFoundationHistory` | New founded Guardian materialized, former patron preserved as `former_patron`, navigation points to the new abode | Do not rewrite the player's soul as the Guardian; do not change patron roles without the foundation create authority | 13 |
<!-- GM1557:A044:END -->
<!-- GM1557:A045:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A045:START -->
| `game_state/control/system_guardian_attraction.json` | Ordinary Chaos Sea only with deterministic Eternal Guardian attraction | Exact requested system Guardian materialized or selected through `UpdateGuardians`, canonical `guardians`, `activeGuardian`, and `chaosSeaNavigation`; `guardians.json.pendingGuardianCreation.mode=system_preset` is related authority context | Active Guardian points to the requested preset and carries `sourcePreset` with `presetId`, `displayName`, `version`, `library`; existing requested Guardian is routed to, not duplicated; accepted `guardians.json` must remove `pendingGuardianCreation` once that Guardian is materialized | Blocks `/incarnate` until resolved or explicitly cancelled; do not create, close, or repair this from Shining Abode; do not substitute a similar archetype; do not use `UpdateNPCs`; do not omit `sourcePreset`; do not clear or replace the attraction with a different Guardian; do not leave a stale startup Guardian request beside a materialized Guardian; do not delete `pendingGuardianCreation` without materializing the requested Guardian into both `activeGuardian` and canonical `guardians[]` | 20 |
<!-- GM1557:A045:END -->
<!-- GM1557:A046:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A046:START -->

Validation repair note: if `validation_repair_request.json.harnessRepairPackets[].kind` is `guardian_pending_creation_materialization_repair`, use that packet before broad Guardian scope examples. Read `game_state/meta/guardians.json.pendingGuardianCreation` as startup authority. For New Game `mode=freeform`, the accepted turn must write `UpdateGuardians.create` as `UpdateGuardians` with `command=create` and `data=<full canonical Guardian>` as the authority, mirror that create result into full `guardians[]`, matching `activeGuardian`, and `chaosSeaNavigation.currentAbodeId`, then remove `pendingGuardianCreation`. The repair packet includes `canonicalCreateSkeleton` and `allowedEnums`; start from that skeleton, keep `relationshipData.lastInteraction` null or ISO 8601, keep `abodePower.tier` derived from `abodePower.currentPower`, leave `abodePower.history` empty unless every entry has timestamp/change/reason/source, keep at least 7 `loreFragments`, use only the listed mood/musing/lore enum values, and omit `relationshipData.guardianRoleToPlayer` unless the repair explicitly concerns a `former_patron` foundation branch. Do not repair only materialized mirrors, do not keep `pendingGuardianCreation` as a pending-only fallback, do not delete it by itself, and do not leave the Guardian only in prose.

<!-- GM1557:A046:END -->
<!-- GM1557:A052:START -->
### Player-founded Guardian foundation preconditions

The GM must treat `pending_player_guardian_foundation.json` as valid only when all of these are true in the pre-turn/current input context:
- Current realm is exactly `Chaos Sea`; never process the ritual in `Shining Abode` or `Mortal World`.
- The soul has a non-empty `soulName` / founder identity; the new Guardian is a separate Guardian actor, not the player soul rewritten as an NPC.
- `shining_abode_state.json.availability = sealed_until_next_ascension`; `active` means the Shining Abode is still open and the foundation ritual is not authorized.
- `shining_abode_state.json.preparedIncarnationPackage` is absent/null; a prepared package handoff blocks foundation.
- No active, malformed, wrong-reason, or otherwise blocking `afterlife_return_guard.json`; no malformed/existing `pending_player_guardian_foundation.json` from another request; and no existing `soul_state.playerFoundedGuardianId` or already founded Guardian for this soul.
- The pending request must echo `sourceShiningAvailability = sealed_until_next_ascension`, `founderSoulName`, previous Guardian identity, proposed mantle identity, `mantleSummary`, `mantleCreed`, appearance motifs, turn and UTC timing.

<!-- GM1557:A052:END -->
<!-- GM1557:A055:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A055:START -->
| `[ABODE_RESIDENT_RELIC_GRANT]` | Direct resident action, no pending file | The player accepts a companion-echo reward from an existing afterlife resident. Resolve directly through resident and Soul Relic state. | Required: `metaStateUpdates.soulRelicOperations.addRelic` must add a new current-turn `relicType=companion_echo` with complete `companionSeed`, `UpdateGuardianAbodeResidents` must move the existing pre-turn resident to `bondRewardState=granted` and `grantedRelicId=<new relicId>`, plus a new current-turn `residentInteractionLogUpdates` entry. Forbidden: `UpdateGuardianAbodeResidentInteractionReceipts`, roster receipts, transfer receipts, `pending_resident_companion_manifestation_request.json`, `UpdateNPCs`. | 22 |
| `[ABODE_RESIDENT_QUEST_REQUEST]` | Direct resident action, no pending file | The player helps or accepts a request from an afterlife resident. Resolve as an ordinary Soul Quest linked back to the resident. | Required: `UpdateSoulQuests` must create or progress a current-turn Soul Quest with `relatedAfterlifeResidentId`; if resident state changes, `UpdateGuardianAbodeResidents` with `linkedSoulQuestId` or bond fields; add a new current-turn `residentInteractionLogUpdates` entry. Forbidden: invented resident request receipts unless a real pending resident file exists. | 22 |
<!-- GM1557:A055:END -->
<!-- GM1557:A057:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A057:START -->
| `[ABODE_RESIDENT_ROSTER_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_abode_residents_request.json`; close the resident roster row. | Use full `UpdateGuardianAbodeResidents` plus `UpdateGuardianAbodeResidentRosterReceipts`. Do not emit identity-only resident fragments. | 10, 22 |
| `[ABODE_RESIDENT_HISTORY_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_abode_resident_interactions.json`; close the matching resident `interactionType=history` request. | Use `UpdateGuardianAbodeResidentInteractionReceipts` with `abodeId` and `historyEntryId`; also write the matching `UpdateGuardianAbodeResidentHistoryLog` entry. | 10, 22 |
| `[ABODE_RESIDENT_TALK]` | Pending-backed routing marker | Read `pending_guardian_abode_resident_interactions.json`; close the matching resident `interactionType=talk` request. | Use `UpdateGuardianAbodeResidentInteractionReceipts` with canonical resident `responseMode`; write resident logs/state only when the scene changes them. | 10, 22 |
<!-- GM1557:A057:END -->
<!-- GM1557:A058:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A058:START -->
| `[ABODE_RESIDENT_TRANSFER_REQUEST]` | Pending-backed routing marker | Read `pending_guardian_abode_resident_transfers.json`; close the exact transfer request. | Preserve `selectionMode`, `competitionScore`, `competitionLabel`, and `competitionReason` from the pending contract in reasoning/receipt context; write transfer receipt plus source/target resident state/history. | 11, 22 |
<!-- GM1557:A058:END -->
<!-- GM1557:A060:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A060:START -->
| `[PLAYER_GUARDIAN_FOUNDATION]` | Pending-backed routing marker | Read `pending_player_guardian_foundation.json`; close the player-founded Guardian row. | Use foundation authority surfaces: `UpdateGuardians.create`, canonical `guardians`, `activeGuardian`, `chaosSeaNavigation`, soul foundation fields, and `playerGuardianFoundationHistory`. | 13, 22 |
<!-- GM1557:A060:END -->
<!-- GM1557:A071:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A071:START -->
| `[ABODE_RESIDENT_RELIC_GRANT]` | Ordinary afterlife with explicit existing resident reward action | New current-turn companion-echo relic through `metaStateUpdates.soulRelicOperations.addRelic`, current-turn resident reward transition through `UpdateGuardianAbodeResidents`, new `residentInteractionLogUpdates` entry | No pending file is closed by this tag; do not satisfy it with an already granted relic/resident state; do not invent resident interaction/roster/transfer receipts | 22 |
| `[ABODE_RESIDENT_QUEST_REQUEST]` | Ordinary afterlife with explicit existing resident quest action | New or changed current-turn `UpdateSoulQuests`, optional `UpdateGuardianAbodeResidents`, new `residentInteractionLogUpdates` entry | The quest must carry `relatedAfterlifeResidentId`; do not satisfy it with an unchanged pre-existing quest/log; do not route through Mortal `UpdateQuests` or NPC state | 22 |
| `[CHAOS_SEA_TRAVEL]` Abode travel | Ordinary Chaos Sea, target abode selected from Guardian Abodes already present in pre-turn `chaosSeaNavigation.discoveredAbodes[]` | `game_state/meta/guardians.json`: `activeGuardian` set exactly to `targetGuardianId`, `chaosSeaNavigation.currentAbodeId` set exactly to `targetAbodeId`, `chaosSeaNavigation.discoveredAbodes[]` still contains `targetAbodeId`, target guardian `abode.isDiscovered=true` | The GM must not redirect to a different discovered abode/guardian or make travel legal by adding the target to `discoveredAbodes` during the same turn; keep target `guardianId`/`abodeId` aligned; do not use Mortal World `currentLocationData`, `UpdateNPCs`, weather/time, or `worldEventsLog` for afterlife travel | 7A |
<!-- GM1557:A071:END -->
<!-- GM1557:A072:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A072:START -->
| Freeform Chaos Sea Abode search from `/обители` | Ordinary Chaos Sea only; the player uses free text after the "Поиск новой обители" preview and there is no pending/control file | Failure: narrative only and no hidden state. Distant signal: narrative clue only unless a concrete Guardian/Abode is actually materialized. Found Abode: `UpdateGuardians.create` for a new Guardian or supported Guardian update for an existing one, plus canonical `guardians`, `activeGuardian`, and `chaosSeaNavigation` roots when the player actually arrives | Every stateful search outcome must carry explicit `guardianId`, `guardianName`, `abodeId` or location label, and `reason/source = chaos_sea_abode_search` in the authored audit/reasoning. Do not create hidden Guardians, do not teleport on a distant signal, do not write Mortal World `currentLocationData`, `UpdateNPCs`, `worldEventsLog`, weather, time, or world travel state | 23 |
<!-- GM1557:A072:END -->
<!-- GM1557:A076:START -->
For freeform Chaos Sea Abode search, see example 23 before deciding whether the outcome is failure, distant signal, found Abode, or rare Guardian.

<!-- GM1557:A076:END -->
<!-- GM1557:A079:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A079:START -->
- Shining resident faction persistence requires canonical Shining resident fields. A resident aligned to Shining state must remain an ascended resident with `ascensionState = "ascended"` and a `shiningFactionId` that resolves to an existing Shining faction. If the resident is not ascended, canonical normalization uses `ascensionState = "remained_in_chaos_sea"`, clears `shiningFactionId` and `residentRole`, and resets `factionLoyaltyLevel = 0`, `factionLoyaltyTier = alienated`, `factionRestlessness = 0`, and `factionRealignmentState = settled`. If the resident is ascended but `shiningFactionId` is missing or points at a missing Shining faction, runtime preserves `ascensionState = "ascended"` and treats the resident as ascended but unaffiliated: `shiningFactionId` and `residentRole` are cleared and the same loyalty/restlessness/realignment fields reset. If the resident is ascended and the faction exists, normalization derives or validates `residentRole`, `factionLoyaltyLevel`, `factionLoyaltyTier`, `factionRestlessness`, and `factionRealignmentState` from the current resident/faction state; do not preserve stale non-canonical `shiningAlignment` fields.
<!-- GM1557:A079:END -->
<!-- GM1557:A084:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A084:START -->
- Canonical Abode resident object fields used by roster, transfer, social, and Shining migration contracts:
  - Required identity/context: `residentId`, `residentName` or `displayName`, `residentKind`, `originType`, `isPresent`, `sourceGuardianId`, `sourceAbodeId`.
  - Required disposition/progression: `bondLevel`, `bondTier`, `abodeDevotionLevel`, `abodeDevotionTier`, `restlessness`, `migrationState`, `abodeDisposition`.
  - Optional but canonical when relevant: `formerGuardianId`, `formerAbodeId`, `linkedSoulRelicId`, `grantedRelicId`, `relatedQuestIds`, `historyLog`, `interactionLog`.
  - Shining-specific extension fields are allowed only for ascended residents: `ascensionState`, `shiningFactionId`, `residentRole`, `factionLoyaltyLevel`, `factionLoyaltyTier`, `factionRestlessness`, `factionRealignmentState`.
- Abode resident canonical disposition/devotion/migration vocabulary:
  - `abodeDisposition.powerSensitivity`, `communalOrientation`, and `stabilityNeed`: `low`, `medium`, `high`.
  - `abodeDisposition.migrationDisposition`: `rooted`, `selective`, `opportunistic`, `wandering`.
  - `abodeDevotionTier`: `alienated`, `uncertain`, `attached`, `devoted`, `steadfast`; derived from `abodeDevotionLevel` ranges `0..19`, `20..39`, `40..59`, `60..79`, `80..100`.
  - `migrationState`: `settled`, `wavering`, `restless`, `considering_departure`, `ready_to_transfer`; derive from devotion/restlessness: `ready_to_transfer` when devotion `<=15` and restlessness `>=70`, `considering_departure` when devotion `<=30` and restlessness `>=55`, `restless` when devotion `<=45` or restlessness `>=45`, `wavering` when devotion `<=60` or restlessness `>=30`, otherwise `settled`.
<!-- GM1557:A084:END -->
<!-- GM1557:A087:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A087:START -->
- Resident history log entries use canonical history identity fields such as `entryId`, `residentId`, `title`, `summary`, `revealedAtTurn`, and `revealedAtUtc`; receipts reference those entries by id when closing history requests.
<!-- GM1557:A087:END -->
