# Tasks: Daren Cabinet Lock Partial Literary Aftermath

**Input**: `specs/1004-daren-cabinet-partial/spec.md`, `plan.md`, issue #1004 body, parent #955 context, source scene #974, completed sibling #1003, remaining sibling #1005, completed downstream siblings #1006/#1007/#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), completed sibling [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), remaining sibling [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), and completed downstream siblings [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/1004-daren-cabinet-partial`, read governing repo instructions, and select #1004 after #1003 closure.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/1004-daren-cabinet-partial/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `lock_pick` partial result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #1004 guard fails for the expected reason.

## Phase 3: User Story 1 - Mixed Cabinet Lock Partial Aftermath (Priority: P1)

**Goal**: Replace only `lock_pick_action` partial text with a substantial Russian dark-fantasy mixed cabinet-lock aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `lock_pick` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, success/fail result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/1004-daren-cabinet-partial/` references #1004/#955/#974/#1003/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify #1005, other result/scene follow-ups, and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` commit on `work/1004-daren-cabinet-partial`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1004 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1004, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1004-daren-cabinet-partial` started from `origin/main` at `d64bfe6`; source issue #1004 and parent #955 are the tracked tasks. #1003 / PR #1030 are already merged/closed in `main`.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1004.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 61 passed / 0 failed / 0 skipped / 61 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 330 passed / 0 failed / 0 skipped / 330 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1004-daren-cabinet-partial\\specs\\1004-daren-cabinet-partial` with `contracts/` and `tasks.md`.
- T005: Added `DarenLockPickPartial_ReadsAsMixedCabinetAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, kept the #1003 success guard focused on success while requiring partial trace signals, and allowed `lock_pick_action` partial to use long aftermath prose in the generic transition guard.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 61 passed / 1 failed / 0 skipped / 62 total. Expected failure: `DarenLockPickPartial_ReadsAsMixedCabinetAftermathWithoutMechanicDrift` rejected the old exact text `Замок сдаётся, но отмычка царапает накладку; Дарен уносит этот след вместе с тревогой.`
- T007: Replaced only `lock_pick_action` partial prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. Built route-data measurement for the new partial aftermath: 2416 characters, 25 scene sentences, 7 `Дарен` mentions, 386 words.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 62 passed / 0 failed / 0 skipped / 62 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 331 passed / 0 failed / 0 skipped / 331 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 post-commit diff check: `git diff --check origin/main...HEAD` => passed with no output.
- T012 pre-commit and post-commit code added-line static scans over `BookOfEternityClient` and `BookOfEternityClient.Tests` returned `NO_MATCHES`.
- T013 diff scope review: changed files are limited to `BookOfEternityClient/Services/QteSceneService.Daren.cs`, `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, and this #1004 Spec Kit directory. The C# diff changes only the `lock_pick_action` partial result text plus tests; no frontend/browser files, endpoints, runtime state, reward/profile/New Game grants, route order, action id, check type/config, routing, score deltas, #1003 success prose, #1005 fail prose, or #1006/#1007/#1008 rune-memory prose changed.
- T014/T015: `spec.md`, `plan.md`, `tasks.md`, contract, and checklist reference #1004/#955/#974/#1003/#1005/#1006/#1007/#1008. #1005 fail rewrite, other Daren result/scene follow-ups, parent #955 lifecycle, independent review, PR, merge, issue closure, and cleanup remain Hermes-owned and out of Codex implementation scope.
- T016: Created one focused commit with message `Rewrite Daren cabinet partial aftermath [skip ci]`; final amended SHA is reported in the Codex final response.
- T017 independent review: detached Codex review in `E:/Games/review-worktrees/boe-1004-daren-cabinet-partial-review` returned `APPROVED` with no Critical/Important/Minor findings. Reviewer verified scope/literary mixed-outcome quality, reran focused Daren `62 passed / 0 failed / 0 skipped`, affected slice `331 passed / 0 failed / 0 skipped`, `git diff --check origin/main...HEAD` clean, production added-line meta-term scan `NO_MATCHES`, and code/test security static scan `NO_MATCHES`. Detached review could not run feature-branch Spec Kit prerequisite checks because it was on detached `HEAD`; Hermes implementation-worktree Spec Kit prerequisite evidence remains authoritative.
- T018 PR: created PR #1031 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1031`) against `main` with `Closes #1004`, local verification evidence, independent-review verdict, GitHub Actions not-required note, and non-closing references for parent/sibling context. GitHub readback showed `closingIssuesReferences` includes #1004 and `mergeStateStatus=CLEAN` before this lifecycle-evidence amend.
