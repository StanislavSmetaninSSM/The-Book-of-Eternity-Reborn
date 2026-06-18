# Tasks: Console QTE Live Playability

**Input**: `specs/1081-qte-live-pacing/spec.md`, `specs/1081-qte-live-pacing/plan.md`, issue [#1081](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)
**Source Issues**: #1081
**Branch**: `work/1081-qte-live-pacing`

## Phase 1: Setup and baseline

- [x] **T001 Baseline focused QTE/console verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "Qte|Daren|RhythmPulse|ConsoleE2ESmokeTests" --logger "console;verbosity=minimal"` passed on 2026-06-18 from `E:/Games/worktrees/boe-1081-qte-live-pacing` with 0 failed / 356 passed / 0 skipped / 356 total.

- [ ] **T002 Read issue, reopen comments, Spec Kit artifacts, and relevant QTE references**
  Inspect #1081 body and comments, `specs/944-console-qte-live-render/`, `BookOfEternityClient/Services/QteSceneService.cs`, existing QTE tests/source guards, and console E2E harness helpers. Record root-cause notes before production edits.

- [ ] **T003 Confirm live evidence strategy**
  Decide whether this branch will use an existing deterministic in-app/harness seam, add a small focused harness/test seam, or capture manual console artifacts. Static source guards alone are not enough. Record the chosen strategy and limitations in this file.

## Phase 2: RED coverage and root-cause tests

- [ ] **T004 Add RED coverage for TimingBar pacing**
  Add a focused test or live-path probe that fails on current behavior because TimingBar high difficulty/stat-adjusted movement remains too slow or does not materially differ from low difficulty. The test must avoid brittle long wall-clock sleeps when possible.

- [ ] **T005 Add RED coverage for PromptChain immediate-fail startup**
  Add a focused test or live-path probe that fails when PromptChain starts with the actionable input timer already effectively expired or fails before a readable display/grace/input window.

- [ ] **T006 Add RED coverage for BalanceMeter control readability**
  Add a focused source/copy/runtime assertion or live snapshot that fails if the live BalanceMeter panel does not communicate A/← and D/→ direction plus effective step/current/safe-range information.

- [ ] **T007 Add RED coverage for PatternMemory input reveal leakage**
  Add a focused test or live snapshot that fails if PatternMemory input phase renders the full original reveal sequence/answer after reveal ends.

## Phase 3: Minimal implementation fixes

- [ ] **T008 Fix TimingBar difficulty-sensitive pacing**
  Adjust the live TimingBar speed/window/timing logic so difficulty matters and high difficulty is not trivially winnable, while normal/easy remain fair. Preserve grade semantics and stat-tier influence.

- [ ] **T009 Fix PromptChain readable startup/input timing**
  Ensure PromptChain displays the current sign/step/mistake/timer information and provides a readable initial window before timeout can fail the attempt. Preserve timeout/cancel semantics.

- [ ] **T010 Fix BalanceMeter live control copy/state display**
  Update the live BalanceMeter panel so player-facing Russian copy shows direction, effective step, current marker, and safe/target range. Verify key movement matches the visible hint.

- [ ] **T011 Fix PatternMemory input-phase rendering**
  Ensure the reveal sequence is cleared/hidden from the input phase and only progress/current prompt/control information remains visible. Preserve reveal phase and result summaries.

## Phase 4: Live/harness evidence

- [ ] **T012 Produce live/harness evidence for TimingBar**
  Capture deterministic output, harness snapshot, or manual transcript proving TimingBar pacing/difficulty behavior uses the live path. Record artifact path and result.

- [ ] **T013 Produce live/harness evidence for PromptChain**
  Capture deterministic output, harness snapshot, or manual transcript proving PromptChain no longer fails immediately before the player can read/react. Record artifact path and result.

- [ ] **T014 Produce live/harness evidence for BalanceMeter**
  Capture deterministic output, harness snapshot, or manual transcript proving BalanceMeter visible controls/step/range are legible. Record artifact path and result.

- [ ] **T015 Produce live/harness evidence for PatternMemory**
  Capture deterministic output, harness snapshot, or manual transcript proving PatternMemory input phase no longer displays the full reveal sequence. Record artifact path and result.

## Phase 5: Verification and Spec evidence

- [ ] **T016 Verify focused #1081 regression tests GREEN**
  Run the new/focused #1081 test filter and record exact pass/fail/skip counts.

- [ ] **T017 Verify broader QTE neighborhood GREEN**
  Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` and record exact counts.

- [ ] **T018 Run build, Spec Kit prerequisite check, diff hygiene, and static scan**
  Run:
  - `SPECIFY_FEATURE_DIRECTORY=specs/1081-qte-live-pacing powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `git diff --check origin/main...HEAD`
  - added-line static scan for hardcoded secrets, shell/eval/pickle/SQL injection, and accidental run artifacts.
  Record exact outputs or counts here. Note: plain prerequisite script currently follows the tracked `.specify/feature.json` pointer for #1092 on `main`; use the environment override for this branch and do not commit transient active-feature pointer churn unless the repository convention is intentionally changed.

- [ ] **T019 Reconcile docs/prompts impact**
  If player-facing control/help copy changed only inside client-owned console QTE UI, record that no GM prompt/example update was required. If any GM-authored QTE contract/schema/rules changed, update `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, manifests/source guards, and this Spec Kit feature before proceeding.

## Phase 6: Independent review and Hermes-owned closure

- [ ] **T020 Independent pre-merge review**
  Obtain an independent review against issue #1081, reopen comments, this spec/plan/tasks/contract, and `origin/main...HEAD`. Critical/Important findings must be fixed before PR/merge.

- [ ] **T021 Create PR**
  Push `work/1081-qte-live-pacing`, create a PR to `main` that closes #1081, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T022 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge with `[skip ci]`, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #1081 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T023 Post issue evidence comment and cleanup**
  Comment closure evidence on #1081, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
