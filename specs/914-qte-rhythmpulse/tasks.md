# Tasks: QTE v2 RhythmPulse

**Input**: `specs/914-qte-rhythmpulse/spec.md`, `specs/914-qte-rhythmpulse/plan.md`, `specs/914-qte-rhythmpulse/contracts/rhythmpulse-qte-contract.md`

**Source issues**: #914 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/914; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Later QTE v2 child issues, browser parity, scoring/ranks, and practice/training modes remain out of scope.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/914-qte-rhythmpulse`, `git status --short`, source issues #914/#911, and active Spec Kit feature path `specs/914-qte-rhythmpulse/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #914/#911 issue body supplied by the user, #912/#913/#920 QTE artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in final evidence.
  - Evidence: baseline passed 85/85, failed 0, skipped 0.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the initial RhythmPulse contract in `specs/914-qte-rhythmpulse/contracts/rhythmpulse-qte-contract.md`, including required fields, pattern variations, validation errors, local resolution, cancel behavior, visual/accessibility fallback, and browser boundary.
- [X] T005 Decide and document bounded numeric limits for `pulseCount`, `beatIntervalMs`, `hitWindowMs`, and `allowedMisses` in spec/contract.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective RhythmPulse requirements.
  - Evidence: `RhythmPulseEffectiveRequirement_IsMonotonicForStatTierAndDifficulty` passed in `QteSceneServiceTests`.
- [X] T007 Run Spec Kit prerequisite/discoverability check against `specs/914-qte-rhythmpulse/` before production implementation.
  - Evidence: prerequisite check returned `FEATURE_DIR` as `specs/914-qte-rhythmpulse` with `contracts/` and `tasks.md`.

---

## Phase 3: User Story 1 - GM can author valid RhythmPulse offers

**Goal**: Validation accepts a valid RhythmPulse QTE offer and rejects malformed RhythmPulse config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated RhythmPulse offers.

### Tests for User Story 1

- [X] T008 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid RhythmPulse offer produces no RhythmPulse config issue.
- [X] T009 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for zero/negative `pulseCount`, invalid `beatIntervalMs`, invalid/overlapping `hitWindowMs`, invalid/impossible `allowedMisses`, and malformed/unsupported `patternVariation`.
- [X] T010 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing RhythmPulse support, not for test typos.
  - RED evidence: `ValidationServiceQteTests` failed 12/37 because valid RhythmPulse was rejected as `qte_invalid_check_type` and malformed variants did not yet produce `qte_rhythm_pulse_*` codes.

### Implementation for User Story 1

- [X] T011 [US1] Update QTE validation in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` to recognize `RhythmPulse` and validate required config fields.
- [X] T012 [US1] Reuse existing QTE constants/helpers where appropriate and avoid adding any GM-authored keyboard-layout map for RhythmPulse.
- [X] T013 [US1] Rerun the `ValidationServiceQteTests` filter and verify RhythmPulse validation tests pass without regressing v1 QTE, MashInput, or PatternMemory validation.
  - GREEN evidence: `ValidationServiceQteTests` passed 37/37, failed 0, skipped 0.

---

## Phase 4: User Story 2 - Console player can resolve RhythmPulse locally

**Goal**: Console QTE runtime resolves RhythmPulse pulse timings to success, partial, fail, no-input timeout, and cancel through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T014 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving RhythmPulse success, partial, and fail grade calculations from pulse offsets and input offsets.
- [X] T015 [US2] Add a RED deterministic test proving no meaningful input by the end of the pulse pattern resolves RhythmPulse as `fail`.
- [X] T016 [US2] Add a RED deterministic test proving Escape/cancel resolves RhythmPulse as `fail` and does not throw.
- [X] T017 [US2] Add a RED deterministic test proving supported pattern variation generates a stable, strictly increasing schedule.
- [X] T018 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing RhythmPulse resolver support.
  - RED evidence: `QteSceneServiceTests` failed to compile because `RhythmPulseInput`, `RhythmPulseEffectiveRequirement`, and RhythmPulse helper methods were missing.

### Implementation for User Story 2

- [X] T019 [US2] Add focused RhythmPulse config/effective-requirement/schedule/helper types in or near `BookOfEternityClient/Services/QteSceneService.cs` for deterministic testing.
- [X] T020 [US2] Wire `check.type = "RhythmPulse"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T021 [US2] Implement console visual/text pulse copy with clear Russian labels, Space input guidance, progress counts, and remaining time.
- [X] T022 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing RhythmPulse screens.
- [X] T023 [US2] Rerun the `QteSceneServiceTests` filter and verify new RhythmPulse tests plus existing #920/v1/#912/#913 tests pass.
  - GREEN evidence: `QteSceneServiceTests` passed 54/54, failed 0, skipped 0.

