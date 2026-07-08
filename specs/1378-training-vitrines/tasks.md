# Tasks: Training Vitrines

**Input**: Design documents from `specs/1378-training-vitrines/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/training-showcases.md

**Tests**: Behavior changes require test-first tasks. Validator/contract tests are mandatory before implementation.

## Phase 1: Setup

- [x] T001 Confirm clean worktree `feature/1378-training-system`, source issues #1377-#1385, and active Spec Kit path.
- [x] T002 Read AGENTS.md, constitution, spec, plan, tasks, and nearby trade/progression/validator code.
- [x] T003 Identify existing state files for NPC skills, player skills, XP/current-level progress, Spiritual Arts, Guardian/resident profiles, and trade receipts.
- [x] T004 Identify console command/catalog and browser command-result rendering entrypoints.

## Phase 2: Foundational Contract and Tests

- [x] T005 [P] Add failing tests for training showcase staleness metadata and purchase receipt legality in `BookOfEternityClient.Tests`.
- [x] T006 [P] Add failing tests for Mortal teacher cap, current-level XP spending, and no-delevel rule.
- [x] T007 [P] Add failing tests for afterlife mentor discounts, self-training multipliers, and special-art fallback unlock rejection.
- [x] T008 [P] Add failing docs/source-guard coverage for Mortal and afterlife GM training examples.
- [x] T009 Define canonical JSON/state paths and response fields for showcases, refresh requests, and receipts.

## Phase 3: Mortal Teacher Training (P1)

- [x] T010 [US1] Implement training state models and load/save helpers in `BookOfEternityClient/Services/`.
- [x] T011 [US1] Implement Mortal teacher showcase discovery, freshness checks, and offer evaluation.
- [x] T012 [US1] Implement Mortal purchase execution with money and current-level XP progress deductions.
- [x] T013 [US1] Implement Mortal receipt writing and validation checks.
- [x] T014 [US1] Add Mortal training fixture data for at least one teacher with multiple learn/upgrade offers.
- [x] T015 [US1] Verify US1 independently in tests.

## Phase 4: Afterlife Mentor Training (P1)

- [x] T016 [US2] Implement afterlife mentor showcase discovery, freshness checks, and offer evaluation.
- [x] T017 [US2] Implement mentor price modifiers: 100%, 80%, 60%.
- [x] T018 [US2] Implement self-training fallback multipliers: standard art 400%, Soul Focus 300%, known special art 500%.
- [x] T019 [US2] Block fallback unlock of new special Spiritual Arts.
- [x] T020 [US2] Implement afterlife purchase receipt writing and validation checks.
- [x] T021 [US2] Add Chaos Sea and Shining Abode fixture data for mentor training.
- [x] T022 [US2] Verify US2 independently in tests.

## Phase 5: Refresh and Validation Guards (P1)

- [x] T023 [US3] Implement refresh request creation for missing/stale training showcases.
- [x] T024 [US3] Add validation/normalizer checks for wrong realm, missing actor, cap mismatch, stale snapshot, resource mismatch, and duplicate offers.
- [x] T025 [US3] Add repair-friendly validation messages that tell the GM exactly which showcase/receipt is wrong.
- [x] T026 [US3] Verify stale and illegal cases independently in tests.

## Phase 6: Console UI (P2)

- [x] T027 [US4] Register `/обучение` and English alias in the command catalog/help.
- [x] T028 [US4] Add console teacher/mentor selectors, offer cards, detail actions, buy, refresh, and back actions.
- [x] T029 [US4] Ensure console escaping/localization and no raw JSON/internal keys in player-facing output.
- [x] T030 [US4] Add command-output tests or snapshots where existing patterns support them.

## Phase 7: Browser UI (P2)

- [x] T031 [US5] Extend command result payloads for structured training data.
- [x] T032 [US5] Render training teachers/mentors/offers using the approved nested-card data prototype.
- [x] T033 [US5] Add selector/filter behavior for many teachers/offers.
- [x] T034 [US5] Add frontend tests/verification for localized labels and non-flattened nested data.

## Phase 8: GM Docs, Examples, and Live Tests (P2)

- [x] T035 [US6] Update Mortal World TaskGuides/prompts with teacher showcase and receipt workflow.
- [x] T036 [US6] Update afterlife guides/examples/coverage for mentor training, fallback costs, and special-art source limits.
- [x] T037 [US6] Update `Examples/example_validation_manifest.json` and documentation tests.
- [x] T038 [US6] Add training vitrines to the live GM test checklist.

## Phase 9: Verification and Integration

- [x] T039 Run focused C# tests for Training/Skill/SpiritualArt/Validation.
- [x] T040 Run documentation coverage tests.
- [x] T041 Run frontend verification.
- [ ] T042 Run manual console smoke checks for Mortal World, Chaos Sea, and Shining Abode.
- [ ] T043 Run manual browser smoke checks for training command output.
- [ ] T044 Review diffs, update issue comments, commit, and merge only after evidence is available.
- [x] T045 [Live-test harness follow-up #1452] Add exact training-showcase stale snapshot hash diagnostics and a dedicated validation repair packet so GM repairs can apply `sourceActorSnapshotHash` without source-code archaeology.
- [x] T046 [Live-test harness follow-up #1426] Block Mortal bootstrap acceptance when player-authored start promises a teacher/training surface but no usable `teacherProfile.canTeach=true` NPC exists for `/обучение`.
- [x] T047 [Live-test harness follow-up #1455] Dispatch missing/stale `/обучение` and Mortal NPC trade showcase GM requests immediately from console/browser command flows instead of waiting for the player's next ordinary turn.
- [x] T048 [Live-test harness follow-up #1460] Add a GM helper for `pending_training_showcase_requests.json` so Mortal and afterlife showcase refreshes can be resolved without manual source actor lookup or broad illegal JSON patches.
- [x] T049 [Live-test harness follow-up #1463] Dispatch paid Mortal `mortal_training_skill_evolution` requests immediately after purchase in console/browser command flows instead of leaving the player in the training screen with a silent pending request.

## Dependencies

- Phase 2 blocks implementation.
- Mortal and afterlife slices can proceed after shared models exist, but validation must be reconciled before UI is considered complete.
- Browser UI depends on structured command payloads.
- Docs/examples must be updated before completion because the GM authors showcase data.
