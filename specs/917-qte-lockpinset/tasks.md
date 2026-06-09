# Tasks: QTE v2 LockPinSet

**Input**: `specs/917-qte-lockpinset/spec.md`, `specs/917-qte-lockpinset/plan.md`, `specs/917-qte-lockpinset/contracts/lockpinset-qte-contract.md`

**Source issues**: #917 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/917; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Browser parity, scoring/ranks, and practice/training modes remain out of scope.

## Implementation Evidence

- Setup: branch `work/917-qte-lockpinset`, active feature path `specs/917-qte-lockpinset/`, issue #917 and parent #911 verified with `gh issue view`; issue body matched this spec's scope.
- Baseline before Spec Kit artifact creation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` passed with 163 passed, 0 failed, 0 skipped.
- Independent review fix run: added live-control reachability coverage for the committed `18..32` first-pin example window; live console controls now raise with `adjustKey` and lower with `Shift+adjustKey`. Added hard-difficulty regression coverage proving `baseDifficulty` 4/5 configs with `partialMaxTimeMs == timerMs` still resolve success/partial/fail after effective threshold clamping.
- Independent re-review fix run: validation now rejects identical effective `adjustKey`/`setKey` values, including conflicts with defaults, so accepted LockPinSet offers cannot map raise/lower and confirm to the same live control. Docs/spec contract/source guards now state the keys must be distinct.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/917-qte-lockpinset`, `git status --short`, source issues #917/#911, and active Spec Kit feature path `specs/917-qte-lockpinset/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #917/#911 issue body, #912/#913/#914/#915/#916/#920 QTE artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in final evidence.
  - Evidence: baseline passed 163/163, failed 0, skipped 0 before Spec Kit artifact creation or production changes.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the initial LockPinSet contract in `specs/917-qte-lockpinset/contracts/lockpinset-qte-contract.md`, including required fields, grade thresholds, pin windows, durability/mistake model, timer pressure, cancel behavior, stable console guidance, and browser boundary.
- [X] T005 Decide and document bounded limits for `pinCount`, `pinWindows`, `timerMs`, `pickDurability`, `maxMistakes`, `pinDriftPerSecond`, and `gradeThresholds` in spec/contract.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective LockPinSet requirements.
- [X] T007 Run Spec Kit prerequisite/discoverability check against `specs/917-qte-lockpinset/` before production implementation.
  - Evidence: prerequisite check returned `FEATURE_DIR=E:\Games\worktrees\boe-917-qte-lockpinset\specs\917-qte-lockpinset` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`; monotonic runtime tests pass in `QteSceneServiceTests`.

---

## Phase 3: User Story 1 - GM can author valid LockPinSet offers

**Goal**: Validation accepts a valid LockPinSet QTE offer and rejects malformed LockPinSet config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated LockPinSet offers.

### Tests for User Story 1

- [X] T008 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid LockPinSet offer produces no LockPinSet config issue.
- [X] T009 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for missing/non-object config, invalid `pinCount`, malformed `pinWindows`, invalid `timerMs`, invalid `pickDurability`, invalid `maxMistakes`, invalid `pinDriftPerSecond`, missing tri-grade routing, missing grade thresholds, and non-monotonic success/partial grade thresholds.
- [X] T010 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing LockPinSet support, not for test typos.
  - RED evidence: validation filter failed because valid LockPinSet was rejected as `qte_invalid_check_type` and malformed mutations lacked their expected `qte_lock_pin_set_*` codes.

### Implementation for User Story 1

- [X] T011 [US1] Update QTE validation in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` to recognize `LockPinSet` and validate required config fields.
- [X] T012 [US1] Reuse existing QTE constants/helpers where appropriate and avoid adding any GM-authored keyboard-layout map for LockPinSet.
- [X] T013 [US1] Rerun the `ValidationServiceQteTests` filter and verify LockPinSet validation tests pass without regressing v1 QTE, MashInput, PatternMemory, RhythmPulse, PrecisionChoice, or StealthNoise validation.
  - GREEN evidence: validation filter passed with 113 passed, 0 failed, 0 skipped.

---

## Phase 4: User Story 2 - Console player can resolve LockPinSet locally

**Goal**: Console QTE runtime resolves lockpicking pin pressure to success, partial, fail, cancel, timeout, and broken-pick outcomes through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T014 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving LockPinSet clean success, slow/noisy partial, and fail grade calculations from elapsed time, pin positions/windows, mistakes, and pick durability.
- [X] T015 [US2] Add a RED deterministic test proving exceeding `maxMistakes` or breaking the pick resolves as `fail`.
- [X] T016 [US2] Add a RED deterministic test proving timeout resolves using the configured grade thresholds and defaults to `fail` when the lock is not open.
- [X] T017 [US2] Add a RED deterministic test proving Escape/cancel resolves LockPinSet as `fail` and does not throw.
- [X] T018 [US2] Add a RED deterministic test proving malformed config resolves safely as `fail` in runtime.
- [X] T019 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing LockPinSet resolver support.
  - RED evidence: first runtime filter failed on missing `QteSceneService.LockPinSet*` helper types; after adding a skeleton, the filter failed behaviorally with expected `success` vs actual `fail`.

