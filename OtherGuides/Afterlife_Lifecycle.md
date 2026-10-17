# Afterlife_Lifecycle.md

Read when: Resolved ordinary afterlife realm; no handoff. Follow only the current dispatch/contract; Missing/null/empty realm remains unresolved fault; no ordinary topics does not independently activate this topic.
Prerequisites: Current request, authoritative realm and exact active pending/issued packet; [applicable common-core paragraphs](OtherGuides/GM_Common_Core.md).
Author here: only existing contract-authorized outputs; this reading guide grants no additional authority.
Never author: do not invent client-owned identities, seals or unissued receipts, or mutate pending/canonical state outside the current contract. Legitimate GM-authored receipts and pending closures remain permitted only through their existing explicitly authorized owner/response surface.
Worked examples: [E_CLI_Afterlife_Lifecycle.txt](Examples/E_CLI_Afterlife_Lifecycle.txt); paired rules: [Afterlife_Lifecycle.md](OtherGuides/Afterlife_Lifecycle.md). Whole original examples/negative labels remain below.

<!-- GM1557:A008:CONTEXT -->
## Universal Afterlife Rules

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A008:START -->
- Fresh New Game system Guardian seed is client-owned when the player selects a named system Guardian during New Game; freeform New Game Guardian seed is client-owned as well. Both builders feed the same deterministic Fresh resource plan: Guardian/profile/Soul owner after-images and the definitions/state/history/owner-authority quartet are published by one coordinated resource write set, never by earlier separate owner writes. In that startup flow `game_state/meta/guardians.json` already contains canonical `guardians[]`, `activeGuardian`, and `chaosSeaNavigation`, with no `pendingGuardianCreation`; system seeds use `guard_system_*` actors and carry `sourcePreset`, while freeform seeds carry `originType=freeform` and `freeformSourceDescription`. `game_state/meta/afterlife_entity_profiles.json` also contains a client-created profile for the same Guardian actor with `mentorProfile.canTeach=true`, starter `standardArts`, `progressionStrategy`, and `ledger`. The client may create a basic `mentorTrainingShowcase.generatedBy=client_system_guardian_starter_showcase` for built-in system Guardians directly from those starter `standardArts` so the first `/обучение` screen does not block on GM service work. The GM narrates the first meeting instead of materializing or repairing the Guardian; do not emit `UpdateGuardians.create`, do not add a new `pendingGuardianCreation`, and do not replace this starter profile with a bare identity stub. If `/обучение` creates an `afterlife_teacher_showcase` request for a non-client-owned or authorial mentor, answer it by updating the existing profile through `afterlifeEntityProfileUpdates[].mentorTrainingShowcase` with the requested `sourceActorSnapshotHash`.
- If a New Game / afterlife Guardian repair request reaches `harnessRepairPackets[].kind = "accepted_turn_output_artifact_repair"` or issue code `accepted_turn_stale_player_facing_output_after_canonical_repair`, the canonical Guardian state is already the authority for that repair step. Use `game_state/control/gm_context_pack/Templates/OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md` and treat packet `targetFiles[]` as the exact allowlist. Rewrite only listed stale output files so they match repaired canonical state. If `output/debug_logs.json` is absent, preserve its valid Actor Brain and exact Guardian/resident/entity/faction journal surface unchanged; if listed, keep the complete Actor Brain and exact `Изменения состояния` evidence instead of generic `state unchanged` wording. Do not rematerialize Guardians or edit canonical afterlife state unless the same current repair request still lists canonical errors. When one current repair request does list both afterlife canonical and player-facing targets, complete every canonical write first and only then rewrite dependent output. The repair loop retains the original canonical target set through all later output-only retries: output must be strictly newer than the latest actual write of every retained target, equal timestamps are stale, request creation/dispatch/repair-loop start never substitute for that boundary, and an unobservable target write fails closed.
- Realm-wrong pending files are not free authoring prompts. A valid MortalWorldProfile-only companion manifestation file is preserved as next-life context, while valid empty request files may be removed as stale clutter; malformed pending files are preserved for repair. Non-empty or malformed Mortal-only NPC social/trade pending files block Soul Gates until repair or Mortal resolution. The GM must not close realm-wrong files by inventing afterlife receipts, Mortal NPCs, world events, or manual notifications.
<!-- GM1557:A008:END -->
<!-- GM1557:A021:START -->
## Realm Mode Matrix

