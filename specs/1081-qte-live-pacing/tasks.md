# Tasks: Console QTE Live Playability

**Input**: `specs/1081-qte-live-pacing/spec.md`, `specs/1081-qte-live-pacing/plan.md`, issue [#1081](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)
**Source Issues**: #1081
**Branch**: `work/1081-qte-live-pacing`

## Phase 1: Setup and baseline

- [x] **T001 Baseline focused QTE/console verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "Qte|Daren|RhythmPulse|ConsoleE2ESmokeTests" --logger "console;verbosity=minimal"` passed on 2026-06-18 from `E:/Games/worktrees/boe-1081-qte-live-pacing` with 0 failed / 356 passed / 0 skipped / 356 total.

- [x] **T002 Read issue, reopen comments, Spec Kit artifacts, and relevant QTE references**
  Inspect #1081 body and comments, `specs/944-console-qte-live-render/`, `BookOfEternityClient/Services/QteSceneService.cs`, existing QTE tests/source guards, and console E2E harness helpers. Record root-cause notes before production edits.
  Evidence: inspected `AGENTS.md`, `.specify/memory/constitution.md`, this feature's `spec.md`/`plan.md`/`contracts/qte-live-playability.md`, `specs/944-console-qte-live-render/plan.md`, `QteSceneService.cs`, existing `QteSceneServiceTests`, `QteSceneRenderingSourceGuardTests`, and console E2E helpers. Root causes found:
  - TimingBar had faster high-difficulty tick math on paper, but no bounded live attempt duration; repeated marker passes still made success forgiving.
  - PromptChain could shrink to roughly half-second per prompt at high difficulty and started the first prompt countdown immediately, leaving no readable startup grace.
  - BalanceMeter used terse numeric copy (`A/←: -10 | D/→: +10`) that did not plainly state left/right direction and effective step in the live frame.
  - PatternMemory rendered reveal and input as two separate `RunMiniGameLiveAsync` displays with `AutoClear(false)`, so the reveal frame could remain visible above the input frame even though the input builder did not contain the sequence.

- [x] **T003 Confirm live evidence strategy**
  Decide whether this branch will use an existing deterministic in-app/harness seam, add a small focused harness/test seam, or capture manual console artifacts. Static source guards alone are not enough. Record the chosen strategy and limitations in this file.
  Evidence: chose deterministic in-process harness evidence. The first implementation added helper/source coverage, but independent review found that was not sufficient live-path proof. The follow-up fix factors the four reported mini-game live loops into internal `Run*LiveLoopAsync` methods still invoked from `RunMiniGameLiveAsync`, and `BookOfEternityClient.Tests/QteLivePlayabilityTests.cs` drives those loops with fake renderer/input/clock objects to capture actual renderer frame updates without wall-clock sleeps. No manual human/ConPTY visual artifact was captured in this autonomous run; the substitute evidence is deterministic live-loop harness output.

## Phase 2: RED coverage and root-cause tests

- [x] **T004 Add RED coverage for TimingBar pacing**
  Add a focused test or live-path probe that fails on current behavior because TimingBar high difficulty/stat-adjusted movement remains too slow or does not materially differ from low difficulty. The test must avoid brittle long wall-clock sleeps when possible.
  RED Evidence: original helper/source RED used `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteLive|QteSceneRenderingSourceGuardTests" --logger "console;verbosity=minimal"` and failed before production edits with 8 failed / 10 passed / 0 skipped / 18 total. TimingBar failures: `QteLive_TimingBarHardDifficultyUsesBoundedFastPacing` missing `ComputeTimingBarLiveRequirement`; `TimingBarSpeed_MustGetFasterWhenDifficultyIncreases` missing bounded requirement/timeout use. Review-blocker RED later replaced the helper-only `QteLive` assertion with `QteLive_TimingBarLoopRecordsDifficultyBoundedFramesThroughLiveRenderer`, which failed to compile until the deterministic renderer/input/clock live-loop seam existed.

- [x] **T005 Add RED coverage for PromptChain immediate-fail startup**
  Add a focused test or live-path probe that fails when PromptChain starts with the actionable input timer already effectively expired or fails before a readable display/grace/input window.
  RED Evidence: same original helper/source RED command failed before production edits. PromptChain failures: `QteLive_PromptChainHardDifficultyKeepsReadableFirstPrompt` missing `ComputePromptChainLiveRequirement`; `PromptChainStartup_MustUseReadableFirstPromptWindow` missing explicit first-prompt grace. Review-blocker RED later replaced the helper-only `QteLive` assertion with `QteLive_PromptChainFirstPromptSurvivesPastBaseTimeoutThroughLiveLoop`, which failed to compile until the live-loop seam existed.

- [x] **T006 Add RED coverage for BalanceMeter control readability**
  Add a focused source/copy/runtime assertion or live snapshot that fails if the live BalanceMeter panel does not communicate A/← and D/→ direction plus effective step/current/safe-range information.
  RED Evidence: same original helper/source RED command failed before production edits. BalanceMeter failures: `QteLive_BalanceMeterFrameExplainsControlsStepAndSafeRange` missing `BuildBalanceMeterLiveFrame`; `BalanceMeterProgress_MustExplainPositionAndControlStep` did not find plain "влево на", "вправо на", or "Шаг управления" copy. Review-blocker RED later replaced the helper-only `QteLive` assertion with `QteLive_BalanceMeterLiveLoopShowsControlCopyAndAppliesSameStep`, which failed to compile until the live-loop seam existed.

- [x] **T007 Add RED coverage for PatternMemory input reveal leakage**
  Add a focused test or live snapshot that fails if PatternMemory input phase renders the full original reveal sequence/answer after reveal ends.
  RED Evidence: same original helper/source RED command failed before production edits. PatternMemory failures: `QteLive_PatternMemoryInputFrameDoesNotContainRevealSequence` missing deterministic reveal/input frame seam; `PatternMemory_MustReplaceRevealWithInputInSameLiveDisplay` found 2 `RunMiniGameLiveAsync` calls in `RunPatternMemoryAsync`. Review-blocker RED later replaced the helper-only `QteLive` assertion with `QteLive_PatternMemoryLiveLoopReplacesRevealFrameBeforeInput`, which failed to compile until the live-loop seam existed.

## Phase 3: Minimal implementation fixes

- [x] **T008 Fix TimingBar difficulty-sensitive pacing**
  Adjust the live TimingBar speed/window/timing logic so difficulty matters and high difficulty is not trivially winnable, while normal/easy remain fair. Preserve grade semantics and stat-tier influence.
  Evidence: added `TimingBarLiveRequirement`/`ComputeTimingBarLiveRequirement`, changed `RunTimingBarAsync` to use the computed requirement through `RunTimingBarLiveLoopAsync`, faster high-difficulty tick/window, visible remaining time, and bounded timeout. Focused GREEN below shows hard difficulty has a bounded 80-220 ms success window and 1200-3000 ms timeout in the deterministic live-loop harness.

- [x] **T009 Fix PromptChain readable startup/input timing**
  Ensure PromptChain displays the current sign/step/mistake/timer information and provides a readable initial window before timeout can fail the attempt. Preserve timeout/cancel semantics.
  Evidence: added `PromptChainLiveRequirement`/`ComputePromptChainLiveRequirement`; high difficulty keeps per-prompt timeout in 700-1200 ms range and first prompt in 1000-1800 ms range through explicit `FirstPromptGraceMs`, enforced by `RunPromptChainLiveLoopAsync`.

- [x] **T010 Fix BalanceMeter live control copy/state display**
  Update the live BalanceMeter panel so player-facing Russian copy shows direction, effective step, current marker, and safe/target range. Verify key movement matches the visible hint.
  Evidence: `RunBalanceMeterAsync` now uses a single `BalanceMeterMovementStep` constant through `RunBalanceMeterLiveLoopAsync` for A/left and D/right movement, instructions are Russian/in-world, and live renderer frames show position, safe range, `A/Ф или ←: влево на 10`, `D/В или →: вправо на 10`, and `Шаг управления: 10`.

- [x] **T011 Fix PatternMemory input-phase rendering**
  Ensure the reveal sequence is cleared/hidden from the input phase and only progress/current prompt/control information remains visible. Preserve reveal phase and result summaries.
  Evidence: `RunPatternMemoryAsync` now uses one `RunMiniGameLiveAsync` call and updates the attached live display from reveal title/body to input title/body, replacing the reveal frame instead of leaving it above input. Input frame builders still omit `FormatPatternMemorySequence` and `Показ:`.

## Phase 4: Live/harness evidence

- [x] **T012 Produce live/harness evidence for TimingBar**
  Capture deterministic output, harness snapshot, or manual transcript proving TimingBar pacing/difficulty behavior uses the live path. Record artifact path and result.
  Evidence: deterministic harness test `QteLive_TimingBarLoopRecordsDifficultyBoundedFramesThroughLiveRenderer` in `BookOfEternityClient.Tests/QteLivePlayabilityTests.cs` passed after the review-blocker fix. It calls `RunTimingBarLiveLoopAsync`, the same live loop invoked from `RunTimingBarAsync` inside `RunMiniGameLiveAsync`, captures renderer frames, verifies the live delay tick, and asserts bounded hard-difficulty pacing.

- [x] **T013 Produce live/harness evidence for PromptChain**
  Capture deterministic output, harness snapshot, or manual transcript proving PromptChain no longer fails immediately before the player can read/react. Record artifact path and result.
  Evidence: deterministic harness test `QteLive_PromptChainFirstPromptSurvivesPastBaseTimeoutThroughLiveLoop` in `BookOfEternityClient.Tests/QteLivePlayabilityTests.cs` passed after the review-blocker fix. It calls `RunPromptChainLiveLoopAsync`, schedules the first input after the base per-prompt timeout but before the grace-extended first timeout, and captures the first prompt live frame.

- [x] **T014 Produce live/harness evidence for BalanceMeter**
  Capture deterministic output, harness snapshot, or manual transcript proving BalanceMeter visible controls/step/range are legible. Record artifact path and result.
  Evidence: deterministic harness test `QteLive_BalanceMeterLiveLoopShowsControlCopyAndAppliesSameStep` in `BookOfEternityClient.Tests/QteLivePlayabilityTests.cs` passed after the review-blocker fix. It calls `RunBalanceMeterLiveLoopAsync` with deterministic zero drift, injects `D`, captures the live frame at position 60/100, and verifies the visible D/right step copy matches the applied +10 movement.

- [x] **T015 Produce live/harness evidence for PatternMemory**
  Capture deterministic output, harness snapshot, or manual transcript proving PatternMemory input phase no longer displays the full reveal sequence. Record artifact path and result.
  Evidence: deterministic harness test `QteLive_PatternMemoryLiveLoopReplacesRevealFrameBeforeInput` in `BookOfEternityClient.Tests/QteLivePlayabilityTests.cs` passed after the review-blocker fix. It calls `RunPatternMemoryLiveLoopAsync`, captures reveal and input renderer frames from the same live loop, and verifies input frames use the input title/progress without `Показ:` or the full `Q / Й  W / Ц  Space` reveal sequence. `PatternMemory_MustReplaceRevealWithInputInSameLiveDisplay` in `QteSceneRenderingSourceGuardTests` remains as a supplemental source guard.

## Phase 5: Verification and Spec evidence

- [x] **T016 Verify focused #1081 regression tests GREEN**
  Run the new/focused #1081 test filter and record exact pass/fail/skip counts.
  Evidence: review-blocker focused GREEN `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteLive|QteSceneRenderingSourceGuardTests" --logger "console;verbosity=minimal"` passed with 0 failed / 18 passed / 0 skipped / 18 total. Main focused GREEN `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteLive|QteSceneRenderingSourceGuardTests|QteSceneServiceTests" --logger "console;verbosity=minimal"` passed with 0 failed / 97 passed / 0 skipped / 97 total and includes the live-loop harness evidence above.

- [x] **T017 Verify broader QTE neighborhood GREEN**
  Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` and record exact counts.
  Evidence: review-blocker fix-run command above passed with 0 failed / 243 passed / 0 skipped / 243 total. Earlier reopened-issue baseline `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Qte|Daren|RhythmPulse|ConsoleE2ESmokeTests" --logger "console;verbosity=minimal"` passed with 0 failed / 362 passed / 0 skipped / 362 total.

- [x] **T018 Run build, Spec Kit prerequisite check, diff hygiene, and static scan**
  Run:
  - `SPECIFY_FEATURE_DIRECTORY=specs/1081-qte-live-pacing powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `git diff --check origin/main...HEAD`
  - added-line static scan for hardcoded secrets, shell/eval/pickle/SQL injection, and accidental run artifacts.
  Record exact outputs or counts here. Note: plain prerequisite script currently follows the tracked `.specify/feature.json` pointer for #1092 on `main`; use the environment override for this branch and do not commit transient active-feature pointer churn unless the repository convention is intentionally changed.
  Evidence:
  - `$env:SPECIFY_FEATURE_DIRECTORY='specs/1081-qte-live-pacing'; .\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-1081-qte-live-pacing\specs\1081-qte-live-pacing`, `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings / 0 errors.
  - `git diff --check` passed with no whitespace errors; Git printed CRLF normalization warnings only.
  - Added-line static scan over `origin/main` plus current working changes reported: `Static scan clean: 0 findings over added lines (secrets/artifacts all files; injection patterns code files only).`

- [x] **T019 Reconcile docs/prompts impact**
  If player-facing control/help copy changed only inside client-owned console QTE UI, record that no GM prompt/example update was required. If any GM-authored QTE contract/schema/rules changed, update `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, manifests/source guards, and this Spec Kit feature before proceeding.
  Evidence: changes are limited to client-owned console live QTE pacing/rendering/copy and deterministic tests. No GM-authored QTE schema, validation rule, prompt, example, save/runtime-state, browser DTO, Daren route, or afterlife contract changed; no GM prompt/example update required.

## Phase 6: Independent review and Hermes-owned closure

- [x] **T020 Independent pre-merge review**
  Obtain an independent review against issue #1081, reopen comments, this spec/plan/tasks/contract, and `origin/main...HEAD`. Critical/Important findings must be fixed before PR/merge.
  Evidence: initial independent review in `E:/Games/codex-runs/20260618-1347-review-boe-1081-qte-live-pacing/final.md` returned `CHANGES_REQUIRED` because the first implementation proved helper/source seams rather than actual live mini-game loops. Follow-up fix-run `E:/Games/codex-runs/20260618-1400-fix-boe-1081-qte-live-harness/` added deterministic live-loop harness evidence. Re-review `E:/Games/codex-runs/20260618-1414-rereview-boe-1081-qte-live-pacing/final.md` returned `VERDICT: APPROVED` with no blocking findings; reviewer confirmed the previous blocker was fixed and that the new tests drive live-loop methods used by `RunMiniGameLiveAsync` with fake renderer/input/clock.

- [x] **T021 Create PR**
  Push `work/1081-qte-live-pacing`, create a PR to `main` that closes #1081, and include local verification evidence plus `GitHub Actions: not used / not required`.
  Evidence: pushed `work/1081-qte-live-pacing` to origin and created PR [#1098](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1098) titled `Fix console live QTE playability`. PR readback confirmed base `main`, head `work/1081-qte-live-pacing`, `mergeStateStatus=CLEAN`, `closingIssuesReferences=[#1081]`, and body includes local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T022 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge with `[skip ci]`, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #1081 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T023 Post issue evidence comment and cleanup**
  Comment closure evidence on #1081, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
