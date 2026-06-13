# Tasks: Daren Rune Memory Fail Literary Aftermath

**Input**: `specs/1008-daren-rune-fail/spec.md`, `plan.md`, issue #1008 body, parent #955 context, source scene #975, completed siblings #1006/#1007, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), completed siblings [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006) and [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1008-daren-rune-fail`, read governing repo instructions, and select #1008 after #1007 closure/report verification.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1008-daren-rune-fail/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `rune_memory` fail result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1008 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Rune Memory Fail Aftermath (Priority: P1)

**Goal**: Replace only `rune_memory_action` fail text with a substantial Russian dark-fantasy dangerous rune/ward aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `rune_memory` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/partial result surfaces.
- [X] T014 Verify `specs/1008-daren-rune-fail/` references #1008/#955/#975/#1006/#1007 and that tasks/checklists match the final diff.
- [X] T015 Verify #1006/#1007, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1008-daren-rune-fail`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1008 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1008, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1008-daren-rune-fail` started from `origin/main` at `05c0fe0`; source issue #1008 and parent #955 are the tracked tasks. #1007 / PR #1028 were verified merged/closed/reported before selecting #1008.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1008.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 59 passed / 0 failed / 0 skipped / 59 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 328 passed / 0 failed / 0 skipped / 328 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1008-daren-rune-fail\\specs\\1008-daren-rune-fail` with `contracts/` and `tasks.md`.
- T005: Added `DarenRuneMemoryFail_ReadsAsDangerousRuneAftermathWithoutMechanicDrift` to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; the guard rejects the retired one-sentence fail text, asserts grouped rune/ward/body/evidence/alarm/Renara motifs, and pins route/action/check/config/routing/score invariants.
- T006 RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected against the old text: 59 passed / 1 failed / 0 skipped / 60 total. Failure: `Daren rune_memory fail should not keep the old one-sentence result notification.`
- T007: Replaced only `rune_memory_action` fail prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a dangerous Russian rune/ward aftermath. Success and partial raw result strings were not edited.
- T008 GREEN focused Daren: same focused command passed after implementation: 60 passed / 0 failed / 0 skipped / 60 total.
- T009 affected C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 329 passed / 0 failed / 0 skipped / 329 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors.
- T011 whitespace: `git diff --check origin/main...HEAD` returned no output before implementation commit; working-tree `git diff --check` also returned no whitespace errors before commit. Final post-commit rerun is reported by Codex.
- T012 added-line source/test static scan for hardcoded secrets, shell execution, eval, unsafe deserialization, and SQL formatting returned `NO_MATCHES`.
- T013 diff scope: source/test diff touches only `BookOfEternityClient/Services/QteSceneService.Daren.cs` fail prose and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` guard/helper expectations; no route ids, beat order, action id/label, check type, Perception characteristic, difficulty/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser files, or success/partial raw result strings changed.
- T014 Spec Kit references: `spec.md`, `plan.md`, `tasks.md`, contract, and checklist continue to reference #1008 with parent #955, source #975, and sibling #1006/#1007 boundaries.
- T015 out of scope: no #1006/#1007 sibling result rewrite, other Daren result/scene follow-up, parent #955 lifecycle action, frontend change, runtime contract change, or GM-facing docs/examples update was performed.
- T016 commit: one focused `[skip ci]` commit is created after these evidence updates; final commit hash is reported in the Codex final response.
- T017 independent review: detached Codex review in `E:/Games/worktrees/boe-1008-daren-rune-fail-review` returned `APPROVED` with no Critical/Important/Minor findings. Reviewer reran focused Daren tests: 60 passed / 0 failed / 0 skipped, verified `git diff --check 05c0fe0...HEAD` clean, and confirmed final review worktree status clean. Spec Kit prerequisite check was inspected directly because the detached review checkout is not on a feature branch.
- T018 PR: created [PR #1029](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1029) with local verification evidence, independent-review verdict, explicit local-gated/no-Actions note, `Closes #1008`, and non-closing references for #955/#975/#1006/#1007.
