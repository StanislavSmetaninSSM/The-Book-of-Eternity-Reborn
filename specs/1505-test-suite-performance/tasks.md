# Tasks: Test Suite Performance and Verification Lanes

**Input**: Design documents from `specs/1505-test-suite-performance/`

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

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

- [x] T006 [US1] Add compile-failing selection API tests in `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`.
- [x] T007 [US1] Run the focused test filter and retain RED evidence before production edits.

### Implementation

- [x] T008 [US1] Add `GameStateValidationPhase` and mask validation in `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`.
- [x] T009 [US1] Add the internal overload and keep the public facade pinned to `All` in `BookOfEternityClient/Services/ValidationService.cs`.
- [x] T010 [US1] Conditionally dispatch all 26 phases in canonical order in `ValidationService.ValidationPhases.cs`.
- [x] T011 [US1] Run selection tests GREEN plus representative existing validation regressions.

---

## Phase 3: Guardian Migration

**Goal**: Remove the 26-phase multiplier from 295 sequential guardian calls.

**Independent test**: The fixed two-test benchmark is at least 5x faster and all 460 guardian cases retain their results.

### Tests first

- [x] T012 [US1] Add guardian profile/broad-call source guards in `BookOfEternityClient.Tests/TestLaneSourceGuardTests.cs` and capture RED evidence.
- [x] T013 [US1] Add named non-empty domain profiles in `BookOfEternityClient.Tests/GuardianValidationProfiles.cs`.

### Migration

- [x] T014 [US1] Migrate `AcceptedAuthority`, `IdleValidation`, `LifecycleSnapshots`, and `PowerJournalOfferings` broad calls to reviewed profiles.
- [x] T015 [US1] Migrate `ProjectsPower`, `QuestProgress`, `RivalResidents`, and `TradeOfferingResonance` broad calls to reviewed profiles.
- [x] T016 [US1] Run representative methods from every guardian domain and expand a profile only from focused failure evidence.
- [x] T017 [US1] Run all reviewed Guardian domain chunks and the retained
  broad-sentinel manifest under bounded controls; preserve discovered cases,
  assertions, and the broad-call budget.
- [x] T018 [US1] Run the fixed benchmark three times and record median speedup.
- [x] T019 [US1] Use discovery-validated, non-overlapping Guardian domain chunks and one isolated prepared fixture snapshot after bounded evidence showed physical class extraction was unnecessary and excessive concurrency was slower.

---

## Phase 4: Production Equivalence

**Goal**: Demonstrate that runtime validation remains the same full pipeline.

**Independent test**: Public validation and explicit `All` return identical ordered issues on valid and invalid fixtures.

- [x] T020 [US2] Verify all runtime callers still use the public no-argument method.
- [x] T021 [US2] Run full/all equivalence and phase-order tests on representative fixtures.
- [x] T022 [US2] Add a source guard preventing scoped validation use outside the runtime dispatcher and test assembly.

---

## Phase 5: Predictable Verification Lanes

**Goal**: Provide physically isolated fast/integration projects, bounded
focused diagnostics, and one globally bounded PreMerge command.

**Independent test**: Project/source guards prove dependency and classification
boundaries; runner tests prove explicit project routing, exact non-overlapping
filters, hard limits, result aggregation, and exact-owned-tree cleanup.

### Tests first

- [x] T023 [US3] Extend lane boundary guards with RED checks for
  `FullValidation`, `RegressionIntegration`, `LifecycleIntegration`,
  `PreMergeSentinel`, `ProcessIntegration`, and `E2E` classification.
- [x] T024 [US3] Add RED source checks for lane filter mapping, TRX/log output, timeout, and owned-tree termination.

### Implementation

- [x] T025 [US3] Add traits to intentional broad-validation, file-backed
  regression-integration, complete GameEngine lifecycle, exact PreMerge
  lifecycle sentinels, real process-integration, and E2E classes/methods.
- [x] T026 [US3] Create `BookOfEternityClient.TestSupport`, keep it free of
  test packages, create `BookOfEternityClient.IntegrationTests`, and physically
  move every reviewed slow source without reverse project references or split
  partial classes.
