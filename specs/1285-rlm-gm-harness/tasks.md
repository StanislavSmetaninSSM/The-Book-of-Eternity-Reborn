# Tasks: RLM-Inspired GM Harness

**Input**: `specs/1285-rlm-gm-harness/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`

## Phase 1: Setup

- [X] T001 Link this Spec Kit feature from `AGENTS.md` and `.specify/feature.json`.
- [X] T002 Add GitHub issue references #1249 and #1285-#1290 to implementation commits and issue comments as work progresses.

## Phase 2: Foundational

- [X] T003 Finish and verify #1280 compact turn/repair templates in `BookOfEternityClient/game_master_daemon.ps1` and `BookOfEternityClient.Tests/GmTurnHelperContractTests.cs`.
- [X] T004 [P] Review existing worker proposal and validation repair docs in `OtherGuides/GM_Worker_Bridges.md` and `Examples/example_validation_manifest.json` for references that the new ledger must preserve.
- [X] T005 [P] Identify current session context-pack output paths in `BookOfEternityClient/game_master_daemon.ps1` and related tests before adding new artifacts.

## Phase 3: User Story 1 - Live Turn Trajectory Ledger (P1)

**Goal**: Every live turn/repair path writes a compact structured trajectory record.

**Independent Test**: Simulate successful and repair turns and assert ledger records include identity, validation, repair, worker, rollback, timing, and rubric fields.

- [X] T006 [US1] Add failing tests for successful-turn trajectory emission in `BookOfEternityClient.Tests/GmTurnHelperContractTests.cs`.
- [X] T007 [US1] Add failing tests for repair-turn trajectory emission in `BookOfEternityClient.Tests/GmTurnHelperContractTests.cs`.
- [X] T008 [US1] Implement trajectory record creation in `BookOfEternityClient/game_master_daemon.ps1` or the existing harness owner selected by nearby code.
- [X] T009 [US1] Include validation, repair, worker, rollback, dispatch, and rubric fields without embedding giant prompts or secrets.
- [X] T010 [US1] Update GM-facing guidance if the ledger path or interpretation is exposed to the GM. Ledger is harness-owned and not exposed as GM instruction yet; no prompt/example update required for US1.

## Phase 4: User Story 2 - Compact Experience Memory (P2)

**Goal**: Prior trajectory lessons can be retrieved into future context packs.

**Independent Test**: Given mixed trajectory records, only relevant compact lessons are selected under the configured cap.

- [X] T011 [US2] Add failing tests for experience lesson relevance filtering and output caps in `BookOfEternityClient.Tests/GmTurnHelperContractTests.cs`.
- [X] T012 [US2] Implement compact lesson extraction and context-pack rendering in the existing context-pack generation flow.
- [X] T013 [US2] Add version/staleness fields so old template or contract advice does not silently override current validators.
- [X] T014 [US2] Update GM-facing prompt/docs so lessons are hints subordinate to validators and templates.

## Phase 5: User Story 3 - Safe GM Context-Probing Surface (P2)

**Goal**: The GM gets bounded context probes/summaries instead of raw implementation spelunking.

**Independent Test**: Generated context packs expose safe probes/templates and ordinary play prompts do not direct the GM to source code as default authority.

- [X] T015 [US3] Add failing tests for safe-probe/context-pack references and source-path avoidance in `BookOfEternityClient.Tests/GmBridgeDiagnosticsContractTests.cs`.
- [X] T016 [US3] Add generated safe-probe index or summaries for realm, pending contracts, validation issues, output templates, rollback status, and worker roles.
- [X] T017 [US3] Update daemon prompts to prefer safe probes, compact templates, and repair packets before implementation source.
- [X] T018 [US3] Update GM-facing docs/examples for Mortal World and afterlife if new probe guidance affects GM workflow. Generated context-pack README/manifest/directives were updated; no Mortal World or afterlife gameplay examples are required because this adds harness navigation guidance, not a GM-authored gameplay contract.

## Phase 6: User Story 4 - Recursive Worker Delegation Flow (P3)

**Goal**: Main GM can use hidden worker proposals as bounded RLM-like subcalls.

**Independent Test**: Simulate proposal-only and validation-repair worker events and assert no direct canonical writes occur.

- [X] T019 [US4] Add failing tests for worker delegation events appearing in the trajectory ledger.
- [X] T020 [US4] Ensure task packets include role, task type, context refs, allowed surfaces, schema, timeout, acceptance criteria, and forbidden actions.
- [X] T021 [US4] Ensure worker proposal receipt, rejection, apply, timeout, and validation outcomes are recorded in ledger records.
- [X] T022 [US4] Update `OtherGuides/GM_Worker_Bridges.md` with the delegation workflow and authority limits.

## Phase 7: User Story 5 - RLM-Inspired Live-Test Rubric (P3)

**Goal**: The next live test measures harness friction, not only final turn success.

**Independent Test**: Manual live run produces ledger records and rubric notes tied to follow-up issues/comments.

- [X] T023 [US5] Add a live-test checklist or generated context-pack note for the rubric dimensions from #1290.
- [X] T024 [US5] Run a short live GM bridge test with `codex -m gpt-5.5 -c model_reasoning_effort="high" --dangerously-bypass-approvals-and-sandbox`.
- [X] T025 [US5] Record findings as comments on #1285-#1290 or create follow-up issues for repeated harness gaps.

## Final Phase: Polish & Verification