| Mode | When active | What GM may process | What GM must suppress |
|---|---|---|---|
| Unresolved realm fault | `currentRealm` is missing, null, or empty | No gameplay systems; preserve state and require authoritative `soul_state.currentRealm` repair | Do not infer Chaos Sea; do not run afterlife scheduler, pending contracts, Mortal World systems, or lifecycle handoffs |
| Ordinary `Chaos Sea` | `currentRealm = Chaos Sea`/`Море Хаоса`, no active Shining package handoff | Guardians, Abodes, Soul state, Soul Relics, direct Chaos Sea gacha, afterlife Ink Feather whitelist, Guardian projects, residents, archive, ordinary incarnation and ascension choices | Mortal World combat/NPC/location/faction/time systems; direct Shining owner-state writes to `shining_abode_state.json` |
| Ordinary active `Shining Abode` | `currentRealm = Shining Abode`, `shining_abode_state.json.availability = active`, and `preparedIncarnationPackage = null`/absent | Shining civic state, Shining factions, Shining trade, Shining core actions, Guardians/residents that persist into the Abode, afterlife scheduler | Mortal World systems, sealed Shining gameplay, pending-bootstrap handoff, and ordinary Chaos-only lifecycle shortcuts that do not apply in Shining |
| `Shining Abode pending-bootstrap handoff` | `currentRealm = Shining Abode`, `preparedIncarnationPackage` is a valid bootstrap package object, and Soul Gates found no unresolved afterlife pending/control contracts | Only `TriggerIncarnation` / `game_state/control/incarnation_trigger.json` for the frozen package; client performs Mortal bootstrap after acceptance | Ordinary Shining scheduler, Guardian/Abode roleplay, Shining core actions, trade, politics, package clearing/mutation, unrelated pending contract closure, GM-side Mortal bootstrap materialization |
| `Shining Abode package fault` | `currentRealm = Shining Abode` and `preparedIncarnationPackage` is present but not an object or fails package validation | No ordinary Shining gameplay; preserve state for repair | Do not infer ordinary active Shining, do not process Shining core actions, do not delete/null the package |

<!-- GM1557:A021:END -->
<!-- GM1557:A041:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A041:START -->
| `pending_npc_social_interactions.json` | MortalWorldProfile-only | Nothing in afterlife; preserve as wrong-realm repair-only context | None in afterlife. Runtime rejects NPC social pending contracts while `currentRealm` is `Chaos Sea` or `Shining Abode`; non-empty or malformed files block Soul Gates until repair or Mortal resolution. Mortal requests may include optional `topic`, which is answered only by a Mortal accepted turn. | Do not close it through `npcInteractionJournalUpdates`, `UpdateNPCs`, or any `game_state/npcs/*` surface during afterlife | Mortal docs |
<!-- GM1557:A041:END -->
<!-- GM1557:A042:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A042:START -->
| `pending_npc_trade_inventory_requests.json` / `[NPC_TRADE_REQUEST]` | MortalWorldProfile-only | Nothing in afterlife; preserve as wrong-realm repair-only context | None in afterlife. Runtime rejects NPC trade pending contracts while `currentRealm` is `Chaos Sea` or `Shining Abode`; non-empty or malformed files block Soul Gates until repair or Mortal resolution. | Do not materialize NPC trade stock, `UpdateNpcTradeInventoryReceipts`, or any `game_state/npcs/*` surface during afterlife | Mortal docs |
<!-- GM1557:A042:END -->
<!-- GM1557:A043:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A043:START -->
| `pending_resident_companion_manifestation_request.json` | MortalWorldProfile-only next-life companion manifestation | Nothing in afterlife; preserve valid non-empty files as next-life context | None in afterlife. Valid files can originate from afterlife resident rewards or imprint-bearing Soul Relics, closure happens later after Mortal bootstrap through source fields such as `sourceResidentId`, `sourceImprintId`, `sourceGuardianId`, `futureCompanionPrompt`, and `targetIncarnation`, and valid non-empty files do not block Soul Gates. Malformed files block Soul Gates until repair. | Do not materialize Mortal NPCs, encounters, `UpdateNPCs`, `NPCsInScene`, `worldEventsLog`, or companion quests while current realm is `Chaos Sea` or `Shining Abode`; malformed files are preserved for repair rather than "fixed" by an afterlife response | 22 |
<!-- GM1557:A043:END -->
<!-- GM1557:A047:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A047:START -->
| `game_state/control/archive_candidate_manifest.json` | Client-owned Life Evaluation / afterlife archive candidate manifest | No GM-authored output; read only for context/repair | Runtime validates JSON object shape, `sourceLife`, `lastExtractedAt`, and `candidates[]`; candidates may later become archive entries through client-controlled flows | Do not edit, clear, or treat as an afterlife pending contract; it is not closed by GM receipt | 22 |
<!-- GM1557:A047:END -->
<!-- GM1557:A048:CONTEXT -->
## Pending Contract Matrix