- [x] T027 [US3] Implement `scripts/test-csharp.ps1` with explicit project
  routing, lane-specific hard caps including the amended 20-minute PreMerge
  deadline,
  non-overlapping phases, JSON/TRX/log evidence, duplicate detection, and
  exact-owned-tree cleanup.
- [x] T028 [US3] Add and run focused project/source/runner guards, enumerate
  every lane plan, and document the final commands and working rhythm in
  `docs/testing.md`.

---

## Phase 6: Regression Visibility

**Goal**: Prevent gradual reintroduction of the broad guardian multiplier.

- [x] T029 [US4] Prove the guardian source guard fails against a temporary ninth/unapproved broad call or equivalent in-memory fixture.
- [x] T030 [US4] Verify every retained full-pipeline sentinel is categorized and documented.
- [x] T031 [US4] Verify bounded-run output records wall time, result, TRX/log paths, timeout state, and cleanup state.

---

## Phase 7: Final Verification and Integration

- [x] T032 Run focused selection, source/project/runner guards, Guardian domain
  migration batches, retained broad sentinels, lifecycle flake regression, and
  representative existing validation tests.
- [x] T033 Build the production solution, fast project, and integration project
  sequentially with zero warnings and errors.
- [x] T034 Run two consecutive Fast controls below the five-minute hard limit
  and retain separate post-review summaries (`2587/2587` in `2:59.057` and
  `2:28.905`).
- [x] T035 Run LifecycleIntegration once (`186/186` in `5:31.972`), retain the
  unchanged DeepValidation control (`2142/2142` in `14:15.857`), and retain the
  final post-review PreMerge control (`4522/4522` in `12:12.687`). Require
  LifecycleIntegration below ten minutes, DeepValidation and PreMerge below 15
  minutes, floors of 186/1,950/4,490, completed ProcessIntegration and E2E,
  exact ten lifecycle sentinels in PreMerge, no failures or duplicate IDs, and
  complete owned-tree cleanup.
- [x] T036 Re-index Serena to a green health-check, run final acceptance/diff
  checks, fill fresh evidence into all artifacts, and commit exactly the seven
  documentation/spec files.
- [x] T037 Complete independent branch review and resolve every Critical or
  Important finding with fresh bounded evidence.
- [x] T038 Push and merge issue-linked PR #1506 into `main` as
  `de246f917d18b4790d9758b3df41e1e1cb46a19d`; #1505 closed from that merge
  after its acceptance criteria passed.

---

## Phase 8: Phase 45 PreMerge Capacity Amendment

- [x] T039 Retain and analyze the exact clean-checkout PreMerge capacity
  failure: `4606/4606` available cases green, zero duplicates and cleanup debt,
  but the 15-minute deadline expired before the final 15 E2E cases.
- [x] T040 Add RED/GREEN boundary coverage for `RegressionIntegrationOnly`,
  the exact ten-method spiritual-conflict sentinel manifest, the 4,240-result
  floor, and PreMerge duration-aware long-first scheduling.
- [x] T041 Keep all 358 `AfterlifeSpiritualConflictValidationTests` cases in
  RegressionIntegration, admit only the exact ten reviewed sentinels to
  routine PreMerge, and verify discovery plus the ten-case executable sample.
- [x] T042 Synchronize the runner contract and agent guidance, then run one
  final exact clean-checkout PreMerge below 20 minutes with at least 4,240
  non-duplicate results, completed ProcessIntegration/E2E, and complete owned
  cleanup. Satisfied by the accepted #1526/T047 evidence.
- [ ] T043 Record final evidence in #1502/T253, complete exact-diff review, and
  integrate the issue-linked Phase 45 PR only after the bounded gate is green.

---

## Phase 9: Issue #1526 Suite-Growth Capacity Correction

- [x] T044 Retain the `15:00.187` capacity failure and compare both browser
  shards against isolated and browser-only concurrent focused controls.
