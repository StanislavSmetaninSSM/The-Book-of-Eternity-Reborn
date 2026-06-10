# Tasks: Browser QTE Interactive Mini-Games

**Input**: `specs/918-browser-qte-parity/spec.md`, `specs/918-browser-qte-parity/plan.md`, issue [#918](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918)
**Source Issues**: #918, parent #680, QTE v2 parent #911
**Branch**: `work/918-browser-qte-parity`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**
  Evidence: `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest player-facing slice 44/44 and Vite build success; `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 247/247.

- [x] **T002 Add failing C# browser QTE config projection tests**
  Evidence: added `BookOfEternityClient.Tests/BrowserQteMiniGameContractTests.cs`. RED command `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "FullyQualifiedName~BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` failed 2/2 because `checkConfig` was missing/null for supported and unknown actions.

- [x] **T003 Add failing frontend default-player tests for no manual grade selector**
  Evidence: added `BookOfEternityClient.WebFrontend/test/qteMiniGameHelpers.test.ts` and `test/qteScenePanelMiniGames.test.tsx`. RED command `npx vitest run test/qteMiniGameHelpers.test.ts test/qteScenePanelMiniGames.test.tsx` failed because `../src/qte/qteGradeHelpers` did not exist and `QteScenePanel` still rendered the old `Исход проверки` manual selector/buttons.

## Phase 2: C# Browser API projection

- [x] **T004 Extend `QteWebActionDto` with typed check config projection**
  Evidence: `BookOfEternityClient/WebUi/QteWebInteractionService.cs` now projects a read-only `checkConfig` JSON object for TimingBar, PromptChain, BalanceMeter, ChargeRelease, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, StealthNoise, LockPinSet, BranchChoice, and explicit Unsupported; endpoint/write semantics still route through the existing action resolver.

- [x] **T005 Update TypeScript API contracts and fixtures**
  Evidence: updated `BookOfEternityClient.WebFrontend/src/api/contracts.ts`, `src/api/contract-fixtures/qte-state.json`, and `BrowserApiContractTests` fixture generation for the normalized browser QTE check configs.

- [x] **T006 Verify C# config projection GREEN**
  Evidence: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "FullyQualifiedName~BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` passed 2/2; combined `BrowserQteMiniGameContractTests|BrowserApiContractTests` passed 27/27.

## Phase 3: Frontend mini-game model and components

- [x] **T007 Add pure mini-game grade helpers**
  Evidence: added `BookOfEternityClient.WebFrontend/src/qte/qteGradeHelpers.ts` with deterministic success/partial/fail helpers for all supported check families and focused helper tests.

- [x] **T008 Implement mini-game UI components**
  Evidence: added `BookOfEternityClient.WebFrontend/src/components/qte/QteMiniGame.tsx` plus scoped QTE styles for TimingBar, PromptChain, BalanceMeter, ChargeRelease, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, StealthNoise, LockPinSet, BranchChoice, and Unsupported player-facing surfaces.

- [x] **T009 Refactor `QteScenePanel.tsx` to use mini-game components**
  Evidence: `QteScenePanel.tsx` delegates active actions to `QteMiniGame`, removed the default grade dropdown/quick grade buttons, and submits computed grades through the existing `browserApi.resolveQteAction({ actionId, grade })` path.

- [x] **T010 Verify frontend mini-game tests GREEN**
  Evidence: `npx vitest run test/qteMiniGameHelpers.test.ts test/qteScenePanelMiniGames.test.tsx` passed 14/14 across 2 files after implementation.

## Phase 4: Documentation, guards, and UX integrity

- [x] **T011 Update docs/source guards only where behavior/API changed**
  Evidence: updated `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, `BookOfEternityClient.WebFrontend/README.md`, and `PromptDocumentationCoverageTests.cs` to replace stale "browser support not live" guidance with #918 browser mini-game/C# authority guidance; no GM-authored fields were added.

- [x] **T012 Add player-facing leak guard**
  Evidence: `test/qteScenePanelMiniGames.test.tsx` asserts supported default QTE markup excludes `/api/`, DTO, endpoint, debug, JSON, manual, grade selector, the old `Исход проверки`, quick grade buttons, and `<select`.

- [x] **T013 Add responsive/accessibility smoke evidence**
  Evidence: `test/qteScenePanelMiniGames.test.tsx` covers focusable mini-game regions, pointer buttons, and responsive QTE CSS wrappers (`flex-wrap`, `auto-fit` grids, `overflow: hidden`) for representative checks. No screenshot evidence is claimed.

## Phase 5: Verification and review

- [x] **T014 Run full frontend verification**
  Evidence: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: typecheck, `test:player-facing` with 8 Vitest files and 58/58 tests, and Vite production build.

- [x] **T015 Run C# build and focused QTE/browser/docs gate**
  Evidence: `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore` passed with 0 warnings/0 errors; `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings/0 errors; required focused `dotnet test` filter passed 247/247 after updating the frontend script source guard.

- [x] **T016 Run Spec Kit prerequisites and diff hygiene**
  Evidence: `.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-918-browser-qte-parity\specs\918-browser-qte-parity` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`; `git diff --cached --check` passed before commit; post-commit `git diff --check origin/main...HEAD` passed; added-line security scan on `origin/main...HEAD` reported no hits.

- [x] **T017 Update this tasks file with implementation evidence**
  Evidence: T002-T018 contain implementation, RED/GREEN, verification, hygiene, and review evidence. Hermes-owned PR/merge/closure tasks T019-T021 remain open.

- [x] **T018 Independent pre-merge review**
  Evidence: independent reviewer `Russell` found three issues: untracked new QTE files, fake finish/timeout buttons for timed v2 mini-games, and hard-coded browser projection stat tier. Fixes staged the new files, added real deadline/reveal behavior in `QteMiniGame.tsx`, and changed C# projection to use `QteSceneService.ResolveQteStatTierAsync` plus effective runtime helpers. Re-review reported all three findings resolved. Post-fix focused evidence: `npx vitest run test/qteMiniGameHelpers.test.ts test/qteScenePanelMiniGames.test.tsx` passed 14/14; `dotnet test ... --filter "FullyQualifiedName~BrowserQteMiniGameContractTests|FullyQualifiedName~BrowserApiContractTests"` passed 28/28.

## Phase 6: Hermes-owned PR, merge, and closure

- [ ] **T019 Create PR**
  Push `work/918-browser-qte-parity`, create a PR to `main` that closes #918, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T020 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #918 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T021 Post issue evidence comment and cleanup**
  Comment closure evidence on #918, remove the temporary worktree/local branch, prune stale branches, and report in Russian with next target selection rationale.
