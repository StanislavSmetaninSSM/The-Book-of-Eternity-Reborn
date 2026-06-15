# Tasks: Daren Scene 13 Full Literary Page

**Input**: `specs/981-daren-first-dash/spec.md`, `plan.md`, issue #981 body, parent #955 context, prior scene #980, and existing Daren route/test patterns.
**Tracked issue and related context**: [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980), next scene [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/981-daren-first-dash` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/981-daren-first-dash/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `pursuit` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #981 guard fails for the expected reason.

## Phase 3: User Story 1 - First Dash Literary Page (Priority: P1)

**Goal**: Replace `pursuit` with a substantial Russian dark-fantasy first-dash escape page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `pursuit` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/981-daren-first-dash/` references #981/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify #982-#983, result/aftermath issues #988-#1014, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/981-daren-first-dash`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #981 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #981, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/981-daren-first-dash` started from `origin/main` at `583bf5b`; source issue #981 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #981.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 80 passed / 0 failed / 0 skipped / 80 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 349 passed / 0 failed / 0 skipped / 349 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-981-daren-first-dash\\specs\\981-daren-first-dash` with `contracts/` and `tasks.md`.
- T005/T006 RED guard: added `DarenPursuit_ReadsAsFirstDashPageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 80 passed / 1 failed / 0 skipped / 81 total. Expected failure: current `pursuit` synopsis has 236 chars, 2 sentences, and 1 Daren mention, below the #981 full-page guard.
- T007/T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 81 passed / 0 failed / 0 skipped / 81 total. New `pursuit` prose metrics: 3495 chars, 31 sentences, 11 Daren mentions.
- T009 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 350 passed / 0 failed / 0 skipped / 350 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors.
- T011 whitespace check: `git diff --check origin/main...HEAD` exited 0 with no output before commit.
- T012 added-line static/security scan over `BookOfEternityClient/` and `BookOfEternityClient.Tests/` added lines returned `NO_MATCHES` for hardcoded secrets, shell execution, eval/exec, unsafe deserialization, and SQL string-formatting patterns. Git emitted LF-to-CRLF normalization warnings for the two edited C# files; `git diff --check` remained clean.
- T013 diff inspection: production diff changes only the `pursuit` `DarenShowcaseBeat.PlayerText` in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; action id, label, check type, characteristic, difficulty, config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, and frontend/browser files are unchanged.
- T014/T015 reconciliation: active Spec Kit artifacts continue to reference #981 and parent #955; #982-#983, result/aftermath #988-#1014, and parent #955 lifecycle remain out of scope. Hermes-owned T019-T021 are intentionally still unchecked until merge/closure/cleanup evidence exists.
- T016 commit: this focused local `[skip ci]` commit contains the #981 test, `pursuit` prose rewrite, and Spec Kit evidence only; final SHA is available from `git rev-parse HEAD` after commit creation.
- T017 independent review: delegate reviewer inspected issue criteria, Spec Kit artifacts, diff scope, and prose metrics; verdict `APPROVED`, with no Critical/Important/Minor findings. Reviewer reran `git diff --check origin/main...HEAD` and focused Daren tests => 81 passed / 0 failed / 0 skipped / 81 total.
- T018 PR: created https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1050 with `Closes #981`, local verification evidence, review verdict, and parent #955 / sibling scene scope boundaries.
