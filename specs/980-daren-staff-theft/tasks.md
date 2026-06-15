# Tasks: Daren Scene 12 Full Literary Page

**Input**: `specs/980-daren-staff-theft/spec.md`, `plan.md`, issue #980 body, parent #955 context, prior scene #979, and existing Daren route/test patterns.
**Tracked issue and related context**: [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#979](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/979), next scene [#981](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/981)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/980-daren-staff-theft` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/980-daren-staff-theft/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `staff_theft` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #980 guard fails for the expected reason.

## Phase 3: User Story 1 - Staff Theft Literary Page (Priority: P1)

**Goal**: Replace `staff_theft` with a substantial Russian dark-fantasy theft page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `staff_theft` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/980-daren-staff-theft/` references #980/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify #981-#983, result/aftermath issues #988-#1014, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/980-daren-staff-theft`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #980 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #980, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/980-daren-staff-theft` started from `origin/main` at `4bb6896`; source issue #980 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #980.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 79 passed / 0 failed / 0 skipped / 79 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 348 passed / 0 failed / 0 skipped / 348 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-980-daren-staff-theft\\specs\\980-daren-staff-theft` with `contracts/` and `tasks.md`.
- T005: Added `DarenStaffTheft_ReadsAsRelicTheftPageWithoutMechanicDrift` to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; it pins beat title, shared `PlayerText`/`Narrative`, `staff_theft_action`, `BalanceMeter`, `Characteristics.Dexterity`, difficulty `4`, null config, label, routing to `pursuit`, score deltas, no technical terms, and grouped story motifs.
- T006 RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed as expected before production prose changes: 1 failed (`DarenStaffTheft_ReadsAsRelicTheftPageWithoutMechanicDrift`) / 79 passed / 0 skipped / 80 total. Expected message: `Daren staff_theft narrative should be a substantial relic-theft literary page, not a compact synopsis.`
- T007: Replaced only the shared `staff_theft` prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; `staff_theft_action` and all mechanics remain unchanged.
- T008 GREEN focused Daren: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 80 passed / 0 failed / 0 skipped / 80 total.
- T009 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 349 passed / 0 failed / 0 skipped / 349 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors.
- T011 whitespace checks: `git diff --check origin/main...HEAD` => clean; `git diff --check` => clean with only Git LF-to-CRLF working-copy normalization warnings for the two touched C# files.
- T012 added-line static scan over `BookOfEternityClient` and `BookOfEternityClient.Tests` for hardcoded secrets, shell execution, `eval`, unsafe deserialization, and SQL formatting => `NO_MATCHES`.
- T013 diff inspection: tracked source/test diff is limited to `BookOfEternityClient/Services/QteSceneService.Daren.cs` and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; no QTE action block, route id/order, rewards/profile/New Game grants, endpoints, runtime state, frontend/browser files, or sibling scene prose changed.
- T014 Spec Kit reconciliation: `spec.md`, `plan.md`, `tasks.md`, contract, and checklist reference #980/#955 and match the final implementation scope.
- T015 scope check: #981-#983, #988-#1014, and parent #955 lifecycle remain untouched; Hermes-owned T017-T021 remain unchecked.
- T016: One focused local `[skip ci]` commit was prepared on `work/980-daren-staff-theft` as `9485513`; the subsequent independent review found a CRLF-sensitive prose-length blocker, so the commit was amended after the fix.
- T017 first independent review attempt: detached Codex review run `E:/Games/codex-runs/20260615-232618-review-boe-980-daren-staff-theft` returned `CHANGES_REQUIRED` because `DarenChapters_HaveLiterarySceneProseForEveryBeat` measured `staff_theft` at 3618 chars on Windows CRLF checkout, above the 3600 max.
- Review-fix evidence: shortened only the `staff_theft` literary prose with margin while preserving theft/balance/evidence motifs and all mechanics; reran focused Daren => 80 passed / 0 failed / 0 skipped / 80 total; affected slice => 349 passed / 0 failed / 0 skipped / 349 total; client/test builds => 0 warnings / 0 errors; Spec Kit prerequisites still resolve `specs/980-daren-staff-theft`; `git diff --check origin/main...HEAD` clean; scoped code/static and production-prose technical/meta scan => `NO_MATCHES`.
- T017 approved re-review: detached Codex re-review run `E:/Games/codex-runs/20260615-234157-rereview-boe-980-daren-staff-theft` returned `APPROVED`; Critical/Important/Minor findings: none. The reviewer reran focused Daren 80/80, affected slice 349/349, builds clean, `git diff --check` clean, player-facing technical/meta scan `NO_MATCHES`, and measured `staff_theft` at 3583/3600 chars on a CRLF checkout.
- T018 PR evidence: created PR #1049 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1049`) from `work/980-daren-staff-theft` to `main`; GitHub readback reported `OPEN`, `mergeStateStatus=CLEAN`, and `closingIssuesReferences=[980]` only. PR body names parent #955 and sibling scene/result tasks only under non-closing references.
