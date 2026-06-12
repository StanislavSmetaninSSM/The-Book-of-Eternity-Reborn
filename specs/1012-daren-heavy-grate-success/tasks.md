# Tasks: Daren Heavy-Grate Success Literary Aftermath

**Input**: `specs/1012-daren-heavy-grate-success/spec.md`, `plan.md`, issue #1012 body, parent #955 context, source scene #977, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), sibling result follow-ups [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013) and [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1012-daren-heavy-grate-success` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1012-daren-heavy-grate-success/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [ ] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `physical_pressure` success result and pins mechanics.
- [ ] T006 Run focused Daren tests and record RED evidence showing the new #1012 guard fails for the expected reason.

## Phase 3: User Story 1 - Clean Heavy-Grate Success Aftermath (Priority: P1)

**Goal**: Replace only `physical_pressure_action` success text with a substantial Russian dark-fantasy aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [ ] T007 [US1] Replace only the `physical_pressure` action success result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [ ] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [ ] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [ ] T010 Run client/test-project build commands and record results.
- [ ] T011 Run `git diff --check origin/main...HEAD`.
- [ ] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [ ] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or partial/fail result surfaces.
- [ ] T014 Verify `specs/1012-daren-heavy-grate-success/` references #1012/#955/#977 and that tasks/checklists match the final diff.
- [ ] T015 Verify #1013/#1014, other result/scene follow-ups, and parent #955 remain out of scope.
- [ ] T016 Make one focused `[skip ci]` commit on `work/1012-daren-heavy-grate-success`.

## Hermes-Owned Lifecycle

- [ ] T017 Independent review of #1012 implementation and literary quality bar.
- [ ] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1012, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1012-daren-heavy-grate-success` started from `origin/main` at `30d9ee7`; source issue #1012 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1012.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 51 passed / 0 failed / 0 skipped / 51 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 320 passed / 0 failed / 0 skipped / 320 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-1012-daren-heavy-grate-success\specs\1012-daren-heavy-grate-success` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default.
