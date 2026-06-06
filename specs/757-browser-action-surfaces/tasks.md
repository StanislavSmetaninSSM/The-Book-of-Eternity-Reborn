# Tasks: Browser Action Result Surfaces (#757)

**Input**: `specs/757-browser-action-surfaces/spec.md`, `specs/757-browser-action-surfaces/plan.md`
**Source issue:** [#757](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/757)

## Format: `[ID] [P?] [Story] Description`

- **[P]** means the task can run in parallel because it touches separate files.
- Stories map to the independently testable scenarios in `spec.md`.

## Phase 1: Investigation and RED Tests

- [x] T001 [US1] Inspect current selected-action/result rendering paths in `BookOfEternityClient.WebFrontend/src/context/ShellContext.tsx`, `src/components/CommandResultView.tsx`, `src/components/CommandResult.tsx`, `src/components/BlockRenderer.tsx`, `src/utils/playerCopy.ts`, and `src/playerFacingCommandResult.ts`. Root cause/current behavior recorded in `plan.md`: the primary React shell path already preserved blocks, while the shared default sanitizer still encoded the old drop-safe-blocks default.

- [x] T002 [US1] RED: add/update a focused frontend test proving a safe read-only `ExplorerCommandResult` block remains visible in default player mode. RED evidence recorded in `plan.md`: focused player-facing command failed on the new safe-block assertion before production code changed.

- [x] T003 [US3] RED: add/update a focused frontend/source-guard test proving unsafe technical/raw blocks are hidden, sanitized, or replaced in default player mode while safe blocks remain visible.

- [x] T004 [US2] RED: existing focused source guard coverage in `commandResultViewSections.test.ts` proves result/prompt surfaces still use `executeCommand`, `browserApi.submitPromptSession`, and `browserApi.cancelPromptSession`; no React gameplay handlers were added.

## Phase 2: Implementation

- [x] T005 [US1] GREEN: implemented the minimal presentation-layer change to preserve/render safe blocks by default and keep unsafe details out of default UI. C# gameplay/runtime authority unchanged.

- [x] T006 [US2] GREEN: prompt rendering behavior for live sessions and read-only summaries preserved; submitting/cancelling still uses existing browser API calls.

- [x] T007 [US3] GREEN: raw endpoint/API/protocol/file/raw-JSON/debug wording remains out of default sanitized result surfaces; advanced-mode diagnostics were not changed.

- [x] T008 [P] REFACTOR: no refactor was needed after GREEN; keeping the change minimal avoided broadening scope.

## Phase 3: Verification and Evidence

- [x] T009 Run focused new/updated frontend tests and record RED/GREEN evidence in `plan.md`.

- [x] T010 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record the typecheck/test/build counts in `plan.md`.

- [x] T011 Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~CommandResult"` and record the non-zero pass/fail/skip counts in `plan.md`.

- [x] T012 Run `git diff --check origin/main...HEAD` and an added-line static security scan excluding docs/spec false positives. Pre-commit `git diff --check` and added-line scan passed; final commit-range check remains part of the commit gate.

- [x] T013 [P] If the result surface visibly changes, generate a dependency-light visual smoke artifact under `TestResults/browser-smoke/` and document whether it is committed or intentionally left untracked. Not generated: sanitizer behavior changed, not layout/styling/modal rendering.

- [x] T014 Reconcile Spec Kit: update `specs/757-browser-action-surfaces/plan.md` and this `tasks.md` with final evidence. Mark tasks complete only after evidence exists.

- [x] T015 Commit one focused implementation with `[skip ci]` in the commit message. Hermes owns independent review, PR creation/merge, and issue closure.

## Out-of-Scope Guard

Do not implement the broader interactive parity child issues (#805–#816) in this change. If #757 investigation reveals a missing command/write-handler, record the gap and leave it to the relevant child issue unless it is required to prove the selected-result surface itself.
