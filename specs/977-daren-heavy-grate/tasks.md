# Tasks: Daren Scene 09 Full Literary Page

**Input**: `specs/977-daren-heavy-grate/spec.md`, `plan.md`, issue #977 body, parent #955 context, prior scene #976, and existing Daren route/test patterns.
**Source Issues**: [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/977-daren-heavy-grate` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/977-daren-heavy-grate/`.
- [X] T003 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T004 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `physical_pressure` scene and pins mechanics.
- [X] T005 Run focused Daren tests and record RED evidence showing the new #977 guard fails for the expected reason.

## Phase 3: User Story 1 - Heavy Grate Literary Page (Priority: P1)

**Goal**: Replace `physical_pressure` with a substantial Russian dark-fantasy physical-pressure page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T006 [US1] Replace only the `physical_pressure` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T007 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T008 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T009 Run client/test-project build commands and record results.
- [X] T010 Run `git diff --check origin/main...HEAD`.
- [X] T011 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T012 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T013 Verify `specs/977-daren-heavy-grate/` references #977/#955 and that tasks/checklists match the final diff.
- [X] T014 Verify #978-#983 and parent #955 remain out of scope.
- [X] T015 Make one focused `[skip ci]` commit on `work/977-daren-heavy-grate`.

## Hermes-Owned Lifecycle

- [X] T016 Independent review of #977 implementation and literary quality bar.
- [X] T017 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T018 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T019 Comment closure evidence on #977, close as completed, confirm issue state closed.
- [ ] T020 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/977-daren-heavy-grate` started clean against `origin/main`; source issue #977 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #977.
- T003 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-977-daren-heavy-grate\specs\977-daren-heavy-grate` with `contracts/` and `tasks.md`; no branch-name caveat and no AGENTS/global pointer patch required.
- T004/T005 RED: Added `DarenPhysicalPressure_ReadsAsHeavyGratePageWithoutMechanicDrift` before production prose changes. Focused Daren command failed for the expected reason: 49 passed / 1 failed / 0 skipped / 50 total, with the new guard reporting that `physical_pressure` was still a compact synopsis rather than a substantial heavy-grate physical-pressure page.
- T006/T007 GREEN: Replaced only the shared `physical_pressure` authored prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; route/action/check/routing/scoring code remained unchanged. Initial post-prose focused run exposed overly literal Russian test tokens (`решет` vs `решёт`, `ниша` vs inflected `ниши`/`нише`), so the guard was corrected to use robust motif roots. Focused Daren command then passed: 50 passed / 0 failed / 0 skipped / 50 total.
- T008 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 319 passed / 0 failed / 0 skipped / 319 total.
- T009 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors.
- T010/T011 working-tree checks: `git diff --check` => no whitespace errors, with Git LF-to-CRLF normalization warnings for the two tracked edited files; added-line static scan excluding Spec Kit docs returned `NO_MATCHES`.
- T012/T014 scope reconciliation: inspected the tracked diff and verified only `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and `specs/977-daren-heavy-grate/` artifacts changed; no frontend files, endpoints, runtime state, rewards, route ids, beat ids/order, action ids/labels, check type/config, routing targets, score helpers, or future scene prose were modified. #978-#983 and parent #955 remain out of scope.
- T013 Spec Kit reconciliation: `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` reference #977 and parent #955 as source scope; #976/Renara references are limited to prior-scene continuity and stale-reference review notes.
- T015 local commit: one focused `[skip ci]` commit is created for this Codex implementation; final SHA is reported in the Codex final response.
- T016 independent review: Hermes created detached review worktree `E:/Games/worktrees/boe-977-daren-heavy-grate-review` at `90ed8b584c55158f0a6afed9d8da4de053ad1af3`; independent review returned `APPROVED` with no Critical, Important, or Minor issues after inspecting the production/test diff, Spec Kit artifacts, `git diff --check`, a production/test technical-term scan, and the fresh Hermes verification evidence.
- T017 PR: Created PR [#1019](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1019) with `Closes #977`, local verification evidence, independent-review summary, `GitHub Actions: not used / not required`, and explicit non-closing references for parent #955 plus sibling scene tasks #978-#983.