| Pending/control file | Valid mode | GM must write | Required closure proof | Forbidden/common mistake | Example |
|---|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A048:START -->
| `game_state/control/next_life_scenario_core.json` | Client-owned next-life bootstrap context generated/refreshed from `incarnation_world_setup.json` | No GM-authored output; read only when preparing or resolving the next Mortal bootstrap | `scenarioCoreAssertions[]` are hard facts for the next life and must not be contradicted; `candidateAssertions[]` are candidate-only until explicitly confirmed by later accepted state | Do not edit, clear, or close this file through a receipt; do not treat candidate assertions as canonical facts; do not let narrative override scenario core assertions | 18 |
| `game_state/world/guardian_corrections.json` | Client-owned current-Mortal-life correction journal created at bootstrap from Guardian correction resolution | No GM-authored afterlife output; read only in Mortal turns when present | It explains which compatible Guardian corrections were applied around `next_life_scenario_core.json`, including `claimants[]`, `contestedSlots[]`, `resolutionOrder[]`, and `corrections[]`; matching Abode Power spends use `guardianPowerEvents.reasonType=correction_spend` with `sourceSurface=guardian_corrections` | Do not create, edit, clear, or close it from Chaos Sea/Shining Abode; do not treat it as a pending request; do not overwrite scenario core facts through corrections | 22 |
<!-- GM1557:A048:END -->
<!-- GM1557:A061:CONTEXT -->
## PlayerAction Routing Tag Matrix

These tags may appear inside `input/turn_request.json.playerAction`. Treat them as routing markers for the contract named here; do not resolve them as generic prose.

