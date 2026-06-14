# Tasks: Daren Silent Gallery Partial Literary Aftermath

**Input**: `specs/998-daren-gallery-partial/spec.md`, `plan.md`, issue #998 body, parent #955 context, source scene #972, completed success sibling #997, future fail sibling #999, completed downstream #1000-#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), completed success sibling [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997), future fail sibling [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), and completed downstream siblings [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/998-daren-gallery-partial`, read governing repo instructions, and select #998 after #997 closure.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/998-daren-gallery-partial/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `stealth_crossing` partial result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #998 guard fails for the expected reason.

## Phase 3: User Story 1 - Costly Silent Gallery Partial Aftermath (Priority: P1)

**Goal**: Replace only `stealth_crossing_action` partial text with a substantial Russian dark-fantasy mixed gallery aftermath insert while preserving existing mechanics and sibling/downstream result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `stealth_crossing` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, #997 success result surface, #999 fail result surface, downstream keykeeper result surfaces, downstream lock-pick result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/998-daren-gallery-partial/` references #998/#955/#972/#997/#999/#1000/#1001/#1002/#1003/#1004/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify other result/scene follow-ups and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` commit on `work/998-daren-gallery-partial`.

## Hermes-Owned Lifecycle

- [x] T017 Independent review of #998 implementation and literary quality bar.
- [x] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #998, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/998-daren-gallery-partial` started from `origin/main` at `273c41c`; source issue #998 and parent #955 are the tracked tasks. #997 / PR #1036 is already merged/closed in `main`, completing the clean-success sibling for `stealth_crossing_action`.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #998.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 67 passed / 0 failed / 0 skipped / 67 total.
- T003 baseline affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 336 passed / 0 failed / 0 skipped / 336 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-998-daren-gallery-partial\\specs\\998-daren-gallery-partial` with `contracts/` and `tasks.md`.
- T005: Added `DarenStealthCrossingPartial_ReadsAsCostlyGalleryAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`. The guard rejects the old one-sentence partial string, pins `stealth_crossing_action` ids/label/check/config/routing/score deltas, preserves the #999 fail string, checks grouped aftermath motifs, and allows the shared transition guard to treat the partial result as a long aftermath insert.
- T006 RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 67 passed / 1 failed / 0 skipped / 68 total. Expected failure: `DarenStealthCrossingPartial_ReadsAsCostlyGalleryAftermathWithoutMechanicDrift` failed at `Assert.NotEqual()` because the production partial text still matched `Один страж шевелится от скрипа...`.
- T007: Replaced only `stealth_crossing_action` partial prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. Final partial metrics from added production lines: 1528 characters, 13 counted sentences, 6 `Дарен` mentions, 245 words. #997 success and #999 fail strings were not edited.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 68 passed / 0 failed / 0 skipped / 68 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 337 passed / 0 failed / 0 skipped / 337 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 whitespace checks: pre-commit `git diff --check` => exit 0 with no whitespace errors; post-commit `git diff --check origin/main...HEAD` => exit 0 with no whitespace errors.
- T012 code/test added-line static scan for hardcoded secrets, shell invocation, eval/exec, unsafe deserialization, and SQL formatting => `NO_MATCHES`. Added production prose scan for forbidden technical/meta terms (`GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`) => `NO_MATCHES`.
- T013 diff inspection: changed files are scoped to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` and `BookOfEternityClient/Services/QteSceneService.Daren.cs`; the production diff replaces only the `stealth_crossing_action` partial raw string. The test guard pins mechanics/routing/config/score deltas and the #999 fail string. No frontend, endpoint, runtime state, reward profile, downstream keykeeper/lock-pick/rune-memory, or GM-facing files changed.
- T014 reference reconciliation: `rg` checks found #998, #955, #972, #997, #999, and #1000-#1008 in `specs/998-daren-gallery-partial/`; the requirements checklist remains complete and aligned with the final scoped diff.
- T015 scope reconciliation: other Daren result/scene follow-ups and parent #955 remain out of scope; no issue closure or lifecycle action was taken.
- T016: One focused `[skip ci]` implementation commit was created on `work/998-daren-gallery-partial`.
- T017: Independent detached Codex review run `E:/Games/codex-runs/20260615-064321-review-boe-998-daren-gallery-partial` against implementation head `17fe61c997b400c91f63814d4b2689717fb18eb8` returned `VERDICT: APPROVED` with no Critical or Important findings. Reviewer reran focused Daren tests (`68 passed / 0 failed / 0 skipped`), `git diff --check origin/main...HEAD`, and production-prose forbidden-term scan; detached Spec Kit prerequisite failure was branch-shape only and non-blocking because Hermes implementation-worktree prerequisites already resolved this feature directory.
- T018: PR #1037 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1037`) was created from `work/998-daren-gallery-partial` to `main`; readback reported `mergeStateStatus=CLEAN` and `closingIssuesReferences=[998]`. PR body uses #998 as the only closing reference and keeps #955/#997/#999/#1000-#1008 in a safe non-closing references section.
- T019-T021 remain Hermes-owned lifecycle tasks.
