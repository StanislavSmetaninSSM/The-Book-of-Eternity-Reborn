# Tasks: QTE Practice Mode

**Input**: `specs/925-qte-practice-mode/spec.md`, `specs/925-qte-practice-mode/plan.md`, issue [#925](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925)
**Source Issues**: #925, parent #911, related #918/#920/#924, consumer #919
**Branch**: `work/925-qte-practice-mode`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 266/266; `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 52 packages and 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with player-facing Vitest 60/60 and Vite build success.

- [ ] **T002 Add failing practice catalog and no-campaign launch tests**
  Add C# tests proving Practice Mode can open with no active campaign, lists implemented QTE types, hides/marks unavailable types safely, and does not create or advance a normal campaign session. RED command should fail because no practice catalog/entry point exists yet.

- [ ] **T003 Add failing no-mutation tests for practice attempts**
  Add tests around representative practice attempts that snapshot campaign/progression/permanent reward surfaces and assert opening/completing/retrying/exiting practice does not write achievements, Ink Feathers, XP, inventory, quests, Daren reward state, pending campaign actions, or ordinary turn state. RED command should fail because practice attempt isolation does not exist yet.

- [ ] **T004 Add failing browser practice surface tests**
  Add frontend and/or browser API tests proving the browser can render practice catalog/attempt/result surfaces, reuse #918 QTE mini-games, avoid raw endpoint/DTO/debug/manual-grade wording, preserve keyboard shortcut bubbling guards, and expose retry/change/exit affordances. RED command should fail because the practice route/surface is absent.

- [ ] **T005 Add failing documentation/source guards for the practice boundary**
  Add docs/source guard tests proving the QTE docs/help mention client-owned practice, no rewards, no GM-authored practice scenes, no campaign mutation, and relationship to Daren #919 without making #919 a dependency. RED command should fail until docs are updated.

## Phase 2: C# practice authority and console entry point

- [ ] **T006 Implement practice catalog and deterministic presets**
  Add a client-owned catalog for implemented QTE types (`BranchChoice`, `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, `LockPinSet`) with player-facing names/instructions/supported surfaces/difficulty presets. Generate deterministic practice configs from type+difficulty and validate them through existing QTE validation paths.

- [ ] **T007 Implement ephemeral practice attempt lifecycle**
  Add the minimal C# service/state needed to start, resolve, retry, change difficulty/type, and exit a practice attempt while keeping campaign/permanent reward state unchanged. Reuse existing QTE resolution/score helpers; do not introduce Daren rewards or new GM-authored contracts.

- [ ] **T008 Add console/menu/help entry point**
  Expose Practice Mode from an appropriate console player-facing surface with Russian/in-world training copy. Include pre-timer instructions, #920 RU/EN key labels, grade feedback, retry/change/exit controls, and no-reward/no-campaign messaging.

- [ ] **T009 Verify C# practice tests GREEN**
  Run the focused C# practice/catalog/no-mutation tests added in T002-T003 and record exact pass counts plus any RED/GREEN notes here.

## Phase 3: Browser practice surface

- [ ] **T010 Add browser API/projection for practice catalog and attempts**
  Add or extend local web API/projection only as needed for practice catalog, attempt state, grade submission, and result feedback. Keep C# as attempt lifecycle/result/write authority and avoid exposing debug/raw endpoint language in default UI.

- [ ] **T011 Implement React practice route/components**
  Add browser route/navigation entry and components that render catalog, difficulty selection, instructions, QTE mini-game attempts, result feedback, retry/change/exit actions, and local-only score summaries. Reuse existing #918 QTE mini-game helpers/components instead of duplicating gameplay rules.

- [ ] **T012 Verify browser practice tests GREEN**
  Run focused frontend/browser API tests added in T004 and record exact pass counts. Include `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser/frontend code changed.

## Phase 4: Documentation, source guards, and Spec evidence

- [ ] **T013 Update docs/help/examples/source guards**
  Update QTE docs/help/source guards so Practice Mode is documented as client-owned training with no rewards, no campaign mutation, no GM-authored practice scenes, and a clear relationship to #919 Daren showcase. Do not add new afterlife pending/control docs because this feature should not touch afterlife contracts.

- [ ] **T014 Verify documentation/source guards GREEN**
  Run focused documentation/source guard tests and record exact counts. Include `PromptDocumentationCoverageTests` and `ExampleDocumentationValidationTests` if docs/examples changed.

- [ ] **T015 Update Spec Kit tasks with implementation evidence**
  Fill T002-T014 with RED/GREEN/final verification evidence. Leave Hermes-owned PR/merge/closure tasks open until after independent review and local gates.

## Phase 5: Verification and review

- [ ] **T016 Run final local gates**
  Run Spec Kit prerequisites, `git diff --check origin/main...HEAD`, added-line static scan, focused QTE practice/runtime/browser/docs tests, C# builds, and frontend verification if frontend/browser changed. Record exact commands and pass/fail counts.

- [ ] **T017 Independent pre-merge review**
  Obtain independent review against #925 acceptance criteria, Spec Kit artifacts, diff, no-mutation guarantees, and console/browser parity. Fix critical/important findings and rerun focused gates before PR.

## Phase 6: Hermes-owned PR, merge, and closure

- [ ] **T018 Create PR**
  Push `work/925-qte-practice-mode`, create a PR to `main` that closes #925, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T019 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge with `[skip ci]`, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #925 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T020 Post issue evidence comment and cleanup**
  Comment closure evidence on #925, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