- [x] T045 Reject the Fast-drain-only experiment after its exact timeout, then
  add RED/GREEN executable guards for retained PreMerge class costs and shared
  loaded browser-audit templates.
- [x] T046 Route all browser and console audit rows through prepared templates
  with isolated per-row clones, fresh state/service objects, and one fixture
  disposal hash verification; preserve every filter, case, assertion, phase,
  and concurrency ceiling. Verify all `166/166` rows and both generated shards
  together. Drain a four-descriptor ExplorerWeb startup wave before the
  remaining parallel descriptors without changing either concurrency ceiling
  or any exclusive phase boundary.
- [x] T047 Preserve the green browser/Explorer fixture optimizations, update
  the executable and documented PreMerge hard limit from 15 to the explicitly
  approved 20 minutes, then run one exact PreMerge below 20 minutes with at
  least 4,240 non-duplicate results, completed ProcessIntegration/E2E, and
  complete owned cleanup. Accepted evidence: `4,836/4,836` passed in
  `14:18.302`, ProcessIntegration `490/490`, E2E `15/15`, zero duplicate IDs,
  and complete cleanup in
  `20260813-044749-940-43368-f64c53dc7a7e48f5a30055b05c1b7e95-premerge`.
- [ ] T048 Record #1526 evidence and integrate only after review.

---

## Phase 10: Issue #1547 Measured PreMerge Deadline Correction

- [x] T049 [US3] Retain the exact 20-minute capacity-red run plus isolated
  ProcessIntegration/E2E lower-bound evidence in
  `specs/1505-test-suite-performance/research.md`.
- [x] T050 [US3] Add a RED executable guard for the 30-minute PreMerge default
  in `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`.
- [x] T051 [US3] Raise only the PreMerge/Complete lane-wide deadline to 30
  minutes in `scripts/test-csharp.ps1` and synchronize `docs/testing.md`,
  `specs/1505-test-suite-performance/data-model.md`,
  `specs/1505-test-suite-performance/quickstart.md`, and active implementation
  plans without changing coverage, filters, phases, ordering, or concurrency.
- [x] T052 [US3] Run the focused runner-contract guard and updated PreMerge
  PlanOnly contract through `scripts/test-csharp.ps1`, retaining green evidence.
- [x] T053 [US3] Run one exact 30-minute PreMerge through
  `scripts/test-csharp.ps1`, require completed ProcessIntegration/E2E, zero
  failures/duplicates, timeout false, and complete cleanup, then record the
  result in `docs/testing.md` and `specs/1505-test-suite-performance/quickstart.md`.

---

## Phase 11: Issue #1551 Fast Physical-Boundary Repair

**Goal**: Restore Fast to honest unit/contract feedback with approximately
three-minute operating time while retaining its five-minute hard limit and all
existing test cases and assertions.

**Independent test**: Exact source/category guards pass, every moved source is
discoverable through Focused Integration and its diagnostic lane, Fast PlanOnly
has no duplicate membership, and two Fast controls finish below five minutes.

- [x] T054 [US3] Record the approved #1551 design and amend
  `specs/1505-test-suite-performance/spec.md`, `plan.md`, and `tasks.md` with
  the measured 7,797-case boundary regression and no-gameplay/no-GM-contract
  scope.
- [ ] T055 [US4] Add RED exact relative-path and category expectations for the
  reviewed #1551 sources in
  `BookOfEternityClient.Tests/FastTestBoundaryTests.cs` and
  `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`, then
  run both focused guards while the sources are still in Fast.
- [ ] T056 [US3] Move the canonical wound and browser transport group into
  `BookOfEternityClient.IntegrationTests/` with `RegressionIntegration`:
  `MortalWoundRecoveryTests.cs`,
  `MortalWoundTreatmentCapabilityAuthorityTests.cs`,
  `WebUi/BrowserMortalWorldGenerationFencingTests.cs`, and
  `WebUi/BrowserStorageTransportParityTests.cs`.
