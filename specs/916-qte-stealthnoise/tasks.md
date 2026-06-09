# Tasks: QTE v2 StealthNoise

**Input**: `specs/916-qte-stealthnoise/spec.md`, `specs/916-qte-stealthnoise/plan.md`, `specs/916-qte-stealthnoise/contracts/stealthnoise-qte-contract.md`

**Source issues**: #916 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/916; parent #911 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911.

**Tests**: Behavior changes require TDD. Write RED tests first, verify expected failure, implement minimal code, then rerun focused and broader gates.

**Organization**: Tasks are grouped by independently testable user stories. Later QTE v2 child issues, browser parity, scoring/ranks, and practice/training modes remain out of scope.

## Implementation Evidence

- Setup: branch `work/916-qte-stealthnoise`, active feature path `specs/916-qte-stealthnoise/`, issue #916 and parent #911 verified with `gh issue view`; latest issue bodies matched this spec's scope.
- Spec Kit prerequisite check before implementation: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-916-qte-stealthnoise\specs\916-qte-stealthnoise`, `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- RED validation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` failed as expected with 23 failed, 60 passed, 0 skipped; failures showed `StealthNoise` was still `qte_invalid_check_type` and no StealthNoise-specific config codes existed.
- RED runtime: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` failed as expected at compile with CS0426 missing `QteSceneService.StealthNoiseGradeThresholds`, `StealthNoiseEffectiveRequirement`, and `StealthNoiseInput`.
- RED documentation/source guard: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"` failed as expected with 1 failed, 8 passed, 0 skipped; `StealthNoise` was not documented yet.
- GREEN validation: same `ValidationServiceQteTests` command passed with 83 passed, 0 failed, 0 skipped after validator support.
- GREEN runtime: same `QteSceneServiceTests` command passed with 66 passed, 0 failed, 0 skipped after deterministic resolver/runtime support.
- GREEN docs/examples/validation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` passed with 97 passed, 0 failed, 0 skipped.
- Focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` passed with 163 passed, 0 failed, 0 skipped.
- Build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings and 0 errors.
- Diff hygiene gate: `git diff --check origin/main...HEAD` passed after the implementation commit.
- Added-line static security scan: `git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/**' ':(exclude)docs/**' ':(exclude)*plan*'` with credential/secret patterns found no matches after excluding QTE key-token terminology false positives.
- Browser/frontend gate: not run because no browser frontend files were changed; `BookOfEternityClient/WebUi/QteWebInteractionService.cs` was inspected and still exposes non-BranchChoice QTEs as submitted-grade/manual browser actions without claiming live StealthNoise parity.
- Artifact drift: `contracts/stealthnoise-qte-contract.md` example changed `primaryCharacteristic` from noncanonical `agility` to canonical `dexterity`, matching existing `Characteristics.All` and GM docs.

## Phase 1: Setup and baseline

- [X] T001 Confirm branch `work/916-qte-stealthnoise`, `git status --short`, source issues #916/#911, and active Spec Kit feature path `specs/916-qte-stealthnoise/`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #916/#911 issue body, #912/#913/#914/#915/#920 QTE artifacts, and nearby QTE service/validation/tests/docs before editing.
- [X] T003 Run focused baseline: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`; record exact pass/fail/skip counts in final evidence.
  - Evidence: baseline passed 133/133, failed 0, skipped 0 before production changes.

---

## Phase 2: Foundational contract decisions

- [X] T004 Document the initial StealthNoise contract in `specs/916-qte-stealthnoise/contracts/stealthnoise-qte-contract.md`, including required fields, grade thresholds, noise drift, recovery controls, cancel behavior, stable console meter/timer guidance, and browser boundary.
- [X] T005 Decide and document bounded limits for `durationMs`, `startingNoise`, `dangerThreshold`, `noiseDriftPerSecond`, `recoveryPerInput`, `allowedOverThresholdMs`, and `gradeThresholds` in spec/contract.
- [X] T006 Decide and test a monotonic difficulty/stat adjustment rule for effective StealthNoise requirements.
- [X] T007 Run Spec Kit prerequisite/discoverability check against `specs/916-qte-stealthnoise/` before production implementation.

---

## Phase 3: User Story 1 - GM can author valid StealthNoise offers

**Goal**: Validation accepts a valid StealthNoise QTE offer and rejects malformed StealthNoise config with precise field-specific errors.

**Independent Test**: `ValidationServiceQteTests` valid/mutated StealthNoise offers.

### Tests for User Story 1

- [X] T008 [US1] Add a RED validation test in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` proving a minimal valid StealthNoise offer produces no StealthNoise config issue.
- [X] T009 [US1] Add RED validation tests in `BookOfEternityClient.Tests/ValidationServiceQteTests.cs` for missing/non-object config, invalid `durationMs`, invalid `startingNoise`, invalid `dangerThreshold`, invalid `noiseDriftPerSecond`, invalid `recoveryPerInput`, invalid `allowedOverThresholdMs`, missing grade thresholds, and non-monotonic success/partial grade thresholds.
- [X] T010 [US1] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing StealthNoise support, not for test typos.

