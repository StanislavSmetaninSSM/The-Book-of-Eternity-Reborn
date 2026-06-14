# Tasks: Daren Silent Gallery Fail Literary Aftermath

**Input**: `specs/999-daren-gallery-fail/spec.md`, `plan.md`, issue #999 body, parent #955 context, source scene #972, completed siblings #997/#998, completed downstream #1000-#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972), completed siblings [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997) and [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998), and completed downstream siblings [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/999-daren-gallery-fail`, read governing repo instructions, and select #999 after #998 closure/report delivery.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/999-daren-gallery-fail/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `stealth_crossing` fail result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #999 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Silent Gallery Fail Aftermath (Priority: P1)

**Goal**: Replace only `stealth_crossing_action` fail text with a substantial Russian dark-fantasy dangerous gallery aftermath insert while preserving existing mechanics and sibling/downstream result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `stealth_crossing` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, #997 success result surface, #998 partial result surface, downstream keykeeper result surfaces, downstream lock-pick result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/999-daren-gallery-fail/` references #999/#955/#972/#997/#998/#1000/#1001/#1002/#1003/#1004/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify other result/scene follow-ups and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` commit on `work/999-daren-gallery-fail`.

## Hermes-Owned Lifecycle

- [x] T017 Independent review of #999 implementation and literary quality bar.
- [x] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #999, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/999-daren-gallery-fail` started from `origin/main` at `dd2be29`; source issue #999 and parent #955 are the tracked tasks. #998 / PR #1037 is already merged/closed in `main`, completing the mixed-partial sibling for `stealth_crossing_action`.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #999.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 68 passed / 0 failed / 0 skipped / 68 total.
- T003 baseline affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 337 passed / 0 failed / 0 skipped / 337 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-999-daren-gallery-fail\\specs\\999-daren-gallery-fail` with `contracts/` and `tasks.md`.
- T005: Added `DarenStealthCrossingFail_ReadsAsDangerousGalleryAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`. The guard rejects the old one-sentence fail string; pins title, chapter id, action id, action label, `StealthNoise`, Dexterity, base difficulty, full `DarenStealthNoiseConfig()` values, routing to `guard_interrogation`, success/partial/fail score deltas, sibling success/partial anchors, substantial length/sentence/Daren-count thresholds, grouped gallery/noise/body/witness/alarm/evidence/service-door motifs, and forbidden player-facing technical terms. Existing success/partial sibling guards now require authored distinct fail text instead of pinning the obsolete #999 target string; the shared transition helper now treats `stealth_crossing_action` fail as a long aftermath result.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => failed as expected: 68 passed / 1 failed / 0 skipped / 69 total. Expected failure: `DarenStealthCrossingFail_ReadsAsDangerousGalleryAftermathWithoutMechanicDrift` failed at `Assert.NotEqual()` because the actual fail text still equaled `Доска отвечает резким треском, и Дарен видит, как в дальнем крыле поднимается тревожный фонарь со свидетелем.`
- T007: Replaced only `stealth_crossing_action` fail prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. Final fail prose metrics from local extraction: 2068 characters / 14 counted sentences / 7 `Дарен` mentions / 321 words.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 69 passed / 0 failed / 0 skipped / 69 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 338 passed / 0 failed / 0 skipped / 338 total.
- T010 client build command: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build command: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T012 added-line code/test static scan command over `BookOfEternityClient/Services/QteSceneService.Daren.cs` and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` for hardcoded secrets, shell execution/injection, `eval`, unsafe deserialization, and SQL raw formatting patterns => `NO_MATCHES`. Production-prose forbidden technical/meta term scan over added `QteSceneService.Daren.cs` lines for `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, and `score` => `NO_MATCHES`.
- T013 diff scope inspection before task-log update: `git diff --name-only` listed only `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` and `BookOfEternityClient/Services/QteSceneService.Daren.cs`; source diff changes only the `stealth_crossing_action` fail prose, and test diff changes only the focused #999 guard, obsolete sibling fail-string assertions, and long-result helper allowance. No route ids, beat order, action ids, check type/config, routing, score deltas, reward/profile/New Game grants, endpoints, runtime state, frontend/browser code, #997 success prose, #998 partial prose, or downstream #1000-#1008 result prose changed.
- T014 corrected Spec Kit reference check command joined `spec.md`, `plan.md`, `tasks.md`, `contracts/daren-result-aftermath.md`, and `checklists/requirements.md` into one buffer and verified all required references are present: #999, #955, #972, #997, #998, #1000, #1001, #1002, #1003, #1004, #1005, #1006, #1007, #1008. Checklist remains unchanged because implementation evidence did not require requirement changes.
- T015: Other result/scene follow-ups and parent #955 remain out of scope. No GitHub issue/PR lifecycle action was taken; Hermes-owned T017-T021 remain pending.
- T011 committed-range diff check after initial focused commit: `git diff --check origin/main...HEAD` => exit 0 with no output.
- T016: Created one focused implementation commit on `work/999-daren-gallery-fail` with message `Implement #999 Daren gallery fail aftermath [skip ci]`. Initial commit hash was `61a0314`; task-log evidence was then amended into the same final commit.
- T017: Independent detached Codex review run `E:/Games/codex-runs/20260615-073016-review-boe-999-daren-gallery-fail` against implementation head `cc0ec5504c486d230e645de25431a6b23971fe5e` returned `VERDICT: APPROVED` with no Critical, Important, or Minor findings. Reviewer inspected product/test/spec diffs, verified literary quality/scope/mechanics, reran focused Daren (`69 passed / 0 failed / 0 skipped`), affected slice (`338 passed / 0 failed / 0 skipped`), `git diff --check origin/main...HEAD`, and added-line/static forbidden-term scans. Detached Spec Kit prerequisite failure was branch-shape only and non-blocking because Hermes implementation-worktree prerequisites already resolved this feature directory.
- T018: PR #1038 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1038`) was created from `work/999-daren-gallery-fail` to `main`; readback reported `mergeStateStatus=CLEAN` and `closingIssuesReferences=[999]`. PR body uses #999 as the only closing reference and keeps #955/#997/#998/#1000-#1008 in a safe non-closing references section.
- T019-T021 remain Hermes-owned lifecycle tasks.
