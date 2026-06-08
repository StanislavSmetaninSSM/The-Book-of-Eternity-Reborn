# Tasks: QTE v2 MashInput

**Input**: `specs/912-qte-mashinput/spec.md`, `specs/912-qte-mashinput/plan.md`, `specs/912-qte-mashinput/contracts/mashinput-qte-contract.md`

**Source issues**: #912 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/912; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Later QTE v2 child issues remain out of scope.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/912-qte-mashinput`, `git status --short`, source issues #912/#911, and active Spec Kit feature path `specs/912-qte-mashinput/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #912/#911 issue bodies, #920 QTE layout artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in PR evidence.
  - Evidence: baseline passed 50/50, failed 0, skipped 0.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the final MashInput contract in `specs/912-qte-mashinput/contracts/mashinput-qte-contract.md`, including required fields, supported key tokens, validation errors, local resolution, cancel behavior, and browser boundary.
- [X] T005 Decide and document bounded numeric limits for `durationMs` and `targetPresses` in code/tests/docs; values must be playable and testable.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective MashInput requirements.

---

## Phase 3: User Story 1 — GM can author valid MashInput offers

**Goal**: Validation accepts a valid MashInput QTE offer and rejects malformed MashInput config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated MashInput offers.

### Tests for User Story 1

- [X] T007 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid MashInput offer produces no MashInput config issue.
- [X] T008 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for empty `keys`, unsupported key token, duplicate key token, invalid `durationMs`, invalid `targetPresses`, and invalid `partialThreshold`.
- [X] T009 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing MashInput support, not for test typos.
  - RED evidence: failed 10/12 with `qte_invalid_check_type` and missing `qte_mash_input_*` issue codes before implementation.

### Implementation for User Story 1

- [X] T010 [US1] Update the QTE validation code in `BookOfEternityClient/Core/ValidationService*.cs` or the existing validation partial that owns QTE offer validation to recognize `MashInput` and validate required config fields.
- [X] T011 [US1] Reuse existing `QteKeyInput` canonical tokens when validating MashInput keys; do not create a separate keyboard-layout mapping.
- [X] T012 [US1] Rerun the `ValidationServiceQteTests` filter and verify the MashInput validation tests pass without regressing v1 QTE validation.
  - GREEN evidence: `ValidationServiceQteTests` passed 12/12, failed 0, skipped 0.

---

## Phase 4: User Story 2 — Console player can resolve MashInput locally

**Goal**: Console QTE runtime resolves MashInput to success, partial, fail, and cancel through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T013 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving MashInput success, partial, and fail grade calculations from matching key counts.
- [X] T014 [US2] Add a RED deterministic test proving Escape/cancel resolves MashInput as `fail` and does not throw.
- [X] T015 [US2] Add a RED deterministic test proving RU/EN fallback characters from #920 count only for configured MashInput QTE keys.
- [X] T016 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing MashInput resolver support.
  - RED evidence: after rerunning sequentially, failed 4/43 because MashInput resolver/helper methods were missing.

### Implementation for User Story 2

- [X] T017 [US2] Add a focused MashInput config/effective-requirement helper in or near `QteSceneService.cs` if needed for deterministic testing.
- [X] T018 [US2] Wire `check.type = "MashInput"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T019 [US2] Implement console prompt/progress copy for MashInput with clear Russian timer/progress/key labels and `QteKeyInput.FormatPromptLabel` for physical/RU labels.
- [X] T020 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing MashInput screens.
- [X] T021 [US2] Rerun the `QteSceneServiceTests` filter and verify new MashInput tests and existing #920/v1 tests pass.
  - GREEN evidence: `QteSceneServiceTests` passed 43/43, failed 0, skipped 0.

---

## Phase 5: User Story 3 — Difficulty and characteristic influence MashInput fairly

**Goal**: Effective MashInput requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective targets across difficulty/stat tiers.

- [X] T022 [US3] Add RED tests proving higher relevant stat tier does not make the same MashInput config harder.
- [X] T023 [US3] Add RED tests proving higher `baseDifficulty` does not make the same MashInput config easier.
- [X] T024 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T025 [US3] Rerun `QteSceneServiceTests` and verify all MashInput difficulty/stat tests pass.

---

## Phase 6: User Story 4 — GM-facing docs and examples teach MashInput

**Goal**: GM rules/examples/documentation coverage stay synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover MashInput guidance and the worked example.

- [X] T026 [US4] Update `Rules/Block_CLI_QTE.txt` to list MashInput as a QTE v2 node type with required config fields and key-layout ownership.
- [X] T027 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid MashInput action/scene demonstrating rapid input, success/partial/fail routing, and positive success XP.
- [X] T028 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
  - Evidence: inspected manifest; no QTE-specific registration required for this example.
- [X] T029 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/` so MashInput docs/examples cannot drift from validation.
- [X] T030 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.
  - RED evidence: `PromptDocumentationCoverageTests` failed 1/5 before docs updates because `MashInput` was missing.
  - GREEN evidence: `PromptDocumentationCoverageTests` passed 5/5 and `ExampleDocumentationValidationTests` passed 5/5 after docs/example updates.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T031 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive MashInput unless required to keep existing read-only metadata honest.
- [X] T032 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
  - Evidence: no frontend files changed; frontend verify not applicable.
- [X] T033 Verify existing v1 QTE behavior remains compatible by running the focused QTE service and validation filters.

---

## Phase 8: Final verification, review, PR, and closure

- [X] T034 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
  - Evidence: passed 65/65, failed 0, skipped 0.
- [X] T035 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
  - Evidence: build succeeded with warnings 0, errors 0.
- [X] T036 Run `git diff --check origin/main...HEAD`.
  - Evidence: exit code 0, no output.
- [X] T037 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
  - Evidence: added-line scan excluding `specs/**` returned `NO_MATCHES`.
- [X] T038 Reconcile `specs/912-qte-mashinput/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
- [X] T039 Obtain independent review; fix Critical/Important findings before PR/merge.
  - Evidence: independent Codex review in `E:/Games/codex-runs/20260609-0705-boe-912-qte-mashinput-review` returned `APPROVED` with no blocking findings.
- [ ] T040 Create PR with local-gate evidence, squash-merge with `[skip ci]`, verify PR merged and #912 closed, post issue evidence comment, fast-forward `main`, and remove the temporary worktree/branch.

## Dependencies & Execution Order

- Phases 1–2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same PR as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
