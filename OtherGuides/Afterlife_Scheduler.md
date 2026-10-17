# Afterlife_Scheduler.md

Read when: mustEvaluateGuardianProjectProgression=true. Follow only the current dispatch/contract; All due flags false and no catchup does not independently activate this topic.
Prerequisites: Current request, authoritative realm and exact active pending/issued packet; [applicable common-core paragraphs](OtherGuides/GM_Common_Core.md). Topic prerequisites: [Afterlife_Guardian_Contracts.md](OtherGuides/Afterlife_Guardian_Contracts.md), [Afterlife_Lifecycle.md](OtherGuides/Afterlife_Lifecycle.md).
Author here: only existing contract-authorized outputs; this reading guide grants no additional authority.
Never author: do not invent client-owned identities, seals or unissued receipts, or mutate pending/canonical state outside the current contract. Legitimate GM-authored receipts and pending closures remain permitted only through their existing explicitly authorized owner/response surface.
Worked examples: [E_CLI_Afterlife_Scheduler.txt](Examples/E_CLI_Afterlife_Scheduler.txt); paired rules: [Afterlife_Scheduler.md](OtherGuides/Afterlife_Scheduler.md). Whole original examples/negative labels remain below.

<!-- GM1557:A009:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A009:START -->
- If afterlife scheduler debt is due, write `game_state/control/progression_report.json` through `progressionProcessingReport`.
- `game_state/control/progression_schedule.json` is client-owned scheduler state. The GM reads `input/turn_request.json.progressionControl` as the turn contract and writes only `progression_report.json`; never edit schedule baselines, pending cycle counts, `last*Ordinal`, or catch-up markers directly.
<!-- GM1557:A009:END -->
<!-- GM1557:A022:START -->
## Living-World Scheduler Matrix

Each due contour is mandatory. If the correct outcome is stability/no mutation, the GM still explains that decision in `gm_thoughts_markdown` and reports the contour as processed.

| Contour | Trigger | GM must read | Legal outputs | Required report fields | Example |
|---|---|---|---|---|---|
| Chaos Sea hub | `mustEvaluateChaosSeaProgression=true` | `soul_state.json`, `guardians.json`, `guardian_projects.json`, `guardian_abode_residents.json`, relevant pending files | Narrative hub pressure, Guardian/Soul/Abode state when concrete systems change | `chaosSeaCyclesProcessed`, `newLastChaosSeaSimulationOrdinal` | 1, 16 |
| Guardian projects | `mustEvaluateGuardianProjectProgression=true` | `guardian_projects.json`, `guardians.json`, Guardian journals, relevant project/power pending state | `startGuardianProjects`, `guardianProjectUpdates`, `completeGuardianProjects`, `guardianThoughtJournalUpdates`, `guardianSocialJournalUpdates`, `guardianPowerEvents`, limited `UpdateGuardians` | `guardianProjectCyclesProcessed`, `newLastGuardianProjectCycleOrdinal` | 1, 5, 16 |
| Resident agency | `mustEvaluateResidentAgencyProgression=true` | `guardian_abode_residents.json`, resident pending files, resident-linked Soul Quest state | `residentThoughtJournalUpdates`, `residentInteractionLogUpdates`, `UpdateGuardianAbodeResidentHistoryLog`, `UpdateGuardianAbodeResidents`, `UpdateSoulQuests`, resident relic reward surfaces | `residentAgencyCyclesProcessed`, `newLastResidentAgencyCycleOrdinal` | 1, 10, 11, 17 |
| Shining Abode civic state | `mustEvaluateShiningAbodeProgression=true` | `shining_abode_state.json`, halls, gates draft, radiance, Light Sparks, client-owned treasury, core receipts, package state | Canonical `shining_abode_state.json` mutation plus Shining receipt arrays when a contract is active; treasury operations are local client-owned mutations with no GM receipt | `shiningAbodeCyclesProcessed`, `newLastShiningAbodeCycleOrdinal` | 2, 14, 17 |
| Shining factions | `mustEvaluateShiningFactionProgression=true` | `shining_abode_state.json.factions[]`, `shiningPoliticalActors[]`, resident alignment, leadership, faction pending files | Shining faction state, political actor registry updates, faction receipts, leadership history, resident ascension/alignment updates | `shiningFactionCyclesProcessed`, `newLastShiningFactionCycleOrdinal` | 2, 9, 17 |
| Shining trade | `mustEvaluateShiningTradeProgression=true` | Shining factions, trade inventory, trade receipts, pending Shining trade requests | Faction `tradeInventory`, `tradeInventoryReceipts[]`, trade availability/sold-out state | `shiningTradeCyclesProcessed`, `newLastShiningTradeCycleOrdinal` | 2, 17 |
| Bounded afterlife catch-up | `afterlifeCatchupRequired=true` | All state for contours listed in `afterlifeCatchupContours[]` | Exactly `afterlifeCatchupSummaryEventsRequired` high-level summary outcomes across listed contours | `afterlifeCatchupProcessed=true`, `afterlifeCatchupSummaryEventsProcessed` equal to required count, plus per-contour fields for processed contours | 1, 2 |

