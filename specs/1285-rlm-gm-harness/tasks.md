# Tasks: RLM-Inspired GM Harness

**Input**: `specs/1285-rlm-gm-harness/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`

## Phase 1: Setup

- [X] T001 Link this Spec Kit feature from `AGENTS.md` and `.specify/feature.json`.
- [X] T002 Add GitHub issue references #1285-#1290 to implementation commits and issue comments as work progresses.

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
- [ ] T024 [US5] Run a short live GM bridge test with `codex --dangerously-bypass-approvals-and-sandbox`.
- [ ] T025 [US5] Record findings as comments on #1285-#1290 or create follow-up issues for repeated harness gaps.

## Final Phase: Polish & Verification

- [ ] T026 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"`.
- [ ] T027 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"` if prompts/docs/examples changed.
- [ ] T028 Inspect `git diff --check` and final diffs against #1285-#1290 before committing.

## Dependencies

- T003 precedes the new live test because compact templates reduce noise and are already in progress under #1280.
- US1 precedes US2 and US5 because experience memory and rubric notes need ledger records.
- US3 can proceed after US1 foundations but should reuse context-pack paths identified during T005.
- US4 can be implemented after US1 records have a place for worker events.

## Suggested MVP

Complete #1280, then implement US1 and run a narrow live test. Add experience memory and safe probes after the ledger proves the evidence shape.
