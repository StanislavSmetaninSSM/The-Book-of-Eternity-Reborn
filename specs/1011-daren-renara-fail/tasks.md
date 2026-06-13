# Tasks: Daren Renara Voice Fail Literary Aftermath

**Input**: `specs/1011-daren-renara-fail/spec.md`, `plan.md`, issue #1011 body, parent #955 context, source scene #976, completed success sibling #1009, completed partial sibling #1010, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1011](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1011), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#976](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/976), completed siblings [#1009](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1009) and [#1010](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1010)

## Phase 1: Setup

- [X] T001 Confirm baseline status on branch `work/1011-daren-renara-fail`, read governing repo instructions, and select #1011 after #1010 closure/report verification.
- [X] T002 [P] Create focused Spec Kit artifacts in `specs/1011-daren-renara-fail/`.
- [X] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [X] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [X] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `ward_steward_parley` fail result and pins mechanics.
- [X] T006 Run focused Daren tests and record RED evidence showing the new #1011 guard fails for the expected reason.

## Phase 3: User Story 1 - Dangerous Renara Fail Aftermath (Priority: P1)

**Goal**: Replace only `ward_steward_parley_action` fail text with a substantial Russian dark-fantasy social/ward-alarm aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [X] T007 [US1] Replace only the `ward_steward_parley` action fail result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [X] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [X] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [X] T010 Run client/test-project build commands and record results.
- [X] T011 Run `git diff --check origin/main...HEAD`.
- [X] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [X] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/partial result surfaces.
- [X] T014 Verify `specs/1011-daren-renara-fail/` references #1011/#955/#976/#1009/#1010 and that tasks/checklists match the final diff.
- [X] T015 Verify #1009/#1010, other result/scene follow-ups, and parent #955 remain out of scope.
- [X] T016 Make one focused `[skip ci]` commit on `work/1011-daren-renara-fail`.

## Hermes-Owned Lifecycle

- [X] T017 Independent review of #1011 implementation and literary quality bar.
- [X] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1011, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1011-daren-renara-fail` started from `origin/main` at `514fc0f`; source issue #1011 and parent #955 are the tracked tasks. #1010 / PR #1025 were verified merged/closed/reported before selecting #1011.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1011.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 56 passed / 0 failed / 0 skipped / 56 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 325 passed / 0 failed / 0 skipped / 325 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1011-daren-renara-fail\\specs\\1011-daren-renara-fail` with `contracts/` and `tasks.md`; `specify version` reported CLI 0.9.3.
- T005: Added focused guard `DarenWardStewardParleyFail_ReadsAsDangerousSocialAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`. The guard pins the existing Renara action id/label, `PrecisionChoice` Wisdom check, timeout/config choice mapping, routing to `physical_pressure`, success/partial/fail score deltas, absence of player-facing implementation terms, and grouped motif coverage for Renara/ward voice, Daren's failed challenge, concrete alarm/pursuit pressure, evidence/identity/witness pressure, listening-house hostility, and heavy-grate continuity.
- T006 RED focused command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 56 passed / 1 failed / 0 skipped / 57 total. Expected failing assertion: `Daren ward_steward_parley fail should be a substantial dangerous social aftermath insert, not a one-sentence result notification.`
- T007: Replaced only `ward_steward_parley_action` fail result text in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. Success prose, partial prose, scene opening, action config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, frontend/browser files, and GM-facing docs/examples were left unchanged.
- T008 GREEN focused command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 57 passed / 0 failed / 0 skipped / 57 total.
- T009 affected C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 326 passed / 0 failed / 0 skipped / 326 total.
- T010 client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => succeeded, 0 warnings / 0 errors. Test-project build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => succeeded, 0 warnings / 0 errors.
- T011 whitespace checks: pre-commit `git diff --check` => exit 0 with no whitespace errors; Git reported expected LF-to-CRLF working-copy warnings for the two touched C# files. Post-commit exact command `git diff --check origin/main...HEAD` => exit 0 with no output.
- T012 added-line static scan over changed non-Spec source/test files for hardcoded secrets, shell injection, standalone eval/exec, unsafe deserialization, and SQL string-formatting => `NO_MATCHES` pre-commit and `NO_MATCHES` post-commit against `origin/main...HEAD`.
- T013 diff inspection: non-Spec changes are limited to `BookOfEternityClient/Services/QteSceneService.Daren.cs` fail prose and `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` guard/helper updates. No mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, or success/partial authored prose changed.
- T014 Spec Kit reconciliation: `spec.md`, `plan.md`, `tasks.md`, `checklists/requirements.md`, and `contracts/daren-result-aftermath.md` reference #1011, #955, #976, #1009, and #1010 where appropriate; checklist/task evidence now matches the implementation diff except Hermes-owned lifecycle items.
- T015 Scope confirmation: #1009 success, #1010 partial, other Daren result/scene follow-ups, and parent #955 closure remain out of scope.
- T016 Commit: one focused `[skip ci]` implementation commit created on `work/1011-daren-renara-fail` with message `[skip ci] [QTE/Daren] Rewrite Renara fail aftermath (#1011)`. Final SHA is reported in the Codex final response.
- T017 Independent review: detached/fresh-context review returned `APPROVED` with no Critical, Important, or Minor issues. Reviewer inspected `origin/main...HEAD`, verified scope/literary quality/Spec Kit alignment, and reran focused Daren `57 passed / 0 failed`, affected slice `326 passed / 0 failed`, both builds `0 warnings / 0 errors`, Spec Kit prerequisite check, `git diff --check`, and added-line static scan `NO_MATCHES`.
- T018 PR: created [PR #1026](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1026) with local verification evidence, review evidence, parent #955 scope boundary, and `Closes #1011`. GitHub reported `closingIssuesReferences=[1011]`, `mergeStateStatus=CLEAN`, and no required status checks at creation.
