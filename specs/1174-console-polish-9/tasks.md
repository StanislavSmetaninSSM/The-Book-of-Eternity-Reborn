# Tasks: Console Client Polish Pass 2 to 9/10

**Input**: `specs/1174-console-polish-9/spec.md`, `plan.md`, source GitHub issues #1174-#1180, `AGENTS.md`, `.specify/memory/constitution.md`.

**Tests**: Behavior changes require RED/GREEN tests before production edits.

**Hard exclusions**: Do not modify browser/frontend files. Do not modify QTE engine, QTE balance, QTE scenarios, or QTE test mode.

## Phase 1: Setup and Baseline

- [x] T001 Create isolated worktree `E:/Games/worktrees/boe-1174-console-polish-9` on branch `work/1174-console-polish-9`.
- [x] T002 Confirm source issues #1174-#1180 exist and are linked in Spec Kit artifacts.
- [x] T003 Run baseline build for `BookOfEternityClient/BookOfEternityClient.csproj`.
- [x] T004 Run baseline build for `BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj`.
- [x] T005 Run Spec Kit prerequisite check for `specs/1174-console-polish-9`.
- [x] T006 Mark source issues #1174-#1180 with `codex-agent in-progress` once implementation begins.

## Phase 2: Command Matrix and Fixture Coverage (#1175)

- [ ] T007 Inspect console command registration, aliases, help text, and command result builders.
- [ ] T008 Inspect reusable Mortal World, Chaos Sea, and Shining Abode command-display fixtures/tests.
- [ ] T009 Create `docs/audits/console-command-matrix-1174.md` with every ordinary non-QTE console command, realm, lifecycle, fixture coverage, expected output shape, and current status.
- [ ] T010 Create follow-up issues for missing fixture data that blocks honest command inspection.
- [ ] T011 Update #1175 with the matrix path and coverage summary.

## Phase 3: Dry Command Sweep (#1176)

- [ ] T012 Identify the safest existing command execution/test harness for non-interactive command rendering.
- [ ] T013 Add RED tests for dry sweep classification of raw JSON/internal keys/markup errors on representative command output.
- [ ] T014 Implement or document the dry sweep runner/report format.
- [ ] T015 Run the sweep on prepared non-QTE command fixtures and save a lightweight report under `docs/audits/`.
- [ ] T016 Update #1176 with the sweep command, report path, and first findings.

## Phase 4: Mortal World Output and Navigation Polish (#1177)

- [ ] T017 Run targeted command-display tests and dry sweep for Mortal World commands.
- [ ] T018 Add RED tests for the highest-impact narrow Mortal World output/navigation defects.
- [ ] T019 Implement narrow fixes in console renderers/builders without touching browser files.
- [ ] T020 Rerun focused Mortal World command-display tests and affected dry sweep entries.
- [ ] T021 Create follow-up issues for broad Mortal World UX gaps not safely fixed in this branch.
- [ ] T022 Update #1177 with fixes, tests, and residual issues.

## Phase 5: Afterlife Output and Navigation Polish (#1178)

- [ ] T023 Run targeted command-display tests and dry sweep for Chaos Sea and Shining Abode commands.
- [ ] T024 Add RED tests for the highest-impact narrow afterlife output/navigation defects.
- [ ] T025 Implement narrow afterlife display fixes. If runtime/GM contracts change, update afterlife docs/examples/tests in the same commit.
- [ ] T026 Rerun focused afterlife command-display tests and documentation coverage tests when required.
- [ ] T027 Create follow-up issues for broad afterlife UX gaps not safely fixed in this branch.
- [ ] T028 Update #1178 with fixes, tests, and residual issues.

## Phase 6: Live Codex-GM E2E Playtest Without QTE (#1179)

- [ ] T029 Prepare a disposable game session and launch metadata for the console client, Agent Console, GM daemon, and Codex bridge.
- [ ] T030 Run a short adventure without QTE that covers conversation, exploration, inventory/item, book/document, NPC/faction/quest/world-news, simple non-QTE action, lifecycle/reward/afterlife where feasible.
- [ ] T031 Record route notes, command outputs, friction findings, bridge status, and player-visible failures in `docs/audits/console-live-playtest-1179.md`.
- [ ] T032 File or link follow-up issues for blocker/major friction not fixed before the final audit.
- [ ] T033 Update #1179 with the live report and residual risk.

## Phase 7: Final Acceptance Audit (#1180)

- [ ] T034 Create `docs/audits/console-acceptance-score-1180.md` with the final 1-10 rubric.
- [ ] T035 Run focused tests for all changed command surfaces.
- [ ] T036 Run final build and broad non-browser C# verification.
- [ ] T037 Run `git diff --check` and inspect `git diff --stat`.
- [ ] T038 Update #1180 with score, verification commands, changed files, and residual risks.
- [ ] T039 Commit with source issue references, open PR, review, merge when verified, and close completed issues only after evidence is inspected.

## Evidence Log

- Worktree: `E:/Games/worktrees/boe-1174-console-polish-9`, branch `work/1174-console-polish-9`, created from `origin/main` at `4c25586`.
- Baseline client build: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj` succeeded with 0 warnings and 0 errors.
- Baseline test-project build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` succeeded with 0 warnings and 0 errors.
- Baseline full test note: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` did not complete before the 120s tool timeout; targeted verification will be used during implementation and broad non-browser suite will be retried before merge.
- Spec Kit prerequisite check: `$env:SPECIFY_FEATURE_DIRECTORY='specs/1174-console-polish-9'; .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-1174-console-polish-9\specs\1174-console-polish-9` and `AVAILABLE_DOCS=["tasks.md"]`.