Scheduler report rule: each `newLast*Ordinal` belongs only to its own contour. Do not advance Guardian, resident, Shining faction, or trade markers by the largest backlog from another contour.

## Living-World Outcome Selection

- Chaos Sea hub cycles answer what the Sea itself did while the player focused elsewhere: soul-current pressure, Abode omens, Guardian politics, relic/archive currents, or metaphysical hazards. If nothing changes, explain the stable equilibrium and still process the contour.
- Guardian project cycles answer which Guardian plan advanced, stalled, stabilized, created pressure, relieved pressure, or produced a musing/lore/power consequence. Use project surfaces and Guardian journals, not NPC activity.
- Resident agency cycles answer which resident thought, remembered, requested, withdrew, helped, realigned, transferred, unlocked history, linked a Soul Quest, or granted a relic. Residents are authored afterlife actors and must not freeze between player visits.
- Shining Abode cycles answer which hall, gate draft, public ritual, Radiance pressure, Light Spark context, civic order, or Abode-wide tension changed. If a Shining mutation invalidates gate inputs, mark open gates stale.
- Shining faction cycles answer which institution changed strength, support, leadership, claims, loyalty, resident alignment, unrest, alliance, or project pressure. Use Shining faction state, not Mortal faction files. If leadership uses `headActorType = radiant_actor`, maintain the `shiningPoliticalActors[]` registry so `headActorId` resolves to an existing `actorId`; accepted radiant-actor succession sets the new head actor to `currentFactionId=<factionId>`, `politicalStatus=head`, and moves a replaced radiant head to `politicalStatus=former_head`.
- Shining trade cycles answer which faction trade inventory, rarity ceiling, service multiplier, sold-out state, or availability changed. Use Shining trade receipts and canonical inventories.
- Catch-up cycles are never simulated one by one. Produce exactly the requested number of summary outcomes and map each summary to the affected `afterlifeCatchupContours`.

<!-- GM1557:A022:END -->
<!-- GM1557:A033:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A033:START -->
| `game_state/control/progression_schedule.json` | Client-owned afterlife and Mortal scheduler state | Nothing directly; when `progressionControl` requires work, write `progressionProcessingReport` / `game_state/control/progression_report.json` only | Report matches current `sessionId`, `requestId`, `turnNumber`, exact processed counts, catch-up proof, and per-contour `newLast*Ordinal` values from `progressionControl` | Do not write or repair `progression_schedule.json` manually; do not advance schedule ordinals by editing the schedule; do not put processed counts into the schedule; do not use Mortal `world_time.json` or `timeChange` as afterlife proof | 1, 2, 16, 17 |
<!-- GM1557:A033:END -->
