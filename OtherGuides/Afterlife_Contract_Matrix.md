# Afterlife Contract Matrix — Chaos Sea and Shining Abode

This guide is mandatory operational context for every GM turn where `game_state/meta/soul_state.json.currentRealm` is `Chaos Sea`, `Море Хаоса`, `Shining Abode`, or `Сияющая Обитель`, and for any turn whose `progressionControl` contains afterlife debt.

The GM does not need to read client code. Use this matrix to decide which afterlife contracts are active, which files must be read, which canonical surfaces must be written, and which Mortal World substitutions are forbidden. Use `Examples/E_CLI_Afterlife_Turns.txt` for full worked JSON fragments after selecting the relevant contract rows here.

## Universal Afterlife Rules

- Always run realm gate first: ordinary `Chaos Sea`, ordinary active `Shining Abode`, or `Shining Abode pending-bootstrap handoff`.
- Always read `input/turn_request.json.progressionControl` before selecting the scene, tone, relevant actors, or player-action resolution.
- Always inspect `game_state/control/` pending afterlife files before deciding that the turn is only narrative.
- If a pending file exists, it is a client-authored contract. It must close through the exact canonical state/receipt surface named below.
- If afterlife scheduler debt is due, write `game_state/control/progression_report.json` through `progressionProcessingReport`.
- Copy `sessionId`, `requestId`, and `turnNumber` exactly from the current request into every required report/receipt that carries turn correlation.
- Declare all changed Guardians, residents, Shining factions, halls, gates, head actors, and institutions in `gm_thoughts_markdown` actor scope.
- Do not write `game_state/control/afterlife_notifications.json`; the client derives notifications from canonical state and receipts.
- Do not satisfy afterlife meaning through Mortal World channels: `UpdateNPCs`, `NPCsInScene`, `UpdateQuests`, `worldEventsLog`, `factionDataChanges`, `factionProjectUpdates`, `completeFactionProjects`, `factionChronicleUpdates`, `currentLocationData`, `worldMapUpdates`, `timeChange`, `setWorldTime`, `weatherChange`, combat fields, XP, skills, money, health, poise, or regular inventory.

## Realm Mode Matrix

| Mode | When active | What GM may process | What GM must suppress |
|---|---|---|---|
| Ordinary `Chaos Sea` | `currentRealm` is empty/`Chaos Sea`/`Море Хаоса`, no active Shining package handoff | Guardians, Abodes, Soul state, Soul Relics, direct Chaos Sea gacha, afterlife Ink Feather whitelist, Guardian projects, residents, archive, ordinary incarnation and ascension choices | Mortal World combat/NPC/location/faction/time systems |
| Ordinary active `Shining Abode` | `currentRealm = Shining Abode` and `shining_abode_state.json.preparedIncarnationPackage = null` | Shining civic state, Shining factions, Shining trade, Shining core actions, Guardians/residents that persist into the Abode, afterlife scheduler | Mortal World systems and ordinary Chaos-only lifecycle shortcuts that do not apply in Shining |
| `Shining Abode pending-bootstrap handoff` | `currentRealm = Shining Abode` and `preparedIncarnationPackage` is a non-null object | Only lifecycle/bootstrap `TriggerIncarnation` for the frozen package | Ordinary Shining scheduler, Guardian/Abode roleplay, Shining core actions, trade, politics, package clearing/mutation |

## Living-World Scheduler Matrix

Each due contour is mandatory. If the correct outcome is stability/no mutation, the GM still explains that decision in `gm_thoughts_markdown` and reports the contour as processed.