| `playerAction` tag | Contract type | GM routing rule | Required/forbidden outputs | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A061:START -->
| `[GUARDIAN_PROVOCATION]` or `[GUARDIAN_PROVOCATION: guardianId]` | Legacy deterministic evidence tag for Guardian-forced incarnation, not a generic pending file | Only relevant if the GM writes `TriggerIncarnation.source=guardian_forced`; no-id tag is accepted as explicit provocation, id tag is accepted only when it equals the trigger `guardianId` and the current active Guardian's pre-turn `CurrentReputation <= -21` | Do not use this tag in Shining handoff or protected post-life turns; do not point it at one Guardian and force incarnation from another; conflict-resolved forced incarnation may instead use current-turn `recentConflicts[]` proof below | 6A, 15 |
<!-- GM1557:A061:END -->
<!-- GM1557:A067:START -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
| Ordinary `TriggerIncarnation` | Ordinary Chaos Sea with explicit player choice/prerequisites | `TriggerIncarnation` to `game_state/control/incarnation_trigger.json` | Requires validated accepted-turn authority from the current turn request, accepted `ready/turn_complete.json`, and `game_state/control/pending_turn_snapshot`; input-only snapshots are not authority. Do not write the trigger out-of-band or as manual repair. Do not switch `soul_state.currentRealm` to Mortal World in the same turn; client performs bootstrap | 6 |
| Guardian-forced `TriggerIncarnation` | Ordinary player-driven Chaos Sea turn with either explicit provocation against current active Guardian or current-turn resolved afterlife spiritual conflict proof. Legacy evidence is `[GUARDIAN_PROVOCATION]` or `[GUARDIAN_PROVOCATION: guardianId]`; conflict evidence is `recentConflicts[]` with matching `guardianId`, `resolvedAtTurn`, `operationType=force_incarnation`, and player loss/surrender/concession. Current active Guardian pre-turn `CurrentReputation <= -21` is still required in both paths | `TriggerIncarnation` with `source=guardian_forced`, guardian id, severity, reason, provocation summary, and normal world/character/circumstances | Requires validated accepted-turn authority from the current turn request, accepted `ready/turn_complete.json`, and `game_state/control/pending_turn_snapshot`; input-only snapshots are not authority. Harsh but survivable; severity mapping is `-21..-50 => harsh`, `-51..-100 => severe`; active Guardian, current Abode, reputation, and severity are read from the validated pre-turn guardian snapshot, not from same-turn `guardians.json` edits; never from Shining pending-bootstrap handoff; never when `afterlife_return_guard.json` still protects the first post-life turn | 6A, 15, 24 |
| Active `afterlife_return_guard.json` | Ordinary afterlife turn immediately after Life Evaluation returned the soul | Do not write or mutate this client-owned file; process ordinary afterlife narration/scheduler only | This guard protects at least one ordinary afterlife turn; while active, never write `TriggerIncarnation` with `source=guardian_forced`; malformed or wrong-reason guard state is fail-closed until validation repair or explicit client/runtime clear | 21 |
| `AscensionTrigger` / `game_state/control/ascension.json` | Chaos Sea only, ascension-ready Enlightenment (`enlightenment.experience` or `soulProgression.totalExperience >= 60`, or legacy max/Transcendence markers), explicit `playerChoice=Ascension`, and validated pre-turn Chaos Sea snapshot | `AscensionTrigger=true` and `playerChoice="Ascension"` only | `ascension.json` contains only the ascension trigger contract; the client performs the realm handoff after validation | Do not combine with `TriggerLifeEnd` / `life_transitions.json`, `TriggerIncarnation`, manual `soul_state.currentRealm` switch, Shining faction/trade state, Mortal bootstrap, or Mortal World files | 6B |
| Shining pending-bootstrap `TriggerIncarnation` | `Shining Abode` with non-null valid `preparedIncarnationPackage`, no unresolved afterlife pending/control contracts, and no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict` | `TriggerIncarnation` only | Requires validated accepted-turn authority from the current turn request, accepted `ready/turn_complete.json`, and `game_state/control/pending_turn_snapshot`; input-only snapshots are not authority. Preserve `preparedIncarnationPackage` exactly; runtime consumes/clears it after successful Mortal bootstrap. If any Guardian, resident, archive, offering, Source of Light (`pending_source_of_light_capstone.json`), Saref Wings search (`pending_saref_wings_infiltration.json`), active spiritual conflict, Shining core/trade/politics, malformed wrong-realm NPC, or legacy `pendingNativeFactionDiscovery` contract is still present, Soul Gates must block handoff until it is resolved/repaired; the GM must not combine that closure with the bootstrap turn. | 3, 18 |
| Client-owned `reenter_shining_abode` | Chaos Sea local route into an already active stored Shining Abode; malformed, wrong-reason, active, or otherwise blocking `afterlife_return_guard.json`, or `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`, blocks the route fail-closed | No GM-authored output; client re-enters locally without resetting ascension-local counters or refilling Light Sparks; runtime may sync return-cycle gacha charges and create a Shining trade auto-refresh request if inventory is stale | Do not synthesize Shining state, do not consume `preparedIncarnationPackage`, do not carry an active Chaos Sea conflict into Shining, and do not treat this as a new Shining bootstrap | covered by core docs |
| Client-owned `return_to_chaos_sea` / legacy alias `new_game_plus` | Ordinary active Shining Abode with no active or malformed Shining pending contracts, no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`, and `pendingNativeFactionDiscovery = null` | No GM-authored output; client seals the Abode, returns locally to Chaos Sea, resets Enlightenment/Просветление to baseline, and preserves Ink Feathers, Soul Relics, Guardians, Shining achievements, halls, factions, and Radiance progress | Non-empty `requests[]` or malformed Shining pending files block return and must be resolved/repaired; valid explicit-empty `{ "requests": [] }` pending files are stale local artifacts that runtime may delete; unresolved or malformed `pending_source_of_light_capstone.json`, unresolved or malformed `pending_saref_wings_infiltration.json`, and active spiritual conflicts block return until closed or repaired; do not use while `shining_abode_state.json.pendingNativeFactionDiscovery` is any non-null value, including malformed/non-object evidence; GM must not describe this as a destructive global reset or Ink Feather wipe | covered by core docs |
<!-- GM1557:A067:END -->
<!-- GM1557:A069:CONTEXT -->
## Lifecycle And Direct Action Matrix

