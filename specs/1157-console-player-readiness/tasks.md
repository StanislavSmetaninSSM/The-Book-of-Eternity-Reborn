# Tasks: Console Client Live Player-Readiness Pass

**Input**: Design documents from `specs/1157-console-player-readiness/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/console-live-playtest-contract.md`, `quickstart.md`

**Source GitHub issue**: #1157 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1157

**Tests**: Behavior fixes require test-first coverage unless the defect is documented as live-only.

**Organization**: Tasks are grouped by independently testable user story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm context, branch, and verification baseline before running live tests.

- [x] T001 Confirm source issue #1157, `git status --short`, current branch, and active Spec Kit path in `.specify/feature.json`
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/1157-console-player-readiness/spec.md`, `plan.md`, `contracts/console-live-playtest-contract.md`, and `docs/superpowers/plans/2026-06-09-console-e2e-codex-gm-playtest.md`
- [ ] T003 [P] Run baseline console build from `BookOfEternityClient/BookOfEternityClient.csproj`
- [ ] T004 [P] Run focused Agent Console tests from `BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj`
- [ ] T005 Record baseline command outputs and run metadata in the #1157 issue comments or the live run summary artifact

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Prepare a disposable, reproducible run without touching production sessions.

- [ ] T006 Create a disposable run root under `%TEMP%` and copy `FileSystemExample/game_session` into it
- [ ] T007 Configure only the sandbox `game_session/config.json` for `gmBridgeEnabled=true`, `gmBridgeBackend=ConPTYBridge`, and `gmCliLaunchCommand=codex --dangerously-bypass-approvals-and-sandbox`
- [ ] T008 Start the GM bridge through `BookOfEternityClient/Launcher/bookofeternity.ps1` and confirm `game_state/control/gm_bridge_status.json` exists in the sandbox
- [ ] T009 Start the console client with Agent Console enabled according to `docs/e2e/agent-console-runbook.md`
- [ ] T010 Confirm Agent Console snapshot and event endpoints respond before any player action

**Checkpoint**: Live test infrastructure is ready; do not inspect sandbox JSON during active play except for setup/teardown or post-failure debugging.

---

## Phase 3: User Story 1 - Live Console Adventure Can Be Tested (Priority: P1) MVP

**Goal**: Execute a real console playtest route through Agent Console with a live Codex GM bridge.

**Independent Test**: The run has launch metadata, snapshots/events before and after actions, and a clear pass/blocked/failed route summary.

- [ ] T011 [US1] Save an Agent Console snapshot before every player action under the run root
- [ ] T012 [US1] Drive all player text/actions through Agent Console only
- [ ] T013 [US1] Attempt visible lifecycle progression from the starting state into mortal play or afterlife interaction
- [ ] T014 [US1] Record bridge status, player-visible output, and events after each meaningful action
- [ ] T015 [US1] Stop and classify any P0/P1 harness or client blocker before continuing unrelated actions

**Checkpoint**: The live route is attempted with enough artifacts for reproduction.

---

## Phase 4: User Story 2 - Command Output Is Audited As Player-Facing UI (Priority: P1)

**Goal**: Cover console command output quality across the available mortal-world and reachable afterlife surfaces.

**Independent Test**: At least 12 command/action surfaces are observed, or a blocker explains why fewer were reachable.

- [ ] T016 [US2] Audit `/статус` output for useful state, bars, effects, and no debug/internal text
- [ ] T017 [US2] Audit `/инв` and item details for readable bonuses, structural bonuses, documents, and markup safety
- [ ] T018 [US2] Audit `/книги` for selectable/readable document detail instead of raw or bulk-unusable output
- [ ] T019 [US2] Audit `/эффекты` and `/навыки` for summary/detail authority and localized mechanical fields
- [ ] T020 [US2] Audit `/квесты`, NPC, and faction surfaces for useful summary plus discoverable detailed views
- [ ] T021 [US2] Audit `/карта`, location/navigation, and world-news surfaces for lifecycle-correct messages and useful drill-downs
- [ ] T022 [US2] Audit combat/QTE entry points if reachable and classify usability issues separately from balance/design wishes
- [ ] T023 [US2] Audit end-life/afterlife reward surfaces if reachable and record missing discoverability or corruption
- [ ] T024 [US2] Write a command-output findings table in the run summary artifact and link it from #1157

**Checkpoint**: Command output findings are classified with artifacts.

---

## Phase 5: User Story 3 - Blocking Console Defects Are Repaired With Evidence (Priority: P2)

**Goal**: Repair narrow, high-impact console defects found during the pass and file follow-ups for broader work.

**Independent Test**: Each repaired defect has red/green evidence or an explicit live-only verification note.

- [ ] T025 [US3] For each in-scope defect, identify the owning files in `BookOfEternityClient/` and existing related tests in `BookOfEternityClient.Tests/`
- [ ] T026 [US3] Add a failing regression/source-guard test in `BookOfEternityClient.Tests/` before changing implementation
- [ ] T027 [US3] Implement the narrow fix in the relevant `BookOfEternityClient/` console, renderer, command, or launcher path
- [ ] T028 [US3] Rerun the focused test and affected live command/action step to verify the fix
- [ ] T029 [US3] Create follow-up GitHub issues for broad, browser-owned, GM-contract, validation, or design-level defects that are not safely repairable in #1157

**Checkpoint**: Fixed defects have evidence; non-fixed defects have issue links.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Reconcile artifacts, run broad verification, and prepare merge.

- [ ] T030 Update `specs/1157-console-player-readiness/tasks.md` checkboxes for completed work with evidence
- [ ] T031 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore` if any code changed
- [ ] T032 Clean generated `TestResults/` and temporary pass artifacts that are not intentionally preserved
- [ ] T033 Run `git diff --check` and inspect `git diff --stat`
- [ ] T034 Commit Spec Kit artifacts, code/tests, and any docs updates tied to #1157
- [ ] T035 Open, verify, merge PR for #1157 when acceptance criteria are met
- [ ] T036 Post final #1157 issue comment with live run result, verification commands, fixed defects, follow-up issues, and residual risk

## Dependencies & Execution Order

- Phase 1 before all other phases.
- Phase 2 before live play.
- US1 and US2 run sequentially in the same live route.
- US3 begins only after a defect is classified as in-scope.
- Phase 6 waits for desired repairs and verification.

## Parallel Opportunities

- T003 and T004 can run in parallel.
- Individual command-output audit notes in T016-T023 can be summarized in parallel after snapshots exist.
- Regression tests for independent defects can be developed in parallel if they touch different files.

## Implementation Strategy

1. Complete setup and live infrastructure first.
2. Execute the minimum live route and baseline command audit.
3. Fix only narrow P0/P1/repeated P2 issues with tests.
4. File precise follow-up issues for broad or out-of-scope problems.
5. Verify, commit, PR, merge, and close #1157 only with evidence.