### Implementation for User Story 2

- [X] T020 [US2] Add focused LockPinSet config/effective-requirement/helper types in or near `BookOfEternityClient/Services/QteSceneService.cs` for deterministic testing.
- [X] T021 [US2] Wire `check.type = "LockPinSet"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T022 [US2] Implement console pin-state/timer/durability copy with clear Russian labels, per-pin state, target-window feedback, remaining-time cue, mistake/durability cue, and warning state.
- [X] T023 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing LockPinSet screens.
- [X] T024 [US2] Rerun the `QteSceneServiceTests` filter and verify new LockPinSet tests plus existing #920/v1/#912/#913/#914/#915/#916 tests pass.
  - GREEN evidence: `QteSceneServiceTests` filter passed with 73 passed, 0 failed, 0 skipped.

---

## Phase 5: User Story 3 - Difficulty and characteristic influence LockPinSet fairly

**Goal**: Effective LockPinSet requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective pin windows, drift, durability/mistakes, or timer across difficulty/stat tiers.

- [X] T025 [US3] Add RED tests proving higher relevant stat tier does not make the same LockPinSet config harder.
- [X] T026 [US3] Add RED tests proving higher `baseDifficulty` does not make the same LockPinSet config easier.
- [X] T027 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T028 [US3] Rerun `QteSceneServiceTests` and verify all LockPinSet difficulty/stat tests pass.
  - Evidence: monotonic stat/difficulty tests pass in the 73-test `QteSceneServiceTests` run; contract formula reconciled in `contracts/lockpinset-qte-contract.md`.

---

## Phase 6: User Story 4 - GM-facing docs and examples teach LockPinSet

**Goal**: GM rules/API/example documentation coverage stays synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover LockPinSet guidance and the worked example.

- [X] T029 [US4] Update `CLI_API_Specification.md` to list LockPinSet as a QTE v2 node type with required config fields, grade thresholds, pin windows, timer pressure, durability/mistakes, stable console guidance, and browser boundary.
- [X] T030 [US4] Update `Rules/Block_CLI_QTE.txt` to list LockPinSet as a QTE v2 node type with required config fields, limits, lockpicking/pin guidance, durability controls, and browser boundary.
- [X] T031 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid LockPinSet lockpicking scene demonstrating pin windows, durability pressure, and success/partial/fail routing.
- [X] T032 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
- [X] T033 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs` so LockPinSet docs/examples cannot drift from validation.
- [X] T034 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.
  - Evidence: `PromptDocumentationCoverageTests|ExampleDocumentationValidationTests` passed with 15 passed, 0 failed, 0 skipped; `ValidationServiceQteTests` passed with 113 passed, 0 failed, 0 skipped. Manifest update was not required because the existing QTE example remains covered by example validation.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T035 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive LockPinSet unless required to keep existing read-only metadata honest.
- [X] T036 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [X] T037 Verify existing v1 QTE, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, #915 PrecisionChoice, and #916 StealthNoise behavior remains compatible by running the focused QTE service and validation filters.
  - Evidence: browser QTE surfaces remain manual-grade/read-only for non-BranchChoice actions and do not claim interactive LockPinSet support; no frontend files changed, so the frontend gate was not applicable. Final focused filter passed with 201 passed, 0 failed, 0 skipped.

---

## Phase 8: Final verification, commit, and Hermes-owned closure

- [X] T038 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
  - Evidence: passed with 201 passed, 0 failed, 0 skipped.
- [X] T039 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
  - Evidence: build succeeded with 0 warnings and 0 errors.
- [X] T040 Run Spec Kit prerequisite/discoverability gate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
  - Evidence: returned `FEATURE_DIR=E:\Games\worktrees\boe-917-qte-lockpinset\specs\917-qte-lockpinset` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- [X] T041 Run `git diff --check origin/main...HEAD`.
  - Evidence: post-commit `git diff --check origin/main...HEAD` completed with no whitespace errors.
- [X] T042 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/docs plan recipe false positives.
  - Evidence: added-line scan over committed code/test changes found no hardcoded credentials, shell execution, eval/exec, unsafe deserialization, or SQL string-formatting matches after excluding docs/specs.
- [X] T043 Reconcile `specs/917-qte-lockpinset/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
  - Evidence: contract formulas match the runtime helper, docs/examples/source guards match validation limits, and verification evidence is recorded in this task list.
- [X] T044 Create one focused implementation commit with message `feat(qte): add LockPinSet QTE v2 support [skip ci]`.
  - Evidence: focused implementation commit created with the required message; Hermes owns review, PR, merge, and issue closure.
- [X] T045 Hermes-owned: obtain independent review before PR/merge.
  - Evidence: independent final re-review returned `VERDICT: APPROVED` after the distinct `adjustKey`/`setKey` blocker was fixed and locally verified.
- [ ] T046 Hermes-owned: merge PR, post issue evidence comment, close #917, fast-forward `main`, and remove the temporary worktree/branch.
  - Evidence in progress: PR #934 opened at https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/934 after local gates and independent approval; merge/issue evidence/main verification/cleanup remain Hermes-owned.

## Dependencies & Execution Order

- Phases 1-2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same commit as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
