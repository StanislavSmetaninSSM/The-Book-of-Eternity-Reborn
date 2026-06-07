# Tasks: Browser Shining Abode Relic Forge

**Input**: GitHub issue [#813](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/813), umbrella [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console forge flow, browser #810/#811/#812 Shining parity patterns, constitution, repository AGENTS.md

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm source branch `task/813-browser-relic-forge`, clean tracked worktree, and open GitHub issue #813.
- [x] T002 Inspect constitution, AGENTS.md, issue body, existing browser Shining parity patterns, and console relic forge source.
- [x] T003 Create #813 Spec Kit artifacts linked to issue #813/#817 and scoped to existing Shining forge/core-action contract reuse.
- [x] T004 Run focused baseline before implementation and record exact counts.

## Phase 2: RED Tests First

- [x] T005 Add browser parity test for opening the Shining relic forge prompt with player-facing faction/action/relic choices.
- [x] T006 Add browser parity tests for reshape submit, `ActionTypeForgeRelicReshape`, `targetFormTag`, quoted costs, and relic-reroll commit behavior.
- [x] T007 Add browser parity tests for retune submit, `ActionTypeForgeRelicRetuneProperty`, `propertyIndex`, `replacementProperty`, and optional reroll commit behavior.
- [x] T008 Add browser parity tests for strengthen submit, `ActionTypeForgeRelicStrengthenBand`, `propertyIndex`, and quoted costs.
- [x] T009 Add browser parity tests for stabilize submit, `ActionTypeForgeRelicStabilizeEcho`, and absence of browser-only relic mutation.
- [x] T010 Add browser parity tests for uplift submit, `ActionTypeForgeRelicUpliftRarity`, `addedProperties`, and quoted costs.
- [x] T011 Add command-open and stale prompt-submit guard coverage for realm, pending core action/local write, missing relic, invalid action, invalid property, invalid target form, exhausted reroll, and insufficient-resource blockers.
- [x] T012 Add/update command coverage, command menu/help, API fixture, and source guard tests proving #813 actions are browser-supported and player-facing.
- [x] T013 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T014 Add #813 forge command descriptor(s), aliases, and player-facing metadata.
- [x] T015 Add prompt-session local UI lock coverage for #813 mutating forge command(s).
- [x] T016 Implement browser prompt builders for faction, forge action, relic, reshape target form/reroll, retune property/replacement/reroll, strengthen property, stabilize confirmation, uplift additional properties, and final confirmation.
- [x] T017 Implement browser write handlers that re-check state and use existing `ShiningAbodeState.TryQuoteForgeAction` / `ShiningCoreActionRequestState` / `WriteForgeRequestWithRelicRerollCommitAsync` authority.
- [x] T018 Update browser command coverage/help/menu/API fixtures so #813 forge actions are treated as covered guided forms while #817 remains open.
- [x] T019 Keep afterlife runtime contract shape unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T020 Run focused RED/GREEN test filter and record exact result counts.
- [x] T021 Run final focused Shining/browser/API parity sweep and record exact result counts.
- [x] T022 Run documentation-sensitive tests if any afterlife contract/doc-impacting surface changes, or record why not required.
- [x] T023 Run C# build commands for touched projects and record results.
- [x] T024 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T025 Run frontend verification if frontend files or generated frontend-facing artifacts change; otherwise record why it was not run.
- [x] T026 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T027 Inspect final `git diff --stat origin/main...HEAD` and `git status --short` for accidental run artifacts/caches.
- [x] T028 Create a local focused commit with `[skip ci]`.
- [ ] T029 Final Codex report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Verification Evidence

- Spec Kit setup: `specs/813-browser-relic-forge/spec.md`, `plan.md`, and `tasks.md` created by Hermes on 2026-06-07 from issue #813/#817 before implementation.
- Baseline command before production changes: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningTradeAndForge|BrowserAfterlifeWriteServiceTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- Baseline result before implementation: passed, 0 failed / 199 passed / 0 skipped / 199 total. Restore/build ran first in the fresh worktree and produced test binaries normally.
- Spec Kit prerequisite check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-813-relic-forge\specs\813-browser-relic-forge` and `AVAILABLE_DOCS=["tasks.md"]`.
- RED command to run after adding failing tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningRelicForgeParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- Final focused sweep command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningTradeAndForge|BrowserAfterlifeWriteServiceTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- Documentation-sensitive sweep command if contract/doc-impacting surfaces changed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- Build commands: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`.
- Frontend verification command if frontend files or fixtures change: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- RED tests added before production code in `BookOfEternityClient.Tests/WebUi/BrowserShiningRelicForgeParityTests.cs`.
- RED command after test addition: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningRelicForgeParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- RED result before production implementation: failed as expected, 15 failed / 35 passed / 0 skipped / 50 total. Expected failures showed `/shining_relic_forge` and `/сияющая_ковка` were unregistered, prompt/session fields were absent, help/coverage lacked #813, and `BrowserAfterlifeWriteService` did not yet call `ShiningAbodeState.TryQuoteForgeAction` or `WriteForgeRequestWithRelicRerollCommitAsync`.
- GREEN command after implementation and fixture regeneration: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningRelicForgeParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- GREEN result after implementation: passed, 0 failed / 50 passed / 0 skipped / 50 total.
- Final focused Shining/browser/API sweep command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningTradeAndForge|BrowserAfterlifeWriteServiceTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- Final focused Shining/browser/API sweep result: passed, 0 failed / 199 passed / 0 skipped / 199 total.
- Documentation-sensitive sweep command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- Documentation-sensitive sweep result: passed, 0 failed / 99 passed / 0 skipped / 99 total. This was run even though runtime afterlife contract shape remained unchanged.
- C# build command: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- C# test build command: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`; result: succeeded, 0 warnings / 0 errors.
- Frontend dependency command: `npm ci --prefix BookOfEternityClient.WebFrontend`; result: added 54 packages, audited 55 packages, 0 vulnerabilities.
- Frontend verification command: `npm run verify --prefix BookOfEternityClient.WebFrontend`; result: passed `typecheck`, `test:player-facing`, `vitest` 2 files / 29 tests, and `vite build`.
- Post-verification cleanup: removed ignored `BookOfEternityClient.WebFrontend/node_modules` and `BookOfEternityClient.WebFrontend/dist`; both paths then returned `False` from `Test-Path`.
- Final Spec Kit prerequisite check command: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`; result: `FEATURE_DIR=E:\Games\worktrees\boe-813-relic-forge\specs\813-browser-relic-forge`, `AVAILABLE_DOCS=["tasks.md"]`.
- Diff whitespace check command: `git diff --check origin/main...HEAD`; result: no findings.
- Added-line security scan command: `git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/**' | Select-String -Pattern '^\+[^+].*(password|secret|token|api[_-]?key|authorization|bearer|private key|connectionstring)' -CaseSensitive:$false`; result: no matches.
- Pre-commit staged status: `git status --short --branch` showed only the intended staged files for source, tests, frontend fixtures, and `specs/813-browser-relic-forge/`; ignored `node_modules` and `dist` were absent.
- Pre-commit staged stat: `git diff --cached --stat` showed 13 files changed, 2096 insertions, 11 deletions.
- Runtime afterlife contract shape: unchanged. The implementation writes the existing `pending_shining_abode_actions.json` core-action request shape through `WriteForgeRequestWithRelicRerollCommitAsync`; no pending/control/action field, receipt, validator rule, normalizer side effect, or GM-facing contract shape was added or renamed.

## Notes

- Sibling issues #814-#816 and umbrella #817 closure are out of scope.
- Contract docs/examples are intentionally unchanged unless implementation changes the afterlife pending/control contract shape.
- PR creation, merge, issue closure, cron edit, or task lifecycle closure is not part of the Codex implementation run.
