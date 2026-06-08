# Tasks: QTE v2 PatternMemory

**Input**: `specs/913-qte-patternmemory/spec.md`, `specs/913-qte-patternmemory/plan.md`, `specs/913-qte-patternmemory/contracts/patternmemory-qte-contract.md`

**Source issues**: #913 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/913; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Later QTE v2 child issues and browser parity remain out of scope.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/913-qte-patternmemory`, `git status --short`, source issues #913/#911, and active Spec Kit feature path `specs/913-qte-patternmemory/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #913/#911 issue bodies, #912/#920 QTE artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in PR evidence.
  - Evidence: baseline passed 65/65, failed 0, skipped 0.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the initial PatternMemory contract in `specs/913-qte-patternmemory/contracts/patternmemory-qte-contract.md`, including required fields, supported key tokens, validation errors, local resolution, cancel behavior, and browser boundary.
- [X] T005 Decide and document bounded numeric limits for `sequenceLength`, `revealMs`, `inputTimeoutMs`, and `allowedMistakes` in spec/contract.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective PatternMemory requirements.

---

## Phase 3: User Story 1 — GM can author valid PatternMemory offers

**Goal**: Validation accepts a valid PatternMemory QTE offer and rejects malformed PatternMemory config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated PatternMemory offers.

### Tests for User Story 1

- [X] T007 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid PatternMemory offer produces no PatternMemory config issue.
- [X] T008 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for empty `alphabet`, duplicate key token, unsupported key token, invalid `sequenceLength`, invalid `revealMs`, invalid `inputTimeoutMs`, and invalid `allowedMistakes`.
- [X] T009 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing PatternMemory support, not for test typos.

### Implementation for User Story 1

- [X] T010 [US1] Update the QTE validation code in `BookOfEternityClient/Core/ValidationService*.cs` or the existing validation partial that owns QTE offer validation to recognize `PatternMemory` and validate required config fields.
- [X] T011 [US1] Reuse existing `QteKeyInput` canonical tokens when validating PatternMemory alphabet; do not create a separate keyboard-layout mapping.
- [X] T012 [US1] Rerun the `ValidationServiceQteTests` filter and verify the PatternMemory validation tests pass without regressing v1 QTE or MashInput validation.

---

## Phase 4: User Story 2 — Console player can resolve PatternMemory locally

**Goal**: Console QTE runtime resolves PatternMemory reveal/input sequences to success, partial, fail, timeout, and cancel through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T013 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving PatternMemory exact repeat resolves `success`, tolerated mistakes resolve `partial`, and excessive mistakes resolve `fail`.
- [X] T014 [US2] Add a RED deterministic test proving timeout resolves PatternMemory as `fail` and does not throw.
- [X] T015 [US2] Add a RED deterministic test proving Escape/cancel during reveal/input resolves PatternMemory as `fail`.
- [X] T016 [US2] Add a RED deterministic test proving RU/EN fallback characters from #920 match only configured PatternMemory QTE keys.
- [X] T017 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing PatternMemory resolver support.

### Implementation for User Story 2

- [X] T018 [US2] Add a focused PatternMemory config/effective-requirement/sequence helper in or near `QteSceneService.cs` if needed for deterministic testing.
- [X] T019 [US2] Wire `check.type = "PatternMemory"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T020 [US2] Implement console reveal-phase and input-phase copy with clear Russian labels and `QteKeyInput.FormatPromptLabel` for physical/RU labels.
- [X] T021 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing PatternMemory screens.
- [X] T022 [US2] Rerun the `QteSceneServiceTests` filter and verify new PatternMemory tests plus existing #920/v1/#912 tests pass.

---

## Phase 5: User Story 3 — Difficulty and characteristic influence PatternMemory fairly

**Goal**: Effective PatternMemory requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective sequence/timing/tolerance across difficulty/stat tiers.

- [X] T023 [US3] Add RED tests proving higher relevant stat tier does not make the same PatternMemory config harder.
- [X] T024 [US3] Add RED tests proving higher `baseDifficulty` does not make the same PatternMemory config easier.
- [X] T025 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T026 [US3] Rerun `QteSceneServiceTests` and verify all PatternMemory difficulty/stat tests pass.

---

## Phase 6: User Story 4 — GM-facing docs and examples teach PatternMemory

**Goal**: GM rules/examples/documentation coverage stay synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover PatternMemory guidance and the worked example.

- [X] T027 [US4] Update `Rules/Block_CLI_QTE.txt` to list PatternMemory as a QTE v2 node type with required config fields, reveal/input phases, and key-layout ownership.
- [X] T028 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid PatternMemory action/scene demonstrating magical lock/rune memory, reveal/input phases, success/partial/fail routing, and positive success XP.
- [X] T029 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
- [X] T030 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/` so PatternMemory docs/examples cannot drift from validation.
- [X] T031 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T032 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive PatternMemory unless required to keep existing read-only metadata honest.
- [X] T033 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [X] T034 Verify existing v1 QTE, #920 key normalization, and #912 MashInput behavior remains compatible by running the focused QTE service and validation filters.

---

## Phase 8: Final verification, review, PR, and closure

- [X] T035 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
- [X] T036 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] T037 Run `git diff --check origin/main...HEAD`.
- [X] T038 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/plan recipe false positives.
- [X] T039 Reconcile `specs/913-qte-patternmemory/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
- [X] T040 Obtain independent review; fix Critical/Important findings before PR/merge.
  - Evidence: independent Hermes reviewer returned `APPROVED` with no Critical/Important findings after inspecting `origin/main...HEAD`, #913 acceptance criteria, Spec Kit artifacts, PatternMemory validation/runtime/docs, and fresh local gate evidence.
- [ ] T041 Create PR with local-gate evidence, squash-merge with `[skip ci]`, verify PR merged and #913 closed, post issue evidence comment, fast-forward `main`, and remove the temporary worktree/branch.

## Dependencies & Execution Order

- Phases 1–2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same PR as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