### Implementation for User Story 1

- [X] T011 [US1] Update QTE validation in `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` to recognize `StealthNoise` and validate required config fields.
- [X] T012 [US1] Reuse existing QTE constants/helpers where appropriate and avoid adding any GM-authored keyboard-layout map for StealthNoise.
- [X] T013 [US1] Rerun the `ValidationServiceQteTests` filter and verify StealthNoise validation tests pass without regressing v1 QTE, MashInput, PatternMemory, RhythmPulse, or PrecisionChoice validation.

---

## Phase 4: User Story 2 - Console player can resolve StealthNoise locally

**Goal**: Console QTE runtime resolves stealth/noise pressure to success, partial, fail, cancel, and over-threshold outcomes through existing routing.

**Independent Test**: Deterministic `QteSceneServiceTests` using pure helpers or injected input/time.

### Tests for User Story 2

- [X] T014 [US2] Add RED deterministic tests in `BookOfEternityClient.Tests/QteSceneServiceTests.cs` proving StealthNoise success, partial, and fail grade calculations from elapsed duration, recovery inputs, final noise, and accumulated over-threshold time.
- [X] T015 [US2] Add a RED deterministic test proving crossing the threshold beyond `allowedOverThresholdMs` resolves as `fail`.
- [X] T016 [US2] Add a RED deterministic test proving Escape/cancel resolves StealthNoise as `fail` and does not throw.
- [X] T017 [US2] Add a RED deterministic test proving malformed config resolves safely as `fail` in runtime.
- [X] T018 [US2] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests" --logger "console;verbosity=minimal"` and verify the new tests fail for missing StealthNoise resolver support.

### Implementation for User Story 2

- [X] T019 [US2] Add focused StealthNoise config/effective-requirement/helper types in or near `BookOfEternityClient/Services/QteSceneService.cs` for deterministic testing.
- [X] T020 [US2] Wire `check.type = "StealthNoise"` into the existing QTE action resolver in `BookOfEternityClient/Services/QteSceneService.cs`.
- [X] T021 [US2] Implement console noise-meter/timer copy with clear Russian labels, current-noise value, danger threshold, recovery input, remaining-time cue, and over-threshold warning.
- [X] T022 [US2] Preserve Spectre.Console escaping for dynamic GM/player-authored text and avoid raw debug/API wording in player-facing StealthNoise screens.
- [X] T023 [US2] Rerun the `QteSceneServiceTests` filter and verify new StealthNoise tests plus existing #920/v1/#912/#913/#914/#915 tests pass.

---

## Phase 5: User Story 3 - Difficulty and characteristic influence StealthNoise fairly

**Goal**: Effective StealthNoise requirements account for base difficulty and primary characteristic monotonically.

**Independent Test**: Focused helper tests compare effective drift, threshold/allowance, recovery, or duration across difficulty/stat tiers.

