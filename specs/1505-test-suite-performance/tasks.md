# Tasks: Test Suite Performance and Verification Lanes

**Input**: Design documents from `specs/1505-test-suite-performance/`

**Source issue**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

**Prerequisites**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [quickstart.md](quickstart.md)

## Phase 1: Setup and Baseline

- [x] T001 Confirm issue #1505, branch `work/1505-test-suite-performance`, isolated worktree, and clean tracked baseline.
- [x] T002 Read `AGENTS.md`, constitution, issue evidence, Spec Kit templates, validation orchestration, and guardian fixture structure.
- [x] T003 Record the 6,560-case inventory, 965 broad calls, 295 guardian calls, bounded timing samples, and fixed benchmark in `research.md`.
- [x] T004 Approve `spec.md` and define the no-gameplay/no-GM-contract scope.
- [x] T005 Update the active Spec Kit pointer and managed root `AGENTS.md` plan reference to feature 1505.

---

## Phase 2: Validation Selection Foundation

**Goal**: Add a fail-closed internal selector while preserving the public all-phase contract.

**Independent test**: `ValidationPhaseSelectionTests` prove invalid masks, phase isolation/order, all/public equivalence, and state isolation.

### Tests first

- [ ] T006 [US1] Add compile-failing selection API tests in `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`.
- [ ] T007 [US1] Run the focused test filter and retain RED evidence before production edits.

### Implementation

- [ ] T008 [US1] Add `GameStateValidationPhase` and mask validation in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`.
- [ ] T009 [US1] Add the internal overload and keep the public facade pinned to `All` in `BookOfEternityClient/Services/ValidationService.cs`.
- [ ] T010 [US1] Conditionally dispatch all 26 phases in canonical order in `ValidationService.ValidationPhases.cs`.
- [ ] T011 [US1] Run selection tests GREEN plus representative existing validation regressions.

---

## Phase 3: Guardian Migration

**Goal**: Remove the 26-phase multiplier from 295 sequential guardian calls.

**Independent test**: The fixed two-test benchmark is at least 5x faster and all 460 guardian cases retain their results.

### Tests first

- [ ] T012 [US1] Add guardian profile/broad-call source guards in `BookOfEternityClient.Tests/TestLaneSourceGuardTests.cs` and capture RED evidence.
- [ ] T013 [US1] Add named non-empty domain profiles in `BookOfEternityClient.Tests/GuardianValidationProfiles.cs`.

### Migration

- [ ] T014 [US1] Migrate `AcceptedAuthority`, `IdleValidation`, `LifecycleSnapshots`, and `PowerJournalOfferings` broad calls to reviewed profiles.
- [ ] T015 [US1] Migrate `ProjectsPower`, `QuestProgress`, `RivalResidents`, and `TradeOfferingResonance` broad calls to reviewed profiles.
- [ ] T016 [US1] Run representative methods from every guardian domain and expand a profile only from focused failure evidence.
- [ ] T017 [US1] Run the complete guardian class bounded; preserve discovered cases/assertions and meet the broad-call budget.
- [ ] T018 [US1] Run the fixed benchmark three times and record median speedup.
- [ ] T019 [US1] Partition safe guardian domains into independent classes only if T017/T018 show the accepted budgets still require it.

---

## Phase 4: Production Equivalence

**Goal**: Demonstrate that runtime validation remains the same full pipeline.

**Independent test**: Public validation and explicit `All` return identical ordered issues on valid and invalid fixtures.

- [ ] T020 [US2] Verify all runtime callers still use the public no-argument method.
- [ ] T021 [US2] Run full/all equivalence and phase-order tests on representative fixtures.
- [ ] T022 [US2] Add a source guard preventing scoped validation use outside the runtime dispatcher and test assembly.

---

## Phase 5: Predictable Verification Lanes

**Goal**: Provide bounded fast, focused, explicit slow, and complete commands.

**Independent test**: Source guards prove classification coverage and runner tests prove exact lane filters/timeouts.

### Tests first

- [ ] T023 [US3] Extend `TestLaneSourceGuardTests.cs` with RED checks for `FullValidation`, `ProcessIntegration`, and `E2E` classification.
- [ ] T024 [US3] Add RED source checks for lane filter mapping, TRX/log output, timeout, and owned-tree termination.

### Implementation

- [ ] T025 [US3] Add traits to intentional broad-validation, real process-integration, and E2E classes/methods.
- [ ] T026 [US3] Implement `scripts/test-csharp.ps1`.
- [ ] T027 [US3] Document commands, filters, results, and expected durations in `docs/testing.md`.
- [ ] T028 [US3] Run focused source guards GREEN and enumerate each lane.

---

## Phase 6: Regression Visibility

**Goal**: Prevent gradual reintroduction of the broad guardian multiplier.

- [ ] T029 [US4] Prove the guardian source guard fails against a temporary ninth/unapproved broad call or equivalent in-memory fixture.
- [ ] T030 [US4] Verify every retained full-pipeline sentinel is categorized and documented.
- [ ] T031 [US4] Verify bounded-run output records wall time, result, TRX/log paths, timeout state, and cleanup state.

---

## Phase 7: Final Verification and Integration

- [ ] T032 Run focused selection, source-guard, guardian-domain, and representative existing validation tests.
- [ ] T033 Run `dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore` with zero errors.
- [ ] T034 Run `Fast`, `FullValidation`, `ProcessIntegration`, and `E2E` bounded lanes and retain evidence.
- [ ] T035 Run one `Complete` lane with a 20-minute bound; confirm at most 15 minutes, expected discovery count, and no owned child processes.
- [ ] T036 Run `git diff --check`, inspect exact staged diff, reconcile Spec Kit artifacts, and record the no-GM-doc-update rationale.
- [ ] T037 Complete code review and resolve every Critical or Important finding with fresh evidence.
- [ ] T038 Commit, push, merge into `main`, verify local/remote hashes, and close #1505 only after all acceptance criteria pass.

## Dependencies and Execution Order

- T005 completes setup.
- T006–T011 are blocking foundation work.
- T012–T019 deliver the measured speedup.
- T020–T022 independently protect production equivalence.
- T023–T028 depend on stable category/profile names.
- T029–T031 depend on source guards and runner implementation.
- T032–T038 are final gates; T035 is the only complete-suite run.

## Notes

- Tests precede production changes for every behavior boundary.
- No task may claim speedup by removing assertions or silently reducing discovery.
- Do not launch an unbounded complete test run.
- Keep `.serena/` local and stage exact repository paths.
