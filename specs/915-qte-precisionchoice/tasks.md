# Tasks: QTE v2 PrecisionChoice

**Input**: `specs/915-qte-precisionchoice/spec.md`, `specs/915-qte-precisionchoice/plan.md`, `specs/915-qte-precisionchoice/contracts/precisionchoice-qte-contract.md`

**Source issues**: #915 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/915; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Later QTE v2 child issues, browser parity, scoring/ranks, and practice/training modes remain out of scope.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/915-qte-precisionchoice`, `git status --short`, source issues #915/#911, and active Spec Kit feature path `specs/915-qte-precisionchoice/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #915/#911 issue body, #912/#913/#914/#920 QTE artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in final evidence.
  - Evidence: baseline passed 103/103, failed 0, skipped 0.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the initial PrecisionChoice contract in `specs/915-qte-precisionchoice/contracts/precisionchoice-qte-contract.md`, including required fields, grade mapping, timeout behavior, decoy hints, local resolution, cancel behavior, stable console choice/timer guidance, and browser boundary.
- [X] T005 Decide and document bounded limits for `choices`, `timeoutMs`, `timeoutGrade`, and `decoyHints` in spec/contract.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective PrecisionChoice requirements.
  - Decision: effective timeout uses `timeoutMs - ((baseDifficulty - 3) * 300) + (statTier * 250)`, clamped to `1000..30000` and never below half the authored timeout; hint clarity reveals a monotonic count of escaped choice/decoy observations.
  - Evidence: `QteSceneServiceTests` PrecisionChoice effective-requirement tests passed in the focused GREEN run.
- [X] T007 Run Spec Kit prerequisite/discoverability check against `specs/915-qte-precisionchoice/` before production implementation.
  - Evidence: prerequisite check returned `FEATURE_DIR` as `specs/915-qte-precisionchoice` with `contracts/` and `tasks.md`.

---

## Phase 3: User Story 1 - GM can author valid PrecisionChoice offers

**Goal**: Validation accepts a valid PrecisionChoice QTE offer and rejects malformed PrecisionChoice config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated PrecisionChoice offers.

### Tests for User Story 1

- [X] T008 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid PrecisionChoice offer produces no PrecisionChoice config issue.
- [X] T009 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for missing/duplicate choices, missing `correctChoiceId`, correct id mismatch, invalid choice grade, invalid/impossible `timeoutMs`, invalid `timeoutGrade`, and malformed `decoyHints`.
- [X] T010 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing PrecisionChoice support, not for test typos.
  - RED evidence: command failed with 23 failed, 37 passed, 0 skipped, 60 total; failures showed `qte_invalid_check_type` and missing expected `qte_precision_choice_*` issue codes.

### Implementation for User Story 1

- [X] T011 [US1] Update QTE validation in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` to recognize `PrecisionChoice` and validate required config fields.
- [X] T012 [US1] Reuse existing QTE constants/helpers where appropriate and avoid adding any GM-authored keyboard-layout map for PrecisionChoice.
- [X] T013 [US1] Rerun the `ValidationServiceQteTests` filter and verify PrecisionChoice validation tests pass without regressing v1 QTE, MashInput, PatternMemory, or RhythmPulse validation.
  - GREEN evidence: command passed with 0 failed, 60 passed, 0 skipped, 60 total.

---

## Phase 4: User Story 2 - Console player can resolve PrecisionChoice locally

**Goal**: Console QTE runtime resolves timed choices to success, partial, fail, timeout, cancel, and invalid-selection outcomes through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T014 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving PrecisionChoice success, partial, and fail grade calculations from selected choice ids and elapsed time.
- [X] T015 [US2] Add a RED deterministic test proving no selection by timeout resolves according to `timeoutGrade`, defaulting to `fail`.
- [X] T016 [US2] Add a RED deterministic test proving Escape/cancel resolves PrecisionChoice as `fail` and does not throw.
- [X] T017 [US2] Add a RED deterministic test proving an unknown choice id resolves safely as `fail`.
- [X] T018 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing PrecisionChoice resolver support.
  - RED evidence: command failed during build with CS0426 for missing `QteSceneService.PrecisionChoiceChoice` and `QteSceneService.PrecisionChoiceEffectiveRequirement`.

### Implementation for User Story 2

- [X] T019 [US2] Add focused PrecisionChoice config/effective-requirement/helper types in or near `BookOfEternityClient/Services/QteSceneService.cs` for deterministic testing.
- [X] T020 [US2] Wire `check.type = "PrecisionChoice"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T021 [US2] Implement console choice/timer copy with clear Russian labels, stable numbering/columns, optional hint display, and remaining-time guidance.
- [X] T022 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing PrecisionChoice screens.
- [X] T023 [US2] Rerun the `QteSceneServiceTests` filter and verify new PrecisionChoice tests plus existing #920/v1/#912/#913/#914 tests pass.
  - GREEN evidence: command passed with 0 failed, 60 passed, 0 skipped, 60 total.

