# Tasks: Daren Heavy-Grate Partial Literary Aftermath

**Input**: `specs/1013-daren-heavy-grate-partial/spec.md`, `plan.md`, issue #1013 body, parent #955 context, source scene #977, preceding result #1012, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), preceding result [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), sibling result follow-up [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1013-daren-heavy-grate-partial` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1013-daren-heavy-grate-partial/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `physical_pressure` partial result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1013 guard fails for the expected reason.

## Phase 3: User Story 1 - Costly Heavy-Grate Partial Aftermath (Priority: P1)

**Goal**: Replace only `physical_pressure_action` partial text with a substantial Russian dark-fantasy aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `physical_pressure` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/fail result surfaces.
- [X] T014 Verify `specs/1013-daren-heavy-grate-partial/` references #1013/#955/#977/#1012 and that tasks/checklists match the final diff.
- [X] T015 Verify #1012/#1014, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1013-daren-heavy-grate-partial`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1013 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1013, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1013-daren-heavy-grate-partial` started from `origin/main` at `30b6622`; source issue #1013 and parent #955 are the tracked tasks. Hermes read `AGENTS.md`, checked pause/complete sentinels, fetched `origin/main`, verified GitHub auth, and verified #1012 was already closed/reported before selecting #1013.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1013.
- T003 focused Daren baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 52 passed / 0 failed / 0 skipped / 52 total.
- T003 affected baseline command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 321 passed / 0 failed / 0 skipped / 321 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-1013-daren-heavy-grate-partial\specs\1013-daren-heavy-grate-partial` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default.
- T005: Added `DarenPhysicalPressurePartial_ReadsAsCostlyAftermathWithoutMechanicDrift` to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, preserving action/check/config/routing/score assertions and grouped motif coverage for the costly partial aftermath. Removed the obsolete old one-sentence partial pin from the success guard and allowed the generic `physical_pressure_action` partial transition to exceed the old compact-result limit.
- T006 RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 52 passed / 1 failed / 0 skipped / 53 total. Expected failure: `DarenPhysicalPressurePartial_ReadsAsCostlyAftermathWithoutMechanicDrift` rejected the old one-sentence mixed-result notification as not substantial.
- T007: Replaced only the `physical_pressure_action` partial result string in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a three-paragraph Russian aftermath insert. The success and fail result strings, scene opening, route mechanics, config, routing, score deltas, rewards, endpoints, runtime state, and frontend/browser code were not changed.
- T008 GREEN focused command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 53 passed / 0 failed / 0 skipped / 53 total. One interim focused run failed on a motif-wording mismatch (`выходит/выводит` vs. extraction guard); root cause was wording, fixed by changing the prose to `Дарен вывел добычу к себе`.
- T009 affected-slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 322 passed / 0 failed / 0 skipped / 322 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 final diff hygiene command: `git diff --check origin/main...HEAD` => exit 0, no findings.
- T012 added-line static scan over changed non-Spec source/test files for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string-formatting => `NO_MATCHES`.
- T013 diff review: code/test diff is limited to the new partial guard, the helper max-length allowance for `physical_pressure_action` partial prose, removal of the obsolete old partial pin from the success test, and the single shared C# partial-result rewrite.
- T014: Verified the active Spec Kit directory references #1013, #955, #977, #1012, and #1014, and reconciled this task log/checklist with the implementation scope.
- T015: Verified #1012 success text, #1014 fail text, other Daren scenes/results, parent #955 closure, frontend/browser code, GM-facing docs/examples, endpoints, state files, and dialogue runtime remain out of scope.
- T016: Created one focused implementation commit with message `feat(daren): rewrite heavy-grate partial aftermath [skip ci]`; final SHA is reported by Codex after amend/verification.
- T017 independent review: detached worktree `E:/Games/worktrees/boe-1013-daren-heavy-grate-partial-review` at `4fbaf9d`; reviewer verdict `APPROVED`, with no Critical or Important findings. Review confirmed scope stayed limited to the partial result prose/tests/Spec Kit artifacts, no route/mechanics/browser/GM contract drift, and the Russian aftermath prose meets the mixed-success literary quality bar. Review command evidence included focused `DarenQteShowcaseTests` => 53 passed / 0 failed / 0 skipped / 53 total and `git diff --check origin/main...HEAD` => exit 0.
- T018 PR created: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1022 with local verification evidence, review verdict, `Closes #1013`, and safe non-closing references for #955/#977/#1012/#1014.
