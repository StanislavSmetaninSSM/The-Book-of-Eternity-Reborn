# Tasks: Daren Heavy-Grate Fail Literary Aftermath

**Input**: `specs/1014-daren-heavy-grate-fail/spec.md`, `plan.md`, issue #1014 body, parent #955 context, source scene #977, preceding results #1012/#1013, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), preceding result [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), preceding result [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1014-daren-heavy-grate-fail` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1014-daren-heavy-grate-fail/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current terse `physical_pressure` fail result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1014 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Heavy-Grate Fail Aftermath (Priority: P1)

**Goal**: Replace only `physical_pressure_action` fail text with a substantial Russian dark-fantasy aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `physical_pressure` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/partial result surfaces.
- [X] T014 Verify `specs/1014-daren-heavy-grate-fail/` references #1014/#955/#977/#1012/#1013 and that tasks/checklists match the final diff.
- [X] T015 Verify #1012/#1013, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1014-daren-heavy-grate-fail`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1014 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1014, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1014-daren-heavy-grate-fail` started from `origin/main` at `3232f7b`; source issue #1014 and parent #955 are the tracked tasks. Hermes read `AGENTS.md`, checked pause/complete sentinels, fetched `origin/main`, verified GitHub auth, and verified #1013 was already closed/reported before selecting #1014.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1014.
- T003 focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 53 passed / 0 failed / 0 skipped / 53 total.
- T003 affected baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 322 passed / 0 failed / 0 skipped / 322 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-1014-daren-heavy-grate-fail\specs\1014-daren-heavy-grate-fail` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default.
- T005: Added `DarenPhysicalPressureFail_ReadsAsDangerousAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; the guard pins action id/label, `MashInput`, Strength, difficulty/config, routing, score deltas, forbidden terms, and grouped fail-aftermath motifs.
- T006 RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 53 passed / 1 failed / 0 skipped / 54 total. Expected failure: `Daren physical_pressure fail should be a substantial dangerous aftermath insert, not a one-sentence result notification.`
- T007: Replaced only `physical_pressure_action` fail prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; production success and partial result strings, route mechanics, config, routing, score deltas, rewards, endpoints, runtime state, and frontend files were not edited.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 54 passed / 0 failed / 0 skipped / 54 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 323 passed / 0 failed / 0 skipped / 323 total.
- T010 client build command: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test build command: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 committed-range whitespace check: `git diff --check origin/main...HEAD` => exit 0 / no output.
- T012 added-line static scan over changed non-Spec source/test files from `origin/main...HEAD` for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string-formatting => `NO_MATCHES`.
- T013 diff inspection: `BookOfEternityClient/Services/QteSceneService.Daren.cs` changes only the `physical_pressure_action` fail text. `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` adds the fail guard, adds `score` to forbidden player-facing terms, updates sibling tests away from the old fail sentence, and expands the existing transition length allowance for this one action.
- T014: `spec.md`, `plan.md`, `tasks.md`, `contracts/daren-result-aftermath.md`, and `checklists/requirements.md` all reference #1014 with parent/context #955/#977/#1012/#1013 as applicable; final diff matches the active feature scope.
- T015: #1012/#1013 remain limited to existing success/partial prose, other Daren follow-ups are untouched, parent #955 remains lifecycle-owned by Hermes and out of this implementation scope.
- T016: Prepared one focused implementation commit with `[skip ci]` on branch `work/1014-daren-heavy-grate-fail`; final commit SHA is reported by Codex after the post-commit verification checks.
- T017: Independent review in detached worktree `E:/Games/worktrees/boe-1014-daren-heavy-grate-fail-review-100546` approved HEAD `f426735fb675bdc9a7ee4e59bf8c883e074217a8` with no Critical/Important findings. Reviewer verified scope, literary quality, forbidden-term absence, route/action contract preservation, beat-order preservation, grouped test coverage, Spec Kit artifact alignment, and supplied fresh local verification evidence.
- T018: Created PR #1023 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1023`) against `main` from `work/1014-daren-heavy-grate-fail`; PR body includes local verification evidence, `Closes #1014`, and non-closing parent/context references for #955/#977/#1012/#1013.
