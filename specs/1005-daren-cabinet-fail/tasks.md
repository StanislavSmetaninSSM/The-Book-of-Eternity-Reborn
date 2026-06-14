# Tasks: Daren Cabinet Lock Fail Literary Aftermath

**Input**: `specs/1005-daren-cabinet-fail/spec.md`, `plan.md`, issue #1005 body, parent #955 context, source scene #974, completed siblings #1003/#1004, completed downstream siblings #1006/#1007/#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#974](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/974), completed siblings [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003) and [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), and completed downstream siblings [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/1005-daren-cabinet-fail`, read governing repo instructions, and select #1005 after #1004 closure.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/1005-daren-cabinet-fail/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `lock_pick` fail result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #1005 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Cabinet Lock Fail Aftermath (Priority: P1)

**Goal**: Replace only `lock_pick_action` fail text with a substantial Russian dark-fantasy dangerous cabinet-lock aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `lock_pick` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, success/partial result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/1005-daren-cabinet-fail/` references #1005/#955/#974/#1003/#1004/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify other result/scene follow-ups and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` commit on `work/1005-daren-cabinet-fail`.

## Hermes-Owned Lifecycle

- [x] T017 Independent review of #1005 implementation and literary quality bar.
- [x] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1005, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1005-daren-cabinet-fail` started from `origin/main` at `bb33bf7`; source issue #1005 and parent #955 are the tracked tasks. #1003 / PR #1030 and #1004 / PR #1031 are already merged/closed in `main`.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1005.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 62 passed / 0 failed / 0 skipped / 62 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 331 passed / 0 failed / 0 skipped / 331 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1005-daren-cabinet-fail\\specs\\1005-daren-cabinet-fail` with `contracts/` and `tasks.md`.
- T005: Added `DarenLockPickFail_ReadsAsDangerousCabinetAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; the guard pins the `lock_pick_action` contract, keeps #1003/#1004/#1006/#1007/#1008 sibling prose protected by sentinel assertions, rejects the old one-sentence fail text, requires substantial fail aftermath metrics, and checks grouped motifs for failed pin craft, Daren body control, loud lock consequence, trace/evidence/pursuit pressure, guard/house awareness, forced cabinet movement, and rune/futlar continuity.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 62 passed / 1 failed / 0 skipped / 63 total. Expected failure: `Assert.NotEqual() Failure: Strings are equal` against old fail text `Замок щёлкает слишком громко...`.
- T007: Replaced only `lock_pick_action` fail prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. New fail aftermath measurements: 2252 characters / 350 words / 18 sentences / 7 `Дарен` mentions.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 63 passed / 0 failed / 0 skipped / 63 total.
- T009 affected slice command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 332 passed / 0 failed / 0 skipped / 332 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings / 0 errors.
- T011 pre-commit whitespace check: `git diff --check` => exit 0, no whitespace errors; Git emitted CRLF normalization warnings for the two edited C# files only. Final post-commit `git diff --check origin/main...HEAD` evidence is reported in Codex final handoff.
- T012 code/test added-line static scan over `BookOfEternityClient/Services/QteSceneService.Daren.cs` and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`, excluding docs/spec artifacts, for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting => `NO_MATCHES`.
- T013 diff inspection: only the `lock_pick_action` fail text changed in production; tests added the #1005 guard and adjusted fail-result source guards. No route id, beat order, action id, label, check type, characteristic, difficulty, config, routing, score delta, reward/profile/New Game, endpoint/runtime state, frontend/browser, success/partial prose, or downstream rune-memory prose drift found.
- T014: `spec.md`, `plan.md`, `tasks.md`, `contracts/daren-result-aftermath.md`, and `checklists/requirements.md` reference #1005/#955/#974/#1003/#1004/#1006/#1007/#1008; this task log and checklist were reconciled with RED/GREEN/build/static-scan evidence.
- T015: Other Daren result/scene follow-ups, parent #955 closure, PR creation/merge, issue closure, Hermes cleanup, and independent review remain out of Codex implementation scope.
- T016: One focused `[skip ci]` commit is prepared for this verified change set on `work/1005-daren-cabinet-fail`; final SHA is reported in Codex final handoff.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260614-145737-review-boe-1005-daren-cabinet-fail` exited 0 and returned `APPROVED` at review HEAD `b505261d50ec1b9fe000a32219a605f6daa19fa1` with no Critical/Important/Minor findings. The reviewer inspected the full `origin/main...HEAD` diff, `QteSceneService.Daren.cs`, `DarenQteShowcaseTests.cs`, and all `specs/1005-daren-cabinet-fail/*` artifacts; `git diff --check origin/main...HEAD` passed; added production prose forbidden-term scan and added C#/test static-risk scan returned no matches. The review checkout could not run Spec Kit prerequisites because it was detached `HEAD`, so Hermes implementation-worktree prerequisites remain authoritative.
- T018 PR evidence: PR #1032 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1032`) was created with local-gated verification evidence, independent-review verdict, parent #955/sibling scope boundaries, `Resolves #1005`, and non-closing references for #955/#974/#1003/#1004/#1006/#1007/#1008.
