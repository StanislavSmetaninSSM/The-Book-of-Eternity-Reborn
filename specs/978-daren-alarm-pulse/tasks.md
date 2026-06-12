# Tasks: Daren Scene 10 Full Literary Page

**Input**: `specs/978-daren-alarm-pulse/spec.md`, `plan.md`, issue #978 body, parent #955 context, prior scene #977, and existing Daren route/test patterns.
**Tracked issue and related context**: [#978](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/978), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prior scene [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/978-daren-alarm-pulse` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/978-daren-alarm-pulse/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `timed_rhythm` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #978 guard fails for the expected reason.

## Phase 3: User Story 1 - Alarm Pulse Literary Page (Priority: P1)

**Goal**: Replace `timed_rhythm` with a substantial Russian dark-fantasy rhythm-stealth page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `timed_rhythm` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/978-daren-alarm-pulse/` references #978/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify #979-#983, result/aftermath issues #988-#1014, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/978-daren-alarm-pulse`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #978 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #978, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/978-daren-alarm-pulse` started from `origin/main` at `3918fcd`; source issue #978 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #978.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 50 passed / 0 failed / 0 skipped / 50 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 319 passed / 0 failed / 0 skipped / 319 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-978-daren-alarm-pulse\specs\978-daren-alarm-pulse` with `contracts/` and `tasks.md`; no branch-name caveat and no AGENTS/global pointer patch required.
- T005: Added `DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; it pins title, shared `beat.PlayerText == chapter.Narrative`, action id/label/check type/Speed/difficulty, routing, rhythm config, score deltas, substantial prose length/sentence/POV thresholds, grouped motifs, and forbidden technical terms.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 50 passed / 1 failed / 0 skipped / 51 total. Expected failure: `DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift` failed with `Daren timed_rhythm narrative should be a substantial alarm-pulse literary page, not a compact synopsis.`
- T007: Replaced only `timed_rhythm` / "Пульс сигнализации" narrative in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; no action contract, routing, scoring, reward, endpoint, runtime-state, frontend, GM-facing docs, or sibling scene files changed.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 51 passed / 0 failed / 0 skipped / 51 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 320 passed / 0 failed / 0 skipped / 320 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 committed-range diff check: `git diff --check origin/main...HEAD` => exit 0, no whitespace errors.
- T012 added-line static scan: header-safe `^+[^+]` scan over non-Spec changed files for hardcoded secrets, shell invocation/injection, `eval`, unsafe deserialization, and SQL string-formatting patterns => `NO_MATCHES`.
- T013 diff inspection: touched code/test files only replace the `timed_rhythm` narrative, add the #978 focused guard, and extend the broad Daren prose max-length allowlist for `timed_rhythm`; no mechanics/config/routing/scoring/reward/frontend/runtime drift found.
- T014: `specs/978-daren-alarm-pulse/` keeps #978/#955 source links and now records RED/GREEN, affected-slice, build, static-scan, and scope reconciliation evidence.
- T015: Scope remains limited to #978; sibling scenes #979-#983, result/aftermath issues #988-#1014, and parent #955 lifecycle remain Hermes-owned and untouched.
- T016: Created one focused implementation commit on `work/978-daren-alarm-pulse` with message `Rewrite Daren alarm pulse scene [skip ci]`.
- T017 independent review: read-only Codex review of committed diff `3918fcd...1897cb8` returned `APPROVED` with no blocking or non-blocking findings. Reviewer confirmed focused Daren tests 51/51, affected Daren/QTE/browser/docs slice 320/320, diff-check and production technical-term scan passed, `timed_rhythm` is 3556 chars with CRLF under the 3600 cap, prose meets the action/rhythm literary quality bar, and mechanics/frontend/reward scope stayed unchanged. Detached review Spec Kit prerequisite failed only because `HEAD` is detached; implementation branch prerequisite already resolves `specs/978-daren-alarm-pulse`.
- T018 PR evidence: opened PR #1020 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1020`) with local-gated verification, independent-review evidence, `Closes #978`, and explicit boundary that parent #955 plus #979-#983/result-aftermath follow-ups remain out of scope.