- [X] T029 [Follow-up #1292] Prevent Codex bridge from marking `Ready=true` while Codex CLI is still booting MCP servers or showing `model: loading`.
- [X] T030 [Follow-up #1291] Make `start-bridge` prefer an existing `BookOfEternityGMBridge.exe` launch path so stale bridge processes do not force a rebuild and DLL copy.
- [X] T031 [Follow-up #1293] Add session-local daemon status/heartbeat, active-peer refusal, and daemon-timeout terminal artifact resolution before the next live test.
- [X] T032 [Follow-up #1282] Normalize actor reasoning block matching for harmless trailing punctuation and add regression coverage.
- [X] T033 [Follow-up #1281] Add GM helper preflight that blocks raw Mortal World profile mutations against the pending-turn snapshot before afterlife turn/repair completion.
- [X] T035 [Follow-up #1281] Compare pending-turn snapshot JSON semantically so formatting-only serialization differences do not trigger false wrong-realm mutation rollback during afterlife live tests.
- [X] T034 [Follow-up #1283] Repeat a Chaos Sea spiritual-conflict live turn and verify the compact `tempoAdvantage` template prevents `advantageId` / `sourceId` repair.
- [X] T036 [Follow-up #1249] Ignore realm-segregation false positives caused only by normalizer-added empty JSON containers in tracked files.
- [X] T037 [Follow-up #1249] Skip diagnostic-only validation repair requests in the daemon instead of dispatching them back to the GM bridge.
- [X] T038 [Follow-up #1249] Update the live runbook so Agent Console live tests launch the console client, main bridge, and `game_master_daemon.ps1`.
- [X] T039 [Follow-up #1249] Quote the daemon live-run launch command so repository paths with spaces do not break `Start-Process`.
- [X] T040 [Follow-up #1249] Fail closed client-owned diagnostic-only validation repair requests instead of waiting forever for `validation_repair_ready.json`.
- [X] T041 [Follow-up #1249] Preserve `validation_diagnostic_failure_report.json` after rollback and expose contract-error pause screens as Agent Console key input.
- [X] T042 [Follow-up #1249] Update the live runbook so disposable Agent Console runroots copy `system_guardians` next to `game_session`.
- [X] T043 [Follow-up #1249] Retest Chaos Sea abode travel with live Codex GM bridge and confirm diagnostic-only repair returns to a playable prompt instead of hanging.
- [X] T044 [Follow-up #1249] Project authorized `[CHAOS_SEA_TRAVEL]` target guardian/abode into guardian policy authority so discovered-abode travel is not rejected as stale active-guardian state.
- [X] T045 [Follow-up #1249] Ignore `.rollback.*` backup artifacts in the afterlife wrong-realm raw profile mutation scanner while still blocking real Mortal World profile mutations.
- [X] T046 [Follow-up #1249] Retest `/обители` Chaos Sea travel with the live Codex GM bridge and confirm accepted validation, updated active guardian, and a playable prompt.
- [X] T047 [Follow-up #1249/#1288] Treat a valid worker proposal written before worker timeout/nonzero exit as a proposal-received result, while still rejecting missing or invalid proposals.
- [X] T048 [Follow-up #1249/#1288] Make the worker CLI runner prompt include a self-contained `worker-proposal-v1` JSON skeleton and required-field rules before the next live worker delegation test.
- [X] T049 [Follow-up #1249/#1290] Run one short Mortal World console live gameplay turn through Agent Console and Codex GM after the RLM harness fixes, then record player-facing and harness-friction findings.
- [X] T050 [Follow-up #1249/#1290] Prevent rejected validation-repair trajectories from being promoted into "accepted fix" experience lessons.
- [X] T051 [Follow-up #1249/#1290] Mark ordinary turn trajectory validation as rejected when a correlated validation repair request exists after terminal success, instead of recording a false accepted turn.
- [X] T052 [Follow-up #1249/#1290] Retry transient runtime artifact deletes so accepted-turn cleanup does not crash when bridge/daemon briefly holds `input/turn_request.json`.
- [X] T053 [Follow-up #1249/#1290] Add a safe launcher-owned `start-daemon` action so live tests do not hand-roll fragile `Start-Process -File` commands for paths with spaces.
- [X] T054 [Follow-up #1249/#1290] Prevent bridge `ready`/diagnostics from reporting `Ready=true` while Codex CLI is still booting MCP servers after the trust prompt.
- [X] T055 [Follow-up #1249/#1290] Ensure validation repair loops write an accepted terminal trajectory record when the final repair clears validation and the client returns to a playable prompt.
- [X] T056 [Follow-up #1249/#1290] Allow Agent Console `/action` to select text-prompt choices so live-test agents do not need to parse and resubmit option text manually.
- [X] T057 [Follow-up #1249/#1290] Make RLM lessons for `mortal_relevant_actor_missing_persistence` actionable enough to prevent repeated NPC persistence repair loops during live tests.
- [X] T058 [Follow-up #1249/#1290] Add compact Mortal World NPC update template/repair guidance so GM-created NPC scene objects do not require multi-step schema repair loops.
- [X] T059 [Follow-up #1249/#1290] Route RLM lessons for NPC schema/location validation issues to `MORTAL_NPC_UPDATE_TEMPLATE.md` instead of generic validation or actor reasoning guidance.
- [X] T060 [Follow-up #1299] Make `shutdown-bridge` deterministic, session-local, idempotent, and machine-readable with a bounded fallback for unresponsive bridge processes.
- [X] T061 [Follow-up #1300] Reduce GM bridge not-ready delay after a safe Codex prompt returns, while preserving boot/trust/update/model-loading guards.
- [X] T062 [Follow-up #1218] Add a shared content-authoring worker contract with structured `authoringProposal`, a disabled inventory worker template, proposal-only validation, inbox diagnostics, launcher defaults, and GM-facing documentation/example coverage.
- [X] T063 [Follow-up #1219] Harden `inventory-content` proposals with player-facing item descriptions, owner/storage links, balance details, readable-content link checks for book/document items, and matching GM-facing documentation/examples.
- [X] T064 [Follow-up #1221] Add `skill-content` worker template, skill/effect proposal validation for detailed descriptions, localized scaling, bonus explanations, effect/status/combat links, and matching GM-facing documentation/examples.
- [X] T065 [Follow-up #1220] Add `npc-content` worker template, NPC dossier validation for separate public/private knowledge, thought journal, relationship hooks, personal quests, dialogue seeds, detail surfaces, required links, and matching GM-facing documentation/examples.
- [X] T066 [Follow-up #1302] Add compact Mortal NPC validation repair packets for same-turn location, debug-log current-location, full-object shape, relationship tier, cultural stance, and orphan journal/reference errors, with template refs, expected shape, safe correction rules, and GM-facing example guidance.
- [X] T067 [Follow-up #1304] Require accepted-turn flows to run full materialized-state validation after runtime normalization before returning to a playable prompt, and mark repair-ready ledger entries as correlated repair acceptance rather than full canonical acceptance.
- [X] T068 [Follow-up #1305] Add compact Mortal faction validation repair packets, context-pack template, and experience-lesson routing for unknown faction ids and invalid faction sidecars.
- [X] T069 [Follow-up #1306] Add compact Mortal location-transition repair packets, context-pack template, and experience-lesson routing for unknown current/NPC locations and duplicate same-turn map coordinates.
- [X] T070 [Follow-up #1308] Repair the local working `BookOfEternityClient/game_session` afterlife archive source-life drift, add a local-session validation smoke test, and guard tracked fixtures against future `afterlifeArchive.stored[]` entries without numeric `sourceLife`.
- [X] T071 [Follow-up #1309] Keep the GM daemon watcher alive after recoverable polling/status errors, persist explicit fatal diagnostics, document daemon repair-loop checks, and guard the lifecycle contract with tests.
- [X] T072 [Follow-up #1231] Add shared realm-aware afterlife worker task/proposal contract, Mortal-substitute guards, builder support, runner prompt guidance, and GM-facing documentation/example coverage.
- [X] T073 [Follow-up #1232] Add `guardian-abode-content` worker role, Guardian/Abode task/proposal contract, hidden-dossier and Mortal-substitute guards, disabled profile templates, runner guidance, and GM-facing documentation/example coverage.
- [X] T074 [Follow-up #1249/#1285] Pin main GM and hidden worker Codex launch commands to explicit `gpt-5.5 high`, update GM-facing examples, and verify with a live Chaos Sea turn.
- [X] T075 [Follow-up #1249/#1285] Make fresh New Game startup pass through Agent Console/redirected output without Spectre selection-prompt failure, then rerun a full new-game live GM test from main menu.
- [X] T076 [Follow-up #1249/#1285] Reject or repair stale `pendingGuardianCreation` after fresh Guardian materialization so `/хранители` does not report a new Guardian is still pending after accepted bootstrap.
- [X] T077 [Follow-up #1249/#1285] Publish New Game text prompts (soul name/form and freeform Guardian description) into Agent Console snapshots so live-test agents do not have to read redirected stdout while the API input queue is already waiting for text.
- [X] T078 [Follow-up #1249/#1285] Allow locally staged afterlife social requests to validate before the pending-turn snapshot is created, while accepted-turn resolution still requires a validated snapshot context.
- [X] T079 [Follow-up #1249/#1285] Make fresh New Game bootstrap create an explicit guardian-project rollback/baseline authority before dispatch so accepted first turns cannot fail canonical materialization with missing tracker/guardian backups.
- [X] T080 [Follow-up #1249/#1285] Publish all setup/key-driven screens into Agent Console snapshots: incarnation character/world/circumstances prompts, press-any-key interstitials, and stat allocation state, so live-test agents never need redirected stdout to continue New Game.
- [X] T081 [Follow-up #1249/#1285] Add a hard Mortal World bootstrap harness/scaffold so fresh incarnation starts with valid location, map, faction, lore bootstrap, required empty arrays/objects, stable ids, and no multi-repair loop before the first playable mortal prompt.
- [X] T082 [Follow-up #1249/#1285] Make fresh Mortal World bootstrap materialize player-facing scene anchors as canonical entities when they are actionable: starting item/prop, initial investigation quest or objective, known exits/map links, and faction hooks.
- [X] T083 [Follow-up #1249/#1285] Polish fresh New Game player-facing console output after live-test findings: hide raw `/incarnate` JSON contract dumps, avoid empty status fields, and make `/help` navigable when the command list is long.
- [X] T084 [Follow-up #1249/#1285] Let a truly empty fresh `game_session` reach the New Game main menu without requiring `soul_state.currentRealm`, while keeping real turn-control/progression calls fail-closed until a realm exists.
- [X] T085 [Follow-up #1249/#1285] Preserve session-local GM bridge runtime status across New Game state resets so the daemon does not lose the hidden bridge before dispatching the first turn.
- [X] T086 [Follow-up #1249/#1285] Treat a lower visible Codex idle prompt as ready even when stale `Working` text remains above it, so bridge dispatch does not stall after long repair loops.
- [X] T087 [Follow-up #1249/#1285] Materialize selected system Guardian presets into a complete client-owned canonical fresh New Game guardian root before the first GM turn, so the GM narrates the meeting instead of repairing guardian schema from scratch.
- [X] T088 [Follow-up #1249/#1285] Materialize mandatory fresh Chaos Sea lore/meta bootstrap files before the first GM turn, so the GM narrates from existing lore authority instead of repairing missing `character_chronicle.json`, `lore/chaos_sea/*`, and empty `codex_entries.json`.
- [X] T089 [Follow-up #1249/#1285] Detect a Codex GM bridge returning to the idle prompt after writing turn output but without `ready/turn_complete.json` or `ready/turn_error.json`, emit a correlated daemon terminal error, and record the RLM harness failure instead of waiting for the full turn timeout.
- [X] T090 [Follow-up #1249/#1285] Bound the daemon's GM bridge dispatch phase when the ConPTY named pipe is unreachable, emit `gm_bridge_dispatch_unavailable` as a correlated terminal error, and record the harness failure instead of retrying dispatch forever before terminal wait begins.
- [X] T091 [Follow-up #1249/#1285] Make Mortal NPC full-object repair packets preserve the validator's exact missing fields and include safe minimal shapes for fields such as `inventory`, so live repairs do not repeat broad schema advice without fixing the concrete error.
- [X] T092 [Follow-up #1249/#1285] Add a compact Mortal bootstrap materialization repair packet for first-life errors across current-world codex, faction sidecars, readable documents, starting items, location coordinates/control, and scaffold reuse so the GM receives one executable bootstrap checklist instead of unrelated generic validation errors.
- [X] T093 [Live-test blocker #1249/#1285] Fix the new-game Agent Console flow after choosing an eternal/system Guardian so the client publishes the next selectable prompt instead of leaving the previous screen closed with `awaitingInput=false` and no GM turn request.
- [X] T094 [Live-test follow-up #1249/#1285] Extend Mortal bootstrap repair packets to cover complete starting-item canonical shape, item slot enums, string-array fields, and Mortal relevant-actor persistence errors observed during fresh New Game live testing.
- [X] T095 [RLM ledger follow-up #1249/#1285] Make GM trajectory ledger records preserve validation repair packet kinds from the correlated repair request so live-test analysis can distinguish generic repairs from harness-guided repairs.
- [X] T096 [Live-test follow-up #1249/#1285/#1290] Preserve generated `game_state/control/gm_context_pack/**` JSON artifacts across New Game game-state reset so the GM does not receive a partially deleted RLM context pack after daemon startup.
- [X] T097 [Live-test follow-up #1249/#1285/#1290] Backfill `gm_live_test_notes.jsonl` entries with matching trajectory `recordId` after ledger emission when the GM had to write the note before the record existed.
- [X] T098 [Live-test follow-up #1249/#1285/#1290] Align Mortal NPC location/inventory repair packets and compact NPC guidance with validator semantics for known current-scene locations vs same-turn new locations, so fresh Mortal bootstrap does not oscillate between `npc_scene_missing_current_location_id`, `npc_same_turn_initial_location_requires_null_current_location`, and `npc_existing_inventory_resend_forbidden`.
- [X] T099 [Live-test follow-up #1249/#1285/#1290] Record a terminal accepted trajectory after a validation repair clears full canonical validation and returns to a playable prompt, so RLM ledger analysis is not left with only `fullCanonicalStateAccepted=false` correlated repair-ready records.
- [X] T100 [Live-test follow-up #1249/#1285/#1290] Prevent manual `ready` bridge commands from setting `Ready=true` while Codex diagnostics still show boot/trust/update/model-loading state; return a machine-readable not-ready reason instead.
- [X] T101 [Live-test follow-up #1249/#1285/#1290] Publish Agent Console waiting snapshots while a GM turn or validation repair is in progress, instead of leaving the previous screen closed with `awaitingInput=false`.
- [X] T102 [Live-test follow-up #1249/#1285/#1290] Make first Mortal bootstrap repair packets cover `missing_actor_current_location` in the initial actionable repair request, so fresh incarnation does not require a second repair turn for actor current-location reasoning.
- [X] T103 [Live-test follow-up #1249/#1285/#1290] Fix inflated daemon status/heartbeat turn counters such as `Status: 20 turns, 0 errors` during short live tests.
- [X] T104 [Live-test follow-up #1249/#1285/#1290] Publish Guardian mode and system preset setup screens as explicit menu-selection snapshots, not text prompts with hidden selectable actions.
- [X] T105 [Live-test follow-up #1249/#1285/#1290] Publish an Agent Console transition/loading snapshot immediately after game-loop text/action input is consumed and before the turn request/context pack is prepared, so live-test agents do not see a stale non-awaiting game-loop screen during GM dispatch preparation.
- [X] T106 [Live-test follow-up #1249/#1285/#1290] Persist or reconstruct last dialogue options after client restart/continue so a resumed live New Game does not lose player-facing choices and force freeform input.
- [X] T107 [Live-test follow-up #1249/#1285/#1290] Publish an Agent Console GM-waiting snapshot on the ordinary ProcessPlayerTurn path before its direct terminal-signal wait, so live tests can distinguish request preparation from active GM processing.
- [X] T108 [Live-test follow-up #1249/#1285/#1290] Preserve compact validation repair diagnostics in the trajectory ledger or live-test notes after successful cleanup, including concrete validation codes and repair packet kind, so RLM analysis can explain why a turn needed repair.
- [X] T109 [Live-test follow-up #1249/#1285/#1290] Stop pre-turn canonical faction validation from treating `game_state/factions/faction_core.json.factions[]` entries as unknown full-object GM deltas when their permanent `factionId` is present in the same canonical core file.
- [X] T110 [Live-test follow-up #1249/#1285/#1290] Tighten Mortal NPC harness guidance so visible, speaking, acting, or directly addressed role-identifiable scene actors are materialized through `MORTAL_NPC_UPDATE_TEMPLATE.md` even before the GM knows their personal name, instead of hiding them under faction/quest/location state only.
- [X] T111 [Live-test follow-up #1249/#1285/#1290] Add compact Mortal faction resource repair packets for `canonical_faction_resource_entry_missing_required_fields` and related resource-entry shape errors observed when the GM materializes a new creditor faction during fresh Mortal live tests.
- [X] T112 [Live-test follow-up #1249/#1285/#1290] Make Agent Console menu-selection actions activate the requested item in one API call, so autonomous live tests cannot accidentally select a menu item and then submit a later game-loop action with a stale repeated `option-*` request.
- [X] T113 [Live-test follow-up #1249/#1285/#1290] Add compact Mortal world-map adjacency repair packets for `world_map_adjacency_unknown_target` and related unknown-location link errors, so the GM can either materialize the target location or remove/downgrade the link instead of receiving only unrelated NPC-location repair guidance.
- [X] T114 [Live-test follow-up #1249/#1285/#1290] Add compact Mortal NPC scope repair packets for `structured_npc_update_out_of_scope`, so the GM repairs the declared Relevant actors/reasoning coverage or removes the unauthorized structured NPC update instead of receiving only NPC-location guidance.
- [X] T115 [Live-test follow-up #1249/#1285/#1290] Inject compact relevant RLM experience lessons directly into ordinary GM turn prompts, so repeated validation patterns such as `structured_npc_update_out_of_scope` are visible before the GM writes the next turn instead of only as an optional context-pack file.
- [X] T116 [Live-test follow-up #1249/#1285/#1290] Fix console NPC list identity handling so canonical Mortal NPCs with `NPCId: null` are not deduplicated into a single `id:null` entry, and `/нпс` remains useful during fresh New Game live tests.
- [X] T117 [Console polish follow-up #1249/#1285/#1290] Clean Mortal NPC detail panels after live-test findings: localize rarity/progression/companion directive values, hide or translate technical ids/status fields, and keep debug-only raw fields out of normal player-facing `/нпс` output.
- [X] T118 [Live-test follow-up #1249/#1285/#1290] Make Agent Console menu-selection actions beyond the first nine options activate safely through explicit menu `inputValue` digits or fallback navigation plus Enter, so New Game Guardian preset actions like option 10-12 do not fail with `unsupportedActionResolution` or select the wrong neighbor.
- [X] T119 [Live-test follow-up #1249/#1285/#1290] Align Mortal bootstrap scaffold NPC core top-level guidance with the validator's `game_state/npcs/npc_core.json` contract, so fresh Mortal World GM turns are not told to write `NPCJournals`, `NPCQuestUpdates`, or `NPCRelationshipUpdates` into npc_core and then repaired for following scaffold advice.
- [X] T120 [Console polish follow-up #1249/#1285/#1290] Clean fresh Mortal command projections found during live testing: localize inventory item type/quality/slot and quest status values, hide raw item bookkeeping fields such as `value`, `currentLocationId`, `isCarried`, `isEquipped`, `visibility`, avoid misleading inventory actions for non-ordinary quest objects, and prevent NPC nested detail sections from reintroducing semicolon-packed raw values.
- [X] T121 [Live-test harness follow-up #1249/#1285/#1290] Add an Agent Console live-test driver for local command screens: safely return to `game-loop` through intermediate key/menu screens without label-guessing in autonomous live tests.
- [X] T122 [Live-test harness follow-up #1249/#1285/#1290] Expose enough `/карта` state in Agent Console snapshots for autonomous tests to verify map location selection instead of seeing only "Локальная карта открыта".
- [X] T123 [Console polish live-test follow-up #1249/#1285/#1290] Fix fresh Chaos Sea `/статус` wording so an unopened Shining Abode is described as not yet opened/available, not "missing or damaged" data.
- [X] T124 [Live-test harness follow-up #1249/#1285/#1290] Prevent repeated first-Mortal `missing_actor_current_location` repairs by putting the required current-location actor line into the compact turn/actor reasoning templates that the GM reads before ordinary turns.
- [X] T125 [Live-test harness follow-up #1249/#1285/#1290] Add a compact afterlife chronicle string-array repair packet for `persistentConsequences[]` and `openThreads[]` shape errors, so first Chaos Sea bootstrap repairs receive an executable file/field checklist instead of generic validation advice.
- [X] T126 [Live-test harness follow-up #1249/#1285/#1290] Add a compact afterlife chronicle authoring template and route ordinary afterlife turns through it, so significant Guardian meetings/dialogue persist useful external memory instead of remaining only in transient prose.
- [X] T127 [Console live-test follow-up #1249/#1285/#1290] Make afterlife external memory discoverable from normal player command hints/help, so a player who sees `/архив_души` is not left thinking the newly written afterlife chronicle is missing.
- [X] T128 [RLM harness live-test follow-up #1312/#1249/#1285/#1290] Preserve concrete validation repair diagnostics after accepted repair cleanup, so RLM/live-test analysis can recover original validation codes, affected files/fields, and harness repair packet kinds without deleted transient repair files.
- [X] T129 [Console live-test follow-up #1313/#1249/#1285/#1290] Make `/инв` item details distinguish carried backpack items from known current-location clues, avoiding backpack status/actions for `isCarried=false` scene items.
- [X] T130 [Agent Console live-test follow-up #1314/#1249/#1285/#1290] Preserve the accepted action `inputKind` in Agent Console `inputAccepted` acknowledgement events instead of reporting menu actions as generic key input.
- [X] T131 [Console live-test follow-up #1315/#1249/#1285/#1290] Make inventory item durability render a clean single value when `maxDurability` is absent, with no dangling slash in player-facing output.
- [X] T132 [RLM harness live-test follow-up #1316/#1249/#1285/#1290] Reject technical turn-anchor prefixes in player-facing inventory item `textContent` and item text append surfaces, so GM repairs remove `#[turn]` markers instead of leaking them into `/инв`.
- [X] T133 [Console live-test follow-up #1317/#1249/#1285/#1290] Hide canonical quest `detailsLog` turn anchors from player-facing `/квесты` output while preserving the canonical anchored state contract.
- [X] T134 [Console live-test follow-up #1318/#1249/#1285/#1290] Hide canonical `lastEventsDescription` turn anchors from player-facing location outputs such as `/где_я` while preserving the canonical anchored state contract.
- [X] T135 [Agent Console live-test follow-up #1319/#1249/#1285/#1290] Make `return-to-game-loop-step` choose nested local-command close actions such as `← Закрыть разделы НПС` / `← Закрыть разделы фракции` instead of leaving autonomous tests stuck in section menus.
- [X] T136 [Agent Console live-test follow-up #1320/#1249/#1285/#1290] Make the `/help` root section menu expose a safe close action so `return-to-game-loop-step` can recover autonomous live tests from help selection screens.
- [X] T137 [Agent Console live-test follow-up #1321/#1249/#1285/#1290] Make `return-to-game-loop-step` avoid false close-action matches inside ordinary player-facing words such as `закрытого`, and keep deterministic menu index input for real back/close actions.
- [X] T138 [Console polish live-test follow-up #1322/#1249/#1285/#1290] Localize player-facing Guardian Corrections and location chooser values so `/коррективы_хранителя` and `/локации` do not leak raw enum/category labels such as `none`, `identity_anchor`, `Slot`, or `indoor`.
- [X] T139 [Harness/RLM live-test follow-up #1324/#1285/#1303] Normalize string `output/interface_updates.json.dialogueOptions[]` entries into canonical dialogue option objects so harmless GM shape drift does not require a validation repair loop.
- [X] T140 [Harness/RLM live-test follow-up #1325/#1285/#1303] Surface GM turn timeout as an explicit Agent Console/player recovery state instead of silently returning to an indistinguishable prior game loop.
- [X] T141 [Harness/RLM live-test follow-up #1326/#1285/#1303] Preserve the Agent Console two-step menu action contract so ordinary action requests select first and only activate already selected/default entries, while explicit return-to-game-loop helpers may still unwind in one step.
- [X] T142 [Harness/RLM live-test follow-up #1327/#1285/#1303] Prevent timed-out GM workers from writing late output into the active session by killing or isolating stale worker process trees and recording cleanup evidence.
- [X] T143 [Harness/RLM live-test follow-up #1328/#1285/#1303] Normalize missing canonical afterlife chronicle `eventDescriptions[]` archive arrays to an empty client-owned array, so direct canonical chronicle writes do not require a validation repair loop for a read-only field.
- [X] T144 [Harness/RLM live-test follow-up #1329/#1285/#1303] Preserve existing afterlife chronicle archive entries and archive the previous `lastEventsDescription` when a direct canonical chronicle write replaces the same `chronicleId`.
- [X] T145 [Harness/RLM live-test follow-up #1330/#1285/#1303] Add compact validation repair packets for missing accepted-turn output artifacts such as `output/narrative_response.json` and `output/debug_logs.json.gm_thoughts_markdown`, so terminal-success protocol repairs get executable harness guidance.
- [X] T146 [Console polish/harness live-test follow-up #1331/#1285/#1290] Prevent afterlife chronicles from leaking internal English realm terms or raw chronicle ids in normal player-facing output, with validation/repair guidance and console projection coverage.
- [X] T147 [Harness/RLM live-test follow-up #1333/#1285/#1303] Prevent first Mortal bootstrap from causing NPCsInScene location mismatch and player self relevant-actor persistence repairs by tightening scaffold/compact guidance and adding regression coverage.
- [X] T148 [Harness/RLM live-test follow-up #1334/#1285/#1303] Prevent first Mortal bootstrap from causing item durability percentage-string repairs by aligning scaffold, context-pack lessons, repair packets, docs/examples, and regression coverage.
- [X] T149 [Harness/RLM live-test follow-up #1335/#1285/#1303] Detect GM artifact-writing stalls before the full turn timeout, preserving diagnostics and surfacing a retryable Agent Console recovery state.
- [X] T150 [Harness/RLM live-test follow-up #1336/#1285/#1303] Prevent Mortal item `journalEntries` object-entry repairs by documenting the string-array contract in context-pack lessons, repair packets, GM-facing docs/examples, and regression coverage.
- [X] T151 [Harness/RLM live-test follow-up #1341/#1285/#1290] Prevent current-turn afterlife conflict dice authority from forcing repair of prior accepted `exchangeLog[]` entries, preserving historical dice while keeping current/new exchanges strict.
- [X] T152 [Harness/RLM live-test follow-up #1340/#1285/#1290] Add a client-owned `afterlifeSpiritualConflictPreview` turn-request surface with action-cost authority and first opposed dice outcome preview, so GM turns stop guessing art tiers and outcome bands.
- [X] T153 [Harness/RLM live-test follow-up #1342/#1285/#1290] Prevent prepared live turns from exposing stale `pending_dice_state.json` as a competing current-turn dice authority; `turn_request.json.preGeneratedDices1d20` is the only prepared-turn dice source.
- [X] T154 [Console polish live-test follow-up #1344/#1285/#1290] Remove implementation wording such as `client bootstrap baseline` from fresh Mortal bootstrap quest data and guard it with regression coverage.
- [X] T155 [Console polish/RLM live-test follow-up #1316/#1285/#1290] Extend the inventory turn-anchor guard from readable item text to item journal entries and sidecar item journals, and strip legacy `#[turn]` prefixes from `/инв` item journal rendering.
- [X] T156 [Harness/RLM live-test follow-up #1343/#1285/#1290] Prevent first incarnation bootstrap repair patterns by classifying narrative timestamp errors as accepted-turn artifact repairs and adding pre-turn compact-template guidance for timestamps, NPC Scope, progression counts, and localized afterlife realm terms.
- [X] T157 [Harness/RLM live-test follow-up #1345/#1285/#1290] Prevent first Chaos Sea system Guardian bootstrap from rewriting the client-owned Guardian mirror by blocking unauthorized `guardians.json` changes in the GM turn helper and documenting the afterlife chronicle route for first-meeting memory.
- [X] T158 [Harness/RLM live-test follow-up #1343/#1285/#1290] Prevent first Mortal bootstrap coordinate repair by publishing canonical coordinate authority in `mortal_bootstrap_scaffold.json`, compact daemon guidance, and GM examples for `current_location_coordinates_mismatch`.
- [X] T159 [Harness/RLM live-test follow-up #1343/#1285/#1290] Normalize internal English realm terms in player-facing afterlife chronicle text before validation, so deterministic `Mortal World` / `afterlife` slips do not require a repair loop while GM-facing guidance still requires Russian in-world terms.
- [X] T160 [Harness/RLM live-test follow-up #1343/#1285/#1290] Normalize useful `output/interface_updates.json` payloads by adding a missing ISO timestamp before accepted-turn validation, without allowing timestamp-only interface stubs.
- [X] T161 [Harness/RLM live-test follow-up #1316/#1343/#1285/#1290] Strip legacy technical `#[turn]` prefixes from player-facing inventory item journal entries and sidecar item journals, and remove the bad GM harness example that taught the prefix.
- [X] T162 [Harness/RLM live-test follow-up #1343/#1285/#1290] Treat explicit player-character parenthetical actor annotations such as `(персонаж игрока)` as player scope during Mortal reasoning validation, not missing NPC persistence.
- [X] T163 [Harness/RLM live-test follow-up #1343/#1285/#1290] Treat standalone `player character` as a Mortal player-scope marker and accept compact English `Situation`/`Thoughts`/`Actions` actor reasoning labels, matching the GM helper wording observed during fresh New Game live testing.
- [X] T164 [Harness/RLM live-test follow-up #1346/#1285/#1290] Add a race-safe Agent Console `default-action` endpoint so autonomous live-test drivers can accept the latest enabled default action without stale `screenId` mismatches.
- [X] T165 [Console polish live-test follow-up #1347/#1249/#1285/#1290] Keep default Chaos Sea player screens on Russian command hints and remove audit/GM-only wording from ordinary `/статус` and `/хранители`, while preserving explicit `/status audit` diagnostics.
- [X] T166 [Console polish live-test follow-up #1348/#1249/#1285/#1290] Clean remaining ordinary afterlife command leaks found by live testing: localize `/перья` realm phase, replace missing-file output in `/политика_хранителей`, and use the Russian `/хроники_посмертия` alias in chronicle detail actions.
- [X] T167 [Harness/RLM live-test follow-up #1343/#1285/#1290] Prevent first Mortal bootstrap location/map shape repairs by routing missing location arrays, difficulty profiles, and map link preview fields into executable location-transition repair packets and GM-facing compact templates.
- [X] T170 [Harness/RLM live-test follow-up #1343/#1285/#1290] Route world-map adjacency/link/storage/threat unknown-target lessons to the Mortal location template so future turns see the fully-materialized target rule before writing map links.
- [X] T171 [Harness/RLM live-test follow-up #1351/#1349/#1285/#1290] Add a Mortal skill progression compact template and GM-facing guidance so training, learned skills, active skill use, and mastery updates do not remain prose-only.
- [X] T172 [Harness/RLM live-test follow-up #1352/#1349/#1285/#1290] Make fresh Mortal bootstrap create `game_state/player/experience.json` and add compact Mortal XP/level guidance so combat rewards, level thresholds, and stat-allocation tests do not remain prose-only.
- [X] T173 [Harness/RLM live-test follow-up #1353/#1349/#1285/#1290] Add Mortal combat materialization validation, repair packet, compact combat state template, and GM-facing guidance so explicit Mortal combat with XP/mastery/status changes leaves `/бой` useful instead of resolving only in prose.
- [X] T174 [Harness/RLM live-test follow-up #1354/#1349/#1285/#1290] Persist a restart-safe Mortal level-up stat-point awarded-through marker and refresh computed characteristics after local stat allocation so live-test restarts cannot duplicate stat awards.
- [X] T175 [Harness/RLM live-test follow-up #1356/#1349/#1285/#1290] Reject Mortal accepted turns whose XP crosses `experienceForNextLevel` without materialized `playerLevel`/`level` and next-threshold advancement, while preserving client-owned stat-point marker authority.
- [X] T176 [Harness/RLM live-test blocker #1358/#1350/#1285/#1290] Snapshot client-owned current-world lore from rollback backups during Life Evaluation / Chaos Sea transitions so missing baseline evidence does not produce impossible GM repair loops.
- [X] T177 [Harness/RLM live-test blocker #1359/#1350/#1285/#1290] Publish Explorer local confirmation prompts as Agent Console confirmation snapshots so autonomous tests can confirm client-owned spiritual art upgrades without hidden terminal `[y/n]` prompts.
- [X] T178 [Harness/RLM live-test blocker #1360/#1350/#1285/#1290] Keep `player_soul.currencies` synchronized with authoritative local currency state after client-owned special spiritual art upgrades.
- [X] T179 [Harness/RLM live-test blocker #1362/#1350/#1285/#1290] Publish Explorer freeform `Ask` prompts as Agent Console text snapshots so commands such as `/духовное_действие` can accept autonomous text input without stale snapshots.
- [X] T180 [Harness/RLM live-test follow-up #1361/#1350/#1285/#1290] Add compact afterlife repair packets and RLM lessons for spiritual-conflict reward eligibility and entity-profile special-art scaffold failures.
- [X] T181 [Harness/RLM live-test follow-up #1396/#1285/#1290] Add a reusable Agent Console read-only command sweep helper that submits commands only from `game-loop` text prompts, unwinds through `return-to-game-loop-step`, records forbidden-marker artifacts, and fails closed on `turn-preparing` or unsafe text prompts.
- [X] T182 [Harness/RLM live-test blocker #1400/#1285/#1290] Prevent first Mortal bootstrap idle-with-no-output failures by adding a first-Mortal output checklist to the daemon prompt, selecting no-output idle terminal failures as RLM lessons, and updating the GM example guidance.
- [X] T168 [Live-test coverage #1349/#1285/#1290] Run and record Mortal World combat, skill mastery/progression, level-up, and stat-allocation coverage through Agent Console with Codex GM bridge.
- [X] T169 [Live-test coverage #1350/#1340/#1285/#1290] Run and record afterlife spiritual combat, spiritual art learning, and spiritual art mastery/progression coverage through Agent Console with Codex GM bridge.
- [X] T183 [Harness/RLM live-test follow-up #1419/#1350/#1285/#1290] Add machine-readable `exactFieldCorrections[]` to afterlife spiritual conflict action-cost repair packets and daemon repair guidance, so GM repairs apply concrete path -> expected values before recomputing dependent ОД fields.
- [X] T184 [Harness/RLM live-test follow-up #1420/#1350/#1285/#1290] Reject stale player-facing `narrative_response` / `interface_updates` artifacts after a canonical validation repair, so repaired state cannot return to the player with contradictory prose or options.
- [X] T185 [Harness/RLM live-test follow-up #1434/#1285/#1290] Publish validation repair progress in Agent Console snapshots, including turn, attempt, issue summary, and machine-readable diagnostics while preserving existing trajectory ledger postmortems.
- [X] T186 [Harness/RLM live-test follow-up #1435/#1285/#1290] Detect GM payload files written without a correlated terminal ready signal, emit a bounded daemon terminal error with changed-file diagnostics, and document the new `gm_output_without_terminal_signal` RLM pattern for future turns.
- [X] T187 [Harness/RLM live-test follow-up #1436/#1285/#1290] Add output-only accepted-turn repair template, route stale player-facing output repairs to it, and detect validation repair artifact stalls without target-file progress.
- [X] T188 [Harness/RLM live-test blocker #1443/#1285/#1290] Add a dedicated Guardian trade request resolution repair packet/template guidance so `pending_guardian_trade_request.json` repairs materialize `guardian.tradeInventory` and receipts instead of stalling in generic Guardian scope repair.
- [X] T189 [Harness/RLM live-test blocker #1444/#1285/#1290] Add a dedicated startup Guardian creation repair packet/template guidance so freeform `pendingGuardianCreation` materializes through the supported Guardian create surface instead of leaving `/хранители` empty.
- [X] T190 [Harness/RLM live-test blocker #1444/#1285/#1290] Reject accepted New Game startup turns that leave freeform `pendingGuardianCreation` unresolved with no `activeGuardian`/`guardians[]`, so the GM cannot pass validation by retreating to pending-only state.
- [X] T191 [Harness/RLM live-test blocker #1444/#1285/#1290] Make the startup Guardian creation repair packet spell out the exact `UpdateGuardians[{ command=create, data=<full guardian> }]` authority surface so repeated repairs stop editing only materialized mirrors.
- [X] T192 [Harness/RLM live-test blocker #1444/#1285/#1290] Treat `activeGuardian` id-only mirrors as safely reconstructable when a valid same-turn `UpdateGuardians.create.data` and matching `guardians[]` authority exist, preventing startup Guardian repair loops over a derived mirror field.
- [X] T193 [Harness/RLM live-test follow-up #1446/#1349/#1285/#1290] Materialize fresh Mortal starter competencies from explicit player character concepts and keep early skill checks from remaining prose-only during live tests.
- [X] T194 [Harness/RLM live-test blocker #1447/#1285/#1290] Publish a terminal Agent Console recovery state when validation repair artifact stall cleanup stops the GM bridge, instead of leaving autonomous tests on a non-interactive repair-progress screen.
- [X] T195 [Harness/RLM live-test blocker #1448/#1349/#1285/#1290] Split or constrain first Mortal World entry so it cannot stall as one oversized GM turn with no output, preserving starter skill/bootstrap coverage for autonomous live tests.
- [X] T196 [Harness/RLM live-test blocker #1449/#1349/#1285/#1290] Prevent freeform Guardian startup from entering repeated materialized-mirror repair loops by making the canonical create surface exact, bounded, and hard to misuse.
- [X] T026 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"`.
- [X] T027 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"` if prompts/docs/examples changed.
- [X] T028 Inspect `git diff --check` and final diffs against #1285-#1290 before committing.

## Dependencies

- T003 precedes the new live test because compact templates reduce noise and are already in progress under #1280.
- US1 precedes US2 and US5 because experience memory and rubric notes need ledger records.
- US3 can proceed after US1 foundations but should reuse context-pack paths identified during T005.
- US4 can be implemented after US1 records have a place for worker events.

## Suggested MVP

Complete #1280, then implement US1 and run a narrow live test. Add experience memory and safe probes after the ledger proves the evidence shape.
