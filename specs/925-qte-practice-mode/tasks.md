# Tasks: QTE Practice Mode

**Input**: `specs/925-qte-practice-mode/spec.md`, `specs/925-qte-practice-mode/plan.md`, issue [#925](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925)
**Source Issues**: #925, parent #911, related #918/#920/#924, consumer #919
**Branch**: `work/925-qte-practice-mode`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 266/266; `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 52 packages and 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with player-facing Vitest 60/60 and Vite build success.

- [x] **T002 Add failing practice catalog and no-campaign launch tests**
  Add C# tests proving Practice Mode can open with no active campaign, lists implemented QTE types, hides/marks unavailable types safely, and does not create or advance a normal campaign session. RED command should fail because no practice catalog/entry point exists yet.
  RED evidence: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QtePracticeModeTests|QtePracticeWebInteractionTests|PromptDocumentationCoverageTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` failed before implementation with `CS0246: QtePracticeWebStateDto` missing.

- [x] **T003 Add failing no-mutation tests for practice attempts**
  Add tests around representative practice attempts that snapshot campaign/progression/permanent reward surfaces and assert opening/completing/retrying/exiting practice does not write achievements, Ink Feathers, XP, inventory, quests, Daren reward state, pending campaign actions, or ordinary turn state. RED command should fail because practice attempt isolation does not exist yet.
  RED evidence: same focused C# command above failed before implementation because the practice catalog/web DTO and attempt APIs were absent, so no mutation-safe practice lifecycle existed.

- [x] **T004 Add failing browser practice surface tests**
  Add frontend and/or browser API tests proving the browser can render practice catalog/attempt/result surfaces, reuse #918 QTE mini-games, avoid raw endpoint/DTO/debug/manual-grade wording, preserve keyboard shortcut bubbling guards, and expose retry/change/exit affordances. RED command should fail because the practice route/surface is absent.
  RED evidence: `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` failed before implementation with `TS2305: QtePracticeWebStateDto` not exported and `TS2307: Cannot find module '../src/components/QtePracticeView'`.

- [x] **T005 Add failing documentation/source guards for the practice boundary**
  Add docs/source guard tests proving the QTE docs/help mention client-owned practice, no rewards, no GM-authored practice scenes, no campaign mutation, and relationship to Daren #919 without making #919 a dependency. RED command should fail until docs are updated.
  RED evidence: the focused C# command could not reach the new documentation guard because the practice DTO compile failure stopped the test assembly first; the guard now requires the practice boundary copy in `Rules/Block_CLI_QTE.txt`, `CLI_API_Specification.md`, `Examples/E_CLI_QTE_Offer.txt`, and `TaskGuides/CLI_Step_Main.txt`.

## Phase 2: C# practice authority and console entry point

- [x] **T006 Implement practice catalog and deterministic presets**
  Add a client-owned catalog for implemented QTE types (`BranchChoice`, `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, `LockPinSet`) with player-facing names/instructions/supported surfaces/difficulty presets. Generate deterministic practice configs from type+difficulty and validate them through existing QTE validation paths.
  Evidence: `QteSceneService.GetPracticeCatalog()` now returns the 11 implemented types only, with console/browser surfaces and easy/normal/hard presets. `QtePracticeModeTests.PracticeAttempt_GeneratedOfferHasValidQteConfigShape` runs each generated offer through `ValidationService.ValidateAcceptedTurnQteOfferAsync`, allowing only the two campaign-only validator errors (`qte_missing_pending_manifest`, `qte_success_outcome_requires_xp`) that practice intentionally must not satisfy.

- [x] **T007 Implement ephemeral practice attempt lifecycle**
  Add the minimal C# service/state needed to start, resolve, retry, change difficulty/type, and exit a practice attempt while keeping campaign/permanent reward state unchanged. Reuse existing QTE resolution/score helpers; do not introduce Daren rewards or new GM-authored contracts.
  Evidence: `QteSceneService.StartPracticeAttempt` and `ResolvePracticeAction` keep attempt state in memory, reuse existing grade routing/score/rank helpers, and avoid `qte_runtime.json`, `qte_history.json`, `qte_offer.json`, XP, inventory, quest, Ink Feather, Daren reward, pending campaign action, and ordinary turn writes. `QtePracticeModeTests.PracticeAttempt_ResolvesScoredAttemptWithoutMutatingCampaignOrRewardFiles` snapshots sentinel files before/after completion.

- [x] **T008 Add console/menu/help entry point**
  Expose Practice Mode from an appropriate console player-facing surface with Russian/in-world training copy. Include pre-timer instructions, #920 RU/EN key labels, grade feedback, retry/change/exit controls, and no-reward/no-campaign messaging.
  Evidence: console main menu now includes `qte_practice`; `QteSceneService.RunPracticeModeAsync` uses the existing console QTE check runners and result panels with retry/change/another/exit choices and no-reward/no-campaign copy.

- [x] **T009 Verify C# practice tests GREEN**
  Run the focused C# practice/catalog/no-mutation tests added in T002-T003 and record exact pass counts plus any RED/GREEN notes here.
  GREEN evidence: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QtePracticeModeTests|QtePracticeWebInteractionTests|PromptDocumentationCoverageTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 82/82 after implementation.

## Phase 3: Browser practice surface

- [x] **T010 Add browser API/projection for practice catalog and attempts**
  Add or extend local web API/projection only as needed for practice catalog, attempt state, grade submission, and result feedback. Keep C# as attempt lifecycle/result/write authority and avoid exposing debug/raw endpoint language in default UI.
  Evidence: `QteWebInteractionService` now exposes typed practice state/start/action/retry/exit methods and projects practice active scenes through the existing QTE action/check DTO builder. `LocalWebUiHost` maps `/api/qte/practice*` endpoints; React submits grades only and C# resolves routing/score/rank.

- [x] **T011 Implement React practice route/components**
  Add browser route/navigation entry and components that render catalog, difficulty selection, instructions, QTE mini-game attempts, result feedback, retry/change/exit actions, and local-only score summaries. Reuse existing #918 QTE mini-game helpers/components instead of duplicating gameplay rules.
  Evidence: `QtePracticeView` renders catalog, difficulty buttons, a pre-action ready gate so browser mini-game timers start only after the player confirms readiness, active attempts through `QteMiniGame`, completion score/rank feedback, and retry/change/another/exit controls. `ShellContext`, `App`, `tabBarConfig`, and `GameLauncher` expose practice from the launcher and tab bar without the ordinary command composer.

- [x] **T012 Verify browser practice tests GREEN**
  Run focused frontend/browser API tests added in T004 and record exact pass counts. Include `npm run verify --prefix BookOfEternityClient.WebFrontend` if browser/frontend code changed.
  GREEN evidence: `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` passed with Vitest 8 files and 64/64 tests after practice route/component/API updates. Full `npm run verify` remains part of T016 final gates.

## Phase 4: Documentation, source guards, and Spec evidence

- [x] **T013 Update docs/help/examples/source guards**
  Update QTE docs/help/source guards so Practice Mode is documented as client-owned training with no rewards, no campaign mutation, no GM-authored practice scenes, and a clear relationship to #919 Daren showcase. Do not add new afterlife pending/control docs because this feature should not touch afterlife contracts.
  Evidence: updated `Rules/Block_CLI_QTE.txt`, `CLI_API_Specification.md`, `Examples/E_CLI_QTE_Offer.txt`, and `TaskGuides/CLI_Step_Main.txt` with client-owned/no-reward/no-mutation/no-GM-authored-practice boundary copy and #919/Daren non-scope notes. No afterlife contract files were changed.

- [x] **T014 Verify documentation/source guards GREEN**
  Run focused documentation/source guard tests and record exact counts. Include `PromptDocumentationCoverageTests` and `ExampleDocumentationValidationTests` if docs/examples changed.
  GREEN evidence: the focused C# command in T009 included `PromptDocumentationCoverageTests`, `BrowserApiContractTests`, and `BrowserFrontendWorkspaceTests` and passed 82/82. `ExampleDocumentationValidationTests` remains part of T016 final focused gate because examples/docs changed.

- [x] **T015 Update Spec Kit tasks with implementation evidence**
  Fill T002-T014 with RED/GREEN/final verification evidence. Leave Hermes-owned PR/merge/closure tasks open until after independent review and local gates.
  Evidence: T002-T014 now contain RED/GREEN implementation evidence. T016-T020 remain open until final gates, independent review, and Hermes-owned lifecycle work complete.

## Phase 5: Verification and review

- [x] **T016 Run final local gates**
  Run Spec Kit prerequisites, `git diff --check origin/main...HEAD`, added-line static scan, focused QTE practice/runtime/browser/docs tests, C# builds, and frontend verification if frontend/browser changed. Record exact commands and pass/fail counts.
  Evidence:
  - `.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` passed and returned the active `specs/925-qte-practice-mode` feature with `contracts/` and `tasks.md`.
  - `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` passed with 0 warnings / 0 errors.
  - `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings / 0 errors.
  - `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "QtePracticeModeTests|QtePracticeWebInteractionTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` passed 299/299.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: typecheck, player-facing Vitest 8 files / 64 tests, and Vite production build.
  - `git diff --check origin/main...HEAD` passed.
  - Added-line static security scan over `origin/main...HEAD` passed with no matches for secret/shell/eval/pickle/SQL patterns; markdown/txt docs and Spec Kit task evidence were excluded to avoid documentation false positives.

- [ ] **T017 Independent pre-merge review**
  Obtain independent review against #925 acceptance criteria, Spec Kit artifacts, diff, no-mutation guarantees, and console/browser parity. Fix critical/important findings and rerun focused gates before PR.
  Evidence so far: independent Codex review run `E:/Games/codex-runs/20260611-014716-boe-925-qte-practice-review` returned `CHANGES_REQUIRED` for two browser blockers: raw QTE type ids in default practice UI and live mini-game timers mounting before a separate ready gate. Fix applied by replacing raw type-id pills with player-facing copy, adding a browser ready gate before mounting `QteMiniGame`, and strengthening `qteScenePanelMiniGames.test.tsx` to guard both behaviors. RED: targeted Vitest failed on the two new assertions before the fix. GREEN: `npm exec vitest run test/qteScenePanelMiniGames.test.tsx` passed 10/10 from the frontend workdir, then `npm run verify --prefix BookOfEternityClient.WebFrontend` passed typecheck, player-facing Vitest 8 files / 64 tests, and Vite production build. Re-review is still required before marking this task complete.

## Phase 6: Hermes-owned PR, merge, and closure

- [ ] **T018 Create PR**
  Push `work/925-qte-practice-mode`, create a PR to `main` that closes #925, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T019 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge with `[skip ci]`, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #925 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T020 Post issue evidence comment and cleanup**
  Comment closure evidence on #925, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
