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
- [X] T024 [US5] Run a short live GM bridge test with `codex --dangerously-bypass-approvals-and-sandbox`.
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
