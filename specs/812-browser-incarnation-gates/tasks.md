# Tasks: Browser Shining Abode Incarnation Gates

**Input**: GitHub issue [#812](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/812), umbrella [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817), [spec.md](spec.md), [plan.md](plan.md)
**Prerequisites**: Existing console Gates flows, browser #810/#811 Shining parity patterns, constitution, repository AGENTS.md

## Phase 1: Setup & Spec Kit

- [x] T001 Confirm source branch `task/812-browser-incarnation-gates`, clean tracked worktree, and open GitHub issue #812.
- [x] T002 Inspect constitution, AGENTS.md, issue body, existing #811 Spec Kit/browser Shining action pattern, and console Gates lifecycle source.
- [x] T003 Create #812 Spec Kit artifacts linked to issue #812/#817 and scoped to existing Shining Gates/core-action contract reuse.
- [x] T004 Run focused baseline before implementation and record exact counts.

## Phase 2: RED Tests First

- [x] T005 Add browser parity tests for open Gates prompt and `ActionTypeOpenGates` pending request write.
- [x] T006 Add browser parity tests for blessing card select and deselect prompt submissions using existing `ShiningAbodeState` semantics.
- [x] T007 Add browser parity tests for Gates draft reroll using existing `TryRerollGatesDraft` semantics.
- [x] T008 Add browser parity tests for prepare-incarnation-package prompt and `ActionTypePrepareIncarnationPackage` pending request write with selected card snapshots.
- [x] T009 Add command-open and stale prompt-submit guard coverage for realm, pending core action, stale/closed draft, missing card, no-reroll, and prepared-package blockers.
- [x] T010 Add/update command coverage, command menu/help, API fixture, and source guard tests proving #812 actions are browser-supported and player-facing.
- [x] T011 Run focused RED command and record expected failing tests before production implementation.

## Phase 3: Minimal Implementation

- [x] T012 Add #812 Gates lifecycle command descriptors and player-facing metadata/aliases.
- [x] T013 Add prompt-session local UI lock coverage for #812 mutating commands.
- [x] T014 Implement browser prompt builders for open Gates, blessing select/deselect, reroll, and prepare incarnation package.
- [x] T015 Implement browser write/local-mutation handlers that re-check state and use existing `ShiningCoreActionRequestState` / `ShiningAbodeState` authority.
- [x] T016 Update browser command coverage/help/menu/API fixtures so #812 actions are treated as covered guided forms/local mutations.
- [x] T017 Keep afterlife runtime contract shape unchanged; if this becomes impossible, update contract matrix/examples/manifest/docs tests before continuing.

## Phase 4: GREEN & Verification

- [x] T018 Run focused RED/GREEN test filter and record exact result counts.
- [x] T019 Run final focused Shining/browser/API parity sweep and record exact result counts.
- [x] T020 Run documentation-sensitive tests if any afterlife contract/doc-impacting surface changes, or record why not required.
- [x] T021 Run C# build commands for touched projects and record results.
- [x] T022 Run `git diff --check origin/main...HEAD` and added-line security scan excluding Spec Kit docs; record results.
- [x] T023 Run frontend verification only if frontend files or generated frontend-facing artifacts change; otherwise record why it was not run.
- [x] T024 Update this task list with completed task statuses and verification evidence.

## Phase 5: Commit & Handoff

- [x] T025 Inspect final `git diff --stat origin/main...HEAD` and `git status --short` for accidental run artifacts/caches.
- [x] T026 Create a local focused commit with `[skip ci]`.
- [ ] T027 Final Codex report includes summary, files changed, Spec Kit artifacts, exact verification results/counts, commit SHA, remaining risks/blockers, and note that PR/merge/issue closure remain with Hermes.

## Verification Evidence

- Baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"`
- Baseline result before implementation: passed, 0 failed / 368 passed / 0 skipped / 368 total.
- RED test command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningIncarnationGatesParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserWebUiParity|BrowserApiContractTests" --logger "console;verbosity=minimal"`
- RED result before production implementation: failed as expected, 17 failed / 35 passed / 0 skipped / 52 total. Failures covered missing browser commands/prompts, missing command coverage/help/menu/API fixture recognition, and missing #812 source-guard evidence.
- GREEN focused command: same focused command, passed, 0 failed / 52 passed / 0 skipped / 52 total.
- Final focused sweep: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"` passed, 0 failed / 373 passed / 0 skipped / 373 total.
- Documentation-sensitive sweep: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` passed, 0 failed / 99 passed / 0 skipped / 99 total. Runtime contract shape was unchanged; docs/prompts were not edited.
- Build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors.
- Diff/static checks: `git diff --check` passed with no whitespace errors, only Windows CRLF warnings for touched C# files. Added-line secret scan excluding `specs/**` found no token/secret/password/API-key/bearer/private-key patterns.
- Frontend verification: initial `npm run verify --prefix BookOfEternityClient.WebFrontend` failed because `node_modules` was absent and `tsc` was not installed. After `npm ci --prefix BookOfEternityClient.WebFrontend` installed 54 packages with 0 vulnerabilities, rerun passed: TypeScript checks passed, player-facing/vitest tests passed 29/29, and Vite production build passed.
- Commit/status: staged diff contained only the intended #812 source, tests, frontend fixtures, and Spec Kit files; local commit was created with `[skip ci]`.
- Spec consistency: issue #812/#817 links present in `spec.md`, `plan.md`, and `tasks.md`; implementation reused existing Shining core-action/Gates runtime contracts and made no afterlife contract shape change.

## Notes

- No sibling issue #813-#816 work is planned.
- No PR creation, merge, issue closure, cron edit, or task lifecycle closure is part of the Codex implementation run.
- Contract docs/examples are intentionally unchanged unless implementation changes the afterlife pending/control contract shape.