| Action | Valid mode | GM must write | Required caution | Example |
|---|---|---|---|---|
<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A069:START -->
| `pending_source_of_light_capstone.json` / `/source_of_light` / `/источник_света` | Ordinary active Shining Abode only: `currentRealm = Shining Abode`, `availability = active`, `preparedIncarnationPackage = null`, full Radiance (`radiance.tier = 4`, `radiance.experience >= 580`), no active/malformed afterlife pending/control contract, no `game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict`, and no previous Source of Light reward | Author the Source of Light roleplay scene, mark `shining_abode_state.json.sourceOfLightCapstone.completed = true`, grant `soul_state.afterlifeCombatProfile.capstones.lightIncarnate` (`light_incarnate` / `Воплощение Света`), and add exactly one Soul Relic `source_of_light_incarnated_light` / `Воплощенный Свет` through canonical Soul Relic state | This is not a `pending_shining_abode_actions.json` core action and has no `coreActionReceipts[]`. The request is one direct object, not `requests[]`. The pending file blocks Soul Gates and `/return_to_chaos_sea` until closed or repaired. Active spiritual conflict must be closed through `afterlifeSpiritualConflictUpdate.mode=resolve` or `mode=repair_cancel` before Source of Light starts/closes. The closure is one-per-soul and the combat passive is trusted only when all three closure surfaces are present: completed Shining marker, soul passive, and exactly one matching Soul Relic. Do not duplicate `light_incarnate`, do not duplicate `source_of_light_incarnated_light`, and do not grant Mortal skills/items outside Soul Relics. `source_of_light_incarnated_light` must grant `+25` to every primary Mortal characteristic through `effects.characteristicBonuses`. Post-unlock contested dice audits must include explicit turn evidence and the `light_incarnate` modifier; pre-unlock or incomplete-closure modifiers are invalid. | 25 |
<!-- GM1557:A069:END -->
<!-- GM1557:A075:START -->
### TriggerIncarnation accepted-turn authority

`game_state/control/incarnation_trigger.json` is a lifecycle control file, not a free-standing repair or setup file. The GM may author it only inside a valid accepted afterlife turn whose `input/turn_request.json`, `ready/turn_complete.json`, validated `game_state/control/pending_turn_snapshot`, and client-owned `game_state/control/pending_turn_snapshot.authority.json` context still identify the same session, request, and turn. If that validated accepted-turn authority is missing, stale, unreadable, or mismatched, runtime raises/repairs through `incarnation_trigger_invalid_validated_snapshot_context` and ignores/deletes the trigger instead of starting Mortal bootstrap. Do not recreate the trigger later by hand; rerun the correct Chaos Sea or Shining pending-bootstrap turn. The GM must not author, repair, or edit `pending_turn_snapshot.authority.json`; it is a client-owned lifecycle integrity/tamper-evidence proof used only to explain lifecycle authority errors, not a security boundary.

<!-- GM1557:A075:END -->
<!-- GM1557:A080:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A080:START -->
- `next_life_scenario_core.json` is a client-owned bootstrap input, not a GM-authored pending contract. Read it before Mortal bootstrap or Shining handoff reasoning if present. Treat `scenarioCoreAssertions[]` as non-contradictable hard facts; treat `candidateAssertions[]` as candidate-only until later confirmation. Do not edit, clear, or close this file in an afterlife response.
<!-- GM1557:A080:END -->
<!-- GM1557:A089:CONTEXT -->
## Canonical State Reminders

<!-- GM1557:CONTEXT:END -->
<!-- GM1557:A089:START -->
- `preparedIncarnationPackage` is frozen state. During Shining handoff the GM preserves it; during `prepare_incarnation_package` the GM creates it with selected cards and draft version; only runtime clears it after successful bootstrap.
- Every `coreActionReceipts[]` entry that closes `pending_shining_abode_actions.json` must echo `quotedCostFeathers` and `quotedCostLightSparks` exactly from the request, including zero-cost actions. Forge receipts are stricter: `forge_relic.retune_property` echoes the exact `replacementProperty`, `forge_relic.uplift_rarity` echoes the exact `addedProperties`, and other forge actions must not invent those payload fields.
<!-- GM1557:A089:END -->
