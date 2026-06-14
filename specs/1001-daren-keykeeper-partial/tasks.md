# Tasks: Daren Keykeeper Gallery Partial Literary Aftermath

**Input**: `specs/1001-daren-keykeeper-partial/spec.md`, `plan.md`, issue #1001 body, parent #955 context, source scene #973, completed success sibling #1000, remaining fail sibling #1002, completed downstream siblings #1003-#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#1001](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1001), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#973](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/973), completed success sibling [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000), remaining fail sibling [#1002](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1002), and completed downstream siblings [#1003](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1003), [#1004](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1004), [#1005](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1005), [#1006](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1006), [#1007](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1007), and [#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008)

## Phase 1: Setup

- [x] T001 Confirm baseline status on branch `work/1001-daren-keykeeper-partial`, read governing repo instructions, and select #1001 after #1000 closure.
- [x] T002 [P] Create focused Spec Kit artifacts in `specs/1001-daren-keykeeper-partial/`.
- [x] T003 [P] Run focused Daren and affected-slice baseline tests before implementation.
- [x] T004 [P] Run Spec Kit prerequisite check and record feature-directory discoverability evidence.

## Phase 2: Foundational TDD Guard

- [x] T005 Add a focused failing test in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` that rejects the current one-sentence `guard_interrogation` partial result and pins mechanics.
- [x] T006 Run focused Daren tests and record RED evidence showing the new #1001 guard fails for the expected reason.

## Phase 3: User Story 1 - Mixed Keykeeper Partial Aftermath (Priority: P1)

**Goal**: Replace only `guard_interrogation_action` partial text with a substantial Russian dark-fantasy mixed keykeeper aftermath insert while preserving existing mechanics and sibling result surfaces.

**Independent Test**: The focused Daren guard passes and the route/action invariants remain unchanged.

- [x] T007 [US1] Replace only the `guard_interrogation` action partial result prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] T008 [US1] Run focused Daren tests and record GREEN evidence.
- [x] T009 [US1] Run the affected Daren/QTE/docs/browser C# slice and record exact pass/fail/skip counts.

## Phase 4: Verification and Reconciliation

- [x] T010 Run client/test-project build commands and record results.
- [x] T011 Run `git diff --check origin/main...HEAD`.
- [x] T012 Run added-line static scan for hardcoded secrets/shell injection/eval/unsafe deserialization/SQL formatting and record `NO_MATCHES` or findings.
- [x] T013 Verify diff does not change QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, reward tiers/profile/New Game grants, endpoints, runtime state, frontend/browser code, success/fail result surfaces, downstream lock-pick result surfaces, or downstream rune-memory result surfaces.
- [x] T014 Verify `specs/1001-daren-keykeeper-partial/` references #1001/#955/#973/#1000/#1002/#1003/#1004/#1005/#1006/#1007/#1008 and that tasks/checklists match the final diff.
- [x] T015 Verify other result/scene follow-ups and parent #955 remain out of scope.
- [x] T016 Make one focused `[skip ci]` commit on `work/1001-daren-keykeeper-partial`.

## Hermes-Owned Lifecycle

- [x] T017 Independent review of #1001 implementation and literary quality bar.
- [x] T018 Create/update PR with local verification evidence and parent #955 scope boundary.
- [ ] T019 Squash-merge PR after local gates and review, using local verification rather than GitHub Actions.
- [ ] T020 Comment closure evidence on #1001, close as completed, confirm issue state closed.
- [ ] T021 Clean up worktree/branches and report next target.

## Evidence Log

- T001: Branch `work/1001-daren-keykeeper-partial` started from `origin/main` at `2cd7ee4`; source issue #1001 and parent #955 are the tracked tasks. #1000 / PR #1033 is already merged/closed in `main`, starting the `guard_interrogation_action` result trio and making #1001 the next logical sibling.
- T002: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #1001.
- T003 baseline focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 64 passed / 0 failed / 0 skipped / 64 total.
- T003 baseline affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 333 passed / 0 failed / 0 skipped / 333 total.
- T004 Spec Kit prerequisites: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-1001-daren-keykeeper-partial\\specs\\1001-daren-keykeeper-partial` with `contracts/` and `tasks.md`.
- T005: Added `DarenGuardInterrogationPartial_ReadsAsMixedKeykeeperAftermathWithoutMechanicDrift` in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; the guard rejects the old one-sentence partial, checks substantial mixed aftermath requirements, pins `guard_interrogation_action` mechanics/config/routing/score deltas, and preserves sibling result identities.
- T006 RED focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 64 passed / 1 failed / 0 skipped / 65 total. Expected failure: `Assert.NotEqual()` rejected the unchanged old partial string `Лукьян пропускает Дарена с сомнением...`.
- T007: Replaced only `guard_interrogation_action` partial prose in `BookOfEternityClient/Services/QteSceneService.Daren.cs`. New aftermath metrics: 2,386 characters, 17 counted sentences, 7 `Дарен` mentions, 360 words.
- T008 GREEN focused Daren command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` => 65 passed / 0 failed / 0 skipped / 65 total. One initial GREEN attempt exposed a motif wording gap (`тиш`/`молч`); root cause was prose using `тихой`/`бесшумно`, fixed by naming `тишина` in the gallery sentence.
- T009 affected C# slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` => 334 passed / 0 failed / 0 skipped / 334 total.
- T010 builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` => 0 warnings / 0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` => 0 warnings / 0 errors.
- T011 diff hygiene: `git diff --check` returned exit 0 with only CRLF conversion warnings for the two edited C# files; committed `git diff --check origin/main...HEAD` is rerun in the final handoff verification.
- T012 static scan: added-line code/test scan for hardcoded secrets, shell invocation, eval, unsafe deserialization, and SQL formatting returned `NO_MATCHES`; added production prose scan for forbidden player-facing technical terms returned `NO_MATCHES`.
- T013 diff inspection: only `guard_interrogation_action` partial prose, focused Daren test coverage, and the shared long-result allowance for this exact partial result changed; no QTE mechanics, route ids, beat order, action ids/check types/config, routing, score deltas, rewards/profile/New Game grants, endpoints, runtime state, frontend/browser code, success/fail prose, downstream lock-pick prose, or downstream rune-memory prose changed.
- T014 reconciliation: `spec.md`, `plan.md`, `tasks.md`, `contracts/daren-result-aftermath.md`, and `checklists/requirements.md` reference #1001/#955/#973/#1000/#1002/#1003/#1004/#1005/#1006/#1007/#1008; Codex-owned tasks/checklist rows now match the focused implementation diff and evidence.
- T015 scope boundary: #1000 success, #1002 fail, downstream #1003-#1008 result surfaces, parent #955 closure, frontend, runtime state, rewards, profile/New Game grants, and GM-facing docs/examples remain out of scope.
- T016: Prepared one focused `[skip ci]` implementation commit on `work/1001-daren-keykeeper-partial`; final SHA is reported in the Codex handoff.
- T017 independent review: detached Codex review run `E:/Games/codex-runs/20260614-184152-review-boe-1001-daren-keykeeper-partial` reviewed commit `0b02cc79a9cc4e42abb288fb8145864687301d18` against `origin/main...HEAD` and returned `APPROVED` with no Critical/Important/Minor findings. Review reran focused Daren `65 passed / 0 failed / 0 skipped`, `git diff --check origin/main...HEAD` clean, and production-prose technical-term scan no matches; Spec Kit prerequisite check failed only because the review worktree was detached, while artifacts were reviewed directly.
- T018 PR evidence: created PR [#1034](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1034) from `work/1001-daren-keykeeper-partial` to `main` with local verification evidence, independent-review result, `Closes #1001`, parent #955 boundary, and safe non-closing references for siblings/downstream issues. Initial PR head was `7bb3365cf63e653186e212079de93160cd9602ac`; this lifecycle-evidence amend updates the final branch head after PR creation.
- T019-T021: pending Hermes-owned merge, issue closure, parent #955 boundary confirmation, and cleanup evidence.
