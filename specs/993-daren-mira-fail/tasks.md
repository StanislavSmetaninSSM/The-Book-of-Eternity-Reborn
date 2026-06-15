# Tasks: Daren Mira Whisper Fail Literary Aftermath

**Input**: `specs/993-daren-mira-fail/spec.md`, `plan.md`, issue #993 body, parent #955 context, source scene #970, completed same-scene siblings #991/#992, previous-result siblings #988/#989/#990, completed downstream #994-#1008, and existing Daren route/test patterns.
**Tracked issue and related context**: [#993](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/993), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), source scene [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970), completed siblings [#991](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/991) and [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992), previous-result siblings [#988](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/988), [#989](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/989), [#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990), and completed downstream siblings [#994](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/994)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008).

## Phase 0: Hermes Preparation

- [x] **T001 - Worktree/branch prepared**: Created `E:/Games/worktrees/boe-993-daren-mira-fail` on `work/993-daren-mira-fail` from `origin/main` at `d0cba80`.
- [x] **T002 - Source issue analyzed**: Confirmed #993 is open, labeled `status: triaged`, and asks only for `informant_parley_action` fail result prose.
- [x] **T003 - Spec Kit artifacts created**: Added `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and `tasks.md` under `specs/993-daren-mira-fail/`.
- [x] **T004 - Baseline focused gate recorded**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` passed 74/74 on 2026-06-15 before implementation.
- [x] **T005 - Baseline affected gate recorded**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed 343/343 on 2026-06-15 before implementation.
- [x] **T006 - Spec Kit prerequisite check recorded**: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-993-daren-mira-fail\specs\993-daren-mira-fail` with `AVAILABLE_DOCS=["contracts/","tasks.md"]`.

## Phase 1: Codex TDD Implementation

- [x] **T007 - Add RED focused guard**: Added `DarenInformantParleyFail_ReadsAsDangerousMiraWitnessAftermathWithoutMechanicDrift` to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; it rejects the old fail text, checks substantial length/sentence/Daren-count thresholds, grouped Mira/threat/witness/source-pressure motifs, forbidden technical terms, #991/#992 sibling sentinels, previous #988-#990 sentinels, downstream #994-#1008 sentinels, and unchanged action mechanics.
- [x] **T008 - Verify RED**: Focused Daren RED run failed as expected before production prose changes: 75 total, 74 passed, 1 failed; failing test `DarenInformantParleyFail_ReadsAsDangerousMiraWitnessAftermathWithoutMechanicDrift`; message `Daren informant_parley fail should reject the old one-sentence Mira threat result notification.`
- [x] **T009 - Rewrite only fail prose**: Replaced only the `informant_parley_action` fail result text in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- [x] **T010 - Verify GREEN focused gate**: Focused Daren GREEN run passed after implementation: 75 total, 75 passed, 0 failed, 0 skipped.
- [x] **T011 - Verify affected slice**: Affected Daren/QTE/docs/browser C# slice passed after implementation: 344 total, 344 passed, 0 failed, 0 skipped.
- [x] **T012 - Verify builds**: Client build passed with 0 warnings/0 errors; test-project build passed with 0 warnings/0 errors.
- [x] **T013 - Run hygiene checks**: Pre-commit working diff `git diff --check origin/main` passed; added-line static scan over code/test/spec diff returned `NO_MATCHES`. Codex reruns the required post-commit `git diff --check origin/main...HEAD` and static scan before final report.
- [x] **T014 - Reconcile Spec Kit evidence**: Updated `tasks.md` and `checklists/requirements.md` with RED/GREEN counts, prose metrics, build results, hygiene scan evidence, and feature-directory evidence before the implementation commit.
- [x] **T015 - Commit implementation**: Prepared one focused implementation commit with `[skip ci]`, including spec artifacts, tests, and production prose, and excluding run artifacts, `.hermes/`, `bin/`, `obj/`, `TestResults/`, frontend dependency artifacts, and unrelated scratch. Final SHA is reported by Codex after commit creation.

## Phase 2: Hermes Review / PR / Merge / Closure

- [x] **T016 - Hermes fresh verification**: Hermes reconciled Codex run `E:/Games/codex-runs/20260615-164324-boe-993-daren-mira-fail` (`exit-code.txt=0`, `final.md` present), confirmed `work/993-daren-mira-fail` is clean/ahead one at `69160ad`, confirmed no run artifacts entered the repo diff, and reran fresh gates before review/PR.
- [x] **T017 - Independent review**: Detached review run `E:/Games/codex-runs/20260615-170348-review-boe-993-daren-mira-fail` on exact head `69160ad` exited `0`; `final.md` verdict is `APPROVED` with no Critical/Important/Minor findings.
- [x] **T018 - PR creation**: Pushed `work/993-daren-mira-fail` and created PR #1044 closing only #993, with safe non-closing wording for sibling/parent references.
- [x] **T019 - Pre-merge reconciliation**: Recorded review and PR evidence in this Spec Kit artifact before merge; Hermes reruns focused sanity, `git diff --check`, and static scan after evidence amendments.
- [ ] **T020 - Squash merge and issue closure**: Squash-merge after local verification/review, fast-forward `main`, confirm PR merged and #993 closed/completed, and post an issue evidence comment.
- [ ] **T021 - Label and cleanup**: Move #993 from `status: in-progress` to `status: verified`, remove implementation/review worktrees and branches, and verify remote branch deletion.
- [ ] **T022 - Report and continue**: Send the Russian closure report, then select the next logical Daren sibling unless a higher-priority blocker appears.

## Hermes Lifecycle Evidence

- T001/T002: Branch `work/993-daren-mira-fail` started from `origin/main` at `d0cba80`; source issue #993 and parent #955 are the tracked tasks. #991 / PR #1042 and #992 / PR #1043 are already merged/closed in `main`, leaving #993 to complete the `informant_parley_action` result trio.
- T003: Created `spec.md`, `plan.md`, `contracts/daren-result-aftermath.md`, `checklists/requirements.md`, and this `tasks.md` for #993.

## Codex Implementation Evidence

- Prerequisite check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR=E:\Games\worktrees\boe-993-daren-mira-fail\specs\993-daren-mira-fail` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- RED focused guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~DarenQteShowcaseTests" --logger "console;verbosity=minimal"` failed before production prose changes with 75 total, 74 passed, 1 failed, 0 skipped; expected failing test `DarenInformantParleyFail_ReadsAsDangerousMiraWitnessAftermathWithoutMechanicDrift`; expected message `Daren informant_parley fail should reject the old one-sentence Mira threat result notification.`
- Prose metrics after rewrite: `informant_parley_action` fail result length 2485 characters, 23 scene sentences, 8 mentions of `Дарен`.
- GREEN focused guard: same focused Daren command passed after implementation with 75 total, 75 passed, 0 failed, 0 skipped.
- Affected slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"` passed with 344 total, 344 passed, 0 failed, 0 skipped.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings/0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings/0 errors.
- Hygiene: pre-commit `git diff --check origin/main` passed; added-line static scan over code/test/spec diff returned `NO_MATCHES` for hardcoded secrets, shell execution, standalone eval/exec, unsafe deserialization, and SQL string formatting.
- Scope readback: production diff is limited to `BookOfEternityClient/Services/QteSceneService.Daren.cs` fail prose; test diff is limited to `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`; Spec Kit updates are limited to this feature directory.

## Hermes Verification / Review Evidence

- T016 Hermes fresh verification: run artifacts reconciled from `E:/Games/codex-runs/20260615-164324-boe-993-daren-mira-fail`; `exit-code.txt=0`, `final.md` present, implementation commit `69160ad`, branch clean/ahead one, no PR existed, issue #993 still `OPEN/status: in-progress`, and no run artifacts appeared in `git diff origin/main...HEAD`.
- T016 fresh gates: Spec Kit prerequisite resolved the active feature directory; focused Daren `75/75`; affected Daren/QTE/docs/browser slice `344/344`; client build `0 warnings/0 errors`; test-project build `0 warnings/0 errors`; `git diff --check origin/main...HEAD` clean; added-line code/test/spec static scan `NO_MATCHES`; refined production-prose technical/meta scan `NO_MATCHES`.
- T017 independent review: detached review worktree `E:/Games/worktrees/boe-993-daren-mira-fail-review-20260615-170348` reviewed exact head `69160ad`; review run `E:/Games/codex-runs/20260615-170348-review-boe-993-daren-mira-fail` exited `0`; verdict `APPROVED`; Critical/Important/Minor findings: none.
- T018 PR evidence: PR #1044 created from `work/993-daren-mira-fail` with `Closes #993` as the only closing reference. GitHub readback showed `state=OPEN`, `mergeStateStatus=CLEAN`, and `closingIssuesReferences=[#993]` before final evidence amend/force-push.
- T019 pre-merge reconciliation: PR/review evidence was recorded before merge; final PR head is read back from GitHub after the evidence-only amend and force-push.
