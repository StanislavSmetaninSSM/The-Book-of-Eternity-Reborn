# Tasks: Console QTE Live Rendering

**Input**: `specs/944-console-qte-live-render/spec.md`, `specs/944-console-qte-live-render/plan.md`, issue [#944](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/944)
**Source Issues**: #944
**Branch**: `work/944-console-qte-live-render`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|GameEngineSourceGuardTests" --logger "console;verbosity=minimal"` passed 288/288 on 2026-06-11 from `E:/Games/worktrees/boe-944-console-qte-live-render` before this feature directory was created.

- [ ] **T002 Add failing QTE rendering source guard**
  Add `BookOfEternityClient.Tests/QteSceneRenderingSourceGuardTests.cs` or equivalent. The guard must fail on current `origin/main` because `RenderMiniGamePanel` calls `AnsiConsole.Clear()` and representative high-frequency loops (`RunTimingBarAsync`, `RunMashInputAsync`, `RunRhythmPulseAsync`, plus `RunLockPinSetAsync` or `RunStealthNoiseAsync` if applicable) still use the clear-per-tick helper. RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneRenderingSourceGuardTests" --logger "console;verbosity=minimal"`.

- [ ] **T003 Add renderer behavior test if feasible**
  If the implementation introduces an internal renderer abstraction, add a focused test proving repeated updates do not invoke a clear operation while still accepting frame updates and completion. If this would require brittle Spectre.Console internals, keep the source guard as the authoritative automated test and document why no lower-level renderer unit test was added.

## Phase 2: Live/update renderer implementation

- [ ] **T004 Introduce no-clear mini-game rendering path**
  Replace the clear-per-tick `RenderMiniGamePanel` behavior with a stable live/update rendering path. Prefer Spectre.Console live rendering after inspecting the installed API. Keep title, instructions, border/frame, and layout support note stable while updating only the dynamic body.

- [ ] **T005 Wire representative timed QTE loops through the new path**
  Ensure TimingBar, MashInput, RhythmPulse, and at least one newer timed type such as LockPinSet or StealthNoise use the no-clear update path. Preserve Esc cancellation, timeout handling, grade resolution, scoring summaries, and #920 QTE input normalization.

- [ ] **T006 Preserve deliberate one-time scene clears**
  Keep existing one-time clears for offers, preludes, menus, result screens, or blocking transitions where they are not inside high-frequency animation/timer loops. Do not broaden the issue into a full console UI rewrite.

## Phase 3: Verification and Spec evidence

- [ ] **T007 Verify focused QTE rendering tests GREEN**
  Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|QteSceneRenderingSourceGuardTests|GameEngineSourceGuardTests" --logger "console;verbosity=minimal"` and record exact pass/fail counts here.

- [ ] **T008 Verify broader QTE neighborhood GREEN**
  Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` and record exact pass/fail counts here.

- [ ] **T009 Run build, Spec Kit prerequisite check, diff hygiene, and static scan**
  Run:
  - `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`
  - `git diff --check origin/main...HEAD`
  - added-line static scan for secrets/shell/eval/pickle/SQL injection and run artifacts.
  Record exact outputs or counts here.

- [ ] **T010 Record manual/visual verification status**
  If an actual console visual smoke can be run, cover TimingBar, MashInput, RhythmPulse, and one newer QTE type and record the evidence. If the autonomous environment cannot perform human observation, record that limitation and point to automated source/renderer evidence instead.

## Phase 4: Independent review and Hermes-owned closure

- [ ] **T011 Independent pre-merge review**
  Obtain an independent review against issue #944, this spec/plan/tasks, and `origin/main...HEAD`. Critical/Important findings must be fixed before PR/merge.

- [ ] **T012 Create PR**
  Push `work/944-console-qte-live-render`, create a PR to `main` that closes #944, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T013 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge with `[skip ci]`, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #944 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T014 Post issue evidence comment and cleanup**
  Comment closure evidence on #944, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