- [X] T024 [US3] Add RED tests proving higher relevant stat tier does not make the same StealthNoise config harder.
- [X] T025 [US3] Add RED tests proving higher `baseDifficulty` does not make the same StealthNoise config easier.
- [X] T026 [US3] Implement the smallest monotonic adjustment rule and document it in comments/docs where GM-facing behavior is described.
- [X] T027 [US3] Rerun `QteSceneServiceTests` and verify all StealthNoise difficulty/stat tests pass.

---

## Phase 6: User Story 4 - GM-facing docs and examples teach StealthNoise

**Goal**: GM rules/API/example documentation coverage stays synchronized with the new QTE contract.

**Independent Test**: Documentation/source guard and example validation tests cover StealthNoise guidance and the worked example.

- [X] T028 [US4] Update `CLI_API_Specification.md` to list StealthNoise as a QTE v2 node type with required config fields, grade thresholds, noise drift, recovery controls, stable console guidance, and browser boundary.
- [X] T029 [US4] Update `Rules/Block_CLI_QTE.txt` to list StealthNoise as a QTE v2 node type with required config fields, limits, noise-meter guidance, recovery controls, and browser boundary.
- [X] T030 [US4] Update `Examples/E_CLI_QTE_Offer.txt` with a short valid StealthNoise infiltration scene demonstrating noise threshold pressure and success/partial/fail routing.
- [X] T031 [US4] Update `Examples/example_validation_manifest.json` if required so the changed QTE example remains covered by example validation.
- [X] T032 [US4] Add or update docs/source guard tests in `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs` so StealthNoise docs/examples cannot drift from validation.
- [X] T033 [US4] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|ValidationServiceQteTests" --logger "console;verbosity=minimal"` and verify docs/examples/validation pass.

---

## Phase 7: Browser boundary and compatibility checks

- [X] T034 Inspect existing browser QTE DTO/metadata handling in `BookOfEternityClient/WebUi/QteWebInteractionService.cs` and `BookOfEternityClient.WebFrontend/`; do not implement full interactive StealthNoise unless required to keep existing read-only metadata honest.
- [X] T035 If frontend files change, add/update focused frontend tests and run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [X] T036 Verify existing v1 QTE, #920 key normalization, #912 MashInput, #913 PatternMemory, #914 RhythmPulse, and #915 PrecisionChoice behavior remains compatible by running the focused QTE service and validation filters.

---

## Phase 8: Final verification, commit, and Hermes-owned closure

- [X] T037 Run focused final gate: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
- [X] T038 Run build gate: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] T039 Run Spec Kit prerequisite/discoverability gate: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
- [X] T040 Run `git diff --check origin/main...HEAD`.
- [X] T041 Run added-line static security scan over `origin/main...HEAD`, excluding Spec Kit/docs plan recipe false positives.
- [X] T042 Reconcile `specs/916-qte-stealthnoise/spec.md`, `plan.md`, `tasks.md`, and contract with the final diff and verification evidence.
- [X] T043 Create one focused implementation commit with message `feat(qte): add StealthNoise QTE v2 support [skip ci]`.
- [X] T044 Hermes-owned: obtain independent review before PR/merge.
  - Evidence: independent Codex review in `E:/Games/codex-runs/20260610-034600-boe-916-stealthnoise-review` returned `Verdict: APPROVED` with no blocking findings.
- [ ] T045 Hermes-owned: merge PR, post issue evidence comment, close #916, fast-forward `main`, and remove the temporary worktree/branch.
  - Evidence: PR #933 was created for branch `work/916-qte-stealthnoise`: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/933. Remaining post-merge closure evidence will be recorded in GitHub issue/PR and the autonomous worker report after merge.

## Dependencies & Execution Order

- Phases 1-2 must complete before user-story implementation.
- US1 validation should complete before US2 runtime so invalid offers cannot enter the new resolver.
- US2 runtime should complete before US3 adjustment documentation is finalized.
- US4 docs/examples must land in the same commit as validation/runtime because this is a GM-authored contract change.
- Browser boundary work is inspection-first and only edits frontend files if existing surfaces would otherwise misrepresent support.

## Parallel Opportunities

- After contract decisions, validation tests and docs/source-guard tests can be drafted independently from runtime helper tests, but all touch the same branch and should be committed together after local verification.
