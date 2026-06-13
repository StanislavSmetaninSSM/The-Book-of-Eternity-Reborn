# Tasks: Daren Rune Memory Partial Literary Aftermath

**Input**: `specs/1007-daren-rune-partial/spec.md`, `plan.md`, issue #1007 body, parent #955 context, source scene #975, completed sibling #1006, remaining sibling #1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#975](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/975), completed sibling [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), remaining sibling [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1007-daren-rune-partial`, read governing repo instructions, and select #1007 after #1006 closure/report verification.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1007-daren-rune-partial/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `rune_memory` partial result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1007 guard fails for the expected reason.

## Phase 3: User Story 1 - Costly Rune Memory Partial Aftermath (Priority: P1)

**Goal**: Replace only `rune_memory_action` partial text with a substantial Russian dark-fantasy mixed-outcome rune/ward aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `rune_memory` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/fail result surfaces.
- [X] T014 Verify `specs/1007-daren-rune-partial/` references #1007/#955/#975/#1006/#1008 and that tasks/checklists match the final diff.
- [X] T015 Verify #1006/#1008, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1007-daren-rune-partial`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1007 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1007, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1007-daren-rune-partial` started from `origin/main` at `58c69ee`; source issue #1007 and parent #955 are the tracked tasks. #1006 / PR #1027 were verified merged/closed/reported before selecting #1007.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1007.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 58 passed / 0 failed / 0 skipped / 58 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 327 passed / 0 failed / 0 skipped / 327 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1007-daren-rune-partial\\specs\\1007-daren-rune-partial` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3 and `specify integration list` reported Codex CLI installed/default after clearing Python environment contamination.
- T005/T006 RED: added `DarenRuneMemoryPartial_ReadsAsCostlyRuneAftermathWithoutMechanicDrift`; focused command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected with `Assert.NotEqual() Failure: Strings are equal` because the actual partial text was `Одна руна трескается и оставляет след в стекле, но Дарен удерживает порядок знаков, пока дверь открыта.` Result: 58 passed / 1 failed / 0 skipped / 59 total.
- T007/T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 59 passed / 0 failed / 0 skipped / 59 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 328 passed / 0 failed / 0 skipped / 328 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 whitespace check: pre-commit `git diff --check` exited 0; Git reported expected LF-to-CRLF working-copy warnings for the touched C# files. Final exact `git diff --check origin/main...HEAD` is run after the single commit because `HEAD` must include the implementation diff.
- T012 added-line source/test static scan for hardcoded secrets, shell injection, standalone eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`. Added-line Spec Kit accidental-secret assignment scan => `NO_ACCIDENTAL_SECRET_ASSIGNMENTS_IN_SPECS`.
- T013 diff inspection: production diff changes only the `rune_memory_action` partial result string in shared C# route data; tests add a partial guard, update the old sibling assertion, and allow the rune partial to be a long aftermath result. No route ids, beat order, action ids, check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, frontend/browser files, scene opening, success result, or fail result prose changed.
- T014/T015 reconciliation: `spec.md`, `plan.md`, `tasks.md`, contract, and checklist retain #1007/#955/#975/#1006/#1008 context; final checked items are limited to #1007 local implementation/verification. Hermes-owned review, PR, merge, issue closure, parent #955 handling, and cleanup/report remain unchecked and out of scope.
- T016: one focused commit is prepared on `work/1007-daren-rune-partial` with message `[skip ci] [QTE/Daren] Rewrite rune partial aftermath (#1007)`.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260614-074457-review-boe-1007-daren-rune-partial` reviewed product commit `10944553e86789f8d4eeebc2318fc9cd1c210684` against `origin/main...HEAD` and returned `APPROVED` with no Critical, Important, or Minor findings blocking merge. Review worktree `check-prerequisites.ps1` failed only because the review worktree was detached (`HEAD (no branch)`); Hermes fresh implementation-worktree prerequisite/test/build gates above remain authoritative. The subsequent amend records review evidence only and does not change product code, prose, tests, or Spec Kit acceptance criteria.
- T018 PR: Created PR [#1028](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1028) from `work/1007-daren-rune-partial` to `main` with local verification evidence, independent-review verdict, safe non-closing references for #1006/#1008/#955, and closing reference only for #1007. Initial PR readback reported `mergeStateStatus=CLEAN` and `closingIssuesReferences=[#1007]`.