| Contour | Trigger | GM must read | Legal outputs | Required report fields | Example |
|---|---|---|---|---|---|
| Chaos Sea hub | `mustEvaluateChaosSeaProgression=true` | `soul_state.json`, `guardians.json`, `guardian_projects.json`, `guardian_abode_residents.json`, relevant pending files | Narrative hub pressure, Guardian/Soul/Abode state when concrete systems change | `chaosSeaCyclesProcessed`, `newLastChaosSeaSimulationOrdinal` | 1, 16 |
| Guardian projects | `mustEvaluateGuardianProjectProgression=true` | `guardian_projects.json`, `guardians.json`, Guardian journals, relevant project/power pending state | `guardianProjectUpdates`, `completeGuardianProjects`, `guardianThoughtJournalUpdates`, `guardianSocialJournalUpdates`, `guardianPowerEvents`, limited `UpdateGuardians` | `guardianProjectCyclesProcessed`, `newLastGuardianProjectCycleOrdinal` | 1, 5, 16 |
| Resident agency | `mustEvaluateResidentAgencyProgression=true` | `guardian_abode_residents.json`, resident pending files, resident-linked Soul Quest state | `residentThoughtJournalUpdates`, `residentInteractionLogUpdates`, `UpdateGuardianAbodeResidentHistoryLog`, `UpdateGuardianAbodeResidents`, `UpdateSoulQuests`, resident relic reward surfaces | `residentAgencyCyclesProcessed`, `newLastResidentAgencyCycleOrdinal` | 1, 10, 11, 17 |
| Shining Abode civic state | `mustEvaluateShiningAbodeProgression=true` | `shining_abode_state.json`, halls, gates draft, radiance, Light Sparks, core receipts, package state | Canonical `shining_abode_state.json` mutation plus Shining receipt arrays when a contract is active | `shiningAbodeCyclesProcessed`, `newLastShiningAbodeCycleOrdinal` | 2, 14, 17 |
| Shining factions | `mustEvaluateShiningFactionProgression=true` | `shining_abode_state.json.factions[]`, resident alignment, leadership, faction pending files | Shining faction state, faction receipts, leadership history, resident ascension/alignment updates | `shiningFactionCyclesProcessed`, `newLastShiningFactionCycleOrdinal` | 2, 9, 17 |
| Shining trade | `mustEvaluateShiningTradeProgression=true` | Shining factions, trade inventory, trade receipts, pending Shining trade requests | Faction `tradeInventory`, `tradeInventoryReceipts[]`, trade availability/sold-out state | `shiningTradeCyclesProcessed`, `newLastShiningTradeCycleOrdinal` | 2, 17 |
| Bounded afterlife catch-up | `afterlifeCatchupRequired=true` | All state for contours listed in `afterlifeCatchupContours[]` | Exactly `afterlifeCatchupSummaryEventsRequired` high-level summary outcomes across listed contours | `afterlifeCatchupProcessed=true`, `afterlifeCatchupSummaryEventsProcessed` equal to required count, plus per-contour fields for processed contours | 1, 2 |

Scheduler report rule: each `newLast*Ordinal` belongs only to its own contour. Do not advance Guardian, resident, Shining faction, or trade markers by the largest backlog from another contour.

## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
| `pending_abode_offering.json` with `offeringType=ink_feathers` | Afterlife only | `guardianPowerEvents` with `reasonType=offering` and `sourceSurface=guardianAbodeOffering`; `output/ink_feather_action_result.json` with `actionTag=ABODE_OFFERING` | Ink Feather receipt `stateEvidence` includes affected files plus `guardianId`, `returnCycleId`, `powerGain`, `powerEventId`; power event id matches the Abode Power journal/event | Do not use an unsupported feather amount; do not omit `powerGain`; do not list unrelated proof files | 8, 16 |
| `pending_abode_offering.json` with `soul_relic`, `archive_lore_fragment`, or `archive_secret_record` | Afterlife only | `guardianPowerEvents` with `reasonType=offering` and `sourceSurface=guardianAbodeOffering` | Matching offering audit and Abode Power event | Do not write `output/ink_feather_action_result.json`; no fake `costInFeathers` | 8 |
| `pending_guardian_trade_request.json` | Afterlife with current Guardian trade context | Materialized `guardian.tradeInventory` plus `UpdateGuardianTradeInventoryReceipts` | Matching `requestId`, `guardianId`, `abodeId`, `tradeCycleId`, `status=ready`, `itemCount`, timing | Do not place commandless `tradeInventory` inside ignored `UpdateGuardians`; stock without receipt is not closed | 4 |
| `pending_guardian_abode_residents_request.json` | Afterlife | Full canonical residents through `UpdateGuardianAbodeResidents` plus `UpdateGuardianAbodeResidentRosterReceipts` | Receipt with matching request and roster count; resident objects include required identity, kind, origin, presence, bond, imprint/reward fields | Do not emit identity-only resident fragments | 10 |
| `pending_guardian_abode_resident_interactions.json` | Afterlife | `UpdateGuardianAbodeResidentInteractionReceipts`; accepted outcomes also update resident logs/history/state when relevant | Matching `requestId`, `residentId`, `interactionType`, `status`, `abodeId`; history requests include `historyEntryId` pointing to `UpdateGuardianAbodeResidentHistoryLog` | Do not close a history request with a receipt that has no canonical history entry | 1, 10 |
| `pending_guardian_abode_resident_transfers.json` | Afterlife | `UpdateGuardianAbodeResidentTransferReceipts`, source/target resident state, matching history/log entries | Matching `requestId`, transfer mode/status, source and target abode/guardian data | Do not move a resident silently without a transfer receipt | 11 |
| `pending_guardian_social_interactions.json` | Afterlife | `guardianSocialJournalUpdates` | Matching `requestId`, `guardianId`, `interactionType` (`talk` or `lore`), `status`, title/summary/timing | Do not use NPC social journals or `UpdateNPCs` for Guardians | 12 |
| `pending_player_guardian_foundation.json` | Ordinary Chaos Sea only | `UpdateGuardians.create`, `guardians`, `activeGuardian`, `chaosSeaNavigation`, `metaStateUpdates` for soul foundation state, `playerGuardianFoundationHistory` | New founded Guardian materialized, former patron preserved as `former_patron`, navigation points to the new abode | Do not rewrite the player's soul as the Guardian; do not change patron roles without the foundation create authority | 13 |
| `pending_archive_consultation_request.json` | Afterlife | `archiveActionResolutions` and canonical `soul_state.afterlifeArchive.actionReceipts[]` | Matching `requestId`, `archiveId`, requested mode/status, available archive reservation or completed receipt | Do not create mortal inventory/lore notifications as the closure | 5 |
| `pending_archive_project_fuel_request.json` | Afterlife | `archiveActionResolutions`; project/log effects only when allowed by the request | Matching archive action receipt; `lore_fragment` may resolve as project work, `secret_record` resolves as pressure relief | Do not use a `secret_record` as `project_work` fuel | 5 |
| `pending_shining_abode_actions.json` | Ordinary active Shining Abode only, with `preparedIncarnationPackage=null` | Canonical `shining_abode_state.json` mutation plus `coreActionReceipts[]` | Receipt matches request `requestId`, `actionType`, status/timing, target ids, cost fields, and action-specific snapshots | Do not process while package handoff is pending; do not route through mortal factions | 14, 17, 18 |
| `pending_shining_faction_foundings.json` | Ordinary active Shining Abode | New/updated Shining faction state plus `factionFoundingReceipts[]` | Matching request, faction identity, founder/role data, status/timing | Do not create Mortal World `factionDataChanges` | 9 |
| `pending_shining_faction_realignments.json` | Ordinary active Shining Abode | Resident/faction alignment state plus `factionRealignmentReceipts[]` | Matching resident, source/target faction, realignment mode, status/timing | Do not use non-canonical `shiningAlignment`; ascended residents need canonical Shining resident fields | 9 |
| `pending_shining_faction_leadership_transitions.json` | Ordinary active Shining Abode | Faction leadership state/history plus faction `leadershipReceipts[]` | Matching transition mode, incumbent, resolved candidate/vacancy, status/timing | Do not put leadership in Mortal World faction chronicles | 9 |
| `pending_shining_trade_inventory_requests.json` | Ordinary active Shining Abode | Faction `tradeInventory` plus faction `tradeInventoryReceipts[]` | Inventory matches derived tier/slot count/rarity/service multiplier/trade cycle; receipt has `status=ready`, `itemCount`, `soldOutCount`, timing | Do not invent one-slot inventory when request derives more slots; do not omit item `priceInFeathers` or nested `relicData` | 2, 17 |
| `pending_resident_companion_manifestation_request.json` | MortalWorldProfile only | Nothing in afterlife; treat as stale/repair-only context | None in afterlife | Do not materialize mortal NPCs or encounters in `Chaos Sea` or `Shining Abode` | none |

