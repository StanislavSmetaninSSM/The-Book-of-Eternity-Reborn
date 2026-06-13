# Tasks: Daren Renara Voice Partial Literary Aftermath

**Input**: `specs/1010-daren-renara-partial/spec.md`, `plan.md`, issue #1010 body, parent #955 context, source scene #976, completed success sibling #1009, fail sibling #1011, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), completed success sibling [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009), and fail sibling [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1010-daren-renara-partial` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1010-daren-renara-partial/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `ward_steward_parley` partial result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1010 guard fails for the expected reason.

## Phase 3: User Story 1 - Mixed Renara Partial Aftermath (Priority: P1)

**Goal**: Replace only `ward_steward_parley_action` partial text with a substantial Russian dark-fantasy social aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `ward_steward_parley` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/fail result surfaces.
- [X] T014 Verify `specs/1010-daren-renara-partial/` references #1010/#955/#976/#1009/#1011 and that tasks/checklists match the final diff.
- [X] T015 Verify #1009/#1011, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1010-daren-renara-partial`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1010 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1010, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1010-daren-renara-partial` started from `origin/main` at `be0bbd4`; source issue #1010 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1010.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 55 passed / 0 failed / 0 skipped / 55 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 324 passed / 0 failed / 0 skipped / 324 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1010-daren-renara-partial\\specs\\1010-daren-renara-partial` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default.
- T005: Added `DarenWardStewardParleyPartial_ReadsAsMixedSocialAftermathWithoutMechanicDrift` to guard the partial aftermath length, sentence count, Daren POV, grouped motifs, prohibited terminology, and route/action/config/routing/score invariants.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 55 passed / 1 failed / 0 skipped / 56 total; expected failure: old `ward_steward_parley` partial text is not a substantial mixed social aftermath insert.
- T007: Replaced only `ward_steward_parley_action` partial prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; success/fail strings and action mechanics were left unchanged.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 56 passed / 0 failed / 0 skipped / 56 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 325 passed / 0 failed / 0 skipped / 325 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => passed with 0 warnings / 0 errors. Test build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => passed with 0 warnings / 0 errors.
- T011 committed-range whitespace check: `git diff --check origin/main...HEAD` => passed with exit code 0 and no output.
- T012 added-line static scan over changed non-Spec source/test files for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string-formatting => `NO_MATCHES`.
- T013 diff review: source diff changes only `ward_steward_parley_action` partial prose plus test guards/helper allowance for that partial; route ids, beat order, action id/label, check type/config, routing targets, score deltas, reward/profile/New Game code, endpoints, runtime state, frontend/browser code, success prose, and fail prose remain unchanged.
- T014 Spec Kit references: `spec.md`, `plan.md`, `tasks.md`, checklist, and contract reference #1010 with #955/#976/#1009/#1011 context; final task evidence now reflects code/test diff and verification.
- T015 scope review: no #1009 success rewrite, no #1011 fail rewrite, no parent #955 closure, and no other Daren result/scene follow-up implementation in the diff.
- T016 commit: one focused implementation commit created on `work/1010-daren-renara-partial` with message `[skip ci] Rewrite Daren Renara partial aftermath`; no push, PR, merge, issue closure, or Hermes lifecycle action was performed.
- T017 independent review: detached worktree `E:/Games/review-worktrees/boe-1010-daren-renara-partial-review-20260613-222302`, review run `E:/Games/codex-runs/20260613-222302-review-boe-1010-daren-renara-partial`, verdict `APPROVED`, Critical/Important/Minor findings: none; reviewer ran focused Daren tests => 56 passed / 0 failed / 0 skipped and confirmed scope/literary quality/Spec Kit alignment.
- T018 PR: opened https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1025 with local verification evidence, non-closing #955/#1009/#1011 references, and closing reference only for #1010; initial PR readback showed `mergeStateStatus=CLEAN` and `closingIssuesReferences=[#1010]`.
