# Tasks: Daren Keykeeper Gallery Fail Literary Aftermath

**Input**: `specs/1002-daren-keykeeper-fail/spec.md`, `plan.md`, issue #1002 body, parent #955 context, source scene #973, completed keykeeper siblings #1000/#1001, completed downstream siblings #1003-#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), completed keykeeper siblings [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000) and [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), and completed downstream siblings [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/1002-daren-keykeeper-fail`, read governing repo instructions, and select #1002 after #1001 closure.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/1002-daren-keykeeper-fail/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `guard_interrogation` fail result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #1002 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Keykeeper Fail Aftermath (Priority: P1)

**Goal**: Replace only `guard_interrogation_action` fail text with a substantial Russian dark-fantasy dangerous keykeeper aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `guard_interrogation` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, success/partial result surfaces, downstream lock-pick result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/1002-daren-keykeeper-fail/` references #1002/#955/#973/#1000/#1001/#1003/#1004/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify other result/scene follow-ups and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` implementation commit on `work/1002-daren-keykeeper-fail`.

## Hermes-Owned Lifecycle

- [x] T017 Independent review of #1002 implementation and literary quality bar.
- [x] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1002, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1002-daren-keykeeper-fail` started from `origin/main` at `b99a8a2`; source issue #1002 and parent #955 are the tracked tasks. #1000 / PR #1033 and #1001 / PR #1034 are already merged/closed in `main`, completing success/partial and making #1002 the next logical sibling for the `guard_interrogation_action` result trio.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1002.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 65 passed / 0 failed / 0 skipped / 65 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 334 passed / 0 failed / 0 skipped / 334 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1002-daren-keykeeper-fail\\specs\\1002-daren-keykeeper-fail` with `contracts/` and `tasks.md`.
- T005: Added `DarenGuardInterrogationFail_ReadsAsDangerousKeykeeperAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; updated existing keykeeper sibling guards to stop pinning the old one-sentence fail string while continuing to assert non-empty/different result surfaces.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 65 passed / 1 failed / 0 skipped / 66 total. Expected failure: `DarenGuardInterrogationFail_ReadsAsDangerousKeykeeperAftermathWithoutMechanicDrift` rejected the old exact fail text with `Assert.NotEqual() Failure: Strings are equal`.
- T007: Replaced only the `guard_interrogation_action` fail prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; rewritten text metrics: 2228 characters, 19 sentences, 6 `Дарен` mentions, 334 words.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 66 passed / 0 failed / 0 skipped / 66 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 335 passed / 0 failed / 0 skipped / 335 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded with 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded with 0 warnings / 0 errors.
- T011 diff hygiene: `git diff --check origin/main...HEAD` => exit 0 with no output.
- T012 added-line static scan over `BookOfEternityClient` and `BookOfEternityClient.Tests` diff for hardcoded secrets/shell execution/eval/unsafe deserialization/SQL formatting => `NO_MATCHES`.
- T013 diff inspection: code changes are limited to the focused Daren showcase test guard/helper and the single `guard_interrogation_action` fail prose string; no route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser files, success/partial surfaces, downstream lock-pick surfaces, or downstream rune-memory surfaces were changed.
- T014 reference check: `rg --fixed-strings` confirmed #1002, #955, #973, #1000, #1001, #1003, #1004, #1005, #1006, #1007, and #1008 are referenced under `specs/1002-daren-keykeeper-fail/`; tasks/checklist rows were reconciled to this diff and local evidence.
- T015 scope check: no other Daren result/scene follow-up, parent #955 closure work, GM-facing docs/examples, frontend files, or lifecycle files were changed.
- T016: Created one focused implementation commit with message `[skip ci] Rewrite Daren keykeeper fail aftermath`; this row is amended into the same final commit so no second implementation commit is introduced.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260614-202051-review-boe-1002-daren-keykeeper-fail` reviewed commit `5c3cf2ec4825bcff520ae28df207d1177ea74808` against `origin/main...HEAD` and returned `APPROVED` with no Critical/Important/Minor findings. Review reran focused Daren `66 passed / 0 failed / 0 skipped`, `git diff --check origin/main...HEAD` clean, and code/test static scan `NO_MATCHES`; Spec Kit prerequisite check failed only because the review worktree was detached, while implementation-worktree prerequisite evidence remained authoritative.
- T018 PR evidence: created PR [#1035](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1035) from `work/1002-daren-keykeeper-fail` to `main` with local verification evidence, independent-review result, `Closes #1002`, parent #955 boundary, and safe non-closing references for siblings/downstream issues. Initial PR head was `1cfffe7b31aa97085a8b0b3cec7272ba57066041`; this lifecycle-evidence amend updates the final branch head after PR creation.
- T019-T021: merge, issue closure, parent #955 boundary confirmation, and cleanup evidence remain pending for Hermes-owned lifecycle steps.