## Shining Core Action Matrix

All rows below use `pending_shining_abode_actions.json` as input and close through `shining_abode_state.json.coreActionReceipts[]`. They are legal only in ordinary active `Shining Abode`.

If a Shining core action mutates the faction/project inputs used by the blessing-card gates draft (`invest_in_faction`, `complete_project`, `support_project`, `unsupport_project`, or `retire_project`) and the pre-turn state has `gates.hasOpenDraft = true`, preserve the canonical `gates` object and set `gates.isStale = true`. A stale draft cannot be used for `prepare_incarnation_package`; the player must regenerate it through `open_gates`.

| `actionType` | GM state responsibility | Required caution |
|---|---|---|
| `discover_native_faction` | Materialize the discovered Shining faction/institution and receipt | The discovered faction must be Shining state, not Mortal World faction state |
| `invest_in_faction` | Apply faction strength/support effects and receipt | Costs/effects must match the client-authored pending request; if gates are open, mark `gates.isStale = true` |
| `complete_project` | Move the Shining project to completed/result state and receipt | Project-sourced effects must come from a project actually present in pre-turn Shining state; if gates are open, mark `gates.isStale = true` |
| `support_project` | Toggle support state and receipt | Support/unsupport toggles have quoted Light Sparks cost `0`; do not spend Light Sparks; if gates are open, mark `gates.isStale = true` |
| `unsupport_project` | Toggle support off and receipt | Same zero-cost rule as `support_project`; if gates are open, mark `gates.isStale = true` |
| `retire_project` | Move eligible project out of active rotation and receipt | Do not delete unrelated project history; if gates are open, mark `gates.isStale = true` |
| `open_gates` | Update the canonical Shining gates blessing-card draft container and receipt | `gates` is not a custom gate registry; use the canonical draft state such as draft version/open/stale/card selection fields |
| `prepare_incarnation_package` | Persist `preparedIncarnationPackage` and receipt with frozen selected card snapshot | Package uses `selectedCardIds`, `selectedCards`, `generatedFromDraftVersion`, `preparedAtTurn`, `preparedAtUtc`; do not use `packageId`, `createdAt*`, or `sourceDraftVersion` |
| `pull_relic_gacha` | Add/result a Shining relic pull through Shining state and receipt | This is not direct Chaos Sea `/gacha` and not Guardian-mediated `UpdateGuardians.processGacha` |
| `forge_relic.reshape` | Apply supported forge reshape result and receipt | Keep target relic and property identity aligned with request |
| `forge_relic.retune_property` | Apply property retune and receipt | Keep target property index/id aligned with request |
| `forge_relic.strengthen_band` | Apply strength band increase and receipt | Do not exceed the request-authorized outcome |
| `forge_relic.stabilize_echo` | Apply echo stabilization and receipt | Do not invent unrelated relic effects |
| `forge_relic.uplift_rarity` | Apply rarity uplift and receipt | Resulting rarity must be supported by Shining forge contract |

## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
| Ordinary `TriggerIncarnation` | Ordinary Chaos Sea with explicit player choice/prerequisites | `TriggerIncarnation` to `game_state/control/incarnation_trigger.json` | Do not switch `soul_state.currentRealm` to Mortal World in the same turn; client performs bootstrap | 6 |
| Guardian-forced `TriggerIncarnation` | Ordinary player-driven Chaos Sea turn with explicit provocation against current active Guardian | `TriggerIncarnation` with `source=guardian_forced`, guardian id, severity, reason, provocation summary, and normal world/character/circumstances | Harsh but survivable; never from Shining pending-bootstrap handoff | 15 |
| `AscensionTrigger` | Chaos Sea only, max Enlightenment, explicit `playerChoice=Ascension` | `AscensionTrigger` / `playerChoice` to `ascension.json` | Do not combine with `TriggerLifeEnd` or manual Shining realm switch | covered by core docs |
| Shining pending-bootstrap `TriggerIncarnation` | `Shining Abode` with non-null valid `preparedIncarnationPackage` | `TriggerIncarnation` only | Preserve `preparedIncarnationPackage` exactly; runtime consumes/clears it after successful Mortal bootstrap | 3, 18 |
| Direct Chaos Sea `/gacha` / `[CHAOS_SEA_DIRECT_GACHA]` | Ordinary Chaos Sea | Soul Relic result through Soul/meta surfaces | Do not emit `UpdateGuardians.processGacha`; do not spend feathers a second time if the client already deducted cost | 7 |
| Guardian-mediated gacha | Ordinary afterlife with eligible Guardian and remaining charges | `UpdateGuardians.processGacha` and resulting Soul Relic state | Respect per-return charge limits and Guardian reputation tier; direct `/gacha` does not consume Guardian charges | 7 |
| Freeform Guardian command | Afterlife only | Supported `UpdateGuardians` command, Guardian journals/projects/power surfaces as needed | Guardians are not NPCs; actor scope must cover changed Guardians | 15 |
| Afterlife Ink Feather action | Afterlife only and action-specific prerequisites satisfied | `output/ink_feather_action_result.json` plus the canonical state surface for the action | Afterlife whitelist is only Donate to Guardian, Cultivate Enlightenment, Guardian Favor, Memory Gates, Soul Imprint, and Ink Feather Abode Offering | see Ink Feather examples |

## Canonical State Reminders

- Shining resident faction persistence requires canonical Shining resident fields. A resident aligned to Shining state must remain an ascended resident, including `ascensionState = "ascended"` where applicable; otherwise normalization can clear Shining faction affiliation.
- Shining trade inventory must match the pending request's derived values: `tradeCycleId`, `generatedAtUtc`, `generationTradeTier`, `generationRarityCeiling` (`none`, `common`, `uncommon`, `rare`, `radiant`), `merchantProfile` (normally `shining_faction`), `serviceMultiplierSnapshot`, and item slots with positive `priceInFeathers` plus nested `relicData`.
- Guardian trade inventory is stored on the Guardian, while Shining trade inventory is stored on the Shining faction. Do not swap these surfaces.
- Resident history log entries use canonical history identity fields such as `entryId`, `residentId`, `title`, `summary`, `revealedAtTurn`, and `revealedAtUtc`; receipts reference those entries by id when closing history requests.
- Actor journal entries require stable identity and timing. Use explicit entry ids/timestamps rather than anonymous narrative fragments.
- `preparedIncarnationPackage` is frozen state. During Shining handoff the GM preserves it; during `prepare_incarnation_package` the GM creates it with selected cards and draft version; only runtime clears it after successful bootstrap.
- `afterlife_notifications.json` is never a GM-authored proof surface.

## Example Coverage Map

| Need | Example |
|---|---|
| Chaos Sea catch-up, Guardian project, resident history | 1 |
| Active Shining core action, faction/trade/resident living world | 2 |
| Shining pending-bootstrap handoff | 3 |
| Guardian trade request | 4 |
| Archive consultation/project fuel | 5 |
| Ordinary Chaos Sea incarnation | 6 |
| Direct Chaos Sea gacha and Guardian-mediated gacha | 7 |
| Afterlife Abode Offering | 8 |
| Shining founding/realignment/leadership | 9 |
| Resident roster request | 10 |
| Resident transfer request | 11 |
| Guardian social interaction | 12 |
| Player-founded Guardian foundation | 13 |
| Shining core action variants | 14 |
| Freeform Guardian commands | 15 |
| Combined Chaos Sea scheduler + offering + action | 16 |
| Combined active Shining scheduler + core action + trade | 17 |
| Prepare package now, bootstrap later | 18 |
