# Tasks: GM Workers Live Regression

**Input**: `specs/1189-gm-workers-live-regression/spec.md`, `plan.md`, `quickstart.md`, GitHub issue #1189, existing GM worker specs #1113/#1143/#1145/#1147/#1149/#1151.

**Tests**: Runtime behavior changes require RED/GREEN tests before production edits. Pure live-run/report work requires focused verification plus preserved artifacts.

## Phase 1: Setup

- [x] T001 Create isolated worktree `E:/Games/worktrees/boe-1189-gm-workers-live` on branch `work/1189-gm-workers-live-regression`.
- [x] T002 Mark GitHub issue #1189 with `codex-agent in-progress`.
- [x] T003 Create Spec Kit artifacts under `specs/1189-gm-workers-live-regression/`.
- [x] T004 Update `AGENTS.md` active Spec Kit plan pointer to `specs/1189-gm-workers-live-regression/plan.md`.
- [x] T005 Run Spec Kit prerequisite check for `specs/1189-gm-workers-live-regression`.

## Phase 2: Baseline Context and Runbook

- [x] T006 Inspect existing worker contracts/docs/tests: `OtherGuides/GM_Worker_Bridges.md`, `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1`, `BookOfEternityClient/Services/GmWorkers/`, and focused tests.
- [x] T007 Run baseline focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"`.
- [x] T008 Create `docs/e2e/gm-workers-live-regression-runbook.md` with reproducible launch/setup/teardown steps.

## Phase 3: Live Regression

- [x] T009 Prepare disposable run root and copied game session outside the repo.
- [x] T010 Enable Codex worker profiles in sandbox settings for `NarrativeDraft`, `Analysis`, and `ValidationRepair` using hidden/background launch.
- [x] T011 Launch console client, Agent Console, GM daemon, and main Codex GM bridge.
- [x] T012 Produce narrative or analysis worker delegation evidence.
- [x] T013 Produce validation-repair worker delegation evidence or a precise controlled-failure record.
- [x] T014 Inspect proposal inbox, worker audit, task packets, Agent Console snapshots, and process visibility.
- [x] T015 Stop client, daemon, GM bridge, and worker processes using run metadata.

## Phase 4: Report, Follow-Ups, and Verification

- [x] T016 Create `docs/audits/gm-workers-live-regression-1189.md` with run root, commands, proposals, findings, readiness score, and residual risks.
- [x] T017 File focused bug issues for blocker/major findings, or record that none were found.
- [x] T018 Run final focused verification for changed files.
- [x] T019 Run `git diff --check` and inspect `git diff --stat`.
- [x] T020 Update #1189 with evidence and readiness assessment.
- [ ] T021 Commit, PR, merge, remove `codex-agent in-progress`, and close #1189 only after verification evidence is inspected.

## Evidence Log

- Worktree: `E:/Games/worktrees/boe-1189-gm-workers-live`, branch `work/1189-gm-workers-live-regression`, created from `origin/main` at `9e67b75`.
- GitHub #1189 marked `codex-agent in-progress`.
- Spec Kit prerequisite check: `$env:SPECIFY_FEATURE_DIRECTORY='specs/1189-gm-workers-live-regression'; .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-1189-gm-workers-live\specs\1189-gm-workers-live-regression` and `AVAILABLE_DOCS=["quickstart.md","tasks.md"]`.
- Spec Kit consistency scan found no `NEEDS CLARIFICATION`/TODO placeholders and confirmed task coverage for runbook, live run, proposal evidence, verification, and report deliverables.
- T006 context inspection confirmed `gm_worker_cli_runner.ps1` passes strict `BOE_WORKER_*` env vars to the configured worker agent command, uses hidden/no-window process startup, and requires a non-empty `worker-proposal-v1` handoff file. `OtherGuides/GM_Worker_Bridges.md`, `GmWorkerBridgeProfileTemplates`, `GmWorkerProposalOnlyDispatchService`, and `BookOfEternityGMBridge` confirm proposal-only `dispatchworkertask` support for `narrative-draft` and `analysis`, while validation-repair is wired through the validation repair delegator/apply gate.
- T007 baseline first failed before test execution because the fresh worktree lacked `BookOfEternityClient.Tests/obj/project.assets.json` under `--no-restore`; `dotnet restore BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj` restored packages successfully. Re-running `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"` passed: 145 passed, 0 failed, 0 skipped.
- T008 added `docs/e2e/gm-workers-live-regression-runbook.md` covering sandbox setup, camelCase `config.json` worker profile enablement, Agent Console launch, main GM bridge launch, direct `dispatchworkertask` pipe requests, validation-repair evidence expectations, preserved artifacts, and teardown.
- T009-T015 live run evidence:
  - Primary successful run root: `C:\Temp\boe-gm-workers-live-20260621-075955`.
  - Agent Console: `http://127.0.0.1:63511`.
  - Bridge pipe: `boe-gmbridge-live-7fa09637ea9e45f7bf4833f96de0d196`.
  - Proposal-only narrative dispatch `dispatch.narrative.exec.json`: `workerDispatch.outcome=completed`, proposal `worker_proposal_narrative_draft_19bec5ee7a92458da65b1999d96c0452`, review-only draft, zero changed files.
  - Proposal-only analysis dispatch `dispatch.analysis.exec.json`: `workerDispatch.outcome=completed`, proposal `worker_proposal_analysis_de59187585e642a8ad3c43c33ed0bcad`, three findings, zero changed files.
  - Validation-repair harness `validation-repair.result.json`: outcome `6`, proposal `worker_proposal_validation_repair_0001`, `ApplyResult=Accepted`, `ReadySignalCreated=true`, canonical `game_state/world/weather.json` repaired through apply gate.
  - Worker audit `game_session/game_state/control/gm_worker_audit.jsonl` contains `task-dispatched`, `proposal-received`, `proposal-applied`, and `validation-repair-ready-created`.
  - Process scan after teardown found no remaining run-root worker/client processes.
- Live blockers found and fixed under #1189 with RED/GREEN coverage:
  - Relative runner `-File "BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1"` failed from copied `game_session`; fixed by resolving runner paths in `GmWorkerBridgePool.CreateWorkerStartInfo`.
  - Bare `codex` did not resolve to the Windows shim under `UseShellExecute=false`; fixed by `where.exe` command resolution in `gm_worker_cli_runner.ps1`.
  - Hidden worker prompt stdin was not UTF-8 safe for Cyrillic JSON; fixed by writing UTF-8 bytes through `StandardInput.BaseStream`.
  - Interactive `codex --dangerously-bypass-approvals-and-sandbox` is not a hidden worker command; worker templates/docs now use `codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -`.
- T016 added `docs/audits/gm-workers-live-regression-1189.md` with readiness score `8/10`, artifact paths, bugs fixed, residual risk, and verification commands.
- T017 recorded no separate blocker/major GitHub follow-up is required; the live blockers were narrow enough to fix under #1189.
- T018 final verification:
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"` passed: 148 passed, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` passed: 0 warnings, 0 errors.
  - `dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore` passed: 0 warnings, 0 errors.
- T019 `git diff --check` passed. `git diff --stat` inspected; tracked diff before adding new report/spec/runbook files was 18 files, 275 insertions, 29 deletions.
- T020 posted GitHub issue comment: `https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1189#issuecomment-4760161532`.
