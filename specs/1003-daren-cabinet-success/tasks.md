# Tasks: Daren Cabinet Lock Success Literary Aftermath

**Input**: `specs/1003-daren-cabinet-success/spec.md`, `plan.md`, issue #1003 body, parent #955 context, source scene #974, sibling issues #1004/#1005, completed downstream siblings #1006/#1007/#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), siblings [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004) and [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), completed downstream siblings [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1003-daren-cabinet-success`, read governing repo instructions, and select #1003 after #1008 closure/report verification.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1003-daren-cabinet-success/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `lock_pick` success result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1003 guard fails for the expected reason.

## Phase 3: User Story 1 - Clean Cabinet Lock Success Aftermath (Priority: P1)

**Goal**: Replace only `lock_pick_action` success text with a substantial Russian dark-fantasy clean cabinet-lock aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `lock_pick` action success result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, partial/fail result surfaces, or downstream rune-memory result surfaces.
- [X] T014 Verify `specs/1003-daren-cabinet-success/` references #1003/#955/#974/#1004/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [X] T015 Verify #1004/#1005, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1003-daren-cabinet-success`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1003 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1003, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1003-daren-cabinet-success` started from `origin/main` at `aaea4d8`; source issue #1003 and parent #955 are the tracked tasks. #1008 / PR #1029 were verified merged/closed/reported before selecting #1003.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1003.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 60 passed / 0 failed / 0 skipped / 60 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 329 passed / 0 failed / 0 skipped / 329 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1003-daren-cabinet-success\\specs\\1003-daren-cabinet-success` with `contracts/` and `tasks.md`.
- T005/T006 RED guard: added `DarenLockPickSuccess_ReadsAsCleanCabinetAftermathWithoutMechanicDrift`; focused command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected: 60 passed / 1 failed / 0 skipped / 61 total. Failure: `Assert.NotEqual()` because the success text still equaled `Штифты становятся ровно, и Дарен открывает кабинет без следа, так тихо, что пыль на ручке не дрожит.`
- T007: Replaced only `lock_pick_action` success prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; measured result is 1,909 characters / 16 sentences / 4 `Дарен` mentions / 298 words.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 61 passed / 0 failed / 0 skipped / 61 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 330 passed / 0 failed / 0 skipped / 330 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T012 working-tree code-focused added-line scan over `BookOfEternityClient` and `BookOfEternityClient.Tests` for hardcoded secrets, shell execution, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`.
- T013 scope inspection: source diff changes only the `lock_pick_action` success string and a focused Daren test/long-aftermath guard; partial/fail sibling strings, downstream `rune_memory_action` prose, mechanics/config/routing/score deltas/rewards/endpoints/runtime state/frontend/browser files are unchanged.
- T014/T015 reference and boundary inspection: `rg` confirms #1003/#955/#974/#1004/#1005/#1006/#1007/#1008 are referenced in the feature artifacts; #1004/#1005, other follow-ups, parent #955 lifecycle, PR, merge, issue closure, labels, and cleanup remain Hermes-owned/out of scope.
- T011 post-commit diff check: `git diff --check origin/main...HEAD` => passed with exit code 0.
- T012 post-commit code-focused added-line scan over `origin/main...HEAD` for hardcoded secrets, shell execution, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`.
- T016: Created one focused commit with `[skip ci]` in the commit message; final amended commit hash is reported by Codex final output.
- T017 independent review: detached Codex review in `E:/Games/worktrees/boe-1003-daren-cabinet-success-review` returned `APPROVED` with no Critical/Important/Minor findings. Reviewer reran focused Daren tests: 61 passed / 0 failed / 0 skipped, verified `git diff --check aaea4d8...HEAD` clean, and confirmed `HEAD=e98b1131ebd674a9e0ebabd170b22e5bd78c3092`. Spec Kit prerequisite check was inspected directly because the detached review checkout is not on a feature branch.
- T018 PR: created [PR #1030](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1030) with local verification evidence, independent-review verdict, explicit local-gated/no-Actions note, `Closes #1003`, and non-closing references for #955/#974/#1004/#1005/#1006/#1007/#1008.
