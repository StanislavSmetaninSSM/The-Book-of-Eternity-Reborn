# Tasks: Daren Scene 11 Full Literary Page

**Input**: `specs/979-daren-orangery-route/spec.md`, `plan.md`, issue #979 body, parent #955 context, prior scene #978, and existing Daren route/test patterns.
**Tracked issue and related context**: [#979](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/979), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978), next scene [#980](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/980)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/979-daren-orangery-route` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/979-daren-orangery-route/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `route_decision` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #979 guard fails for the expected reason.

## Phase 3: User Story 1 - Orangery Route Choice Literary Page (Priority: P1)

**Goal**: Replace `route_decision` with a substantial Russian dark-fantasy route-choice page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `route_decision` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/979-daren-orangery-route/` references #979/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify #980-#983, result/aftermath issues #988-#1014, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/979-daren-orangery-route`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #979 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #979, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/979-daren-orangery-route` started from `origin/main` at `426004f`; source issue #979 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #979.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 78 passed / 0 failed / 0 skipped / 78 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 347 passed / 0 failed / 0 skipped / 347 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-979-daren-orangery-route\\specs\\979-daren-orangery-route` with `contracts/` and `tasks.md`; no branch-name caveat and no AGENTS/global pointer patch required.
- T005: Added `DarenRouteDecision_ReadsAsOrangeryRouteChoicePageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; guard pins shared beat/chapter parity, `route_decision_action`, `PrecisionChoice` config/choices, routing to `staff_theft`, score deltas, grouped motif coverage, and forbidden player-facing technical terms.
- T006 RED command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => expected failure in `DarenRouteDecision_ReadsAsOrangeryRouteChoicePageWithoutMechanicDrift`: "Daren route_decision narrative should be a substantial orangery route-choice literary page, not a compact synopsis."; 78 passed / 1 failed / 0 skipped / 79 total.
- T007: Replaced only `route_decision` / "Развилка в оранжерее" `DarenShowcaseBeat.PlayerText` in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with a full Russian dark-fantasy orangery route-choice page. No action contract, result text, route order, rewards, runtime state, endpoint, frontend, GM-facing doc, or sibling-scene file was changed.
- T008 GREEN focused command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 79 passed / 0 failed / 0 skipped / 79 total.
- T009 affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 348 passed / 0 failed / 0 skipped / 348 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings / 0 errors.
- T013: Diff inspection showed the only production code change is the `route_decision` narrative string; the new focused guard asserts unchanged action id, `PrecisionChoice` type/config/choices, routing, and score deltas. No frontend/browser, endpoint, runtime state, reward, profile, New Game grant, GM prompt/doc/example, validation, or pending/control file changed.
- T014: `specs/979-daren-orangery-route/` remains the active feature artifact set for #979/#955; tasks/checklist rows were updated only for Codex-owned implementation and verification evidence.
- T015: No #980-#983 scenes, result/aftermath surfaces #988-#1014, or parent #955 lifecycle state were edited.
- T011 hygiene: `git diff --cached --check` on the staged commit candidate => clean after removing trailing Markdown spaces in `spec.md`; post-commit `git diff --check origin/main...HEAD` => clean.
- T012 static scan: added-line scan over committed `git diff --unified=0 --no-ext-diff origin/main...HEAD` after unsetting `PYTHONHOME`, `UV_INTERNAL__PYTHONHOME`, and `PYTHONPATH` => `NO_REAL_FINDINGS`.
- T016 commit: one focused `[skip ci]` implementation commit on `work/979-daren-orangery-route` at `8f4aad0` before Hermes review-evidence amend.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260615-114534-review-boe-979-daren-orangery-route` returned `Verdict: APPROVED`, with no Critical/Important/Minor findings. Reviewer verified scoped diff, focused Daren tests `79 passed / 0 failed / 0 skipped`, and `git diff --check origin/main...HEAD` clean. The review-worktree Spec Kit prerequisite failure (`HEAD`, not feature branch) is classified as the expected detached-review caveat; the implementation worktree had fresh Spec Kit prerequisite success.
- T018 PR evidence: PR #1048 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1048`) was created from `work/979-daren-orangery-route` to `main`; GitHub readback before the final evidence amend reported `OPEN`, `CLEAN`, and closing references `[979]` only. The final head is authoritative from the post-amend GitHub readback/PR body, not this historical evidence row.
