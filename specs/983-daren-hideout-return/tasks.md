# Tasks: Daren Scene 15 Full Literary Page

**Input**: `specs/983-daren-hideout-return/spec.md`, `plan.md`, issue #983 body, parent #955 context, previous scene #982, and existing Daren route/test patterns.
**Tracked issue and related context**: [#983](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/983), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), previous scene [#982](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/982), result/aftermath follow-ups #988-#1014.

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/983-daren-hideout-return` and read governing repo instructions.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/983-daren-hideout-return/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current synopsis-length `hideout_return` scene and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #983 guard fails for the expected reason.

## Phase 3: User Story 1 - Under-Bridge Hideout Literary Page (Priority: P1)

**Goal**: Replace `hideout_return` with a substantial Russian dark-fantasy under-bridge hideout/cache-cleanup page while preserving existing mechanics.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `hideout_return` shared C# scene prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, terminal routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, or frontend/browser code.
- [X] T014 Verify `specs/983-daren-hideout-return/` references #983/#955 and that tasks/checklists match the final diff.
- [X] T015 Verify result/aftermath issues #988-#1014 and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/983-daren-hideout-return`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #983 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #983, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/983-daren-hideout-return` started from `origin/main` at `1759348f1cf6095da2288816ec7bf2724776d10f`; source issue #983 and parent #955 are the tracked tasks.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-scene-page.md`, `checklists/requirements.md`, and this `tasks.md` for #983.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 82 passed / 0 failed / 0 skipped / 82 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 351 passed / 0 failed / 0 skipped / 351 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-983-daren-hideout-return\\specs\\983-daren-hideout-return` with `contracts/` and `tasks.md`.
- T005: Added `DarenHideoutReturn_ReadsAsUnderBridgeHideoutPageWithoutMechanicDrift` to pin `hideout_return` prose substance, grouped motifs, shared `beat.PlayerText == chapter.Narrative`, terminal routing, `BranchChoice`/Wisdom difficulty/config, action label, and `DarenScoreDeltas(hideout: 6, evidence: -3)`.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => failed as expected: 82 passed / 1 failed / 0 skipped / 83 total. Failure was `DarenHideoutReturn_ReadsAsUnderBridgeHideoutPageWithoutMechanicDrift` rejecting the existing 239-character synopsis with 2 scene sentences, 1 Daren mention, and missing required motif groups.
- T007: Replaced only the `hideout_return` narrative in `BookOfEternityClient/Services/QteSceneService.Daren.cs` with an under-bridge hideout/cache-cleanup Russian dark-fantasy page; action labels, routing, config, score deltas, rewards, endpoints, runtime state, frontend, and sibling scenes were not edited.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 83 passed / 0 failed / 0 skipped / 83 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 352 passed / 0 failed / 0 skipped / 352 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors.
- T010 test build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 diff check: `git diff --check origin/main...HEAD` => passed with no whitespace errors.
- T012 added-line static scan over `BookOfEternityClient/` and `BookOfEternityClient.Tests/` production/test code, excluding docs/spec artifacts, for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`.
- T013 diff inspection: production diff is limited to `hideout_return` narrative text in shared C# route data; action block, route ids/order, action id/label/check/config, terminal routing, score deltas, rewards, profile/New Game grants, endpoints, runtime state, frontend/browser files, and sibling scenes are unchanged.
- T014 reference check: `rg -n "#983|#955|#982|#988|#1014|T017|T018|T019|T020|T021" specs\\983-daren-hideout-return` confirms #983/#955 references and related/out-of-scope markers remain in Spec Kit artifacts.
- T015 scope check: no result/aftermath issue #988-#1014 scene, parent #955 lifecycle state, frontend, endpoint, reward, runtime-state, or GM-facing file was edited.
- T016: This change set is prepared for the required focused `[skip ci]` implementation commit on `work/983-daren-hideout-return`; final SHA is reported outside the committed task log.
- T017 independent review: read-only Codex review run `E:/Games/codex-runs/20260615-181020-review-boe-983-daren-hideout-return` returned `APPROVED` with no blocking findings. Reviewer confirmed prose quality, preserved `hideout_return_action` / `BranchChoice` / Wisdom difficulty 3 / `DarenBranchChoiceConfig("success")` / terminal `daren_hideout_return` / `DarenScoreDeltas(hideout: 6, evidence: -3)`, no forbidden implementation terminology, and safe CRLF-counted length margin (3117 chars, 483 below the 3600 cap).
- Pre-PR Hermes gate after review: affected Daren/QTE/docs/browser slice command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 352 passed / 0 failed / 0 skipped / 352 total.
- Pre-PR Spec Kit prerequisite check after review: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-983-daren-hideout-return\\specs\\983-daren-hideout-return` with `contracts/` and `tasks.md`.
- T018 PR: created https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1053 with local-gated verification evidence, independent review verdict, `Closes #983`, and an explicit parent #955 / #988-#1014 out-of-scope boundary.