---

## Phase 5: User Story 3 - Difficulty and characteristic influence RhythmPulse fairly

**Goal**: Effective RhythmPulse requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective pulse count/window/tolerance across difficulty/stat tiers.

- [X] T024 [US3] Add RED tests proving higher relevant stat tier does not make the same RhythmPulse config harder.
- [X] T025 [US3] Add RED tests proving higher `baseDifficulty` does not make the same RhythmPulse config easier.
- [X] T026 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T027 [US3] Rerun `QteSceneServiceTests` and verify all RhythmPulse difficulty/stat tests pass.
  - GREEN evidence: `QteSceneServiceTests` passed 54/54, including RhythmPulse monotonic helper coverage.

---

## Phase 6: User Story 4 - GM-facing docs and examples teach RhythmPulse

**Goal**: GM rules/API/example documentation coverage stays synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover RhythmPulse guidance and the worked example.

- [X] T028 [US4] Update `CLI_API_Specification.md` to list RhythmPulse as a QTE v2 node type with required config fields, variation limits, visual/accessibility fallback, and browser boundary.
- [X] T029 [US4] Update `Rules/Block_CLI_QTE.txt` to list RhythmPulse as a QTE v2 node type with required config fields, variation limits, visual/accessibility fallback, and browser boundary.
- [X] T030 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid RhythmPulse ritual or chase scene demonstrating pulse timing, success/partial/fail routing, and positive success XP.
- [X] T031 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
  - Evidence: inspected manifest; no QTE-specific registration is required because `Examples/E_CLI_QTE_Offer.txt` is already covered by the example parse sweep.
- [X] T032 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs` so RhythmPulse docs/examples cannot drift from validation.
- [X] T033 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.
  - RED evidence: `PromptDocumentationCoverageTests` failed 1/7 before docs updates because `RhythmPulse` was missing.
  - GREEN evidence: `PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests` passed 49/49, failed 0, skipped 0.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T034 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive RhythmPulse unless required to keep existing read-only metadata honest.
- [X] T035 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
  - Evidence: no frontend files changed; frontend verify not applicable.
- [X] T036 Verify existing v1 QTE, #920 key normalization, #912 MashInput, and #913 PatternMemory behavior remains compatible by running the focused QTE service and validation filters.
  - Evidence: final focused gate passed 103/103 across `QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests`.

---

## Phase 8: Final verification, commit, and Hermes-owned closure

- [X] T037 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
  - Evidence: passed 103/103, failed 0, skipped 0.
- [X] T038 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
  - Evidence: build succeeded with 0 warnings and 0 errors.
- [X] T039 Run Spec Kit prerequisite/discoverability gate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
  - Evidence: `FEATURE_DIR` resolved to `specs/914-qte-rhythmpulse` and `AVAILABLE_DOCS` included `contracts/` and `tasks.md`.
- [X] T040 Run `git diff --check origin/main...HEAD`.
  - Evidence: range diff check completed with no whitespace errors.
- [X] T041 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/docs plan recipe false positives.
  - Evidence: added-line scan returned `NO_MATCHES`.
- [X] T042 Reconcile `specs/914-qte-rhythmpulse/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
  - Evidence: spec, plan, tasks, and contract reflect the RhythmPulse runtime/validation/docs slice and local verification gates.
- [X] T043 Create one focused implementation commit with message `feat(qte): add RhythmPulse QTE v2 support [skip ci]`.
  - Evidence: one focused implementation commit created on `work/914-qte-rhythmpulse`.
- [ ] T044 Hermes-owned: obtain independent review, create/merge PR, post issue evidence comment, close #914, fast-forward `main`, and remove the temporary worktree/branch.
  - Review evidence: first independent review returned `CHANGES_REQUIRED` for transient Spec Kit active-feature pointers; `.specify/feature.json` was removed from the commit and the `AGENTS.md` Spec Kit managed block was restored to the generic current-plan pointer.
  - Re-review evidence: independent read-only review approved the current diff with no Critical or Important issues; it noted only a non-blocking future UX nit for elapsed-miss display and the expected Hermes-owned T044 orchestration caveat.

## Dependencies & Execution Order

- Phases 1-2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same commit as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