- [ ] T057 [US3] Extract every deterministic QTE input/grading assertion into
  fixture-free `BookOfEternityClient.Tests/QteDeterministicLogicTests.cs`, move
  the remaining canonical lifecycle in `QteSceneServiceTests.cs` to
  `BookOfEternityClient.IntegrationTests/` with `RegressionIntegration`, and
  prove every original test method/theory row exists exactly once.
- [ ] T058 [US3] Move `BookOfEternityClient.Tests/GmWorkerLiveSmokeTests.cs` to
  Integration with `ProcessIntegration` and
  `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs` to Integration with
  `E2E`, preserving existing method-level traits and process/host cleanup.
- [ ] T059 [US3] Run focused Fast QTE logic, focused Integration selections for
  the three changed categories, and both exact boundary guards through
  `scripts/test-csharp.ps1`.
- [ ] T060 [US4] Run Fast PlanOnly plus one bounded Fast checkpoint, retain
  counts/timings/duplicate/cleanup evidence, and compare the wall time with the
  four pre-change descriptor measurements.
- [ ] T061 [US3] If T060 exceeds four minutes, move the exact measured
  file-backed second group listed in `plan.md` to Integration with
  `RegressionIntegration` (or `ProcessIntegration` for real-worker sources),
  update both manifests, and rerun focused category plus PlanOnly controls.
- [ ] T062 [US3] Synchronize the final #1551 placement rules and evidence in
  `docs/testing.md`, `specs/1505-test-suite-performance/research.md`,
  `data-model.md`, and `quickstart.md`; record that GM prompts, examples,
  manifests, client UI, and afterlife runtime docs are unchanged because this
  is internal test scheduling only.
- [ ] T063 [US3] Run two representative Fast controls below the unchanged
  five-minute hard limit, preferably around three minutes, with zero duplicate
  IDs and complete owned-process cleanup.
- [ ] T064 [US4] Inspect exact diffs, run `git diff --check`, complete
  independent code review and Spec Kit consistency analysis, and retain the
  branch ready for the wound-materialization continuation without running
  PreMerge until merge is explicitly requested.

## Dependencies and Execution Order

- T005 completes setup.
- T006–T011 are blocking foundation work.
- T012–T019 deliver the measured speedup.
- T020–T022 independently protect production equivalence.
- T023–T028 depend on stable category/profile names and the project split.
- T029–T031 depend on source/project guards and runner implementation.
- T032–T038 are final gates; T034 is exactly two post-review Fast runs and T035
  contains the final post-review PreMerge run plus retained unchanged
  LifecycleIntegration and DeepValidation evidence.
- T039–T043 are the #1502 capacity amendment. They preserve the completed
  #1505 history while updating the current PreMerge contract and final evidence.
- T044–T048 are the #1526 capacity correction. They retain the same selected
  coverage and depend on measured contention/setup evidence plus a green exact
  gate.
- T049–T053 are the #1547 deadline correction. T049 supplies the measured
  lower bound; T050 must be RED before T051; T052 verifies the executable
  contract before T053 runs the exact final gate.
- T054–T064 are the #1551 Fast physical-boundary repair. T055 must be RED before
  T056–T058; T059 proves the moved groups; T060 decides whether the exact
  second group in T061 is required; T062 follows the final placement; T063 is
  exactly two post-review Fast controls; T064 performs review without a
  merge-only PreMerge run.

## Notes

- Tests precede production changes for every behavior boundary.
- No task may claim speedup by removing assertions or silently reducing discovery.
- Run focused controls during implementation, one Fast control at a meaningful
  checkpoint, and one final PreMerge control. Do not serially run all
  diagnostic lanes before PreMerge unless a focused failure requires diagnosis.
- LifecycleIntegration and DeepValidation are conditional and explicit; run
  them only for a relevant boundary change, related diagnosis, or an explicitly
  requested exhaustive control.
- Do not launch an unbounded complete test run. `Complete` is only a temporary
  alias for `PreMerge`.
- Keep `.serena/` local and stage exact repository paths.

## Documented Runner Interface

```powershell
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane LifecycleIntegration
.\scripts\test-csharp.ps1 -Lane DeepValidation
.\scripts\test-csharp.ps1 -Lane PreMerge
```