---

## Phase 5: User Story 3 - Difficulty and characteristic influence PrecisionChoice fairly

**Goal**: Effective PrecisionChoice requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective timeout and hint clarity across difficulty/stat tiers.

- [X] T024 [US3] Add RED tests proving higher relevant stat tier does not make the same PrecisionChoice config harder.
- [X] T025 [US3] Add RED tests proving higher `baseDifficulty` does not make the same PrecisionChoice config easier.
- [X] T026 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T027 [US3] Rerun `QteSceneServiceTests` and verify all PrecisionChoice difficulty/stat tests pass.
  - Evidence: included in the same RED build failure and GREEN `QteSceneServiceTests` run recorded for T018/T023.

---

## Phase 6: User Story 4 - GM-facing docs and examples teach PrecisionChoice

**Goal**: GM rules/API/example documentation coverage stays synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover PrecisionChoice guidance and the worked example.

- [X] T028 [US4] Update `CLI_API_Specification.md` to list PrecisionChoice as a QTE v2 node type with required config fields, grade mapping, timeout behavior, decoy hints, stable console guidance, and browser boundary.
- [X] T029 [US4] Update `Rules/Block_CLI_QTE.txt` to list PrecisionChoice as a QTE v2 node type with required config fields, limits, timer/choice guidance, and browser boundary.
- [X] T030 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid PrecisionChoice chase or trap scene demonstrating timed choice, success/partial/fail routing, and positive success XP.
- [X] T031 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
  - Decision: no manifest change required; the added QTE example is parseable by existing example validation without a new runtime scenario or exemption.
- [X] T032 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs` so PrecisionChoice docs/examples cannot drift from validation.
- [X] T033 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.
  - RED evidence: command failed with 1 failed, 72 passed, 0 skipped, 73 total because `PrecisionChoice` was missing from docs.
  - GREEN evidence: command passed with 0 failed, 73 passed, 0 skipped, 73 total.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T034 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive PrecisionChoice unless required to keep existing read-only metadata honest.
  - Evidence: inspected `QteWebInteractionService` and frontend QTE surfaces; no PrecisionChoice choices/timer/config metadata or React-side gameplay resolver exists in this slice.
- [X] T035 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
  - Not applicable: no frontend or browser files were changed.
- [X] T036 Verify existing v1 QTE, #920 key normalization, #912 MashInput, #913 PatternMemory, and #914 RhythmPulse behavior remains compatible by running the focused QTE service and validation filters.
  - Evidence: `QteSceneServiceTests` passed 60/60 and `ValidationServiceQteTests` passed 60/60 after implementation.

---

## Phase 8: Final verification, commit, and Hermes-owned closure

- [X] T037 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
  - Evidence: passed with 0 failed, 133 passed, 0 skipped, 133 total.
- [X] T038 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
  - Evidence: build succeeded with 0 warnings and 0 errors.
- [X] T039 Run Spec Kit prerequisite/discoverability gate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
  - Evidence: returned `FEATURE_DIR` = `E:\Games\worktrees\boe-915-qte-precisionchoice\specs\915-qte-precisionchoice` and `AVAILABLE_DOCS` = `contracts/`, `tasks.md`.
- [X] T040 Run `git diff --check origin/main...HEAD`.
  - Evidence: pre-commit working-tree `git diff --check` passed with exit 0; only CRLF normalization warnings were printed.
- [X] T041 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/docs plan recipe false positives.
  - Evidence: pre-commit high-signal source/test added-line scan found no secret pattern matches.
- [X] T042 Reconcile `specs/915-qte-precisionchoice/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
  - Evidence: implementation follows the existing spec/plan/contract decisions; `tasks.md` records RED/GREEN and gate evidence, and no accepted requirement change required spec/plan/contract edits.
- [X] T043 Create one focused implementation commit with message `feat(qte): add PrecisionChoice QTE v2 support [skip ci]`.
  - Evidence: focused implementation commit created with the required message.
- [X] T044 Hermes-owned: obtain independent review before PR/merge.
  - Evidence: independent Codex review in `E:/Games/codex-runs/20260610-024910-boe-915-precisionchoice-review` returned `Verdict: APPROVED` with no blocking findings.
- [ ] T045 Hermes-owned: merge PR, post issue evidence comment, close #915, fast-forward `main`, and remove the temporary worktree/branch.
  - Evidence: PR #932 was created for branch `work/915-qte-precisionchoice`: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/932. Remaining post-merge closure evidence will be recorded in GitHub issue/PR and the autonomous worker report after merge.

## Dependencies & Execution Order

- Phases 1-2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same commit as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
