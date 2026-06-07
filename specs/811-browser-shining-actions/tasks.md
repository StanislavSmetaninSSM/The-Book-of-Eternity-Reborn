# Tasks: Browser Shining Abode Actions

**Input**: GitHub issue [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console Shining action flows, browser #810 Shining politics parity patterns, constitution, repository AGENTS.md

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm clean worktree, source branch, and open GitHub issue #811.
- [x] T002 Inspect constitution, AGENTS.md, issue body, existing #805-#810 Spec Kit patterns, console flows, and browser prompt/write patterns.
- [x] T003 Create #811 Spec Kit artifacts linked to issue #811 and scoped to existing Shining core action contract reuse.

## Phase 2: RED Tests First

- [x] T004 Add browser parity tests for native faction discovery prompt and pending request write.
- [x] T005 Add browser parity tests for faction investment prompt filtering and pending request write.
- [x] T006 Add browser parity tests for project support, unsupport, and retirement prompt filtering and pending request writes.
- [x] T007 Add direct command-open realm guard and stale prompt-submit guard coverage for the #811 commands.
- [x] T008 Add/update command coverage, command menu/help, and source guard tests proving #811 actions are browser-supported and reuse `ShiningCoreActionRequestState`.
- [x] T009 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T010 Add #811 Shining action command descriptors and player-facing metadata.
- [x] T011 Add prompt-session local UI lock coverage for the #811 mutating commands.
- [x] T012 Implement C# Shining action prompt builders that enumerate canonical visible eligible factions/projects and quote existing costs.
- [x] T013 Implement browser write handlers that re-check realm and submit through existing `ShiningCoreActionRequestState` validation/write authority.
- [x] T014 Update browser command coverage/help gaps so #811 actions are treated as covered guided forms.
- [x] T015 Keep afterlife runtime contract shape unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T016 Run focused RED/GREEN test filter and record exact result counts.
- [x] T017 Run final focused Shining/browser parity sweep and record exact result counts.
- [x] T018 Run `dotnet build` for touched C# projects/solution and record result.
- [x] T019 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T020 Run frontend verification only if frontend files are changed; otherwise record why it was not run.
- [x] T021 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T022 Inspect final `git diff --stat` and `git status --short`.
- [x] T023 Create a local focused commit with a concise message containing `[skip ci]`.
- [x] T024 Final report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Verification Evidence

- RED test command: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningActionsParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserWebUiParity" --logger "console;verbosity=minimal"` before production code. Result: failed as expected, 18 failed / 11 passed / 0 skipped / 29 total. Failures were missing #811 commands, prompt sessions, help/coverage rows, and Shining core writer source guard.
- GREEN focused test command: same focused command after implementation. Result: passed, 0 failed / 29 passed / 0 skipped / 29 total.
- Final parity sweep: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests" --logger "console;verbosity=minimal"`. Result: passed, 0 failed / 343 passed / 0 skipped / 343 total.
- Build: `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore` passed with 0 warnings / 0 errors; `dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore` passed with 0 warnings / 0 errors. The root command `dotnet build BookOfEternityClient.sln --no-restore` failed with MSB1009 because the solution file is under `BookOfEternityClient\`.
- Diff check: `git diff --check` passed with no whitespace errors; Git reported normal LF-to-CRLF working-copy warnings.
- Security scan: `git diff --unified=0 -- . ":(exclude)specs/811-browser-shining-actions/**" | Select-String -Pattern "password|passwd|secret|token|apikey|api_key|authorization|bearer|client_secret|connectionstring|private_key|BEGIN RSA|BEGIN OPENSSH" -CaseSensitive:$false` returned no matches; Git reported normal LF-to-CRLF working-copy warnings.
- Frontend verification: initial Codex run did not run it because no React/TypeScript source files changed; Hermes reconciliation then refreshed `game-screen.json` and `command-coverage.json` after `BrowserApiContractTests` caught fixture drift, installed frontend dependencies in the worktree with `npm ci --prefix BookOfEternityClient.WebFrontend`, and reran `npm run verify --prefix BookOfEternityClient.WebFrontend`: TypeScript passed, Vitest/player-facing tests passed 29/29, and Vite build succeeded.
- Hermes reconciliation Browser API contract gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserApiContractTests" --logger "console;verbosity=minimal"` passed 24/24 after fixture sync.
- Hermes reconciliation affected Shining/browser/API gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"` passed 367/367.
- Hermes reconciliation docs-sensitive gate: `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` passed 99/99, confirming no afterlife contract documentation update was required for pure browser parity over existing contracts.
- Hermes reconciliation builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` both passed with 0 warnings / 0 errors.
- Hermes reconciliation diff/static checks: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` found `specs/811-browser-shining-actions`; `git diff --check origin/main...HEAD` and `git diff --check` passed; added-line static scan found only internal C# `rawStateError` variable names, reviewed as non-player-facing/non-secret false positives; refined default player-facing raw diagnostic scan returned no matches.
- Spec consistency: manual pass confirmed issue #811 links in `spec.md`, `plan.md`, `tasks.md`, and supporting artifacts, plus no planned afterlife contract shape change.
- Commit: local focused commit created with `[skip ci]`; final SHA is updated by Hermes reconciliation after fixture-sync amend.
- Independent review fix: Codex review `E:/Games/codex-runs/20260607-1608-boe-811-shining-actions-review` returned `CHANGES_REQUIRED` because stale-submit Shining core action validation could leak internal pending/action wording. Hermes added `SubmitAsync_ShiningActionPromptAfterAnotherPendingCoreActionAppears_ReturnsPlayerFacingBlocker`, expanded raw-diagnostic guards for Shining action ids/no-op/internal pending text, sanitized non-realm Shining core action validation messages to player-facing copy, and verified RED by temporarily restoring the previous conditional sanitizer: 1 failed / 0 passed with `pending Shining` leak; restored fix passed 1/1.

## Notes

- No sibling issue #812-#816 work is planned.
- No PR creation, merge, issue closure, cron edit, or task lifecycle closure is part of this Codex implementation.
- Contract docs/examples are intentionally unchanged unless the implementation changes the afterlife pending/control contract shape.
