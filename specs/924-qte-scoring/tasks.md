# Tasks: QTE Score Metrics and Ending Ranks

**Input**: `specs/924-qte-scoring/spec.md`, `specs/924-qte-scoring/plan.md`, `specs/924-qte-scoring/contracts/qte-scoring-contract.md`, issue [#924](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924)
**Source Issues**: #924, parent #911, consumers #919/#925, related #918
**Branch**: `work/924-qte-scoring`

## Phase 1: Setup and RED coverage

- [x] **T001 Baseline verification before Spec Kit edits**
  Evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 247/247. `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 52 packages and 0 vulnerabilities. `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with typecheck, player-facing tests 59/59, and Vite build.

- [x] **T002 Add failing validation tests for scored QTE offers**
  RED evidence 2026-06-10: added `ValidationServiceQteTests.ValidateAcceptedTurnQteOfferAsync_AcceptsValidScoreModel` plus malformed cases for duplicate/invalid metrics, invalid bounds, initial outside bounds, invalid visibility, unknown delta/threshold metrics, invalid grade keys/delta values, impossible thresholds, duplicate/missing fallback ranks, and bad rank order. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "QteSceneServiceTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` failed with 15 failed / 192 passed / 207 total; malformed score model cases failed because no `qte_score_*` validation issues were produced.

- [x] **T003 Add failing runtime/history tests for score application and final ranks**
  RED evidence 2026-06-10: added `QteSceneServiceTests.ResolveActiveActionAsync_AppliesScoreDeltasComputesRankAndWritesHistory` and `ResolveActiveActionAsync_LeavesUnscoredQteHistoryUnchanged`. The same focused run failed the scored runtime test with `KeyNotFoundException` at `activeScene.scoreState`, proving the runtime does not initialize score state yet; the unscored compatibility assertion remained in the focused suite.

- [x] **T004 Add failing browser/console player-facing projection tests if score surfaces are missing**
  RED evidence 2026-06-10: added `BrowserQteMiniGameContractTests.BuildReadOnlyStateAsync_ProjectsReadOnlyScoreStateWithVisibility`, which failed with `NullReferenceException` on missing `activeScene.scoreState` in the browser DTO projection. Added `qteScenePanelMiniGames.test.tsx` coverage for active score metrics and final rank; `npx vitest run test/qteScenePanelMiniGames.test.tsx` from `BookOfEternityClient.WebFrontend` failed 1/6 because `QteScenePanel` did not render `Счёт сцены`.

## Phase 2: C# score model contract and validation

- [x] **T005 Implement score model parsing/domain helpers**
  Implemented generic C# `QteScoreModel`, metric/rank/threshold/delta/state/audit/summary types in `QteSceneService` with no Daren, reward, achievement, Ink Feather, inventory, or practice persistence fields.

- [x] **T006 Implement score model validation**
  Implemented `qte_score_*` validation for score metrics, bounds, initial values, visibility, ranks, rank order, fallback ranks, thresholds, and action `scoreDeltas`. Existing unscored QTE validation remained in the same focused suite.

- [x] **T007 Verify validation tests GREEN**
  GREEN evidence 2026-06-10: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "QteSceneServiceTests|ValidationServiceQteTests|BrowserQteMiniGameContractTests" --logger "console;verbosity=minimal"` passed 211/211.

## Phase 3: Runtime score application and history

- [x] **T008 Apply score deltas during QTE action resolution**
  `BeginAcceptedSceneAsync` initializes score state from `scoreModel`; console and browser/local action resolution apply only the selected grade's deltas, clamp to metric bounds, and append C# audit entries.

- [x] **T009 Compute deterministic final rank and summary**
  Final rank selection evaluates `rankOrder` deterministically, falls back to the authored fallback rank, appends rank text to the completion/reminder summary, and writes final score plus audit into QTE history.

- [x] **T010 Verify runtime/history tests GREEN**
  GREEN evidence 2026-06-10: same focused C# command passed 211/211, including scored runtime/history and unscored compatibility assertions.

## Phase 4: Console/browser player-facing surfaces

- [x] **T011 Update console active/final score display**
  Active console QTE prelude now shows `always` visible metrics under Russian `Счёт сцены` copy with Spectre.Console markup escaping. Final completion/reminder summary includes the rank label.

- [x] **T012 Update browser DTO/contracts/UI for read-only score state**
  Browser DTOs now project read-only active score state and final score summary; TypeScript contracts/fixture and `QteScenePanel` render visible metrics/final rank while filtering hidden metrics from default UI.

- [x] **T013 Verify console/browser projection tests GREEN**
  GREEN evidence 2026-06-10: focused C# QTE/browser command passed 211/211. `npx vitest run test/qteScenePanelMiniGames.test.tsx` from `BookOfEternityClient.WebFrontend` passed 6/6.

## Phase 5: Docs, examples, and source guards

- [x] **T014 Update GM-facing QTE documentation and example**
  Updated `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, and `Examples/E_CLI_QTE_Offer.txt` with generic optional `scoreModel` authoring rules, metric visibility, score deltas, deterministic ranks, final score summary guidance, read-only browser scope, and an ordinary MashInput scored QTE example with no Daren/practice/reward-system fields.

- [x] **T015 Update documentation/source guard tests**
  RED evidence 2026-06-10: added `PromptDocumentationCoverageTests.ScoredQteContract_IsDocumentedForGmAndPlayers`; `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "ScoredQteContract_IsDocumentedForGmAndPlayers" --logger "console;verbosity=minimal"` failed 1/1 because `Rules/Block_CLI_QTE.txt` did not contain `scoreModel`.

- [x] **T016 Verify docs/example tests GREEN**
  GREEN evidence 2026-06-10: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "ScoredQteContract_IsDocumentedForGmAndPlayers|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` passed 6/6 after docs and example updates.

## Phase 6: Verification, review, and Spec Kit reconciliation

- [x] **T017 Run full focused local verification**
  Final evidence 2026-06-10:
  - `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` passed with `FEATURE_DIR=E:\Games\worktrees\boe-924-qte-scoring\specs\924-qte-scoring`.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings / 0 errors.
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings / 0 errors.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --no-restore --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 265/265.
  - Expanded final focused run including `BrowserQteMiniGameContractTests` passed 269/269.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: typecheck, player-facing tests 60/60, and Vite build.
  - `git diff --check` passed; `git diff --check origin/main...HEAD` passed for committed branch diff at the time of checking.
  - Added-line production-source credential scan found no secrets/tokens/passwords in production C#/frontend source.

- [x] **T018 Update this tasks file with RED/GREEN and final evidence**
  This tasks file now records RED evidence for validation/runtime/browser/docs guards and GREEN evidence for implementation, docs, frontend, and final verification gates. Hermes-owned review/PR/merge/closure tasks remain open.

- [ ] **T019 Independent pre-merge review**
  Hermes must obtain independent review after Codex implementation. Critical/Important findings block PR/merge until fixed and re-reviewed.

## Phase 7: Hermes-owned PR, merge, and closure

- [ ] **T020 Create PR**
  Push `work/924-qte-scoring`, create a PR to `main` that closes #924, and include local verification evidence plus `GitHub Actions: not used / not required`.

- [ ] **T021 Squash-merge and verify closure**
  After local gates and independent review are clean, squash-merge, delete the remote branch, fast-forward main, verify PR `MERGED` and issue #924 `CLOSED`/`COMPLETED`, and run post-merge focused verification on `main`.

- [ ] **T022 Post issue evidence comment and cleanup**
  Comment closure evidence on #924, remove temporary worktree/local branch when safe, prune stale branches, and report in Russian with next target selection rationale.
